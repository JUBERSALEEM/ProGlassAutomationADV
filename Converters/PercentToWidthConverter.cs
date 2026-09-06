// ═══════════════════════════════════════════════════════════════════════
// FILE: Converters/PercentToWidthConverter.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Converters
//
// PURPOSE:
//   Converts a percentage into a pixel width for progress bar inner fill.
//
// DUAL-INTERFACE SUPPORT (Chat 15 hotfix):
//   Now implements BOTH IValueConverter AND IMultiValueConverter.
//
//   USAGE 1 — Single value + ConverterParameter (WPF standard):
//     Width="{Binding NormalizedWidth,
//             Converter={StaticResource PercentToWidthConverter},
//             ConverterParameter=260}"
//     - values[0] = 0.0 to 1.0 (normalized) OR 0-100 (percent)
//     - parameter = target pixel width (e.g. "260")
//     - Auto-detects: value <= 1.0 → treated as 0-1, else as 0-100
//
//   USAGE 2 — MultiBinding with 2 values (original DN behavior):
//     <MultiBinding Converter="{StaticResource PercentToWidthConverter}">
//       <Binding Path="ProgressPercent"/>
//       <Binding Path="ActualWidth"
//                RelativeSource="{RelativeSource AncestorType=Border}"/>
//     </MultiBinding>
//     - values[0] = percent 0-100
//     - values[1] = container ActualWidth
//     - No ConverterParameter used
//
//   Both usages coexist safely — existing DeliveryNote MultiBindings
//   continue to work unchanged; new Tax Invoice single-value bindings
//   also work.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    /// <summary>
    /// Converts a percentage into a pixel width for progress-bar inner fill.
    /// Supports both single-value (IValueConverter) and multi-value
    /// (IMultiValueConverter) usage patterns.
    /// </summary>
    public class PercentToWidthConverter : IValueConverter, IMultiValueConverter
    {
        // ═════════════════════════════════════════════════════════════════
        //  IValueConverter — single value + ConverterParameter
        //  Used by Tax Invoice dashboard progress bars.
        // ═════════════════════════════════════════════════════════════════

        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            // Extract the pixel width from ConverterParameter
            double totalWidth = ParseDouble(parameter, defaultValue: 0d);
            if (totalWidth <= 0d) return 0d;

            // Extract percent/normalized value from value
            double raw = ParseDouble(value, defaultValue: 0d);

            // Auto-detect scale:
            //   0.0 to 1.0 → normalized (treat as fraction)
            //   > 1.0      → percent (0-100), divide by 100
            double fraction = raw <= 1.0d ? raw : raw / 100d;

            // Clamp to [0.0, 1.0]
            if (fraction < 0d) fraction = 0d;
            if (fraction > 1d) fraction = 1d;

            return totalWidth * fraction;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
            => Binding.DoNothing;

        // ═════════════════════════════════════════════════════════════════
        //  IMultiValueConverter — MultiBinding with 2 values
        //  Used by DeliveryNote Pending Qty progress bars (original).
        // ═════════════════════════════════════════════════════════════════

        public object Convert(
            object[] values,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return 0d;

            double pct = ParseDouble(values[0], defaultValue: 0d);
            double totalWidth = ParseDouble(values[1], defaultValue: 0d);

            if (totalWidth <= 0d) return 0d;

            // Multi-binding path always treats value as 0-100 percent
            double filled = totalWidth * pct / 100d;
            return Math.Max(0d, Math.Min(totalWidth, filled));
        }

        public object[] ConvertBack(
            object value,
            Type[] targetTypes,
            object parameter,
            CultureInfo culture)
            => Enumerable.Repeat(Binding.DoNothing, targetTypes.Length).ToArray();

        // ═════════════════════════════════════════════════════════════════
        //  PRIVATE HELPER — Safe conversion of any numeric object to double
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Safely parses any object into a double.
        /// Handles: double, float, decimal, int, long, and string (invariant).
        /// Returns defaultValue for null / unparseable input.
        /// </summary>
        private static double ParseDouble(object value, double defaultValue)
        {
            if (value == null) return defaultValue;

            switch (value)
            {
                case double d: return d;
                case float f: return f;
                case decimal m: return (double)m;
                case int i: return i;
                case long l: return l;
                case string s:
                    if (double.TryParse(
                            s,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out var parsed))
                        return parsed;
                    return defaultValue;
                default:
                    // Last-resort try
                    try
                    {
                        return System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        return defaultValue;
                    }
            }
        }
    }
}