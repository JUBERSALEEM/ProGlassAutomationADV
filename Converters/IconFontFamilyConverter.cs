// ═══════════════════════════════════════════════════════════════════════
// FILE: Converters/IconFontFamilyConverter.cs
// PROJECT: ProGlassAutomation
// MODULE: Shared — picks the right font family for an icon string
//
// Single-char strings in the private-use area are Segoe MDL2 Assets
// glyphs (e.g. "\xE710"); everything else (emoji, text) renders with
// Segoe UI Emoji / Segoe UI. Lets shared controls accept BOTH glyph
// codes and legacy emoji without breaking either.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ProGlassAutomation.Converters
{
    /// <summary>Returns the font family matching an icon string.</summary>
    public sealed class IconFontFamilyConverter : IValueConverter
    {
        private static readonly FontFamily Mdl2 =
            new FontFamily("Segoe MDL2 Assets");
        private static readonly FontFamily Emoji =
            new FontFamily("Segoe UI Emoji");

        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            if (value is string s && s.Length == 1 && s[0] >= '\uE700' && s[0] <= '\uF8FF')
                return Mdl2;

            return Emoji;
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
