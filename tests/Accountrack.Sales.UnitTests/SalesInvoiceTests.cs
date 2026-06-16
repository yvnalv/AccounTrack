using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.Sales.Application.Abstractions;
using Accountrack.Sales.Application.Features;
using Accountrack.Sales.Domain;
using Accountrack.SharedKernel.Results;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Sales.UnitTests;

public class SalesInvoiceTests
{
    private static readonly DateOnly Date = new(2026, 6, 16);
    private static readonly DateOnly Due = new(2026, 7, 16);

    private sealed class DirectUnitOfWork : ICrossModuleUnitOfWork
    {
        public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct) =>
            work(ct);
    }

    private readonly ISalesOrderRepository _orders = Substitute.For<ISalesOrderRepository>();
    private readonly ISalesInvoiceRepository _invoices = Substitute.For<ISalesInvoiceRepository>();
    private readonly IGeneralLedgerPoster _ledger = Substitute.For<IGeneralLedgerPoster>();
    private readonly IPostingAccountResolver _accounts = Substitute.For<IPostingAccountResolver>();
    private readonly ISubledgerPosting _subledger = Substitute.For<ISubledgerPosting>();

    private LedgerPostingRequest? _posted;

    // An approved SO whose single line has been delivered (so it can be invoiced).
    private static SalesOrder DeliveredOrder(decimal qty = 10m, decimal unitPrice = 100m, decimal taxRate = 0.11m, decimal delivered = 10m)
    {
        var so = SalesOrder.CreateDraft("SO/202606/00001", Guid.NewGuid(), Guid.NewGuid(), "IDR", Date, null);
        so.AddLine(Guid.NewGuid(), qty, unitPrice, taxRate, null);
        so.MarkAutoApproved(Guid.NewGuid());
        so.DeliverLine(so.Lines[0].Id, delivered);
        return so;
    }

    private PostSalesInvoiceHandler Handler() =>
        new(_orders, _invoices, new DirectUnitOfWork(), _ledger, _accounts, _subledger);

    private void StubAccountsAndPosting()
    {
        _accounts.ResolveAsync("SalesInvoice", Arg.Any<string>(), Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(_ => Result.Success(Guid.NewGuid()));
        _ledger.PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci => { _posted = ci.Arg<LedgerPostingRequest>(); return Result.Success(Guid.NewGuid()); });
        _subledger.OpenReceivableAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(),
                Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Guid.NewGuid()));
    }

    // --- Domain ---

    [Fact]
    public void A_line_can_be_invoiced_only_up_to_what_has_been_delivered()
    {
        var so = DeliveredOrder(qty: 10m, delivered: 4m);
        so.InvoiceLine(so.Lines[0].Id, 4m);
        so.Lines[0].InvoicedQuantity.Should().Be(4m);
        so.Lines[0].UninvoicedDeliveredQuantity.Should().Be(0m);

        var act = () => so.InvoiceLine(so.Lines[0].Id, 1m);
        act.Should().Throw<InvalidOperationException>().WithMessage("*exceeds*");
    }

    // --- Handler orchestration ---

    [Fact]
    public async Task Posting_an_invoice_books_ar_revenue_and_vat_and_opens_an_ar_item()
    {
        var so = DeliveredOrder(qty: 10m, unitPrice: 100m, taxRate: 0.11m, delivered: 10m);
        var lineId = so.Lines[0].Id;
        _orders.GetByIdAsync(so.Id, Arg.Any<CancellationToken>()).Returns(so);
        StubAccountsAndPosting();

        SalesInvoice? captured = null;
        _invoices.When(r => r.Add(Arg.Any<SalesInvoice>())).Do(ci => captured = ci.Arg<SalesInvoice>());

        var result = await Handler().Handle(
            new PostSalesInvoiceCommand(so.Id, Date, Due, null, new[] { new SalesInvoiceLineInput(lineId, 10m) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured!.SubTotal.Should().Be(1000m);
        captured.TaxTotal.Should().Be(110m);
        captured.GrandTotal.Should().Be(1110m);
        so.Lines[0].InvoicedQuantity.Should().Be(10m);

        // Dr AR 1110 (carries customer) / Cr Revenue 1000 + Cr VAT 110, balanced.
        _posted!.Lines.Sum(l => l.Debit).Should().Be(1110m);
        _posted.Lines.Sum(l => l.Credit).Should().Be(1110m);
        _posted.Lines.Should().Contain(l => l.Debit == 1110m && l.SubledgerPartyId == so.CustomerId);
        _posted.Source.Should().Be(LedgerSource.SalesInvoice);

        await _subledger.Received(1).OpenReceivableAsync(
            so.CustomerId, Arg.Any<Guid>(), Arg.Any<string>(), Date, Due, 1110m, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invoicing_more_than_delivered_is_rejected_before_posting()
    {
        var so = DeliveredOrder(qty: 10m, delivered: 3m);
        var lineId = so.Lines[0].Id;
        _orders.GetByIdAsync(so.Id, Arg.Any<CancellationToken>()).Returns(so);

        var result = await Handler().Handle(
            new PostSalesInvoiceCommand(so.Id, Date, Due, null, new[] { new SalesInvoiceLineInput(lineId, 5m) }),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("SALES.OVER_INVOICE");
        await _ledger.DidNotReceive().PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>());
        await _subledger.DidNotReceive().OpenReceivableAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(),
            Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_zero_tax_invoice_omits_the_vat_line()
    {
        var so = DeliveredOrder(qty: 5m, unitPrice: 200m, taxRate: 0m, delivered: 5m);
        var lineId = so.Lines[0].Id;
        _orders.GetByIdAsync(so.Id, Arg.Any<CancellationToken>()).Returns(so);
        StubAccountsAndPosting();

        var result = await Handler().Handle(
            new PostSalesInvoiceCommand(so.Id, Date, Due, null, new[] { new SalesInvoiceLineInput(lineId, 5m) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _posted!.Lines.Should().HaveCount(2); // Dr AR + Cr Revenue only
        _posted.Lines.Sum(l => l.Debit).Should().Be(1000m);
        _posted.Lines.Sum(l => l.Credit).Should().Be(1000m);
    }
}
