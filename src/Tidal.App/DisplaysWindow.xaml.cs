using Tidal.App.Tray;
using Tidal.App.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Tidal.App;

/// <summary>Detailed view of every display: brightness, contrast and why control may be unavailable.</summary>
public partial class DisplaysWindow : FluentWindow
{
    public DisplaysWindow(DisplayListViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Icon = Logo.Render(64, Logo.BrandColor);
        SystemThemeWatcher.Watch(this);
    }
}
