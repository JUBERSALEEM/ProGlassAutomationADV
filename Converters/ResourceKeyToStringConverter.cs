// ═══════════════════════════════════════════════════════════════════════
//  FILE: Converters/ResourceKeyToStringConverter.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-02 — data-driven cards without losing localization
//  ---------------------------------------------------------------------
//  WHY THIS EXISTS
//
//  Hand-written cards embed their label as {DynamicResource Nav.Overview},
//  which the localization system rewrites live when the language changes.
//  Moving those cards into an ItemsControl means the label now arrives as
//  DATA — a plain string on a view model — and a data string cannot carry
//  a DynamicResource.
//
//  Naively binding the literal title would silently DROP translation: the
//  rail would stay English while the rest of the app switched to Arabic.
//  That is a regression disguised as a refactor, so instead the view model
//  carries the resource KEY ("Nav.Overview") and this converter resolves it
//  against the merged language dictionary at bind time.
//
//  LIVE LANGUAGE SWITCHING
//  LocalizationService swaps the Themes/Languages/{code}.xaml dictionary at
//  runtime. A one-shot IValueConverter would not re-run on that swap, so
//  each item's key binding must be refreshed — SalesReportViewModel raises
//  a property-changed notification for its nav collection when the language
//  changes, which re-evaluates every binding through this converter.
//
//  FALLBACK BEHAVIOUR
//  An unknown key returns the key itself rather than empty string. A label
//  reading "Nav.Overview" is an obvious, findable bug; a blank card is a
//  mystery. Never fail silently in the UI.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    /// <summary>
    /// Resolves an application resource key (e.g. "Nav.Overview") to its
    /// current localized string. Used by data-driven card lists so that
    /// item labels stay translatable.
    /// </summary>
    public sealed class ResourceKeyToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            var key = value as string;
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            try
            {
                var found = Application.Current?.TryFindResource(key);
                if (found is string s && !string.IsNullOrEmpty(s))
                    return s;
            }
            catch
            {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("Convert suppressed");
            }

            // Visible, greppable fallback — see header note.
            return key;
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
