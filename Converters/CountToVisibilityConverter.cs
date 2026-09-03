// ═══════════════════════════════════════════════════════════════════════
// FILE: Converters/CountToVisibilityConverter.cs
// PROJECT: ProGlassAutomation
// MODULE: Value converter — collection count → Visibility
//
// WHAT IT IS
//   Returns Visible when the collection count is 0 (empty state);
//   Collapsed when count > 0 (items present).
//
//   With ConverterParameter="Inverted": returns Visible when count > 0;
//   Collapsed when count is 0.
//
//   Used for empty-state overlays in tables and lists.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    /// <summary>
    /// Converts a collection (or count) to Visibility for empty-state overlays.
    /// Default: count=0 → Visible (show empty state), count>0 → Collapsed.
    /// ConverterParameter="Inverted": count>0 → Visible, count=0 → Collapsed.
    /// </summary>
    public sealed class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            int count = value switch
            {
                int i => i,
                ICollection c => c.Count,
                IEnumerable e => CountEnumerable(e),
                _ => 0
            };

            bool inverted = parameter is string s &&
                            s.Equals("Inverted", StringComparison.OrdinalIgnoreCase);

            if (inverted)
                return count > 0 ? Visibility.Visible : Visibility.Collapsed;
            else
                return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException("CountToVisibilityConverter is one-way only.");
        }

        private static int CountEnumerable(IEnumerable enumerable)
        {
            int count = 0;
            foreach (var _ in enumerable) count++;
            return count;
        }
    }
}
