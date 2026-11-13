using System.Windows;
using ProGlassAutomation.Theming;

namespace ProGlassAutomation.ThemeGlassmorphism;

/// <summary>Glassmorphism owns its visual composition. It does not load, cache, or mutate other theme packages.</summary>
public static class GlassResources
{
    public static ResourceDictionary Load(Appearance appearance)
    {
        Application.Current.Dispatcher.VerifyAccess();
        if (!Enum.IsDefined(appearance)) throw new ArgumentOutOfRangeException(nameof(appearance));
        var package = new ResourceDictionary { Source = Resource($"ThemeGlassmorphism.{appearance}.xaml") };
        package.MergedDictionaries.Add(new ResourceDictionary { Source = Resource($"ThemeGlassmorphism.{appearance}.Extras.xaml") });
        package.MergedDictionaries.Add(new ResourceDictionary { Source = Resource("step1.e.Components/step1.e.1.Components.xaml") });
        GlassWallpaper.Install(package, appearance);
        return package;
    }

    private static Uri Resource(string file) => new(
        $"pack://application:,,,/ProGlassAutomation;component/Theme/ThemeGlassmorphism/{file}", UriKind.Absolute);
}
