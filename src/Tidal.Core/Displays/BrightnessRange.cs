namespace Tidal.Core.Displays;

/// <summary>
/// The band of brightness a display may use, in percent (inclusive). Everything Tidal sets —
/// sliders, hotkeys, linked and adaptive changes — stays inside it.
/// </summary>
public readonly record struct BrightnessRange
{
    public static readonly BrightnessRange Full = new(0, 100);

    public BrightnessRange(int min, int max)
    {
        min = Math.Clamp(min, 0, 100);
        max = Math.Clamp(max, 0, 100);
        Min = Math.Min(min, max);
        Max = Math.Max(min, max);
    }

    public int Min { get; }
    public int Max { get; }
    public bool IsFull => Min == 0 && Max == 100;

    public int Clamp(int value) => Math.Clamp(value, Min, Max);

    /// <summary>
    /// Moves <paramref name="value"/> by <paramref name="delta"/> and lands inside the range, but
    /// never in the opposite direction: "brighter" on a display already above the maximum (e.g. set
    /// with the monitor's own buttons) leaves it alone rather than dimming it.
    /// </summary>
    public int Step(int value, int delta) => delta switch
    {
        > 0 when value >= Max => value,
        < 0 when value <= Min => value,
        0 => value,
        _ => Clamp(value + delta),
    };

    /// <summary>Where <paramref name="value"/> sits in the range, from 0 (at Min) to 1 (at Max).</summary>
    public double ToFraction(int value) => Max == Min ? 1 : (Clamp(value) - Min) / (double)(Max - Min);

    /// <summary>The value <paramref name="fraction"/> of the way from Min to Max.</summary>
    public int FromFraction(double fraction) => (int)Math.Round(Min + Math.Clamp(fraction, 0, 1) * (Max - Min));

    public override string ToString() => $"{Min}–{Max}%";
}
