using System.Globalization;
using System.Windows.Data;

namespace Tidal.App.Controls;

/// <summary>
/// Width of a pill slider's filled part: from the left edge to the thumb's right edge, so the fill
/// always ends in the thumb's round cap. Values: Value, Minimum, Maximum, track width; parameter: thumb size.
/// </summary>
public sealed class SliderFillConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 4 || values[0] is not double value || values[1] is not double min
            || values[2] is not double max || values[3] is not double width) return 0.0;

        double thumb = parameter is string text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var t) ? t : 30;
        double fraction = max > min ? Math.Clamp((value - min) / (max - min), 0, 1) : 0;
        return thumb + Math.Max(0, width - thumb) * fraction;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
