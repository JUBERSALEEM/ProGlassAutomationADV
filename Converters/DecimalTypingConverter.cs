using System;
using System.Globalization;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    /// <summary>
    /// Two-way decimal converter that does not swallow a trailing decimal
    /// separator while the user is still typing.
    ///
    /// WPF's default converter + StringFormat + PropertyChanged turns
    /// "150." into 150 and writes "150" back, so a rate / SQM box can
    /// never accept a decimal. Returning Binding.DoNothing for incomplete
    /// input leaves the typed text in the box until a full number exists.
    /// </summary>
    public sealed class DecimalTypingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not decimal number)
                return string.Empty;

            if (number == 0m)
                return string.Empty;

            string format = parameter as string ?? "0.##";
            return number.ToString(format, CultureInfo.InvariantCulture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = (value as string ?? string.Empty).Trim();
            if (text.Length == 0)
                return 0m;

            // Incomplete while typing: keep the TextBox text, do not push.
            if (text is "." or "," or "-" or "-." or "-,")
                return Binding.DoNothing;

            if (text.EndsWith(".", StringComparison.Ordinal) ||
                text.EndsWith(",", StringComparison.Ordinal))
                return Binding.DoNothing;

            if (decimal.TryParse(
                    text.Replace(',', '.'),
                    NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out decimal parsed))
                return parsed;

            return Binding.DoNothing;
        }
    }
}
