namespace Tidal.Core.Adaptive;

/// <summary>Follows the sun at a fixed location: dim at night, brightest at the day's highest sun.</summary>
public sealed class SunSource(double latitude, double longitude, TimeProvider? timeProvider = null) : BrightnessSource
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromMinutes(1);

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private ITimer? _timer;

    public double Latitude { get; } = latitude;
    public double Longitude { get; } = longitude;

    /// <summary>Sunrise, sunset and solar noon for today's local date.</summary>
    public SunDay Today => SolarCalculator.Day(DateOnly.FromDateTime(_time.GetLocalNow().DateTime), Latitude, Longitude);

    public override void Start() => _timer ??= _time.CreateTimer(_ => Update(), null, TimeSpan.Zero, UpdateInterval);

    private void Update()
    {
        double elevation = SolarCalculator.Elevation(_time.GetUtcNow(), Latitude, Longitude);
        Report(AdaptiveCurves.SunLevel(elevation, Today.NoonElevation));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer?.Dispose();
    }
}
