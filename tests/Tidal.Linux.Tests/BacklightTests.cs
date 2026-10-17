using Tidal.Linux.Displays;

namespace Tidal.Linux.Tests;

public class BacklightTests
{
    private const string Intel = "/sys/class/backlight/intel_backlight";

    private static FakeSysfs Laptop(string type = "raw", int max = 400, int brightness = 200) => new FakeSysfs()
        .File($"{Intel}/type", type)
        .File($"{Intel}/max_brightness", max.ToString())
        .File($"{Intel}/brightness", brightness.ToString())
        .Link($"{Intel}/device", "/sys/devices/pci0000:00/0000:00:02.0/drm/card0/card0-eDP-1");

    [Fact]
    public void Reads_brightness_as_a_percentage_of_the_maximum()
    {
        var backlight = Backlight.Find(Laptop(max: 400, brightness: 300), new FakeLogind(), "card0-eDP-1");

        Assert.Equal(75, backlight?.ReadPercent());
    }

    [Fact]
    public void Writes_through_sysfs_when_allowed()
    {
        var sysfs = Laptop(max: 400);
        var logind = new FakeLogind();

        Backlight.Find(sysfs, logind, "card0-eDP-1")!.WritePercent(30);

        Assert.Equal("120", sysfs.Text($"{Intel}/brightness"));
        Assert.Empty(logind.Calls);
    }

    [Fact]
    public void Falls_back_to_logind_when_sysfs_refuses_and_stays_with_it()
    {
        var sysfs = Laptop(max: 400);
        sysfs.ReadOnly.Add($"{Intel}/brightness");
        var logind = new FakeLogind();
        var backlight = Backlight.Find(sysfs, logind, "card0-eDP-1")!;

        backlight.WritePercent(30);
        sysfs.ReadOnly.Clear(); // even if it would work now, logind is what works
        backlight.WritePercent(40);

        Assert.Equal([("backlight", "intel_backlight", 120u), ("backlight", "intel_backlight", 160u)], logind.Calls);
    }

    [Fact]
    public void Zero_percent_keeps_a_raw_backlight_on()
    {
        var sysfs = Laptop(type: "raw");

        Backlight.Find(sysfs, new FakeLogind(), "card0-eDP-1")!.WritePercent(0);

        Assert.Equal("1", sysfs.Text($"{Intel}/brightness"));
    }

    [Fact]
    public void Zero_percent_is_the_lowest_firmware_level()
    {
        var sysfs = Laptop(type: "firmware");

        Backlight.Find(sysfs, new FakeLogind(), "card0-eDP-1")!.WritePercent(0);

        Assert.Equal("0", sysfs.Text($"{Intel}/brightness"));
    }

    [Fact]
    public void Prefers_firmware_then_platform_then_raw()
    {
        var sysfs = Laptop()
            .File("/sys/class/backlight/acpi_video0/type", "firmware")
            .File("/sys/class/backlight/acpi_video0/max_brightness", "15")
            .File("/sys/class/backlight/dell_backlight/type", "platform")
            .File("/sys/class/backlight/dell_backlight/max_brightness", "7");

        Assert.Equal("acpi_video0", Backlight.Find(sysfs, new FakeLogind(), "card0-eDP-1")?.Name);
    }

    [Fact]
    public void Skips_backlights_of_other_outputs_and_external_monitors()
    {
        var sysfs = Laptop()
            .File("/sys/class/backlight/ddcci5/type", "firmware")
            .File("/sys/class/backlight/ddcci5/max_brightness", "100")
            .Link("/sys/class/backlight/ddcci5/device", "/sys/devices/pci0000:00/0000:00:02.0/i2c-5/5-0037");

        Assert.Equal("intel_backlight", Backlight.Find(sysfs, new FakeLogind(), "card0-eDP-1")?.Name);
        Assert.Null(Backlight.Find(sysfs, new FakeLogind(), "card0-eDP-2"));
    }
}
