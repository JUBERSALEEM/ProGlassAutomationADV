using System;
using System.Globalization;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    /// <summary>
    /// Converts a percentage (0-100) + available container width into a
    /// pixel width for progress bar inner fill.
    ///
    /// Usage (MultiBinding):
    ///   Value[0] = double ProgressPercent (0-100)
    ///   Value[1] = double ActualWidth of the container border
    ///
    /// Returns: Math.Min(containerWidth, containerWidth * pct / 100)
    /// Used in: DeliveryNote Pending Qty tab progress bars.
    /// </summary>
    public class PercentToWidthConverter : IMultiValueConverter
    {
        public object Convert(
            object[] values,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return 0d;

            if (values[0] is not double pct)
                return 0d;

            if (values[1] is not double totalWidth)
                return 0d;

            if (totalWidth <= 0)
                return 0d;

            double filled = totalWidth * pct / 100d;
            return Math.Max(0d, Math.Min(totalWidth, filled));
        }

        public object[] ConvertBack(
            object value,
            Type[] targetTypes,
            object parameter,
            CultureInfo culture)
            => throw new NotImplementedException();
    }
}