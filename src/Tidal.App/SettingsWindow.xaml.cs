using Tidal.App.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Tidal.App;

/// <summary>
/// Settings: a tab per display (brightness limits, contrast, adaptive brightness), plus adaptive
/// brightness and the brightness shortcuts.
/// </summary>
public partial class SettingsWindow : FluentWindow
{
    public SettingsWindow(AppViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SystemThemeWatcher.Watch(this, WindowBackdropType.None, updateAccents: false); // TidalTheme owns the accent
    }
}
