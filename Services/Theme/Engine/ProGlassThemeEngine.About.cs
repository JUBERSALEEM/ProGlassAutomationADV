using System.Windows;
namespace ProGlassAutomation.Services.Theme.Engine
{
    /// <summary>Maps the historical About resource names onto the live ProGlass theme palette.</summary>
    public static partial class ProGlassThemeEngine
    {
        private static void PublishAboutAliases(ResourceDictionary resources)
        {
            CopyBrush(resources, "Ios26CardBrush", "SettingsCardFaceBrush", "ThemeCardFaceBrush");
            CopyBrush(resources, "Ios26TextTitleBrush", "TextPrimaryBrush", "PrimaryForegroundBrush");
            CopyBrush(resources, "Ios26TextMutedBrush", "TextSecondaryBrush", "SecondaryForegroundBrush");
            CopyBrush(resources, "Ios26TextBodyBrush", "TextPrimaryBrush", "PrimaryForegroundBrush");
            CopyBrush(resources, "Ios26AccentSoftBrush", "TintIndigoBrush", "AccentBrush");
            CopyBrush(resources, "Ios26SuccessSoftBrush", "TintGreenBrush", "SuccessSoftBrush");
            CopyBrush(resources, "Ios26ValuePillBrush", "ThemeCardHighlightBrush", "SurfaceTertiaryBrush");
            CopyBrush(resources, "Ios26TileBrush", "ThemeControlFaceBrush", "SurfaceTertiaryBrush");
            CopyBrush(resources, "Ios26HairlineBrush", "ThemeCardEdgeBrush", "BorderDefaultBrush");
            CopyBrush(resources, "Ios26HoverBrush", "ThemeCardHighlightBrush", "SurfaceTertiaryBrush");
            CopyBrush(resources, "Ios26TrackBrush", "ThemeControlEdgeBrush", "BorderDefaultBrush");
            CopyBrush(resources, "Ios26AccentBrush", "AccentBrush", "AccentBrush");
            CopyBrush(resources, "Ios26TextSecondaryBrush", "TextSecondaryBrush", "SecondaryForegroundBrush");
            CopyBrush(resources, "Ios26CanvasBrush", "SurfaceWorkspaceBrush", "WindowBackgroundBrush");
            CopyBrush(resources, "Ios26OrbTealBrush", "TintBlueBrush", "AccentBrush");
            CopyBrush(resources, "Ios26OrbBlueBrush", "TintIndigoBrush", "AccentBrush");
            CopyBrush(resources, "Ios26OrbLavenderBrush", "TintPurpleBrush", "AccentBrush");
            CopyBrush(resources, "Ios26OrbGreenBrush", "TintGreenBrush", "AccentBrush");
            CopyBrush(resources, "Ios26OnAccentChipBrush", "OnAccentForegroundBrush", "PrimaryForegroundBrush");
            CopyBrush(resources, "Ios26OnAccentBrush", "OnAccentForegroundBrush", "PrimaryForegroundBrush");
            CopyBrush(resources, "Ios26TileBorderBrush", "ThemeControlEdgeBrush", "BorderDefaultBrush");
            CopyBrush(resources, "Ios26DangerDotBrush", "DangerBrush", "AccentBrush");
            CopyBrush(resources, "Ios26TextFaintBrush", "TextTertiaryBrush", "MutedForegroundBrush");
            CopyBrush(resources, "Ios26AccentSoftTextBrush", "AccentBrush", "AccentBrush");
            CopyBrush(resources, "Ios26SuccessBrush", "SuccessBrush", "AccentBrush");
            CopyBrush(resources, "Ios26WarningBrush", "WarningBrush", "AccentBrush");
            CopyBrush(resources, "Ios26TileBadgeBrush", "TintIndigoBrush", "AccentBrush");
            CopyBrush(resources, "Ios26SearchBrush", "ThemeControlFaceBrush", "SurfaceInputBrush");
        }
    }
}
