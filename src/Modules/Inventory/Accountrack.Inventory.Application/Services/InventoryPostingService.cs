using Accountrack.Inventory.Domain;
using Accountrack.Modules.Contracts.Inventory;
using Accountrack.SharedKernel.Results;
using IInventoryLedger = Accountrack.Inventory.Application.Abstractions.IInventoryLedger;
using LedgerResult = Accountrack.Inventory.Application.Abstractions.StockMovementResult;

namespace Accountrack.Inventory.Application.Services;

/// <summary>
/// Adapter exposing the inventory ledger as the public <see cref="IInventoryPosting"/> contract so
/// other modules can post stock movements inside their atomic transaction (INTEGRATION_EVENTS.md §3).
/// Save-less — the caller's <c>ICrossModuleUnitOfWork</c> commits the movement.
/// </summary>
public sealed class InventoryPostingService : IInventoryPosting
{
    private readonly IInventoryLedger _ledger;

    public InventoryPostingService(IInventoryLedger ledger) => _ledger = ledger;

    public async Task<Result<StockMovementResult>> ReceiveAsync(
        Guid productId, Guid warehouseId, string currency, decimal quantity, decimal unitCost,
        DateOnly date, Guid sourceDocumentId, string? description, CancellationToken ct)
    {
        var result = await _ledger.ReceiveAsync(
            productId, warehouseId, currency, quantity, unitCost, date,
            MovementType.Receipt, MovementSource.Purchasing, sourceDocumentId, description, ct);

        return Map(result);
    }

    public async Task<Result<StockMovementResult>> IssueAsync(
        Guid productId, Guid warehouseId, decimal quantity,
        DateOnly date, Guid sourceDocumentId, string? description, bool allowNegative, CancellationToken ct)
    {
        var result = await _ledger.IssueAsync(
            productId, warehouseId, quantity, date,
            MovementType.Issue, MovementSource.Sales, sourceDocumentId, description, allowNegative, ct);

        return Map(result);
    }

    private static Result<StockMovementResult> Map(Result<LedgerResult> result)
    {
        if (result.IsFailure)
        {
            return result.Error;
        }

        var r = result.Value;
        return new StockMovementResult(r.TransactionId, r.CostApplied, r.RunningQtyAfter, r.RunningAvgCostAfter);
    }
}
