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
                // Material-role resources stay family-painter-owned. Do not
                // alias these back to one semantic brush: doing so collapses
                // distinct card/frost/crystal/raised surfaces after Publish().
                // Semantic consumers read from these roles via SemanticSources.
                ["GlassBorderSoft"] = "BorderSubtleBrush",
                ["GlassBorderBright"] = "BorderStrongBrush",
                ["LiquidCard"] = "SurfacePrimaryBrush",
                ["LiquidOverlayBrush"] = "SurfaceOverlayBrush",
                ["ElevatedSurfaceBrush"] = "SurfaceSecondaryBrush",
                // ["PopupSurfaceBrush"] = "SurfaceOverlayBrush"  ← REMOVED.
                // This one entry inverted the meaning of the app's popup
                // surface token. DualWriteLegacy() runs at the END of every
                // Publish(), after the family painters, so it overwrote
                // PopupSurfaceBrush with the live SurfaceOverlayBrush — an
                // OVERLAY/scrim token (SemanticColors.xaml: #140F172A, 8%
                // opaque) that SemanticSources re-points at LiquidOverlayBrush
                // or MenuHoverBrush. PopupSurfaceBrush is authored OPAQUE in
                // LightTheme (#FFF8FAFC) and DarkTheme (#FF111827), written
                // opaque by the iOS 26 painter (#FFFFFF / #26272E) and by
                // GuaranteeCriticalResources (Color.FromRgb) — all three were
                // being undone here, on every theme/family/accent change, for
                // all 14 of its consumers. It is not a legacy Crystal/Liquid/
                // Glass key, so it does not belong in this bridge at all.
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

                // ═══ ABOUT MODULE DYNAMIC BRIDGE ═══
                // Dual-write semantic tokens to the Ios26* keys read by About views
                // so the About module cards and text dynamically adapt to Monet,
                // Crystal Fluid, Industrial, and custom accent colors in real-time.
                ["Ios26CardBrush"] = "SurfacePrimaryBrush",
                ["Ios26TileBrush"] = "SurfaceSecondaryBrush",
                ["Ios26RailBrush"] = "SurfaceSecondaryBrush",
                ["Ios26CardBorderBrush"] = "BorderDefaultBrush",
                ["Ios26TileBorderBrush"] = "BorderSubtleBrush",
                ["Ios26HairlineBrush"] = "DividerBrush",
                ["Ios26TrackBrush"] = "DividerBrush",
                ["Ios26HoverBrush"] = "SurfaceTertiaryBrush",
                ["Ios26TextTitleBrush"] = "TextPrimaryBrush",
                ["Ios26TextBodyBrush"] = "TextPrimaryBrush",
                ["Ios26TextSecondaryBrush"] = "TextSecondaryBrush",
                ["Ios26TextMutedBrush"] = "TextTertiaryBrush",
                ["Ios26TextFaintBrush"] = "TextTertiaryBrush",
                ["Ios26AccentBrush"] = "AccentBrush",
                ["Ios26AccentDeepBrush"] = "AccentPressedBrush",
                ["Ios26AccentSoftBrush"] = "TintBlueBrush",
                ["Ios26AccentSoftTextBrush"] = "AccentBrush",
                ["Ios26SuccessBrush"] = "SuccessBrush",
                ["Ios26SuccessSoftBrush"] = "TintGreenBrush",
                ["Ios26ValuePillBrush"] = "SurfaceSecondaryBrush",
                ["Ios26SearchBrush"] = "SurfaceInputBrush",
                ["Ios26NumberBrush"] = "AccentBrush",
                ["Ios26DangerDotBrush"] = "DangerBrush",
            };

        /// <summary>Engine-managed keys copied INTO the TokenStore (live family paint → semantic).</summary>
        public static IReadOnlyList<(string Semantic, string Source, string Fallback)> SemanticSources { get; } =
            new (string, string, string)[]
            {
                ("SurfacePrimaryBrush", "GlassUltraClearBrush", "ElevatedSurfaceBrush"),
                ("SurfaceSecondaryBrush", "ElevatedSurfaceBrush", "ModuleBackgroundBrush"),
                ("SurfaceTertiaryBrush", "MenuHoverBrush", "GlassSurfaceUltra"),
                ("SurfaceInputBrush", "GlassSurfaceStandard", "GlassSurfaceUltra"),
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
