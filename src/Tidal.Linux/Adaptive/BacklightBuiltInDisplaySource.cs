using Tidal.Core.Adaptive;
using Tidal.Linux.Displays;

namespace Tidal.Linux.Adaptive;

/// <summary>
/// Follows the built-in screen's backlight, so it sees every change: brightness keys, the desktop's
/// slider, or Tidal. Not every change tells anyone, so it reads the level every second; the read is cheap.
/// </summary>
public sealed class BacklightBuiltInDisplaySource : BuiltInDisplaySource
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly Backlight? _backlight;
    private Timer? _poll;

    public BacklightBuiltInDisplaySource() : this(new Sysfs())
    {
    }

    internal BacklightBuiltInDisplaySource(ISysfs sysfs) => _backlight = FindBacklight(sysfs);

    /// <summary>True when this PC has a built-in screen, turned on, with a backlight Linux reports.</summary>
    public static bool IsAvailable => FindBacklight(new Sysfs()) is not null;

    private static Backlight? FindBacklight(ISysfs sysfs)
        => DrmConnector.ReadAll(sysfs).FirstOrDefault(c => c.IsInternal && c.IsActive) is { } screen
            ? Backlight.Find(sysfs, new Logind(), screen.Name) // only read here, never written
            : null;

    public override void Start()
    {
        if (_backlight is null || _poll is not null) return;
        _poll = new Timer(_ => Poll(), null, TimeSpan.Zero, PollInterval);
    }

    private void Poll()
    {
        if (_backlight?.ReadPercent() is int percent) ReportPercent(percent);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _poll?.Dispose();
    }
}
