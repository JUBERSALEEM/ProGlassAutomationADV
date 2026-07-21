using System;
using System.Globalization;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    /// <summary>
    /// Scales a normalized double (0.0–1.0) to a pixel height.
    /// ConverterParameter = max height in pixels as a string, e.g. "160".
    /// Enforces a minimum of 4px so empty bars are still visible.
    /// Used by all dashboard bar charts.
    /// </summary>
    public class ScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            if (value is double normalized
                && parameter is string paramStr
                && double.TryParse(paramStr,
                       System.Globalization.NumberStyles.Any,
                       System.Globalization.CultureInfo.InvariantCulture,
                       out double maxHeight))
            {
                return Math.Max(normalized * maxHeight, 4.0);
            }
            return 4.0;
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}