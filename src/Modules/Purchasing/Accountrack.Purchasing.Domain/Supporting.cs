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

public static class PurchasingErrors
{
    public static readonly Error NotFound =
        Error.NotFound("PURCHASING.PO_NOT_FOUND", "Purchase order not found.");

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
