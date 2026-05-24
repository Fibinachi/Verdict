using System;
using System.Globalization;
using System.Windows.Data;

namespace Verdict.Converters;

/// <summary>
/// Maps age to hair height for avatar. Younger people have fuller hair, older people show balding.
/// </summary>
public sealed class AgeToHairHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int age)
        {
            if (age < 25) return 4.0;  // Full hair
            if (age < 40) return 4.0;  // Full hair
            if (age < 55) return 3.0;  // Slightly thinning
            if (age < 65) return 2.0;  // Partial bald
            return 1.0;                // Mostly bald
        }
        return 4.0; // Default full hair
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}