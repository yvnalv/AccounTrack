using Accountrack.SharedKernel.Domain;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Purchasing.Domain;

/// <summary>Per-company gapless counter for purchase-order numbers.</summary>
public sealed class PurchaseOrderNumberSequence : TenantOwnedEntity
{
    private PurchaseOrderNumberSequence() { }

    public PurchaseOrderNumberSequence(int next = 1) => Next = next;

    public int Next { get; private set; }

    public string Take(DateOnly date)
    {
        var value = Next;
        Next++;
        return $"PO/{date.Year:D4}{date.Month:D2}/{value:D5}";
    }
}

/// <summary>Per-company gapless counter for goods-receipt numbers.</summary>
public sealed class GoodsReceiptNumberSequence : TenantOwnedEntity
{
    private GoodsReceiptNumberSequence() { }

    public GoodsReceiptNumberSequence(int next = 1) => Next = next;

    public int Next { get; private set; }

    public string Take(DateOnly date)
    {
        var value = Next;
        Next++;
        return $"GR/{date.Year:D4}{date.Month:D2}/{value:D5}";
    }
}

/// <summary>Per-company gapless counter for purchase-invoice numbers.</summary>
public sealed class PurchaseInvoiceNumberSequence : TenantOwnedEntity
{
    private PurchaseInvoiceNumberSequence() { }

    public PurchaseInvoiceNumberSequence(int next = 1) => Next = next;

    public int Next { get; private set; }

    public string Take(DateOnly date)
    {
        var value = Next;
        Next++;
        return $"PI/{date.Year:D4}{date.Month:D2}/{value:D5}";
    }
}

public static class PurchasingErrors
{
    public static readonly Error NotFound =
        Error.NotFound("PURCHASING.PO_NOT_FOUND", "Purchase order not found.");

    public static readonly Error NoInvoiceLines =
        Error.BusinessRule("BR-PUR-3", "A purchase invoice requires at least one line.", "PURCHASING.NO_INVOICE_LINES");

    public static Error OverInvoice(decimal uninvoicedReceived, decimal requested) =>
        Error.BusinessRule(
            "BR-PUR-3",
            $"Cannot invoice {requested}; only {uninvoicedReceived} has been received and not yet invoiced on this line.",
            "PURCHASING.OVER_INVOICE");

    public static readonly Error NotReceivable =
        Error.Conflict("PURCHASING.NOT_RECEIVABLE", "Goods can only be received against an approved purchase order.");

    public static readonly Error NoReceiptLines =
        Error.BusinessRule("BR-PUR-2", "A goods receipt requires at least one line.", "PURCHASING.NO_RECEIPT_LINES");

    public static Error PurchaseOrderLineNotFound(Guid lineId) =>
        Error.Validation("PURCHASING.PO_LINE_NOT_FOUND", $"Purchase-order line {lineId} does not exist on this order.");

    public static Error OverReceipt(decimal outstanding, decimal requested) =>
        Error.BusinessRule(
            "BR-PUR-2",
            $"Cannot receive {requested}; only {outstanding} is outstanding on this line.",
            "PURCHASING.OVER_RECEIPT");

    public static readonly Error SupplierNotFound =
        Error.Validation("PURCHASING.SUPPLIER_NOT_FOUND", "The specified supplier does not exist.");

    public static readonly Error WarehouseNotFound =
        Error.Validation("PURCHASING.WAREHOUSE_NOT_FOUND", "The specified warehouse does not exist.");

    public static Error ProductNotFound(Guid productId) =>
        Error.Validation("PURCHASING.PRODUCT_NOT_FOUND", $"Product {productId} does not exist.");

    public static readonly Error NotDraft =
        Error.Conflict("PURCHASING.NOT_DRAFT", "Only a draft purchase order can be submitted.");

    public static readonly Error NoLines =
        Error.BusinessRule("BR-PUR-1", "A purchase order requires at least one line item.", "PURCHASING.NO_LINES");
}
