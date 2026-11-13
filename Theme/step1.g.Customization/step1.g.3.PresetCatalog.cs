using ProGlassAutomation.Theming;
using ProGlassAutomation.ThemeBento;

namespace ProGlassAutomation.Customization;

public static class PresetCatalog
{
    public static IReadOnlyList<NamedPreset> BuiltIn { get; } = [
        new("Balanced Bento", new BentoCustomizationState(LayoutPreset: BentoLayoutPreset.Balanced), Appearance.Light),
        new("Executive Overview", new BentoCustomizationState(LayoutPreset: BentoLayoutPreset.Executive, Canvas: BentoCanvas.Warm, SurfaceStyle: BentoSurfaceStyle.Elevated, Shadow: BentoShadowIntensity.Elevated, Spacing: BentoSpacing.Spacious), Appearance.Light),
        new("Analytics Focus", new BentoCustomizationState(LayoutPreset: BentoLayoutPreset.AnalyticsFocus, SurfaceStyle: BentoSurfaceStyle.Contrast, Shadow: BentoShadowIntensity.Subtle, CardPadding: BentoCardPadding.Compact), Appearance.Dark),
        new("Production Operations", new BentoCustomizationState(LayoutPreset: BentoLayoutPreset.ProductionOps, Density: BentoDensity.Compact, BorderIntensity: BentoBorderIntensity.Strong, TextContrast: BentoTextContrast.High), Appearance.Light),
        new("Compact Data Dense", new BentoCustomizationState(LayoutPreset: BentoLayoutPreset.CompactDense, Density: BentoDensity.Compact, Spacing: BentoSpacing.Compact, CardPadding: BentoCardPadding.Compact, Shadow: BentoShadowIntensity.None), Appearance.Light),
    ];

    public static bool IsBuiltIn(string name) => BuiltIn.Any(p => p.Name == name);
}
