using Accountrack.Application.Abstractions.Idempotency;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Inventory;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.Purchasing.Application.Abstractions;
using Accountrack.Purchasing.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Purchasing.Application.Features;

public sealed record PurchaseReturnLineInput(Guid PurchaseInvoiceLineId, decimal Quantity);

/// <summary>
/// Returns goods to a supplier against a posted purchase invoice and raises a debit note (BR-PUR-7).
/// Atomically (INTEGRATION_EVENTS.md §2): issues each line out of stock at moving-average cost
/// (Cr Inventory), reverses billing (Dr AP control / Cr VAT Input), books any cost-vs-price variance,
/// reduces the invoice's AP open item, and records the debit note — all in one transaction. When the
/// invoice is already paid (in full or part) the debit exceeds the outstanding payable; the excess is
/// recovered into <see cref="RefundCashAccountId"/> (Dr Cash-Bank — a refund from the supplier).
/// </summary>
public sealed record PostPurchaseReturnCommand(
    Guid PurchaseInvoiceId, DateOnly ReturnDate, string? Notes, IReadOnlyList<PurchaseReturnLineInput> Lines,
    Guid? RefundCashAccountId = null)
    : ICommand<Guid>, IIdempotentCommand;

public sealed class PostPurchaseReturnValidator : AbstractValidator<PostPurchaseReturnCommand>
{
    public PostPurchaseReturnValidator()
    {
        RuleFor(x => x.PurchaseInvoiceId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("A purchase return requires at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.PurchaseInvoiceLineId).NotEmpty();
            l.RuleFor(x => x.Quantity).GreaterThan(0);
        });
    }
}

public sealed class PostPurchaseReturnHandler : ICommandHandler<PostPurchaseReturnCommand, Guid>
{
    private readonly IPurchaseInvoiceRepository _invoices;
    private readonly IPurchaseOrderRepository _orders;
    private readonly IPurchaseReturnRepository _returns;
    private readonly ICrossModuleUnitOfWork _uow;
    private readonly IInventoryPosting _inventory;
    private readonly IGeneralLedgerPoster _ledger;
    private readonly IPostingAccountResolver _accounts;
    private readonly ISubledgerPosting _subledger;

    public PostPurchaseReturnHandler(
        IPurchaseInvoiceRepository invoices,
        IPurchaseOrderRepository orders,
        IPurchaseReturnRepository returns,
        ICrossModuleUnitOfWork uow,
        IInventoryPosting inventory,
        IGeneralLedgerPoster ledger,
        IPostingAccountResolver accounts,
        ISubledgerPosting subledger)
    {
        _invoices = invoices;
        _orders = orders;
        _returns = returns;
        _uow = uow;
        _inventory = inventory;
        _ledger = ledger;
        _accounts = accounts;
        _subledger = subledger;
    }

    public Task<Result<Guid>> Handle(PostPurchaseReturnCommand request, CancellationToken ct) =>
        _uow.ExecuteAsync(token => PostAsync(request, token), ct);

    private async Task<Result<Guid>> PostAsync(PostPurchaseReturnCommand request, CancellationToken ct)
    {
        var invoice = await _invoices.GetByIdAsync(request.PurchaseInvoiceId, ct);
        if (invoice is null)
        {
            return PurchasingErrors.ReturnInvoiceNotFound;
        }

        if (invoice.JournalEntryId is null || invoice.ApOpenItemId is null)
        {
            return PurchasingErrors.InvoiceNotPosted;
        }

        var order = await _orders.GetByIdAsync(invoice.PurchaseOrderId, ct);
        if (order is null)
        {
            return PurchasingErrors.NotFound;
        }

        var sequence = await _returns.GetSequenceAsync(ct);
        if (sequence is null)
        {
            sequence = new PurchaseReturnNumberSequence();
            _returns.AddSequence(sequence);
        }

        var number = sequence.Take(request.ReturnDate);
        var purchaseReturn = PurchaseReturn.Create(
            number, invoice.Id, order.Id, invoice.SupplierId, order.WarehouseId, invoice.Currency,
            request.ReturnDate, request.Notes);

        decimal totalCost = 0m;
        foreach (var input in request.Lines)
        {
            var line = invoice.Lines.FirstOrDefault(l => l.Id == input.PurchaseInvoiceLineId);
            if (line is null)
            {
                return PurchasingErrors.PurchaseInvoiceLineNotFound(input.PurchaseInvoiceLineId);
            }

            if (input.Quantity > line.ReturnableQuantity)
            {
                return PurchasingErrors.OverReturn(line.ReturnableQuantity, input.Quantity);
            }

            // Remove goods from stock at moving-average cost; the cost applied is the GL Cr Inventory.
            var movement = await _inventory.IssueAsync(
                line.ProductId, order.WarehouseId, input.Quantity, request.ReturnDate, purchaseReturn.Id,
                $"Purchase return {number}", ct);

            if (movement.IsFailure)
            {
                return movement.Error;
            }

            var unitCost = input.Quantity == 0 ? 0m : movement.Value.CostApplied / input.Quantity;
            purchaseReturn.AddLine(line.Id, line.PurchaseOrderLineId, line.ProductId, input.Quantity, line.UnitPrice, line.TaxRate, unitCost);
            invoice.ReturnLine(line.Id, input.Quantity);
            totalCost += movement.Value.CostApplied;
        }

        // Resolve accounts (configuration, not hardcoded).
        var apControl = await _accounts.ResolveAsync("PurchaseReturn", PostingKeys.ApControl, PostingSelector.None, ct);
        if (apControl.IsFailure)
        {
            return apControl.Error;
        }

        var inventoryAccount = await _accounts.ResolveAsync(
            "PurchaseReturn", PostingKeys.Inventory, new PostingSelector(WarehouseId: order.WarehouseId), ct);
        if (inventoryAccount.IsFailure)
        {
            return inventoryAccount.Error;
        }

        // Split the debit: apply up to the invoice's still-outstanding payable, recover the rest as cash.
        var outstanding = await _subledger.GetOutstandingAsync(invoice.ApOpenItemId.Value, ct);
        if (outstanding.IsFailure)
        {
            return outstanding.Error;
        }

        var applyToAp = Math.Min(purchaseReturn.GrandTotal, outstanding.Value);
        var refund = purchaseReturn.GrandTotal - applyToAp;
        if (refund > 0m && request.RefundCashAccountId is null)
        {
            return PurchasingErrors.RefundAccountRequired;
        }

        // Debit note (reverse billing) + de-stock:
        //   Dr AP control (applied) + Dr Cash-Bank (refund) / Cr VAT Input (tax) + Cr Inventory (cost) + Cr/Dr variance
        var lines = new List<LedgerLine>();

        if (applyToAp > 0m)
        {
            lines.Add(new LedgerLine(apControl.Value, applyToAp, 0m, "Accounts payable (debit note)", invoice.SupplierId));
        }

        if (refund > 0m)
        {
            lines.Add(new LedgerLine(request.RefundCashAccountId!.Value, refund, 0m, "Supplier refund"));
        }

        if (purchaseReturn.TaxTotal > 0m)
        {
            var vatInput = await _accounts.ResolveAsync("PurchaseReturn", PostingKeys.VatInput, PostingSelector.None, ct);
            if (vatInput.IsFailure)
            {
                return vatInput.Error;
            }

            lines.Add(new LedgerLine(vatInput.Value, 0m, purchaseReturn.TaxTotal, "VAT input reversal (PPN Masukan)"));
        }

        if (totalCost > 0m)
        {
            lines.Add(new LedgerLine(inventoryAccount.Value, 0m, totalCost, "Inventory returned to supplier"));
        }

        // Cost-vs-price variance: the goods value credited to AP (net) may differ from the moving-average
        // cost removed from stock. Post the difference to the inventory-variance account.
        var variance = Math.Round(purchaseReturn.SubTotal - totalCost, 4, MidpointRounding.ToEven);
        if (variance != 0m)
        {
            var varianceAccount = await _accounts.ResolveAsync(
                "PurchaseReturn", PostingKeys.InventoryVariance, PostingSelector.None, ct);
            if (varianceAccount.IsFailure)
            {
                return varianceAccount.Error;
            }

            lines.Add(variance > 0m
                ? new LedgerLine(varianceAccount.Value, 0m, variance, "Purchase price variance")
                : new LedgerLine(varianceAccount.Value, -variance, 0m, "Purchase price variance"));
        }

        var posting = new LedgerPostingRequest(
            request.ReturnDate, LedgerSource.PurchaseReturn, purchaseReturn.Id,
            $"Purchase return {number} for invoice {invoice.Number}", lines);

        var journal = await _ledger.PostAsync(posting, ct);
        if (journal.IsFailure)
        {
            return journal.Error;
        }

        // Reduce the invoice's payable by the applied debit (keeps AP subledger in step with GL).
        if (applyToAp > 0m)
        {
            var allocation = await _subledger.AllocateAsync(
                invoice.ApOpenItemId.Value, number, request.ReturnDate, applyToAp, purchaseReturn.Id, ct);
            if (allocation.IsFailure)
            {
                return allocation.Error;
            }
        }

        purchaseReturn.SetJournal(journal.Value);
        _returns.Add(purchaseReturn);

        return purchaseReturn.Id;
    }
}
