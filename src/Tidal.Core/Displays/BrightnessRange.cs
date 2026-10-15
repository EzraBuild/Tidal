namespace Tidal.Core.Displays;

/// <summary>
/// The band of brightness a display may use, in percent (inclusive). Everything Tidal sets —
/// sliders, hotkeys, linked and adaptive changes — stays inside it.
/// </summary>
/// <remarks>
/// With <see cref="BelowZero"/> on, the scale continues past the bottom of the band into
/// software dimming: values from -1 down to -<see cref="BelowZeroDepth"/> keep the hardware at
/// <see cref="Min"/> and darken the picture instead (see <see cref="SoftwareDimming"/>).
/// </remarks>
public readonly record struct BrightnessRange
{
    public static readonly BrightnessRange Full = new(0, 100);

    /// <summary>How far below zero the scale goes when dimming below zero.</summary>
    public const int BelowZeroDepth = 50;

    public BrightnessRange(int min, int max, bool belowZero = false)
    {
        min = Math.Clamp(min, 0, 100);
        max = Math.Clamp(max, 0, 100);
        Min = Math.Min(min, max);
        Max = Math.Max(min, max);
        BelowZero = belowZero;
    }

    public int Min { get; }
    public int Max { get; }

    /// <summary>Whether the scale continues below zero into software dimming.</summary>
    public bool BelowZero { get; }

    public bool IsFull => Min == 0 && Max == 100;

    /// <summary>The lowest value on the scale: -<see cref="BelowZeroDepth"/> when dimming below zero, otherwise <see cref="Min"/>.</summary>
    public int Bottom => BelowZero ? -BelowZeroDepth : Min;

    /// <summary>
    /// The nearest allowed value. Below zero is allowed only when dimming below zero; the gap
    /// between zero and <see cref="Min"/> never is.
    /// </summary>
    public int Clamp(int value) => BelowZero && value < 0 ? Math.Max(value, -BelowZeroDepth) : Math.Clamp(value, Min, Max);

    /// <summary>
    /// Moves <paramref name="value"/> by <paramref name="delta"/> and lands inside the range, but
    /// never in the opposite direction: "brighter" on a display already above the maximum (e.g. set
    /// with the monitor's own buttons) leaves it alone rather than dimming it. When dimming below
    /// zero, a step pauses at <see cref="Min"/> on the way down and lands on it on the way up, so
    /// software dimming starts and ends exactly at the display's lowest hardware brightness.
    /// </summary>
    public int Step(int value, int delta) => delta switch
    {
        0 => value,
        > 0 when value >= Max => value,
        > 0 when value < 0 => value + delta >= 0 ? Min : value + delta,
        > 0 => Clamp(value + delta),
        < 0 when value <= Bottom => value,
        < 0 when value > Min => Math.Clamp(value + delta, Min, Max),
        < 0 when BelowZero => Math.Max(Math.Min(value, 0) + delta, -BelowZeroDepth),
        _ => value, // at or below the minimum, with nowhere further to go
    };

    /// <summary>
    /// Where <paramref name="value"/> sits in the range, from 0 (at Min) to 1 (at Max). Below zero
    /// it continues to -1 (the darkest software dimming).
    /// </summary>
    public double ToFraction(int value)
    {
        if (BelowZero && value < 0) return Clamp(value) / (double)BelowZeroDepth;
        return Max == Min ? 1 : (Math.Clamp(value, Min, Max) - Min) / (double)(Max - Min);
    }

    /// <summary>The value <paramref name="fraction"/> of the way from Min to Max; negative fractions dim below zero when allowed.</summary>
    public int FromFraction(double fraction)
    {
        if (BelowZero && fraction < 0)
        {
            int below = (int)Math.Round(Math.Max(fraction, -1) * BelowZeroDepth);
            return below < 0 ? below : Min;
        }
        return (int)Math.Round(Min + Math.Clamp(fraction, 0, 1) * (Max - Min));
    }

    public override string ToString() => $"{Min}–{Max}%";
}
