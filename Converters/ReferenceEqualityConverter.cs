using System;
using System.Globalization;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    /// <summary>
    /// MultiBinding converter: returns true when both values are the same
    /// object reference. Used by the Dashboard tab strip to detect the
    /// active tab page.
    ///   values[0] = ActivePage  (from DashboardViewModel)
    ///   values[1] = current item (DashboardPageBase in the tab list)
    /// </summary>
    public class ReferenceEqualityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType,
                              object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return false;
            return ReferenceEquals(values[0], values[1]);
        }

        public object[] ConvertBack(object value, Type[] targetTypes,
                                    object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}