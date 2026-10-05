using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    /// <summary>
    /// Returns true/Visible if the bound decimal is > 0.
    /// When ConverterParameter == "Visibility", returns Visibility.Visible / Collapsed.
    /// Otherwise returns bool.
    /// </summary>
    public class DecimalIsPositiveConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isPositive = value is decimal d && d > 0m;

            // Visibility mode
            if (parameter is string p && p.Equals("Visibility", StringComparison.OrdinalIgnoreCase))
                return isPositive ? Visibility.Visible : Visibility.Collapsed;

            return isPositive;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}