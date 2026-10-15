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
        }
    }
}

public sealed class DisplaySettings
{
    public int MinBrightness { get; set; }
    public int MaxBrightness { get; set; } = 100;

    [JsonIgnore]
    public BrightnessRange BrightnessRange => new(MinBrightness, MaxBrightness);
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
