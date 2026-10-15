using Windows.Devices.Sensors;

namespace Tidal.Core.Adaptive;

/// <summary>Follows the ambient light sensor: brighter in a bright room, dimmer in the dark.</summary>
public sealed class LightSensorSource : BrightnessSource
{
    // Weight of each new reading. Smoothing keeps a passing hand or shadow from making displays flicker.
    private const double Smoothing = 0.3;
    private const uint PreferredReportIntervalMs = 1000;

    private readonly LightSensor? _sensor = LightSensor.GetDefault();
    private readonly object _gate = new();
    private double? _smoothedLux;

    public static bool IsAvailable => LightSensor.GetDefault() is not null;

    /// <summary>Smoothed illuminance in lux, once a reading has arrived.</summary>
    public double? Lux
    {
        get { lock (_gate) return _smoothedLux; }
    }

    public override void Start()
    {
        if (_sensor is null) return;
        _sensor.ReportInterval = Math.Max(_sensor.MinimumReportInterval, PreferredReportIntervalMs);
        _sensor.ReadingChanged += OnReadingChanged;
        if (_sensor.GetCurrentReading() is { } reading) Update(reading.IlluminanceInLux);
    }

    private void OnReadingChanged(LightSensor sender, LightSensorReadingChangedEventArgs e) => Update(e.Reading.IlluminanceInLux);

    private void Update(double lux)
    {
        double smoothed;
        lock (_gate)
        {
            smoothed = _smoothedLux is double previous ? previous + Smoothing * (lux - previous) : lux;
            _smoothedLux = smoothed;
        }
        Report(AdaptiveCurves.AmbientLevel(smoothed));
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing || _sensor is null) return;
        _sensor.ReadingChanged -= OnReadingChanged;
        _sensor.ReportInterval = 0; // back to the driver default
    }
}
