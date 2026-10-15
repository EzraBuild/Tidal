using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Tidal.App.Tray;
using Tidal.App.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;

namespace Tidal.App;

/// <summary>
/// Tidal lives in the notification area: no window at startup, a popup from the tray icon, and a
/// menu on right-click. Runs until "Quit Tidal".
/// </summary>
public partial class App : Application
{
    private SingleInstance? _singleInstance;
    private DisplayListViewModel? _displays;
    private TrayIcon? _trayIcon;
    private TrayPopup? _popup;
    private DisplaysWindow? _displaysWindow;
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

        ApplicationThemeManager.ApplySystemTheme();

        _displays = new DisplayListViewModel();
        _displays.PropertyChanged += OnDisplaysPropertyChanged;

        _popup = new TrayPopup(_displays);
        _popup.DisplaysWindowRequested += (_, _) => OpenDisplaysWindow();

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
        _trayIcon?.Dispose();
        _popup?.AllowClose();
        _displays?.Dispose(); // flushes pending brightness writes
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void TogglePopup()
    {
        if (_popup is null) return;
        if (_popup.IsOpen) _popup.HidePopup();
        else if (!_popup.WasJustHidden) ShowPopup();
    }

    private void ShowPopup() => _popup?.ShowNear(_trayIcon?.GetBounds());

    private void OpenDisplaysWindow()
    {
        _popup?.HidePopup();
        if (_displaysWindow is null)
        {
            _displaysWindow = new DisplaysWindow(_displays!);
            _displaysWindow.Closed += (_, _) => _displaysWindow = null;
        }
        _displaysWindow.Show();
        if (_displaysWindow.WindowState == WindowState.Minimized) _displaysWindow.WindowState = WindowState.Normal;
        _displaysWindow.Activate();
    }

    private void ShowTrayMenu()
    {
        _popup?.HidePopup();
        var menu = new ContextMenu { Placement = PlacementMode.MousePoint };
        menu.Items.Add(CreateMenuItem("Brightness", SymbolRegular.BrightnessHigh20, ShowPopup));
        menu.Items.Add(CreateMenuItem("Displays…", SymbolRegular.Desktop20, OpenDisplaysWindow));
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
