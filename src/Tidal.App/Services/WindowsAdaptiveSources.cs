using System.Runtime.InteropServices;
using Tidal.Core.Adaptive;
using Tidal.ViewModels.Platform;
using Tidal.Windows.Adaptive;
using Windows.Devices.Geolocation;

namespace Tidal.App.Services;

/// <summary>The built-in screen (WMI), the light sensor (WinRT) and Windows' location service.</summary>
internal sealed class WindowsAdaptiveSources : IAdaptiveSources
{
    public bool HasBuiltInDisplay { get; } = WmiBuiltInDisplaySource.IsAvailable;

    public bool HasLightSensor { get; } = WindowsLightSensorSource.IsAvailable;

    public BuiltInDisplaySource CreateBuiltInDisplaySource() => new WmiBuiltInDisplaySource();

    public LightSensorSource CreateLightSensorSource() => new WindowsLightSensorSource();

    public async Task<GeoPoint> LocateAsync()
    {
        try
        {
            if (await Geolocator.RequestAccessAsync() != GeolocationAccessStatus.Allowed)
                throw new LocationException("Windows location access is off for desktop apps. Turn it on in Settings › Privacy & security › Location, or type your coordinates.");
            var locator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
            var position = await locator.GetGeopositionAsync(TimeSpan.FromHours(1), TimeSpan.FromSeconds(15));
            var point = position.Coordinate.Point.Position;
            return new GeoPoint(point.Latitude, point.Longitude);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or TaskCanceledException or COMException)
        {
            throw new LocationException("Windows couldn't find a location. Type your coordinates instead.");
        }
    }
}
