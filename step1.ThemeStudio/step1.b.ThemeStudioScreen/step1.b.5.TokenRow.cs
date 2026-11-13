using System.Globalization;
using System.Windows.Media;

namespace ProGlassAutomation.ThemeStudio;

public sealed record TokenRow(string Name, string Kind, string Value, Brush? Preview)
{
    public static TokenRow From(string name, object value) => new(name, value.GetType().Name,
        value is double number ? number.ToString("0.##", CultureInfo.InvariantCulture) : value.ToString() ?? "",
        value as Brush);
}
