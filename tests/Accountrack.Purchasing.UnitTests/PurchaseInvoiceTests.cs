using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.Purchasing.Application.Abstractions;
using Accountrack.Purchasing.Application.Features;
using Accountrack.Purchasing.Domain;
using Accountrack.SharedKernel.Results;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Purchasing.UnitTests;

public class PurchaseInvoiceTests
{
    private static readonly DateOnly Date = new(2026, 6, 14);
    private static readonly DateOnly Due = new(2026, 7, 14);

    private sealed class DirectUnitOfWork : ICrossModuleUnitOfWork
    {
        public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct) =>
            work(ct);
    }

    private readonly IPurchaseOrderRepository _orders = Substitute.For<IPurchaseOrderRepository>();
    private readonly IPurchaseInvoiceRepository _invoices = Substitute.For<IPurchaseInvoiceRepository>();
    private readonly IGeneralLedgerPoster _ledger = Substitute.For<IGeneralLedgerPoster>();
    private readonly IPostingAccountResolver _accounts = Substitute.For<IPostingAccountResolver>();
    private readonly ISubledgerPosting _subledger = Substitute.For<ISubledgerPosting>();

    private LedgerPostingRequest? _posted;

    // A PO whose single line has been received (so it can be invoiced).
    private static PurchaseOrder ReceivedOrder(decimal qty = 10m, decimal unitPrice = 100m, decimal taxRate = 0.11m, decimal received = 10m)
    {
        var po = PurchaseOrder.CreateDraft("PO/202606/00001", Guid.NewGuid(), Guid.NewGuid(), "IDR", Date, null);
        po.AddLine(Guid.NewGuid(), qty, unitPrice, taxRate, null);
        po.MarkAutoApproved(Guid.NewGuid());
        po.ReceiveLine(po.Lines[0].Id, received);
        return po;
    }

    private PostPurchaseInvoiceHandler Handler() =>
        new(_orders, _invoices, new DirectUnitOfWork(), _ledger, _accounts, _subledger);

    // Stubs accounts + AP open item, and captures the posted journal into _posted for assertions.
    private void StubAccountsAndPosting()
    {
        _accounts.ResolveAsync("PurchaseInvoice", Arg.Any<string>(), Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(_ => Result.Success(Guid.NewGuid()));
        _ledger.PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci => { _posted = ci.Arg<LedgerPostingRequest>(); return Result.Success(Guid.NewGuid()); });
        _subledger.OpenPayableAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(),
                Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Guid.NewGuid()));
    }

    // --- Domain ---

    [Fact]
    public void A_line_can_be_invoiced_only_up_to_what_has_been_received()
    {
        var po = ReceivedOrder(qty: 10m, received: 4m);
        po.InvoiceLine(po.Lines[0].Id, 4m);

        po.Lines[0].InvoicedQuantity.Should().Be(4m);
        po.Lines[0].UninvoicedReceivedQuantity.Should().Be(0m);

        var act = () => po.InvoiceLine(po.Lines[0].Id, 1m);
        act.Should().Throw<InvalidOperationException>().WithMessage("*exceeds*");
    }

    // --- Handler orchestration ---

    [Fact]
    public async Task Posting_an_invoice_books_grir_vat_and_payable_and_opens_an_ap_item()
    {
        var po = ReceivedOrder(qty: 10m, unitPrice: 100m, taxRate: 0.11m, received: 10m);
        var lineId = po.Lines[0].Id;
        _orders.GetByIdAsync(po.Id, Arg.Any<CancellationToken>()).Returns(po);
        StubAccountsAndPosting();

        PurchaseInvoice? captured = null;
        _invoices.When(r => r.Add(Arg.Any<PurchaseInvoice>())).Do(ci => captured = ci.Arg<PurchaseInvoice>());

        var result = await Handler().Handle(
            new PostPurchaseInvoiceCommand(po.Id, "SUP-INV-1", Date, Due, null,
                new[] { new PurchaseInvoiceLineInput(lineId, 10m) }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // 1000 net + 110 VAT = 1110 gross.
        captured.Should().NotBeNull();
        captured!.SubTotal.Should().Be(1000m);
        captured.TaxTotal.Should().Be(110m);
        captured.GrandTotal.Should().Be(1110m);
        po.Lines[0].InvoicedQuantity.Should().Be(10m);

        // Journal: Dr GR/IR 1000 + Dr VAT 110 / Cr AP 1110, balanced, with the AP line carrying the supplier.
        _posted.Should().NotBeNull();
        _posted!.Lines.Sum(l => l.Debit).Should().Be(1110m);
        _posted.Lines.Sum(l => l.Credit).Should().Be(1110m);
        _posted.Lines.Should().Contain(l => l.Credit == 1110m && l.SubledgerPartyId == po.SupplierId);

        // AP open item raised for the gross amount.
        await _subledger.Received(1).OpenPayableAsync(
            po.SupplierId, Arg.Any<Guid>(), Arg.Any<string>(), Date, Due, 1110m, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invoicing_more_than_received_is_rejected_before_posting()
    {
        var po = ReceivedOrder(qty: 10m, received: 3m); // only 3 received
        var lineId = po.Lines[0].Id;
        _orders.GetByIdAsync(po.Id, Arg.Any<CancellationToken>()).Returns(po);

        var result = await Handler().Handle(
            new PostPurchaseInvoiceCommand(po.Id, null, Date, Due, null,
                new[] { new PurchaseInvoiceLineInput(lineId, 5m) }), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PURCHASING.OVER_INVOICE");
        await _ledger.DidNotReceive().PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>());
        await _subledger.DidNotReceive().OpenPayableAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(),
            Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_zero_tax_invoice_omits_the_vat_line()
    {
        var po = ReceivedOrder(qty: 5m, unitPrice: 200m, taxRate: 0m, received: 5m);
        var lineId = po.Lines[0].Id;
        _orders.GetByIdAsync(po.Id, Arg.Any<CancellationToken>()).Returns(po);
        StubAccountsAndPosting();

        var result = await Handler().Handle(
            new PostPurchaseInvoiceCommand(po.Id, null, Date, Due, null,
                new[] { new PurchaseInvoiceLineInput(lineId, 5m) }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _posted!.Lines.Should().HaveCount(2); // Dr GR/IR + Cr AP only
        _posted.Lines.Sum(l => l.Debit).Should().Be(1000m);
        _posted.Lines.Sum(l => l.Credit).Should().Be(1000m);
    }
}
