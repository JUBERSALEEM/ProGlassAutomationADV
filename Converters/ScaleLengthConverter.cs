// ═══════════════════════════════════════════════════════════════════════
// FILE: Converters/ScaleLengthConverter.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Converters
//
// PURPOSE
//   Scales a bound Length (e.g. a Window's ActualWidth/ActualHeight) by a
//   constant factor from ConverterParameter:
//
//       MaxWidth="{Binding ActualWidth,
//                 RelativeSource={RelativeSource AncestorType=Window},
//                 Converter={StaticResource ScaleLengthConverter},
//                 ConverterParameter=0.94}"
//
// WHY THIS EXISTS
//   WPF has no percentage Length. The Glass Blush AI chat overlay
//   (Views/Personal/GlassShell.xaml) must never exceed 94% of the window
//   in either dimension. The first attempt wrote MaxWidth="0.94*" — a
//   star GridLength — which is legal ONLY on Grid ColumnDefinition/
//   RowDefinition and crashes every startup with
//   "'0.94*' string cannot be converted to Length" (XamlParseException
//   from GlassShell BAML, seen from MainWindow.InitializeComponent).
//   This converter expresses the same 94%-of-window intent with a
//   valid Length, and is reusable for any constant-fraction cap.
//
// EDGE CASES
//   • null value            → Double.NaN (Length "unset" — element keeps
//                              its own size; safe degradation)
//   • unparseable value     → Double.NaN
//   • null/unparseable param→ the value passes through unscaled (×1)
//   • negative result       → clamped to 0
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Globalization;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    /// <summary>
    /// Bound Length × constant scale factor (ConverterParameter).
    /// One-way; ConvertBack throws NotSupportedException.
    /// </summary>
    public class ScaleLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return double.NaN;

            if (!double.TryParse(
                    Convert.ToString(value, culture),
                    NumberStyles.Float,
                    culture,
                    out double length))
                return double.NaN;

            double scale = 1.0;
            if (parameter != null &&
                double.TryParse(
                    Convert.ToString(parameter, CultureInfo.InvariantCulture),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double parsed))
                scale = parsed;

            double result = length * scale;
            return result < 0 ? 0 : result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
