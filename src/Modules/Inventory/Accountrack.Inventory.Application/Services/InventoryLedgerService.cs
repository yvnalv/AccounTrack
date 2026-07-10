using Accountrack.Application.Abstractions.Context;
using Accountrack.Inventory.Application.Abstractions;
using Accountrack.Inventory.Domain;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Company;
using Accountrack.Modules.Contracts.MasterData;
using Accountrack.SharedKernel.Inventory;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Inventory.Application.Services;

/// <summary>Default <see cref="IInventoryLedger"/> over the bucket + transaction repositories.</summary>
public sealed class InventoryLedgerService : IInventoryLedger
{
    private readonly IStockBucketRepository _buckets;
    private readonly IStockCostLayerRepository _layers;
    private readonly IInventoryTransactionRepository _transactions;
    private readonly ICompanyDirectory _companies;
    private readonly IMasterDataLookup _masterData;
    private readonly ITenantContext _tenant;
    private readonly IGeneralLedgerPoster _ledger;
    private readonly IPostingAccountResolver _accounts;

    public InventoryLedgerService(
        IStockBucketRepository buckets, IStockCostLayerRepository layers,
        IInventoryTransactionRepository transactions,
        ICompanyDirectory companies, IMasterDataLookup masterData, ITenantContext tenant,
        IGeneralLedgerPoster ledger, IPostingAccountResolver accounts)
    {
        _buckets = buckets;
        _layers = layers;
        _transactions = transactions;
        _companies = companies;
        _masterData = masterData;
        _tenant = tenant;
        _ledger = ledger;
        _accounts = accounts;
    }

    public async Task<Result<StockMovementResult>> ReceiveAsync(
        Guid productId, Guid warehouseId, string currency, decimal quantity, decimal unitCost,
        DateOnly date, MovementType type, MovementSource source, Guid? sourceDocumentId, string? description,
        CancellationToken ct, Guid? transferGroupId = null)
    {
        if (quantity <= 0)
        {
            return InventoryErrors.InvalidQuantity;
        }

        var bucket = await _buckets.GetAsync(productId, warehouseId, ct);
        if (bucket is null)
        {
            var method = await _masterData.GetCostingMethodAsync(productId, ct);
            bucket = StockCostBucket.Create(productId, warehouseId, currency, method);
            _buckets.Add(bucket);
        }

        if (await IsBackDatedAsync(productId, warehouseId, date, ct))
        {
            return await RecomputeBackDatedAsync(
                bucket, productId, warehouseId, type, quantity, unitCost, date, source, sourceDocumentId,
                description, transferGroupId, allowNegative: false, ct);
        }

        // Receipt quantity/value and the derived average are maintained the same way for both methods;
        // FIFO additionally opens a cost layer at this receipt's unit cost (ADR-0034).
        var totalCost = bucket.Receive(quantity, unitCost);
        var recorded = Record(productId, warehouseId, type, quantity, unitCost, totalCost,
            bucket.Currency, date, source, sourceDocumentId, description, bucket, transferGroupId);

        if (bucket.CostingMethod == CostingMethod.Fifo && recorded.IsSuccess)
        {
            _layers.Add(StockCostLayer.Create(
                productId, warehouseId, bucket.Currency, recorded.Value.TransactionId, date, unitCost, quantity));
        }

        return recorded;
    }

    public async Task<Result<StockMovementResult>> IssueAsync(
        Guid productId, Guid warehouseId, decimal quantity,
        DateOnly date, MovementType type, MovementSource source, Guid? sourceDocumentId, string? description,
        CancellationToken ct, Guid? transferGroupId = null)
    {
        if (quantity <= 0)
        {
            return InventoryErrors.InvalidQuantity;
        }

        // The negative-stock policy is a per-company setting (default: disallow). BR-INV-3, ADR-0016.
        var allowNegative = await _companies.GetBoolSettingAsync(
            _tenant.CompanyId, CompanySettingKeys.AllowNegativeStock, false, ct);

        var bucket = await _buckets.GetAsync(productId, warehouseId, ct);

        if (bucket is not null && await IsBackDatedAsync(productId, warehouseId, date, ct))
        {
            return await RecomputeBackDatedAsync(
                bucket, productId, warehouseId, type, quantity, unitCost: 0m, date, source, sourceDocumentId,
                description, transferGroupId, allowNegative, ct);
        }

        var onHand = bucket?.OnHandQty ?? 0m;
        if (!allowNegative && quantity > onHand)
        {
            return InventoryErrors.InsufficientStock(onHand, quantity);
        }

        if (bucket is null)
        {
            // Only reachable when negative stock is allowed and this product has no bucket yet.
            var method = await _masterData.GetCostingMethodAsync(productId, ct);
            bucket = CreateAndTrack(productId, warehouseId, "XXX", method);
        }

        var (cost, unitCost) = bucket.CostingMethod == CostingMethod.Fifo
            ? await ConsumeFifoAsync(bucket, productId, warehouseId, quantity, allowNegative, ct)
            : IssueMovingAverage(bucket, quantity, allowNegative);

        return Record(productId, warehouseId, type, quantity, unitCost, cost,
            bucket.Currency, date, source, sourceDocumentId, description, bucket, transferGroupId);
    }

    /// <summary>
    /// Routes a back-dated movement to the right recompute (ADR-0033/0037/0038). Both costing methods use the
    /// <em>cross-bucket</em> replay when the product has a transfer on or after the movement's date (the change
    /// may cascade through it) and the single-bucket replay otherwise.
    /// </summary>
    private async Task<Result<StockMovementResult>> RecomputeBackDatedAsync(
        StockCostBucket bucket, Guid productId, Guid warehouseId, MovementType type, decimal quantity,
        decimal unitCost, DateOnly date, MovementSource source, Guid? sourceDocumentId, string? description,
        Guid? transferGroupId, bool allowNegative, CancellationToken ct)
    {
        var crossBucket = await _transactions.HasTransferOnOrAfterAsync(productId, date, ct);

        if (bucket.CostingMethod == CostingMethod.Fifo)
        {
            return crossBucket
                ? await RecomputeAcrossBucketsFifoAsync(
                    bucket, productId, warehouseId, type, quantity, unitCost, date, source, sourceDocumentId,
                    description, transferGroupId, allowNegative, ct)
                : await RecomputeFifoWithBackDatedMovementAsync(
                    bucket, productId, warehouseId, type, quantity, unitCost, date, source, sourceDocumentId,
                    description, allowNegative, ct);
        }

        return crossBucket
            ? await RecomputeAcrossBucketsAsync(
                bucket, productId, warehouseId, type, quantity, unitCost, date, source, sourceDocumentId,
                description, transferGroupId, allowNegative, ct)
            : await RecomputeWithBackDatedMovementAsync(
                bucket, productId, warehouseId, type, quantity, unitCost, date, source, sourceDocumentId,
                description, allowNegative, ct);
    }

    private static (decimal Cost, decimal UnitCost) IssueMovingAverage(
        StockCostBucket bucket, decimal quantity, bool allowNegative)
    {
        var cost = bucket.Issue(quantity, allowNegative);
        return (cost, bucket.AvgUnitCost);
    }

    /// <summary>
    /// Values a FIFO issue by consuming the oldest open layers first (ADR-0034), applies the takes to
    /// the layer entities, and steps the bucket. When negative stock is allowed and the layers are
    /// exhausted, the shortfall is costed at the bucket's last average (reconciled on the next receipt).
    /// </summary>
    private async Task<(decimal Cost, decimal UnitCost)> ConsumeFifoAsync(
        StockCostBucket bucket, Guid productId, Guid warehouseId, decimal quantity, bool allowNegative,
        CancellationToken ct)
    {
        var openLayers = await _layers.ListOpenForBucketAsync(productId, warehouseId, ct);
        var byId = openLayers.ToDictionary(l => l.Id);

        var consumption = FifoCosting.Consume(
            openLayers.Select(l => new FifoCosting.OpenLayer(l.Id, l.RemainingQty, l.UnitCost)).ToList(),
            quantity);

        foreach (var take in consumption.Takes)
        {
            byId[take.LayerId].Consume(take.Quantity);
        }

        var cost = consumption.TotalCost;
        if (consumption.Shortfall > 0m)
        {
            cost = Math.Round(cost + (consumption.Shortfall * bucket.AvgUnitCost), 4, MidpointRounding.ToEven);
        }

        bucket.IssueFifo(quantity, cost, allowNegative);
        var unitCost = quantity == 0m ? 0m : Math.Round(cost / quantity, 4, MidpointRounding.ToEven);
        return (cost, unitCost);
    }

    public async Task<decimal> GetOnHandAsync(Guid productId, Guid warehouseId, CancellationToken ct)
    {
        var bucket = await _buckets.GetAsync(productId, warehouseId, ct);
        return bucket?.OnHandQty ?? 0m;
    }

    public async Task<bool> IsBackDatedAsync(Guid productId, Guid warehouseId, DateOnly date, CancellationToken ct)
    {
        var max = await _transactions.MaxMovementDateAsync(productId, warehouseId, ct);
        return max is { } latest && date < latest;
    }

    /// <summary>
    /// Inserts a back-dated movement and replays the whole cost bucket in chronological order
    /// (ADR-0033, Option A): every later movement's running average — and each later issue's cost — is
    /// recomputed, the ledger rows are restated in place (the running snapshot is a rebuildable
    /// projection, ADR-0014), the bucket is set to the final state, and one net adjusting journal
    /// corrects the COGS/variance difference of the already-posted later issues (posted journals stay
    /// immutable, ADR-0009). Cross-bucket effects (a later transfer/production movement) are rejected.
    /// </summary>
    private async Task<Result<StockMovementResult>> RecomputeWithBackDatedMovementAsync(
        StockCostBucket bucket, Guid productId, Guid warehouseId, MovementType type, decimal quantity,
        decimal unitCost, DateOnly date, MovementSource source, Guid? sourceDocumentId, string? description,
        bool allowNegative, CancellationToken ct)
    {
        var existing = await _transactions.ListForBucketChronologicalAsync(productId, warehouseId, ct);

        var isOutbound = MovingAverageReplay.IsOutbound(type);
        var newTxn = InventoryTransaction.Record(
            productId, warehouseId, type, quantity, isOutbound ? 0m : unitCost, 0m, bucket.Currency,
            date, source, sourceDocumentId, description, 0m, 0m);

        // Chronological order; the new movement sorts last within its own date (it was entered last).
        var ordered = existing
            .Select(t => (Txn: t, t.MovementDate, Seq: t.CreatedAt))
            .Append((Txn: newTxn, MovementDate: date, Seq: DateTime.MaxValue))
            .OrderBy(x => x.MovementDate)
            .ThenBy(x => x.Seq)
            .Select(x => x.Txn)
            .ToList();

        var input = ordered
            .Select(t => new MovingAverageReplay.Movement(
                t.Id, t.Type, t.Quantity, MovingAverageReplay.IsOutbound(t.Type) ? 0m : t.UnitCost))
            .ToList();

        IReadOnlyList<MovingAverageReplay.Line> lines;
        try
        {
            lines = MovingAverageReplay.Replay(input, allowNegative);
        }
        catch (InvalidOperationException)
        {
            return InventoryErrors.BackDatingWouldGoNegative;
        }

        var lineById = lines.ToDictionary(l => l.TransactionId);
        var insertIndex = ordered.FindIndex(t => ReferenceEquals(t, newTxn));

        // Accumulate the GL correction for already-posted later issues, grouped by their target account.
        decimal cogsDelta = 0m, varianceDelta = 0m;
        for (var i = 0; i < ordered.Count; i++)
        {
            var t = ordered[i];
            if (ReferenceEquals(t, newTxn))
            {
                continue;
            }

            var line = lineById[t.Id];

            if (i > insertIndex && line.IsOutbound)
            {
                var delta = line.TotalCost - t.TotalCost;
                if (delta != 0m)
                {
                    switch (t.Source)
                    {
                        case MovementSource.Sales:
                            cogsDelta += delta;
                            break;
                        case MovementSource.Adjustment:
                            varianceDelta += delta;
                            break;
                        default:
                            // Transfer-out / production-consume move cost into another bucket — out of scope.
                            return InventoryErrors.BackDatingCrossesTransfer;
                    }
                }
            }

            t.Restate(line.UnitCost, line.TotalCost, line.RunningQtyAfter, line.RunningAvgCostAfter);
        }

        var newLine = lineById[newTxn.Id];
        newTxn.Restate(newLine.UnitCost, newLine.TotalCost, newLine.RunningQtyAfter, newLine.RunningAvgCostAfter);
        _transactions.Add(newTxn);

        var last = lines[^1];
        bucket.SetState(last.RunningQtyAfter, last.RunningAvgCostAfter);

        if (cogsDelta != 0m || varianceDelta != 0m)
        {
            var posted = await PostRecomputeDeltaAsync(warehouseId, date, newTxn.Id, cogsDelta, varianceDelta, ct);
            if (posted.IsFailure)
            {
                return posted.Error;
            }
        }

        return new StockMovementResult(newTxn.Id, newLine.TotalCost, newLine.RunningQtyAfter, newLine.RunningAvgCostAfter);
    }

    /// <summary>
    /// FIFO analogue of <see cref="RecomputeWithBackDatedMovementAsync"/> (ADR-0037): inserts a
    /// back-dated movement, replays the whole cost bucket consuming the oldest layers first
    /// (<see cref="FifoReplay"/>), restates every ledger row in place, and — because inserting a
    /// movement changes which layers each later issue consumed — <em>rebuilds every cost layer's
    /// remaining quantity</em> (opening a new layer for a back-dated receipt). One net adjusting journal
    /// corrects the COGS/variance of the already-posted later issues; posted journals stay immutable
    /// (ADR-0009). Cross-bucket effects (a later transfer/production movement) are rejected as for
    /// moving average.
    /// </summary>
    private async Task<Result<StockMovementResult>> RecomputeFifoWithBackDatedMovementAsync(
        StockCostBucket bucket, Guid productId, Guid warehouseId, MovementType type, decimal quantity,
        decimal unitCost, DateOnly date, MovementSource source, Guid? sourceDocumentId, string? description,
        bool allowNegative, CancellationToken ct)
    {
        var existing = await _transactions.ListForBucketChronologicalAsync(productId, warehouseId, ct);

        var isOutbound = FifoReplay.IsOutbound(type);
        var newTxn = InventoryTransaction.Record(
            productId, warehouseId, type, quantity, isOutbound ? 0m : unitCost, 0m, bucket.Currency,
            date, source, sourceDocumentId, description, 0m, 0m);

        // Chronological order; the new movement sorts last within its own date (it was entered last).
        var ordered = existing
            .Select(t => (Txn: t, t.MovementDate, Seq: t.CreatedAt))
            .Append((Txn: newTxn, MovementDate: date, Seq: DateTime.MaxValue))
            .OrderBy(x => x.MovementDate)
            .ThenBy(x => x.Seq)
            .Select(x => x.Txn)
            .ToList();

        var input = ordered
            .Select(t => new FifoReplay.Movement(
                t.Id, t.Type, t.Quantity, FifoReplay.IsOutbound(t.Type) ? 0m : t.UnitCost))
            .ToList();

        IReadOnlyList<FifoReplay.Line> lines;
        try
        {
            lines = FifoReplay.Replay(input, allowNegative);
        }
        catch (InvalidOperationException)
        {
            return InventoryErrors.BackDatingWouldGoNegative;
        }

        var lineById = lines.ToDictionary(l => l.TransactionId);
        var insertIndex = ordered.FindIndex(t => ReferenceEquals(t, newTxn));

        // Accumulate the GL correction for already-posted later issues, grouped by their target account.
        decimal cogsDelta = 0m, varianceDelta = 0m;
        for (var i = 0; i < ordered.Count; i++)
        {
            var t = ordered[i];
            if (ReferenceEquals(t, newTxn))
            {
                continue;
            }

            var line = lineById[t.Id];

            if (i > insertIndex && line.IsOutbound)
            {
                var delta = line.TotalCost - t.TotalCost;
                if (delta != 0m)
                {
                    switch (t.Source)
                    {
                        case MovementSource.Sales:
                            cogsDelta += delta;
                            break;
                        case MovementSource.Adjustment:
                            varianceDelta += delta;
                            break;
                        default:
                            // Transfer-out / production-consume move cost into another bucket — out of scope.
                            return InventoryErrors.BackDatingCrossesTransfer;
                    }
                }
            }

            t.Restate(line.UnitCost, line.TotalCost, line.RunningQtyAfter, line.RunningAvgCostAfter);
        }

        var newLine = lineById[newTxn.Id];
        newTxn.Restate(newLine.UnitCost, newLine.TotalCost, newLine.RunningQtyAfter, newLine.RunningAvgCostAfter);
        _transactions.Add(newTxn);

        // Rebuild every layer's remaining quantity from the replay. Existing inbound movements each own a
        // layer (created at receipt); a back-dated inbound opens a new one. A later issue may re-consume a
        // layer that was previously fully spent, so all layers are restated — including 0-remaining ones.
        var existingLayers = await _layers.ListAllForBucketAsync(productId, warehouseId, ct);
        var layerByTxn = existingLayers.ToDictionary(l => l.SourceTransactionId);
        foreach (var line in lines)
        {
            if (line.IsOutbound)
            {
                continue;
            }

            if (line.TransactionId == newTxn.Id)
            {
                var newLayer = StockCostLayer.Create(
                    productId, warehouseId, bucket.Currency, newTxn.Id, date, unitCost, quantity);
                newLayer.Restate(line.LayerRemainingQty);
                _layers.Add(newLayer);
            }
            else if (layerByTxn.TryGetValue(line.TransactionId, out var layer))
            {
                layer.Restate(line.LayerRemainingQty);
            }
        }

        var last = lines[^1];
        bucket.SetState(last.RunningQtyAfter, last.RunningAvgCostAfter);

        if (cogsDelta != 0m || varianceDelta != 0m)
        {
            var posted = await PostRecomputeDeltaAsync(warehouseId, date, newTxn.Id, cogsDelta, varianceDelta, ct);
            if (posted.IsFailure)
            {
                return posted.Error;
            }
        }

        return new StockMovementResult(newTxn.Id, newLine.TotalCost, newLine.RunningQtyAfter, newLine.RunningAvgCostAfter);
    }

    /// <summary>
    /// Posts the net inventory-revaluation journal correcting the COGS/variance of later issues after a
    /// back-dated recompute. Accounts resolve from the posting-rule engine (ADR-0024). Each account line
    /// is a debit when its delta is positive, a credit when negative; Inventory balances the total. The
    /// journal is dated at the back-dated movement (in the open period), so the GL period guard applies.
    /// </summary>
    private async Task<Result<Guid>> PostRecomputeDeltaAsync(
        Guid warehouseId, DateOnly date, Guid sourceTransactionId, decimal cogsDelta, decimal varianceDelta,
        CancellationToken ct)
    {
        var inventory = await _accounts.ResolveAsync(
            "InventoryRecompute", PostingKeys.Inventory, new PostingSelector(WarehouseId: warehouseId), ct);
        if (inventory.IsFailure) return inventory.Error;

        var lines = new List<LedgerLine>();

        if (cogsDelta != 0m)
        {
            var cogs = await _accounts.ResolveAsync("InventoryRecompute", PostingKeys.Cogs, PostingSelector.None, ct);
            if (cogs.IsFailure) return cogs.Error;
            lines.Add(new LedgerLine(cogs.Value, Debit(cogsDelta), Credit(cogsDelta), "COGS recompute (back-dated)"));
        }

        if (varianceDelta != 0m)
        {
            var variance = await _accounts.ResolveAsync(
                "InventoryRecompute", PostingKeys.InventoryVariance, PostingSelector.None, ct);
            if (variance.IsFailure) return variance.Error;
            lines.Add(new LedgerLine(variance.Value, Debit(varianceDelta), Credit(varianceDelta), "Variance recompute (back-dated)"));
        }

        var net = cogsDelta + varianceDelta;
        lines.Add(new LedgerLine(inventory.Value, Debit(-net), Credit(-net), "Inventory recompute (back-dated)"));

        return await _ledger.PostAsync(
            new LedgerPostingRequest(
                date, LedgerSource.StockAdjustment, sourceTransactionId, "Back-dated inventory recompute", lines),
            ct);
    }

    private static decimal Debit(decimal delta) => delta > 0m ? delta : 0m;
    private static decimal Credit(decimal delta) => delta < 0m ? -delta : 0m;

    private StockCostBucket CreateAndTrack(
        Guid productId, Guid warehouseId, string currency, CostingMethod method)
    {
        var bucket = StockCostBucket.Create(productId, warehouseId, currency, method);
        _buckets.Add(bucket);
        return bucket;
    }

    private Result<StockMovementResult> Record(
        Guid productId, Guid warehouseId, MovementType type, decimal quantity, decimal unitCost,
        decimal totalCost, string currency, DateOnly date, MovementSource source, Guid? sourceDocumentId,
        string? description, StockCostBucket bucket, Guid? transferGroupId = null)
    {
        var txn = InventoryTransaction.Record(
            productId, warehouseId, type, quantity, unitCost, totalCost, currency, date, source,
            sourceDocumentId, description, bucket.OnHandQty, bucket.AvgUnitCost, transferGroupId);
        _transactions.Add(txn);

        return new StockMovementResult(txn.Id, totalCost, bucket.OnHandQty, bucket.AvgUnitCost);
    }

    /// <summary>
    /// Cross-bucket back-dated moving-average recompute (ADR-0038): builds the single back-dated movement
    /// and applies it across buckets via <see cref="ApplyCrossBucketMovingAverageAsync"/>.
    /// </summary>
    private async Task<Result<StockMovementResult>> RecomputeAcrossBucketsAsync(
        StockCostBucket originBucket, Guid productId, Guid warehouseId, MovementType type, decimal quantity,
        decimal unitCost, DateOnly date, MovementSource source, Guid? sourceDocumentId, string? description,
        Guid? transferGroupId, bool allowNegative, CancellationToken ct)
    {
        var isOutbound = CrossBucketMovingAverageReplay.IsOutbound(type);
        var newTxn = InventoryTransaction.Record(
            productId, warehouseId, type, quantity, isOutbound ? 0m : unitCost, 0m, originBucket.Currency,
            date, source, sourceDocumentId, description, 0m, 0m, transferGroupId);

        var applied = await ApplyCrossBucketMovingAverageAsync(
            productId, warehouseId, date, new[] { newTxn }, allowNegative, ct);
        if (applied.IsFailure)
        {
            return applied.Error;
        }

        var line = applied.Value[newTxn.Id];
        return new StockMovementResult(newTxn.Id, line.TotalCost, line.RunningQtyAfter, line.RunningAvgCostAfter);
    }

    /// <summary>
    /// The shared cross-bucket <em>moving-average</em> apply (ADR-0038). Inserts <paramref name="newTxns"/>
    /// (one for a back-dated movement; the linked out/in pair for a back-dated transfer document) into the
    /// product's <em>global</em> chronological stream (all warehouses), replays it with
    /// <see cref="CrossBucketMovingAverageReplay"/> — threading each transfer's cost from its source leg to
    /// its destination — restates every existing ledger row in place (a rebuildable projection, ADR-0014),
    /// adds the new rows, sets each affected bucket's final state, and posts <em>one</em> net adjusting
    /// journal for the COGS (later <c>Sales</c>) and variance (later <c>Adjustment</c>) difference across
    /// all buckets. Returns each replayed line by transaction id. A legacy unlinked transfer, or any
    /// production movement, cannot be threaded and is rejected.
    /// </summary>
    private async Task<Result<IReadOnlyDictionary<Guid, CrossBucketMovingAverageReplay.Line>>>
        ApplyCrossBucketMovingAverageAsync(
            Guid productId, Guid journalWarehouseId, DateOnly date,
            IReadOnlyList<InventoryTransaction> newTxns, bool allowNegative, CancellationToken ct)
    {
        var existing = await _transactions.ListForProductChronologicalAsync(productId, ct);

        var rejection = ValidateCrossBucketEligible(existing);
        if (rejection is not null)
        {
            return rejection;
        }

        var newSet = new HashSet<InventoryTransaction>(newTxns);
        var ordered = OrderForCrossBucketReplay(existing, newTxns, date);

        var input = ordered
            .Select(t => new CrossBucketMovingAverageReplay.Movement(
                t.Id, t.WarehouseId, t.Type, t.Quantity,
                CrossBucketMovingAverageReplay.IsOutbound(t.Type) ? 0m : t.UnitCost, t.TransferGroupId))
            .ToList();

        IReadOnlyList<CrossBucketMovingAverageReplay.Line> lines;
        try
        {
            lines = CrossBucketMovingAverageReplay.Replay(input, allowNegative);
        }
        catch (InvalidOperationException)
        {
            return InventoryErrors.BackDatingWouldGoNegative;
        }

        var lineById = lines.ToDictionary(l => l.TransactionId);

        // Restate every existing row and accumulate the GL correction for already-posted later issues.
        decimal cogsDelta = 0m, varianceDelta = 0m;
        foreach (var t in ordered)
        {
            if (newSet.Contains(t))
            {
                continue;
            }

            var line = lineById[t.Id];
            if (line.IsOutbound)
            {
                var delta = ClassifyOutboundDelta(t, line.TotalCost, ref cogsDelta, ref varianceDelta);
                if (delta is not null)
                {
                    return delta;
                }
            }

            t.Restate(line.UnitCost, line.TotalCost, line.RunningQtyAfter, line.RunningAvgCostAfter);
        }

        foreach (var t in newTxns)
        {
            var line = lineById[t.Id];
            t.Restate(line.UnitCost, line.TotalCost, line.RunningQtyAfter, line.RunningAvgCostAfter);
            _transactions.Add(t);
        }

        await SetFinalBucketStatesAsync(
            productId, lines.Select(l => (l.WarehouseId, l.RunningQtyAfter, l.RunningAvgCostAfter)).ToList(), ct);

        if (cogsDelta != 0m || varianceDelta != 0m)
        {
            var posted = await PostRecomputeDeltaAsync(journalWarehouseId, date, newTxns[0].Id, cogsDelta, varianceDelta, ct);
            if (posted.IsFailure)
            {
                return posted.Error;
            }
        }

        return Result<IReadOnlyDictionary<Guid, CrossBucketMovingAverageReplay.Line>>.Of(lineById);
    }

    /// <summary>Rejects a cross-bucket recompute whose stream contains a leg the replay cannot thread: a
    /// legacy <em>unlinked</em> transfer, or any production movement (out of scope). Returns the error, or
    /// null when eligible.</summary>
    private static Error? ValidateCrossBucketEligible(IReadOnlyList<InventoryTransaction> existing)
    {
        foreach (var t in existing)
        {
            if ((t.Type is MovementType.TransferOut or MovementType.TransferIn) && t.TransferGroupId is null)
            {
                return InventoryErrors.BackDatingCrossesTransfer;
            }

            if (t.Type is MovementType.ProductionConsume or MovementType.ProductionReceive)
            {
                return InventoryErrors.BackDatingCrossesTransfer;
            }
        }

        return null;
    }

    /// <summary>Global chronological order for a cross-bucket replay: existing movements by (date, insertion
    /// order), then the new movements last within <paramref name="date"/>, with a transfer-in ordered after
    /// its paired transfer-out so the cost threads forward.</summary>
    private static List<InventoryTransaction> OrderForCrossBucketReplay(
        IReadOnlyList<InventoryTransaction> existing, IReadOnlyList<InventoryTransaction> newTxns, DateOnly date) =>
        existing
            .Select(t => (Txn: t, t.MovementDate, Seq: t.CreatedAt))
            .Concat(newTxns.Select(t => (Txn: t, MovementDate: date, Seq: DateTime.MaxValue)))
            .OrderBy(x => x.MovementDate)
            .ThenBy(x => x.Seq)
            .ThenBy(x => x.Txn.Type == MovementType.TransferIn ? 1 : 0)
            .Select(x => x.Txn)
            .ToList();

    /// <summary>Classifies an already-posted later issue's cost change onto the COGS (Sales) or variance
    /// (Adjustment) accumulator; a transfer leg is GL-neutral. Returns an error for an unknown outbound
    /// source, else null.</summary>
    private static Error? ClassifyOutboundDelta(
        InventoryTransaction t, decimal newTotalCost, ref decimal cogsDelta, ref decimal varianceDelta)
    {
        var delta = newTotalCost - t.TotalCost;
        if (delta == 0m)
        {
            return null;
        }

        switch (t.Source)
        {
            case MovementSource.Sales:
                cogsDelta += delta;
                return null;
            case MovementSource.Adjustment:
                varianceDelta += delta;
                return null;
            case MovementSource.Transfer:
                return null; // GL-neutral; the value change threads into the destination bucket
            default:
                return InventoryErrors.BackDatingCrossesTransfer;
        }
    }

    /// <summary>Sets each affected bucket to its final running quantity/average — the last replayed line for
    /// its warehouse (lines arrive in global order, so the last one per warehouse wins).</summary>
    private async Task SetFinalBucketStatesAsync(
        Guid productId, IReadOnlyList<(Guid WarehouseId, decimal Qty, decimal Avg)> lines, CancellationToken ct)
    {
        var buckets = (await _buckets.ListForProductAsync(productId, ct)).ToDictionary(b => b.WarehouseId);
        var lastByWarehouse = new Dictionary<Guid, (decimal Qty, decimal Avg)>();
        foreach (var l in lines)
        {
            lastByWarehouse[l.WarehouseId] = (l.Qty, l.Avg);
        }

        foreach (var (wh, state) in lastByWarehouse)
        {
            if (buckets.TryGetValue(wh, out var b))
            {
                b.SetState(state.Qty, state.Avg);
            }
        }
    }

    /// <summary>
    /// Cross-bucket back-dated <em>FIFO</em> recompute (ADR-0038): builds the single back-dated movement and
    /// applies it across buckets via <see cref="ApplyCrossBucketFifoAsync"/>.
    /// </summary>
    private async Task<Result<StockMovementResult>> RecomputeAcrossBucketsFifoAsync(
        StockCostBucket originBucket, Guid productId, Guid warehouseId, MovementType type, decimal quantity,
        decimal unitCost, DateOnly date, MovementSource source, Guid? sourceDocumentId, string? description,
        Guid? transferGroupId, bool allowNegative, CancellationToken ct)
    {
        var isOutbound = CrossBucketFifoReplay.IsOutbound(type);
        var newTxn = InventoryTransaction.Record(
            productId, warehouseId, type, quantity, isOutbound ? 0m : unitCost, 0m, originBucket.Currency,
            date, source, sourceDocumentId, description, 0m, 0m, transferGroupId);

        var applied = await ApplyCrossBucketFifoAsync(
            productId, warehouseId, date, new[] { newTxn }, allowNegative, ct);
        if (applied.IsFailure)
        {
            return applied.Error;
        }

        var line = applied.Value[newTxn.Id];
        return new StockMovementResult(newTxn.Id, line.TotalCost, line.RunningQtyAfter, line.RunningAvgCostAfter);
    }

    /// <summary>
    /// The shared cross-bucket <em>FIFO</em> apply (ADR-0038) — the FIFO analogue of
    /// <see cref="ApplyCrossBucketMovingAverageAsync"/>. Inserts <paramref name="newTxns"/> into the
    /// product's global chronological stream, replays with <see cref="CrossBucketFifoReplay"/> — consuming
    /// each bucket's oldest layers first and threading a transfer's cost from its source leg into the single
    /// blended layer its destination leg opens — restates every existing ledger row in place, rebuilds every
    /// cost layer's remaining quantity across all affected buckets (a new inbound movement opens a layer),
    /// adds the new rows, sets each bucket's final state, and posts <em>one</em> net adjusting journal.
    /// Returns each replayed line by transaction id. A legacy unlinked transfer, or any production movement,
    /// is rejected. A receipt layer's unit cost is an immutable fact (only its remainder is restated); a
    /// transfer-in layer's cost is derived from the (now-restated) source, so both are rebuilt.
    /// </summary>
    private async Task<Result<IReadOnlyDictionary<Guid, CrossBucketFifoReplay.Line>>>
        ApplyCrossBucketFifoAsync(
            Guid productId, Guid journalWarehouseId, DateOnly date,
            IReadOnlyList<InventoryTransaction> newTxns, bool allowNegative, CancellationToken ct)
    {
        var existing = await _transactions.ListForProductChronologicalAsync(productId, ct);

        var rejection = ValidateCrossBucketEligible(existing);
        if (rejection is not null)
        {
            return rejection;
        }

        var newSet = new HashSet<InventoryTransaction>(newTxns);
        var ordered = OrderForCrossBucketReplay(existing, newTxns, date);

        var input = ordered
            .Select(t => new CrossBucketFifoReplay.Movement(
                t.Id, t.WarehouseId, t.Type, t.Quantity,
                CrossBucketFifoReplay.IsOutbound(t.Type) ? 0m : t.UnitCost, t.TransferGroupId))
            .ToList();

        IReadOnlyList<CrossBucketFifoReplay.Line> lines;
        try
        {
            lines = CrossBucketFifoReplay.Replay(input, allowNegative);
        }
        catch (InvalidOperationException)
        {
            return InventoryErrors.BackDatingWouldGoNegative;
        }

        var lineById = lines.ToDictionary(l => l.TransactionId);

        decimal cogsDelta = 0m, varianceDelta = 0m;
        foreach (var t in ordered)
        {
            if (newSet.Contains(t))
            {
                continue;
            }

            var line = lineById[t.Id];
            if (line.IsOutbound)
            {
                var delta = ClassifyOutboundDelta(t, line.TotalCost, ref cogsDelta, ref varianceDelta);
                if (delta is not null)
                {
                    return delta;
                }
            }

            t.Restate(line.UnitCost, line.TotalCost, line.RunningQtyAfter, line.RunningAvgCostAfter);
        }

        foreach (var t in newTxns)
        {
            var line = lineById[t.Id];
            t.Restate(line.UnitCost, line.TotalCost, line.RunningQtyAfter, line.RunningAvgCostAfter);
            _transactions.Add(t);
        }

        // Rebuild every existing layer's remainder (transfer-in layers also have their derived cost rebuilt),
        // then open a fresh layer for each new inbound movement (a back-dated receipt or a transfer-in).
        var newIds = newTxns.Select(t => t.Id).ToHashSet();
        var existingLayers = await _layers.ListAllForProductAsync(productId, ct);
        var layerByTxn = existingLayers.ToDictionary(l => l.SourceTransactionId);
        foreach (var line in lines)
        {
            if (line.IsOutbound || newIds.Contains(line.TransactionId))
            {
                continue;
            }

            if (layerByTxn.TryGetValue(line.TransactionId, out var layer))
            {
                if (line.Type == MovementType.TransferIn)
                {
                    layer.RestateCost(line.UnitCost, line.LayerRemainingQty);
                }
                else
                {
                    layer.Restate(line.LayerRemainingQty);
                }
            }
        }

        foreach (var t in newTxns)
        {
            if (CrossBucketFifoReplay.IsOutbound(t.Type))
            {
                continue;
            }

            var line = lineById[t.Id];
            var newLayer = StockCostLayer.Create(productId, t.WarehouseId, t.Currency, t.Id, date, line.UnitCost, t.Quantity);
            newLayer.Restate(line.LayerRemainingQty);
            _layers.Add(newLayer);
        }

        await SetFinalBucketStatesAsync(
            productId, lines.Select(l => (l.WarehouseId, l.RunningQtyAfter, l.RunningAvgCostAfter)).ToList(), ct);

        if (cogsDelta != 0m || varianceDelta != 0m)
        {
            var posted = await PostRecomputeDeltaAsync(journalWarehouseId, date, newTxns[0].Id, cogsDelta, varianceDelta, ct);
            if (posted.IsFailure)
            {
                return posted.Error;
            }
        }

        return Result<IReadOnlyDictionary<Guid, CrossBucketFifoReplay.Line>>.Of(lineById);
    }

    /// <summary>
    /// Cross-bucket recompute of a directly back-dated <em>transfer document</em> (ADR-0038 Phase 2b):
    /// inserts BOTH legs (a linked <see cref="MovementType.TransferOut"/>/<c>TransferIn</c> pair) into the
    /// product's global stream and replays across buckets in one atomic pass — so the cost the back-dated
    /// transfer carries, and every later issue in either bucket it disturbs, is recomputed and a single net
    /// journal posted. Routes to the FIFO or moving-average engine by the product's costing method; the
    /// negative-stock policy and functional currency are resolved here (as the forward transfer path does).
    /// </summary>
    public async Task<Result<TransferMovementResult>> TransferBackDatedAsync(
        Guid productId, Guid fromWarehouseId, Guid toWarehouseId, decimal quantity, DateOnly date, CancellationToken ct)
    {
        if (quantity <= 0)
        {
            return InventoryErrors.InvalidQuantity;
        }

        var allowNegative = await _companies.GetBoolSettingAsync(
            _tenant.CompanyId, CompanySettingKeys.AllowNegativeStock, false, ct);
        var company = await _companies.GetAsync(_tenant.CompanyId, ct);
        var currency = company?.FunctionalCurrency ?? "XXX";

        var sourceBucket = await _buckets.GetAsync(productId, fromWarehouseId, ct);
        var method = sourceBucket?.CostingMethod ?? await _masterData.GetCostingMethodAsync(productId, ct);

        // One TransferGroupId links the legs so the replay threads the carried cost (ADR-0038).
        var group = Guid.NewGuid();
        var newOut = InventoryTransaction.Record(
            productId, fromWarehouseId, MovementType.TransferOut, quantity, 0m, 0m, currency,
            date, MovementSource.Transfer, null, "Transfer out", 0m, 0m, group);
        var newIn = InventoryTransaction.Record(
            productId, toWarehouseId, MovementType.TransferIn, quantity, 0m, 0m, currency,
            date, MovementSource.Transfer, null, "Transfer in", 0m, 0m, group);
        var newTxns = new[] { newOut, newIn };

        decimal outTotal;
        if (method == CostingMethod.Fifo)
        {
            var applied = await ApplyCrossBucketFifoAsync(productId, fromWarehouseId, date, newTxns, allowNegative, ct);
            if (applied.IsFailure) return applied.Error;
            outTotal = applied.Value[newOut.Id].TotalCost;
        }
        else
        {
            var applied = await ApplyCrossBucketMovingAverageAsync(productId, fromWarehouseId, date, newTxns, allowNegative, ct);
            if (applied.IsFailure) return applied.Error;
            outTotal = applied.Value[newOut.Id].TotalCost;
        }

        var unitCost = quantity == 0m ? 0m : outTotal / quantity;
        return new TransferMovementResult(newOut.Id, newIn.Id, unitCost);
    }
}
