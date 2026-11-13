using System.Windows;
using ProGlassAutomation.Theming;

namespace ProGlassAutomation.ThemeLiquidGlass;

/// <summary>Liquid Glass owns its visual composition. It does not load Bento, Clay, Fluent or Neumorph resources.</summary>
public static class LiquidResources
{
    public static ResourceDictionary Load(Appearance appearance)
    {
        Application.Current.Dispatcher.VerifyAccess();
        if (!Enum.IsDefined(appearance)) throw new ArgumentOutOfRangeException(nameof(appearance));
        var package = new ResourceDictionary { Source = Resource($"ThemeLiquidGlass.{appearance}.xaml") };
        package.MergedDictionaries.Add(new ResourceDictionary { Source = Resource($"ThemeLiquidGlass.{appearance}.Extras.xaml") });
        package.MergedDictionaries.Add(new ResourceDictionary { Source = Resource("step1.e.Components/step1.e.1.Components.xaml") });
        LiquidWallpaper.Install(package, appearance);
        return package;
    }

    private static Uri Resource(string file) => new(
        $"pack://application:,,,/ProGlassAutomation;component/Theme/ThemeLiquidGlass/{file}", UriKind.Absolute);
}
