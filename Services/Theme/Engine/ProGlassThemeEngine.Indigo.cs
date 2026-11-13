using System.Windows;

namespace ProGlassAutomation.Services.Theme.Engine
{
    /// <summary>
    /// Compatibility bridge for the existing Indigo/Ov dashboard component
    /// library. The Ov.* names remain available to legacy XAML consumers,
    /// but their values are now sourced from the active ProGlass family.
    /// </summary>
    public static partial class ProGlassThemeEngine
    {
        private static void PublishIndigoAliases(ResourceDictionary resources)
        {
            CopyBrush(resources, "InfoBrush", "AccentBrush", "AccentBrush");
            CopyBrush(resources, "Ov.Canvas", "SurfaceWorkspaceBrush", "WindowBackgroundBrush");
            CopyBrush(resources, "Ov.Surface", "ThemeCardFaceBrush", "SurfacePrimaryBrush");
            CopyBrush(resources, "Ov.Soft", "ThemeCardHighlightBrush", "SurfaceTertiaryBrush");
            CopyBrush(resources, "Ov.Line", "ThemeCardEdgeBrush", "BorderDefaultBrush");
            CopyBrush(resources, "Ov.Ink", "TextPrimaryBrush", "PrimaryForegroundBrush");
            CopyBrush(resources, "Ov.Muted", "TextSecondaryBrush", "SecondaryForegroundBrush");
            CopyBrush(resources, "Ov.Accent", "AccentBrush", "AccentBrush");
            CopyBrush(resources, "Ov.AccentHover", "AccentHoverBrush", "AccentBrush");
            CopyBrush(resources, "Ov.AreaEnd", "AccentBrush", "AccentBrush");
            CopyBrush(resources, "Ov.OnAccent", "OnAccentForegroundBrush", "PrimaryForegroundBrush");
            CopyBrush(resources, "Ov.OnAccentMuted", "TextSecondaryBrush", "SecondaryForegroundBrush");
            CopyBrush(resources, "Ov.Success", "SuccessBrush", "AccentBrush");
            CopyBrush(resources, "Ov.Danger", "DangerBrush", "AccentBrush");
            CopyBrush(resources, "Ov.DangerSoft", "TintRedBrush", "DangerSoftBrush");
            CopyBrush(resources, "Ov.Warning", "WarningBrush", "AccentBrush");
            CopyBrush(resources, "Ov.SuccessSoft", "TintGreenBrush", "SuccessSoftBrush");
            CopyBrush(resources, "Ov.WarningSoft", "TintAmberBrush", "WarningSoftBrush");
            CopyBrush(resources, "Ov.Hero", "ThemeCardFaceBrush", "SurfaceElevatedBrush");
        }
    }
}
