using Tidal.Core.Adaptive;

namespace Tidal.Core.Tests;

public class AdaptiveCurvesTests
{
    [Theory]
    [InlineData(-30, 60, 0)]   // night
    [InlineData(-6, 60, 0)]    // end of civil twilight
    [InlineData(60, 60, 1)]    // the day's highest sun
    [InlineData(-10, -20, 0)]  // polar night
    public void Sun_level_is_0_at_night_and_1_at_noon(double elevation, double noon, double expected)
    {
        Assert.Equal(expected, AdaptiveCurves.SunLevel(elevation, noon), precision: 6);
    }

    [Fact]
    public void Sun_level_rises_steadily_through_the_morning()
    {
        double previous = -1;
        for (double elevation = -6; elevation <= 60; elevation += 3)
        {
            double level = AdaptiveCurves.SunLevel(elevation, 60);
            Assert.True(level > previous, $"not rising at {elevation}°");
            previous = level;
        }
        Assert.True(AdaptiveCurves.SunLevel(0, 60) > 0.1); // already noticeably brighter at sunrise
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 0)]
    [InlineData(70.71, 0.5)]   // geometric middle of 5 and 1000 lux
    [InlineData(1000, 1)]
    [InlineData(50000, 1)]     // direct sunlight
    public void Ambient_level_follows_a_log_scale(double lux, double expected)
    {
        Assert.Equal(expected, AdaptiveCurves.AmbientLevel(lux), precision: 3);
    }
}
