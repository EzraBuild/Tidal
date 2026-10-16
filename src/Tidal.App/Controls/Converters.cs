using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Tidal.App.ViewModels;
using Wpf.Ui.Controls;

namespace Tidal.App.Controls;

/// <summary>Collapses the element when the value is true.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Title of a Settings tab: the display's name, "Adaptive", "Apps", "Shortcuts" or "General".</summary>
public sealed class PageTitleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        DisplayViewModel display => display.Name,
        AdaptiveViewModel => "Adaptive",
        AppPresetsViewModel => "Apps",
        ShortcutsViewModel => "Shortcuts",
        GeneralViewModel => "General",
        _ => "",
    };

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Icon of a Settings tab.</summary>
public sealed class PageIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        DisplayViewModel { IsInternal: true } => SymbolRegular.Laptop20,
        DisplayViewModel => SymbolRegular.Desktop20,
        AdaptiveViewModel => SymbolRegular.WeatherSunny20,
        AppPresetsViewModel => SymbolRegular.Apps20,
        ShortcutsViewModel => SymbolRegular.Keyboard20,
        GeneralViewModel => SymbolRegular.Settings20,
        _ => SymbolRegular.Empty,
    };

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
