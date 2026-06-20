using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Inventory;
using Accountrack.Modules.Contracts.Transactions;
using Accountrack.Sales.Application.Abstractions;
using Accountrack.Sales.Application.Features;
using Accountrack.Sales.Domain;
using Accountrack.SharedKernel.Results;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Sales.UnitTests;

public class SalesReturnTests
{
    private static readonly DateOnly Date = new(2026, 6, 20);

    private sealed class DirectUnitOfWork : ICrossModuleUnitOfWork
    {
        public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct) =>
            work(ct);
    }

    private readonly ISalesInvoiceRepository _invoices = Substitute.For<ISalesInvoiceRepository>();
    private readonly ISalesOrderRepository _orders = Substitute.For<ISalesOrderRepository>();
    private readonly IDeliveryOrderRepository _deliveries = Substitute.For<IDeliveryOrderRepository>();
    private readonly ISalesReturnRepository _returns = Substitute.For<ISalesReturnRepository>();
    private readonly IInventoryPosting _inventory = Substitute.For<IInventoryPosting>();
    private readonly IGeneralLedgerPoster _ledger = Substitute.For<IGeneralLedgerPoster>();
    private readonly IPostingAccountResolver _accounts = Substitute.For<IPostingAccountResolver>();
    private readonly ISubledgerPosting _subledger = Substitute.For<ISubledgerPosting>();

    private LedgerPostingRequest? _posted;

    private readonly Guid _warehouse = Guid.NewGuid();
    private readonly Guid _customer = Guid.NewGuid();
    private readonly Guid _product = Guid.NewGuid();
    private readonly Guid _soLineId = Guid.NewGuid();

    private PostSalesReturnHandler Handler() =>
        new(_invoices, _orders, _deliveries, _returns, new DirectUnitOfWork(),
            _inventory, _ledger, _accounts, _subledger);

    // A posted invoice (qty 10 @ 100, 11% VAT) for goods delivered at unit cost 50.
    private SalesInvoice PostedInvoice(out Guid invoiceLineId)
    {
        var order = SalesOrder.CreateDraft("SO/202606/00001", _customer, _warehouse, "IDR", Date, null);
        // Recreate the SO line id deterministically by reflection isn't needed — use the order's line.
        order.AddLine(_product, 10m, 100m, 0.11m, null);
        var soLine = order.Lines[0];
        order.MarkAutoApproved(Guid.NewGuid());
        order.DeliverLine(soLine.Id, 10m);
        order.InvoiceLine(soLine.Id, 10m);
        _orders.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var delivery = DeliveryOrder.Create("DO/202606/00001", order.Id, _customer, _warehouse, "IDR", Date, null);
        delivery.AddLine(soLine.Id, _product, 10m, 50m);
        _deliveries.ListBySalesOrderAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { delivery });

        var invoice = SalesInvoice.Create("SI/202606/00001", order.Id, _customer, "IDR", Date, Date.AddMonths(1), null);
        invoice.AddLine(soLine.Id, _product, 10m, 100m, 0.11m);
        invoice.SetPosting(Guid.NewGuid(), Guid.NewGuid());
        invoiceLineId = invoice.Lines[0].Id;
        _invoices.GetByIdAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(invoice);
        return invoice;
    }

    private void StubAccountsPostingInventory()
    {
        _accounts.ResolveAsync("SalesReturn", Arg.Any<string>(), Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(_ => Result.Success(Guid.NewGuid()));
        _ledger.PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci => { _posted = ci.Arg<LedgerPostingRequest>(); return Result.Success(Guid.NewGuid()); });
        _subledger.AllocateAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<decimal>(), Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(Guid.NewGuid()));
        _inventory.ReceiveAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<decimal>(),
                Arg.Any<DateOnly>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result.Success(new StockMovementResult(
                Guid.NewGuid(), ci.ArgAt<decimal>(3) * ci.ArgAt<decimal>(4), 0m, 0m)));
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
    public async Task Posting_a_return_reverses_billing_and_restocks_at_original_cost()
    {
        var invoice = PostedInvoice(out var lineId);
        StubAccountsPostingInventory();

        SalesReturn? captured = null;
        _returns.When(r => r.Add(Arg.Any<SalesReturn>())).Do(ci => captured = ci.Arg<SalesReturn>());

        var result = await Handler().Handle(
            new PostSalesReturnCommand(invoice.Id, Date, null, new[] { new SalesReturnLineInput(lineId, 4m) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // 4 @ 100 = 400 net, 11% = 44 tax, 444 gross; cost 4 @ 50 = 200.
        captured!.SubTotal.Should().Be(400m);
        captured.TaxTotal.Should().Be(44m);
        captured.GrandTotal.Should().Be(444m);
        captured.TotalCost.Should().Be(200m);
        invoice.Lines[0].ReturnedQuantity.Should().Be(4m);

        // Balanced credit-note + restock: Dr Revenue 400 + Dr VAT 44 + Dr Inventory 200 / Cr AR 444 + Cr COGS 200.
        _posted!.Source.Should().Be(LedgerSource.SalesReturn);
        _posted.Lines.Sum(l => l.Debit).Should().Be(644m);
        _posted.Lines.Sum(l => l.Credit).Should().Be(644m);
        _posted.Lines.Should().Contain(l => l.Credit == 444m && l.SubledgerPartyId == _customer);

        await _inventory.Received(1).ReceiveAsync(
            _product, _warehouse, "IDR", 4m, 50m, Date, Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _subledger.Received(1).AllocateAsync(
            invoice.ArOpenItemId!.Value, Arg.Any<string>(), Date, 444m, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returning_an_unposted_invoice_is_rejected()
    {
        var invoice = SalesInvoice.Create("SI/X", Guid.NewGuid(), _customer, "IDR", Date, Date, null);
        invoice.AddLine(_soLineId, _product, 1m, 100m, 0m);
        _invoices.GetByIdAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(invoice);

        var result = await Handler().Handle(
            new PostSalesReturnCommand(invoice.Id, Date, null, new[] { new SalesReturnLineInput(invoice.Lines[0].Id, 1m) }),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("SALES.INVOICE_NOT_POSTED");
        await _ledger.DidNotReceive().PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Over_returning_a_line_is_rejected_before_posting()
    {
        var invoice = PostedInvoice(out var lineId);
        StubAccountsPostingInventory();

        var result = await Handler().Handle(
            new PostSalesReturnCommand(invoice.Id, Date, null, new[] { new SalesReturnLineInput(lineId, 11m) }),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("SALES.OVER_RETURN");
        await _ledger.DidNotReceive().PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>());
    }
}
