using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using Tidal.App.Interop;
using Tidal.App.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using static Tidal.App.Interop.NativeMethods;
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;
using MenuItem = System.Windows.Controls.MenuItem;

namespace Tidal.App;

/// <summary>
/// Settings: a tab per display (brightness limits, contrast, adaptive brightness, dimming below
/// zero), plus adaptive brightness, app presets and the brightness shortcuts.
/// </summary>
public partial class SettingsWindow : FluentWindow
{
    private const double ScreenMargin = 24; // DIPs left free around the window on a small screen

    public SettingsWindow(AppViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SystemThemeWatcher.Watch(this, WindowBackdropType.None, updateAccents: false); // TidalTheme owns the accent
        FitScreenUnderPointer();
    }

    // The window opens centred on the screen under the pointer; on a small or highly scaled screen
    // (e.g. 1366x768, or 1080p at 150%) its full size wouldn't fit, so shrink it to the work area.
    private void FitScreenUnderPointer()
    {
        if (!GetCursorPos(out var pointer)) return;
        var monitor = MonitorFromPoint(pointer, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info)) return;

        double scale = GetMonitorDpi(monitor) / 96.0;
        double width = (info.rcWork.Right - info.rcWork.Left) / scale - ScreenMargin;
        double height = (info.rcWork.Bottom - info.rcWork.Top) / scale - ScreenMargin;
        Width = Math.Min(Width, width);
        Height = Math.Min(Height, height);
        MinWidth = Math.Min(MinWidth, Width);
        MinHeight = Math.Min(MinHeight, Height);
    }

    // "Add app": a menu of the apps that are open, plus a file picker for the rest.
    private async void OnAddAppClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: AppPresetsViewModel presets } button) return;

        IReadOnlyList<RunningApp> apps;
        button.IsEnabled = false;
        try
        {
            apps = await Task.Run(presets.FindRunningApps);
        }
        finally
        {
            button.IsEnabled = true;
        }

        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
        foreach (var app in apps)
        {
            var item = new MenuItem { Header = app.Name, ToolTip = app.Path };
            if (app.Icon is not null) item.Icon = new Image { Source = app.Icon, Width = 16, Height = 16 };
            item.Click += (_, _) => presets.Add(app.Path);
            menu.Items.Add(item);
        }
        if (apps.Count == 0) menu.Items.Add(new MenuItem { Header = "No other apps are open", IsEnabled = false });
        menu.Items.Add(new Separator());

        var browse = new MenuItem { Header = "Browse for a program…", Icon = new SymbolIcon { Symbol = SymbolRegular.FolderOpen20 } };
        browse.Click += (_, _) =>
        {
            var dialog = new OpenFileDialog { Title = "Choose an app", Filter = "Programs (*.exe)|*.exe" };
            if (dialog.ShowDialog(this) == true) presets.Add(dialog.FileName);
        };
        menu.Items.Add(browse);
        menu.IsOpen = true;
    }
}
