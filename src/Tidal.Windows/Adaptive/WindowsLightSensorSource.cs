using Tidal.Core.Adaptive;
using Windows.Devices.Sensors;

namespace Tidal.Windows.Adaptive;

/// <summary>Reads the ambient light sensor through Windows' sensor API.</summary>
public sealed class WindowsLightSensorSource : LightSensorSource
{
    private const uint PreferredReportIntervalMs = 1000;

    private readonly LightSensor? _sensor = LightSensor.GetDefault();

    public static bool IsAvailable => LightSensor.GetDefault() is not null;

    public override void Start()
    {
        if (_sensor is null) return;
        _sensor.ReportInterval = Math.Max(_sensor.MinimumReportInterval, PreferredReportIntervalMs);
        _sensor.ReadingChanged += OnReadingChanged;
        if (_sensor.GetCurrentReading() is { } reading) ReportLux(reading.IlluminanceInLux);
    }

    private void OnReadingChanged(LightSensor sender, LightSensorReadingChangedEventArgs e) => ReportLux(e.Reading.IlluminanceInLux);

    protected override void Dispose(bool disposing)
    {
        if (!disposing || _sensor is null) return;
        _sensor.ReadingChanged -= OnReadingChanged;
        _sensor.ReportInterval = 0; // back to the driver default
    }
}
