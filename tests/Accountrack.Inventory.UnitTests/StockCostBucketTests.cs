using Accountrack.Inventory.Domain;
using FluentAssertions;
using Xunit;

namespace Accountrack.Inventory.UnitTests;

public class StockCostBucketTests
{
    private static StockCostBucket New() => StockCostBucket.Create(Guid.NewGuid(), Guid.NewGuid(), "IDR");

    [Fact]
    public void Receipt_then_receipt_recomputes_weighted_average()
    {
        var b = New();
        b.Receive(10m, 100m);   // 10 @ 100
        b.Receive(10m, 120m);   // 10 @ 120

        b.OnHandQty.Should().Be(20m);
        b.AvgUnitCost.Should().Be(110m); // (1000 + 1200) / 20
    }

    [Fact]
    public void Issue_uses_average_cost_and_leaves_average_unchanged()
    {
        var b = New();
        b.Receive(10m, 100m);
        b.Receive(10m, 120m);   // avg 110, qty 20

        var cost = b.Issue(5m, allowNegative: false);

        cost.Should().Be(550m);          // 5 * 110
        b.OnHandQty.Should().Be(15m);
        b.AvgUnitCost.Should().Be(110m); // unchanged by an issue
    }

    [Fact]
    public void Weighted_average_handles_fractional_quantities()
    {
        var b = New();
        b.Receive(3m, 100m);    // 300
        b.Receive(2m, 150m);    // 300

        b.AvgUnitCost.Should().Be(120m); // 600 / 5
    }

    [Fact]
    public void Issue_beyond_on_hand_is_rejected_when_negative_disallowed()
    {
        var b = New();
        b.Receive(5m, 100m);

        FluentActions.Invoking(() => b.Issue(6m, allowNegative: false))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Issue_beyond_on_hand_is_allowed_when_negative_permitted()
    {
        var b = New();
        b.Receive(5m, 100m);

        var cost = b.Issue(8m, allowNegative: true);

        cost.Should().Be(800m); // 8 * 100
        b.OnHandQty.Should().Be(-3m);
    }

    [Fact]
    public void Receipt_must_be_positive() =>
        FluentActions.Invoking(() => New().Receive(0m, 100m)).Should().Throw<InvalidOperationException>();
}
