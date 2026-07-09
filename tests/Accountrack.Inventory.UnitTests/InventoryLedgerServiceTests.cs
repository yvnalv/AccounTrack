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
        _layers.ListAllForBucketAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
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
    public async Task Fifo_back_dated_cheaper_receipt_reconsumes_layers_and_posts_the_delta_journal()
    {
        // Existing FIFO bucket: Jun-1 receipt 10 @ 100 (layer A), Jun-5 sales issue of 10 consumed it
        // (COGS 1 000); on hand 0.
        var jun1 = new DateOnly(2026, 6, 1);
        var jun5 = new DateOnly(2026, 6, 5);
        var receipt = InventoryTransaction.Record(_product, _warehouse, MovementType.Receipt, 10m, 100m, 1_000m,
            "IDR", jun1, MovementSource.Purchasing, null, null, 10m, 100m);
        var issue = InventoryTransaction.Record(_product, _warehouse, MovementType.Issue, 10m, 100m, 1_000m,
            "IDR", jun5, MovementSource.Sales, null, null, 0m, 0m);
        var layerA = StockCostLayer.Create(_product, _warehouse, "IDR", receipt.Id, jun1, 100m, 10m);
        layerA.Consume(10m); // fully spent by the Jun-5 issue

        var bucket = FifoBucket((10m, 100m));
        bucket.IssueFifo(10m, 1_000m, allowNegative: false); // on hand 0
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucket);
        _txns.MaxMovementDateAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(jun5);
        _txns.ListForBucketChronologicalAsync(_product, _warehouse, Arg.Any<CancellationToken>())
            .Returns(new[] { receipt, issue });

        var service = Service();
        _layers.ListAllForBucketAsync(_product, _warehouse, Arg.Any<CancellationToken>())
            .Returns(new[] { layerA }); // after Service() so it isn't clobbered by the Arg.Any default

        // Back-date a cheaper receipt of 10 @ 80 to May-28 — the new oldest layer the issue now consumes.
        var result = await service.ReceiveAsync(
            _product, _warehouse, "IDR", 10m, 80m, new DateOnly(2026, 5, 28),
            MovementType.Receipt, MovementSource.Purchasing, null, "Late purchase", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CostApplied.Should().Be(800m); // the new receipt itself: 10 @ 80

        // The Jun-5 issue now consumes the cheaper May-28 layer: COGS restated 1 000 → 800.
        issue.TotalCost.Should().Be(800m);

        // The new layer is fully spent by the issue; the original Jun-1 layer is now untouched (10 left).
        layerA.RemainingQty.Should().Be(10m);
        _layers.Received(1).Add(Arg.Is<StockCostLayer>(l => l.OriginalQty == 10m && l.UnitCost == 80m && l.RemainingQty == 0m));

        // Bucket ends at 10 on hand valued from the surviving layer (100).
        bucket.OnHandQty.Should().Be(10m);
        bucket.AvgUnitCost.Should().Be(100m);

        // One correcting journal: Dr Inventory 200 / Cr COGS 200 (COGS was overstated by 200).
        var call = _gl.ReceivedCalls().Should().ContainSingle().Which;
        var request = (LedgerPostingRequest)call.GetArguments()[0]!;
        request.Lines.Should().ContainSingle(l => l.AccountId == CogsAccount && l.Credit == 200m && l.Debit == 0m);
        request.Lines.Should().ContainSingle(l => l.AccountId == InventoryAccount && l.Debit == 200m && l.Credit == 0m);
    }

    [Fact]
    public async Task Fifo_back_dating_before_a_later_transfer_is_rejected()
    {
        // Jun-1 receipt 10 @ 100, Jun-5 transfer-out 10 (moves cost to another bucket — out of scope).
        var jun1 = new DateOnly(2026, 6, 1);
        var jun5 = new DateOnly(2026, 6, 5);
        var receipt = InventoryTransaction.Record(_product, _warehouse, MovementType.Receipt, 10m, 100m, 1_000m,
            "IDR", jun1, MovementSource.Purchasing, null, null, 10m, 100m);
        var transfer = InventoryTransaction.Record(_product, _warehouse, MovementType.TransferOut, 10m, 100m, 1_000m,
            "IDR", jun5, MovementSource.Transfer, null, null, 0m, 0m);
        var layerA = StockCostLayer.Create(_product, _warehouse, "IDR", receipt.Id, jun1, 100m, 10m);
        layerA.Consume(10m);

        var bucket = FifoBucket((10m, 100m));
        bucket.IssueFifo(10m, 1_000m, allowNegative: false);
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucket);
        _txns.MaxMovementDateAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(jun5);
        _txns.ListForBucketChronologicalAsync(_product, _warehouse, Arg.Any<CancellationToken>())
            .Returns(new[] { receipt, transfer });

        var service = Service();
        _layers.ListAllForBucketAsync(_product, _warehouse, Arg.Any<CancellationToken>())
            .Returns(new[] { layerA });

        var result = await service.ReceiveAsync(
            _product, _warehouse, "IDR", 10m, 80m, new DateOnly(2026, 5, 28),
            MovementType.Receipt, MovementSource.Purchasing, null, "Late purchase", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVENTORY.BACKDATING_CROSSES_TRANSFER");
    }

    // ---- Cross-bucket (transfer) back-dated recompute (ADR-0038) ----

    private readonly Guid _warehouseB = Guid.NewGuid();

    [Fact]
    public async Task Back_dated_cheaper_receipt_cascades_through_a_transfer_into_another_warehouses_sale()
    {
        // Warehouse A: Jun-1 receipt 100 @ 10 000; Jun-5 transfer-out 40 to B (carried 400 000).
        // Warehouse B: Jun-5 transfer-in 40 @ 10 000; Jun-10 sales issue of 40 (COGS 400 000).
        // Insert a back-dated A receipt 100 @ 8 000 on May-28 → A avg 9 000, the transfer now carries
        // 40 @ 9 000 = 360 000 to B, so B's later sale drops to 360 000 (a −40 000 COGS correction).
        var jun1 = new DateOnly(2026, 6, 1);
        var jun5 = new DateOnly(2026, 6, 5);
        var jun10 = new DateOnly(2026, 6, 10);
        var group = Guid.NewGuid();

        var aReceipt = InventoryTransaction.Record(_product, _warehouse, MovementType.Receipt, 100m, 10000m, 1_000_000m,
            "IDR", jun1, MovementSource.Purchasing, null, null, 100m, 10000m);
        var aTransferOut = InventoryTransaction.Record(_product, _warehouse, MovementType.TransferOut, 40m, 10000m, 400_000m,
            "IDR", jun5, MovementSource.Transfer, null, null, 60m, 10000m, group);
        var bTransferIn = InventoryTransaction.Record(_product, _warehouseB, MovementType.TransferIn, 40m, 10000m, 400_000m,
            "IDR", jun5, MovementSource.Transfer, null, null, 40m, 10000m, group);
        var bSale = InventoryTransaction.Record(_product, _warehouseB, MovementType.Issue, 40m, 10000m, 400_000m,
            "IDR", jun10, MovementSource.Sales, null, null, 0m, 0m);

        var bucketA = StockCostBucket.Create(_product, _warehouse, "IDR");
        bucketA.Receive(100m, 10000m);
        bucketA.Issue(40m, allowNegative: false); // on hand 60 @ 10 000
        var bucketB = StockCostBucket.Create(_product, _warehouseB, "IDR");
        bucketB.Receive(40m, 10000m);
        bucketB.Issue(40m, allowNegative: false); // on hand 0

        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucketA);
        _buckets.ListForProductAsync(_product, Arg.Any<CancellationToken>()).Returns(new[] { bucketA, bucketB });
        _txns.MaxMovementDateAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(jun5);
        _txns.HasTransferOnOrAfterAsync(_product, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(true);
        _txns.ListForProductChronologicalAsync(_product, Arg.Any<CancellationToken>())
            .Returns(new[] { aReceipt, aTransferOut, bTransferIn, bSale });

        var result = await Service().ReceiveAsync(
            _product, _warehouse, "IDR", 100m, 8000m, new DateOnly(2026, 5, 28),
            MovementType.Receipt, MovementSource.Purchasing, null, "Late purchase", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CostApplied.Should().Be(800_000m);      // the new receipt: 100 @ 8 000

        // The transfer legs are restated value-preservingly (both 360 000), and B's sale COGS drops.
        aTransferOut.TotalCost.Should().Be(360_000m);
        bTransferIn.TotalCost.Should().Be(360_000m);
        bSale.TotalCost.Should().Be(360_000m);

        // Buckets end: A 160 @ 9 000, B 0.
        bucketA.OnHandQty.Should().Be(160m);
        bucketA.AvgUnitCost.Should().Be(9000m);
        bucketB.OnHandQty.Should().Be(0m);

        // One net journal for the −40 000 COGS correction: Dr Inventory 40 000 / Cr COGS 40 000.
        var call = _gl.ReceivedCalls().Should().ContainSingle().Which;
        var request = (LedgerPostingRequest)call.GetArguments()[0]!;
        request.Lines.Should().ContainSingle(l => l.AccountId == CogsAccount && l.Credit == 40_000m && l.Debit == 0m);
        request.Lines.Should().ContainSingle(l => l.AccountId == InventoryAccount && l.Debit == 40_000m && l.Credit == 0m);
    }

    [Fact]
    public async Task Back_dating_across_a_legacy_unlinked_transfer_is_rejected()
    {
        // Same shape, but the transfer legs predate the TransferGroupId link (null group) — the recompute
        // cannot thread the cost, so it is rejected rather than miscomputed (ADR-0038 safe degradation).
        var jun1 = new DateOnly(2026, 6, 1);
        var jun5 = new DateOnly(2026, 6, 5);

        var aReceipt = InventoryTransaction.Record(_product, _warehouse, MovementType.Receipt, 100m, 10000m, 1_000_000m,
            "IDR", jun1, MovementSource.Purchasing, null, null, 100m, 10000m);
        var aTransferOut = InventoryTransaction.Record(_product, _warehouse, MovementType.TransferOut, 40m, 10000m, 400_000m,
            "IDR", jun5, MovementSource.Transfer, null, null, 60m, 10000m, transferGroupId: null);
        var bTransferIn = InventoryTransaction.Record(_product, _warehouseB, MovementType.TransferIn, 40m, 10000m, 400_000m,
            "IDR", jun5, MovementSource.Transfer, null, null, 40m, 10000m, transferGroupId: null);

        var bucketA = StockCostBucket.Create(_product, _warehouse, "IDR");
        bucketA.Receive(100m, 10000m);
        bucketA.Issue(40m, allowNegative: false);
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucketA);
        _txns.MaxMovementDateAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(jun5);
        _txns.HasTransferOnOrAfterAsync(_product, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(true);
        _txns.ListForProductChronologicalAsync(_product, Arg.Any<CancellationToken>())
            .Returns(new[] { aReceipt, aTransferOut, bTransferIn });

        var result = await Service().ReceiveAsync(
            _product, _warehouse, "IDR", 100m, 8000m, new DateOnly(2026, 5, 28),
            MovementType.Receipt, MovementSource.Purchasing, null, "Late purchase", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVENTORY.BACKDATING_CROSSES_TRANSFER");
        await _gl.DidNotReceiveWithAnyArgs().PostAsync(default!, default);
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
