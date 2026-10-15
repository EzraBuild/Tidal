using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Tidal.App.ViewModels;

namespace Tidal.App.Controls;

public partial class InputPicker : UserControl
{
    public InputPicker() => InitializeComponent();

    private void OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DisplayViewModel display) return;

        var menu = new ContextMenu { PlacementTarget = PickerButton, Placement = PlacementMode.Bottom };
        menu.Items.Add(new MenuItem { Header = "Switch input", IsEnabled = false });
        foreach (var input in display.Inputs)
        {
            var item = new MenuItem
            {
                Header = input.Name,
                IsCheckable = true,
                IsChecked = input == display.CurrentInput,
            };
            item.Click += (_, _) => display.SwitchInput(input);
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }
}
