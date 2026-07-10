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
    public async Task Fifo_back_dated_cheaper_receipt_cascades_through_a_transfer_into_another_warehouses_sale()
    {
        // FIFO product. Warehouse A: Jun-1 receipt 100 @ 10 000; Jun-5 transfer-out 40 to B (carried 400 000).
        // Warehouse B: Jun-5 transfer-in 40 @ 10 000; Jun-10 sales issue of 40 (COGS 400 000).
        // Insert a back-dated A receipt 100 @ 8 000 on May-28 → FIFO consumes the oldest (8 000) layer on the
        // transfer-out, so B receives 40 @ 8 000 = 320 000 and its later sale drops to 320 000 (a −80 000 COGS
        // correction — larger than moving average's −40 000 because FIFO carries the cheap layer, not the mean).
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

        var aLayer = StockCostLayer.Create(_product, _warehouse, "IDR", aReceipt.Id, jun1, 10000m, 100m);
        aLayer.Consume(40m); // 60 remaining after the original transfer-out
        var bLayer = StockCostLayer.Create(_product, _warehouseB, "IDR", bTransferIn.Id, jun5, 10000m, 40m);
        bLayer.Consume(40m); // fully sold

        var bucketA = StockCostBucket.Create(_product, _warehouse, "IDR", CostingMethod.Fifo);
        var bucketB = StockCostBucket.Create(_product, _warehouseB, "IDR", CostingMethod.Fifo);

        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucketA);
        _buckets.ListForProductAsync(_product, Arg.Any<CancellationToken>()).Returns(new[] { bucketA, bucketB });
        _txns.MaxMovementDateAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(jun5);
        _txns.HasTransferOnOrAfterAsync(_product, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(true);
        _txns.ListForProductChronologicalAsync(_product, Arg.Any<CancellationToken>())
            .Returns(new[] { aReceipt, aTransferOut, bTransferIn, bSale });
        _layers.ListAllForProductAsync(_product, Arg.Any<CancellationToken>())
            .Returns(new[] { aLayer, bLayer });

        var result = await Service().ReceiveAsync(
            _product, _warehouse, "IDR", 100m, 8000m, new DateOnly(2026, 5, 28),
            MovementType.Receipt, MovementSource.Purchasing, null, "Late purchase", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CostApplied.Should().Be(800_000m);      // the new receipt: 100 @ 8 000

        // The transfer legs are restated value-preservingly at the oldest layer's cost, and B's sale COGS drops.
        aTransferOut.TotalCost.Should().Be(320_000m);
        bTransferIn.TotalCost.Should().Be(320_000m);
        bSale.TotalCost.Should().Be(320_000m);

        // Buckets end: A 160 @ 9 250 (cheap layer 60 left + 100 @ 10 000), B 0.
        bucketA.OnHandQty.Should().Be(160m);
        bucketA.AvgUnitCost.Should().Be(9250m);
        bucketB.OnHandQty.Should().Be(0m);

        // The destination transfer-in layer's derived cost is rebuilt to 8 000 (it is fully consumed).
        bLayer.UnitCost.Should().Be(8000m);
        aLayer.RemainingQty.Should().Be(100m); // the expensive layer is now untouched by the transfer

        // One net journal for the −80 000 COGS correction: Dr Inventory 80 000 / Cr COGS 80 000.
        var call = _gl.ReceivedCalls().Should().ContainSingle().Which;
        var request = (LedgerPostingRequest)call.GetArguments()[0]!;
        request.Lines.Should().ContainSingle(l => l.AccountId == CogsAccount && l.Credit == 80_000m && l.Debit == 0m);
        request.Lines.Should().ContainSingle(l => l.AccountId == InventoryAccount && l.Debit == 80_000m && l.Credit == 0m);
    }

    [Fact]
    public async Task Fifo_cross_bucket_rebuilds_a_partially_sold_transfer_in_layers_cost_and_remainder()
    {
        // Same shape, but B sells only 25 of the 40 transferred, leaving residual stock — so the transfer-in
        // layer's derived cost (not just its remainder) must be rebuilt for the destination to value correctly.
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
        var bSale = InventoryTransaction.Record(_product, _warehouseB, MovementType.Issue, 25m, 10000m, 250_000m,
            "IDR", jun10, MovementSource.Sales, null, null, 15m, 10000m);

        var aLayer = StockCostLayer.Create(_product, _warehouse, "IDR", aReceipt.Id, jun1, 10000m, 100m);
        aLayer.Consume(40m);
        var bLayer = StockCostLayer.Create(_product, _warehouseB, "IDR", bTransferIn.Id, jun5, 10000m, 40m);
        bLayer.Consume(25m); // 15 remaining

        var bucketA = StockCostBucket.Create(_product, _warehouse, "IDR", CostingMethod.Fifo);
        var bucketB = StockCostBucket.Create(_product, _warehouseB, "IDR", CostingMethod.Fifo);

        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucketA);
        _buckets.ListForProductAsync(_product, Arg.Any<CancellationToken>()).Returns(new[] { bucketA, bucketB });
        _txns.MaxMovementDateAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(jun5);
        _txns.HasTransferOnOrAfterAsync(_product, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(true);
        _txns.ListForProductChronologicalAsync(_product, Arg.Any<CancellationToken>())
            .Returns(new[] { aReceipt, aTransferOut, bTransferIn, bSale });
        _layers.ListAllForProductAsync(_product, Arg.Any<CancellationToken>())
            .Returns(new[] { aLayer, bLayer });

        var result = await Service().ReceiveAsync(
            _product, _warehouse, "IDR", 100m, 8000m, new DateOnly(2026, 5, 28),
            MovementType.Receipt, MovementSource.Purchasing, null, "Late purchase", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // B sold 25 @ 8 000 now (was 25 @ 10 000): a −50 000 COGS correction.
        bSale.TotalCost.Should().Be(200_000m);

        // The transfer-in layer is rebuilt to 15 remaining @ 8 000, and B ends 15 @ 8 000.
        bLayer.UnitCost.Should().Be(8000m);
        bLayer.RemainingQty.Should().Be(15m);
        bucketB.OnHandQty.Should().Be(15m);
        bucketB.AvgUnitCost.Should().Be(8000m);

        var call = _gl.ReceivedCalls().Should().ContainSingle().Which;
        var request = (LedgerPostingRequest)call.GetArguments()[0]!;
        request.Lines.Should().ContainSingle(l => l.AccountId == CogsAccount && l.Credit == 50_000m && l.Debit == 0m);
    }

    // ---- Directly back-dating a transfer document (ADR-0038 Phase 2b) ----

    [Fact]
    public async Task Transfer_back_dated_moving_average_reprices_a_later_destination_sale()
    {
        // MAIN: Jun-1 receipt 100 @ 10 000. STORE: Jun-2 receipt 10 @ 6 000; Jun-10 sale 10 (COGS 60 000).
        // Directly back-date a transfer of 40 MAIN -> STORE on Jun-5: STORE's average before the sale becomes
        // (10*6 000 + 40*10 000)/50 = 9 200, so its later sale is repriced 60 000 -> 92 000 (+32 000 COGS).
        var jun1 = new DateOnly(2026, 6, 1);
        var jun2 = new DateOnly(2026, 6, 2);
        var jun10 = new DateOnly(2026, 6, 10);

        var mainReceipt = InventoryTransaction.Record(_product, _warehouse, MovementType.Receipt, 100m, 10000m, 1_000_000m,
            "IDR", jun1, MovementSource.Purchasing, null, null, 100m, 10000m);
        var storeReceipt = InventoryTransaction.Record(_product, _warehouseB, MovementType.Receipt, 10m, 6000m, 60_000m,
            "IDR", jun2, MovementSource.Purchasing, null, null, 10m, 6000m);
        var storeSale = InventoryTransaction.Record(_product, _warehouseB, MovementType.Issue, 10m, 6000m, 60_000m,
            "IDR", jun10, MovementSource.Sales, null, null, 0m, 0m);

        var bucketMain = StockCostBucket.Create(_product, _warehouse, "IDR");
        var bucketStore = StockCostBucket.Create(_product, _warehouseB, "IDR");

        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucketMain);
        _buckets.ListForProductAsync(_product, Arg.Any<CancellationToken>()).Returns(new[] { bucketMain, bucketStore });
        _txns.ListForProductChronologicalAsync(_product, Arg.Any<CancellationToken>())
            .Returns(new[] { mainReceipt, storeReceipt, storeSale });

        var result = await Service().TransferBackDatedAsync(
            _product, _warehouse, _warehouseB, 40m, new DateOnly(2026, 6, 5), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UnitCost.Should().Be(10000m);                 // 400 000 carried / 40

        storeSale.TotalCost.Should().Be(92_000m);                  // repriced from 60 000
        bucketMain.OnHandQty.Should().Be(60m);                     // 100 − 40 transferred
        bucketMain.AvgUnitCost.Should().Be(10000m);
        bucketStore.OnHandQty.Should().Be(40m);                    // 10 + 40 − 10 sold
        bucketStore.AvgUnitCost.Should().Be(9200m);

        // One net journal: Dr COGS 32 000 / Cr Inventory 32 000.
        var call = _gl.ReceivedCalls().Should().ContainSingle().Which;
        var request = (LedgerPostingRequest)call.GetArguments()[0]!;
        request.Lines.Should().ContainSingle(l => l.AccountId == CogsAccount && l.Debit == 32_000m && l.Credit == 0m);
        request.Lines.Should().ContainSingle(l => l.AccountId == InventoryAccount && l.Credit == 32_000m && l.Debit == 0m);
    }

    [Fact]
    public async Task Transfer_back_dated_fifo_reprices_a_later_source_sale()
    {
        // MAIN (FIFO): Jun-1 receipt 100 @ 8 000, Jun-2 receipt 100 @ 10 000; Jun-10 sale 120 (originally
        // 100 @ 8 000 + 20 @ 10 000 = 1 000 000). Back-date a transfer of 40 MAIN -> STORE on Jun-5: the
        // transfer consumes the oldest 8 000 layer, so the later sale now takes 60 @ 8 000 + 60 @ 10 000 =
        // 1 080 000 (+80 000 COGS). STORE receives the 40 @ 8 000.
        var jun1 = new DateOnly(2026, 6, 1);
        var jun2 = new DateOnly(2026, 6, 2);
        var jun10 = new DateOnly(2026, 6, 10);

        var r1 = InventoryTransaction.Record(_product, _warehouse, MovementType.Receipt, 100m, 8000m, 800_000m,
            "IDR", jun1, MovementSource.Purchasing, null, null, 100m, 8000m);
        var r2 = InventoryTransaction.Record(_product, _warehouse, MovementType.Receipt, 100m, 10000m, 1_000_000m,
            "IDR", jun2, MovementSource.Purchasing, null, null, 200m, 9000m);
        var mainSale = InventoryTransaction.Record(_product, _warehouse, MovementType.Issue, 120m, 8333.3333m, 1_000_000m,
            "IDR", jun10, MovementSource.Sales, null, null, 80m, 10000m);

        var l1 = StockCostLayer.Create(_product, _warehouse, "IDR", r1.Id, jun1, 8000m, 100m);
        l1.Consume(100m); // originally fully consumed by the sale
        var l2 = StockCostLayer.Create(_product, _warehouse, "IDR", r2.Id, jun2, 10000m, 100m);
        l2.Consume(20m); // originally 20 consumed → 80 remaining

        var bucketMain = StockCostBucket.Create(_product, _warehouse, "IDR", CostingMethod.Fifo);
        var bucketStore = StockCostBucket.Create(_product, _warehouseB, "IDR", CostingMethod.Fifo);

        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucketMain);
        _buckets.ListForProductAsync(_product, Arg.Any<CancellationToken>()).Returns(new[] { bucketMain, bucketStore });
        _txns.ListForProductChronologicalAsync(_product, Arg.Any<CancellationToken>())
            .Returns(new[] { r1, r2, mainSale });
        _layers.ListAllForProductAsync(_product, Arg.Any<CancellationToken>())
            .Returns(new[] { l1, l2 });

        var result = await Service().TransferBackDatedAsync(
            _product, _warehouse, _warehouseB, 40m, new DateOnly(2026, 6, 5), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UnitCost.Should().Be(8000m);                  // oldest layer carried

        mainSale.TotalCost.Should().Be(1_080_000m);                // repriced from 1 000 000
        l2.RemainingQty.Should().Be(40m);                          // 100 − 60 now consumed by the sale
        bucketStore.OnHandQty.Should().Be(40m);
        bucketStore.AvgUnitCost.Should().Be(8000m);

        // One net journal: Dr COGS 80 000 / Cr Inventory 80 000.
        var call = _gl.ReceivedCalls().Should().ContainSingle().Which;
        var request = (LedgerPostingRequest)call.GetArguments()[0]!;
        request.Lines.Should().ContainSingle(l => l.AccountId == CogsAccount && l.Debit == 80_000m && l.Credit == 0m);
    }

    [Fact]
    public async Task Transfer_back_dated_across_a_legacy_unlinked_transfer_is_rejected()
    {
        // The product's existing stream has an unlinked (legacy) transfer, which the replay cannot thread —
        // so a new back-dated transfer is rejected rather than miscomputed (ADR-0038 safe degradation).
        var jun1 = new DateOnly(2026, 6, 1);
        var jun3 = new DateOnly(2026, 6, 3);
        var thirdWh = Guid.NewGuid();

        var mainReceipt = InventoryTransaction.Record(_product, _warehouse, MovementType.Receipt, 100m, 10000m, 1_000_000m,
            "IDR", jun1, MovementSource.Purchasing, null, null, 100m, 10000m);
        var legacyOut = InventoryTransaction.Record(_product, _warehouse, MovementType.TransferOut, 10m, 10000m, 100_000m,
            "IDR", jun3, MovementSource.Transfer, null, null, 90m, 10000m, transferGroupId: null);
        var legacyIn = InventoryTransaction.Record(_product, thirdWh, MovementType.TransferIn, 10m, 10000m, 100_000m,
            "IDR", jun3, MovementSource.Transfer, null, null, 10m, 10000m, transferGroupId: null);

        var bucketMain = StockCostBucket.Create(_product, _warehouse, "IDR");
        _buckets.GetAsync(_product, _warehouse, Arg.Any<CancellationToken>()).Returns(bucketMain);
        _txns.ListForProductChronologicalAsync(_product, Arg.Any<CancellationToken>())
            .Returns(new[] { mainReceipt, legacyOut, legacyIn });

        var result = await Service().TransferBackDatedAsync(
            _product, _warehouse, _warehouseB, 40m, new DateOnly(2026, 6, 2), CancellationToken.None);

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
