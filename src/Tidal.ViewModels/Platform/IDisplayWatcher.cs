namespace Tidal.ViewModels.Platform;

/// <summary>Says when displays may have come or gone, so Tidal detects them again.</summary>
public interface IDisplayWatcher
{
    /// <summary>A display was connected or disconnected, or the layout changed. Raised on any thread.</summary>
    event EventHandler? DisplaysChanged;

    /// <summary>The computer woke from sleep. Raised on any thread.</summary>
    event EventHandler? Resumed;
}
