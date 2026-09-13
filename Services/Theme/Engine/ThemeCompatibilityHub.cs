using System.Collections.Generic;

namespace ProGlassAutomation.Services.Theme.Engine
{
    /// <summary>
    /// MIGRATION BRIDGE. Old Crystal/Liquid/Glass keys are written to the
    /// SAME frozen brush instance as the semantic TokenStore key after every
    /// Publish. New XAML must use the semantic name. Delete this hub in
    /// Phase 12 after a zero-reference sweep.
    /// Direction: semantic owns legacy.
    /// </summary>
    public static class ThemeCompatibilityHub
    {
        /// <summary>legacy key → semantic TokenStore key.</summary>
        public static IReadOnlyDictionary<string, string> LegacyToSemantic { get; } =
            new Dictionary<string, string>
            {
                ["GlassUltraClearBrush"] = "SurfacePrimaryBrush",
                ["GlassSurfaceBrush"] = "SurfacePrimaryBrush",
                ["GlassCrystalBrush"] = "SurfacePrimaryBrush",
                ["GlassBorderSoft"] = "BorderSubtleBrush",
                ["GlassBorderPrimary"] = "BorderDefaultBrush",
                ["GlassBorderBright"] = "BorderStrongBrush",
                ["GlassBorderGradientCrystal"] = "BorderDefaultBrush",
                ["LiquidCard"] = "SurfacePrimaryBrush",
                ["LiquidOverlayBrush"] = "SurfaceOverlayBrush",
                ["GlassFrostedBrush"] = "SurfaceSecondaryBrush",
                ["ModuleBackgroundBrush"] = "SurfacePrimaryBrush",
                ["ElevatedSurfaceBrush"] = "SurfaceSecondaryBrush",
                ["GlassSurfaceElevated"] = "SurfaceSecondaryBrush",
                ["GlassSurfaceUltra"] = "SurfaceTertiaryBrush",
                ["GlassSurfaceStandard"] = "SurfaceTertiaryBrush",
                ["PopupSurfaceBrush"] = "SurfaceOverlayBrush",
                ["MenuHoverBrush"] = "SurfaceTertiaryBrush",
                ["MutedForegroundBrush"] = "TextTertiaryBrush",
                ["LiquidControlDisabledBrush"] = "TextDisabledBrush",
                ["OutlineDefaultBrush"] = "BorderDefaultBrush",
                ["OutlineBrush"] = "BorderDefaultBrush",
                ["LiquidFocusBrush"] = "BorderFocusBrush",
                ["AccentLightBrush"] = "AccentSoftBrush",
                ["AccentDarkBrush"] = "AccentPressedBrush",
                ["LiquidSelectionBrush"] = "SelectionBrush",
                ["PrintSurfaceBrush"] = "ReportPrimaryBrush",
                ["PrintBorderBrush"] = "ReportBorderBrush",
                ["PrintTextPrimaryBrush"] = "ReportTextBrush",
                ["PrintAccentBrush"] = "ReportAccentBrush",
            };

        /// <summary>Engine-managed keys copied INTO the TokenStore (live family paint → semantic).</summary>
        public static IReadOnlyList<(string Semantic, string Source, string Fallback)> SemanticSources { get; } =
            new (string, string, string)[]
            {
                ("SurfacePrimaryBrush", "GlassUltraClearBrush", "ElevatedSurfaceBrush"),
                ("SurfaceSecondaryBrush", "ElevatedSurfaceBrush", "ModuleBackgroundBrush"),
                ("SurfaceTertiaryBrush", "MenuHoverBrush", "GlassSurfaceUltra"),
                ("SurfaceInputBrush", "ModuleBackgroundBrush", "GlassSurfaceUltra"),
                ("SurfaceOverlayBrush", "LiquidOverlayBrush", "MenuHoverBrush"),
                ("TextTertiaryBrush", "MutedForegroundBrush", "TextSecondaryBrush"),
                ("TextDisabledBrush", "LiquidControlDisabledBrush", "MutedForegroundBrush"),
                ("TextPlaceholderBrush", "MutedForegroundBrush", "TextSecondaryBrush"),
                ("BorderSubtleBrush", "GlassBorderSoft", "OutlineBrush"),
                ("BorderStrongBrush", "GlassBorderBright", "OutlineStrongBrush"),
                ("BorderDefaultBrush", "OutlineDefaultBrush", "OutlineBrush"),
                ("BorderFocusBrush", "LiquidFocusBrush", "AccentBrush"),
                ("AccentSoftBrush", "AccentLightBrush", "AccentBrush"),
                ("AccentPressedBrush", "AccentDarkBrush", "AccentBrush"),
                ("InputBorderBrush", "OutlineBrush", "BorderBrush"),
                ("InputFocusBorderBrush", "AccentBrush", "OutlineStrongBrush"),
                ("InputPlaceholderBrush", "MutedForegroundBrush", "TextSecondaryBrush"),
                ("InputValidationBrush", "DangerBrush", "AccentBrush"),
                ("TabActiveBrush", "AccentBrush", "AccentDarkBrush"),
                ("TabHoverBrush", "MenuHoverBrush", "GlassSurfaceUltra"),
                ("TabFocusBrush", "LiquidFocusBrush", "AccentBrush"),
                ("SelectionBrush", "LiquidSelectionBrush", "AccentLightBrush"),
                ("SelectionForegroundBrush", "OnAccentForegroundBrush", "TextPrimaryBrush"),
                ("ReportPrimaryBrush", "PrintSurfaceBrush", "ElevatedSurfaceBrush"),
                ("ReportTextBrush", "TextPrimaryBrush", "PrimaryForegroundBrush"),
                ("ReportBorderBrush", "PrintBorderBrush", "OutlineBrush"),
                ("ReportAccentBrush", "PrintAccentBrush", "AccentDarkBrush"),
            };
    }
}
