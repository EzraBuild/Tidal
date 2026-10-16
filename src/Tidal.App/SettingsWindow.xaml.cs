using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using Tidal.App.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
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
    public SettingsWindow(AppViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SystemThemeWatcher.Watch(this, WindowBackdropType.None, updateAccents: false); // TidalTheme owns the accent
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
