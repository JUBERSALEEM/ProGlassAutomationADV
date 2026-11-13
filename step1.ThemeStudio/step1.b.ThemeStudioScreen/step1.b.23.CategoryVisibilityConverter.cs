using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProGlassAutomation.ThemeStudio;
public sealed class CategoryVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var current = value as string;
        var wanted = parameter as string;
        return string.Equals(current, wanted, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
public sealed class BoolToVisibilityConverter2 : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}
public sealed class ComponentSelectionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var selected = value as string;
        var tag = parameter as string;
        return string.Equals(selected, tag, StringComparison.Ordinal) ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
