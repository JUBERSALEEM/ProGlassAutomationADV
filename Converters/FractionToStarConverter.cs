// ═══════════════════════════════════════════════════════════════════════
// FILE: Converters/FractionToStarConverter.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Converters
//
// PURPOSE
//   Converts a normalised fraction (0.0–1.0) into a star GridLength so a
//   progress/severity bar can be laid out proportionally by a Grid.
//
// WHY THIS EXISTS
//   The dashboard bars previously sized their fill with
//     Width="{Binding NormalizedWidth,
//             Converter={StaticResource PercentToWidthConverter},
//             ConverterParameter=260}"
//   That hardcodes the track at 260 pixels. The card it sits in is
//   width-driven by the window, so on any window that is not exactly
//   the assumed size the fill was proportionally wrong — at 1920 the
//   "100%" bar stopped less than half way across its track.
//
//   Binding a raw double straight to ColumnDefinition.Width does NOT
//   work either: GridLength's type converter reads 0.85 as 0.85
//   ABSOLUTE PIXELS, so every bar collapses to a hairline. It has to be
//   an explicit star GridLength.
//
//   Two columns share the track:
//     col0 Width = fraction*          (the filled part)
//     col1 Width = (1 - fraction)*    (the remainder)  ← ConverterParameter=invert
//   Star weights then resolve to exactly fraction of the real width,
//   whatever that turns out to be.
//
// EDGE CASES
//   • null / unparseable       → 0*  (nothing filled)
//   • value > 1                → treated as a 0–100 percentage, matching
//                                PercentToWidthConverter's auto-detect,
//                                so either scale can be bound safely
//   • clamped to [0,1] so a bad value can never overflow the track
//   • fraction 0 with invert   → 1*, i.e. the whole track is remainder
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    /// <summary>
    /// Normalised fraction (0.0–1.0) → star <see cref="GridLength"/>.
    /// Pass ConverterParameter="invert" to get the complement (1 - f).
    /// </summary>
    public class FractionToStarConverter : IValueConverter
    {
        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            double fraction = ToFraction(value);

            bool invert =
                parameter is string s &&
                s.Equals("invert", StringComparison.OrdinalIgnoreCase);

            if (invert) fraction = 1d - fraction;

            // GridLength rejects negative and NaN; clamp defensively.
            if (double.IsNaN(fraction) || fraction < 0d) fraction = 0d;
            if (fraction > 1d) fraction = 1d;

            return new GridLength(fraction, GridUnitType.Star);
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
            => throw new NotSupportedException(
                "FractionToStarConverter is one-way.");

        /// <summary>
        /// Accepts double/float/decimal/int/long/string. Values above 1
        /// are read as a 0–100 percentage, mirroring the auto-detect in
        /// PercentToWidthConverter so both scales are safe to bind.
        /// </summary>
        private static double ToFraction(object value)
        {
            double raw;

            switch (value)
            {
                case double d: raw = d; break;
                case float f: raw = f; break;
                case decimal m: raw = (double)m; break;
                case int i: raw = i; break;
                case long l: raw = l; break;
                case string str:
                    if (!double.TryParse(
                            str,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out raw))
                    {
                        return 0d;
                    }
                    break;
                default:
                    return 0d;
            }

            if (double.IsNaN(raw) || double.IsInfinity(raw)) return 0d;
            if (raw < 0d) return 0d;

            return raw <= 1d ? raw : raw / 100d;
        }
    }
}
