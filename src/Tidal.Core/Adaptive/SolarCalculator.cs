namespace Tidal.Core.Adaptive;

/// <summary>
/// The sun on one day at one place. Sunrise and sunset are null when the sun stays up all day
/// (polar day, <see cref="NoonElevation"/> above zero) or down all day (polar night).
/// </summary>
public readonly record struct SunDay(DateTimeOffset? Sunrise, DateTimeOffset? Sunset, DateTimeOffset SolarNoon, double NoonElevation);

/// <summary>
/// Sun position from the NOAA solar calculator equations, accurate to about a minute for sunrise
/// and sunset between ±72° latitude. Latitude is north-positive, longitude east-positive, in degrees.
/// </summary>
public static class SolarCalculator
{
    // Sunrise/sunset: the sun's upper edge on the horizon, allowing for atmospheric refraction.
    private const double SunriseZenith = 90.833;

    /// <summary>Elevation of the sun above the horizon in degrees (negative when below).</summary>
    public static double Elevation(DateTimeOffset time, double latitude, double longitude)
    {
        var utc = time.UtcDateTime;
        var sun = SunAt(utc);
        double trueSolarTime = Mod(utc.TimeOfDay.TotalMinutes + sun.EquationOfTime + 4 * longitude, 1440);
        double hourAngle = trueSolarTime / 4 - 180;

        double lat = Rad(latitude), declination = Rad(sun.Declination);
        double cosZenith = Math.Sin(lat) * Math.Sin(declination)
                         + Math.Cos(lat) * Math.Cos(declination) * Math.Cos(Rad(hourAngle));
        return 90 - Deg(Math.Acos(Math.Clamp(cosZenith, -1, 1)));
    }

    /// <summary>Sunrise, sunset and solar noon (as UTC instants) on a local calendar day.</summary>
    public static SunDay Day(DateOnly date, double latitude, double longitude)
    {
        var midnightUtc = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        // Solar noon falls at about 12:00 minus 4 minutes per degree east; evaluate the sun there,
        // then refine once with the corrected time.
        var solarNoon = midnightUtc.AddMinutes(720 - 4 * longitude);
        var sun = SunAt(solarNoon.UtcDateTime);
        solarNoon = midnightUtc.AddMinutes(720 - 4 * longitude - sun.EquationOfTime);
        sun = SunAt(solarNoon.UtcDateTime);

        double noonElevation = Elevation(solarNoon, latitude, longitude);
        double lat = Rad(latitude), declination = Rad(sun.Declination);
        double cosHourAngle = Math.Cos(Rad(SunriseZenith)) / (Math.Cos(lat) * Math.Cos(declination))
                            - Math.Tan(lat) * Math.Tan(declination);
        if (cosHourAngle is > 1 or < -1) return new SunDay(null, null, solarNoon, noonElevation);

        double halfDayMinutes = 4 * Deg(Math.Acos(cosHourAngle));
        return new SunDay(solarNoon.AddMinutes(-halfDayMinutes), solarNoon.AddMinutes(halfDayMinutes), solarNoon, noonElevation);
    }

    private readonly record struct SunState(double Declination, double EquationOfTime);

    private static SunState SunAt(DateTime utc)
    {
        double julianDay = utc.ToOADate() + 2415018.5;
        double t = (julianDay - 2451545.0) / 36525; // Julian centuries since J2000

        double meanLongitude = Mod(280.46646 + t * (36000.76983 + t * 0.0003032), 360);
        double meanAnomaly = 357.52911 + t * (35999.05029 - 0.0001537 * t);
        double eccentricity = 0.016708634 - t * (0.000042037 + 0.0000001267 * t);
        double center = Math.Sin(Rad(meanAnomaly)) * (1.914602 - t * (0.004817 + 0.000014 * t))
                      + Math.Sin(Rad(2 * meanAnomaly)) * (0.019993 - 0.000101 * t)
                      + Math.Sin(Rad(3 * meanAnomaly)) * 0.000289;
        double omega = 125.04 - 1934.136 * t;
        double apparentLongitude = meanLongitude + center - 0.00569 - 0.00478 * Math.Sin(Rad(omega));
        double meanObliquity = 23 + (26 + (21.448 - t * (46.815 + t * (0.00059 - t * 0.001813))) / 60) / 60;
        double obliquity = meanObliquity + 0.00256 * Math.Cos(Rad(omega));

        double declination = Deg(Math.Asin(Math.Sin(Rad(obliquity)) * Math.Sin(Rad(apparentLongitude))));
        double y = Math.Pow(Math.Tan(Rad(obliquity / 2)), 2);
        double l0 = Rad(meanLongitude), m = Rad(meanAnomaly);
        double equationOfTime = 4 * Deg(
            y * Math.Sin(2 * l0)
            - 2 * eccentricity * Math.Sin(m)
            + 4 * eccentricity * y * Math.Sin(m) * Math.Cos(2 * l0)
            - 0.5 * y * y * Math.Sin(4 * l0)
            - 1.25 * eccentricity * eccentricity * Math.Sin(2 * m));

        return new SunState(declination, equationOfTime);
    }

    private static double Rad(double degrees) => degrees * Math.PI / 180;
    private static double Deg(double radians) => radians * 180 / Math.PI;
    private static double Mod(double value, double modulus) => ((value % modulus) + modulus) % modulus;
}
