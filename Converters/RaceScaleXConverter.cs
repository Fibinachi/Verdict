using System;
using System.Globalization;
using System.Windows.Data;

namespace Verdict.Converters;

/// <summary>
/// Maps race to a horizontal scale factor for avatar face shape.
/// </summary>
public sealed class RaceScaleXConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string race)
        {
            var r = race.ToLowerInvariant();
            if (r.Contains("asian")) return 0.9;   // Slightly narrower face
            if (r.Contains("african") || r.Contains("black")) return 1.1;  // Slightly wider face
        }
        return 1.0; // Default
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}