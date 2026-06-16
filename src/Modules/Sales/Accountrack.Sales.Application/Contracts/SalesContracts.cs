namespace Accountrack.Sales.Application.Contracts;

public sealed record SalesOrderLineDto(
    Guid Id, Guid ProductId, decimal Quantity, decimal UnitPrice, decimal TaxRate,
    decimal LineSubTotal, decimal LineTaxAmount, decimal LineTotal, string? Description,
    decimal DeliveredQuantity, decimal InvoicedQuantity, decimal OutstandingQuantity);

public sealed record SalesOrderDto(
    Guid Id, string Number, Guid CustomerId, Guid WarehouseId, string Currency, DateOnly OrderDate,
    string Status, Guid? ApprovalRequestId, decimal SubTotal, decimal TaxTotal, decimal GrandTotal,
    string? Notes, IReadOnlyList<SalesOrderLineDto> Lines);

public sealed record SalesOrderSummaryDto(
    Guid Id, string Number, Guid CustomerId, string Status, decimal GrandTotal, DateOnly OrderDate);

public sealed record DeliveryOrderLineDto(
    Guid SalesOrderLineId, Guid ProductId, decimal Quantity, decimal UnitCost, decimal LineCost);

public sealed record DeliveryOrderDto(
    Guid Id, string Number, Guid SalesOrderId, Guid CustomerId, Guid WarehouseId, string Currency,
    DateOnly DeliveryDate, decimal TotalCost, Guid? JournalEntryId, string? Notes,
    IReadOnlyList<DeliveryOrderLineDto> Lines);

public sealed record DeliveryOrderSummaryDto(
    Guid Id, string Number, Guid SalesOrderId, DateOnly DeliveryDate, decimal TotalCost, Guid? JournalEntryId);

public sealed record SalesInvoiceLineDto(
    Guid SalesOrderLineId, Guid ProductId, decimal Quantity, decimal UnitPrice, decimal TaxRate,
    decimal LineNet, decimal LineTax, decimal LineTotal);

public sealed record SalesInvoiceDto(
    Guid Id, string Number, Guid SalesOrderId, Guid CustomerId, string Currency,
    DateOnly InvoiceDate, DateOnly DueDate, decimal SubTotal, decimal TaxTotal, decimal GrandTotal,
    Guid? JournalEntryId, Guid? ArOpenItemId, string? Notes, IReadOnlyList<SalesInvoiceLineDto> Lines);

public sealed record SalesInvoiceSummaryDto(
    Guid Id, string Number, Guid SalesOrderId, DateOnly InvoiceDate, DateOnly DueDate, decimal GrandTotal,
    Guid? JournalEntryId);

public sealed record CustomerPaymentAllocationDto(Guid ArOpenItemId, decimal Amount);

public sealed record CustomerPaymentDto(
    Guid Id, string Number, Guid CustomerId, Guid CashAccountId, string Currency, DateOnly PaymentDate,
    decimal TotalAmount, Guid? JournalEntryId, string? Reference, string? Notes,
    IReadOnlyList<CustomerPaymentAllocationDto> Allocations);

public sealed record CustomerPaymentSummaryDto(
    Guid Id, string Number, Guid CustomerId, DateOnly PaymentDate, decimal TotalAmount, Guid? JournalEntryId);
