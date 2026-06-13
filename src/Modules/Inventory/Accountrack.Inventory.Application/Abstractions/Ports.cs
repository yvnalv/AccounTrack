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
        bool allowNegative, CancellationToken ct);

    Task<decimal> GetOnHandAsync(Guid productId, Guid warehouseId, CancellationToken ct);
}
