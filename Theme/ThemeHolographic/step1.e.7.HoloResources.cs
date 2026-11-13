using System.Windows;
using ProGlassAutomation.Theming;

namespace ProGlassAutomation.ThemeHolographic;

/// <summary>Holographic owns its visual composition. It does not load other theme packages.</summary>
public static class HoloResources
{
    public static ResourceDictionary Load(Appearance appearance)
    {
        Application.Current.Dispatcher.VerifyAccess();
        if (!Enum.IsDefined(appearance)) throw new ArgumentOutOfRangeException(nameof(appearance));
        var package = new ResourceDictionary { Source = Resource($"ThemeHolographic.{appearance}.xaml") };
        package.MergedDictionaries.Add(new ResourceDictionary { Source = Resource($"ThemeHolographic.{appearance}.Extras.xaml") });
        package.MergedDictionaries.Add(new ResourceDictionary { Source = Resource("step1.e.Components/step1.e.1.Components.xaml") });
        return package;
    }

    private static Uri Resource(string file) => new(
        $"pack://application:,,,/ProGlassAutomation;component/Theme/ThemeHolographic/{file}", UriKind.Absolute);
}
