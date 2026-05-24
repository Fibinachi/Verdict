using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Verdict.Converters;

/// <summary>
/// Maps age to hair color for avatar. Younger people have darker hair, older people have gray/white.
/// </summary>
public sealed class AgeToHairColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int age)
        {
            if (age < 20) return new SolidColorBrush(Color.FromRgb(45, 35, 25));      // Dark brown/black
            if (age < 35) return new SolidColorBrush(Color.FromRgb(60, 45, 30));      // Brown
            if (age < 50) return new SolidColorBrush(Color.FromRgb(100, 75, 45));     // Medium brown
            if (age < 65) return new SolidColorBrush(Color.FromRgb(140, 110, 75));    // Graying
            return new SolidColorBrush(Color.FromRgb(180, 175, 165));                   // White/gray
        }
        return new SolidColorBrush(Color.FromRgb(60, 45, 30)); // Default brown
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}