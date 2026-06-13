using Accountrack.SharedKernel.Results;

namespace Accountrack.Inventory.Domain;

public static class InventoryErrors
{
    public static Error InsufficientStock(decimal onHand, decimal requested) =>
        Error.BusinessRule("BR-INV-3",
            $"Insufficient stock: on hand {onHand}, requested {requested}.", "INVENTORY.INSUFFICIENT_STOCK");

    public static readonly Error InvalidQuantity =
        Error.Validation("INVENTORY.INVALID_QUANTITY", "Quantity must be positive.");

    public static readonly Error SameWarehouse =
        Error.Validation("INVENTORY.SAME_WAREHOUSE", "Source and destination warehouses must differ.");
}
