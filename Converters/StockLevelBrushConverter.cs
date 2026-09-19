// ═══════════════════════════════════════════════════════════════════════
// FILE: Converters/StockLevelBrushConverter.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// GB-P8 (2026-09-18) — Glass Blush Personal Sheet Store stock bar tint.
//
// PURE UI helper (no business logic): maps a sheet's remaining balance
// (parameter = total stock) to the Glass Blush semantic level brush used
// by the personal card grid's stock bar:
//     balance/stock  ≥ 0.60  →  GlassBlush.Success  (healthy)
//     balance/stock  ≥ 0.30  →  GlassBlush.Warning  (watch)
//     otherwise             →  GlassBlush.Danger   (low)
//
// Usage (MultiBinding — parameter cannot carry a path in WPF):
//   <ProgressBar.Style>
//     <Setter Property="Foreground">
//       <Setter.Value>
//         <MultiBinding Converter="{StaticResource StockLevelBrushConverter}">
//           <Binding Path="Balance"/>
//           <Binding Path="Stock"/>
//         </MultiBinding>
//       </Setter.Value>
//     </Setter>
//   ...
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ProGlassAutomation.Converters
{
    /// <summary>
    /// Maps (balance, total stock) → Glass Blush semantic level brush for
    /// the Personal sheet-store stock bar. Fails soft: any parse error or
    /// zero denominator yields the muted text brush (never a crash).
    /// </summary>
    /// <remarks>
    /// 2026-09-19 (ERR-DE8757): the XAML uses a <c>MultiBinding</c>, whose
    /// <c>Converter</c> must be an <see cref="IMultiValueConverter"/> —
    /// the class originally implemented only <see cref="IValueConverter"/>
    /// and WPF threw InvalidCastException at template load. Both interfaces
    /// are now implemented; the object[] overload feeds the existing
    /// object[]-aware core.
    /// </remarks>
    public sealed class StockLevelBrushConverter : IValueConverter, IMultiValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var resources = Application.Current?.Resources;
                if (resources == null) return Brushes.Gray;

                // MultiBinding pattern: value = object[] { Balance, Stock }.
                // Single-binding fallback: value = Balance, parameter = Stock.
                double balance, stock;
                if (value is object[] pair && pair.Length == 2)
                {
                    balance = ToDouble(pair[0]);
                    stock = ToDouble(pair[1]);
                }
                else
                {
                    balance = ToDouble(value);
                    stock = ToDouble(parameter);
                }

                if (stock <= 0) return (Brush)resources["GlassBlush.TextMuted"];

                double ratio = balance / stock;
                string key = ratio >= 0.60 ? "GlassBlush.Success"
                            : ratio >= 0.30 ? "GlassBlush.Warning"
                                            : "GlassBlush.Danger";
                return resources[key] as Brush ?? Brushes.Gray;
            }
            catch
            {
                return Brushes.Gray;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;

        // ── IMultiValueConverter (MultiBinding usage) ─────────────────
        // Cast to object on purpose: it forces dispatch to the
        // single-value overload (whose core already handles object[]),
        // avoiding infinite recursion to this overload.
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            => Convert((object)(values ?? System.Array.Empty<object>()), targetType, parameter, culture);

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => System.Array.Empty<object>();

        private static double ToDouble(object o) => o switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            decimal m => (double)m,
            string s => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0d,
            _ => 0d
        };
    }
}
