using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace Tidal.App.Themes;

/// <summary>
/// Tidal's monochrome palette: black and white with greys, thin borders and square corners, in
/// light and dark. Also pins WPF-UI's accent to the foreground colour so its toggles and focus
/// rings stay monochrome instead of taking the Windows accent.
/// </summary>
internal static class TidalTheme
{
    public static void Initialize()
    {
        Apply(ApplicationThemeManager.GetAppTheme());
        ApplicationThemeManager.Changed += (theme, _) => Apply(theme);
    }

    private static void Apply(ApplicationTheme theme)
    {
        bool dark = theme != ApplicationTheme.Light;
        var resources = Application.Current.Resources;

        Set(resources, "TidalBackgroundBrush", dark ? "#000000" : "#FFFFFF");
        Set(resources, "TidalSurfaceBrush", dark ? "#0A0A0A" : "#FAFAFA");
        Set(resources, "TidalHoverBrush", dark ? "#171717" : "#F2F2F2");
        Set(resources, "TidalTrackBrush", dark ? "#121212" : "#F2F2F2");
        Set(resources, "TidalBorderBrush", dark ? "#262626" : "#E5E5E5");
        Set(resources, "TidalBorderStrongBrush", dark ? "#3F3F3F" : "#CFCFCF");
        Set(resources, "TidalTextBrush", dark ? "#EDEDED" : "#0A0A0A");
        Set(resources, "TidalMutedBrush", dark ? "#A1A1A1" : "#6B6B6B");
        Set(resources, "TidalFaintBrush", dark ? "#5C5C5C" : "#A3A3A3");
        // Solid, inverted surfaces: primary buttons, selected tabs, slider fills.
        Set(resources, "TidalInvertBrush", dark ? "#FAFAFA" : "#0A0A0A");
        Set(resources, "TidalOnInvertBrush", dark ? "#0A0A0A" : "#FAFAFA");
        // Secondary slider fills (contrast, volume), so they read apart from brightness.
        Set(resources, "TidalSecondaryFillBrush", dark ? "#8A8A8A" : "#8F8F8F");
        Set(resources, "TidalWarningBrush", dark ? "#F5A524" : "#B45309");
        resources["TidalGridBrush"] = CreateGridBrush(dark ? Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x0C, 0x00, 0x00, 0x00));
        // Diagonal hatching marks the below-zero (software dimming) part of a brightness slider.
        resources["TidalHatchBrush"] = CreateHatchBrush(dark ? Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x38, 0x00, 0x00, 0x00));

        var accent = (Color)ColorConverter.ConvertFromString(dark ? "#FAFAFA" : "#0A0A0A");
        ApplicationAccentColorManager.Apply(accent, accent, accent, accent);
    }

    /// <summary>A faint square grid, 40 px per cell, for window backgrounds.</summary>
    private static DrawingBrush CreateGridBrush(Color line)
    {
        var pen = new Pen(new SolidColorBrush(line), 1);
        var geometry = new GeometryGroup();
        geometry.Children.Add(new LineGeometry(new Point(0, 0), new Point(40, 0)));
        geometry.Children.Add(new LineGeometry(new Point(0, 0), new Point(0, 40)));
        var brush = new DrawingBrush(new GeometryDrawing(null, pen, geometry))
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 40, 40),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 40, 40),
            ViewboxUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
        };
        brush.Freeze();
        return brush;
    }

    /// <summary>Thin diagonal lines, 6 px apart.</summary>
    private static DrawingBrush CreateHatchBrush(Color line)
    {
        var pen = new Pen(new SolidColorBrush(line), 1);
        var brush = new DrawingBrush(new GeometryDrawing(null, pen, new LineGeometry(new Point(0, 6), new Point(6, 0))))
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 6, 6),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 6, 6),
            ViewboxUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
        };
        brush.Freeze();
        return brush;
    }

    private static void Set(ResourceDictionary resources, string key, string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        resources[key] = brush;
    }
}
