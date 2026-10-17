using System.Text.RegularExpressions;

namespace Tidal.Linux.Displays;

/// <summary>A video output as the kernel's graphics drivers list it in /sys/class/drm, e.g. <c>card0-DP-1</c>.</summary>
internal sealed partial record DrmConnector
{
    public const string Root = "/sys/class/drm";

    /// <summary>The sysfs name, e.g. <c>card1-HDMI-A-1</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The kind of output, e.g. <c>eDP</c>, <c>DP</c>, <c>HDMI-A</c>.</summary>
    public required string Type { get; init; }

    /// <summary>The output's name without the card, e.g. <c>HDMI-A-1</c>.</summary>
    public required string Port { get; init; }

    /// <summary>True when a monitor is plugged in and the desktop shows a picture on it.</summary>
    public bool IsActive { get; init; }

    public byte[]? EdidBytes { get; init; }

    public Edid? Edid { get; init; }

    /// <summary>The I2C bus to the monitor (its DDC wires or DisplayPort AUX channel), when known.</summary>
    public int? I2cBus { get; init; }

    /// <summary>The monitor's preferred resolution, when it lists its modes.</summary>
    public (int Width, int Height)? Resolution { get; init; }

    /// <summary>Built-in panels: eDP and LVDS in laptops, DSI and DPI in tablets and boards.</summary>
    public bool IsInternal => Type is "eDP" or "LVDS" or "DSI" or "DPI";

    /// <summary>How the display is connected, in the words <see cref="Core.Displays.DisplayInfo.Connection"/> uses.</summary>
    public string Connection => Type switch
    {
        _ when IsInternal => "Built-in",
        "DP" => "DisplayPort",
        "HDMI-A" or "HDMI-B" => "HDMI",
        "DVI-D" or "DVI-I" or "DVI-A" => "DVI",
        "VGA" => "VGA",
        "USB" => "USB display",
        "Virtual" => "Virtual",
        _ => "Other",
    };

    /// <summary>Every output on every graphics card, connected or not.</summary>
    public static List<DrmConnector> ReadAll(ISysfs sysfs)
    {
        var connectors = new List<DrmConnector>();
        foreach (var name in sysfs.List(Root).Order(StringComparer.Ordinal))
        {
            var match = ConnectorName().Match(name);
            if (!match.Success) continue; // card0, renderD128, version

            string directory = $"{Root}/{name}";
            var edid = sysfs.ReadBytes($"{directory}/edid") is { Length: >= 128 } bytes ? bytes : null;
            connectors.Add(new DrmConnector
            {
                Name = name,
                Type = match.Groups["type"].Value,
                Port = name[(name.IndexOf('-') + 1)..],
                IsActive = sysfs.ReadText($"{directory}/status") == "connected"
                           && sysfs.ReadText($"{directory}/enabled") == "enabled",
                EdidBytes = edid,
                Edid = edid is null ? null : Edid.Parse(edid),
                I2cBus = FindBus(sysfs, directory),
                Resolution = ParseResolution(sysfs.ReadText($"{directory}/modes")),
            });
        }
        return connectors;
    }

    /// <summary>The bus number in an adapter's name, e.g. 5 for <c>i2c-5</c>.</summary>
    public static int? ParseBus(string? name)
        => name is not null && name.StartsWith("i2c-", StringComparison.Ordinal) && int.TryParse(name.AsSpan(4), out int bus) ? bus : null;

    // Drivers link an output's DDC bus as "ddc". DisplayPort AUX channels appear as an i2c-N
    // device under the output instead.
    private static int? FindBus(ISysfs sysfs, string directory)
        => ParseBus(sysfs.LinkName($"{directory}/ddc"))
           ?? sysfs.List(directory).Select(ParseBus).FirstOrDefault(bus => bus is not null);

    // "modes" lists the preferred mode first, e.g. "2560x1440".
    private static (int, int)? ParseResolution(string? modes)
    {
        var match = modes is null ? null : ModeSize().Match(modes);
        return match is { Success: true }
            ? (int.Parse(match.Groups[1].ValueSpan), int.Parse(match.Groups[2].ValueSpan))
            : null;
    }

    [GeneratedRegex(@"^card\d+-(?<type>.+)-\d+$")]
    private static partial Regex ConnectorName();

    [GeneratedRegex(@"^(\d+)x(\d+)")]
    private static partial Regex ModeSize();
}
