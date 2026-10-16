using Tidal.Core.Hotkeys;

namespace Tidal.ViewModels.Platform;

/// <summary>System-wide keyboard shortcuts, which work whichever app is in front.</summary>
public interface IHotkeys
{
    /// <summary>
    /// Registers <paramref name="shortcut"/> under <paramref name="id"/>, replacing what that id had.
    /// Returns false if the system or another app already owns the combination.
    /// </summary>
    bool Register(int id, Shortcut shortcut);

    void Unregister(int id);
}
