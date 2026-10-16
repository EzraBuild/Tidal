using Microsoft.Win32;
using Tidal.ViewModels.Platform;

namespace Tidal.App.Services;

/// <summary>Display changes and waking from sleep, from Windows' system events.</summary>
internal sealed class WindowsDisplayWatcher : IDisplayWatcher, IDisposable
{
    public WindowsDisplayWatcher()
    {
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public event EventHandler? DisplaysChanged;

    public event EventHandler? Resumed;

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => DisplaysChanged?.Invoke(this, EventArgs.Empty);

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) Resumed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }
}
