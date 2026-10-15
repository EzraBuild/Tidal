namespace Tidal.Core.Adaptive;

/// <summary>
/// Turn what a source measures into a brightness level from 0 (dimmest) to 1 (brightest), which
/// each display then scales into its own limits.
/// </summary>
public static class AdaptiveCurves
{
    /// <summary>Sun elevation (degrees) at which civil twilight ends; darker than this counts as night.</summary>
    public const double NightElevation = -6;

    private const double DarkLux = 5;     // a dim room at night
    private const double BrightLux = 1000; // an overcast day by a window

    /// <summary>
    /// 0 from the end of evening twilight to the start of morning twilight, rising to 1 at the
    /// day's highest sun. The curve climbs quickly after dawn and flattens towards noon.
    /// </summary>
    public static double SunLevel(double elevation, double noonElevation)
    {
        if (noonElevation <= NightElevation) return 0; // polar night: dim all day
        double t = Math.Clamp((elevation - NightElevation) / (noonElevation - NightElevation), 0, 1);
        return Math.Sin(t * Math.PI / 2);
    }

    /// <summary>
    /// Level from ambient light, on a log scale because perceived brightness is roughly logarithmic:
    /// 5 lux or less is 0, 1000 lux or more is 1.
    /// </summary>
    public static double AmbientLevel(double lux)
        => lux <= DarkLux ? 0 : Math.Clamp(Math.Log(lux / DarkLux) / Math.Log(BrightLux / DarkLux), 0, 1);
}
