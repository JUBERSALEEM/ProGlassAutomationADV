// ═══════════════════════════════════════════════════════════════════════
// FILE: Converters/WindowInsetConverter.cs
// PROJECT: ProGlassAutomation
// MODULE: Converters
//
// Subtracts a fixed inset (ConverterParameter) from a bound size value.
//
// Primary use: the floating AI chat panel has a fixed design height of
// 600 px, anchored to the bottom of the window. On short windows the
// panel used to overflow past the TOP edge — clipping the header and
// the voice-assistant row ("voice assistant gets hidden"). The panel
// now binds MaxHeight = Window.ActualHeight − inset, so it shrinks
// gracefully instead of clipping.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Globalization;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    /// <summary>
    /// value − parameter, floored at a sane minimum (80 px) so the
    /// target never collapses to nothing.
    /// </summary>
    public class WindowInsetConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            double source = value is double d ? d : 0;

            double inset = 0;
            if (parameter != null)
                double.TryParse(
                    parameter.ToString(), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out inset);

            double result = source - inset;
            return Math.Max(result, 80);
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
