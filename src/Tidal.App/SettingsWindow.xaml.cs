using Tidal.App.Tray;
using Tidal.App.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Tidal.App;

/// <summary>
/// Settings: every display (brightness, contrast, limits, why control may be unavailable) and the
/// brightness shortcuts.
/// </summary>
public partial class SettingsWindow : FluentWindow
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Icon = Logo.Render(64, Logo.BrandColor);
        SystemThemeWatcher.Watch(this);
    }
}
