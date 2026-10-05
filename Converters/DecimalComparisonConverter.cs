// ═══════════════════════════════════════════════════════════════════════
// FILE: Converters/DecimalComparisonConverter.cs
// PROJECT: ProGlassAutomation
// MODULE: Value converter for XAML DataTriggers
//
// WHAT IT IS
//   Compares a decimal (or numeric) value against a threshold passed as
//   ConverterParameter. Returns a string result for use in DataTrigger
//   Value matching:
//
//     ConverterParameter="0"     → returns "LessThan" / "EqualTo" / "GreaterThan"
//     ConverterParameter="70"    → returns "LessThan" / "EqualTo" / "GreaterThan"
//
//   XAML usage:
//     <DataTrigger Binding="{Binding Profit, Converter={StaticResource DecimalComparisonConverter}, ConverterParameter=0}"
//                  Value="LessThan">
//         <Setter Property="Foreground" Value="Red"/>
//     </DataTrigger>
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Globalization;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    /// <summary>
    /// Compares a decimal value against a threshold (ConverterParameter).
    /// Returns "LessThan", "EqualTo", or "GreaterThan" as a string.
    /// </summary>
    public sealed class DecimalComparisonConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            decimal input = value switch
            {
                decimal d => d,
                double dbl => (decimal)dbl,
                float f => (decimal)f,
                int i => i,
                long l => l,
                _ => 0m
            };

            decimal threshold = 0m;
            if (parameter is string s && decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                threshold = parsed;
            else if (parameter is decimal pd)
                threshold = pd;
            else if (parameter is int pi)
                threshold = pi;

            if (input < threshold) return "LessThan";
            if (input > threshold) return "GreaterThan";
            return "EqualTo";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException("DecimalComparisonConverter is one-way only.");
        }
    }
}
