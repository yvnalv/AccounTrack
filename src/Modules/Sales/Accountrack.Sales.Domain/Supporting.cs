using Accountrack.SharedKernel.Domain;
using Accountrack.SharedKernel.Results;

namespace Accountrack.Sales.Domain;

/// <summary>Per-company gapless counter for sales-order numbers.</summary>
public sealed class SalesOrderNumberSequence : TenantOwnedEntity
{
    private SalesOrderNumberSequence() { }

    public SalesOrderNumberSequence(int next = 1) => Next = next;

    public int Next { get; private set; }

    public string Take(DateOnly date)
    {
        var value = Next;
        Next++;
        return $"SO/{date.Year:D4}{date.Month:D2}/{value:D5}";
    }
}

/// <summary>Per-company gapless counter for delivery-order numbers.</summary>
public sealed class DeliveryOrderNumberSequence : TenantOwnedEntity
{
    private DeliveryOrderNumberSequence() { }

    public DeliveryOrderNumberSequence(int next = 1) => Next = next;

    public int Next { get; private set; }

    public string Take(DateOnly date)
    {
        var value = Next;
        Next++;
        return $"DO/{date.Year:D4}{date.Month:D2}/{value:D5}";
    }
}

/// <summary>Per-company gapless counter for sales-invoice numbers.</summary>
public sealed class SalesInvoiceNumberSequence : TenantOwnedEntity
{
    private SalesInvoiceNumberSequence() { }

    public SalesInvoiceNumberSequence(int next = 1) => Next = next;

    public int Next { get; private set; }

    public string Take(DateOnly date)
    {
        var value = Next;
        Next++;
        return $"SI/{date.Year:D4}{date.Month:D2}/{value:D5}";
    }
}

public static class SalesErrors
{
    public static readonly Error NotFound =
        Error.NotFound("SALES.SO_NOT_FOUND", "Sales order not found.");

    public static readonly Error NoInvoiceLines =
        Error.BusinessRule("BR-SAL-3", "A sales invoice requires at least one line.", "SALES.NO_INVOICE_LINES");

    public static Error OverInvoice(decimal uninvoicedDelivered, decimal requested) =>
        Error.BusinessRule(
            "BR-SAL-3",
            $"Cannot invoice {requested}; only {uninvoicedDelivered} has been delivered and not yet invoiced on this line.",
            "SALES.OVER_INVOICE");

    public static readonly Error NotDeliverable =
        Error.Conflict("SALES.NOT_DELIVERABLE", "Goods can only be delivered against an approved sales order.");

    public static readonly Error NoDeliveryLines =
        Error.BusinessRule("BR-SAL-2", "A delivery order requires at least one line.", "SALES.NO_DELIVERY_LINES");

    public static Error SalesOrderLineNotFound(Guid lineId) =>
        Error.Validation("SALES.SO_LINE_NOT_FOUND", $"Sales-order line {lineId} does not exist on this order.");

    public static Error OverDelivery(decimal outstanding, decimal requested) =>
        Error.BusinessRule(
            "BR-SAL-2",
            $"Cannot deliver {requested}; only {outstanding} is outstanding on this line.",
            "SALES.OVER_DELIVERY");

    public static readonly Error CustomerNotFound =
        Error.Validation("SALES.CUSTOMER_NOT_FOUND", "The specified customer does not exist.");

    public static readonly Error WarehouseNotFound =
        Error.Validation("SALES.WAREHOUSE_NOT_FOUND", "The specified warehouse does not exist.");

    public static Error ProductNotFound(Guid productId) =>
        Error.Validation("SALES.PRODUCT_NOT_FOUND", $"Product {productId} does not exist.");

    public static readonly Error NotDraft =
        Error.Conflict("SALES.NOT_DRAFT", "Only a draft sales order can be submitted.");

    public static readonly Error NoLines =
        Error.BusinessRule("BR-SAL-1", "A sales order requires at least one line item.", "SALES.NO_LINES");
}
