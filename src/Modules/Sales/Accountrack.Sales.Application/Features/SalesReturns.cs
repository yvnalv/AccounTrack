using Accountrack.Application.Abstractions.Idempotency;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Inventory;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.Sales.Application.Abstractions;
using Accountrack.Sales.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Sales.Application.Features;

public sealed record SalesReturnLineInput(Guid SalesInvoiceLineId, decimal Quantity);

/// <summary>
/// Returns goods against a posted sales invoice and credits the customer (BR-SAL-8). Atomically
/// (INTEGRATION_EVENTS.md §2): restocks each line at its original delivered cost (Dr Inventory /
/// Cr COGS), reverses billing (Dr Revenue + Dr VAT Output / Cr AR control), reduces the invoice's AR
/// open item, and records the credit note — all in one transaction. When the invoice is already paid
/// (in full or part) the credit exceeds the outstanding receivable; the excess is refunded from
/// <see cref="RefundCashAccountId"/> (Cr Cash-Bank) instead of AR.
/// </summary>
public sealed record PostSalesReturnCommand(
    Guid SalesInvoiceId, DateOnly ReturnDate, string? Notes, IReadOnlyList<SalesReturnLineInput> Lines,
    Guid? RefundCashAccountId = null)
    : ICommand<Guid>, IIdempotentCommand;

public sealed class PostSalesReturnValidator : AbstractValidator<PostSalesReturnCommand>
{
    public PostSalesReturnValidator()
    {
        RuleFor(x => x.SalesInvoiceId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("A sales return requires at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.SalesInvoiceLineId).NotEmpty();
            l.RuleFor(x => x.Quantity).GreaterThan(0);
        });
    }
}

public sealed class PostSalesReturnHandler : ICommandHandler<PostSalesReturnCommand, Guid>
{
    private readonly ISalesInvoiceRepository _invoices;
    private readonly ISalesOrderRepository _orders;
    private readonly IDeliveryOrderRepository _deliveries;
    private readonly ISalesReturnRepository _returns;
    private readonly ICrossModuleUnitOfWork _uow;
    private readonly IInventoryPosting _inventory;
    private readonly IGeneralLedgerPoster _ledger;
    private readonly IPostingAccountResolver _accounts;
    private readonly ISubledgerPosting _subledger;

    public PostSalesReturnHandler(
        ISalesInvoiceRepository invoices,
        ISalesOrderRepository orders,
        IDeliveryOrderRepository deliveries,
        ISalesReturnRepository returns,
        ICrossModuleUnitOfWork uow,
        IInventoryPosting inventory,
        IGeneralLedgerPoster ledger,
        IPostingAccountResolver accounts,
        ISubledgerPosting subledger)
    {
        _invoices = invoices;
        _orders = orders;
        _deliveries = deliveries;
        _returns = returns;
        _uow = uow;
        _inventory = inventory;
        _ledger = ledger;
        _accounts = accounts;
        _subledger = subledger;
    }

    public Task<Result<Guid>> Handle(PostSalesReturnCommand request, CancellationToken ct) =>
        _uow.ExecuteAsync(token => PostAsync(request, token), ct);

    private async Task<Result<Guid>> PostAsync(PostSalesReturnCommand request, CancellationToken ct)
    {
        var invoice = await _invoices.GetByIdAsync(request.SalesInvoiceId, ct);
        if (invoice is null)
        {
            return SalesErrors.ReturnInvoiceNotFound;
        }

        if (invoice.JournalEntryId is null || invoice.ArOpenItemId is null)
        {
            return SalesErrors.InvoiceNotPosted;
        }

        var order = await _orders.GetByIdAsync(invoice.SalesOrderId, ct);
        if (order is null)
        {
            return SalesErrors.NotFound;
        }

        // Original delivered unit cost per sales-order line (weighted average across deliveries),
        // so restocking reverses COGS at the cost the goods left at (BR-SAL-8).
        var deliveries = await _deliveries.ListBySalesOrderAsync(order.Id, ct);
        var costByOrderLine = deliveries
            .SelectMany(d => d.Lines)
            .GroupBy(l => l.SalesOrderLineId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(l => l.Quantity) == 0 ? 0m : g.Sum(l => l.LineCost) / g.Sum(l => l.Quantity));

        var sequence = await _returns.GetSequenceAsync(ct);
        if (sequence is null)
        {
            sequence = new SalesReturnNumberSequence();
            _returns.AddSequence(sequence);
        }

        var number = sequence.Take(request.ReturnDate);
        var salesReturn = SalesReturn.Create(
            number, invoice.Id, order.Id, invoice.CustomerId, order.WarehouseId, invoice.Currency,
            request.ReturnDate, request.Notes);

        decimal totalCost = 0m;
        foreach (var input in request.Lines)
        {
            var line = invoice.Lines.FirstOrDefault(l => l.Id == input.SalesInvoiceLineId);
            if (line is null)
            {
                return SalesErrors.SalesInvoiceLineNotFound(input.SalesInvoiceLineId);
            }

            if (input.Quantity > line.ReturnableQuantity)
            {
                return SalesErrors.OverReturn(line.ReturnableQuantity, input.Quantity);
            }

            var unitCost = costByOrderLine.GetValueOrDefault(line.SalesOrderLineId, 0m);

            // Restock at the original cost; the cost received becomes the COGS reversal.
            var movement = await _inventory.ReceiveAsync(
                line.ProductId, order.WarehouseId, invoice.Currency, input.Quantity, unitCost,
                request.ReturnDate, salesReturn.Id, $"Sales return {number}", ct);

            if (movement.IsFailure)
            {
                return movement.Error;
            }

            salesReturn.AddLine(line.Id, line.SalesOrderLineId, line.ProductId, input.Quantity, line.UnitPrice, line.TaxRate, unitCost);
            invoice.ReturnLine(line.Id, input.Quantity);
            totalCost += movement.Value.CostApplied;
        }

        // Resolve accounts (configuration, not hardcoded).
        var arControl = await _accounts.ResolveAsync("SalesReturn", PostingKeys.ArControl, PostingSelector.None, ct);
        if (arControl.IsFailure)
        {
            return arControl.Error;
        }

        var revenue = await _accounts.ResolveAsync("SalesReturn", PostingKeys.Revenue, PostingSelector.None, ct);
        if (revenue.IsFailure)
        {
            return revenue.Error;
        }

        var cogs = await _accounts.ResolveAsync("SalesReturn", PostingKeys.Cogs, PostingSelector.None, ct);
        if (cogs.IsFailure)
        {
            return cogs.Error;
        }

        var inventoryAccount = await _accounts.ResolveAsync(
            "SalesReturn", PostingKeys.Inventory, new PostingSelector(WarehouseId: order.WarehouseId), ct);
        if (inventoryAccount.IsFailure)
        {
            return inventoryAccount.Error;
        }

        // Split the credit: apply up to the invoice's still-outstanding receivable, refund the rest.
        var outstanding = await _subledger.GetOutstandingAsync(invoice.ArOpenItemId.Value, ct);
        if (outstanding.IsFailure)
        {
            return outstanding.Error;
        }

        var applyToAr = Math.Min(salesReturn.GrandTotal, outstanding.Value);
        var refund = salesReturn.GrandTotal - applyToAr;
        if (refund > 0m && request.RefundCashAccountId is null)
        {
            return SalesErrors.RefundAccountRequired;
        }

        // Credit note (reverse billing) + restock (reverse COGS), one balanced journal:
        //   Dr Revenue (net) + Dr VAT Output (tax) / Cr AR control (applied) + Cr Cash-Bank (refund)
        //   Dr Inventory (cost)                     / Cr COGS (cost)
        var lines = new List<LedgerLine>
        {
            new(revenue.Value, salesReturn.SubTotal, 0m, "Sales revenue (return)"),
        };

        if (applyToAr > 0m)
        {
            lines.Add(new LedgerLine(arControl.Value, 0m, applyToAr, "Accounts receivable (credit note)", invoice.CustomerId));
        }

        if (refund > 0m)
        {
            lines.Add(new LedgerLine(request.RefundCashAccountId!.Value, 0m, refund, "Customer refund"));
        }

        if (salesReturn.TaxTotal > 0m)
        {
            var vatOutput = await _accounts.ResolveAsync("SalesReturn", PostingKeys.VatOutput, PostingSelector.None, ct);
            if (vatOutput.IsFailure)
            {
                return vatOutput.Error;
            }

            lines.Add(new LedgerLine(vatOutput.Value, salesReturn.TaxTotal, 0m, "VAT output reversal (PPN Keluaran)"));
        }

        if (totalCost > 0m)
        {
            lines.Add(new LedgerLine(inventoryAccount.Value, totalCost, 0m, "Inventory returned"));
            lines.Add(new LedgerLine(cogs.Value, 0m, totalCost, "Cost of goods sold reversal"));
        }

        var posting = new LedgerPostingRequest(
            request.ReturnDate, LedgerSource.SalesReturn, salesReturn.Id,
            $"Sales return {number} for invoice {invoice.Number}", lines);

        var journal = await _ledger.PostAsync(posting, ct);
        if (journal.IsFailure)
        {
            return journal.Error;
        }

        // Reduce the invoice's receivable by the applied credit (keeps AR subledger in step with GL).
        if (applyToAr > 0m)
        {
            var allocation = await _subledger.AllocateAsync(
                invoice.ArOpenItemId.Value, number, request.ReturnDate, applyToAr, salesReturn.Id, ct);
            if (allocation.IsFailure)
            {
                return allocation.Error;
            }
        }

        salesReturn.SetJournal(journal.Value);
        _returns.Add(salesReturn);

        return salesReturn.Id;
    }
}
