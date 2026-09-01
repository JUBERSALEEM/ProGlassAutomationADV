// ═══════════════════════════════════════════════════════════════════════
// FILE: Converters/IndexEqualsConverter.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 2.5 — tab-strip migration. Maps a SelectedTabIndex (int) to a
// RadioButton's IsChecked: true when the bound index equals the
// ConverterParameter index. ConvertBack returns the parameter index when
// the radio becomes checked, Binding.DoNothing on uncheck (so a group's
// uncheck event never overwrites the newly selected tab).
// ═══════════════════════════════════════════════════════════════════════

using System.Globalization;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    public class IndexEqualsConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return System.Convert.ToInt32(value, culture) == System.Convert.ToInt32(parameter, culture);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter != null)
                return System.Convert.ToInt32(parameter, culture);
            return Binding.DoNothing;
        }
    }
}
