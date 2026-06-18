using Accountrack.Application.Abstractions.Idempotency;
using Accountrack.Application.Abstractions.Messaging;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.Sales.Application.Abstractions;
using Accountrack.Sales.Domain;
using Accountrack.SharedKernel.Results;
using FluentValidation;

namespace Accountrack.Sales.Application.Features;

public sealed record SalesInvoiceLineInput(Guid SalesOrderLineId, decimal Quantity);

/// <summary>
/// Bills a customer for goods delivered against a sales order. Atomically posts
/// Dr AR control / Cr Revenue + Cr VAT Output (accounts resolved by posting rules), opens an AR
/// subledger item, advances the SO's invoiced quantities, and records the invoice.
/// </summary>
public sealed record PostSalesInvoiceCommand(
    Guid SalesOrderId, DateOnly InvoiceDate, DateOnly DueDate, string? Notes,
    IReadOnlyList<SalesInvoiceLineInput> Lines) : ICommand<Guid>, IIdempotentCommand;

public sealed class PostSalesInvoiceValidator : AbstractValidator<PostSalesInvoiceCommand>
{
    public PostSalesInvoiceValidator()
    {
        RuleFor(x => x.SalesOrderId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("A sales invoice requires at least one line.");
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.InvoiceDate)
            .WithMessage("Due date cannot be before the invoice date.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.SalesOrderLineId).NotEmpty();
            l.RuleFor(x => x.Quantity).GreaterThan(0);
        });
    }
}

public sealed class PostSalesInvoiceHandler : ICommandHandler<PostSalesInvoiceCommand, Guid>
{
    private readonly ISalesOrderRepository _orders;
    private readonly ISalesInvoiceRepository _invoices;
    private readonly ICrossModuleUnitOfWork _uow;
    private readonly IGeneralLedgerPoster _ledger;
    private readonly IPostingAccountResolver _accounts;
    private readonly ISubledgerPosting _subledger;

    public PostSalesInvoiceHandler(
        ISalesOrderRepository orders,
        ISalesInvoiceRepository invoices,
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

    public Task<Result<Guid>> Handle(PostSalesInvoiceCommand request, CancellationToken ct) =>
        _uow.ExecuteAsync(token => PostAsync(request, token), ct);

    private async Task<Result<Guid>> PostAsync(PostSalesInvoiceCommand request, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(request.SalesOrderId, ct);
        if (order is null)
        {
            return SalesErrors.NotFound;
        }

        var sequence = await _invoices.GetSequenceAsync(ct);
        if (sequence is null)
        {
            sequence = new SalesInvoiceNumberSequence();
            _invoices.AddSequence(sequence);
        }

        var number = sequence.Take(request.InvoiceDate);
        var invoice = SalesInvoice.Create(
            number, order.Id, order.CustomerId, order.Currency, request.InvoiceDate, request.DueDate, request.Notes);

        foreach (var input in request.Lines)
        {
            var line = order.Lines.FirstOrDefault(l => l.Id == input.SalesOrderLineId);
            if (line is null)
            {
                return SalesErrors.SalesOrderLineNotFound(input.SalesOrderLineId);
            }

            if (input.Quantity > line.UninvoicedDeliveredQuantity)
            {
                return SalesErrors.OverInvoice(line.UninvoicedDeliveredQuantity, input.Quantity);
            }

            invoice.AddLine(line.Id, line.ProductId, input.Quantity, line.UnitPrice, line.TaxRate);
            order.InvoiceLine(line.Id, input.Quantity);
        }

        // Resolve accounts (configuration, not hardcoded).
        var arControl = await _accounts.ResolveAsync("SalesInvoice", PostingKeys.ArControl, PostingSelector.None, ct);
        if (arControl.IsFailure)
        {
            return arControl.Error;
        }

        var revenue = await _accounts.ResolveAsync("SalesInvoice", PostingKeys.Revenue, PostingSelector.None, ct);
        if (revenue.IsFailure)
        {
            return revenue.Error;
        }

        // Dr AR (gross, carries customer) / Cr Revenue (net) + Cr VAT Output (tax).
        var lines = new List<LedgerLine>
        {
            new(arControl.Value, invoice.GrandTotal, 0m, "Accounts receivable", order.CustomerId),
            new(revenue.Value, 0m, invoice.SubTotal, "Sales revenue"),
        };

        if (invoice.TaxTotal > 0m)
        {
            var vatOutput = await _accounts.ResolveAsync("SalesInvoice", PostingKeys.VatOutput, PostingSelector.None, ct);
            if (vatOutput.IsFailure)
            {
                return vatOutput.Error;
            }

            lines.Add(new LedgerLine(vatOutput.Value, 0m, invoice.TaxTotal, "VAT output (PPN Keluaran)"));
        }

        var posting = new LedgerPostingRequest(
            request.InvoiceDate, LedgerSource.SalesInvoice, invoice.Id,
            $"Sales invoice {number} for SO {order.Number}", lines);

        var journal = await _ledger.PostAsync(posting, ct);
        if (journal.IsFailure)
        {
            return journal.Error;
        }

        var openItem = await _subledger.OpenReceivableAsync(
            order.CustomerId, invoice.Id, number, request.InvoiceDate, request.DueDate, invoice.GrandTotal, ct);
        if (openItem.IsFailure)
        {
            return openItem.Error;
        }

        invoice.SetPosting(journal.Value, openItem.Value);
        _invoices.Add(invoice);

        return invoice.Id;
    }
}
