namespace Accountrack.Inventory.Domain;

/// <summary>
/// Pure FIFO consumption (ADR-0034): given a bucket's open cost layers oldest-first, computes the cost
/// of issuing a quantity by consuming the oldest layers first. Stateless and rounding-matched to
/// <see cref="StockCostBucket"/> so it is unit-testable in isolation; the caller applies the returned
/// per-layer takes to the layer entities and posts the total as the cost of goods issued.
/// </summary>
public static class FifoCosting
{
    private const int QtyScale = 6;
    private const int CostScale = 4;

    /// <summary>An open layer fed into the calculation, oldest first.</summary>
    public readonly record struct OpenLayer(Guid LayerId, decimal RemainingQty, decimal UnitCost);

    /// <summary>How much to consume from one layer, and at what cost.</summary>
    public readonly record struct Take(Guid LayerId, decimal Quantity, decimal Cost);

    /// <param name="TotalCost">Cost of the quantity covered by layers.</param>
    /// <param name="Takes">Per-layer consumption to apply to the entities.</param>
    /// <param name="Shortfall">Quantity not covered by any layer — only &gt; 0 when negative stock is allowed.</param>
    public readonly record struct Result(decimal TotalCost, IReadOnlyList<Take> Takes, decimal Shortfall);

    public static Result Consume(IReadOnlyList<OpenLayer> layersOldestFirst, decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("Issue quantity must be positive.");
        }

        var remaining = quantity;
        var takes = new List<Take>();
        var total = 0m;

        foreach (var layer in layersOldestFirst)
        {
            if (remaining <= 0)
            {
                break;
            }

            var take = Math.Min(layer.RemainingQty, remaining);
            if (take <= 0)
            {
                continue;
            }

            var cost = Math.Round(take * layer.UnitCost, CostScale, MidpointRounding.ToEven);
            takes.Add(new Take(layer.LayerId, take, cost));
            total = Math.Round(total + cost, CostScale, MidpointRounding.ToEven);
            remaining = Math.Round(remaining - take, QtyScale, MidpointRounding.ToEven);
        }

        return new Result(total, takes, remaining > 0 ? remaining : 0m);
    }
}
