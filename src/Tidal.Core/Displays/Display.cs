namespace Tidal.Core.Displays;

/// <summary>How Tidal talks to a display's hardware.</summary>
public enum ControlMethod
{
    /// <summary>No hardware control; only software dimming is possible.</summary>
    None,
    /// <summary>DDC/CI over the video cable (external monitors).</summary>
    DdcCi,
    /// <summary>The WMI brightness interface (built-in laptop screens on Windows).</summary>
    Wmi,
}

public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public override string ToString() => $"{Width}x{Height} at ({Left},{Top})";
}

/// <summary>Identity and layout of a display, as reported by the operating system.</summary>
public sealed record DisplayInfo
{
    /// <summary>Stable identifier for per-display settings (the monitor's device path when available).</summary>
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>The operating system's name for the display output, e.g. <c>\\.\DISPLAY1</c> on Windows.</summary>
    public required string DeviceName { get; init; }
    /// <summary>Desktop coordinates in physical pixels.</summary>
    public required ScreenRect Bounds { get; init; }
    public bool IsPrimary { get; init; }
    public bool IsInternal { get; init; }
    /// <summary>How the display is connected, e.g. "HDMI", "DisplayPort", "Built-in".</summary>
    public string Connection { get; init; } = "Unknown";
    /// <summary>Manufacturer and product code from the monitor's EDID, e.g. <c>DELA1C8</c>.</summary>
    public string? HardwareId { get; init; }
    /// <summary>Windows' PnP instance path, e.g. <c>DISPLAY\DELA1C8\5&amp;35c26193&amp;0&amp;UID4352</c>.</summary>
    public string? InstancePath { get; init; }
}

/// <summary>A connected display and whatever hardware controls Tidal could reach on it.</summary>
public abstract class Display : IDisposable
{
    private const int Unknown = -1;
    private int _brightness = Unknown;
    private int _contrast = Unknown;
    private int _input = Unknown;
    private int _volume = Unknown;

    protected Display(DisplayInfo info) => Info = info;

    public DisplayInfo Info { get; }
    public string Id => Info.Id;
    public string Name => Info.Name;

    public abstract ControlMethod Method { get; }
    public bool SupportsBrightness { get; protected set; }
    public bool SupportsContrast { get; protected set; }

    /// <summary>Why hardware control is limited or unavailable, worded for the user. Null when everything works.</summary>
    public string? Note { get; protected set; }

    /// <summary>Last known brightness in percent (0–100), or null if unknown or unsupported.</summary>
    public int? Brightness => FromField(Volatile.Read(ref _brightness));

    /// <summary>Last known contrast in percent (0–100), or null if unknown or unsupported.</summary>
    public int? Contrast => FromField(Volatile.Read(ref _contrast));

    /// <summary>Inputs the monitor says it has (VCP 0x60). Empty when it can't switch inputs or hasn't said yet.</summary>
    public IReadOnlyList<InputSource> InputSources { get; protected set; } = [];

    /// <summary>The input the monitor is showing, when known.</summary>
    public InputSource? CurrentInput => Volatile.Read(ref _input) is int code and >= 0 ? new InputSource((byte)code) : null;

    /// <summary>True when the monitor has speakers whose volume Tidal can set (VCP 0x62).</summary>
    public bool SupportsVolume { get; protected set; }

    /// <summary>Last known volume in percent (0–100), or null if unknown or unsupported.</summary>
    public int? Volume => FromField(Volatile.Read(ref _volume));

    /// <summary>Raised on a background thread after values have been read from the hardware.</summary>
    public event EventHandler? ValuesRead;

    /// <summary>Probes what the display supports and reads its current values. Called by <see cref="DisplayManager"/>.</summary>
    protected internal abstract Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Re-reads current values from the hardware, e.g. after they were changed with the monitor's
    /// own buttons. If nothing was controllable before, probes again (the monitor may have been asleep).
    /// Returns false if the hardware didn't answer, in which case the last known values are kept.
    /// </summary>
    public abstract Task<bool> RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>Sets brightness in percent. Returns at once; the write is queued, throttled and coalesced.</summary>
    public void SetBrightness(int percent)
    {
        if (!SupportsBrightness) throw new NotSupportedException($"{Name} has no hardware brightness control.");
        percent = Math.Clamp(percent, 0, 100);
        Volatile.Write(ref _brightness, percent);
        WriteBrightness(percent);
    }

    /// <summary>Sets contrast in percent. Returns at once; the write is queued, throttled and coalesced.</summary>
    public void SetContrast(int percent)
    {
        if (!SupportsContrast) throw new NotSupportedException($"{Name} has no hardware contrast control.");
        percent = Math.Clamp(percent, 0, 100);
        Volatile.Write(ref _contrast, percent);
        WriteContrast(percent);
    }

    /// <summary>
    /// Switches the monitor to another input. If that input isn't this PC, the monitor stops
    /// showing this PC and Windows will drop it until it's switched back (with the monitor's buttons).
    /// </summary>
    public void SetInput(InputSource input)
    {
        if (!InputSources.Contains(input)) throw new ArgumentException($"{Name} has no input {input}.", nameof(input));
        Volatile.Write(ref _input, input.Code);
        WriteInput(input.Code);
    }

    /// <summary>Sets the speaker volume in percent. Returns at once; the write is queued, throttled and coalesced.</summary>
    public void SetVolume(int percent)
    {
        if (!SupportsVolume) throw new NotSupportedException($"{Name} has no volume control.");
        percent = Math.Clamp(percent, 0, 100);
        Volatile.Write(ref _volume, percent);
        WriteVolume(percent);
    }

    protected abstract void WriteBrightness(int percent);

    protected virtual void WriteContrast(int percent) => throw new NotSupportedException();

    protected virtual void WriteInput(byte code) => throw new NotSupportedException();

    protected virtual void WriteVolume(int percent) => throw new NotSupportedException();

    /// <summary>Stores freshly read values (null leaves a value unchanged) and raises <see cref="ValuesRead"/>.</summary>
    protected void OnValuesRead(int? brightness, int? contrast, InputSource? input = null, int? volume = null)
    {
        if (brightness is int b) Volatile.Write(ref _brightness, Math.Clamp(b, 0, 100));
        if (contrast is int c) Volatile.Write(ref _contrast, Math.Clamp(c, 0, 100));
        if (input is { } i) Volatile.Write(ref _input, i.Code);
        if (volume is int v) Volatile.Write(ref _volume, Math.Clamp(v, 0, 100));
        ValuesRead?.Invoke(this, EventArgs.Empty);
    }

    public override string ToString() => Name;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }

    private static int? FromField(int value) => value == Unknown ? null : value;
}
