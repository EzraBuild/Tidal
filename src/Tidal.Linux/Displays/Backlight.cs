using System.Globalization;

namespace Tidal.Linux.Displays;

/// <summary>A backlight in /sys/class/backlight, such as <c>intel_backlight</c> or <c>amdgpu_bl1</c>.</summary>
public sealed class Backlight
{
    public const string Root = "/sys/class/backlight";

    private readonly ISysfs _sysfs;
    private readonly ILogind _logind;
    private bool _writable = true; // until sysfs says otherwise

    internal Backlight(ISysfs sysfs, ILogind logind, string name, string type, int maxBrightness)
    {
        _sysfs = sysfs;
        _logind = logind;
        Name = name;
        Type = type;
        MaxBrightness = maxBrightness;
    }

    public string Name { get; }

    /// <summary>Who controls it: <c>firmware</c> (ACPI), <c>platform</c> (a vendor driver) or <c>raw</c> (the graphics card).</summary>
    public string Type { get; }

    public int MaxBrightness { get; }

    // At 0 a raw or platform backlight may switch off completely and leave the screen black.
    // Firmware levels start at the dimmest visible one.
    private int MinBrightness => Type == "firmware" ? 0 : 1;

    /// <summary>The brightness in percent, or null if it can't be read.</summary>
    public int? ReadPercent()
    {
        if (!int.TryParse(_sysfs.ReadText($"{Root}/{Name}/brightness"), NumberStyles.None, CultureInfo.InvariantCulture, out int raw)) return null;
        return (int)Math.Round(Math.Min(raw, MaxBrightness) * 100.0 / MaxBrightness);
    }

    /// <summary>
    /// Sets the brightness in percent: straight through sysfs where this user may write it (a udev
    /// rule for the video group, for example), otherwise through logind.
    /// </summary>
    public void WritePercent(int percent)
    {
        int raw = Math.Max(MinBrightness, (int)Math.Round(Math.Clamp(percent, 0, 100) * MaxBrightness / 100.0));
        if (_writable && _sysfs.TryWrite($"{Root}/{Name}/brightness", raw.ToString(CultureInfo.InvariantCulture))) return;
        _writable = false;
        _logind.SetBrightness("backlight", Name, (uint)raw);
    }

    /// <summary>The backlight of the built-in screen on output <paramref name="connector"/>, if Linux has one.</summary>
    internal static Backlight? Find(ISysfs sysfs, ILogind logind, string connector)
    {
        var candidates = new List<(Backlight Backlight, string? Owner)>();
        foreach (var name in sysfs.List(Root))
        {
            if (name.StartsWith("ddcci", StringComparison.Ordinal)) continue; // external monitors, through the ddcci driver
            string directory = $"{Root}/{name}";
            if (!int.TryParse(sysfs.ReadText($"{directory}/max_brightness"), NumberStyles.None, CultureInfo.InvariantCulture, out int max) || max <= 0) continue;
            var backlight = new Backlight(sysfs, logind, name, sysfs.ReadText($"{directory}/type") ?? "raw", max);
            candidates.Add((backlight, sysfs.LinkName($"{directory}/device")));
        }

        // Skip backlights another output owns. Of the rest, prefer firmware, then platform, then
        // raw, as GNOME and KDE do.
        return candidates
            .Where(c => c.Owner is null || !c.Owner.StartsWith("card", StringComparison.Ordinal) || c.Owner == connector)
            .OrderBy(c => c.Backlight.Type switch { "firmware" => 0, "platform" => 1, _ => 2 })
            .Select(c => c.Backlight)
            .FirstOrDefault();
    }
}
