using Accountrack.Inventory.Domain;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Inventory.Application.Abstractions;

public interface IStockBucketRepository
{
    Task<StockCostBucket?> GetAsync(Guid productId, Guid warehouseId, CancellationToken ct);
    Task<IReadOnlyList<StockCostBucket>> ListAsync(CancellationToken ct);
    void Add(StockCostBucket bucket);
}

public interface IInventoryTransactionRepository
{
    void Add(InventoryTransaction transaction);
    Task<IReadOnlyList<InventoryTransaction>> ListAsync(Guid productId, Guid? warehouseId, CancellationToken ct);

    /// <summary>
    /// The tracked movements for a single cost bucket in <em>chronological</em> order
    /// (MovementDate, then insertion order), for back-dated moving-average replay (ADR-0033). Returns
    /// tracked entities so a recompute can restate them in place.
    /// </summary>
    Task<IReadOnlyList<InventoryTransaction>> ListForBucketChronologicalAsync(
        Guid productId, Guid warehouseId, CancellationToken ct);

    /// <summary>The latest movement date recorded for a cost bucket, or null if it has none — used to
    /// detect a back-dated movement (ADR-0033).</summary>
    Task<DateOnly?> MaxMovementDateAsync(Guid productId, Guid warehouseId, CancellationToken ct);
}

public interface IStockCostLayerRepository
{
    void Add(StockCostLayer layer);

    /// <summary>Open (RemainingQty &gt; 0) FIFO layers for a bucket, oldest-first (MovementDate, then
    /// insertion order) — the consumption order for an issue (ADR-0034). Returns tracked entities so a
    /// consumed layer's remaining quantity persists.</summary>
    Task<IReadOnlyList<StockCostLayer>> ListOpenForBucketAsync(Guid productId, Guid warehouseId, CancellationToken ct);

    /// <summary>All open layers for the tenant (RemainingQty &gt; 0), for FIFO inventory valuation.</summary>
    Task<IReadOnlyList<StockCostLayer>> ListOpenAsync(CancellationToken ct);
}

public interface IInventoryUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}

public sealed record StockMovementResult(
    Guid TransactionId, decimal CostApplied, decimal RunningQtyAfter, decimal RunningAvgCostAfter);

/// <summary>
/// The stock ledger: applies receipts and issues, maintaining the moving-average bucket and writing
/// the immutable ledger entry (ADR-0014/0015). Does not save — the calling use case owns the unit
/// of work, so a movement can be made atomic with its document (later: Sales/Purchasing).
/// </summary>
public interface IInventoryLedger
{
    Task<Result<StockMovementResult>> ReceiveAsync(
        Guid productId, Guid warehouseId, string currency, decimal quantity, decimal unitCost,
        DateOnly date, MovementType type, MovementSource source, Guid? sourceDocumentId, string? description,
        CancellationToken ct);

    Task<Result<StockMovementResult>> IssueAsync(
        Guid productId, Guid warehouseId, decimal quantity,
        DateOnly date, MovementType type, MovementSource source, Guid? sourceDocumentId, string? description,
        CancellationToken ct);

    Task<decimal> GetOnHandAsync(Guid productId, Guid warehouseId, CancellationToken ct);

    /// <summary>True if <paramref name="date"/> falls before the bucket's latest movement — i.e. this
    /// movement is back-dated and (for supported paths) triggers a moving-average recompute (ADR-0033).</summary>
    Task<bool> IsBackDatedAsync(Guid productId, Guid warehouseId, DateOnly date, CancellationToken ct);
}
