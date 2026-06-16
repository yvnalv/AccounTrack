namespace Accountrack.Purchasing.Application.Contracts;

public sealed record PurchaseOrderLineDto(
    Guid Id, Guid ProductId, decimal Quantity, decimal UnitPrice, decimal TaxRate,
    decimal LineSubTotal, decimal LineTaxAmount, decimal LineTotal, string? Description,
    decimal ReceivedQuantity, decimal OutstandingQuantity);

public sealed record PurchaseOrderDto(
    Guid Id, string Number, Guid SupplierId, Guid WarehouseId, string Currency, DateOnly OrderDate,
    string Status, Guid? ApprovalRequestId, decimal SubTotal, decimal TaxTotal, decimal GrandTotal,
    string? Notes, IReadOnlyList<PurchaseOrderLineDto> Lines);

public sealed record PurchaseOrderSummaryDto(
    Guid Id, string Number, Guid SupplierId, string Status, decimal GrandTotal, DateOnly OrderDate);

public sealed record GoodsReceiptLineDto(
    Guid PurchaseOrderLineId, Guid ProductId, decimal Quantity, decimal UnitCost, decimal LineCost);

public sealed record GoodsReceiptDto(
    Guid Id, string Number, Guid PurchaseOrderId, Guid SupplierId, Guid WarehouseId, string Currency,
    DateOnly ReceiptDate, decimal TotalCost, Guid? JournalEntryId, string? Notes,
    IReadOnlyList<GoodsReceiptLineDto> Lines);

public sealed record GoodsReceiptSummaryDto(
    Guid Id, string Number, Guid PurchaseOrderId, DateOnly ReceiptDate, decimal TotalCost, Guid? JournalEntryId);

public sealed record PurchaseInvoiceLineDto(
    Guid PurchaseOrderLineId, Guid ProductId, decimal Quantity, decimal UnitPrice, decimal TaxRate,
    decimal LineNet, decimal LineTax, decimal LineTotal);

public sealed record PurchaseInvoiceDto(
    Guid Id, string Number, string? SupplierInvoiceNo, Guid PurchaseOrderId, Guid SupplierId, string Currency,
    DateOnly InvoiceDate, DateOnly DueDate, decimal SubTotal, decimal TaxTotal, decimal GrandTotal,
    Guid? JournalEntryId, Guid? ApOpenItemId, string? Notes, IReadOnlyList<PurchaseInvoiceLineDto> Lines);

public sealed record PurchaseInvoiceSummaryDto(
    Guid Id, string Number, Guid PurchaseOrderId, DateOnly InvoiceDate, DateOnly DueDate, decimal GrandTotal,
    Guid? JournalEntryId);

public sealed record SupplierPaymentAllocationDto(Guid ApOpenItemId, decimal Amount);

public sealed record SupplierPaymentDto(
    Guid Id, string Number, Guid SupplierId, Guid CashAccountId, string Currency, DateOnly PaymentDate,
    decimal TotalAmount, Guid? JournalEntryId, string? Reference, string? Notes,
    IReadOnlyList<SupplierPaymentAllocationDto> Allocations);

public sealed record SupplierPaymentSummaryDto(
    Guid Id, string Number, Guid SupplierId, DateOnly PaymentDate, decimal TotalAmount, Guid? JournalEntryId);
