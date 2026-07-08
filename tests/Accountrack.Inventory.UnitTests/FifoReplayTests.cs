using Accountrack.Inventory.Domain;
using FluentAssertions;
using Xunit;

namespace Accountrack.Inventory.UnitTests;

public class FifoReplayTests
{
    private static FifoReplay.Movement In(Guid id, decimal qty, decimal unitCost) =>
        new(id, MovementType.Receipt, qty, unitCost);

    private static FifoReplay.Movement Out(decimal qty) =>
        new(Guid.NewGuid(), MovementType.Issue, qty, 0m);

    [Fact]
    public void Forward_sequence_consumes_the_oldest_layer_first()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var lines = FifoReplay.Replay(new[] { In(a, 10, 100), In(b, 10, 120), Out(15) }, allowNegative: false);

        // Issue of 15 takes 10 @ 100 + 5 @ 120 = 1 600; on hand 5.
        lines[2].TotalCost.Should().Be(1600m);
        lines[2].RunningQtyAfter.Should().Be(5m);
        lines[2].RunningAvgCostAfter.Should().Be(120m); // the surviving layer's cost
        lines[0].LayerRemainingQty.Should().Be(0m);     // layer A fully spent
        lines[1].LayerRemainingQty.Should().Be(5m);     // layer B has 5 left
    }

    [Fact]
    public void Back_dated_cheaper_layer_is_consumed_first_and_lowers_a_later_issue()
    {
        // A May-28 receipt of 10 @ 80 is inserted BEFORE a Jun-1 receipt of 10 @ 100 and a Jun-5 issue
        // of 10. FIFO now consumes the cheaper May-28 layer first, so the issue costs 800, not 1 000.
        var backdated = Guid.NewGuid();
        var existing = Guid.NewGuid();
        var lines = FifoReplay.Replay(
            new[] { In(backdated, 10, 80), In(existing, 10, 100), Out(10) }, allowNegative: false);

        lines[2].TotalCost.Should().Be(800m);       // 10 @ 80
        lines[2].RunningQtyAfter.Should().Be(10m);
        lines[0].LayerRemainingQty.Should().Be(0m); // the back-dated layer is spent
        lines[1].LayerRemainingQty.Should().Be(10m);// the original layer is now untouched
    }

    [Fact]
    public void Replay_of_an_unchanged_sequence_reproduces_the_original_costs()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var lines = FifoReplay.Replay(
            new[] { In(a, 10, 100), Out(4), In(b, 10, 120), Out(12) }, allowNegative: false);

        lines[1].TotalCost.Should().Be(400m);   // 4 @ 100
        // second issue: 6 left @ 100 + 6 @ 120 = 600 + 720 = 1 320
        lines[3].TotalCost.Should().Be(1320m);
        lines[3].RunningQtyAfter.Should().Be(4m);
        lines[3].LayerRemainingQty.Should().Be(0m); // outbound carries no layer
    }

    [Fact]
    public void Issue_below_zero_throws_when_negative_stock_is_disallowed()
    {
        var act = () => FifoReplay.Replay(new[] { Out(10) }, allowNegative: false);

        act.Should().Throw<InvalidOperationException>().WithMessage("*negative*");
    }

    [Fact]
    public void Shortfall_is_costed_at_the_running_display_average_when_negative_is_allowed()
    {
        var a = Guid.NewGuid();
        // Receive 10 @ 100 (avg 100), issue 15: 10 covered by the layer (1 000) + 5 shortfall @ 100 = 1 500.
        var lines = FifoReplay.Replay(new[] { In(a, 10, 100), Out(15) }, allowNegative: true);

        lines[1].TotalCost.Should().Be(1500m);
        lines[1].RunningQtyAfter.Should().Be(-5m);
        lines[0].LayerRemainingQty.Should().Be(0m);
    }
}
