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
    decimal SystemQty, decimal CountedQty, decimal Variance, Guid? TransactionId, decimal CostApplied);

/// <summary>One line of the inventory valuation report — a product's on-hand quantity and value
/// (aggregated across warehouses) at moving-average cost.</summary>
public sealed record InventoryValuationRowDto(
    Guid ProductId, string ProductName, decimal Quantity, decimal AvgUnitCost, decimal Value);

/// <summary>Inventory valuation: ledger value by product + the GL Inventory-account balance it should
/// reconcile to (BR-INV-7).</summary>
public sealed record InventoryValuationDto(
    string Currency, IReadOnlyList<InventoryValuationRowDto> Rows,
    decimal TotalValue, decimal GlInventoryBalance, decimal Difference, bool IsReconciled);
