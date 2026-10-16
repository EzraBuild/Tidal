namespace Tidal.ViewModels.Platform;

/// <summary>Starting Tidal, in the tray, when the user signs in.</summary>
public interface ISignInStartup
{
    /// <summary>True when the system will start this copy of Tidal at sign-in.</summary>
    bool IsEnabled { get; }

    /// <summary>True when Tidal is set to start but the system's own startup settings turned it off.</summary>
    bool IsTurnedOffBySystem { get; }

    /// <summary>
    /// Starts Tidal at sign-in. Throws <see cref="UnauthorizedAccessException"/>,
    /// <see cref="System.Security.SecurityException"/> or <see cref="IOException"/> when the system refuses.
    /// </summary>
    void Enable();

    /// <summary>Stops starting Tidal at sign-in. Throws like <see cref="Enable"/>.</summary>
    void Disable();
}
