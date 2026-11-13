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
            CopyBrush(resources, "Ov.OnAccent", "ThemeNavigationSelectedForegroundBrush", "OnAccentForegroundBrush");
            CopyBrush(resources, "Ov.OnAccentMuted", "ThemeNavigationSelectedForegroundBrush", "OnAccentForegroundBrush");
            CopyBrush(resources, "Ov.Success", "SuccessBrush", "AccentBrush");
            CopyBrush(resources, "Ov.Danger", "DangerBrush", "AccentBrush");
            CopyBrush(resources, "Ov.DangerSoft", "TintRedBrush", "DangerSoftBrush");
            CopyBrush(resources, "Ov.Warning", "WarningBrush", "AccentBrush");
            CopyBrush(resources, "Ov.SuccessSoft", "TintGreenBrush", "SuccessSoftBrush");
            CopyBrush(resources, "Ov.WarningSoft", "TintAmberBrush", "WarningSoftBrush");
            CopyBrush(resources, "Ov.Hero", "ThemeNavigationSelectedBrush", "AccentBrush");

            // Color-valued companions keep scoped SolidColorBrush aliases live.
            // A StaticResource binding to an Ov.* Brush captures the old Brush
            // instance when a view loads; DynamicResource Color tokens instead
            // re-resolve whenever the family publisher replaces the palette.
            var palette = ProGlassPalettes.Resolve(Family, Mode);
            resources["Ov.CanvasColor"] = ReadColor(resources, "Ov.Canvas", palette.Window);
            resources["Ov.SurfaceColor"] = ReadColor(resources, "Ov.Surface", palette.Surface);
            resources["Ov.SoftColor"] = ReadColor(resources, "Ov.Soft", palette.Input);
            resources["Ov.LineColor"] = ReadColor(resources, "Ov.Line", palette.Border);
            resources["Ov.InkColor"] = ReadColor(resources, "Ov.Ink", palette.Text);
            resources["Ov.MutedColor"] = ReadColor(resources, "Ov.Muted", palette.TextMuted);
            resources["Ov.AccentColor"] = ReadColor(resources, "Ov.Accent", palette.Accent);
            resources["Ov.AccentHoverColor"] = ReadColor(resources, "Ov.AccentHover", palette.Accent);
            resources["Ov.OnAccentColor"] = ReadColor(resources, "Ov.OnAccent", System.Windows.Media.Colors.White);
            resources["Ov.SuccessColor"] = ReadColor(resources, "Ov.Success", System.Windows.Media.Color.FromRgb(0x16, 0xA3, 0x4A));
            resources["Ov.SuccessSoftColor"] = ReadColor(resources, "Ov.SuccessSoft", System.Windows.Media.Color.FromArgb(0x35, 0x16, 0xA3, 0x4A));
            resources["Ov.WarningColor"] = ReadColor(resources, "Ov.Warning", System.Windows.Media.Color.FromRgb(0xD9, 0x77, 0x06));
            resources["Ov.WarningSoftColor"] = ReadColor(resources, "Ov.WarningSoft", System.Windows.Media.Color.FromArgb(0x35, 0xD9, 0x77, 0x06));
            resources["Ov.DangerColor"] = ReadColor(resources, "Ov.Danger", System.Windows.Media.Color.FromRgb(0xB9, 0x1C, 0x1C));
            resources["Ov.DangerSoftColor"] = ReadColor(resources, "Ov.DangerSoft", System.Windows.Media.Color.FromArgb(0x35, 0xB9, 0x1C, 0x1C));
        }
    }
}
