using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using Tidal.App.Osd;
using Tidal.App.Services;
using Tidal.App.Themes;
using Tidal.App.Tray;
using Tidal.App.ViewModels;
using Tidal.Core.Settings;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using static Tidal.App.Interop.NativeMethods;
using MenuItem = System.Windows.Controls.MenuItem;

namespace Tidal.App;

/// <summary>
/// Tidal lives in the notification area: no window at startup, a popup from the tray icon, a menu
/// on right-click and global brightness shortcuts. Runs until "Quit Tidal".
/// </summary>
public partial class App : Application
{
    private readonly Dictionary<string, BrightnessOsd> _osds = [];
    private SingleInstance? _singleInstance;
    private SettingsService? _settings;
    private DisplayListViewModel? _displays;
    private DimmingService? _dimming;
    private AdaptiveViewModel? _adaptive;
    private ShortcutsViewModel? _shortcuts;
    private AppViewModel? _app;
    private HotkeyManager? _hotkeys;
    private TrayIcon? _trayIcon;
    private TrayPopup? _popup;
    private SettingsWindow? _settingsWindow;
    private bool _toolTipUpdatePending;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = SingleInstance.TryAcquire();
        if (_singleInstance is null)
        {
            Shutdown(); // the running instance shows its popup instead
            return;
        }

        ApplicationThemeManager.ApplySystemTheme(updateAccent: false); // TidalTheme sets a monochrome accent
        TidalTheme.Initialize();

        SettingsStore.MoveLegacyFile(SettingsStore.LegacyPath, SettingsStore.DefaultPath);
        _settings = new SettingsService(new SettingsStore(SettingsStore.DefaultPath));
        _displays = new DisplayListViewModel(_settings);
        _displays.PropertyChanged += OnDisplaysPropertyChanged;
        _dimming = new DimmingService(_displays, OwnWindowsAboveDimming);
        _adaptive = new AdaptiveViewModel(_settings.Current.Adaptive, _displays, _settings.ScheduleSave);

        _hotkeys = new HotkeyManager();
        _hotkeys.Pressed += OnHotkeyPressed;
        _shortcuts = new ShortcutsViewModel(_settings.Current.Hotkeys, _hotkeys, _settings.ScheduleSave);
        _app = new AppViewModel(_displays, _adaptive, _shortcuts);

        _popup = new TrayPopup(_app);
        _popup.SettingsRequested += (_, _) => OpenSettings();
        _popup.QuitRequested += (_, _) => Shutdown();

        _trayIcon = new TrayIcon("Tidal");
        _trayIcon.Selected += (_, _) => TogglePopup();
        _trayIcon.ContextMenuRequested += (_, _) => ShowTrayMenu();

        _singleInstance.ListenForActivation(() => Dispatcher.InvokeAsync(ShowPopup));

        await _displays.RefreshAsync();

        // Launched by hand: show where Tidal went. "Start with Windows" will pass --background.
        if (!e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase)) ShowPopup();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _settings?.SaveNow();
        _hotkeys?.Dispose();
        _trayIcon?.Dispose();
        _popup?.AllowClose();
        _adaptive?.Dispose();
        _dimming?.Dispose();
        _displays?.Dispose(); // flushes pending brightness writes
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void OnHotkeyPressed(object? sender, int id)
    {
        if (_displays is null || _settings is null) return;
        var hotkeys = _settings.Current.Hotkeys;
        int delta = id == (int)HotkeyAction.BrightnessUp ? hotkeys.Step : -hotkeys.Step;

        DisplayViewModel? only = null;
        if (hotkeys.Target == HotkeyTarget.DisplayUnderPointer)
        {
            if (!GetCursorPos(out var pointer)) return;
            only = _displays.FindDisplayAt(pointer.X, pointer.Y);
            if (only is null) return;
        }

        foreach (var display in _displays.StepBrightness(delta, only)) ShowLevel(display);
    }

    private void ShowLevel(DisplayViewModel display)
    {
        if (!_osds.TryGetValue(display.Id, out var osd)) _osds[display.Id] = osd = new BrightnessOsd();
        osd.ShowLevel(display.Bounds, (int)Math.Round(display.Brightness), (int)display.SliderMinimum);
    }

    // Tidal's own always-on-top windows, which stay above the dimming overlays so they're readable.
    private IEnumerable<IntPtr> OwnWindowsAboveDimming()
    {
        if (_popup is { IsVisible: true }) yield return new WindowInteropHelper(_popup).Handle;
        foreach (var osd in _osds.Values)
        {
            if (osd.IsVisible) yield return new WindowInteropHelper(osd).Handle;
        }
    }

    private void TogglePopup()
    {
        if (_popup is null) return;
        if (_popup.IsOpen) _popup.HidePopup();
        else if (!_popup.WasJustHidden) ShowPopup();
    }

    private void ShowPopup() => _popup?.ShowNear(_trayIcon?.GetBounds());

    private void OpenSettings()
    {
        _popup?.HidePopup();
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(_app!);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        _settingsWindow.Show();
        if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
        _settingsWindow.Activate();
    }

    private void ShowTrayMenu()
    {
        _popup?.HidePopup();
        var menu = new ContextMenu { Placement = PlacementMode.MousePoint };
        menu.Items.Add(CreateMenuItem("Brightness", SymbolRegular.BrightnessHigh20, ShowPopup));
        menu.Items.Add(CreateMenuItem("Settings…", SymbolRegular.Settings20, OpenSettings));
        menu.Items.Add(CreateMenuItem("Detect displays again", SymbolRegular.ArrowClockwise20, () => _ = _displays?.RefreshAsync()));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Quit Tidal", SymbolRegular.Power20, () => Shutdown()));

        _trayIcon?.ActivateHiddenWindow();
        menu.IsOpen = true;
    }

    private static MenuItem CreateMenuItem(string header, SymbolRegular icon, Action onClick)
    {
        var item = new MenuItem { Header = header, Icon = new SymbolIcon { Symbol = icon } };
        item.Click += (_, _) => onClick();
        return item;
    }

    // Brightness changes many times a second while a slider moves; refresh the tooltip once per burst.
    private void OnDisplaysPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DisplayListViewModel.Summary) || _toolTipUpdatePending) return;
        _toolTipUpdatePending = true;
        Dispatcher.InvokeAsync(() =>
        {
            _toolTipUpdatePending = false;
            if (_trayIcon is not null && _displays is not null) _trayIcon.ToolTip = _displays.Summary;
        }, DispatcherPriority.Background);
    }
}
