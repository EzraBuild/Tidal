using System.Collections.Concurrent;
using Tidal.Core.Displays;

namespace Tidal.Core.Tests;

public class DdcDisplayTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static readonly DisplayInfo Info = new()
    {
        Id = "test",
        Name = "Test Monitor",
        DeviceName = "test",
        Bounds = new ScreenRect(0, 0, 1920, 1080),
    };

    [Fact]
    public async Task Reads_brightness_and_contrast_as_percentages_of_the_monitor_maximum()
    {
        var channel = new FakeChannel { [VcpCode.Brightness] = new(40, 80), [VcpCode.Contrast] = new(75, 100) };
        using var display = new DdcDisplay(Info, channel, "no reply");

        await display.InitializeAsync(CancellationToken.None).WaitAsync(Timeout);

        Assert.Equal(ControlMethod.DdcCi, display.Method);
        Assert.Equal(50, display.Brightness);
        Assert.Equal(75, display.Contrast);
        Assert.Null(display.Note);
    }

    [Fact]
    public async Task A_monitor_that_never_answers_gets_the_note_and_no_control()
    {
        var channel = new FakeChannel();
        using var display = new DdcDisplay(Info, channel, "Turn on DDC/CI.");

        await display.InitializeAsync(CancellationToken.None).WaitAsync(Timeout);

        Assert.Equal(ControlMethod.None, display.Method);
        Assert.False(display.SupportsBrightness);
        Assert.Equal("Turn on DDC/CI.", display.Note);
        Assert.DoesNotContain(VcpCode.Contrast, channel.Reads); // no point asking once brightness failed
    }

    [Fact]
    public async Task Failed_reads_are_retried()
    {
        var channel = new FakeChannel { [VcpCode.Brightness] = new(30, 100), [VcpCode.Contrast] = new(50, 100) };
        channel.FailuresLeft = 2;
        using var display = new DdcDisplay(Info, channel, "no reply");

        await display.InitializeAsync(CancellationToken.None).WaitAsync(Timeout);

        Assert.Equal(30, display.Brightness);
    }

    [Fact]
    public async Task Writes_are_scaled_to_the_monitor_maximum()
    {
        var channel = new FakeChannel { [VcpCode.Brightness] = new(0, 50), [VcpCode.Contrast] = new(0, 100) };
        using var display = new DdcDisplay(Info, channel, "no reply");
        await display.InitializeAsync(CancellationToken.None).WaitAsync(Timeout);

        display.SetBrightness(60);
        await display.ReadVcpAsync(VcpCode.Brightness).WaitAsync(Timeout); // queued behind the write

        Assert.Contains((VcpCode.Brightness, 30u), channel.Writes);
    }

    [Fact]
    public async Task Inputs_come_from_the_capabilities_string()
    {
        var channel = new FakeChannel
        {
            [VcpCode.Brightness] = new(50, 100),
            [VcpCode.Contrast] = new(50, 100),
            [VcpCode.InputSource] = new(0x0F, 0x12),
            Capabilities = "(prot(monitor)type(LCD)model(Test)vcp(10 12 60(0F 11 12)))",
        };
        using var display = new DdcDisplay(Info, channel, "no reply");
        var loaded = new TaskCompletionSource();
        display.ValuesRead += (_, _) => { if (display.InputSources.Count > 0) loaded.TrySetResult(); };

        await display.InitializeAsync(CancellationToken.None).WaitAsync(Timeout);
        await loaded.Task.WaitAsync(Timeout);

        Assert.Equal([new InputSource(0x0F), new InputSource(0x11), new InputSource(0x12)], display.InputSources);
        Assert.Equal(new InputSource(0x0F), display.CurrentInput);
        Assert.False(display.SupportsVolume);
    }

    /// <summary>A monitor whose VCP values are set up front. Codes without a value don't answer.</summary>
    private sealed class FakeChannel : IDdcChannel
    {
        private readonly ConcurrentDictionary<byte, VcpReply> _values = new();

        public VcpReply this[byte code] { set => _values[code] = value; }

        public string? Capabilities { get; init; }

        /// <summary>How many of the next commands fail before the monitor starts answering.</summary>
        public int FailuresLeft { get; set; }

        public ConcurrentQueue<byte> Reads { get; } = new();

        public ConcurrentQueue<(byte, uint)> Writes { get; } = new();

        public VcpReply GetVcp(byte code)
        {
            Reads.Enqueue(code);
            FailIfAsked();
            return _values.TryGetValue(code, out var reply) ? reply : throw new IOException("no reply");
        }

        public void SetVcp(byte code, uint value)
        {
            FailIfAsked();
            Writes.Enqueue((code, value));
        }

        public string GetCapabilities()
        {
            FailIfAsked();
            return Capabilities ?? throw new IOException("no reply");
        }

        private void FailIfAsked()
        {
            if (FailuresLeft <= 0) return;
            FailuresLeft--;
            throw new IOException("simulated I2C error");
        }

        public void Dispose()
        {
        }
    }
}
