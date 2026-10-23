using System.Windows;
using ProGlassAutomation.ThemeBento;

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
            // Phase 1 (ADV): only the Bento package is ported and loadable.
            // The remaining eight packages arrive in Phase 4; their identity
            // rows below stay so the nine-theme order and metadata are
            // preserved from the first commit.
            new ThemeDefinition(BentoName, "ThemeBento", BentoResources.Load),
        });

    public static IReadOnlyList<ThemeIdentity> Sequence { get; } =
        Array.AsReadOnly(new[]
        {
            new ThemeIdentity(1, BentoName, "Modular cards, dense information, equal-weight surfaces.", ThemeImplementationStatus.Installed, true, "Bento extras + shared geometry + overlays"),
            new ThemeIdentity(2, ClayName, "Soft inflated forms, tactile depth, playful volume.", ThemeImplementationStatus.Planned, true, "Clay extras + ThemeOwned plumpness + overlays"),
            new ThemeIdentity(3, FluentName, "Layered acrylic, motion, system-aligned chrome.", ThemeImplementationStatus.Planned, false, "Geometry, font, and color plates only — no extras engine"),
            new ThemeIdentity(4, NeumorphismName, "Extruded controls, dual shadows, tactile relief.", ThemeImplementationStatus.Planned, true, "Neu extras + shared rounding + overlays"),
            new ThemeIdentity(5, LiquidGlassName, "Luminous veil, specular highlights, adaptive depth.", ThemeImplementationStatus.Planned, true, "Liquid extras + overlays; visual not accepted"),
            new ThemeIdentity(6, GlassName, "Frosted material, contrast-safe fills, solid fallback.", ThemeImplementationStatus.Planned, false, "Frost surface + solid fallback; owner Home Light+Dark accepted"),
            new ThemeIdentity(7, SpatialName, "Depth layers and spatial chrome.", ThemeImplementationStatus.Planned, false, "Far plane + lifted near cards; no extras engine"),
            new ThemeIdentity(8, SwissName, "Grid, type hierarchy, restrained color.", ThemeImplementationStatus.Planned, false, "Paper, ink, red rule; SharedTokens Crisp; no extras engine"),
            new ThemeIdentity(9, HolographicName, "Iridescent sheen without chart blur.", ThemeImplementationStatus.Planned, false, "Cyan-violet rim + pearl sheen; SharedTokens; no extras engine; no chart blur"),
        });

    public static ThemeDefinition Find(string name) =>
        Installed.FirstOrDefault(theme => theme.Name == name)
        ?? throw new ArgumentException($"Theme '{name}' is not installed.", nameof(name));

    public static ThemeIdentity Identify(string name) =>
        Sequence.FirstOrDefault(theme => theme.Name == name) ?? Sequence[0];
}
