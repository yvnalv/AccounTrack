using Accountrack.Inventory.Domain;
using FluentAssertions;
using Xunit;

namespace Accountrack.Inventory.UnitTests;

public class FifoCostingTests
{
    private static FifoCosting.OpenLayer Layer(decimal qty, decimal cost) =>
        new(Guid.NewGuid(), qty, cost);

    [Fact]
    public void Consumes_a_single_layer_partially()
    {
        var layer = Layer(10m, 100m);

        var result = FifoCosting.Consume(new[] { layer }, 4m);

        result.TotalCost.Should().Be(400m);
        result.Shortfall.Should().Be(0m);
        result.Takes.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { LayerId = layer.LayerId, Quantity = 4m, Cost = 400m });
    }

    [Fact]
    public void Consumes_oldest_layers_first_across_a_boundary()
    {
        var oldest = Layer(10m, 100m);
        var newer = Layer(10m, 120m);

        var result = FifoCosting.Consume(new[] { oldest, newer }, 15m);

        // 10 @ 100 (oldest, fully) + 5 @ 120 (newer, partial) = 1,600
        result.TotalCost.Should().Be(1600m);
        result.Shortfall.Should().Be(0m);
        result.Takes.Should().HaveCount(2);
        result.Takes[0].Should().BeEquivalentTo(new { LayerId = oldest.LayerId, Quantity = 10m, Cost = 1000m });
        result.Takes[1].Should().BeEquivalentTo(new { LayerId = newer.LayerId, Quantity = 5m, Cost = 600m });
    }

    [Fact]
    public void Reports_a_shortfall_when_layers_are_exhausted()
    {
        var result = FifoCosting.Consume(new[] { Layer(3m, 100m) }, 8m);

        result.TotalCost.Should().Be(300m); // only the 3 available, at 100
        result.Shortfall.Should().Be(5m);   // caller costs the remainder (negative-stock path)
    }

    [Fact]
    public void Rejects_a_non_positive_quantity()
    {
        var act = () => FifoCosting.Consume(new[] { Layer(10m, 100m) }, 0m);

        act.Should().Throw<InvalidOperationException>();
    }
}
