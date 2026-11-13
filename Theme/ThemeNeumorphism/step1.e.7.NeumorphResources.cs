using System.Windows;
using ProGlassAutomation.Theming;

namespace ProGlassAutomation.ThemeNeumorphism;

/// <summary>Neumorphism owns its visual composition. It does not load Bento, Clay, or Fluent resources.</summary>
public static class NeumorphResources
{
    public static ResourceDictionary Load(Appearance appearance)
    {
        Application.Current.Dispatcher.VerifyAccess();
        if (!Enum.IsDefined(appearance)) throw new ArgumentOutOfRangeException(nameof(appearance));
        var package = new ResourceDictionary { Source = Resource($"ThemeNeumorphism.{appearance}.xaml") };
        package.MergedDictionaries.Add(new ResourceDictionary { Source = Resource($"ThemeNeumorphism.{appearance}.Extras.xaml") });
        package.MergedDictionaries.Add(new ResourceDictionary { Source = Resource("step1.e.Components/step1.e.1.Components.xaml") });
        return package;
    }

    private static Uri Resource(string file) => new(
        $"pack://application:,,,/ProGlassAutomation;component/Theme/ThemeNeumorphism/{file}", UriKind.Absolute);
}
