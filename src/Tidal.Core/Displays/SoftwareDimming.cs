namespace Tidal.Core.Displays;

/// <summary>
/// How dark Tidal's dimming overlay gets. Software dimming lays a see-through black layer over a
/// display instead of lowering its backlight: it can go below the lowest hardware brightness, and
/// it's the only brightness control for displays Tidal can't reach (no DDC/CI). The backlight stays
/// on, so it saves no power, and the picture loses a little contrast.
/// </summary>
public static class SoftwareDimming
{
    /// <summary>The darkest the overlay gets: 70% black, which keeps the picture readable.</summary>
    public const double MaxOpacity = 0.7;

    /// <summary>
    /// Overlay opacity for a display dimmed only in software, at <paramref name="percent"/>
    /// brightness: 100 is not dimmed, 0 is the darkest.
    /// </summary>
    public static double ForSoftwareBrightness(int percent) => (100 - Math.Clamp(percent, 0, 100)) / 100.0 * MaxOpacity;

    /// <summary>
    /// Overlay opacity for a display dimmed below zero: <paramref name="level"/> from 0 (not
    /// dimmed) down to -<see cref="BrightnessRange.BelowZeroDepth"/> (the darkest). Zero and above give none.
    /// </summary>
    public static double ForBelowZero(int level)
        => Math.Clamp(-level, 0, BrightnessRange.BelowZeroDepth) / (double)BrightnessRange.BelowZeroDepth * MaxOpacity;

    /// <summary>The overlay's alpha byte (0–255) for an opacity from 0 to 1.</summary>
    public static byte ToAlpha(double opacity) => (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255);
}
