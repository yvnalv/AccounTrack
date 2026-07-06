using Accountrack.SharedKernel.Domain;

namespace Accountrack.Inventory.Domain;

/// <summary>
/// An immutable, append-only ledger entry — the source of truth for stock quantity and value
/// (ADR-0014). Each entry records the running on-hand and average cost after the movement for
/// auditability / reconstruction. Tenant + company scoped (filtered like all owned data).
/// </summary>
public sealed class InventoryTransaction : TenantOwnedEntity, IAggregateRoot
{
    private InventoryTransaction() { }

    private InventoryTransaction(
        Guid productId, Guid warehouseId, MovementType type,
        decimal quantity, decimal unitCost, decimal totalCost, string currency,
        DateOnly movementDate, MovementSource source, Guid? sourceDocumentId, string? description,
        decimal runningQtyAfter, decimal runningAvgCostAfter)
    {
        ProductId = productId;
        WarehouseId = warehouseId;
        Type = type;
        Quantity = quantity;
        UnitCost = unitCost;
        TotalCost = totalCost;
        Currency = currency;
        MovementDate = movementDate;
        Source = source;
        SourceDocumentId = sourceDocumentId;
        Description = description;
        RunningQtyAfter = runningQtyAfter;
        RunningAvgCostAfter = runningAvgCostAfter;
    }

    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public MovementType Type { get; private set; }

    /// <summary>Always positive; <see cref="Type"/> conveys direction.</summary>
    public decimal Quantity { get; private set; }

    public decimal UnitCost { get; private set; }
    public decimal TotalCost { get; private set; }
    public string Currency { get; private set; } = default!;
    public DateOnly MovementDate { get; private set; }
    public MovementSource Source { get; private set; }
    public Guid? SourceDocumentId { get; private set; }
    public string? Description { get; private set; }
    public decimal RunningQtyAfter { get; private set; }
    public decimal RunningAvgCostAfter { get; private set; }

    public static InventoryTransaction Record(
        Guid productId, Guid warehouseId, MovementType type,
        decimal quantity, decimal unitCost, decimal totalCost, string currency,
        DateOnly movementDate, MovementSource source, Guid? sourceDocumentId, string? description,
        decimal runningQtyAfter, decimal runningAvgCostAfter) =>
        new(productId, warehouseId, type, quantity, unitCost, totalCost,
            currency, movementDate, source, sourceDocumentId, description, runningQtyAfter, runningAvgCostAfter);

    /// <summary>
    /// Restates the <em>derived</em> valuation of this entry after a back-dated recompute (ADR-0033):
    /// the cost an outbound movement left at, and the running-quantity/average snapshot. The immutable
    /// <em>facts</em> (product, warehouse, type, quantity, date, source) are never touched — this only
    /// rewrites the rebuildable projection the moving-average replay recomputes (ADR-0014).
    /// </summary>
    public void Restate(decimal unitCost, decimal totalCost, decimal runningQtyAfter, decimal runningAvgCostAfter)
    {
        UnitCost = unitCost;
        TotalCost = totalCost;
        RunningQtyAfter = runningQtyAfter;
        RunningAvgCostAfter = runningAvgCostAfter;
    }
}
