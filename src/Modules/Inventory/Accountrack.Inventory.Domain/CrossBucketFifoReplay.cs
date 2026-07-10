namespace Accountrack.Inventory.Domain;

/// <summary>
/// Pure, stateless <em>multi-bucket</em> FIFO replay (ADR-0038) — the FIFO analogue of
/// <see cref="CrossBucketMovingAverageReplay"/> and the cross-bucket analogue of <see cref="FifoReplay"/>.
/// Given <b>all</b> of a product's movements across every warehouse in a single <em>global chronological</em>
/// order, it maintains a separate FIFO cost-layer stack per warehouse, consumes the oldest open layers first
/// for every outbound movement, and threads a warehouse <b>transfer</b>'s cost from its source leg to its
/// destination leg. This is the engine behind back-dated in-period recompute for FIFO products when the change
/// cascades through a transfer: inserting an earlier movement changes which layers the source bucket consumes,
/// which changes what a <c>TransferOut</c> carried, which changes the single blended layer the paired
/// <c>TransferIn</c> opens in the destination and therefore its later issues' cost of goods.
///
/// Why one global pass (and no topological sort / cycle handling): a transfer's <c>TransferOut</c> is always
/// recorded before its paired <c>TransferIn</c>, so in global order the out is processed first and its cost is
/// known by the time the in is reached. A single forward pass therefore propagates every transfer correctly,
/// and a transfer cost-flow cycle is impossible by construction. Transfers are matched by
/// <see cref="Movement.TransferGroupId"/>.
///
/// A transfer's destination leg collapses the source's consumed layers into a <em>single</em> blended layer at
/// <c>issuedTotal / qty</c> — exactly as the forward <c>TransferStockHandler</c> does (issue from source at FIFO
/// cost, receive into the destination at one unit cost) — so replaying an unchanged sequence reproduces the
/// stored numbers and yields a zero GL delta for every unaffected movement. Rounding matches
/// <see cref="StockCostBucket"/>, <see cref="StockCostLayer"/>, and <see cref="FifoReplay"/> exactly.
/// </summary>
public static class CrossBucketFifoReplay
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

    /// <summary>The recomputed state after a movement (its bucket's running qty/derived average, and — for
    /// an inbound movement — its cost layer's final remaining quantity after the whole replay).</summary>
    public readonly record struct Line(
        Guid TransactionId, Guid WarehouseId, MovementType Type, bool IsOutbound,
        decimal UnitCost, decimal TotalCost, decimal RunningQtyAfter, decimal RunningAvgCostAfter,
        decimal LayerRemainingQty);

    /// <summary>True for movements that leave stock (their cost is derived from the layers consumed).</summary>
    public static bool IsOutbound(MovementType type) => MovingAverageReplay.IsOutbound(type);

    /// <summary>A cost layer being rebuilt during the replay, in chronological (oldest-first) order.</summary>
    private sealed class Layer
    {
        public required decimal UnitCost;
        public decimal RemainingQty;
    }

    /// <summary>Per-warehouse running state: on-hand qty, derived display average, and its FIFO layer stack.</summary>
    private sealed class BucketState
    {
        public decimal Qty;
        public decimal Avg;
        public readonly List<Layer> Layers = new();
    }

    /// <summary>
    /// Replays the movements (global chronological order), returning the recomputed line for each in the same
    /// order. Throws <see cref="InvalidOperationException"/> if an outbound movement would drive its bucket's
    /// on-hand below zero and <paramref name="allowNegative"/> is false — the back-dated ordering is invalid and
    /// the caller must reject it. Throws if a transfer-in's paired transfer-out has not been seen (an
    /// unlinked/legacy transfer the caller failed to reject up front).
    /// </summary>
    public static IReadOnlyList<Line> Replay(IReadOnlyList<Movement> orderedMovements, bool allowNegative)
    {
        var buckets = new Dictionary<Guid, BucketState>();
        var transferCost = new Dictionary<Guid, decimal>(); // TransferGroupId -> the source leg's issued total cost

        // The layer a given inbound movement opened, so its final remaining quantity can be reported after the
        // whole replay (a later issue may consume a layer opened earlier in the sequence).
        var openedLayerByTxn = new Dictionary<Guid, Layer>();
        var order = new List<(Movement Movement, decimal UnitCost, decimal TotalCost, decimal Qty, decimal Avg, Layer? Layer)>(orderedMovements.Count);

        foreach (var m in orderedMovements)
        {
            if (m.Quantity <= 0)
            {
                throw new InvalidOperationException("Replay movement quantity must be positive.");
            }

            var state = buckets.TryGetValue(m.WarehouseId, out var existing) ? existing : (buckets[m.WarehouseId] = new BucketState());

            decimal unitCost, totalCost;
            Layer? openedLayer = null;

            if (IsOutbound(m.Type))
            {
                if (!allowNegative && m.Quantity > state.Qty)
                {
                    throw new InvalidOperationException(
                        $"Back-dated recompute would drive stock negative in warehouse {m.WarehouseId}: " +
                        $"on hand {state.Qty}, issued {m.Quantity}.");
                }

                totalCost = ConsumeOldestFirst(state, m.Quantity);
                unitCost = Math.Round(totalCost / m.Quantity, CostScale, MidpointRounding.ToEven);

                // Step on-hand and the derived display average (StockCostBucket.IssueFifo semantics).
                var remainingValue = (state.Qty * state.Avg) - totalCost;
                state.Qty = Math.Round(state.Qty - m.Quantity, QtyScale, MidpointRounding.ToEven);
                if (state.Qty > 0m)
                {
                    state.Avg = Math.Round(remainingValue / state.Qty, CostScale, MidpointRounding.ToEven);
                }
                else if (state.Qty == 0m)
                {
                    state.Avg = 0m;
                }
                // Negative on-hand: keep the last average for the next receipt to reconcile.

                // A transfer-out hands its issued total cost to the paired transfer-in (value-preserving).
                if (m.Type == MovementType.TransferOut && m.TransferGroupId is { } outGroup)
                {
                    transferCost[outGroup] = totalCost;
                }
            }
            else
            {
                // A transfer-in opens a single blended layer valued at the source leg's issued total (divided
                // across the same quantity), exactly as the forward transfer path does; any other inbound opens
                // a layer at its own recorded unit cost.
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
                    storedUnitCost = Math.Round(m.InboundUnitCost, CostScale, MidpointRounding.ToEven);
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

                openedLayer = new Layer
                {
                    UnitCost = storedUnitCost,
                    RemainingQty = Math.Round(m.Quantity, QtyScale, MidpointRounding.ToEven),
                };
                state.Layers.Add(openedLayer);
                openedLayerByTxn[m.TransactionId] = openedLayer;
            }

            order.Add((m, unitCost, totalCost, state.Qty, state.Avg, openedLayer));
        }

        // Layer remainders are only final after the whole replay, so build the output lines here.
        var result = new List<Line>(order.Count);
        foreach (var o in order)
        {
            var isOutbound = IsOutbound(o.Movement.Type);
            result.Add(new Line(
                o.Movement.TransactionId, o.Movement.WarehouseId, o.Movement.Type, isOutbound,
                o.UnitCost, o.TotalCost, o.Qty, o.Avg,
                o.Layer?.RemainingQty ?? 0m));
        }

        return result;
    }

    /// <summary>Consumes <paramref name="quantity"/> from the bucket's oldest open layers first, returning the
    /// total cost. A quantity not covered by layers (only when negative stock is allowed) is costed at the
    /// bucket's running display average, exactly as the forward path does. Rounding matches
    /// <see cref="FifoReplay"/> / <see cref="FifoCosting"/>.</summary>
    private static decimal ConsumeOldestFirst(BucketState state, decimal quantity)
    {
        var remaining = quantity;
        var cost = 0m;
        foreach (var layer in state.Layers)
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

        if (remaining > 0m)
        {
            cost = Math.Round(cost + (remaining * state.Avg), CostScale, MidpointRounding.ToEven);
        }

        return cost;
    }
}
