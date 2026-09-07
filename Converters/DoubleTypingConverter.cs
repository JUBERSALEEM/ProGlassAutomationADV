using System;
using System.Globalization;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    /// <summary>
    /// Two-way double converter for numeric text boxes (e.g. logo width /
    /// height in millimetres). Mirrors <see cref="DecimalTypingConverter"/>
    /// but targets double and does not swallow a trailing decimal separator
    /// while the user is still typing.
    /// </summary>
    public sealed class DoubleTypingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double number)
                return string.Empty;

            if (number == 0d)
                return string.Empty;

            string format = parameter as string ?? "0.#";
            return number.ToString(format, CultureInfo.InvariantCulture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string text = (value as string ?? string.Empty).Trim();
            if (text.Length == 0)
                return 0d;

            // Incomplete while typing: keep the TextBox text, do not push.
            if (text is "." or "," or "-" or "-." or "-,")
                return Binding.DoNothing;

            if (text.EndsWith(".", StringComparison.Ordinal) ||
                text.EndsWith(",", StringComparison.Ordinal))
                return Binding.DoNothing;

            if (double.TryParse(
                    text.Replace(',', '.'),
                    NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out double parsed))
                return parsed;

            return Binding.DoNothing;
        }
    }
}
