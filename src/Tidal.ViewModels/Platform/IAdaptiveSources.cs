using Tidal.Core.Adaptive;

namespace Tidal.ViewModels.Platform;

/// <summary>
/// The adaptive brightness inputs that depend on the computer: its built-in screen, its light
/// sensor, and where it is (for the sun).
/// </summary>
public interface IAdaptiveSources
{
    /// <summary>True when this computer has a built-in screen whose brightness can be read.</summary>
    bool HasBuiltInDisplay { get; }

    /// <summary>True when this computer has an ambient light sensor.</summary>
    bool HasLightSensor { get; }

    BuiltInDisplaySource CreateBuiltInDisplaySource();

    LightSensorSource CreateLightSensorSource();

    /// <summary>
    /// Asks the system where the computer is. Throws <see cref="LocationException"/>, with a message
    /// for the user, when it can't tell.
    /// </summary>
    Task<GeoPoint> LocateAsync();
}

public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>The location couldn't be found; the message says why, for the user.</summary>
public sealed class LocationException(string message) : Exception(message);
