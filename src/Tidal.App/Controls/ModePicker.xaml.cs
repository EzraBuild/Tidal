using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Tidal.App.Controls;

public partial class ModePicker : UserControl
{
    public ModePicker() => InitializeComponent();

    private void OnClick(object sender, RoutedEventArgs e)
    {
        var menu = PickerButton.ContextMenu;
        menu.DataContext = DataContext;
        menu.PlacementTarget = PickerButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }
}
