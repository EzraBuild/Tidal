using Tidal.Core.Displays;

namespace Tidal.Core.Tests;

public class BrightnessRangeTests
{
    [Fact]
    public void Construction_clamps_to_0_100_and_orders_the_bounds()
    {
        Assert.Equal((0, 100), (new BrightnessRange(-20, 140).Min, new BrightnessRange(-20, 140).Max));
        var swapped = new BrightnessRange(80, 20);
        Assert.Equal((20, 80), (swapped.Min, swapped.Max));
        Assert.True(BrightnessRange.Full.IsFull);
    }

    [Theory]
    [InlineData(50, 5, 55)]   // normal step up
    [InlineData(78, 5, 80)]   // stops at the maximum
    [InlineData(80, 5, 80)]   // already at the maximum
    [InlineData(95, 5, 95)]   // above the maximum: "brighter" must not dim it
    [InlineData(10, 5, 20)]   // below the minimum: lands inside the range
    [InlineData(50, -5, 45)]  // normal step down
    [InlineData(22, -5, 20)]  // stops at the minimum
    [InlineData(10, -5, 10)]  // below the minimum: "dimmer" must not brighten it
    [InlineData(95, -5, 80)]  // above the maximum: lands inside the range
    [InlineData(50, 0, 50)]
    public void Step_moves_in_the_requested_direction_and_respects_the_limits(int value, int delta, int expected)
    {
        Assert.Equal(expected, new BrightnessRange(20, 80).Step(value, delta));
    }

    [Fact]
    public void Fractions_map_positions_between_ranges()
    {
        var narrow = new BrightnessRange(20, 60);
        var wide = BrightnessRange.Full;

        Assert.Equal(0.5, narrow.ToFraction(40));
        Assert.Equal(50, wide.FromFraction(narrow.ToFraction(40)));
        Assert.Equal(0, narrow.ToFraction(5));   // clamped below
        Assert.Equal(1, narrow.ToFraction(95));  // clamped above
        Assert.Equal(60, narrow.FromFraction(2));
    }

    [Fact]
    public void A_single_value_range_maps_everything_to_that_value()
    {
        var fixedRange = new BrightnessRange(40, 40);
        Assert.Equal(40, fixedRange.FromFraction(0.2));
        Assert.Equal(40, fixedRange.Step(40, 5));
        Assert.Equal(1, fixedRange.ToFraction(40));
    }
}
