using Tidal.Core.Displays;

namespace Tidal.Core.Tests;

public class InputSourceTests
{
    [Theory]
    [InlineData(0x0F, "DisplayPort 1")]
    [InlineData(0x11, "HDMI 1")]
    [InlineData(0x12, "HDMI 2")]
    [InlineData(0x13, "USB-C")]   // Lenovo
    [InlineData(0x1B, "USB-C")]   // Dell
    [InlineData(0x90, "Input 90")] // unknown vendor code
    public void Inputs_have_readable_names(byte code, string expected)
    {
        Assert.Equal(expected, new InputSource(code).Name);
    }

    [Fact]
    public void Inputs_from_a_real_capabilities_string()
    {
        // Lenovo ThinkVision P27h-20: DisplayPort, HDMI and USB-C.
        var caps = MonitorCapabilities.Parse("(prot(monitor)type(LCD)model(P27h-20)vcp(10 12 60(0F 11 13) D6(01 04 05))mccs_ver(2.2))");
        var inputs = caps.AllowedValues(VcpCode.InputSource).Select(code => new InputSource(code).Name);
        Assert.Equal(new[] { "DisplayPort 1", "HDMI 1", "USB-C" }, inputs);
    }
}
