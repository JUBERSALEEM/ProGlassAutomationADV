using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    public class ListToStringConverter : IValueConverter
    {
        public string Separator { get; set; } = ", ";
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable enumerable)
            {
                var parts = enumerable.Cast<object>().Where(o => o != null).Select(o => o.ToString()).Where(s => !string.IsNullOrWhiteSpace(s));
                return string.Join(Separator, parts);
            }
            return value?.ToString() ?? "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
