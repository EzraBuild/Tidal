using System.Globalization;
using System.Windows.Data;

namespace Tidal.App.Controls;

/// <summary>
/// Width of a slider's below-zero zone: from the left edge to where the thumb's centre sits at
/// value 0, or nothing when the slider doesn't go below zero. Values: Minimum, Maximum, track
/// width; parameter: thumb size.
/// </summary>
public sealed class SliderZeroConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 3 || values[0] is not double min || values[1] is not double max
            || values[2] is not double width || min >= 0 || max <= min) return 0.0;

        double thumb = parameter is string text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var t) ? t : 30;
        double fraction = Math.Clamp(-min / (max - min), 0, 1);
        return thumb / 2 + Math.Max(0, width - thumb) * fraction;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
