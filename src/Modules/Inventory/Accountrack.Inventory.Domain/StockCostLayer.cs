using Accountrack.SharedKernel.Domain;

namespace Accountrack.Inventory.Domain;

/// <summary>
/// A FIFO cost layer for a (Company × Warehouse × Product) bucket (ADR-0034): the un-consumed
/// remainder of an inbound movement, held at its original unit cost. Issues consume the oldest open
/// layers first, so a FIFO bucket's on-hand value is the sum of its open layers
/// (<see cref="RemainingQty"/> × <see cref="UnitCost"/>). Moving-average buckets keep no layers.
/// </summary>
public sealed class StockCostLayer : TenantOwnedEntity, IAggregateRoot
{
    private const int QtyScale = 6;
    private const int CostScale = 4;

    private StockCostLayer() { }

    private StockCostLayer(
        Guid productId, Guid warehouseId, string currency, Guid sourceTransactionId,
        DateOnly movementDate, decimal unitCost, decimal quantity)
    {
        ProductId = productId;
        WarehouseId = warehouseId;
        Currency = currency;
        SourceTransactionId = sourceTransactionId;
        MovementDate = movementDate;
        UnitCost = unitCost;
        OriginalQty = quantity;
        RemainingQty = quantity;
    }

    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public string Currency { get; private set; } = default!;

    /// <summary>The inbound ledger entry that created this layer (traceability / reconstruction).</summary>
    public Guid SourceTransactionId { get; private set; }

    /// <summary>The date of the inbound movement — the FIFO ordering key (oldest first).</summary>
    public DateOnly MovementDate { get; private set; }

    public decimal UnitCost { get; private set; }
    public decimal OriginalQty { get; private set; }

    /// <summary>Quantity still available to be consumed; a layer with 0 remaining is fully spent.</summary>
    public decimal RemainingQty { get; private set; }

    public static StockCostLayer Create(
        Guid productId, Guid warehouseId, string currency, Guid sourceTransactionId,
        DateOnly movementDate, decimal unitCost, decimal quantity) =>
        new(productId, warehouseId, currency.Trim().ToUpperInvariant(), sourceTransactionId, movementDate,
            Math.Round(unitCost, CostScale, MidpointRounding.ToEven),
            Math.Round(quantity, QtyScale, MidpointRounding.ToEven));

    /// <summary>Consumes up to <paramref name="quantity"/> from this layer; returns the amount actually taken.</summary>
    public decimal Consume(decimal quantity)
    {
        var taken = Math.Min(RemainingQty, Math.Max(quantity, 0m));
        RemainingQty = Math.Round(RemainingQty - taken, QtyScale, MidpointRounding.ToEven);
        return taken;
    }

    /// <summary>
    /// Overwrites the layer's remaining quantity with the result of a full FIFO replay (ADR-0037).
    /// Used only by back-dated recompute, which reconstructs every layer's remainder after an inserted
    /// movement changes which layers each later issue consumed. The immutable facts (source movement,
    /// unit cost, original quantity) are never touched — this only rewrites the rebuildable remainder.
    /// </summary>
    public void Restate(decimal remainingQty) =>
        RemainingQty = Math.Round(remainingQty, QtyScale, MidpointRounding.ToEven);

    /// <summary>
    /// Overwrites both the unit cost and the remaining quantity from a cross-bucket FIFO replay (ADR-0038).
    /// Used only for a <em>transfer-in</em> layer, whose cost is <em>derived</em> from the source bucket's
    /// issued cost rather than being an immutable receipt fact — so when a back-dated movement restates the
    /// source, the destination layer's cost is rebuilt with it. For any other (receipt) layer the unit cost
    /// stays immutable and only <see cref="Restate(decimal)"/> is used.
    /// </summary>
    public void RestateCost(decimal unitCost, decimal remainingQty)
    {
        UnitCost = Math.Round(unitCost, CostScale, MidpointRounding.ToEven);
        RemainingQty = Math.Round(remainingQty, QtyScale, MidpointRounding.ToEven);
    }
}
