namespace Accountrack.Inventory.Domain;

/// <summary>
/// Pure, stateless moving-average replay (ADR-0015/ADR-0033). Given a cost bucket's movements in
/// chronological order, it recomputes the running on-hand quantity, the weighted-average unit cost,
/// and — for every <em>outbound</em> movement — the cost issued at the corrected average. This is the
/// engine behind back-dated in-period recompute: inserting an earlier movement changes the average
/// every later issue should have used, so the whole bucket is replayed from scratch.
///
/// Inbound movements (receipts, adjustment-in, transfer-in, production-receive) add stock at their own
/// recorded unit cost — a fact that never changes. Outbound movements (issue, adjustment-out,
/// transfer-out, production-consume) leave at the running average, so their cost is <em>derived</em>
/// and is what recompute corrects. The rounding matches <see cref="StockCostBucket"/> exactly so a
/// replay of an unchanged sequence reproduces the original numbers.
/// </summary>
public static class MovingAverageReplay
{
    private const int QtyScale = 6;
    private const int CostScale = 4;

    /// <summary>One movement fed into the replay, in chronological order.</summary>
    /// <param name="InboundUnitCost">The recorded unit cost for an inbound movement; ignored for outbound.</param>
    public readonly record struct Movement(Guid TransactionId, MovementType Type, decimal Quantity, decimal InboundUnitCost);

    /// <summary>The recomputed state after a movement.</summary>
    public readonly record struct Line(
        Guid TransactionId, MovementType Type, bool IsOutbound,
        decimal UnitCost, decimal TotalCost, decimal RunningQtyAfter, decimal RunningAvgCostAfter);

    /// <summary>True for movements that leave stock at the running average (their cost is derived).</summary>
    public static bool IsOutbound(MovementType type) => type switch
    {
        MovementType.Receipt or MovementType.AdjustmentIn or MovementType.TransferIn
            or MovementType.ProductionReceive => false,
        _ => true,
    };

    /// <summary>
    /// Replays the movements, returning the recomputed line for each (same order as the input). Throws
    /// <see cref="InvalidOperationException"/> if an outbound movement would drive on-hand below zero
    /// and <paramref name="allowNegative"/> is false — that means the back-dated ordering is invalid
    /// (e.g. an issue now precedes the receipt that covered it) and the caller must reject it.
    /// </summary>
    public static IReadOnlyList<Line> Replay(IReadOnlyList<Movement> orderedMovements, bool allowNegative)
    {
        var qty = 0m;
        var avg = 0m;
        var lines = new List<Line>(orderedMovements.Count);

        foreach (var m in orderedMovements)
        {
            if (m.Quantity <= 0)
            {
                throw new InvalidOperationException("Replay movement quantity must be positive.");
            }

            decimal unitCost, totalCost;
            if (IsOutbound(m.Type))
            {
                if (!allowNegative && m.Quantity > qty)
                {
                    throw new InvalidOperationException(
                        $"Back-dated recompute would drive stock negative: on hand {qty}, issued {m.Quantity}.");
                }

                unitCost = avg;
                totalCost = Math.Round(m.Quantity * avg, CostScale, MidpointRounding.ToEven);
                qty = Math.Round(qty - m.Quantity, QtyScale, MidpointRounding.ToEven);
                // Average is unchanged by an issue.
            }
            else
            {
                var newQty = qty + m.Quantity;
                if (newQty > 0)
                {
                    var totalValue = (qty * avg) + (m.Quantity * m.InboundUnitCost);
                    avg = Math.Round(totalValue / newQty, CostScale, MidpointRounding.ToEven);
                }

                qty = Math.Round(newQty, QtyScale, MidpointRounding.ToEven);
                unitCost = m.InboundUnitCost;
                totalCost = Math.Round(m.Quantity * m.InboundUnitCost, CostScale, MidpointRounding.ToEven);
            }

            lines.Add(new Line(m.TransactionId, m.Type, IsOutbound(m.Type), unitCost, totalCost, qty, avg));
        }

        return lines;
    }
}
