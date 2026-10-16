namespace Tidal.Core.Adaptive;

/// <summary>
/// Follows the ambient light sensor: brighter in a bright room, dimmer in the dark. Each operating
/// system reads the sensor its own way and passes readings to <see cref="ReportLux"/>.
/// </summary>
public abstract class LightSensorSource : BrightnessSource
{
    // Weight of each new reading. Smoothing keeps a passing hand or shadow from making displays flicker.
    private const double Smoothing = 0.3;

    private readonly object _gate = new();
    private double? _smoothedLux;

    /// <summary>Smoothed illuminance in lux, once a reading has arrived.</summary>
    public double? Lux
    {
        get { lock (_gate) return _smoothedLux; }
    }

    protected void ReportLux(double lux)
    {
        double smoothed;
        lock (_gate)
        {
            smoothed = _smoothedLux is double previous ? previous + Smoothing * (lux - previous) : lux;
            _smoothedLux = smoothed;
        }
        Report(AdaptiveCurves.AmbientLevel(smoothed));
    }
}
