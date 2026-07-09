namespace Accountrack.Inventory.Domain;

/// <summary>
/// Pure, stateless <em>multi-bucket</em> moving-average replay (ADR-0038) — the cross-bucket analogue of
/// <see cref="MovingAverageReplay"/>. Given <b>all</b> of a product's movements across every warehouse
/// in a single <em>global chronological</em> order, it recomputes each movement's cost while threading a
/// warehouse <b>transfer</b>'s cost from its source leg to its destination leg. This is the engine behind
/// back-dated in-period recompute when the change cascades through a transfer: inserting an earlier
/// movement changes the source bucket's average, which changes what a later transfer-out carried, which
/// changes the destination bucket's average and therefore its later issues' COGS.
///
/// Why one global pass (and no topological sort / cycle handling): a transfer's <c>TransferOut</c> is
/// always recorded before its paired <c>TransferIn</c>, so in global order the out is processed first and
/// its cost is known by the time the in is reached. A single forward pass therefore propagates every
/// transfer correctly, and a transfer cost-flow cycle is impossible by construction (a leg can never
/// depend on a later one). Transfers are matched by <see cref="Movement.TransferGroupId"/>.
///
/// Rounding matches <see cref="StockCostBucket"/> / <see cref="MovingAverageReplay"/> exactly, and the
/// transfer legs are valued the same way the forward <c>TransferStockHandler</c> does (the destination
/// receives the source's issued <em>total</em> cost, value-preserving), so replaying an unchanged
/// sequence reproduces the stored numbers and yields a zero GL delta for every unaffected movement.
/// </summary>
public static class CrossBucketMovingAverageReplay
{
    private const int QtyScale = 6;
    private const int CostScale = 4;

    /// <summary>One movement fed into the replay, in global chronological order across all warehouses.</summary>
    /// <param name="InboundUnitCost">Recorded unit cost for a non-transfer inbound movement; ignored for
    /// outbound and for a transfer-in (whose cost comes from its paired transfer-out).</param>
    /// <param name="TransferGroupId">Correlates a transfer's two legs; null for non-transfer movements
    /// (and for legacy unlinked transfers, which the caller rejects before reaching this engine).</param>
    public readonly record struct Movement(
        Guid TransactionId, Guid WarehouseId, MovementType Type, decimal Quantity,
        decimal InboundUnitCost, Guid? TransferGroupId);

    /// <summary>The recomputed state after a movement (its bucket's running qty/avg).</summary>
    public readonly record struct Line(
        Guid TransactionId, Guid WarehouseId, MovementType Type, bool IsOutbound,
        decimal UnitCost, decimal TotalCost, decimal RunningQtyAfter, decimal RunningAvgCostAfter);

    /// <summary>True for movements that leave stock at the running average (their cost is derived).</summary>
    public static bool IsOutbound(MovementType type) => MovingAverageReplay.IsOutbound(type);

    private struct BucketState
    {
        public decimal Qty;
        public decimal Avg;
    }

    /// <summary>
    /// Replays the movements (global chronological order), returning the recomputed line for each in the
    /// same order. Throws <see cref="InvalidOperationException"/> if an outbound movement would drive its
    /// bucket's on-hand below zero and <paramref name="allowNegative"/> is false — the back-dated ordering
    /// is invalid and the caller must reject it. Throws if a transfer-in's paired transfer-out has not
    /// been seen (an unlinked/legacy transfer the caller failed to reject up front).
    /// </summary>
    public static IReadOnlyList<Line> Replay(IReadOnlyList<Movement> orderedMovements, bool allowNegative)
    {
        var buckets = new Dictionary<Guid, BucketState>();
        var transferCost = new Dictionary<Guid, decimal>(); // TransferGroupId -> the source leg's issued total cost
        var lines = new List<Line>(orderedMovements.Count);

        foreach (var m in orderedMovements)
        {
            if (m.Quantity <= 0)
            {
                throw new InvalidOperationException("Replay movement quantity must be positive.");
            }

            var state = buckets.TryGetValue(m.WarehouseId, out var existing) ? existing : new BucketState();

            decimal unitCost, totalCost;
            if (IsOutbound(m.Type))
            {
                if (!allowNegative && m.Quantity > state.Qty)
                {
                    throw new InvalidOperationException(
                        $"Back-dated recompute would drive stock negative in warehouse {m.WarehouseId}: " +
                        $"on hand {state.Qty}, issued {m.Quantity}.");
                }

                unitCost = state.Avg;
                totalCost = Math.Round(m.Quantity * state.Avg, CostScale, MidpointRounding.ToEven);
                state.Qty = Math.Round(state.Qty - m.Quantity, QtyScale, MidpointRounding.ToEven);
                // Average is unchanged by an issue.

                // A transfer-out hands its issued total cost to the paired transfer-in (value-preserving).
                if (m.Type == MovementType.TransferOut && m.TransferGroupId is { } outGroup)
                {
                    transferCost[outGroup] = totalCost;
                }
            }
            else
            {
                // A transfer-in is valued at the source leg's issued total (divided across the same
                // quantity), exactly as the forward transfer path does; any other inbound uses its own
                // recorded unit cost. Using the exact (unrounded) per-unit value in the average keeps the
                // destination bucket's average identical to the forward computation.
                decimal addedValue, storedUnitCost;
                if (m.Type == MovementType.TransferIn)
                {
                    if (m.TransferGroupId is not { } inGroup || !transferCost.TryGetValue(inGroup, out var sourceTotal))
                    {
                        throw new InvalidOperationException(
                            "Transfer-in has no paired transfer-out cost — the transfer is unlinked (legacy) " +
                            "and must be rejected before replay.");
                    }

                    addedValue = sourceTotal;
                    storedUnitCost = Math.Round(sourceTotal / m.Quantity, CostScale, MidpointRounding.ToEven);
                }
                else
                {
                    addedValue = m.Quantity * m.InboundUnitCost;
                    storedUnitCost = m.InboundUnitCost;
                }

                var newQty = state.Qty + m.Quantity;
                if (newQty > 0)
                {
                    var totalValue = (state.Qty * state.Avg) + addedValue;
                    state.Avg = Math.Round(totalValue / newQty, CostScale, MidpointRounding.ToEven);
                }

                state.Qty = Math.Round(newQty, QtyScale, MidpointRounding.ToEven);
                unitCost = storedUnitCost;
                totalCost = Math.Round(addedValue, CostScale, MidpointRounding.ToEven);
            }

            buckets[m.WarehouseId] = state;
            lines.Add(new Line(
                m.TransactionId, m.WarehouseId, m.Type, IsOutbound(m.Type),
                unitCost, totalCost, state.Qty, state.Avg));
        }

        return lines;
    }
}
