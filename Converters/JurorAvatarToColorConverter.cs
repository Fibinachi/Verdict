using System;
using System.Globalization;
using System.Windows.Data;

namespace Verdict.Converters;

/// <summary>
/// Maps juror political / religious / age traits into a small palette for seat avatars.
/// Input: Agent (as DataContext). Uses Role, Bias, and ReligiousAffiliation when available.
/// </summary>
public sealed class JurorAvatarToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // We return a Brush string; XAML can turn that into a color via ColorConverter if desired.
        // Simpler: return a hex color directly.
        if (value == null) return "#7F8C8D";

        // value is expected to be an Agent.
        // Avoid hard dependency here; use reflection so this converter won't break if model namespaces change.
        var t = value.GetType();
        var get = (string prop) => t.GetProperty(prop)?.GetValue(value);

        string? rel = get("ReligiousAffiliation") as string;
        string? pol = get("PoliticalAffiliation") as string;
        double bias = 0.0;
        var biasObj = get("Bias");
        if (biasObj is double d) bias = d;

        int age = 0;
        var ageObj = get("Age");
        if (ageObj is int i) age = i;

        // Religions: use a distinct palette
        if (!string.IsNullOrWhiteSpace(rel))
        {
            var l = rel.ToLowerInvariant();
            if (l.Contains("catholic") || l.Contains("christian")) return "#D35400"; // orange cross-ish
            if (l.Contains("jewish")) return "#2980B9"; // blue magen-ish
            if (l.Contains("muslim")) return "#27AE60"; // green dome-ish
            if (l.Contains("hindu")) return "#8E44AD"; // purple temple-ish
        }

        // Political: bias primarily drives hat color.
        // Convention: bias > 0 -> prosecution/plaintiff leaning => red; bias < 0 => defense leaning => blue.
        // This is purely visual.
        if (bias >= 0.25) return "#E74C3C"; // red hat
        if (bias <= -0.25) return "#3498DB"; // blue hat

        // Age: older jurors slightly muted
        if (age >= 65) return "#95A5A6";

        // Default/neutral
        return "#2ECC71"; // green
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

