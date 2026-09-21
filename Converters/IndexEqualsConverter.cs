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
using System.Windows;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    public class IndexEqualsConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool match = value != null && parameter != null
                && System.Convert.ToInt32(value, culture) == System.Convert.ToInt32(parameter, culture);
            // RadioButton.IsChecked wants bool. Tab UserControl.Visibility
            // wants Visibility — 11–12 Sep content was Collapsed forever when
            // nested DataTriggers failed to match int vs string.
            if (targetType == typeof(Visibility) || targetType == typeof(Visibility?))
                return match ? Visibility.Visible : Visibility.Collapsed;
            return match;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter != null)
                return System.Convert.ToInt32(parameter, culture);
            return Binding.DoNothing;
        }
    }
}
