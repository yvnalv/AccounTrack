namespace Accountrack.Inventory.Application.Contracts;

public sealed record StockOnHandDto(
    Guid ProductId, Guid WarehouseId, decimal OnHandQty, decimal AvgUnitCost, decimal Value, string Currency);

public sealed record StockCardEntryDto(
    Guid TransactionId, DateOnly Date, string Type, decimal Quantity, decimal UnitCost, decimal TotalCost,
    decimal RunningQtyAfter, decimal RunningAvgCostAfter, string Source, Guid? SourceDocumentId, string? Description);

public sealed record TransferStockResult(Guid OutTransactionId, Guid InTransactionId, decimal UnitCost);
