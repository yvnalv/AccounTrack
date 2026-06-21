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

public class DeliveryOrderTests
{
    private static readonly DateOnly Date = new(2026, 6, 16);

    private sealed class DirectUnitOfWork : ICrossModuleUnitOfWork
    {
        public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken ct) =>
            work(ct);
    }

    private readonly ISalesOrderRepository _orders = Substitute.For<ISalesOrderRepository>();
    private readonly IDeliveryOrderRepository _deliveries = Substitute.For<IDeliveryOrderRepository>();
    private readonly IInventoryPosting _inventory = Substitute.For<IInventoryPosting>();
    private readonly IGeneralLedgerPoster _ledger = Substitute.For<IGeneralLedgerPoster>();
    private readonly IPostingAccountResolver _accounts = Substitute.For<IPostingAccountResolver>();

    private LedgerPostingRequest? _posted;

    private static SalesOrder ApprovedOrder(decimal qty = 10m, decimal unitPrice = 250m)
    {
        var so = SalesOrder.CreateDraft("SO/202606/00001", Guid.NewGuid(), Guid.NewGuid(), "IDR", Date, null);
        so.AddLine(Guid.NewGuid(), qty, unitPrice, 0.11m, null);
        so.MarkAutoApproved(Guid.NewGuid());
        return so;
    }

    private PostDeliveryOrderHandler Handler() =>
        new(_orders, _deliveries, new DirectUnitOfWork(), _inventory, _ledger, _accounts);

    // --- Domain ---

    [Fact]
    public void Delivering_part_then_all_advances_status()
    {
        var so = ApprovedOrder(qty: 10m);
        so.DeliverLine(so.Lines[0].Id, 4m);
        so.Status.Should().Be(SalesOrderStatus.PartiallyDelivered);
        so.Lines[0].OutstandingQuantity.Should().Be(6m);

        so.DeliverLine(so.Lines[0].Id, 6m);
        so.Status.Should().Be(SalesOrderStatus.Delivered);
        so.Lines[0].IsFullyDelivered.Should().BeTrue();
    }

    [Fact]
    public void Over_delivery_is_rejected()
    {
        var so = ApprovedOrder(qty: 10m);
        var act = () => so.DeliverLine(so.Lines[0].Id, 11m);
        act.Should().Throw<InvalidOperationException>().WithMessage("*exceeds*");
    }

    // --- Handler orchestration ---

    [Fact]
    public async Task Posting_a_delivery_issues_stock_posts_cogs_and_advances_the_so()
    {
        var so = ApprovedOrder(qty: 10m);
        var lineId = so.Lines[0].Id;
        _orders.GetByIdAsync(so.Id, Arg.Any<CancellationToken>()).Returns(so);
        // Issue 4 units; moving-average cost applied = 400 (unit 100).
        _inventory.IssueAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), 4m, Date, Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new StockMovementResult(Guid.NewGuid(), 400m, 6m, 100m));
        _accounts.ResolveAsync("GoodsShipment", Arg.Any<string>(), Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(_ => Result.Success(Guid.NewGuid()));
        _ledger.PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci => { _posted = ci.Arg<LedgerPostingRequest>(); return Result.Success(Guid.NewGuid()); });

        DeliveryOrder? captured = null;
        _deliveries.When(r => r.Add(Arg.Any<DeliveryOrder>())).Do(ci => captured = ci.Arg<DeliveryOrder>());

        var result = await Handler().Handle(
            new PostDeliveryOrderCommand(so.Id, Date, "ship", new[] { new DeliveryOrderLineInput(lineId, 4m) }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        so.Status.Should().Be(SalesOrderStatus.PartiallyDelivered);
        captured!.TotalCost.Should().Be(400m);

        // Journal: Dr COGS 400 / Cr Inventory 400, balanced.
        _posted!.Lines.Sum(l => l.Debit).Should().Be(400m);
        _posted.Lines.Sum(l => l.Credit).Should().Be(400m);
        _posted.Source.Should().Be(LedgerSource.Shipment);
    }

    [Fact]
    public async Task Over_delivery_fails_before_issuing_or_posting()
    {
        var so = ApprovedOrder(qty: 10m);
        var lineId = so.Lines[0].Id;
        _orders.GetByIdAsync(so.Id, Arg.Any<CancellationToken>()).Returns(so);

        var result = await Handler().Handle(
            new PostDeliveryOrderCommand(so.Id, Date, null, new[] { new DeliveryOrderLineInput(lineId, 20m) }),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("SALES.OVER_DELIVERY");
        await _inventory.DidNotReceive().IssueAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<DateOnly>(), Arg.Any<Guid>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _ledger.DidNotReceive().PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Insufficient_stock_fails_the_delivery()
    {
        var so = ApprovedOrder(qty: 10m);
        var lineId = so.Lines[0].Id;
        _orders.GetByIdAsync(so.Id, Arg.Any<CancellationToken>()).Returns(so);
        _inventory.IssueAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<DateOnly>(), Arg.Any<Guid>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Error.BusinessRule("BR-INV-1", "Insufficient stock.", "INVENTORY.INSUFFICIENT_STOCK"));

        var result = await Handler().Handle(
            new PostDeliveryOrderCommand(so.Id, Date, null, new[] { new DeliveryOrderLineInput(lineId, 4m) }),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVENTORY.INSUFFICIENT_STOCK");
        await _ledger.DidNotReceive().PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>());
    }
}
