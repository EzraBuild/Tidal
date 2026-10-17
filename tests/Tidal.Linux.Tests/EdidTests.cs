using Tidal.Linux.Displays;

namespace Tidal.Linux.Tests;

public class EdidTests
{
    [Fact]
    public void Reads_maker_model_name_and_serial()
    {
        var edid = Edid.Parse(TestEdid.Create("DEL", 0xA1C8, serial: 0x4C333830, name: "DELL P2422HE", serialText: "8Q2G9N3"));

        Assert.NotNull(edid);
        Assert.Equal("DEL", edid.ManufacturerId);
        Assert.Equal(0xA1C8, edid.ProductCode);
        Assert.Equal("DELA1C8", edid.HardwareId);
        Assert.Equal("DELL P2422HE", edid.Name);
        Assert.Equal("8Q2G9N3", edid.Serial);
    }

    [Fact]
    public void A_name_that_fills_the_descriptor_has_no_line_feed()
    {
        var edid = Edid.Parse(TestEdid.Create("GSM", 0x5B7F, name: "LG ULTRAGEAR+"));

        Assert.Equal("LG ULTRAGEAR+", edid?.Name);
    }

    [Fact]
    public void Without_a_serial_descriptor_the_numeric_serial_is_used()
    {
        Assert.Equal("0000ABCD", Edid.Parse(TestEdid.Create("AOC", 0x1615, serial: 0xABCD))?.Serial);
        Assert.Null(Edid.Parse(TestEdid.Create("AOC", 0x1615))?.Serial);
    }

    [Fact]
    public void Missing_descriptors_are_null()
    {
        var edid = Edid.Parse(TestEdid.Create("AUO", 0xB39E));

        Assert.Null(edid?.Name);
        Assert.Equal("AUOB39E", edid?.HardwareId);
    }

    [Fact]
    public void Anything_else_is_not_an_EDID()
    {
        Assert.Null(Edid.Parse(new byte[128]));
        Assert.Null(Edid.Parse(TestEdid.Create("DEL", 1).AsSpan(0, 100)));
    }
}
