using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Threading;
using Tidal.App.Interop;
using Tidal.ViewModels;
using static Tidal.App.Interop.NativeMethods;

namespace Tidal.App.Services;

/// <summary>
/// Shows each display's software dimming: one <see cref="DimOverlay"/> per display, following its
/// <see cref="DisplayViewModel.DimOpacity"/>, and kept above other always-on-top windows (Windows
/// raises the taskbar over it when the taskbar is clicked). Tidal's own popup and on-screen level
/// stay above the overlays, so they remain readable on a dimmed display.
/// </summary>
internal sealed class DimmingService : IDisposable
{
    // A backstop for windows that rise above the overlay without becoming the foreground window.
    private static readonly TimeSpan RaiseInterval = TimeSpan.FromSeconds(2);

    private readonly DisplayListViewModel _displays;
    private readonly Func<IEnumerable<IntPtr>> _windowsToKeepAbove;
    private readonly Dictionary<string, DimOverlay> _overlays = [];
    private readonly HashSet<DisplayViewModel> _tracked = [];
    private readonly DispatcherTimer _raiseTimer;
    private readonly WinEventProc _onForegroundChanged; // kept in a field so the GC can't collect it while hooked
    private IntPtr _foregroundHook;

    public DimmingService(DisplayListViewModel displays, Func<IEnumerable<IntPtr>> windowsToKeepAbove)
    {
        _displays = displays;
        _windowsToKeepAbove = windowsToKeepAbove;
        _onForegroundChanged = (_, _, _, _, _, _, _) => RaiseOverlays();
        _raiseTimer = new DispatcherTimer { Interval = RaiseInterval };
        _raiseTimer.Tick += (_, _) => RaiseOverlays();

        _displays.Displays.CollectionChanged += OnDisplaysChanged;
        _displays.PropertyChanged += OnListPropertyChanged;
        Sync();
    }

    private void OnDisplaysChanged(object? sender, NotifyCollectionChangedEventArgs e) => Sync();

    // Detection replaces every display's view model. Overlays are kept (and re-matched by display
    // ID) until it finishes, so a dimmed screen doesn't flash bright when another one is plugged in.
    private void OnListPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DisplayListViewModel.IsBusy) && !_displays.IsBusy) Sync();
    }

    private void OnDisplayPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DisplayViewModel.DimOpacity) && sender is DisplayViewModel display) Update(display);
    }

    private void Sync()
    {
        var current = _displays.Displays.ToHashSet();
        foreach (var gone in _tracked.Except(current).ToList())
        {
            gone.PropertyChanged -= OnDisplayPropertyChanged;
            _tracked.Remove(gone);
        }
        foreach (var added in current.Except(_tracked).ToList())
        {
            added.PropertyChanged += OnDisplayPropertyChanged;
            _tracked.Add(added);
        }

        if (!_displays.IsBusy)
        {
            var ids = current.Select(d => d.Id).ToHashSet();
            foreach (var id in _overlays.Keys.Where(id => !ids.Contains(id)).ToList())
            {
                _overlays[id].Dispose();
                _overlays.Remove(id);
            }
        }
        foreach (var display in current) Update(display);
    }

    private void Update(DisplayViewModel display)
    {
        bool dimmed = display.DimOpacity > 0;
        if (!_overlays.TryGetValue(display.Id, out var overlay))
        {
            if (!dimmed) return;
            try
            {
                _overlays[display.Id] = overlay = new DimOverlay();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Tidal] {display.Name}: software dimming unavailable: {ex.Message}");
                return;
            }
        }

        bool wasShown = overlay.IsShown;
        overlay.Update(display.Bounds, display.DimOpacity);
        if (overlay.IsShown && !wasShown) RaiseOwnWindows(); // a newly shown overlay lands on top of everything
        UpdateKeepOnTop();
    }

    private void RaiseOverlays()
    {
        foreach (var overlay in _overlays.Values) overlay.BringToFront();
        RaiseOwnWindows();
    }

    private void RaiseOwnWindows()
    {
        foreach (var handle in _windowsToKeepAbove())
            SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    // Watching for foreground changes costs nothing worth mentioning, but only while something is dimmed.
    private void UpdateKeepOnTop()
    {
        bool anyShown = _overlays.Values.Any(o => o.IsShown);
        if (anyShown && _foregroundHook == IntPtr.Zero)
        {
            _foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero,
                _onForegroundChanged, 0, 0, WINEVENT_OUTOFCONTEXT);
            _raiseTimer.Start();
        }
        else if (!anyShown && _foregroundHook != IntPtr.Zero)
        {
            UnhookWinEvent(_foregroundHook);
            _foregroundHook = IntPtr.Zero;
            _raiseTimer.Stop();
        }
    }

    public void Dispose()
    {
        _displays.Displays.CollectionChanged -= OnDisplaysChanged;
        _displays.PropertyChanged -= OnListPropertyChanged;
        foreach (var display in _tracked) display.PropertyChanged -= OnDisplayPropertyChanged;
        _tracked.Clear();
        _raiseTimer.Stop();
        if (_foregroundHook != IntPtr.Zero) UnhookWinEvent(_foregroundHook);
        _foregroundHook = IntPtr.Zero;
        foreach (var overlay in _overlays.Values) overlay.Dispose();
        _overlays.Clear();
    }
}
