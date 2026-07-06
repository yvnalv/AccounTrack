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

    // --- Back-dated recompute (ADR-0033) ---

    /// <summary>A manual receipt or transfer cannot be back-dated in this version (no cross-module GL
    /// context to post the recompute correction); enter it dated on or after the latest movement.</summary>
    public static readonly Error BackDatingNotSupported =
        Error.BusinessRule("BR-INV-4",
            "This movement type cannot be back-dated before an existing movement for the same product and warehouse.",
            "INVENTORY.BACKDATING_NOT_SUPPORTED");

    /// <summary>A back-dated movement would change the cost of a later transfer or production movement,
    /// which spans cost across buckets — out of scope for the single-bucket recompute (ADR-0033).</summary>
    public static readonly Error BackDatingCrossesTransfer =
        Error.BusinessRule("BR-INV-5",
            "Back-dating before a later stock transfer or production movement is not supported; reverse and re-enter that movement first.",
            "INVENTORY.BACKDATING_CROSSES_TRANSFER");

    /// <summary>Inserting the back-dated movement would drive on-hand negative at some later point
    /// (an issue would precede the stock that covered it) and negative stock is disallowed.</summary>
    public static readonly Error BackDatingWouldGoNegative =
        Error.BusinessRule("BR-INV-3",
            "Back-dating this movement would drive stock negative for a later movement.",
            "INVENTORY.BACKDATING_NEGATIVE");
}
