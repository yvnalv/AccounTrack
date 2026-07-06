using Accountrack.SharedKernel.Domain;

namespace Accountrack.Inventory.Domain;

/// <summary>
/// The moving-average cost bucket for a (Company × Warehouse × Product) — ADR-0015. Holds the
/// authoritative running on-hand quantity and weighted-average unit cost, derived from and kept in
/// step with the <see cref="InventoryTransaction"/> ledger. The serialization point for concurrent
/// stock movements (RowVersion — ADR-0021).
/// </summary>
public sealed class StockCostBucket : TenantOwnedEntity, IAggregateRoot
{
    private const int QtyScale = 6;
    private const int CostScale = 4;

    private StockCostBucket() { }

    private StockCostBucket(Guid productId, Guid warehouseId, string currency)
    {
        ProductId = productId;
        WarehouseId = warehouseId;
        Currency = currency;
        OnHandQty = 0m;
        AvgUnitCost = 0m;
    }

    public Guid ProductId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public string Currency { get; private set; } = default!;

    /// <summary>Quantity currently on hand (may be negative only if negative stock is permitted).</summary>
    public decimal OnHandQty { get; private set; }

    /// <summary>Weighted-average unit cost in the company functional currency.</summary>
    public decimal AvgUnitCost { get; private set; }

    public static StockCostBucket Create(Guid productId, Guid warehouseId, string currency) =>
        new(productId, warehouseId, currency.Trim().ToUpperInvariant());

    /// <summary>
    /// Adds stock at <paramref name="unitCost"/> and recomputes the weighted average.
    /// Returns the total cost of the receipt.
    /// </summary>
    public decimal Receive(decimal quantity, decimal unitCost)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Receipt quantity must be positive.");
        }

        if (unitCost < 0)
        {
            throw new InvalidOperationException("Unit cost must be non-negative.");
        }

        var newQty = OnHandQty + quantity;
        if (newQty > 0)
        {
            var totalValue = (OnHandQty * AvgUnitCost) + (quantity * unitCost);
            AvgUnitCost = Math.Round(totalValue / newQty, CostScale, MidpointRounding.ToEven);
        }

        OnHandQty = Math.Round(newQty, QtyScale, MidpointRounding.ToEven);
        return Math.Round(quantity * unitCost, CostScale, MidpointRounding.ToEven);
    }

    /// <summary>
    /// Removes stock at the current average cost. The average is unchanged by an issue. Returns the
    /// cost of goods issued. Rejects going negative unless <paramref name="allowNegative"/>.
    /// </summary>
    public decimal Issue(decimal quantity, bool allowNegative)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Issue quantity must be positive.");
        }

        if (!allowNegative && quantity > OnHandQty)
        {
            throw new InvalidOperationException(
                $"Insufficient stock: on hand {OnHandQty}, requested {quantity}.");
        }

        var cost = Math.Round(quantity * AvgUnitCost, CostScale, MidpointRounding.ToEven);
        OnHandQty = Math.Round(OnHandQty - quantity, QtyScale, MidpointRounding.ToEven);
        return cost;
    }

    /// <summary>
    /// Overwrites the running on-hand and average with the result of a full moving-average replay
    /// (ADR-0033). Used only by back-dated recompute, which recalculates the bucket from its whole
    /// movement history rather than mutating it forward.
    /// </summary>
    public void SetState(decimal onHandQty, decimal avgUnitCost)
    {
        OnHandQty = Math.Round(onHandQty, QtyScale, MidpointRounding.ToEven);
        AvgUnitCost = Math.Round(avgUnitCost, CostScale, MidpointRounding.ToEven);
    }
}
