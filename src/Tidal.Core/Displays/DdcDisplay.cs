using System.Runtime.InteropServices;
using System.Text;
using Tidal.Core.Native;

namespace Tidal.Core.Displays;

/// <summary>An external monitor controlled over DDC/CI.</summary>
public sealed class DdcDisplay : Display
{
    // The DDC/CI spec asks hosts to leave ~50 ms between commands; many monitors really need it.
    private static readonly TimeSpan CommandGap = TimeSpan.FromMilliseconds(50);
    // DDC/CI runs over I2C with no error correction, so occasional failed reads are normal.
    private const int ReadAttempts = 3;
    private const int WriteAttempts = 2;

    private readonly SafePhysicalMonitorHandle _handle;
    private readonly DeviceCommandQueue _queue;
    private uint _brightnessMax;
    private uint _contrastMax;
    private MonitorCapabilities? _capabilities;

    internal DdcDisplay(DisplayInfo info, SafePhysicalMonitorHandle handle) : base(info)
    {
        _handle = handle;
        _queue = new DeviceCommandQueue(info.Name, CommandGap);
    }

    public override ControlMethod Method => SupportsBrightness || SupportsContrast ? ControlMethod.DdcCi : ControlMethod.None;

    internal override async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var brightness = await ReadVcpAsync(VcpCode.Brightness).WaitAsync(cancellationToken);
        // If brightness didn't answer, DDC/CI is off or blocked; skip further slow, failing reads.
        var contrast = brightness is null ? null : await ReadVcpAsync(VcpCode.Contrast).WaitAsync(cancellationToken);

        SupportsBrightness = brightness is { Maximum: > 0 };
        SupportsContrast = contrast is { Maximum: > 0 };
        if (SupportsBrightness) _brightnessMax = brightness!.Value.Maximum;
        if (SupportsContrast) _contrastMax = contrast!.Value.Maximum;
        Note = SupportsBrightness ? null : DescribeNoDdc(Info);

        Log.Info($"{Name}: DDC/CI brightness={Format(brightness)} contrast={Format(contrast)}");
        OnValuesRead(ToPercent(brightness), ToPercent(contrast));
    }

    public override async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (Method == ControlMethod.None)
        {
            await InitializeAsync(cancellationToken);
            return Method != ControlMethod.None;
        }
        var brightness = SupportsBrightness ? await ReadVcpAsync(VcpCode.Brightness).WaitAsync(cancellationToken) : null;
        var contrast = SupportsContrast ? await ReadVcpAsync(VcpCode.Contrast).WaitAsync(cancellationToken) : null;
        OnValuesRead(ToPercent(brightness), ToPercent(contrast));
        return (!SupportsBrightness || brightness is not null) && (!SupportsContrast || contrast is not null);
    }

    /// <summary>Reads any VCP feature. Returns null if the monitor didn't answer.</summary>
    public Task<VcpReply?> ReadVcpAsync(byte code) => _queue.RunAsync(() => ReadVcpNow(code));

    /// <summary>Queues a write of any VCP feature. Queued writes to the same code are coalesced.</summary>
    public void WriteVcp(byte code, uint value) => _queue.Post(code, () => WriteVcpNow(code, value));

    /// <summary>
    /// Fetches and parses the monitor's capabilities string. This is slow (often 1–3 seconds), so
    /// it isn't part of detection; the result is cached once it succeeds.
    /// </summary>
    public async Task<MonitorCapabilities?> GetCapabilitiesAsync()
    {
        if (_capabilities is { } cached) return cached;
        string? raw = await _queue.RunAsync(ReadCapabilitiesNow);
        return _capabilities = raw is null ? null : MonitorCapabilities.Parse(raw);
    }

    private protected override void WriteBrightness(int percent) => WriteVcp(VcpCode.Brightness, ToRaw(percent, _brightnessMax));

    private protected override void WriteContrast(int percent) => WriteVcp(VcpCode.Contrast, ToRaw(percent, _contrastMax));

    private VcpReply? ReadVcpNow(byte code)
    {
        for (int attempt = 1; attempt <= ReadAttempts; attempt++)
        {
            if (Dxva2.GetVCPFeatureAndVCPFeatureReply(_handle, code, out _, out uint current, out uint maximum))
                return new VcpReply(current, maximum);

            Log.Debug($"{Name}: read VCP 0x{code:X2} failed (attempt {attempt}, error 0x{Marshal.GetLastPInvokeError():X8})");
            if (attempt < ReadAttempts) Thread.Sleep(CommandGap);
        }
        return null;
    }

    private void WriteVcpNow(byte code, uint value)
    {
        for (int attempt = 1; attempt <= WriteAttempts; attempt++)
        {
            if (Dxva2.SetVCPFeature(_handle, code, value)) return;

            Log.Debug($"{Name}: write VCP 0x{code:X2}={value} failed (attempt {attempt}, error 0x{Marshal.GetLastPInvokeError():X8})");
            if (attempt < WriteAttempts) Thread.Sleep(CommandGap);
        }
        Log.Warning($"{Name}: gave up writing VCP 0x{code:X2}={value}");
    }

    private string? ReadCapabilitiesNow()
    {
        for (int attempt = 1; attempt <= ReadAttempts; attempt++)
        {
            if (Dxva2.GetCapabilitiesStringLength(_handle, out uint length) && length > 0)
            {
                var buffer = new byte[length];
                if (Dxva2.CapabilitiesRequestAndCapabilitiesReply(_handle, buffer, length))
                    return Encoding.ASCII.GetString(buffer).TrimEnd('\0');
            }
            Log.Debug($"{Name}: capabilities request failed (attempt {attempt}, error 0x{Marshal.GetLastPInvokeError():X8})");
            if (attempt < ReadAttempts) Thread.Sleep(CommandGap);
        }
        return null;
    }

    private static int? ToPercent(VcpReply? reply) => reply is { Maximum: > 0 } r
        ? (int)Math.Round(Math.Min(r.Current, r.Maximum) * 100.0 / r.Maximum)
        : null;

    private static uint ToRaw(int percent, uint maximum) => (uint)Math.Round(percent * maximum / 100.0);

    private static string Format(VcpReply? reply) => reply is { } r ? $"{r.Current}/{r.Maximum}" : "no reply";

    private static string DescribeNoDdc(DisplayInfo info) => info.Connection switch
    {
        "Wireless" or "Virtual" or "USB display" =>
            $"{info.Connection} connections don't carry DDC/CI, so Tidal can't change this display's hardware settings.",
        _ when info.IsInternal =>
            "Windows doesn't expose brightness control for this built-in screen.",
        _ =>
            "This monitor didn't respond to DDC/CI. Turn on DDC/CI in the monitor's on-screen menu, " +
            "or connect it directly instead of through a dock, KVM switch or adapter.",
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _queue.Dispose(); // flushes pending writes before the handle goes away
            _handle.Dispose();
        }
    }
}
