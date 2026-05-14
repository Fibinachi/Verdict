using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Verdict.Models;

namespace Verdict.Converters
{
    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public static InverseBooleanConverter Instance = new InverseBooleanConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return value;
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                if (parameter?.ToString() == "Inverse") b = !b;
                return b ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Windows.Visibility v)
            {
                bool b = v == System.Windows.Visibility.Visible;
                if (parameter?.ToString() == "Inverse") b = !b;
                return b;
            }
            return false;
        }
    }
    
    public class EnumMatchConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string enumString)
            {
                if (Enum.TryParse(value.GetType(), enumString, out object? enumValue))
                {
                    return value.Equals(enumValue);
                }
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter is string enumString)
            {
                return Enum.Parse(targetType, enumString);
            }
            return Binding.DoNothing;
        }
    }
}

namespace Verdict
{
    public class ClerkTemplateSelector : DataTemplateSelector
    {
        public DataTemplate AgentTemplate { get; set; } = null!;
        public DataTemplate ClerkTemplate { get; set; } = null!;

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is Agent agent)
            {
                if (agent.Role == AgentRole.Reporter) // The clerk/reporter seat
                {
                    return ClerkTemplate;
                }
            }
            return AgentTemplate;
        }
    }
}