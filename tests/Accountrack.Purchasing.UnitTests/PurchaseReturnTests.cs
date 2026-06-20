using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Inventory;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.Purchasing.Application.Abstractions;
using Accountrack.Purchasing.Application.Features;
using Accountrack.Purchasing.Domain;
using Accountrack.SharedKernel.Results;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Purchasing.UnitTests;

public class PurchaseReturnTests
{
    private static readonly DateOnly Date = new(2026, 6, 20);

    private sealed class DirectUnitOfWork : ICrossModuleUnitOfWork
    {
        public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct) =>
            work(ct);
    }

    private readonly IPurchaseInvoiceRepository _invoices = Substitute.For<IPurchaseInvoiceRepository>();
    private readonly IPurchaseOrderRepository _orders = Substitute.For<IPurchaseOrderRepository>();
    private readonly IPurchaseReturnRepository _returns = Substitute.For<IPurchaseReturnRepository>();
    private readonly IInventoryPosting _inventory = Substitute.For<IInventoryPosting>();
    private readonly IGeneralLedgerPoster _ledger = Substitute.For<IGeneralLedgerPoster>();
    private readonly IPostingAccountResolver _accounts = Substitute.For<IPostingAccountResolver>();
    private readonly ISubledgerPosting _subledger = Substitute.For<ISubledgerPosting>();

    private LedgerPostingRequest? _posted;

    private readonly Guid _warehouse = Guid.NewGuid();
    private readonly Guid _supplier = Guid.NewGuid();
    private readonly Guid _product = Guid.NewGuid();

    private PostPurchaseReturnHandler Handler() =>
        new(_invoices, _orders, _returns, new DirectUnitOfWork(),
            _inventory, _ledger, _accounts, _subledger);

    // A posted invoice (qty 10 @ 100, 11% VAT) against a received PO.
    private PurchaseInvoice PostedInvoice(out Guid invoiceLineId)
    {
        var order = PurchaseOrder.CreateDraft("PO/202606/00001", _supplier, _warehouse, "IDR", Date, null);
        order.AddLine(_product, 10m, 100m, 0.11m, null);
        var poLine = order.Lines[0];
        order.MarkAutoApproved(Guid.NewGuid());
        order.ReceiveLine(poLine.Id, 10m);
        order.InvoiceLine(poLine.Id, 10m);
        _orders.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var invoice = PurchaseInvoice.Create("PI/202606/00001", "SUP-001", order.Id, _supplier, "IDR", Date, Date.AddMonths(1), null);
        invoice.AddLine(poLine.Id, _product, 10m, 100m, 0.11m);
        invoice.SetPosting(Guid.NewGuid(), Guid.NewGuid());
        invoiceLineId = invoice.Lines[0].Id;
        _invoices.GetByIdAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(invoice);
        return invoice;
    }

    private void StubAccountsPostingInventory(decimal issueUnitCost = 100m)
    {
        _accounts.ResolveAsync("PurchaseReturn", Arg.Any<string>(), Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(_ => Result.Success(Guid.NewGuid()));
        _ledger.PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci => { _posted = ci.Arg<LedgerPostingRequest>(); return Result.Success(Guid.NewGuid()); });
        _subledger.AllocateAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<decimal>(), Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(Guid.NewGuid()));
        _inventory.IssueAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<DateOnly>(), Arg.Any<Guid>(),
                Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result.Success(new StockMovementResult(
                Guid.NewGuid(), ci.ArgAt<decimal>(2) * issueUnitCost, 0m, 0m)));
    }

    // --- Domain ---

    [Fact]
    public void An_invoice_line_can_be_returned_only_up_to_what_is_invoiced()
    {
        var invoice = PostedInvoice(out var lineId);
        invoice.ReturnLine(lineId, 4m);
        invoice.Lines[0].ReturnedQuantity.Should().Be(4m);
        invoice.Lines[0].ReturnableQuantity.Should().Be(6m);

        var act = () => invoice.ReturnLine(lineId, 7m);
        act.Should().Throw<InvalidOperationException>().WithMessage("*exceeds*");
    }

    // --- Handler orchestration ---

    [Fact]
    public async Task Posting_a_return_reverses_billing_and_destocks_at_cost()
    {
        var invoice = PostedInvoice(out var lineId);
        StubAccountsPostingInventory(issueUnitCost: 100m); // MA cost == purchase price → no variance

        PurchaseReturn? captured = null;
        _returns.When(r => r.Add(Arg.Any<PurchaseReturn>())).Do(ci => captured = ci.Arg<PurchaseReturn>());

        var result = await Handler().Handle(
            new PostPurchaseReturnCommand(invoice.Id, Date, null, new[] { new PurchaseReturnLineInput(lineId, 4m) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // 4 @ 100 = 400 net, 11% = 44 tax, 444 gross; cost 4 @ 100 = 400.
        captured!.SubTotal.Should().Be(400m);
        captured.TaxTotal.Should().Be(44m);
        captured.GrandTotal.Should().Be(444m);
        captured.TotalCost.Should().Be(400m);
        invoice.Lines[0].ReturnedQuantity.Should().Be(4m);

        // Balanced debit note + de-stock: Dr AP 444 / Cr VAT 44 + Cr Inventory 400.
        _posted!.Source.Should().Be(LedgerSource.PurchaseReturn);
        _posted.Lines.Sum(l => l.Debit).Should().Be(444m);
        _posted.Lines.Sum(l => l.Credit).Should().Be(444m);
        _posted.Lines.Should().Contain(l => l.Debit == 444m && l.SubledgerPartyId == _supplier);

        await _inventory.Received(1).IssueAsync(
            _product, _warehouse, 4m, Date, Arg.Any<Guid>(), Arg.Any<string>(), false, Arg.Any<CancellationToken>());
        await _subledger.Received(1).AllocateAsync(
            invoice.ApOpenItemId!.Value, Arg.Any<string>(), Date, 444m, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_cost_vs_price_variance_keeps_the_journal_balanced()
    {
        var invoice = PostedInvoice(out var lineId);
        StubAccountsPostingInventory(issueUnitCost: 90m); // MA cost 90 < price 100 → variance 40 on 4 units

        var result = await Handler().Handle(
            new PostPurchaseReturnCommand(invoice.Id, Date, null, new[] { new PurchaseReturnLineInput(lineId, 4m) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Dr AP 444 / Cr VAT 44 + Cr Inventory 360 + Cr Variance 40.
        _posted!.Lines.Sum(l => l.Debit).Should().Be(444m);
        _posted.Lines.Sum(l => l.Credit).Should().Be(444m);
        _posted.Lines.Should().Contain(l => l.Credit == 360m); // inventory at MA cost
        _posted.Lines.Should().Contain(l => l.Credit == 40m);  // price variance
    }

    [Fact]
    public async Task Returning_an_unposted_invoice_is_rejected()
    {
        var invoice = PurchaseInvoice.Create("PI/X", null, Guid.NewGuid(), _supplier, "IDR", Date, Date, null);
        invoice.AddLine(Guid.NewGuid(), _product, 1m, 100m, 0m);
        _invoices.GetByIdAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(invoice);

        var result = await Handler().Handle(
            new PostPurchaseReturnCommand(invoice.Id, Date, null, new[] { new PurchaseReturnLineInput(invoice.Lines[0].Id, 1m) }),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PURCHASING.INVOICE_NOT_POSTED");
        await _ledger.DidNotReceive().PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Over_returning_a_line_is_rejected_before_posting()
    {
        var invoice = PostedInvoice(out var lineId);
        StubAccountsPostingInventory();

        var result = await Handler().Handle(
            new PostPurchaseReturnCommand(invoice.Id, Date, null, new[] { new PurchaseReturnLineInput(lineId, 11m) }),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PURCHASING.OVER_RETURN");
        await _ledger.DidNotReceive().PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>());
    }
}
