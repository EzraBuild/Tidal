using Tidal.Core.Displays;
using Tidal.Linux.Displays;

namespace Tidal.Linux.Tests;

public class LinuxDisplayDetectorTests
{
    private const string Drm = "/sys/class/drm";
    private const string Gpu = "/sys/devices/pci0000:00/0000:00:02.0";

    private readonly Dictionary<int, FakeMonitor> _buses = [];
    private readonly List<int> _opened = [];

    /// <summary>A laptop with its screen, a Dell on HDMI and an AOC on DisplayPort; one more port is empty.</summary>
    private static FakeSysfs Laptop() => new FakeSysfs()
        .File($"{Drm}/version", "drm 1.1.0")
        .File($"{Drm}/card0/dev", "226:0")
        .Output("card0-eDP-1", TestEdid.Create("AUO", 0xB39E), "1920x1080")
        .Output("card0-HDMI-A-1", TestEdid.Create("DEL", 0xA1C8, name: "DELL P2422HE", serialText: "8Q2G9N3"), "2560x1440\n1920x1080")
        .Link($"{Drm}/card0-HDMI-A-1/ddc", $"{Gpu}/i2c-3")
        .File($"{Drm}/card0-DP-1/status", "disconnected")
        .File($"{Drm}/card0-DP-1/enabled", "disabled")
        .Output("card0-DP-2", TestEdid.Create("AOC", 0x1615, name: "16T15"), "1920x1080")
        .File($"{Drm}/card0-DP-2/i2c-7/name", "DPDDC-B")
        .File("/sys/class/backlight/intel_backlight/type", "raw")
        .File("/sys/class/backlight/intel_backlight/max_brightness", "400")
        .File("/sys/class/backlight/intel_backlight/brightness", "200")
        .Link("/sys/class/backlight/intel_backlight/device", $"{Gpu}/drm/card0/card0-eDP-1");

    private LinuxDisplayDetector Detector(FakeSysfs sysfs) => new(sysfs, OpenBus, new FakeLogind());

    private I2cBus OpenBus(int number)
    {
        _opened.Add(number);
        return _buses.TryGetValue(number, out var monitor) ? monitor : throw new FileNotFoundException($"/dev/i2c-{number}");
    }

    [Fact]
    public void Lists_active_outputs_built_in_screen_first_then_by_output()
    {
        _buses[3] = new FakeMonitor();
        _buses[7] = new FakeMonitor();

        var displays = Detector(Laptop()).Detect();
        try
        {
            Assert.Equal(["Built-in Display", "16T15", "DELL P2422HE"], displays.Select(d => d.Name));
            Assert.IsType<BacklightDisplay>(displays[0]);
            Assert.IsType<DdcDisplay>(displays[1]);
            Assert.IsType<DdcDisplay>(displays[2]);
            Assert.Equal(["Built-in", "DisplayPort", "HDMI"], displays.Select(d => d.Info.Connection));
            Assert.Equal(["AUOB39E/eDP-1", "AOC1615/DP-2", "DELA1C8/8Q2G9N3"], displays.Select(d => d.Id));
            Assert.Equal("card0-HDMI-A-1", displays[2].Info.DeviceName);
            Assert.Equal([true, false, false], displays.Select(d => d.Info.IsPrimary));
            Assert.True(displays[0].Info.IsInternal);
            // Side by side at each monitor's preferred resolution.
            Assert.Equal(
                [new ScreenRect(0, 0, 1920, 1080), new ScreenRect(1920, 0, 3840, 1080), new ScreenRect(3840, 0, 6400, 1440)],
                displays.Select(d => d.Info.Bounds));
            Assert.Equal([7, 3], _opened);
        }
        finally
        {
            Dispose(displays);
        }
    }

    [Fact]
    public async Task Displays_read_their_hardware()
    {
        _buses[3] = new FakeMonitor { Vcp = { [VcpCode.Brightness] = (60, 100), [VcpCode.Contrast] = (75, 100) } };
        _buses[7] = new FakeMonitor(); // DDC/CI turned off: answers nothing

        using var manager = new DisplayManager(Detector(Laptop()));
        var displays = await manager.RefreshAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal([50, null, 60], displays.Select(d => d.Brightness));
        Assert.Equal([ControlMethod.Backlight, ControlMethod.None, ControlMethod.DdcCi], displays.Select(d => d.Method));
        Assert.Equal(DdcDisplay.NoReplyNote, displays[1].Note);
    }

    [Fact]
    public void Skips_turned_off_outputs_and_writeback_connectors()
    {
        var sysfs = Laptop()
            .Output("card0-HDMI-A-2", TestEdid.Create("SAM", 0x7154), "3840x2160", enabled: false)
            .Output("card0-Writeback-1", null, null);
        _buses[3] = new FakeMonitor();
        _buses[7] = new FakeMonitor();

        var displays = Detector(sysfs).Detect();
        Dispose(displays);

        Assert.Equal(3, displays.Count);
    }

    [Fact]
    public void Matches_unlinked_outputs_to_graphics_card_buses_by_EDID()
    {
        // NVIDIA's driver doesn't link outputs to buses. i2c-0 is the system's SMBus (memory, sensors).
        var dell = TestEdid.Create("DEL", 0xA1C8, name: "DELL P2422HE");
        var sysfs = new FakeSysfs()
            .Output("card1-DP-1", dell, "2560x1440")
            .Link("/sys/bus/i2c/devices/i2c-0", "/sys/devices/pci0000:00/0000:00:1f.4/i2c-0")
            .File("/sys/devices/pci0000:00/0000:00:1f.4/class", "0x0c0500")
            .Link("/sys/bus/i2c/devices/i2c-4", "/sys/devices/pci0000:00/0000:00:01.0/0000:01:00.0/i2c-4")
            .Link("/sys/bus/i2c/devices/i2c-5", "/sys/devices/pci0000:00/0000:00:01.0/0000:01:00.0/i2c-5")
            .File("/sys/devices/pci0000:00/0000:00:01.0/0000:01:00.0/class", "0x030000")
            .Link("/sys/bus/i2c/devices/4-0050", "/sys/devices/pci0000:00/0000:00:01.0/0000:01:00.0/i2c-4/4-0050");
        _buses[4] = new FakeMonitor { Edid = TestEdid.Create("GSM", 0x5B7F) }; // another monitor's EDID
        _buses[5] = new FakeMonitor { Edid = dell };

        var displays = Detector(sysfs).Detect();
        Dispose(displays);

        Assert.IsType<DdcDisplay>(Assert.Single(displays));
        Assert.DoesNotContain(0, _opened);
        Assert.Equal([4, 5, 5], _opened); // two EDID reads, then the DDC/CI channel
    }

    [Fact]
    public void Explains_a_bus_this_user_may_not_open()
    {
        var sysfs = new FakeSysfs()
            .Output("card0-HDMI-A-1", TestEdid.Create("DEL", 0xA1C8), "1920x1080")
            .Link($"{Drm}/card0-HDMI-A-1/ddc", $"{Gpu}/i2c-3");
        var detector = new LinuxDisplayDetector(sysfs, _ => throw new UnauthorizedAccessException(), new FakeLogind());

        var display = Assert.IsType<UncontrolledDisplay>(Assert.Single(detector.Detect()));

        Assert.Contains("/dev/i2c-3", display.Note);
        Assert.Contains("i2c group", display.Note);
    }

    [Fact]
    public void Explains_that_i2c_dev_is_needed()
    {
        var sysfs = new FakeSysfs()
            .Output("card0-HDMI-A-1", TestEdid.Create("DEL", 0xA1C8), "1920x1080")
            .Link($"{Drm}/card0-HDMI-A-1/ddc", $"{Gpu}/i2c-3");

        var display = Assert.IsType<UncontrolledDisplay>(Assert.Single(Detector(sysfs).Detect()));

        Assert.Contains("modprobe i2c-dev", display.Note);
    }

    [Fact]
    public void Identical_monitors_without_serial_numbers_get_their_own_names_and_ids()
    {
        var edid = TestEdid.Create("HWP", 0x3398, name: "HP E24");
        var sysfs = new FakeSysfs()
            .Output("card0-DP-1", edid, "1920x1080")
            .Output("card0-DP-2", edid, "1920x1080");

        var displays = Detector(sysfs).Detect();

        Assert.Equal(["HP E24 (1)", "HP E24 (2)"], displays.Select(d => d.Name));
        Assert.Equal(["HWP3398/DP-1", "HWP3398/DP-2"], displays.Select(d => d.Id));
    }

    [Fact]
    public void A_built_in_screen_without_a_backlight_has_no_control()
    {
        var sysfs = new FakeSysfs().Output("card0-eDP-1", TestEdid.Create("SDC", 0x4171), "2880x1800");

        var display = Assert.Single(Detector(sysfs).Detect());

        Assert.IsType<UncontrolledDisplay>(display);
        Assert.Equal("Built-in Display", display.Name);
    }

    private static void Dispose(IEnumerable<Display> displays)
    {
        foreach (var display in displays) display.Dispose();
    }
}

internal static class FakeSysfsOutputs
{
    /// <summary>Adds a graphics output with a monitor plugged in.</summary>
    public static FakeSysfs Output(this FakeSysfs sysfs, string name, byte[]? edid, string? modes, bool enabled = true)
    {
        string directory = $"/sys/class/drm/{name}";
        sysfs.File($"{directory}/status", "connected").File($"{directory}/enabled", enabled ? "enabled" : "disabled");
        if (edid is not null) sysfs.File($"{directory}/edid", edid);
        if (modes is not null) sysfs.File($"{directory}/modes", modes);
        return sysfs;
    }
}
