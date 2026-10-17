using System.Text.Json.Serialization;
using Tidal.Core.Displays;

namespace Tidal.Core.Settings;

/// <summary>Everything Tidal remembers between runs. Serialized to JSON by <see cref="SettingsStore"/>.</summary>
public sealed class TidalSettings
{
    public int Version { get; set; } = 1;

    /// <summary>Move every display's brightness together.</summary>
    public bool LinkAll { get; set; }

    /// <summary>Check for new versions now and then, and download them in the background.</summary>
    public bool AutoUpdate { get; set; } = true;

    public HotkeySettings Hotkeys { get; set; } = new();

    public AdaptiveSettings Adaptive { get; set; } = new();

    /// <summary>Per-display settings, keyed by <see cref="Display.Id"/>.</summary>
    public Dictionary<string, DisplaySettings> Displays { get; set; } = [];

    /// <summary>Brightness to use while particular apps are in front.</summary>
    public List<AppPreset> AppPresets { get; set; } = [];

    /// <summary>The preset for the program at <paramref name="programPath"/> (a path or a file name), if there is one.</summary>
    public AppPreset? FindAppPreset(string programPath) => AppPresets.FirstOrDefault(p => p.Matches(programPath));

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
            display.AdaptiveOffset = double.IsNaN(display.AdaptiveOffset)
                ? 0
                : Math.Clamp(display.AdaptiveOffset, -DisplaySettings.MaxAdaptiveOffset, DisplaySettings.MaxAdaptiveOffset);
            display.BelowZeroLevel = Math.Clamp(display.BelowZeroLevel, 0, BrightnessRange.BelowZeroDepth);
            if (display.SoftwareBrightness is int software) display.SoftwareBrightness = Math.Clamp(software, 0, 100);
        }

        // One preset per program, each naming a program file.
        AppPresets = (AppPresets ?? [])
            .Where(p => p is not null && !string.IsNullOrWhiteSpace(p.App))
            .Select(p =>
            {
                p.App = AppPreset.ProgramName(p.App);
                p.Brightness = Math.Clamp(p.Brightness, 0, 100);
                return p;
            })
            .DistinctBy(p => p.App, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

/// <summary>A brightness to use while a particular app is in front.</summary>
public sealed class AppPreset
{
    /// <summary>The app's program file name, e.g. <c>vlc.exe</c>. Apps are matched by this alone.</summary>
    public string App { get; set; } = "";

    /// <summary>What to call the app in Settings, e.g. "VLC media player".</summary>
    public string? Name { get; set; }

    /// <summary>Where the program was found, for its icon. Optional.</summary>
    public string? Path { get; set; }

    /// <summary>
    /// Brightness while the app is in front, in percent of each display's range: 0 is a display's
    /// lowest, 100 its highest (the same scale as the popup's presets).
    /// </summary>
    public int Brightness { get; set; } = 100;

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? System.IO.Path.GetFileNameWithoutExtension(App) : Name;

    /// <summary>True when <paramref name="programPath"/> (a path or a file name) is this preset's app.</summary>
    public bool Matches(string programPath)
        => programPath.Length > 0 && string.Equals(ProgramName(programPath), ProgramName(App), StringComparison.OrdinalIgnoreCase);

    /// <summary>The program file name for a path or name: <c>C:\Apps\VLC\vlc.exe</c> and <c>vlc</c> both give <c>vlc.exe</c>.</summary>
    public static string ProgramName(string programPath)
    {
        // Either separator, whatever the OS: the paths come from Windows.
        string path = programPath.Trim();
        string name = path[(path.LastIndexOfAny(['/', '\\']) + 1)..];
        return System.IO.Path.HasExtension(name) ? name : name + ".exe";
    }
}

public sealed class DisplaySettings
{
    /// <summary>
    /// Largest adjustment on top of adaptive brightness, as a fraction of the range. Beyond 1 so an
    /// adjustment from the top of the range can reach the bottom of software dimming.
    /// </summary>
    public const double MaxAdaptiveOffset = 2;

    public int MinBrightness { get; set; }
    public int MaxBrightness { get; set; } = 100;

    /// <summary>Follow adaptive brightness. Null means the default: external displays do, the built-in screen doesn't.</summary>
    public bool? Adaptive { get; set; }

    /// <summary>
    /// The user's adjustment on top of the adaptive level, as a fraction of this display's range
    /// (e.g. 0.1 = ten percent of the way further towards its maximum).
    /// </summary>
    public double AdaptiveOffset { get; set; }

    /// <summary>Keep dimming in software after the hardware reaches its lowest brightness.</summary>
    public bool DimBelowZero { get; set; }

    /// <summary>
    /// How far below zero the display is dimmed (0 to <see cref="BrightnessRange.BelowZeroDepth"/>),
    /// kept so the dimming survives a restart or the displays being detected again.
    /// </summary>
    public int BelowZeroLevel { get; set; }

    /// <summary>Brightness of a display Tidal can only dim in software (0–100); null means not dimmed.</summary>
    public int? SoftwareBrightness { get; set; }

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
