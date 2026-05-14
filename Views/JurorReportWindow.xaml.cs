using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Verdict.Models;

namespace Verdict.Views;

public partial class JurorReportWindow : Window
{
    public JurorReportWindow(Agent juror)
    {
        InitializeComponent();
        Title = $"Juror Report - {juror.Name}";
        DataContext = juror;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

/// <summary>
/// Converts a MemoryEntry's Strength (0.0-1.0) to a color for the strength bar.
/// Also supports a ConverterParameter=Width mode to return a proportional width percentage.
/// </summary>
public class StrengthToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double strength = 0.5;
        if (value is double d)
            strength = d;

        if (parameter is string param && param == "Width")
        {
            // Return a percentage of the available width (max 100%)
            return Math.Max(4.0, strength * 200.0); // 200 is approximate available width
        }

        // Return color based on strength
        if (strength >= 0.7)
            return new SolidColorBrush(Color.FromRgb(46, 204, 113)); // Green
        else if (strength >= 0.4)
            return new SolidColorBrush(Color.FromRgb(241, 196, 15)); // Yellow
        else
            return new SolidColorBrush(Color.FromRgb(231, 76, 60));  // Red
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
