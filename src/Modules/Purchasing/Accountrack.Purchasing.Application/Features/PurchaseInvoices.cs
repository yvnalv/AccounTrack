using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.Purchasing.Application.Abstractions;
using Accountrack.Purchasing.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Purchasing.Application.Features;

public sealed record PurchaseInvoiceLineInput(Guid PurchaseOrderLineId, decimal Quantity);

/// <summary>
/// Bills a supplier for goods received against a purchase order. Atomically posts
/// Dr GR/IR + Dr VAT Input / Cr AP control (accounts resolved by posting rules), opens an AP
/// subledger item, advances the PO's invoiced quantities, and records the invoice.
/// </summary>
public sealed record PostPurchaseInvoiceCommand(
    Guid PurchaseOrderId, string? SupplierInvoiceNo, DateOnly InvoiceDate, DateOnly DueDate, string? Notes,
    IReadOnlyList<PurchaseInvoiceLineInput> Lines) : ICommand<Guid>;

public sealed class PostPurchaseInvoiceValidator : AbstractValidator<PostPurchaseInvoiceCommand>
{
    public PostPurchaseInvoiceValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("A purchase invoice requires at least one line.");
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.InvoiceDate)
            .WithMessage("Due date cannot be before the invoice date.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.PurchaseOrderLineId).NotEmpty();
            l.RuleFor(x => x.Quantity).GreaterThan(0);
        });
    }
}

public sealed class PostPurchaseInvoiceHandler : ICommandHandler<PostPurchaseInvoiceCommand, Guid>
{
    private readonly IPurchaseOrderRepository _orders;
    private readonly IPurchaseInvoiceRepository _invoices;
    private readonly ICrossModuleUnitOfWork _uow;
    private readonly IGeneralLedgerPoster _ledger;
    private readonly IPostingAccountResolver _accounts;
    private readonly ISubledgerPosting _subledger;

    public PostPurchaseInvoiceHandler(
        IPurchaseOrderRepository orders,
        IPurchaseInvoiceRepository invoices,
        ICrossModuleUnitOfWork uow,
        IGeneralLedgerPoster ledger,
        IPostingAccountResolver accounts,
        ISubledgerPosting subledger)
    {
        _orders = orders;
        _invoices = invoices;
        _uow = uow;
        _ledger = ledger;
        _accounts = accounts;
        _subledger = subledger;
    }

    public Task<Result<Guid>> Handle(PostPurchaseInvoiceCommand request, CancellationToken ct) =>
        _uow.ExecuteAsync(token => PostAsync(request, token), ct);

    private async Task<Result<Guid>> PostAsync(PostPurchaseInvoiceCommand request, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(request.PurchaseOrderId, ct);
        if (order is null)
        {
            return PurchasingErrors.NotFound;
        }

        var sequence = await _invoices.GetSequenceAsync(ct);
        if (sequence is null)
        {
            sequence = new PurchaseInvoiceNumberSequence();
            _invoices.AddSequence(sequence);
        }

        var number = sequence.Take(request.InvoiceDate);
        var invoice = PurchaseInvoice.Create(
            number, request.SupplierInvoiceNo, order.Id, order.SupplierId, order.Currency,
            request.InvoiceDate, request.DueDate, request.Notes);

        foreach (var input in request.Lines)
        {
            var line = order.Lines.FirstOrDefault(l => l.Id == input.PurchaseOrderLineId);
            if (line is null)
            {
                return PurchasingErrors.PurchaseOrderLineNotFound(input.PurchaseOrderLineId);
            }

            if (input.Quantity > line.UninvoicedReceivedQuantity)
            {
                return PurchasingErrors.OverInvoice(line.UninvoicedReceivedQuantity, input.Quantity);
            }

            invoice.AddLine(line.Id, line.ProductId, input.Quantity, line.UnitPrice, line.TaxRate);
            order.InvoiceLine(line.Id, input.Quantity);
        }

        // Resolve accounts (configuration, not hardcoded).
        var grIr = await _accounts.ResolveAsync("PurchaseInvoice", PostingKeys.GrIrClearing, PostingSelector.None, ct);
        if (grIr.IsFailure)
        {
            return grIr.Error;
        }

        var apControl = await _accounts.ResolveAsync("PurchaseInvoice", PostingKeys.ApControl, PostingSelector.None, ct);
        if (apControl.IsFailure)
        {
            return apControl.Error;
        }

        // Dr GR/IR (net, clears the receipt accrual) + Dr VAT Input (tax) / Cr AP control (gross).
        var lines = new List<LedgerLine>
        {
            new(grIr.Value, invoice.SubTotal, 0m, "Clear GR/IR"),
        };

        if (invoice.TaxTotal > 0m)
        {
            var vatInput = await _accounts.ResolveAsync("PurchaseInvoice", PostingKeys.VatInput, PostingSelector.None, ct);
            if (vatInput.IsFailure)
            {
                return vatInput.Error;
            }

            lines.Add(new LedgerLine(vatInput.Value, invoice.TaxTotal, 0m, "VAT input (PPN Masukan)"));
        }

        lines.Add(new LedgerLine(apControl.Value, 0m, invoice.GrandTotal, "Accounts payable", order.SupplierId));

        var posting = new LedgerPostingRequest(
            request.InvoiceDate, LedgerSource.PurchaseInvoice, invoice.Id,
            $"Purchase invoice {number} for PO {order.Number}", lines);

        var journal = await _ledger.PostAsync(posting, ct);
        if (journal.IsFailure)
        {
            return journal.Error;
        }

        var openItem = await _subledger.OpenPayableAsync(
            order.SupplierId, invoice.Id, number, request.InvoiceDate, request.DueDate, invoice.GrandTotal, ct);
        if (openItem.IsFailure)
        {
            return openItem.Error;
        }

        invoice.SetPosting(journal.Value, openItem.Value);
        _invoices.Add(invoice);

        return invoice.Id;
    }
}
