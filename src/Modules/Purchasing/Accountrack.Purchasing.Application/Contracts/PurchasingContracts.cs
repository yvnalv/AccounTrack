namespace Accountrack.Purchasing.Application.Contracts;

public sealed record PurchaseOrderLineDto(
    Guid ProductId, decimal Quantity, decimal UnitPrice, decimal TaxRate,
    decimal LineSubTotal, decimal LineTaxAmount, decimal LineTotal, string? Description);

public sealed record PurchaseOrderDto(
    Guid Id, string Number, Guid SupplierId, Guid WarehouseId, string Currency, DateOnly OrderDate,
    string Status, Guid? ApprovalRequestId, decimal SubTotal, decimal TaxTotal, decimal GrandTotal,
    string? Notes, IReadOnlyList<PurchaseOrderLineDto> Lines);

public sealed record PurchaseOrderSummaryDto(
    Guid Id, string Number, Guid SupplierId, string Status, decimal GrandTotal, DateOnly OrderDate);
