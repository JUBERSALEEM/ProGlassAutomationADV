// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/CrystalFluid/Converters/ProgressWidthConverter.cs
// PROJECT: ProGlassAutomation
//
// IOS-17PM AUTHORED CONVERTER (P2 2026-09-05) — integrated port of the
// CrystalFluid reference implementation (incoming file 11). No defects
// registered: UnsetValue-safe pattern matching, min==max guard, fraction
// clamped [0,1] — the exact defensive shape the template needs.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Globalization;
using System.Windows.Data;

namespace ProGlassAutomation.Controls.CrystalFluid
{
    /// <summary>
    /// Converts progress bar Value/Min/Max/ActualWidth to fill width.
    /// </summary>
    public class ProgressWidthConverter : IMultiValueConverter
    {
        public static readonly ProgressWidthConverter Instance = new();

        public object Convert(object[] values, Type targetType,
            object parameter, CultureInfo culture)
        {
            if (values.Length < 4)
                return 0.0;

            if (values[0] is double value &&
                values[1] is double min &&
                values[2] is double max &&
                values[3] is double width)
            {
                if (max - min <= 0)
                    return 0.0;

                double fraction = (value - min) / (max - min);
                fraction = Math.Max(0, Math.Min(1, fraction));
                return width * fraction;
            }

            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes,
            object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
