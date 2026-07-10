using Accountrack.Inventory.Domain;
using FluentAssertions;
using Xunit;

namespace Accountrack.Inventory.UnitTests;

public class CrossBucketFifoReplayTests
{
    private static readonly Guid WhA = Guid.NewGuid();
    private static readonly Guid WhB = Guid.NewGuid();
    private static readonly Guid WhC = Guid.NewGuid();

    private static CrossBucketFifoReplay.Movement Receipt(Guid wh, decimal qty, decimal unitCost) =>
        new(Guid.NewGuid(), wh, MovementType.Receipt, qty, unitCost, null);

    private static CrossBucketFifoReplay.Movement Sale(Guid wh, decimal qty) =>
        new(Guid.NewGuid(), wh, MovementType.Issue, qty, 0m, null);

    private static (CrossBucketFifoReplay.Movement Out, CrossBucketFifoReplay.Movement In)
        Transfer(Guid from, Guid to, decimal qty)
    {
        var group = Guid.NewGuid();
        return (
            new(Guid.NewGuid(), from, MovementType.TransferOut, qty, 0m, group),
            new(Guid.NewGuid(), to, MovementType.TransferIn, qty, 0m, group));
    }

    [Fact]
    public void Transfer_carries_source_cost_to_the_destination_value_preserving()
    {
        var (tOut, tIn) = Transfer(WhA, WhB, 40);
        var sale = Sale(WhB, 40);

        var lines = CrossBucketFifoReplay.Replay(
            new[] { Receipt(WhA, 100, 10000), tOut, tIn, sale }, allowNegative: false);

        var outLine = lines.Single(l => l.TransactionId == tOut.TransactionId);
        var inLine = lines.Single(l => l.TransactionId == tIn.TransactionId);
        var saleLine = lines.Single(l => l.TransactionId == sale.TransactionId);

        outLine.TotalCost.Should().Be(400000m);                 // 40 @ 10 000
        inLine.TotalCost.Should().Be(outLine.TotalCost, "a transfer preserves value across warehouses");
        inLine.RunningAvgCostAfter.Should().Be(10000m);
        saleLine.TotalCost.Should().Be(400000m);                // B sells 40 @ 10 000
    }

    [Fact]
    public void Transfer_out_consumes_the_oldest_layer_not_the_average()
    {
        // The FIFO distinction from moving average: with a cheap layer under an expensive one, a transfer-out
        // carries the OLDEST layer's cost (8 000), not the bucket's blended average (9 000). The destination
        // therefore receives — and later sells — at 8 000.
        var (tOut, tIn) = Transfer(WhA, WhB, 40);
        var sale = Sale(WhB, 40);

        var lines = CrossBucketFifoReplay.Replay(
            new[] { Receipt(WhA, 100, 8000), Receipt(WhA, 100, 10000), tOut, tIn, sale }, allowNegative: false);

        lines.Single(l => l.TransactionId == tOut.TransactionId).TotalCost.Should().Be(320000m);  // 40 @ 8 000 (oldest)
        lines.Single(l => l.TransactionId == tIn.TransactionId).TotalCost.Should().Be(320000m);
        lines.Single(l => l.TransactionId == tIn.TransactionId).RunningAvgCostAfter.Should().Be(8000m);
        lines.Single(l => l.TransactionId == sale.TransactionId).TotalCost.Should().Be(320000m); // B sells 40 @ 8 000
    }

    [Fact]
    public void Back_dated_cheaper_receipt_in_source_cascades_into_the_destination_sale_cogs()
    {
        // A: existing receipt 100 @ 10 000; transfer 40 to B; B sells 40 (originally 40 @ 10 000 = 400 000).
        // Insert a back-dated A receipt 100 @ 8 000 before it → FIFO consumes the oldest (8 000) layer on the
        // transfer-out, so B receives 40 @ 8 000 = 320 000 and its later sale drops to 320 000 (−80 000 COGS).
        var (tOut, tIn) = Transfer(WhA, WhB, 40);
        var sale = Sale(WhB, 40);

        var lines = CrossBucketFifoReplay.Replay(
            new[] { Receipt(WhA, 100, 8000), Receipt(WhA, 100, 10000), tOut, tIn, sale }, allowNegative: false);

        lines.Single(l => l.TransactionId == tOut.TransactionId).TotalCost.Should().Be(320000m);
        lines.Single(l => l.TransactionId == tIn.TransactionId).TotalCost.Should().Be(320000m);
        lines.Single(l => l.TransactionId == sale.TransactionId).TotalCost.Should().Be(320000m); // was 400 000
    }

    [Fact]
    public void Cost_threads_through_a_chain_of_transfers()
    {
        // A → B → C, then the oldest A layer's cost must reach C's sale.
        var (aOut, bIn) = Transfer(WhA, WhB, 30);
        var (bOut, cIn) = Transfer(WhB, WhC, 20);
        var sale = Sale(WhC, 20);

        var lines = CrossBucketFifoReplay.Replay(
            new[]
            {
                Receipt(WhA, 100, 6000), Receipt(WhA, 100, 10000), // A layers 6 000 then 10 000
                aOut, bIn,                                          // B receives 30 @ 6 000 (oldest)
                bOut, cIn,                                          // C receives 20 @ 6 000
                sale,                                               // C sells 20 @ 6 000
            },
            allowNegative: false);

        lines.Single(l => l.TransactionId == cIn.TransactionId).RunningAvgCostAfter.Should().Be(6000m);
        lines.Single(l => l.TransactionId == sale.TransactionId).TotalCost.Should().Be(120000m);   // 20 @ 6 000
    }

    [Fact]
    public void Back_and_forth_transfers_are_not_a_cycle_and_replay_forward()
    {
        // A → B then later B → A: legitimate, and processed purely in time order (no cycle rejection).
        var (aOut, bIn) = Transfer(WhA, WhB, 50);
        var (bOut, aIn) = Transfer(WhB, WhA, 20);

        var act = () => CrossBucketFifoReplay.Replay(
            new[] { Receipt(WhA, 100, 10000), aOut, bIn, bOut, aIn, Sale(WhA, 10) }, allowNegative: false);

        act.Should().NotThrow();
    }

    [Fact]
    public void Each_bucket_tracks_its_own_running_quantity()
    {
        var (tOut, tIn) = Transfer(WhA, WhB, 40);

        var lines = CrossBucketFifoReplay.Replay(
            new[] { Receipt(WhA, 100, 10000), tOut, tIn }, allowNegative: false);

        lines.Single(l => l.TransactionId == tOut.TransactionId).RunningQtyAfter.Should().Be(60m);  // A: 100 − 40
        lines.Single(l => l.TransactionId == tIn.TransactionId).RunningQtyAfter.Should().Be(40m);   // B: 0 + 40
    }

    [Fact]
    public void Inbound_layer_remaining_quantity_reflects_later_consumption()
    {
        // The transfer-in opens a 40-unit layer in B; a later 25-unit sale leaves 15 on that layer.
        var (tOut, tIn) = Transfer(WhA, WhB, 40);
        var sale = Sale(WhB, 25);

        var lines = CrossBucketFifoReplay.Replay(
            new[] { Receipt(WhA, 100, 10000), tOut, tIn, sale }, allowNegative: false);

        lines.Single(l => l.TransactionId == tIn.TransactionId).LayerRemainingQty.Should().Be(15m);
        lines.Single(l => l.TransactionId == sale.TransactionId).LayerRemainingQty.Should().Be(0m); // outbound
    }

    [Fact]
    public void Issue_below_zero_in_a_bucket_throws_when_negative_is_disallowed()
    {
        var (tOut, _) = Transfer(WhA, WhB, 40);

        var act = () => CrossBucketFifoReplay.Replay(
            new[] { Receipt(WhA, 30, 10000), tOut }, allowNegative: false);   // only 30 on hand, transfer 40

        act.Should().Throw<InvalidOperationException>().WithMessage("*negative*");
    }

    [Fact]
    public void Transfer_in_without_a_seen_paired_out_throws()
    {
        // A transfer-in whose out is missing (unlinked/legacy) — the caller must reject before replay.
        var orphanIn = new CrossBucketFifoReplay.Movement(
            Guid.NewGuid(), WhB, MovementType.TransferIn, 10, 0m, Guid.NewGuid());

        var act = () => CrossBucketFifoReplay.Replay(new[] { orphanIn }, allowNegative: false);

        act.Should().Throw<InvalidOperationException>().WithMessage("*unlinked*");
    }
}
