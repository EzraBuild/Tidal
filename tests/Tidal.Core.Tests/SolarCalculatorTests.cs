using Tidal.Core.Adaptive;

namespace Tidal.Core.Tests;

public class SolarCalculatorTests
{
    private static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(3);

    [Fact]
    public void London_midsummer_matches_published_times()
    {
        // 21 June 2024: sunrise 04:43 BST, sunset 21:21 BST (UTC+1).
        var day = SolarCalculator.Day(new DateOnly(2024, 6, 21), 51.5074, -0.1278);

        AssertNear(Utc(2024, 6, 21, 3, 43), day.Sunrise);
        AssertNear(Utc(2024, 6, 21, 20, 21), day.Sunset);
        Assert.InRange(day.NoonElevation, 61.5, 62.5); // 90 - 51.5 + 23.4
    }

    [Fact]
    public void New_York_midwinter_matches_published_times()
    {
        // 21 December 2024: sunrise 07:16 EST, sunset 16:32 EST (UTC-5).
        var day = SolarCalculator.Day(new DateOnly(2024, 12, 21), 40.7128, -74.0060);

        AssertNear(Utc(2024, 12, 21, 12, 16), day.Sunrise);
        AssertNear(Utc(2024, 12, 21, 21, 32), day.Sunset);
    }

    [Fact]
    public void Sun_is_nearly_overhead_at_the_equator_at_the_equinox()
    {
        var day = SolarCalculator.Day(new DateOnly(2024, 3, 20), -1.2921, 36.8219); // Nairobi
        Assert.InRange(day.NoonElevation, 88, 90);
        Assert.InRange(day.Sunset!.Value - day.Sunrise!.Value, TimeSpan.FromHours(12), TimeSpan.FromHours(12.3));
    }

    [Fact]
    public void Polar_night_and_midnight_sun_have_no_sunrise()
    {
        var winter = SolarCalculator.Day(new DateOnly(2024, 12, 21), 69.6492, 18.9553); // Tromsø
        Assert.Null(winter.Sunrise);
        Assert.True(winter.NoonElevation < 0);

        var summer = SolarCalculator.Day(new DateOnly(2024, 6, 21), 69.6492, 18.9553);
        Assert.Null(summer.Sunset);
        Assert.True(summer.NoonElevation > 0);
    }

    [Fact]
    public void Elevation_at_sunrise_is_just_below_the_horizon()
    {
        var day = SolarCalculator.Day(new DateOnly(2024, 6, 21), 51.5074, -0.1278);
        Assert.InRange(SolarCalculator.Elevation(day.Sunrise!.Value, 51.5074, -0.1278), -1.2, -0.4);
        Assert.InRange(SolarCalculator.Elevation(day.SolarNoon, 51.5074, -0.1278), 61.5, 62.5);
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute)
        => new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    private static void AssertNear(DateTimeOffset expected, DateTimeOffset? actual)
    {
        Assert.NotNull(actual);
        var difference = (actual.Value - expected).Duration();
        Assert.True(difference <= Tolerance, $"expected {expected:u}, got {actual:u} ({difference.TotalMinutes:0.0} min off)");
    }
}
