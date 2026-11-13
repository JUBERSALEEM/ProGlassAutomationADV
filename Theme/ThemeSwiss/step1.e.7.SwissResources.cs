using System.Collections.ObjectModel;
using System.Windows;
using ProGlassAutomation.Theming;

namespace ProGlassAutomation.ThemeSwiss;

/// <summary>Swiss owns its visual composition. It does not load other theme packages.</summary>
public static class SwissResources
{
    private static IReadOnlyDictionary<object, object>? components;

    public static ResourceDictionary Load(Appearance appearance)
    {
        Application.Current.Dispatcher.VerifyAccess();
        if (!Enum.IsDefined(appearance)) throw new ArgumentOutOfRangeException(nameof(appearance));
        components ??= LoadComponents();
        var package = new ResourceDictionary { Source = Resource($"ThemeSwiss.{appearance}.xaml") };
        var parts = new ResourceDictionary();
        foreach (var (key, component) in components) parts.Add(key, component);
        package.MergedDictionaries.Add(parts);
        return package;
    }

    private static IReadOnlyDictionary<object, object> LoadComponents()
    {
        var values = new Dictionary<object, object>();
        Collect(new ResourceDictionary { Source = Resource("step1.e.Components/step1.e.1.Components.xaml") }, values);
        return new ReadOnlyDictionary<object, object>(values);
    }

    private static void Collect(ResourceDictionary dictionary, Dictionary<object, object> values)
    {
        foreach (object key in dictionary.Keys)
        {
            if (values.ContainsKey(key)) throw new InvalidOperationException($"Duplicate Swiss component: {key}");
            object value = dictionary[key];
            if (value is Style style) style.Seal();
            else throw new InvalidOperationException($"Swiss component must be a Style: {key}");
            values.Add(key, value);
        }
        foreach (ResourceDictionary child in dictionary.MergedDictionaries) Collect(child, values);
    }

    private static Uri Resource(string file) => new(
        $"pack://application:,,,/ProGlassAutomation;component/Theme/ThemeSwiss/{file}", UriKind.Absolute);
}
