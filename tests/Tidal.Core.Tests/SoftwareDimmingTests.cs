using Tidal.Core.Displays;

namespace Tidal.Core.Tests;

public class SoftwareDimmingTests
{
    [Theory]
    [InlineData(100, 0)]
    [InlineData(0, SoftwareDimming.MaxOpacity)]
    [InlineData(50, SoftwareDimming.MaxOpacity / 2)]
    [InlineData(150, 0)]
    [InlineData(-20, SoftwareDimming.MaxOpacity)]
    public void Software_brightness_maps_to_overlay_opacity(int percent, double expected)
    {
        Assert.Equal(expected, SoftwareDimming.ForSoftwareBrightness(percent), precision: 6);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(40, 0)]
    [InlineData(-BrightnessRange.BelowZeroDepth, SoftwareDimming.MaxOpacity)]
    [InlineData(-BrightnessRange.BelowZeroDepth / 2, SoftwareDimming.MaxOpacity / 2)]
    [InlineData(-500, SoftwareDimming.MaxOpacity)]
    public void Below_zero_levels_map_to_overlay_opacity(int level, double expected)
    {
        Assert.Equal(expected, SoftwareDimming.ForBelowZero(level), precision: 6);
    }

    [Fact]
    public void Alpha_covers_the_byte_range()
    {
        Assert.Equal(0, SoftwareDimming.ToAlpha(0));
        Assert.Equal(255, SoftwareDimming.ToAlpha(1));
        Assert.Equal(128, SoftwareDimming.ToAlpha(0.5));
        Assert.Equal(0, SoftwareDimming.ToAlpha(-1));
    }
}
