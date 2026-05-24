using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Verdict.Converters;

/// <summary>
/// Maps gender to a background brush color for avatar base.
/// Male: Blue tint, Female: Pink tint, Other: Neutral gray.
/// </summary>
public sealed class GenderToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string gender)
        {
            var g = gender.ToLowerInvariant();
            if (g == "male") return new SolidColorBrush(Color.FromRgb(102, 153, 204)); // Soft blue
            if (g == "female") return new SolidColorBrush(Color.FromRgb(204, 153, 187)); // Soft pink
        }
        return new SolidColorBrush(Color.FromRgb(128, 128, 128)); // Neutral gray
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}