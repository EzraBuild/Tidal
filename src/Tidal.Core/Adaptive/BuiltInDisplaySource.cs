namespace Tidal.Core.Adaptive;

/// <summary>
/// Follows the built-in screen's brightness, however it changes: brightness keys, the system's
/// slider, the system's own automatic brightness, or Tidal. Each operating system reads it its own way.
/// </summary>
public abstract class BuiltInDisplaySource : BrightnessSource
{
    /// <summary>The built-in screen's brightness in percent, as last read.</summary>
    public int? Percent => Level is double level ? (int)Math.Round(level * 100) : null;

    protected void ReportPercent(int percent) => Report(percent / 100.0);
}
