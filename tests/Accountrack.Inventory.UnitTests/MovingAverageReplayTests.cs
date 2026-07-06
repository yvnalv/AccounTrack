using Accountrack.Inventory.Domain;
using FluentAssertions;
using Xunit;

namespace Accountrack.Inventory.UnitTests;

public class MovingAverageReplayTests
{
    private static MovingAverageReplay.Movement In(decimal qty, decimal unitCost) =>
        new(Guid.NewGuid(), MovementType.Receipt, qty, unitCost);

    private static MovingAverageReplay.Movement Out(decimal qty) =>
        new(Guid.NewGuid(), MovementType.Issue, qty, 0m);

    [Fact]
    public void Forward_sequence_computes_the_running_average_and_issue_cost()
    {
        var lines = MovingAverageReplay.Replay(new[] { In(100, 10000), Out(60) }, allowNegative: false);

        lines[0].RunningAvgCostAfter.Should().Be(10000m);
        lines[0].RunningQtyAfter.Should().Be(100m);
        lines[1].IsOutbound.Should().BeTrue();
        lines[1].UnitCost.Should().Be(10000m);
        lines[1].TotalCost.Should().Be(600000m);   // 60 @ 10 000
        lines[1].RunningQtyAfter.Should().Be(40m);
    }

    [Fact]
    public void Back_dated_cheaper_receipt_lowers_the_average_a_later_issue_pays()
    {
        // ADR-0033 example: a May-28 purchase of 100 @ 8 000 is discovered and inserted BEFORE the
        // existing Jun-1 receipt of 100 @ 10 000 and the Jun-5 issue of 60. After both receipts the
        // average is (100*8000 + 100*10000)/200 = 9 000, so the issue costs 60 * 9 000 = 540 000,
        // not the 600 000 originally posted — a 60 000 COGS correction.
        var backdated = In(100, 8000);
        var existingReceipt = In(100, 10000);
        var existingIssue = Out(60);

        var lines = MovingAverageReplay.Replay(
            new[] { backdated, existingReceipt, existingIssue }, allowNegative: false);

        lines[1].RunningAvgCostAfter.Should().Be(9000m);
        lines[2].TotalCost.Should().Be(540000m);
        lines[2].UnitCost.Should().Be(9000m);
        lines[2].RunningQtyAfter.Should().Be(140m);
    }

    [Fact]
    public void Issue_does_not_change_the_average()
    {
        var lines = MovingAverageReplay.Replay(new[] { In(100, 10000), Out(60), In(40, 5000) }, allowNegative: false);

        lines[1].RunningAvgCostAfter.Should().Be(10000m, "an issue leaves the average untouched");
        // 40 @ 40 on hand → (40*10000 + 40*5000)/80 = 7 500
        lines[2].RunningAvgCostAfter.Should().Be(7500m);
        lines[2].RunningQtyAfter.Should().Be(80m);
    }

    [Fact]
    public void Multiple_issues_after_a_back_dated_receipt_are_all_recomputed()
    {
        var lines = MovingAverageReplay.Replay(
            new[] { In(100, 8000), In(100, 10000), Out(60), Out(30) }, allowNegative: false);

        lines[2].TotalCost.Should().Be(540000m);   // 60 @ 9 000
        lines[3].TotalCost.Should().Be(270000m);   // 30 @ 9 000
        lines[3].RunningQtyAfter.Should().Be(110m);
    }

    [Fact]
    public void Issue_below_zero_throws_when_negative_stock_is_disallowed()
    {
        var act = () => MovingAverageReplay.Replay(new[] { Out(10) }, allowNegative: false);

        act.Should().Throw<InvalidOperationException>().WithMessage("*negative*");
    }

    [Fact]
    public void Issue_below_zero_is_permitted_when_negative_stock_is_allowed()
    {
        var lines = MovingAverageReplay.Replay(new[] { Out(10) }, allowNegative: true);

        lines[0].RunningQtyAfter.Should().Be(-10m);
        lines[0].TotalCost.Should().Be(0m, "there is no established average yet");
    }

    [Fact]
    public void Adjustment_in_and_transfer_in_are_treated_as_inbound_at_their_recorded_cost()
    {
        MovingAverageReplay.IsOutbound(MovementType.AdjustmentIn).Should().BeFalse();
        MovingAverageReplay.IsOutbound(MovementType.TransferIn).Should().BeFalse();
        MovingAverageReplay.IsOutbound(MovementType.ProductionReceive).Should().BeFalse();
        MovingAverageReplay.IsOutbound(MovementType.AdjustmentOut).Should().BeTrue();
        MovingAverageReplay.IsOutbound(MovementType.TransferOut).Should().BeTrue();
        MovingAverageReplay.IsOutbound(MovementType.Issue).Should().BeTrue();
    }
}
