using System;
using System.Globalization;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    /// <summary>
    /// GB-P15 — experience gate for top-bar popups.
    ///
    /// values[0] = the popup's open flag (bool, e.g. IsNotificationsOpen).
    /// values[1] = MainViewModel.IsPersonalExperience.
    /// ConverterParameter "Personal" → open when values[0] && values[1]
    ///   (the Personal shell's own anchored popups, in GlassShell).
    /// anything else (default) → open when values[0] && !values[1]
    ///   (the Professional popups, anchored to the Professional top bar —
    ///    which is collapsed in Personal mode, so un-gated they would
    ///    float at the window origin).
    ///
    /// The Personal and Professional popup pairs are BOTH bound to the
    /// SAME MainViewModel open flags (one source of truth, no new state);
    /// this converter decides which anchored copy may actually open.
    /// </summary>
    public class ExperienceGatedConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0 || values[0] is not bool open)
                return false;

            bool personal = values.Length > 1 && values[1] is true;
            bool forPersonal = string.Equals(
                parameter as string, "Personal", StringComparison.OrdinalIgnoreCase);

            return forPersonal ? open && personal : open && !personal;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
