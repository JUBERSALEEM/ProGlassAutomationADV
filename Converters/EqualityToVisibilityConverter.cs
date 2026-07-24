using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    /// <summary>
    /// Converts a value to Visibility based on equality with the ConverterParameter.
    /// 
    /// USAGE:
    ///   <TextBlock Visibility="{Binding SelectedTabIndex,
    ///              Converter={StaticResource EqualityToVisibilityConverter},
    ///              ConverterParameter=0}"/>
    ///   
    ///   → Visible when SelectedTabIndex == 0, else Collapsed
    /// 
    /// SUPPORTS:
    ///   • int, long, double, decimal (numeric)
    ///   • string (case-insensitive by default)
    ///   • enum
    ///   • bool
    ///   • Any type that implements Equals correctly
    /// 
    /// EDGE CASES:
    ///   • null value + null parameter    → Visible (both null = equal)
    ///   • null value + non-null param    → Collapsed
    ///   • non-null value + null param    → Collapsed
    ///   • Type mismatch (int vs "int" string) → tries string comparison as fallback
    /// 
    /// INVERSE MODE:
    ///   Pass "invert" prefix in ConverterParameter to flip result:
    ///   ConverterParameter="invert:0"  → Collapsed when equals 0, else Visible
    /// </summary>
    public class EqualityToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Parse parameter — may have "invert:" prefix
            bool invert = false;
            object? compareTo = parameter;

            if (parameter is string paramStr && paramStr.StartsWith("invert:", StringComparison.OrdinalIgnoreCase))
            {
                invert = true;
                compareTo = paramStr.Substring("invert:".Length);
            }

            bool isEqual = AreEqual(value, compareTo);

            if (invert)
                isEqual = !isEqual;

            return isEqual ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException(
                "EqualityToVisibilityConverter is one-way only.");
        }

        // ─────────────────────────────────────────────────────────────
        // Equality logic — handles null + type coercion
        // ─────────────────────────────────────────────────────────────
        private static bool AreEqual(object? a, object? b)
        {
            // Both null → equal
            if (a == null && b == null) return true;

            // One null, one not → not equal
            if (a == null || b == null) return false;

            // Direct equality (fast path — same reference or Equals override)
            if (a.Equals(b)) return true;

            // Type mismatch fallback: compare string representations
            // (Handles int vs "5" in XAML parameter)
            string aStr = a.ToString() ?? string.Empty;
            string bStr = b.ToString() ?? string.Empty;

            return string.Equals(aStr, bStr, StringComparison.OrdinalIgnoreCase);
        }
    }
}