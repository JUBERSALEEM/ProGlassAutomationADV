// ═══════════════════════════════════════════════════════════════════════
// FILE: Converters/StringEqualsMultiConverter.cs
// PROJECT: ProGlassAutomation
// MODULE: Shared — multi-binding string equality (case-insensitive)
//
// USAGE (active nav detection in MainWindow sidebar):
//   <MultiBinding Converter="{StaticResource StringEqualsMultiConverter}">
//     <Binding Path="CommandParameter" RelativeSource="{RelativeSource TemplatedParent}"/>
//     <Binding Path="DataContext.CurrentModuleKey" RelativeSource="{RelativeSource TemplatedParent}"/>
//   </MultiBinding>
//   → true when the item's module key equals the current module.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Globalization;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    public sealed class StringEqualsMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType,
                              object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return false;
            return string.Equals(
                values[0]?.ToString(),
                values[1]?.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }

        public object[] ConvertBack(object value, Type[] targetTypes,
                                    object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
