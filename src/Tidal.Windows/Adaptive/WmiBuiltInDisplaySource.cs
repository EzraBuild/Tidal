using System.Management;
using System.Runtime.InteropServices;
using Tidal.Core;
using Tidal.Core.Adaptive;
using Tidal.Windows.Displays;

namespace Tidal.Windows.Adaptive;

/// <summary>
/// Reads the built-in screen's brightness through WMI, so it sees every change: Fn keys, the Windows
/// slider, Windows' own automatic brightness, or Tidal.
/// </summary>
public sealed class WmiBuiltInDisplaySource : BuiltInDisplaySource
{
    // Some drivers never raise the brightness event, so also poll now and then; the read is cheap.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly WmiBrightness? _wmi;
    private ManagementEventWatcher? _watcher;
    private Timer? _poll;

    public WmiBuiltInDisplaySource()
    {
        var instance = WmiBrightness.FindInstances().FirstOrDefault();
        if (instance is not null) _wmi = new WmiBrightness(instance);
    }

    /// <summary>True when this PC has a built-in screen whose brightness Windows reports.</summary>
    public static bool IsAvailable => WmiBrightness.FindInstances().Count > 0;

    public override void Start()
    {
        if (_wmi is null || _poll is not null) return;
        try
        {
            _watcher = new ManagementEventWatcher(
                new ManagementScope(@"root\WMI"), new WqlEventQuery("SELECT * FROM WmiMonitorBrightnessEvent"));
            _watcher.EventArrived += OnBrightnessEvent;
            _watcher.Start();
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
            Log.Warning($"Brightness change events unavailable, polling only: {ex.Message}");
            _watcher?.Dispose();
            _watcher = null;
        }
        _poll = new Timer(_ => Poll(), null, TimeSpan.Zero, PollInterval);
    }

    private void OnBrightnessEvent(object sender, EventArrivedEventArgs e)
    {
        using var reading = e.NewEvent;
        if (reading["Brightness"] is not { } brightness) return;
        Log.Debug($"Built-in brightness event: {brightness}%");
        ReportPercent(Convert.ToInt32(brightness));
    }

    private void Poll()
    {
        if (_wmi?.TryRead() is int percent) ReportPercent(percent);
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        _poll?.Dispose();
        if (_watcher is not null)
        {
            _watcher.EventArrived -= OnBrightnessEvent;
            try { _watcher.Stop(); } catch (ManagementException) { }
            _watcher.Dispose();
        }
        _wmi?.Dispose();
    }
}
