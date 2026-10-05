using System;
using System.Globalization;
using ProGlassAutomation.Services.Logging;
using System.Windows.Data;
using System.Windows.Media;

namespace ProGlassAutomation.Converters
{
    /// <summary>
    /// Converts a hex color string (e.g. "#6366F1") to a SolidColorBrush.
    /// Used by dashboard KPI badges, chart bars, status pills.
    ///
    /// IMPORTANT: XAML Background/Foreground/Fill properties require Brush,
    /// NOT Color. Returning Color causes silent binding failure and
    /// element renders with no color (transparent/default).
    /// </summary>
    public class ColorStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    var brush = new SolidColorBrush(color);
                    brush.Freeze(); // performance — makes it immutable and thread-safe
                    return brush;
                }
                catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // One-way converter (dashboard badges, chart bars, status
            // pills). Never thrown: DoNothing leaves the source untouched
            // instead of crashing a stray two-way binding.
            return Binding.DoNothing;
        }
    }
}