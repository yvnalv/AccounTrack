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

public static class SalesErrors
{
    public static readonly Error NotFound =
        Error.NotFound("SALES.SO_NOT_FOUND", "Sales order not found.");

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
