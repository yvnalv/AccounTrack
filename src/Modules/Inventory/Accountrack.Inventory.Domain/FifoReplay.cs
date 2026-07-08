namespace Accountrack.Inventory.Domain;

/// <summary>
/// Pure, stateless FIFO replay (ADR-0034/ADR-0037) — the FIFO analogue of
/// <see cref="MovingAverageReplay"/>. Given a cost bucket's movements in chronological order it
/// reopens a cost layer for every <em>inbound</em> movement and consumes the oldest open layers first
/// for every <em>outbound</em> movement, recomputing each outbound's cost of goods issued and each
/// inbound layer's final remaining quantity. This is the engine behind back-dated in-period recompute
/// for FIFO products: inserting an earlier movement changes which layers every later issue consumes,
/// so the whole bucket is replayed from scratch and the layer remainders are rebuilt.
///
/// It also tracks a running <em>display</em> average (the FIFO bucket's derived average, ADR-0034) so
/// a replay of an unchanged sequence reproduces the numbers the forward path recorded exactly, and so
/// a negative-stock shortfall is costed at the same average the forward path used. The rounding matches
/// <see cref="StockCostBucket"/>, <see cref="StockCostLayer"/>, and <see cref="FifoCosting"/> exactly.
/// </summary>
public static class FifoReplay
{
    private const int QtyScale = 6;
    private const int CostScale = 4;

    /// <summary>One movement fed into the replay, in chronological order.</summary>
    /// <param name="InboundUnitCost">The recorded unit cost for an inbound movement; ignored for outbound.</param>
    public readonly record struct Movement(Guid TransactionId, MovementType Type, decimal Quantity, decimal InboundUnitCost);

    /// <summary>The recomputed state after a movement.</summary>
    /// <param name="LayerRemainingQty">For an <em>inbound</em> movement, the layer's remaining quantity
    /// after the whole replay (what its <see cref="StockCostLayer"/> must be set to); 0 for outbound.</param>
    public readonly record struct Line(
        Guid TransactionId, MovementType Type, bool IsOutbound,
        decimal UnitCost, decimal TotalCost, decimal RunningQtyAfter, decimal RunningAvgCostAfter,
        decimal LayerRemainingQty);

    /// <summary>True for movements that leave stock (their cost is derived from the layers consumed).</summary>
    public static bool IsOutbound(MovementType type) => MovingAverageReplay.IsOutbound(type);

    /// <summary>A cost layer being rebuilt during the replay, in chronological (oldest-first) order.</summary>
    private sealed class Layer
    {
        public required Guid TransactionId;
        public required decimal UnitCost;
        public decimal RemainingQty;
    }

    /// <summary>
    /// Replays the movements, returning the recomputed line for each (same order as the input). Throws
    /// <see cref="InvalidOperationException"/> if an outbound movement would drive on-hand below zero
    /// and <paramref name="allowNegative"/> is false — that means the back-dated ordering is invalid
    /// (e.g. an issue now precedes the receipt that covered it) and the caller must reject it.
    /// </summary>
    public static IReadOnlyList<Line> Replay(IReadOnlyList<Movement> orderedMovements, bool allowNegative)
    {
        var qty = 0m;
        var avg = 0m; // derived display average, kept in step exactly as the forward path does
        var layers = new List<Layer>(orderedMovements.Count);
        var linesByOrder = new List<(Movement Movement, decimal UnitCost, decimal TotalCost, decimal RunningQty, decimal RunningAvg, Layer? Layer)>(orderedMovements.Count);

        foreach (var m in orderedMovements)
        {
            if (m.Quantity <= 0)
            {
                throw new InvalidOperationException("Replay movement quantity must be positive.");
            }

            decimal unitCost, totalCost;
            Layer? openedLayer = null;

            if (IsOutbound(m.Type))
            {
                if (!allowNegative && m.Quantity > qty)
                {
                    throw new InvalidOperationException(
                        $"Back-dated recompute would drive stock negative: on hand {qty}, issued {m.Quantity}.");
                }

                // Consume the oldest open layers first, matching FifoCosting.Consume rounding.
                var remaining = m.Quantity;
                var cost = 0m;
                foreach (var layer in layers)
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

                    var takeCost = Math.Round(take * layer.UnitCost, CostScale, MidpointRounding.ToEven);
                    cost = Math.Round(cost + takeCost, CostScale, MidpointRounding.ToEven);
                    layer.RemainingQty = Math.Round(layer.RemainingQty - take, QtyScale, MidpointRounding.ToEven);
                    remaining = Math.Round(remaining - take, QtyScale, MidpointRounding.ToEven);
                }

                // Any quantity not covered by layers (only when negative stock is allowed) is costed at
                // the running display average, exactly as the forward path does.
                if (remaining > 0m)
                {
                    cost = Math.Round(cost + (remaining * avg), CostScale, MidpointRounding.ToEven);
                }

                totalCost = cost;
                unitCost = m.Quantity == 0m ? 0m : Math.Round(cost / m.Quantity, CostScale, MidpointRounding.ToEven);

                // Step on-hand and the derived display average (StockCostBucket.IssueFifo semantics).
                var remainingValue = (qty * avg) - cost;
                qty = Math.Round(qty - m.Quantity, QtyScale, MidpointRounding.ToEven);
                if (qty > 0m)
                {
                    avg = Math.Round(remainingValue / qty, CostScale, MidpointRounding.ToEven);
                }
                else if (qty == 0m)
                {
                    avg = 0m;
                }
                // Negative on-hand: keep the last average for the next receipt to reconcile.
            }
            else
            {
                // Inbound: open a layer and update the derived average (StockCostBucket.Receive semantics).
                var newQty = qty + m.Quantity;
                if (newQty > 0)
                {
                    var totalValue = (qty * avg) + (m.Quantity * m.InboundUnitCost);
                    avg = Math.Round(totalValue / newQty, CostScale, MidpointRounding.ToEven);
                }

                qty = Math.Round(newQty, QtyScale, MidpointRounding.ToEven);
                unitCost = m.InboundUnitCost;
                totalCost = Math.Round(m.Quantity * m.InboundUnitCost, CostScale, MidpointRounding.ToEven);

                openedLayer = new Layer
                {
                    TransactionId = m.TransactionId,
                    UnitCost = Math.Round(m.InboundUnitCost, CostScale, MidpointRounding.ToEven),
                    RemainingQty = Math.Round(m.Quantity, QtyScale, MidpointRounding.ToEven),
                };
                layers.Add(openedLayer);
            }

            linesByOrder.Add((m, unitCost, totalCost, qty, avg, openedLayer));
        }

        // Layer remainders are only final after the whole replay (a later issue may have consumed a
        // layer opened earlier in the sequence), so build the output lines here.
        var result = new List<Line>(linesByOrder.Count);
        foreach (var l in linesByOrder)
        {
            var isOutbound = IsOutbound(l.Movement.Type);
            result.Add(new Line(
                l.Movement.TransactionId, l.Movement.Type, isOutbound,
                l.UnitCost, l.TotalCost, l.RunningQty, l.RunningAvg,
                l.Layer?.RemainingQty ?? 0m));
        }

        return result;
    }
}
