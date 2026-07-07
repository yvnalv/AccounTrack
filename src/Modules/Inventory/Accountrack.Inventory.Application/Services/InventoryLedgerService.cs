using Accountrack.Application.Abstractions.Context;
using Accountrack.Inventory.Application.Abstractions;
using Accountrack.Inventory.Domain;
using Accountrack.Modules.Contracts.Accounting;
using Accountrack.Modules.Contracts.Company;
using Accountrack.Modules.Contracts.MasterData;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Inventory.Application.Services;

/// <summary>Default <see cref="IInventoryLedger"/> over the bucket + transaction repositories.</summary>
public sealed class InventoryLedgerService : IInventoryLedger
{
    private readonly IStockBucketRepository _buckets;
    private readonly IInventoryTransactionRepository _transactions;
    private readonly ICompanyDirectory _companies;
    private readonly IMasterDataLookup _masterData;
    private readonly ITenantContext _tenant;
    private readonly IGeneralLedgerPoster _ledger;
    private readonly IPostingAccountResolver _accounts;

    public InventoryLedgerService(
        IStockBucketRepository buckets, IInventoryTransactionRepository transactions,
        ICompanyDirectory companies, IMasterDataLookup masterData, ITenantContext tenant,
        IGeneralLedgerPoster ledger, IPostingAccountResolver accounts)
    {
        _buckets = buckets;
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
        CancellationToken ct)
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
            return await RecomputeWithBackDatedMovementAsync(
                bucket, productId, warehouseId, type, quantity, unitCost, date, source, sourceDocumentId, description,
                allowNegative: false, ct);
        }

        var totalCost = bucket.Receive(quantity, unitCost);
        return Record(productId, warehouseId, type, quantity, unitCost, totalCost,
            bucket.Currency, date, source, sourceDocumentId, description, bucket);
    }

    public async Task<Result<StockMovementResult>> IssueAsync(
        Guid productId, Guid warehouseId, decimal quantity,
        DateOnly date, MovementType type, MovementSource source, Guid? sourceDocumentId, string? description,
        CancellationToken ct)
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
            return await RecomputeWithBackDatedMovementAsync(
                bucket, productId, warehouseId, type, quantity, unitCost: 0m, date, source, sourceDocumentId, description,
                allowNegative, ct);
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
        var cost = bucket.Issue(quantity, allowNegative);
        var unitCost = bucket.AvgUnitCost;

        return Record(productId, warehouseId, type, quantity, unitCost, cost,
            bucket.Currency, date, source, sourceDocumentId, description, bucket);
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
        Guid productId, Guid warehouseId, string currency, Accountrack.SharedKernel.Inventory.CostingMethod method)
    {
        var bucket = StockCostBucket.Create(productId, warehouseId, currency, method);
        _buckets.Add(bucket);
        return bucket;
    }

    private Result<StockMovementResult> Record(
        Guid productId, Guid warehouseId, MovementType type, decimal quantity, decimal unitCost,
        decimal totalCost, string currency, DateOnly date, MovementSource source, Guid? sourceDocumentId,
        string? description, StockCostBucket bucket)
    {
        var txn = InventoryTransaction.Record(
            productId, warehouseId, type, quantity, unitCost, totalCost, currency, date, source,
            sourceDocumentId, description, bucket.OnHandQty, bucket.AvgUnitCost);
        _transactions.Add(txn);

        return new StockMovementResult(txn.Id, totalCost, bucket.OnHandQty, bucket.AvgUnitCost);
    }
}
