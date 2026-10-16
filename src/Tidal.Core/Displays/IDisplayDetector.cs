namespace Tidal.Core.Displays;

/// <summary>Finds the connected displays. Each operating system has its own.</summary>
public interface IDisplayDetector
{
    /// <summary>
    /// Returns a new <see cref="Display"/> for every connected display, in left-to-right order, not
    /// yet probed (<see cref="DisplayManager"/> does that). Runs on a background thread.
    /// </summary>
    IReadOnlyList<Display> Detect();
}
