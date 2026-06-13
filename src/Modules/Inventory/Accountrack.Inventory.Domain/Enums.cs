namespace Accountrack.Inventory.Domain;

/// <summary>The kind of stock movement recorded in the ledger (INVENTORY_DESIGN.md §2).</summary>
public enum MovementType
{
    Receipt = 0,
    Issue = 1,
    AdjustmentIn = 2,
    AdjustmentOut = 3,
    TransferOut = 4,
    TransferIn = 5,
    // Reserved for Manufacturing (Phase 3):
    ProductionConsume = 6,
    ProductionReceive = 7,
}

/// <summary>The module that originated a movement (traceability / drill-down).</summary>
public enum MovementSource
{
    Manual = 0,
    Purchasing = 1,
    Sales = 2,
    Adjustment = 3,
    Transfer = 4,
    Production = 5,
}
