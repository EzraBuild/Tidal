using Tidal.Core;
using Tidal.Core.Displays;
using Tidal.Linux.Native;

namespace Tidal.Linux.Displays;

/// <summary>
/// Lists displays from the kernel's graphics outputs (/sys/class/drm). Each output's EDID gives the
/// monitor's name and identity, the I2C bus its driver links gives DDC/CI, and /sys/class/backlight
/// gives built-in screens.
/// </summary>
/// <remarks>
/// The kernel doesn't know how the desktop arranges displays, so <see cref="DisplayInfo.Bounds"/>
/// is a stand-in: displays side by side at their preferred resolution, built-in screen first.
/// </remarks>
public sealed class LinuxDisplayDetector : IDisplayDetector
{
    private const string I2cDevices = "/sys/bus/i2c/devices";
    private const byte EdidAddress = 0x50;

    private const string ModuleNote =
        "Tidal needs the i2c-dev kernel module to reach monitors over DDC/CI. Load it with \"sudo modprobe i2c-dev\", " +
        "and list it in /etc/modules-load.d so it loads at startup.";

    private readonly ISysfs _sysfs;
    private readonly Func<int, I2cBus> _openBus;
    private readonly ILogind _logind;

    public LinuxDisplayDetector() : this(new Sysfs(), DevI2cBus.Open, new Logind())
    {
    }

    internal LinuxDisplayDetector(ISysfs sysfs, Func<int, I2cBus> openBus, ILogind logind)
    {
        _sysfs = sysfs;
        _openBus = openBus;
        _logind = logind;
    }

    public IReadOnlyList<Display> Detect()
    {
        var connectors = DrmConnector.ReadAll(_sysfs)
            .Where(c => c.IsActive && c.Type != "Writeback")
            .OrderBy(c => !c.IsInternal)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .ToList();
        connectors = LinkBusesByEdid(connectors, out string? scanNote);

        var infos = new List<DisplayInfo>();
        int left = 0;
        foreach (var connector in connectors)
        {
            var info = BuildInfo(connector, left, isPrimary: infos.Count == 0);
            infos.Add(info);
            left = info.Bounds.Right;
        }
        MakeNamesUnique(infos);

        return connectors.Select((c, i) => CreateDisplay(c, infos[i], scanNote)).ToList();
    }

    private Display CreateDisplay(DrmConnector connector, DisplayInfo info, string? scanNote)
    {
        if (connector.IsInternal)
        {
            return Backlight.Find(_sysfs, _logind, connector.Name) is { } backlight
                ? new BacklightDisplay(info, backlight)
                : new UncontrolledDisplay(info, "Linux has no backlight control for this screen.");
        }
        if (connector.Type is "Virtual" or "USB")
            return new UncontrolledDisplay(info, $"{info.Connection} connections don't carry DDC/CI, so Tidal can't change this display's hardware settings.");
        if (connector.I2cBus is not int bus)
            return new UncontrolledDisplay(info, scanNote ?? "Linux didn't link this output to an I2C bus, so Tidal can't reach the monitor over DDC/CI.");

        try
        {
            return new DdcDisplay(info, new I2cDdcChannel(_openBus(bus)), DdcDisplay.NoReplyNote);
        }
        catch (FileNotFoundException)
        {
            return new UncontrolledDisplay(info, ModuleNote);
        }
        catch (UnauthorizedAccessException)
        {
            return new UncontrolledDisplay(info, PermissionNote($"/dev/i2c-{bus}"));
        }
        catch (IOException ex)
        {
            Log.Warning($"{info.Name}: can't open /dev/i2c-{bus}: {ex.Message}");
            return new UncontrolledDisplay(info, $"Tidal couldn't open /dev/i2c-{bus}: {ex.Message}");
        }
    }

    private static string PermissionNote(string path) =>
        $"Linux didn't let Tidal open {path}. Add yourself to the i2c group (\"sudo usermod -aG i2c $USER\") " +
        "or install ddcutil, which grants access, then sign out and back in.";

    private static DisplayInfo BuildInfo(DrmConnector connector, int left, bool isPrimary)
    {
        var edid = connector.Edid;
        var (width, height) = connector.Resolution ?? (1920, 1080);
        return new DisplayInfo
        {
            // Stable across ports when the monitor has a serial number; per port otherwise.
            Id = edid is null ? connector.Name : $"{edid.HardwareId}/{edid.Serial ?? connector.Port}",
            Name = connector.IsInternal ? "Built-in Display" : edid?.Name ?? $"Display {connector.Port}",
            DeviceName = connector.Name,
            Bounds = new ScreenRect(left, 0, left + width, height),
            IsPrimary = isPrimary,
            IsInternal = connector.IsInternal,
            Connection = connector.Connection,
            HardwareId = edid?.HardwareId,
        };
    }

    private static void MakeNamesUnique(List<DisplayInfo> infos)
    {
        foreach (var group in infos.Select((info, index) => (info, index)).GroupBy(x => x.info.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            int n = 1;
            foreach (var (info, index) in group) infos[index] = info with { Name = $"{info.Name} ({n++})" };
        }
    }

    /// <summary>
    /// Some drivers (NVIDIA's own, for one) don't link outputs to their I2C buses. For those, read
    /// the EDID on each unclaimed bus of a graphics card and match it to the output's.
    /// </summary>
    /// <param name="scanNote">Set when the scan couldn't open the buses, for outputs that stay unlinked.</param>
    private List<DrmConnector> LinkBusesByEdid(List<DrmConnector> connectors, out string? scanNote)
    {
        scanNote = null;
        var unlinked = connectors.Where(c => !c.IsInternal && c.I2cBus is null && c.EdidBytes is not null).ToList();
        if (unlinked.Count == 0) return connectors;

        var claimed = connectors.Select(c => c.I2cBus).OfType<int>().ToHashSet();
        var found = new Dictionary<string, int>();
        foreach (int bus in GraphicsCardBuses().Where(b => !claimed.Contains(b)))
        {
            byte[] edid = new byte[128];
            try
            {
                using var i2c = _openBus(bus);
                i2c.Write(EdidAddress, [0]); // read from the start
                i2c.Read(EdidAddress, edid);
            }
            catch (FileNotFoundException)
            {
                scanNote = ModuleNote;
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                scanNote = PermissionNote("/dev/i2c-*");
                continue;
            }
            catch (IOException ex)
            {
                Log.Debug($"i2c-{bus}: no EDID ({ex.Message})");
                continue;
            }

            var match = unlinked.FirstOrDefault(c => !found.ContainsKey(c.Name) && c.EdidBytes!.AsSpan(0, 128).SequenceEqual(edid));
            if (match is null) continue;
            Log.Debug($"{match.Name}: matched to i2c-{bus} by EDID");
            found[match.Name] = bus;
        }
        return connectors.Select(c => found.TryGetValue(c.Name, out int bus) ? c with { I2cBus = bus } : c).ToList();
    }

    // Only buses on a graphics card (PCI class 03xxxx), never the ones to memory or sensors.
    private IEnumerable<int> GraphicsCardBuses()
    {
        foreach (var name in _sysfs.List(I2cDevices))
        {
            if (DrmConnector.ParseBus(name) is not int bus) continue;
            string adapter = _sysfs.RealPath($"{I2cDevices}/{name}");
            string parent = adapter[..Math.Max(0, adapter.LastIndexOf('/'))];
            if (_sysfs.ReadText($"{parent}/class")?.StartsWith("0x03", StringComparison.Ordinal) == true) yield return bus;
        }
    }
}
