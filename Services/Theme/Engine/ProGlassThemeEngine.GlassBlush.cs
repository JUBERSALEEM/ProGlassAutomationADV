using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme.Engine
{
    /// <summary>
    /// Bridges the legacy Personal/GlassBlush component token plane to the
    /// authoritative nine-family ProGlass theme publisher. This keeps the
    /// existing Personal XAML component contract intact while making its
    /// surfaces, accents, text, and semantic states follow the active family
    /// and Light/Dark mode.
    /// </summary>
    public static partial class ProGlassThemeEngine
    {
        private static void PublishGlassBlushAliases(ResourceDictionary resources)
        {
            var palette = ProGlassPalettes.Resolve(Family, Mode);
            Color accent = ReadColor(resources, "AccentBrush", palette.Accent);
            Color text = ReadColor(resources, "TextPrimaryBrush", palette.Text);
            Color muted = ReadColor(resources, "TextSecondaryBrush", palette.TextMuted);
            Color border = ReadColor(resources, "BorderDefaultBrush", palette.Border);
            Color success = ReadColor(resources, "SuccessBrush", Color.FromRgb(0x16, 0xA3, 0x4A));
            Color warning = ReadColor(resources, "WarningBrush", Color.FromRgb(0xD9, 0x77, 0x06));
            Color danger = ReadColor(resources, "DangerBrush", Color.FromRgb(0xB9, 0x1C, 0x1C));
            Color onAccent = ReadColor(resources, "OnAccentForegroundBrush", Colors.White);

            // Color tokens remain available to effects and any legacy bindings
            // that consume the original GlassBlush color-key contract.
            resources["GlassBlush.BackgroundDeepColor"] = palette.Window;
            resources["GlassBlush.BackgroundColor"] = palette.Window;
            resources["GlassBlush.BackgroundAzureColor"] = palette.Surface;
            resources["GlassBlush.BackgroundGlowColor"] = accent;
            resources["GlassBlush.OrbColor"] = WithAlpha(accent, 0x55);
            resources["GlassBlush.EdgeOrbColor"] = WithAlpha(border, 0x35);
            resources["GlassBlush.RailColor"] = palette.Window;
            resources["GlassBlush.CardShadowColor"] = WithAlpha(Colors.Black, Mode == ProGlassThemeMode.Dark ? (byte)0x70 : (byte)0x30);
            resources["GlassBlush.SurfaceColor"] = palette.Surface;
            resources["GlassBlush.SurfaceStrongColor"] = palette.Elevated;
            resources["GlassBlush.SurfaceFaintColor"] = palette.Input;
            resources["GlassBlush.SurfacePressedColor"] = palette.Elevated;
            resources["GlassBlush.BorderColor"] = border;
            resources["GlassBlush.BorderSoftColor"] = WithAlpha(border, 0x75);
            resources["GlassBlush.SheenColor"] = WithAlpha(onAccent, 0x55);
            resources["GlassBlush.SheenFadeColor"] = WithAlpha(onAccent, 0x18);
            resources["GlassBlush.TextColor"] = text;
            resources["GlassBlush.TextMutedColor"] = muted;
            resources["GlassBlush.TextFaintColor"] = WithAlpha(muted, 0xB0);
            resources["GlassBlush.AccentColor"] = accent;
            resources["GlassBlush.AccentGlowColor"] = accent;
            resources["GlassBlush.AccentDeepColor"] = accent;
            resources["GlassBlush.AccentSoftColor"] = WithAlpha(accent, 0x55);
            resources["GlassBlush.OnAccentColor"] = onAccent;
            resources["GlassBlush.SuccessColor"] = success;
            resources["GlassBlush.SuccessSoftColor"] = WithAlpha(success, 0x35);
            resources["GlassBlush.WarningColor"] = warning;
            resources["GlassBlush.WarningSoftColor"] = WithAlpha(warning, 0x35);
            resources["GlassBlush.DangerColor"] = danger;
            resources["GlassBlush.DangerSoftColor"] = WithAlpha(danger, 0x35);
            resources["GlassBlush.InfoColor"] = accent;
            resources["GlassBlush.InfoSoftColor"] = WithAlpha(accent, 0x35);

            // Repoint the actual brush resources consumed by Personal views.
            // These are direct application-resource writes, so they take
            // precedence over the static GlassBlushTokens.xaml fallbacks.
            CopyBrush(resources, "GlassBlush.Background", "SurfaceWorkspaceBrush", "WindowBackgroundBrush");
            CopyBrush(resources, "GlassBlush.OrbTopRight", "TintIndigoBrush", "AccentBrush");
            CopyBrush(resources, "GlassBlush.OrbBottomLeft", "ThemeCardHighlightBrush", "SurfaceTertiaryBrush");
            CopyBrush(resources, "GlassBlush.Sheen", "ThemeCardHighlightBrush", "SurfaceTertiaryBrush");
            CopyBrush(resources, "GlassBlush.Rail", "SurfaceSidebarBrush", "SideMenuBackgroundBrush");
            CopyBrush(resources, "GlassBlush.CardShadow", "BorderSubtleBrush", "BorderDefaultBrush");
            CopyBrush(resources, "GlassBlush.Surface", "ThemeCardFaceBrush", "SurfacePrimaryBrush");
            CopyBrush(resources, "GlassBlush.SurfaceStrong", "SurfaceElevatedBrush", "SurfaceSecondaryBrush");
            CopyBrush(resources, "GlassBlush.SurfaceFaint", "ThemeControlFaceBrush", "SurfaceTertiaryBrush");
            CopyBrush(resources, "GlassBlush.SurfacePressed", "ThemeCardHighlightBrush", "SurfaceTertiaryBrush");
            CopyBrush(resources, "GlassBlush.Border", "ThemeCardEdgeBrush", "BorderDefaultBrush");
            CopyBrush(resources, "GlassBlush.BorderSoft", "ThemeControlEdgeBrush", "BorderSubtleBrush");
            CopyBrush(resources, "GlassBlush.Text", "TextPrimaryBrush", "PrimaryForegroundBrush");
            CopyBrush(resources, "GlassBlush.TextMuted", "TextSecondaryBrush", "SecondaryForegroundBrush");
            CopyBrush(resources, "GlassBlush.TextFaint", "TextTertiaryBrush", "MutedForegroundBrush");
            CopyBrush(resources, "GlassBlush.Accent", "AccentBrush", "AccentBrush");
            CopyBrush(resources, "GlassBlush.AccentGlow", "FocusBrush", "AccentBrush");
            CopyBrush(resources, "GlassBlush.AccentDeep", "AccentHoverBrush", "AccentBrush");
            CopyBrush(resources, "GlassBlush.AccentSoft", "TintIndigoBrush", "AccentBrush");
            CopyBrush(resources, "GlassBlush.OnAccent", "OnAccentForegroundBrush", "PrimaryForegroundBrush");
            CopyBrush(resources, "GlassBlush.Success", "SuccessBrush", "AccentBrush");
            CopyBrush(resources, "GlassBlush.SuccessSoft", "TintGreenBrush", "SuccessSoftBrush");
            CopyBrush(resources, "GlassBlush.Warning", "WarningBrush", "AccentBrush");
            CopyBrush(resources, "GlassBlush.WarningSoft", "TintAmberBrush", "WarningSoftBrush");
            CopyBrush(resources, "GlassBlush.Danger", "DangerBrush", "AccentBrush");
            CopyBrush(resources, "GlassBlush.DangerSoft", "TintRedBrush", "DangerSoftBrush");
            CopyBrush(resources, "GlassBlush.Info", "AccentBrush", "AccentBrush");
            CopyBrush(resources, "GlassBlush.InfoSoft", "TintBlueBrush", "InfoSoftBrush");

            // The night-production palette is also theme-driven; it no longer
            // pins Personal production screens to a separate sapphire palette.
            CopyBrush(resources, "GlassBlushNight.Background", "SurfaceWorkspaceBrush", "WindowBackgroundBrush");
            CopyBrush(resources, "GlassBlushNight.Surface", "ThemeCardFaceBrush", "SurfacePrimaryBrush");
            CopyBrush(resources, "GlassBlushNight.SurfaceStrong", "SurfaceElevatedBrush", "SurfaceSecondaryBrush");
            CopyBrush(resources, "GlassBlushNight.Border", "ThemeCardEdgeBrush", "BorderDefaultBrush");
            CopyBrush(resources, "GlassBlushNight.Accent", "AccentBrush", "AccentBrush");
            CopyBrush(resources, "GlassBlushNight.AccentGlow", "FocusBrush", "AccentBrush");
            CopyBrush(resources, "GlassBlushNight.Text", "TextPrimaryBrush", "PrimaryForegroundBrush");
            CopyBrush(resources, "GlassBlushNight.TextMuted", "TextSecondaryBrush", "SecondaryForegroundBrush");
        }

        private static Color ReadColor(ResourceDictionary resources, string key, Color fallback)
        {
            if (resources.Contains(key))
            {
                if (resources[key] is SolidColorBrush brush)
                    return brush.Color;
                if (resources[key] is Color color)
                    return color;
            }
            return fallback;
        }

        private static Color WithAlpha(Color color, byte alpha)
            => Color.FromArgb(alpha, color.R, color.G, color.B);
    }
}
