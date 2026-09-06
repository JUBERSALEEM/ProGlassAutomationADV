// ═══════════════════════════════════════════════════════════════════════
// FILE: Converters/SliderValueLabelConverter.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Converters
//
// PURPOSE:
//   Formats a Slider's value for the centered label of the modern
//   fill-bar slider style. Auto-detects the unit from the slider's
//   Maximum:
//     • Maximum <= 1  → a fraction (e.g. wallpaper opacity 0.3–1) →
//                       shown as a percentage ("62%").
//     • Maximum > 1   → a plain measure (e.g. card border thickness
//                       0–4 DIP) → shown as a number ("1.5").
//
//   Used with a MultiBinding of { Value, Maximum } so no per-instance
//   format string is needed.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    public class SliderValueLabelConverter : IMultiValueConverter
    {
        public object Convert(
            object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return "";

            double value = ToDouble(values[0]);
            double max = ToDouble(values[1]);

            // Fractions (0–1 sliders like opacity) read as percentages.
            if (max <= 1.0)
            {
                double pct = value * 100.0;
                return $"{(int)Math.Round(pct)}%";
            }

            // Plain measure — show the numeric value.
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        public object[] ConvertBack(
            object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => Enumerable.Repeat(Binding.DoNothing, targetTypes.Length).ToArray();

        private static double ToDouble(object value)
        {
            try { return System.Convert.ToDouble(value, CultureInfo.InvariantCulture); }
            catch { return 0d; }
        }
    }
}
