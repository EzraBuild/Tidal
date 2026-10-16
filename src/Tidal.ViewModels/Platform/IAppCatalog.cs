namespace Tidal.ViewModels.Platform;

/// <summary>The programs on this computer, for app presets.</summary>
public interface IAppCatalog
{
    /// <summary>
    /// Paths of programs with a window open, leaving out Tidal and the system's own windows. May
    /// list a program more than once. Reads every process, so it's called off the UI thread.
    /// </summary>
    IEnumerable<string> FindRunningPrograms();

    /// <summary>The program's name for people, e.g. "Google Chrome" for chrome.exe.</summary>
    string GetDisplayName(string programPath);

    /// <summary>The program's icon as an image the UI framework can show, or null. Usable from any thread.</summary>
    object? GetIcon(string programPath);
}
