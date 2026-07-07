namespace Accountrack.SharedKernel.Inventory;

/// <summary>
/// The inventory cost-flow assumption used to value issues from a stock bucket (ADR-0015/ADR-0034).
/// Chosen per product at creation and immutable thereafter (like the base UoM), since changing it
/// would corrupt historical valuation. A product's stock buckets inherit the product's method.
/// </summary>
public enum CostingMethod
{
    /// <summary>Weighted moving average per (Company × Warehouse × Product) — the default (ADR-0015).</summary>
    MovingAverage = 0,

    /// <summary>First-in, first-out: issues consume the oldest cost layers first (ADR-0034).</summary>
    Fifo = 1,
}
