using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using ProGlassAutomation.Theming;

namespace ProGlassAutomation.ThemeNeumorphism;

public enum NeumorphDepth { Soft, Standard, Deep }
public enum NeumorphBlur { Tight, Standard, Diffuse }
public enum NeumorphIntensity { Quiet, Standard, Strong }

public sealed record NeumorphCustomizationState(
    NeumorphDepth Depth = NeumorphDepth.Standard,
    NeumorphBlur Blur = NeumorphBlur.Standard,
    NeumorphIntensity Intensity = NeumorphIntensity.Standard)
{
    public string? Validate()
    {
        if (!Enum.IsDefined(Depth) || !Enum.IsDefined(Blur) || !Enum.IsDefined(Intensity))
            return "Unsupported neumorph customization choice.";
        return null;
    }
}

public sealed record NeumorphNamedPreset(string Name, NeumorphCustomizationState State, Appearance Appearance);

/// <summary>Neumorph-owned depth engine. Never mutates Bento, Clay, or Fluent packages.</summary>
public static class NeumorphCustomization
{
    public static IReadOnlyList<NeumorphNamedPreset> BuiltIn { get; } =
    [
        new("Standard Lift", new NeumorphCustomizationState(), Appearance.Light),
        new("Soft Quiet", new NeumorphCustomizationState(NeumorphDepth.Soft, NeumorphBlur.Tight, NeumorphIntensity.Quiet), Appearance.Light),
        new("Deep Diffuse", new NeumorphCustomizationState(NeumorphDepth.Deep, NeumorphBlur.Diffuse, NeumorphIntensity.Strong), Appearance.Light),
        new("Night Deep", new NeumorphCustomizationState(NeumorphDepth.Deep, NeumorphBlur.Standard, NeumorphIntensity.Strong), Appearance.Dark),
    ];

    public static bool IsBuiltIn(string name) => BuiltIn.Any(p => p.Name == name);

    public static void Apply(ResourceDictionary package, Appearance mode, NeumorphCustomizationState state)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(state);
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (state.Validate() is not null) throw new ArgumentException(state.Validate());

        double distance = state.Depth switch { NeumorphDepth.Soft => 3d, NeumorphDepth.Deep => 10d, _ => 6d };
        double blur = state.Blur switch { NeumorphBlur.Tight => 8d, NeumorphBlur.Diffuse => 24d, _ => 16d };
        double darkOpacity = state.Intensity switch { NeumorphIntensity.Quiet => 0.22, NeumorphIntensity.Strong => 0.55, _ => 0.4 };
        double lightOpacity = state.Intensity switch { NeumorphIntensity.Quiet => 0.5, NeumorphIntensity.Strong => 0.95, _ => 0.8 };
        SetOwned(package, "NeumorphDistance", distance);
        SetOwned(package, "NeumorphBlur", blur);
        SetOwned(package, "NeumorphLiftDistance", distance + 3d);
        SetOwned(package, "NeumorphDarkOpacity", darkOpacity);
        SetOwned(package, "NeumorphLightOpacity", lightOpacity);
        double spread = 0d;
        SetOwned(package, "NeumorphSpread", spread);
        SetOwned(package, "NeumorphSpreadMargin", new Thickness(-spread));
        SetOwned(package, "NeumorphSpreadThickness", new Thickness(spread));
        SetOwned(package, "NeumorphMotionDuration", MotionDuration());
        Track(package);
        Color surface = SurfaceColor(package, mode);
        Color dark = Mix(surface, Colors.Black, mode == Appearance.Light ? 0.18 : 0.28);
        Color light = Mix(surface, Colors.White, mode == Appearance.Light ? 0.85 : 0.14);
        SetOwned(package, "NeumorphDarkColor", dark);
        SetOwned(package, "NeumorphLightColor", light);
        SetOwned(package, "NeumorphDarkBrush", new SolidColorBrush(dark));
        SetOwned(package, "NeumorphLightBrush", new SolidColorBrush(light));
        SetOwned(package, "NeumorphRimShadeBrush", new SolidColorBrush(Mix(surface, Colors.Black, mode == Appearance.Light ? 0.10 : 0.22)));
        SetOwned(package, "NeumorphRimLightBrush", new SolidColorBrush(Mix(surface, Colors.White, mode == Appearance.Light ? 0.55 : 0.08)));
    }

    public static Duration MotionDuration()
    {
        bool animate = SystemParameters.ClientAreaAnimation
            && SystemParameters.MenuAnimation
            && SystemParameters.ComboBoxAnimation;
        return animate ? new Duration(TimeSpan.FromMilliseconds(160)) : new Duration(TimeSpan.Zero);
    }

    private static WeakReference<ResourceDictionary>? _package;

    private static void Track(ResourceDictionary package)
    {
        _package = new WeakReference<ResourceDictionary>(package);
        SystemParameters.StaticPropertyChanged -= OnSystemParameters;
        SystemParameters.StaticPropertyChanged += OnSystemParameters;
    }

    private static void OnSystemParameters(object? sender, PropertyChangedEventArgs e)
    {
        if (_package is null || !_package.TryGetTarget(out ResourceDictionary? package)) return;
        SetOwned(package, "NeumorphMotionDuration", MotionDuration());
    }

    private static Color SurfaceColor(ResourceDictionary package, Appearance mode)
    {
        if (FindBrush(package, "SurfacePrimary") is SolidColorBrush brush)
            return brush.Color;
        return mode == Appearance.Light
            ? (Color)ColorConverter.ConvertFromString("#E6EBEF")
            : (Color)ColorConverter.ConvertFromString("#2A2F38");
    }

    private static SolidColorBrush? FindBrush(ResourceDictionary dictionary, string key)
    {
        foreach (object existing in dictionary.Keys)
        {
            if (!Equals(existing, key)) continue;
            return dictionary[key] as SolidColorBrush;
        }
        foreach (ResourceDictionary child in dictionary.MergedDictionaries)
        {
            SolidColorBrush? found = FindBrush(child, key);
            if (found is not null) return found;
        }
        return null;
    }

    private static Color Mix(Color from, Color toward, double amount)
    {
        byte Channel(byte a, byte b) => (byte)Math.Clamp((int)Math.Round(a + (b - a) * amount), 0, 255);
        return Color.FromRgb(Channel(from.R, toward.R), Channel(from.G, toward.G), Channel(from.B, toward.B));
    }

    private static void SetOwned(ResourceDictionary package, string key, object value)
    {
        if (ReplaceOwn(package, key, value)) return;
        package[key] = value;
    }

    private static bool ReplaceOwn(ResourceDictionary dictionary, string key, object value)
    {
        foreach (object existing in dictionary.Keys)
        {
            if (!Equals(existing, key)) continue;
            dictionary[key] = value;
            return true;
        }
        foreach (ResourceDictionary child in dictionary.MergedDictionaries)
            if (ReplaceOwn(child, key, value)) return true;
        return false;
    }
}
