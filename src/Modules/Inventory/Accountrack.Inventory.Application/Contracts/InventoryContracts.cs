using Accountrack.Application.Abstractions.Idempotency;

namespace Accountrack.Inventory.Application.Contracts;

public sealed record StockOnHandDto(
    Guid ProductId, Guid WarehouseId, decimal OnHandQty, decimal AvgUnitCost, decimal Value, string Currency);

public sealed record StockCardEntryDto(
    Guid TransactionId, DateOnly Date, string Type, decimal Quantity, decimal UnitCost, decimal TotalCost,
    decimal RunningQtyAfter, decimal RunningAvgCostAfter, string Source, Guid? SourceDocumentId, string? Description);

public sealed record TransferStockResult(Guid OutTransactionId, Guid InTransactionId, decimal UnitCost);

/// <summary>Outcome of a stock opname: the system vs counted quantities, the variance, and the
/// reconciling movement (null when the count matched exactly).</summary>
public sealed record StockOpnameResult(
    decimal SystemQty, decimal CountedQty, decimal Variance, Guid? TransactionId, decimal CostApplied)
    : IIdempotentResult<StockOpnameResult>
{
    // Addressed by the reconciling movement's id so a retried opname never double-posts the adjustment +
    // its variance journal (ADR-0021). An exact-match opname posts nothing, so it has no id (Guid.Empty).
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid IdempotentId => TransactionId ?? Guid.Empty;

    /// <summary>Replay reconstruction (id-only): returns the reconciling movement's id; the quantities
    /// are not stored and come back as defaults. An exact-match no-op maps back to a null movement.</summary>
    public static StockOpnameResult FromIdempotentId(Guid id) =>
        new(0m, 0m, 0m, id == Guid.Empty ? null : id, 0m);
}

/// <summary>One line of the inventory valuation report — a product's on-hand quantity and value
/// (aggregated across warehouses) at moving-average cost.</summary>
public sealed record InventoryValuationRowDto(
    Guid ProductId, string ProductName, decimal Quantity, decimal AvgUnitCost, decimal Value);

/// <summary>Inventory valuation: ledger value by product + the GL Inventory-account balance it should
/// reconcile to (BR-INV-7).</summary>
public sealed record InventoryValuationDto(
    string Currency, IReadOnlyList<InventoryValuationRowDto> Rows,
    decimal TotalValue, decimal GlInventoryBalance, decimal Difference, bool IsReconciled);
