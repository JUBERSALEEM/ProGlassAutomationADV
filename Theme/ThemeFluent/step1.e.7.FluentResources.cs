using System.Windows;
using ProGlassAutomation.Theming;

namespace ProGlassAutomation.ThemeFluent;

/// <summary>Fluent owns its visual composition. It does not load, cache, or mutate Bento or Clay resources.</summary>
public static class FluentResources
{
    public static ResourceDictionary Load(Appearance appearance)
    {
        Application.Current.Dispatcher.VerifyAccess();
        if (!Enum.IsDefined(appearance)) throw new ArgumentOutOfRangeException(nameof(appearance));
        var package = new ResourceDictionary { Source = Resource($"ThemeFluent.{appearance}.xaml") };
        package.MergedDictionaries.Add(new ResourceDictionary { Source = Resource($"ThemeFluent.{appearance}.Extras.xaml") });
        package.MergedDictionaries.Add(new ResourceDictionary { Source = Resource("step1.e.Components/step1.e.1.Components.xaml") });
        return package;
    }

    private static Uri Resource(string file) => new(
        $"pack://application:,,,/ProGlassAutomation;component/Theme/ThemeFluent/{file}", UriKind.Absolute);
}
