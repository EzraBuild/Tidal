using Tidal.Core.Displays;
using Tidal.Linux.Displays;

namespace Tidal.Linux.Tests;

public class I2cDdcChannelTests
{
    private static I2cDdcChannel Channel(FakeMonitor monitor) => new(monitor) { Sleep = _ => { } };

    [Fact]
    public void Get_VCP_sends_the_standard_request()
    {
        var monitor = new FakeMonitor { Vcp = { [VcpCode.Brightness] = (40, 100) } };

        Channel(monitor).GetVcp(VcpCode.Brightness);

        // Source 0x51, length 2, Get VCP 0x01 for code 0x10, checksum over 0x6E and those bytes.
        Assert.Equal([0x51, 0x82, 0x01, 0x10, 0xAC], monitor.Received.Single());
    }

    [Fact]
    public void Get_VCP_reads_current_and_maximum()
    {
        var monitor = new FakeMonitor { Vcp = { [VcpCode.Brightness] = (300, 1000) } };

        var reply = Channel(monitor).GetVcp(VcpCode.Brightness);

        Assert.Equal(new VcpReply(300, 1000), reply);
    }

    [Fact]
    public void Set_VCP_sends_the_value_high_byte_first()
    {
        var monitor = new FakeMonitor();

        Channel(monitor).SetVcp(VcpCode.Brightness, 50);

        Assert.Equal([0x51, 0x84, 0x03, 0x10, 0x00, 0x32, 0x9A], monitor.Received.Single());
    }

    [Fact]
    public void A_reply_with_a_bad_checksum_is_an_error()
    {
        var monitor = new FakeMonitor { Vcp = { [VcpCode.Brightness] = (40, 100) } };
        monitor.Corrupt = reply => { reply[^1] ^= 0xFF; return reply; };

        Assert.Throws<IOException>(() => Channel(monitor).GetVcp(VcpCode.Brightness));
    }

    [Fact]
    public void A_null_message_is_an_error()
    {
        var monitor = new FakeMonitor { Vcp = { [VcpCode.Brightness] = (40, 100) } };
        monitor.Corrupt = _ => [0x6E, 0x80, 0xBE];

        var error = Assert.Throws<IOException>(() => Channel(monitor).GetVcp(VcpCode.Brightness));
        Assert.Contains("null message", error.Message);
    }

    [Fact]
    public void An_unsupported_code_is_an_error()
    {
        var monitor = new FakeMonitor();

        var error = Assert.Throws<IOException>(() => Channel(monitor).GetVcp(VcpCode.AudioVolume));
        Assert.Contains("doesn't support", error.Message);
    }

    [Fact]
    public void A_reply_for_another_code_is_an_error()
    {
        var monitor = new FakeMonitor { Vcp = { [VcpCode.Brightness] = (40, 100) } };
        monitor.Corrupt = _ => FakeMonitor.Frame([0x02, 0x00, VcpCode.Contrast, 0x00, 0, 100, 0, 50]);

        Assert.Throws<IOException>(() => Channel(monitor).GetVcp(VcpCode.Brightness));
    }

    [Fact]
    public void Capabilities_are_put_together_from_fragments()
    {
        const string capabilities = "(prot(monitor)type(LCD)model(P2422HE)cmds(01 02 03 07 0C E3 F3)vcp(02 04 05 08 10 12 14(05 08 0B 0C) 60(0F 11 1B))mccs_ver(2.1))";
        var monitor = new FakeMonitor { Capabilities = capabilities };

        string result = Channel(monitor).GetCapabilities();

        Assert.Equal(capabilities, result);
        // One request per 32-byte fragment plus the empty one that ends it, each at the right offset.
        var offsets = monitor.Received.Select(m => m[3] << 8 | m[4]).ToList();
        Assert.Equal(Enumerable.Range(0, (capabilities.Length + 31) / 32 + 1).Select(i => Math.Min(i * 32, capabilities.Length)), offsets);
    }

    [Fact]
    public void A_bad_capabilities_fragment_is_asked_for_again()
    {
        var monitor = new FakeMonitor { Capabilities = "(prot(monitor)vcp(10 12))" };
        monitor.Corrupt = reply => { reply[5] ^= 0x20; return reply; };

        Assert.Equal("(prot(monitor)vcp(10 12))", Channel(monitor).GetCapabilities());
    }

    [Fact]
    public void Capabilities_end_at_a_NUL()
    {
        var monitor = new FakeMonitor { Capabilities = "(vcp(10))\0" };

        Assert.Equal("(vcp(10))", Channel(monitor).GetCapabilities());
        Assert.Single(monitor.Received);
    }

    [Fact]
    public void Disposing_the_channel_closes_the_bus()
    {
        var monitor = new FakeMonitor();

        Channel(monitor).Dispose();

        Assert.True(monitor.Disposed);
    }
}
