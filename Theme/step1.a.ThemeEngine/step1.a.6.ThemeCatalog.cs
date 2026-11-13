using System.Windows;
using ProGlassAutomation.ThemeBento;
using ProGlassAutomation.ThemeClaymorphism;
using ProGlassAutomation.ThemeFluent;
using ProGlassAutomation.ThemeGlassmorphism;
using ProGlassAutomation.ThemeLiquidGlass;
using ProGlassAutomation.ThemeNeumorphism;
using ProGlassAutomation.ThemeSpatial;
using ProGlassAutomation.ThemeSwiss;
using ProGlassAutomation.ThemeHolographic;

namespace ProGlassAutomation.Theming;

public enum ThemeGeometry { SharedTokens, ThemeOwned }

public enum ThemeImplementationStatus { Installed, Planned }

/// <summary>Nine-theme identity. Installed packages stay on <see cref="ThemeCatalog.Installed"/>.</summary>
public sealed record ThemeIdentity(
    int Order,
    string Name,
    string Description,
    ThemeImplementationStatus Status,
    bool CanCustomizeMaterial,
    string Capabilities);

public sealed record ThemeDefinition(
    string Name,
    string Folder,
    Func<Appearance, ResourceDictionary> LoadResources,
    ThemeGeometry Geometry = ThemeGeometry.SharedTokens);

/// <summary>Register only complete, authorized themes. No placeholder entries.</summary>
public static class ThemeCatalog
{
    public const string BentoName = "Bento Grid / Modular";
    public const string ClayName = "Claymorphism";
    public const string FluentName = "Fluent / Acrylic";
    public const string NeumorphismName = "Neumorphism";
    public const string LiquidGlassName = "Liquid Glass";
    public const string GlassName = "Glassmorphism";
    public const string SpatialName = "Spatial / Immersive";
    public const string SwissName = "Swiss International";
    public const string HolographicName = "Holographic / Iridescent";
    public static IReadOnlyList<ThemeDefinition> Installed { get; } =
        Array.AsReadOnly(new[]
        {
            new ThemeDefinition(BentoName, "ThemeBento", BentoResources.Load),
            new ThemeDefinition(ClayName, "ThemeClaymorphism", ClayResources.Load, ThemeGeometry.ThemeOwned),
            new ThemeDefinition(FluentName, "ThemeFluent", FluentResources.Load),
            new ThemeDefinition(NeumorphismName, "ThemeNeumorphism", NeumorphResources.Load),
            new ThemeDefinition(LiquidGlassName, "ThemeLiquidGlass", LiquidResources.Load),
            new ThemeDefinition(GlassName, "ThemeGlassmorphism", GlassResources.Load),
            new ThemeDefinition(SpatialName, "ThemeSpatial", SpatialResources.Load),
            new ThemeDefinition(SwissName, "ThemeSwiss", SwissResources.Load),
            new ThemeDefinition(HolographicName, "ThemeHolographic", HoloResources.Load),
        });

    public static IReadOnlyList<ThemeIdentity> Sequence { get; } =
        Array.AsReadOnly(new[]
        {
            new ThemeIdentity(1, BentoName, "Modular cards, dense information, equal-weight surfaces.", ThemeImplementationStatus.Installed, true, "Bento extras + shared geometry + overlays"),
            new ThemeIdentity(2, ClayName, "Soft inflated forms, tactile depth, playful volume.", ThemeImplementationStatus.Installed, true, "Clay extras + ThemeOwned plumpness + overlays"),
            new ThemeIdentity(3, FluentName, "Layered acrylic, motion, system-aligned chrome.", ThemeImplementationStatus.Installed, false, "Geometry, font, and color plates only — no extras engine"),
            new ThemeIdentity(4, NeumorphismName, "Extruded controls, dual shadows, tactile relief.", ThemeImplementationStatus.Installed, true, "Neu extras + shared rounding + overlays"),
            new ThemeIdentity(5, LiquidGlassName, "Luminous veil, specular highlights, adaptive depth.", ThemeImplementationStatus.Installed, true, "Liquid extras + overlays; visual not accepted"),
            new ThemeIdentity(6, GlassName, "Frosted material, contrast-safe fills, solid fallback.", ThemeImplementationStatus.Installed, false, "Frost surface + solid fallback; owner Home Light+Dark accepted"),
            new ThemeIdentity(7, SpatialName, "Depth layers and spatial chrome.", ThemeImplementationStatus.Installed, false, "Far plane + lifted near cards; no extras engine"),
            new ThemeIdentity(8, SwissName, "Grid, type hierarchy, restrained color.", ThemeImplementationStatus.Installed, false, "Paper, ink, red rule; SharedTokens Crisp; no extras engine"),
            new ThemeIdentity(9, HolographicName, "Iridescent sheen without chart blur.", ThemeImplementationStatus.Installed, false, "Cyan-violet rim + pearl sheen; SharedTokens; no extras engine; no chart blur"),
        });

    public static ThemeDefinition Find(string name) =>
        Installed.FirstOrDefault(theme => theme.Name == name)
        ?? throw new ArgumentException($"Theme '{name}' is not installed.", nameof(name));

    public static ThemeIdentity Identify(string name) =>
        Sequence.FirstOrDefault(theme => theme.Name == name) ?? Sequence[0];
}
