using System.Text.Json.Serialization;
using Tidal.Core.Displays;

namespace Tidal.Core.Settings;

/// <summary>Everything Tidal remembers between runs. Serialized to JSON by <see cref="SettingsStore"/>.</summary>
public sealed class TidalSettings
{
    public int Version { get; set; } = 1;

    /// <summary>Move every display's brightness together.</summary>
    public bool LinkAll { get; set; }

    public HotkeySettings Hotkeys { get; set; } = new();

    public AdaptiveSettings Adaptive { get; set; } = new();

    /// <summary>Per-display settings, keyed by <see cref="Display.Id"/>.</summary>
    public Dictionary<string, DisplaySettings> Displays { get; set; } = [];

    /// <summary>The settings for a display, created with defaults the first time it is seen.</summary>
    public DisplaySettings GetDisplay(string id)
    {
        if (!Displays.TryGetValue(id, out var settings)) Displays[id] = settings = new DisplaySettings();
        return settings;
    }

    /// <summary>Repairs what a hand-edited or older file may have left missing or out of range.</summary>
    internal void Normalize()
    {
        Hotkeys ??= new HotkeySettings();
        Hotkeys.Step = Math.Clamp(Hotkeys.Step, HotkeySettings.MinStep, HotkeySettings.MaxStep);
        Adaptive ??= new AdaptiveSettings();
        if (Adaptive.Latitude is double lat && (double.IsNaN(lat) || Math.Abs(lat) > 90)) Adaptive.Latitude = null;
        if (Adaptive.Longitude is double lon && (double.IsNaN(lon) || Math.Abs(lon) > 180)) Adaptive.Longitude = null;
        Displays ??= [];
        foreach (var (id, display) in Displays.ToList())
        {
            if (display is null)
            {
                Displays.Remove(id);
                continue;
            }
            var range = display.BrightnessRange;
            (display.MinBrightness, display.MaxBrightness) = (range.Min, range.Max);
            display.AdaptiveOffset = double.IsNaN(display.AdaptiveOffset) ? 0 : Math.Clamp(display.AdaptiveOffset, -1, 1);
        }
    }
}

public sealed class DisplaySettings
{
    public int MinBrightness { get; set; }
    public int MaxBrightness { get; set; } = 100;

    /// <summary>Follow adaptive brightness. Null means the default: external displays do, the built-in screen doesn't.</summary>
    public bool? Adaptive { get; set; }

    /// <summary>
    /// The user's adjustment on top of the adaptive level, as a fraction of this display's range
    /// (e.g. 0.1 = ten percent of the way further towards its maximum).
    /// </summary>
    public double AdaptiveOffset { get; set; }

    [JsonIgnore]
    public BrightnessRange BrightnessRange => new(MinBrightness, MaxBrightness);
}

/// <summary>What adaptive displays follow.</summary>
public enum AdaptiveMode
{
    Off,
    Sun,
    BuiltInDisplay,
    LightSensor,
}

public sealed class AdaptiveSettings
{
    public AdaptiveMode Mode { get; set; } = AdaptiveMode.Off;

    /// <summary>Location for the sun schedule, in degrees (north and east positive).</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

/// <summary>Which displays the brightness hotkeys change.</summary>
public enum HotkeyTarget
{
    AllDisplays,
    DisplayUnderPointer,
}

public sealed class HotkeySettings
{
    public const int MinStep = 1;
    public const int MaxStep = 50;

    /// <summary>Shortcut text such as "Ctrl+Alt+PageUp"; null when unassigned.</summary>
    public string? BrightnessUp { get; set; } = "Ctrl+Alt+PageUp";
    public string? BrightnessDown { get; set; } = "Ctrl+Alt+PageDown";

    /// <summary>Percentage points per key press.</summary>
    public int Step { get; set; } = 5;

    public HotkeyTarget Target { get; set; } = HotkeyTarget.AllDisplays;
}
