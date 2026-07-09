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
    /// Routes a back-dated movement to the right recompute (ADR-0033/0037/0038). FIFO uses the
    /// single-bucket layer-reconstruction replay (cross-bucket FIFO is not yet supported and is rejected
    /// inside it). Moving average uses the <em>cross-bucket</em> replay when the product has a transfer on
    /// or after the movement's date (the change may cascade through it), otherwise the single-bucket replay.
    /// </summary>
    private async Task<Result<StockMovementResult>> RecomputeBackDatedAsync(
        StockCostBucket bucket, Guid productId, Guid warehouseId, MovementType type, decimal quantity,
        decimal unitCost, DateOnly date, MovementSource source, Guid? sourceDocumentId, string? description,
        Guid? transferGroupId, bool allowNegative, CancellationToken ct)
    {
        if (bucket.CostingMethod == CostingMethod.Fifo)
        {
            return await RecomputeFifoWithBackDatedMovementAsync(
                bucket, productId, warehouseId, type, quantity, unitCost, date, source, sourceDocumentId,
                description, allowNegative, ct);
        }

        if (await _transactions.HasTransferOnOrAfterAsync(productId, date, ct))
        {
            return await RecomputeAcrossBucketsAsync(
                bucket, productId, warehouseId, type, quantity, unitCost, date, source, sourceDocumentId,
                description, transferGroupId, allowNegative, ct);
        }

        return await RecomputeWithBackDatedMovementAsync(
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
    /// Cross-bucket back-dated moving-average recompute (ADR-0038). Inserts the back-dated movement into
    /// the product's <em>global</em> chronological movement stream (all warehouses), replays it with
    /// <see cref="CrossBucketMovingAverageReplay"/> — threading each transfer's cost from its source leg
    /// to its destination — restates every ledger row in place (a rebuildable projection, ADR-0014), sets
    /// each affected bucket's final state, and posts <em>one</em> net adjusting journal for the COGS
    /// (later <c>Sales</c> issues) and variance (later <c>Adjustment</c> issues) difference across all
    /// buckets. Transfers are GL-neutral in themselves (value threads to the destination). A legacy
    /// unlinked transfer, or any production movement, cannot be threaded and is rejected.
    /// </summary>
    private async Task<Result<StockMovementResult>> RecomputeAcrossBucketsAsync(
        StockCostBucket originBucket, Guid productId, Guid warehouseId, MovementType type, decimal quantity,
        decimal unitCost, DateOnly date, MovementSource source, Guid? sourceDocumentId, string? description,
        Guid? transferGroupId, bool allowNegative, CancellationToken ct)
    {
        var existing = await _transactions.ListForProductChronologicalAsync(productId, ct);

        // Every transfer leg must be linked (so its cost can be threaded) and there must be no production
        // movements (out of scope) — otherwise the cross-bucket replay cannot be trusted, so reject.
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

        var isOutbound = CrossBucketMovingAverageReplay.IsOutbound(type);
        var newTxn = InventoryTransaction.Record(
            productId, warehouseId, type, quantity, isOutbound ? 0m : unitCost, 0m, originBucket.Currency,
            date, source, sourceDocumentId, description, 0m, 0m, transferGroupId);

        // Global chronological order; the new movement sorts last within its own date, and a transfer-in
        // never precedes its paired transfer-out (they can share a CreatedAt) so the cost threads forward.
        var ordered = existing
            .Select(t => (Txn: t, t.MovementDate, Seq: t.CreatedAt))
            .Append((Txn: newTxn, MovementDate: date, Seq: DateTime.MaxValue))
            .OrderBy(x => x.MovementDate)
            .ThenBy(x => x.Seq)
            .ThenBy(x => x.Txn.Type == MovementType.TransferIn ? 1 : 0)
            .Select(x => x.Txn)
            .ToList();

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

        // Accumulate the GL correction for already-posted later issues across all buckets.
        decimal cogsDelta = 0m, varianceDelta = 0m;
        foreach (var t in ordered)
        {
            if (ReferenceEquals(t, newTxn))
            {
                continue;
            }

            var line = lineById[t.Id];
            if (line.IsOutbound)
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
                        case MovementSource.Transfer:
                            break; // GL-neutral; the value change is threaded into the destination bucket
                        default:
                            return InventoryErrors.BackDatingCrossesTransfer; // unknown outbound source
                    }
                }
            }

            t.Restate(line.UnitCost, line.TotalCost, line.RunningQtyAfter, line.RunningAvgCostAfter);
        }

        var newLine = lineById[newTxn.Id];
        newTxn.Restate(newLine.UnitCost, newLine.TotalCost, newLine.RunningQtyAfter, newLine.RunningAvgCostAfter);
        _transactions.Add(newTxn);

        // Set each affected bucket to its final running state (the last line for its warehouse).
        var buckets = (await _buckets.ListForProductAsync(productId, ct)).ToDictionary(b => b.WarehouseId);
        var lastByWarehouse = new Dictionary<Guid, CrossBucketMovingAverageReplay.Line>();
        foreach (var line in lines)
        {
            lastByWarehouse[line.WarehouseId] = line; // lines are in order, so the last one wins per warehouse
        }

        foreach (var (wh, line) in lastByWarehouse)
        {
            if (buckets.TryGetValue(wh, out var b))
            {
                b.SetState(line.RunningQtyAfter, line.RunningAvgCostAfter);
            }
        }

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
}
