using Accountrack.Application.Abstractions.Context;
using Accountrack.Inventory.Application.Abstractions;
using Accountrack.Inventory.Application.Services;
using Accountrack.Inventory.Domain;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Company;
using Accountrack.Modules.Contracts.MasterData;
using Accountrack.SharedKernel.Inventory;
using Accountrack.SharedKernel.Results;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Accountrack.Inventory.UnitTests;

public class InventoryLedgerServiceTests
{
    private static readonly DateOnly Date = new(2026, 6, 13);
    private readonly IStockBucketRepository _buckets = Substitute.For<IStockBucketRepository>();
    private readonly IStockCostLayerRepository _layers = Substitute.For<IStockCostLayerRepository>();
    private readonly IInventoryTransactionRepository _txns = Substitute.For<IInventoryTransactionRepository>();
    private readonly ICompanyDirectory _companies = Substitute.For<ICompanyDirectory>();
    private readonly IMasterDataLookup _masterData = Substitute.For<IMasterDataLookup>();
    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();
    private readonly IGeneralLedgerPoster _gl = Substitute.For<IGeneralLedgerPoster>();
    private readonly IPostingAccountResolver _accounts = Substitute.For<IPostingAccountResolver>();
    private readonly Guid _product = Guid.NewGuid();
    private readonly Guid _warehouse = Guid.NewGuid();
    private static readonly Guid InventoryAccount = Guid.NewGuid();
    private static readonly Guid CogsAccount = Guid.NewGuid();
    private static readonly Guid VarianceAccount = Guid.NewGuid();

    private InventoryLedgerService Service(bool allowNegative = false)
    {
        _companies.GetBoolSettingAsync(Arg.Any<Guid>(), CompanySettingKeys.AllowNegativeStock, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(allowNegative);
        _accounts.ResolveAsync("InventoryRecompute", PostingKeys.Inventory, Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(InventoryAccount));
        _accounts.ResolveAsync("InventoryRecompute", PostingKeys.Cogs, Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(CogsAccount));
        _accounts.ResolveAsync("InventoryRecompute", PostingKeys.InventoryVariance, Arg.Any<PostingSelector>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(VarianceAccount));
        _gl.PostAsync(Arg.Any<LedgerPostingRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Guid.NewGuid()));
        _masterData.GetCostingMethodAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(CostingMethod.MovingAverage);
        _layers.ListOpenForBucketAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<StockCostLayer>());
        return new(_buckets, _layers, _txns, _companies, _masterData, _tenant, _gl, _accounts);
    }

    [Fact]
    public async Task Receive_creates_a_bucket_and_records_a_transaction()
    {
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns((StockCostBucket?)null);

        var result = await Service().ReceiveAsync(
            _product, _warehouse, "IDR", 10m, 100m, Date, MovementType.Receipt, MovementSource.Manual, null, null,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RunningQtyAfter.Should().Be(10m);
        result.Value.RunningAvgCostAfter.Should().Be(100m);
        result.Value.CostApplied.Should().Be(1000m);
        _buckets.Received(1).Add(Arg.Any<StockCostBucket>());
        _txns.Received(1).Add(Arg.Any<InventoryTransaction>());
    }

    [Fact]
    public async Task Receive_into_existing_bucket_updates_the_average()
    {
        var bucket = StockCostBucket.Create(_product, _warehouse, "IDR");
        bucket.Receive(10m, 100m); // existing: 10 @ 100
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucket);

        var result = await Service().ReceiveAsync(
            _product, _warehouse, "IDR", 10m, 120m, Date, MovementType.Receipt, MovementSource.Manual, null, null,
            CancellationToken.None);

        result.Value.RunningQtyAfter.Should().Be(20m);
        result.Value.RunningAvgCostAfter.Should().Be(110m);
        _buckets.DidNotReceive().Add(Arg.Any<StockCostBucket>()); // reused existing
    }

    [Fact]
    public async Task Issue_returns_cost_at_average_and_reduces_on_hand()
    {
        var bucket = StockCostBucket.Create(_product, _warehouse, "IDR");
        bucket.Receive(20m, 110m); // 20 @ 110
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucket);

        var result = await Service().IssueAsync(
            _product, _warehouse, 5m, Date, MovementType.Issue, MovementSource.Sales, null, null,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CostApplied.Should().Be(550m);
        result.Value.RunningQtyAfter.Should().Be(15m);
        _txns.Received(1).Add(Arg.Any<InventoryTransaction>());
    }

    // ---- FIFO costing (ADR-0034) ----

    private StockCostBucket FifoBucket(params (decimal Qty, decimal Cost)[] receipts)
    {
        var bucket = StockCostBucket.Create(_product, _warehouse, "IDR", CostingMethod.Fifo);
        foreach (var (q, c) in receipts)
        {
            bucket.Receive(q, c);
        }

        return bucket;
    }

    private void SeedFifoLayers(params (decimal Qty, decimal Cost)[] layers)
    {
        var rows = layers
            .Select(l => StockCostLayer.Create(_product, _warehouse, "IDR", Guid.NewGuid(), Date, l.Cost, l.Qty))
            .ToList();
        _layers.ListOpenForBucketAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(rows);
    }

    [Fact]
    public async Task Fifo_receipt_opens_a_cost_layer()
    {
        var bucket = FifoBucket((10m, 100m));
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucket);

        var result = await Service().ReceiveAsync(
            _product, _warehouse, "IDR", 5m, 120m, Date, MovementType.Receipt, MovementSource.Manual, null, null,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _layers.Received(1).Add(Arg.Is<StockCostLayer>(l => l.RemainingQty == 5m && l.UnitCost == 120m));
    }

    [Fact]
    public async Task Fifo_issue_consumes_the_oldest_layers_first()
    {
        var bucket = FifoBucket((10m, 100m), (10m, 120m)); // on hand 20, value 2,200
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucket);

        var service = Service();
        SeedFifoLayers((10m, 100m), (10m, 120m)); // after Service() so it isn't clobbered by the Arg.Any default

        var result = await service.IssueAsync(
            _product, _warehouse, 15m, Date, MovementType.Issue, MovementSource.Sales, null, null,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CostApplied.Should().Be(1600m);      // 10@100 + 5@120
        result.Value.RunningQtyAfter.Should().Be(5m);
        result.Value.RunningAvgCostAfter.Should().Be(120m); // the remaining layer's cost
    }

    [Fact]
    public async Task Fifo_back_dated_movement_is_rejected()
    {
        var bucket = FifoBucket((10m, 100m));
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucket);
        _txns.MaxMovementDateAsync(_product, _warehouse, Arg.Any<CancellationToken>())
            .Returns(Date.AddDays(5)); // an existing later movement → this one is back-dated

        var result = await Service().ReceiveAsync(
            _product, _warehouse, "IDR", 5m, 120m, Date, MovementType.Receipt, MovementSource.Manual, null, null,
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVENTORY.BACKDATING_FIFO_NOT_SUPPORTED");
    }

    [Fact]
    public async Task Issue_fails_when_insufficient_stock_and_negative_disallowed()
    {
        var bucket = StockCostBucket.Create(_product, _warehouse, "IDR");
        bucket.Receive(5m, 100m);
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucket);

        var result = await Service().IssueAsync(
            _product, _warehouse, 10m, Date, MovementType.Issue, MovementSource.Sales, null, null,
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVENTORY.INSUFFICIENT_STOCK");
        _txns.DidNotReceive().Add(Arg.Any<InventoryTransaction>());
    }

    [Fact]
    public async Task Issue_below_zero_is_allowed_when_the_company_setting_permits_it()
    {
        var bucket = StockCostBucket.Create(_product, _warehouse, "IDR");
        bucket.Receive(5m, 100m);
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucket);

        var result = await Service(allowNegative: true).IssueAsync(
            _product, _warehouse, 10m, Date, MovementType.Issue, MovementSource.Sales, null, null,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RunningQtyAfter.Should().Be(-5m);
        _txns.Received(1).Add(Arg.Any<InventoryTransaction>());
    }

    [Fact]
    public async Task Issue_with_no_bucket_fails()
    {
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns((StockCostBucket?)null);

        var result = await Service().IssueAsync(
            _product, _warehouse, 1m, Date, MovementType.Issue, MovementSource.Sales, null, null,
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVENTORY.INSUFFICIENT_STOCK");
    }

    [Fact]
    public async Task Back_dated_cheaper_receipt_recomputes_later_COGS_and_posts_the_delta_journal()
    {
        // Existing bucket: Jun-1 receipt 100 @ 10 000, Jun-5 sales issue of 60 (COGS 600 000).
        var jun1 = new DateOnly(2026, 6, 1);
        var jun5 = new DateOnly(2026, 6, 5);
        var receipt = InventoryTransaction.Record(_product, _warehouse, MovementType.Receipt, 100m, 10000m, 1_000_000m,
            "IDR", jun1, MovementSource.Purchasing, null, null, 100m, 10000m);
        var issue = InventoryTransaction.Record(_product, _warehouse, MovementType.Issue, 60m, 10000m, 600_000m,
            "IDR", jun5, MovementSource.Sales, null, null, 40m, 10000m);

        var bucket = StockCostBucket.Create(_product, _warehouse, "IDR");
        bucket.Receive(100m, 10000m);
        bucket.Issue(60m, allowNegative: false); // on hand 40 @ 10 000
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucket);
        _txns.MaxMovementDateAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(jun5);
        _txns.ListForBucketChronologicalAsync(_product, _warehouse, Arg.Any<CancellationToken>())
            .Returns(new[] { receipt, issue });

        // Back-date a cheaper receipt of 100 @ 8 000 to May-28 — before both existing movements.
        var result = await Service().ReceiveAsync(
            _product, _warehouse, "IDR", 100m, 8000m, new DateOnly(2026, 5, 28),
            MovementType.Receipt, MovementSource.Purchasing, null, "Late purchase", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CostApplied.Should().Be(800_000m);          // the new receipt itself: 100 @ 8 000

        // The later issue is restated from 600 000 to 60 @ 9 000 = 540 000.
        issue.TotalCost.Should().Be(540_000m);
        issue.RunningAvgCostAfter.Should().Be(9000m);

        // The bucket ends at 140 on hand @ 9 000.
        bucket.OnHandQty.Should().Be(140m);
        bucket.AvgUnitCost.Should().Be(9000m);

        // One correcting journal: Dr Inventory 60 000 / Cr COGS 60 000 (COGS was overstated by 60 000).
        var call = _gl.ReceivedCalls().Should().ContainSingle().Which;
        var request = (LedgerPostingRequest)call.GetArguments()[0]!;
        request.Lines.Should().ContainSingle(l => l.AccountId == CogsAccount && l.Credit == 60_000m && l.Debit == 0m);
        request.Lines.Should().ContainSingle(l => l.AccountId == InventoryAccount && l.Debit == 60_000m && l.Credit == 0m);
    }
}
