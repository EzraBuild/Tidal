using System.IO;
using System.Windows.Threading;
using Tidal.App.Interop;
using Tidal.ViewModels;
using static Tidal.App.Interop.NativeMethods;

namespace Tidal.App.Services;

/// <summary>
/// Applies app presets: while an app with a preset is in front, the display showing it (every
/// display with Link all on) takes the preset's brightness, and goes back when another app comes
/// to the front. Watches foreground changes and windows being moved to another display.
/// </summary>
internal sealed class AppPresetService : IDisposable
{
    // Alt+Tab through several apps shouldn't send a brightness change for each one.
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(250);

    // Shell surfaces that come to the front without the user leaving the app they were in.
    private static readonly HashSet<string> IgnoredClasses = new(StringComparer.Ordinal)
    {
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland",
        "XamlExplorerHostIslandWindow", "MultitaskingViewFrame", "ForegroundStaging", "Windows.UI.Core.CoreWindow",
    };

    private static readonly HashSet<string> IgnoredPrograms = new(StringComparer.OrdinalIgnoreCase)
    {
        "StartMenuExperienceHost.exe", "ShellExperienceHost.exe", "SearchHost.exe", "SearchApp.exe", "LockApp.exe",
    };

    private readonly SettingsService _settings;
    private readonly DisplayListViewModel _displays;
    private readonly DispatcherTimer _settleTimer;
    private readonly WinEventProc _onWindowEvent; // kept in a field so the GC can't collect it while hooked
    private readonly List<IntPtr> _hooks = [];
    // Brightness each held display had before a preset took over, by display ID. Kept here rather
    // than in the display's view model so it survives the displays being detected again.
    private readonly Dictionary<string, int> _previous = [];

    public AppPresetService(SettingsService settings, DisplayListViewModel displays)
    {
        _settings = settings;
        _displays = displays;
        _settleTimer = new DispatcherTimer { Interval = SettleTime };
        _settleTimer.Tick += (_, _) =>
        {
            _settleTimer.Stop();
            Apply();
        };
        _onWindowEvent = (_, _, _, _, _, _, _) => ScheduleApply();

        _hooks.Add(SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _onWindowEvent, 0, 0, WINEVENT_OUTOFCONTEXT));
        _hooks.Add(SetWinEventHook(EVENT_SYSTEM_MOVESIZEEND, EVENT_SYSTEM_MOVESIZEEND, IntPtr.Zero, _onWindowEvent, 0, 0, WINEVENT_OUTOFCONTEXT));
        _displays.PropertyChanged += (_, e) =>
        {
            // Freshly detected displays: hold them again if the app in front has a preset.
            if (e.PropertyName == nameof(DisplayListViewModel.IsBusy) && !_displays.IsBusy) ScheduleApply();
        };
    }

    /// <summary>Re-checks the app in front soon, e.g. after a preset was added or changed.</summary>
    public void ScheduleApply()
    {
        _settleTimer.Stop();
        _settleTimer.Start();
    }

    private void Apply()
    {
        if (_displays.IsBusy) return;
        var app = ProgramInfo.GetForeground();
        if (app is null || IsIgnored(app)) return; // keep things as they are, e.g. while Tidal's own popup is open

        var preset = _settings.Current.FindAppPreset(app.ProgramPath);
        var held = new HashSet<DisplayViewModel>();
        if (preset is not null)
        {
            if (_displays.LinkAll) held.UnionWith(_displays.Displays);
            else if (_displays.FindDisplayAt(app.Center.X, app.Center.Y) is { } showing) held.Add(showing);
        }

        foreach (var display in _displays.Displays)
        {
            if (held.Contains(display))
            {
                _previous.TryAdd(display.Id, (int)Math.Round(display.Brightness));
                display.HoldForAppPreset(preset!.DisplayName, preset.Brightness / 100.0);
            }
            else if (_previous.Remove(display.Id, out int previous))
            {
                display.ReleaseAppPreset(previous);
            }
        }
    }

    private static bool IsIgnored(ForegroundApp app)
        => IgnoredClasses.Contains(app.ClassName)
           || IgnoredPrograms.Contains(Path.GetFileName(app.ProgramPath))
           || app.ProcessId == Environment.ProcessId;

    /// <summary>Stops watching and puts every held display back, e.g. when Tidal quits.</summary>
    public void Dispose()
    {
        _settleTimer.Stop();
        foreach (var hook in _hooks.Where(h => h != IntPtr.Zero)) UnhookWinEvent(hook);
        _hooks.Clear();
        foreach (var display in _displays.Displays)
        {
            if (_previous.Remove(display.Id, out int previous)) display.ReleaseAppPreset(previous);
        }
    }
}
