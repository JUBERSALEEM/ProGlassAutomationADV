// ═══════════════════════════════════════════════════════════════════════
// FILE: Converters/StringToVisibilityConverter.cs
// PROJECT: ProGlassAutomation
// MODULE: Value converter — string → Visibility
//
// WHAT IT IS
//   Returns Visible when the input string is non-null and non-empty;
//   Collapsed otherwise. Used to show/hide recommendation boxes,
//   status messages, and other conditional text blocks.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    /// <summary>
    /// Converts a string to Visibility. Non-empty → Visible; null/empty → Collapsed.
    /// </summary>
    public sealed class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string s && !string.IsNullOrWhiteSpace(s))
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException("StringToVisibilityConverter is one-way only.");
        }
    }
}
