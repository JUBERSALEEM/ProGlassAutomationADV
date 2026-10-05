using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ProGlassAutomation.Converters
{
    /// <summary>
    /// Resolves a resource brush KEY name (string) to the actual SolidColorBrush
    /// currently registered in the app's theme resources.
    ///
    /// Usage in VM: TintColor = "TintIndigoBrush"
    /// Usage in XAML:
    ///   Background="{Binding TintColor, Converter={StaticResource BrushKeyConverter}}"
    ///
    /// Automatically adapts to Light/Dark theme swap because it re-resolves
    /// against Application.Current.Resources each time the binding evaluates.
    ///
    /// Fallback chain:
    ///   1. If value is null/empty → returns Brushes.Transparent
    ///   2. If value is a hex string (starts with '#') → parses as hex color
    ///      (backward-compat with any callers still passing raw hex)
    ///   3. Otherwise treated as a brush resource key and looked up
    ///   4. If key not found → returns Brushes.Transparent
    /// </summary>
    public class BrushKeyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            if (value is not string key || string.IsNullOrWhiteSpace(key))
                return Brushes.Transparent;

            // Backward-compat: hex string still works
            if (key.StartsWith("#"))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(key);
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();
                    return brush;
                }
                catch
                {
                    return Brushes.Transparent;
                }
            }

            // Look up brush by resource key
            var resource = Application.Current?.TryFindResource(key);
            if (resource is Brush b)
                return b;

            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}