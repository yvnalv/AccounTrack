using Accountrack.SharedKernel.Results;

namespace Accountrack.Modules.Contracts.Inventory;

/// <summary>The result of a stock movement, including the cost applied (for the COGS/valuation journal).</summary>
public sealed record StockMovementResult(
    Guid TransactionId, decimal CostApplied, decimal RunningQtyAfter, decimal RunningAvgCostAfter);

/// <summary>
/// Public contract for posting stock movements as part of another module's atomic transaction
/// (INTEGRATION_EVENTS.md §3). Implementations are save-less — they enlist in the caller's
/// <see cref="Transactions.ICrossModuleUnitOfWork"/> so the movement commits with the document
/// and its GL journal, or not at all.
/// </summary>
public interface IInventoryPosting
{
    Task<Result<StockMovementResult>> ReceiveAsync(
        Guid productId, Guid warehouseId, string currency, decimal quantity, decimal unitCost,
        DateOnly date, Guid sourceDocumentId, string? description, CancellationToken ct);

    Task<Result<StockMovementResult>> IssueAsync(
        Guid productId, Guid warehouseId, decimal quantity,
        DateOnly date, Guid sourceDocumentId, string? description, bool allowNegative, CancellationToken ct);
}
