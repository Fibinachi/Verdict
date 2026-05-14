using System;
using System.Globalization;
using System.Windows.Data;

namespace Verdict.Converters;

/// <summary>
/// Provides a tiny avatar 'type' for simple WPF drawing.
/// Output values:
/// - "Temple" (for certain religious affiliations)
/// - "Hat" (default)
/// - "Circle" (fallback)
/// </summary>
public sealed class JurorAvatarGeometryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return "Circle";
        var t = value.GetType();
        var get = (string prop) => t.GetProperty(prop)?.GetValue(value);

        var rel = get("ReligiousAffiliation") as string;
        if (string.IsNullOrWhiteSpace(rel)) return "Hat";

        var l = rel.ToLowerInvariant();
        if (l.Contains("muslim") || l.Contains("hindu") || l.Contains("jewish")) return "Temple";
        if (l.Contains("catholic") || l.Contains("protestant") || l.Contains("christian") || l.Contains("other christian")) return "Temple";
        return "Hat";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

