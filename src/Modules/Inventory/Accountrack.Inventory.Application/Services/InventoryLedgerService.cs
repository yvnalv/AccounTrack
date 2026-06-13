using Accountrack.Inventory.Application.Abstractions;
using Accountrack.Inventory.Domain;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Inventory.Application.Services;

/// <summary>Default <see cref="IInventoryLedger"/> over the bucket + transaction repositories.</summary>
public sealed class InventoryLedgerService : IInventoryLedger
{
    private readonly IStockBucketRepository _buckets;
    private readonly IInventoryTransactionRepository _transactions;

    public InventoryLedgerService(IStockBucketRepository buckets, IInventoryTransactionRepository transactions)
    {
        _buckets = buckets;
        _transactions = transactions;
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
            bucket = StockCostBucket.Create(productId, warehouseId, currency);
            _buckets.Add(bucket);
        }

        var totalCost = bucket.Receive(quantity, unitCost);

        return Record(productId, warehouseId, type, quantity, unitCost, totalCost,
            bucket.Currency, date, source, sourceDocumentId, description, bucket);
    }

    public async Task<Result<StockMovementResult>> IssueAsync(
        Guid productId, Guid warehouseId, decimal quantity,
        DateOnly date, MovementType type, MovementSource source, Guid? sourceDocumentId, string? description,
        bool allowNegative, CancellationToken ct)
    {
        if (quantity <= 0)
        {
            return InventoryErrors.InvalidQuantity;
        }

        var bucket = await _buckets.GetAsync(productId, warehouseId, ct);
        var onHand = bucket?.OnHandQty ?? 0m;
        if (!allowNegative && quantity > onHand)
        {
            return InventoryErrors.InsufficientStock(onHand, quantity);
        }

        bucket ??= CreateAndTrack(productId, warehouseId, "XXX"); // only reachable when negative allowed
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

    private StockCostBucket CreateAndTrack(Guid productId, Guid warehouseId, string currency)
    {
        var bucket = StockCostBucket.Create(productId, warehouseId, currency);
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
