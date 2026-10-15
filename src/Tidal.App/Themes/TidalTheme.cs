using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace Tidal.App.Themes;

/// <summary>
/// Tidal's own colours, layered on top of WPF-UI's Fluent theme: warm sun amber for brightness,
/// ocean blue for the brand and selections. Kept in step with the light/dark app theme.
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

        // Brightness: the sun.
        Set(resources, "TidalSunBrush", dark ? "#F6B847" : "#E89B12");
        Set(resources, "TidalSunSoftBrush", dark ? "#24F6B847" : "#1FE89B12");
        Set(resources, "TidalOnSunBrush", "#4A3200");
        // Contrast.
        Set(resources, "TidalContrastBrush", dark ? "#9DAAFF" : "#5B6CE0");
        Set(resources, "TidalContrastSoftBrush", dark ? "#249DAAFF" : "#1A5B6CE0");
        // Volume.
        Set(resources, "TidalVolumeBrush", dark ? "#43CDB8" : "#11998A");
        Set(resources, "TidalVolumeSoftBrush", dark ? "#2443CDB8" : "#1A11998A");
        // Brand: the sea.
        Set(resources, "TidalOceanBrush", dark ? "#3AA6F0" : "#1A84D2");
        Set(resources, "TidalOceanSoftBrush", dark ? "#263AA6F0" : "#1A1A84D2");
        Set(resources, "TidalOnOceanBrush", "#FFFFFF");

        // Surfaces.
        Set(resources, "TidalTrackBrush", dark ? "#1FFFFFFF" : "#14000000");
        Set(resources, "TidalChipBrush", dark ? "#14FFFFFF" : "#0C000000");
        Set(resources, "TidalChipHoverBrush", dark ? "#26FFFFFF" : "#18000000");
        Set(resources, "TidalPanelBrush", dark ? "#0FFFFFFF" : "#08000000");
        Set(resources, "TidalDividerBrush", dark ? "#1AFFFFFF" : "#14000000");
        Set(resources, "TidalKnobBrush", "#FFFFFF");
        Set(resources, "TidalKnobTextBrush", "#3D2C00");

        // The monitor illustration: screens are dark in either theme.
        Set(resources, "TidalScreenBrush", dark ? "#16202E" : "#1E2938");
        Set(resources, "TidalBezelBrush", dark ? "#2E3B4F" : "#3A4759");
        Set(resources, "TidalStandBrush", dark ? "#3A4659" : "#C3CDD9");
        Set(resources, "TidalScreenTextBrush", "#F4F7FB");
        Set(resources, "TidalScreenMutedBrush", "#9AA9BC");

        // Decorative waves along the bottom of the Settings window.
        Set(resources, "TidalWaveBackBrush", dark ? "#143AA6F0" : "#141A84D2");
        Set(resources, "TidalWaveFrontBrush", dark ? "#223AA6F0" : "#221A84D2");
    }

    private static void Set(ResourceDictionary resources, string key, string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        resources[key] = brush;
    }
}
