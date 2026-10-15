using Tidal.Core.Displays;

namespace Tidal.Core.Tests;

public class MonitorCapabilitiesTests
{
    // Captured from a Dell P2422HE.
    private const string DellP2422HE =
        "(prot(monitor)type(lcd)model(P2422HE)cmds(01 02 03 07 0C E3 F3)vcp(02 04 05 08 10 12 14(05 08 0B 0C) 16 18 1A 52 " +
        "60( 0F 11 1B) AA(01 02 04) AC AE B2 B6 C6 C8 C9 CC(02 03 04 06 09 0A 0D 0E) D6(01 04 05) DC(00 03 05 ) DF E0 E1 " +
        "E2(00 02 04 0E 12 14) F1 F2 FD FE)mccs_ver(2.1)mswhql(1))";

    [Fact]
    public void Parses_a_real_capabilities_string()
    {
        var caps = MonitorCapabilities.Parse(DellP2422HE);

        Assert.Equal("P2422HE", caps.Model);
        Assert.Equal("lcd", caps.Type);
        Assert.Equal("2.1", caps.MccsVersion);
        Assert.Equal(31, caps.VcpCodes.Count);
        Assert.True(caps.Supports(VcpCode.Brightness));
        Assert.Empty(caps.AllowedValues(VcpCode.Brightness));
        Assert.Equal(new byte[] { 0x0F, 0x11, 0x1B }, caps.AllowedValues(VcpCode.InputSource));
        Assert.Equal(new byte[] { 0x00, 0x03, 0x05 }, caps.AllowedValues(0xDC));
        Assert.False(caps.Supports(VcpCode.AudioVolume));
    }

    [Fact]
    public void Tolerates_missing_outer_parentheses_and_nul_padding()
    {
        var caps = MonitorCapabilities.Parse("prot(monitor)model(X)vcp(10 12 62)mccs_ver(2.2)\0\0");

        Assert.Equal("X", caps.Model);
        Assert.Equal("2.2", caps.MccsVersion);
        Assert.True(caps.Supports(VcpCode.AudioVolume));
    }

    [Fact]
    public void Splits_codes_and_values_written_without_spaces()
    {
        var caps = MonitorCapabilities.Parse("(vcp(021012 60(0F111B)62))");

        Assert.Equal(new byte[] { 0x02, 0x10, 0x12, 0x60, 0x62 }, caps.VcpCodes.Keys.Order());
        Assert.Equal(new byte[] { 0x0F, 0x11, 0x1B }, caps.AllowedValues(VcpCode.InputSource));
    }

    [Fact]
    public void Does_not_confuse_vcp_with_vcpname()
    {
        var caps = MonitorCapabilities.Parse("(vcp(10 E0)vcpname(E0(Custom thing)))");

        Assert.Equal(new byte[] { 0x10, 0xE0 }, caps.VcpCodes.Keys.Order());
        Assert.Equal("E0(Custom thing)", caps.Section("vcpname"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("(vcp(10 12")]
    [InlineData(")))(((")]
    public void Never_throws_on_malformed_input(string raw)
    {
        var caps = MonitorCapabilities.Parse(raw);
        Assert.NotNull(caps.VcpCodes);
    }

    [Fact]
    public void Keeps_what_it_can_from_an_unterminated_string()
    {
        var caps = MonitorCapabilities.Parse("(model(Y)vcp(10 12 60(0F 11");

        Assert.Equal("Y", caps.Model);
        Assert.True(caps.Supports(VcpCode.Contrast));
        Assert.Equal(new byte[] { 0x0F, 0x11 }, caps.AllowedValues(VcpCode.InputSource));
    }
}
