namespace Accountrack.Sales.Application.Contracts;

public sealed record SalesOrderLineDto(
    Guid Id, Guid ProductId, decimal Quantity, decimal UnitPrice, decimal TaxRate,
    decimal LineSubTotal, decimal LineTaxAmount, decimal LineTotal, string? Description,
    decimal DeliveredQuantity);

public sealed record SalesOrderDto(
    Guid Id, string Number, Guid CustomerId, Guid WarehouseId, string Currency, DateOnly OrderDate,
    string Status, Guid? ApprovalRequestId, decimal SubTotal, decimal TaxTotal, decimal GrandTotal,
    string? Notes, IReadOnlyList<SalesOrderLineDto> Lines);

public sealed record SalesOrderSummaryDto(
    Guid Id, string Number, Guid CustomerId, string Status, decimal GrandTotal, DateOnly OrderDate);
