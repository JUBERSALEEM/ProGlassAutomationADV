// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/VisualThemeEngine.Ios26.cs
// PROJECT: ProGlassAutomation
//
// iOS 26 LIQUID GLASS family — APPLICATION-WIDE (2026-09-11, owner ask):
//   "each view completely adapt to ios 26 theme — card style font style
//    shadow effect etc — complete ui with single theme switch, macOS style."
//
// Two override layers, published ONLY while iOS 26 is the active family
// (removed on every switch-away — see Part1 removal contract):
//   1. GLOBAL layer  — the shared token surface every module consumes
//      dynamically: surfaces, Apple ink text colors, hairlines, the
//      radius scale, the GLOBAL effect tokens, the Liquid/Shell frost
//      vocabulary and the Motion* KeySplines.
//   2. ABOUT layer   — the About redesign specifics (wallpaper, slabs,
//      chrome gate, card shadow).
//
// ⚠ CCTPURITY CONTRACT (ERR-760BA3, 2026-09-11): the type initializer of
//   VisualThemeEngine runs the moment ANY engine member is touched during
//   startup. This file therefore builds NO WPF objects at type-init time:
//   Ios26TokenKeys is a pure string-array merge; ALL brush construction
//   (tables, wallpaper fallback) happens lazily — the tables only inside
//   ApplyIos26 (apply-time, WPF fully alive), the neutral wallpaper via
//   a lazy property. Violating this once killed the app at boot.
//
// PERF GUARD: every global effect is BlurRadius ≤ 14 at Depth ≤ 3
// (the 2026-08-25 freeze was r28 across 20 About cards).
//
// NORMAL RESTORE: SeedNeutralAboutTokens copies the CURRENT Surface.*
// values into the About glass keys on every non-iOS26 apply (Part1,
// BEFORE the family switch — no absence window). No literals, no
// cross-file StaticResource (theme dicts merge before DesignTokens).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme
{
    public static partial class VisualThemeEngine
    {
        /// <summary>iOS 26 Liquid Glass — the full-application family.</summary>
        public const string Ios26 = "iOS 26";

        /// <summary>Switch-away fallback for the About stage wallpaper.
        /// LAZY (see CCTPURITY): first touch happens during a family
        /// switch, never during type initialization.</summary>
        internal static SolidColorBrush Ios26NeutralWallpaper =>
            _ios26NeutralWallpaper ??= CreateIos26NeutralWallpaper();

        private static SolidColorBrush? _ios26NeutralWallpaper;

        private static SolidColorBrush CreateIos26NeutralWallpaper()
        {
            var b = new SolidColorBrush(Colors.Transparent);
            b.Freeze();
            return b;
        }

        /// <summary>
        /// iOS radius that respects the Appearance slider.
        /// The iOS family has its own slightly larger continuous-corner language
        /// (16/14/12 vs 12 baseline), but the user's slider must still drive it.
        /// So we scale the iOS default proportionally to the current CardCornerRadius.
        /// If rounded surfaces are OFF, return 0 — same contract as semantic surfaces.
        /// </summary>
        private static CornerRadius IosRadius(double iosDefault)
        {
            try
            {
                var appearance = App.Config?.Appearance;
                if (appearance?.UseRoundedSurfaces == false)
                    return new CornerRadius(0);

                double baseR = ProGlassAutomation.Services.ThemeManager.CardCornerRadius;
                if (baseR < 0) baseR = 0;
                if (baseR > 28) baseR = 28;
                // iosDefault was authored for base 12, scale proportionally
                double scaled = baseR * (iosDefault / 12.0);
                if (scaled < 0) scaled = 0;
                if (scaled > 48) scaled = 48;
                return new CornerRadius(scaled);
            }
            catch
            {
                return new CornerRadius(iosDefault);
            }
        }



        /// <summary>About-specific keys (redesign milestone).</summary>
        private static readonly string[] Ios26AboutKeys =
        {
            "AboutWallpaperBrush", "AboutGlassSlabBrush", "AboutGlassPanelBrush",
            "AboutGlassSlabStroke", "AboutGlassHairlineStroke",
            "AboutGlassCapsuleBrush", "AboutGlassCaptionBrush",
            "AboutGlassGlowBrush", "AboutCardCornerRadius",
            "AboutPanelCornerRadius", "AboutGlassChromeVisibility",
            "AboutCardEffect",
        };

        /// <summary>Key names of the global override tables. PURE STRINGS —
        /// deliberately NOT derived by calling the table builders (they
        /// construct WPF brushes; the cctor must stay WPF-free, CCTPURITY).
        /// The static battery asserts this list stays in sync with the
        /// tables.</summary>
        private static readonly string[] Ios26GlobalKeyNames =
        {
            "AppBackgroundBrush", "WindowBackgroundBrush", "ModuleBackgroundBrush",
            "SideMenuBackgroundBrush", "ElevatedSurfaceBrush", "PopupSurfaceBrush",
            "DialogSurfaceBrush", "DialogInsetBrush", "SidebarCardBrush",
            "PrimaryForegroundBrush", "SecondaryForegroundBrush",
            "MutedForegroundBrush", "TextPrimaryBrush", "TextSecondaryBrush",
            "DividerBrush", "OutlineBrush", "OutlineDefaultBrush",
            "OutlineStrongBrush", "CardCornerRadius", "CardCornerRadiusLarge",
            "ControlCornerRadius", "Radius.Small", "Radius.Medium",
            "Radius.Large", "Radius.Hero", "ShadowSoft", "ShadowElevated",
            "ShadowDeep", "DashCardShadowEffect", "DashSubtleShadowEffect",
            "DashInteractiveShadowEffect", "GlassShadowPanel", "GlassShadowSoft",
            "GlassShadowSoftButton", "GlassShadowSoftCheckBox",
            "GlassShadowSoftTextBox", "GlassShadowSoftToggle",
            "GlassShadowActive", "GlassShadowActiveButton", "LiquidGlassBrush",
            "LiquidGlassFrostedBrush", "LiquidCard", "LiquidOverlayBrush",
            "ShellLiquidBrush", "ShellLiquidBorderBrush", "ShellSheenBrush",
            "MotionEaseOutKeySpline", "MotionSpringKeySpline",
            "Surface.Primary.BackgroundBrush", "Surface.Secondary.BackgroundBrush",
            "Surface.Detail.BackgroundBrush", "Surface.Primary.BorderBrush",
            "Surface.Secondary.BorderBrush", "Surface.Detail.BorderBrush",
            "Surface.Primary.CornerRadius", "Surface.Secondary.CornerRadius",
            "Surface.Detail.CornerRadius", "LiquidAccentGradient",
            "DashHeroBlueBrush", "DashAmbientBlob1Brush", "DashAmbientBlob2Brush",
            "DashLiquidSurfaceBrush", "DashLiquidBorderBrush",
        };

        /// <summary>Every key the iOS 26 family publishes (About + global).
        /// Part1 iterates this on switch-away; chrome/wallpaper get explicit
        /// fallback SETS (indexer raises ResourcesChanged — Remove() did not
        /// reliably notify, the CrystalWaterDefaultEnabled lesson).</summary>
        internal static readonly string[] Ios26TokenKeys =
            Ios26AboutKeys.Concat(Ios26GlobalKeyNames).Distinct().ToArray();

        // ── APPLY (apply-time only — never the cctor) ─────────────────
        private static void ApplyIos26(ResourceDictionary resources)
        {
            // Unqualified on purpose: ThemeManager lives in
            // ProGlassAutomation.Services (NOT .Services.Theme).
            bool dark = ThemeManager
                .CurrentTheme.ToString().Contains("Dark");

            // Gates FIRST (indexer set → live DynamicResource re-evaluation).
            resources["AboutGlassChromeVisibility"] = Visibility.Visible;

            // ── ABOUT layer ───────────────────────────────────────────
            // Light stage: PLAIN WHITE (owner directive 2026-09-11) with a
            // barely-there neutral sweep; dark keeps the charcoal aurora.
            resources["AboutWallpaperBrush"] = Ios26Lin(dark,
                "#FFFFFF", "#FAFBFD", "#F4F6FA", "#0B0D13", "#141322", "#1D1032");
            resources["AboutGlassSlabBrush"] = Ios26Lin(dark,
                "#F8FFFFFF", "#D2FFFFFF", null, "#33FFFFFF", "#17FFFFFF", null);
            resources["AboutGlassPanelBrush"] = Ios26Lin(dark,
                "#EAFFFFFF", "#C8FFFFFF", null, "#2BFFFFFF", "#12FFFFFF", null);
            resources["AboutGlassCapsuleBrush"] = Ios26Lin(dark,
                "#FBFFFFFF", "#E0FFFFFF", null, "#38FFFFFF", "#1FFFFFFF", null);
            // On white, rims must be quiet ink hairlines (white rims vanish).
            resources["AboutGlassSlabStroke"] = Ios26Sol(dark, "#17000000", "#3DF2F6FF");
            resources["AboutGlassHairlineStroke"] = Ios26Sol(dark, "#0F000000", "#24F2F6FF");
            resources["AboutGlassCaptionBrush"] = Ios26Sol(dark, "#6E6E73", "#AEB7C4");
            resources["AboutGlassGlowBrush"] = Ios26Sol(dark, "#66FFFFFF", "#1AFFFFFF");
            resources["AboutCardCornerRadius"] = IosRadius(20);
            resources["AboutPanelCornerRadius"] = IosRadius(16);
            resources["AboutCardEffect"] = Ios26Shadow(dark, 12, 3, 0.18);

            // ── GLOBAL layer ──────────────────────────────────────────
            var tokens = dark ? Ios26GlobalDark() : Ios26GlobalLight();
            foreach (var kv in tokens)
                resources[kv.Key] = kv.Value;
        }

        // ── GLOBAL TOKEN TABLES (apply-time only) ─────────────────────
        private static Dictionary<string, object> Ios26GlobalLight() =>
            new()
            {
                // PLAIN WHITE stage (owner directive 2026-09-11: "use white
                // background — plain white, enhance more") — vivid icon
                // gradients and pastel ambient blobs glow against pure white.
                ["AppBackgroundBrush"]      = Ios26Sol(false, "#FFFFFF", null),
                ["WindowBackgroundBrush"]   = Ios26Lin2("#FFFFFF", "#FAFAFC", null),
                ["ModuleBackgroundBrush"]   = Ios26Sol(false, "#FFFFFF", null),
                ["SideMenuBackgroundBrush"] = Ios26Sol(false, "#FAFAFC", null),
                ["ElevatedSurfaceBrush"]    = Ios26Sol(false, "#FFFFFF", null),
                ["PopupSurfaceBrush"]       = Ios26Sol(false, "#FFFFFF", null),
                ["DialogSurfaceBrush"]      = Ios26Sol(false, "#FFFFFF", null),
                ["DialogInsetBrush"]        = Ios26Sol(false, "#F2F2F7", null),
                ["SidebarCardBrush"]        = Ios26Lin2("#F4FFFFFF", "#E8FFFFFF", null),

                // Surface.* white family — re-skins every token-driven card,
                // badge and icon tile app-wide (IconBadge*, SurfaceDetail…)
                ["Surface.Primary.BackgroundBrush"]   = Ios26Lin2("#F6FFFFFF", "#E9FFFFFF", null),
                ["Surface.Secondary.BackgroundBrush"] = Ios26Sol(false, "#F7F7FA", null),
                ["Surface.Detail.BackgroundBrush"]    = Ios26Sol(false, "#FFFFFF", null),
                ["Surface.Primary.BorderBrush"]       = Ios26Sol(false, "#14000000", null),
                ["Surface.Secondary.BorderBrush"]     = Ios26Sol(false, "#10000000", null),
                ["Surface.Detail.BorderBrush"]        = Ios26Sol(false, "#10000000", null),
                ["Surface.Primary.CornerRadius"]      = IosRadius(16),
                ["Surface.Secondary.CornerRadius"]    = IosRadius(14),
                ["Surface.Detail.CornerRadius"]       = IosRadius(12),

                // GLOBAL ICON LANGUAGE — fixed vivid iOS app-icon ramp
                // (macOS rule: icons do not follow accent; they glow on white).
                ["LiquidAccentGradient"]   = Ios26Diag3("#0A84FF", "#5E5CE6", "#BF5AF2"),
                ["DashHeroBlueBrush"]      = Ios26Sol(false, "#0A84FF", null),
                ["DashAmbientBlob1Brush"]  = Ios26Sol(false, "#66B7D3FF", null),
                ["DashAmbientBlob2Brush"]  = Ios26Sol(false, "#66E8C8FF", null),
                ["DashLiquidSurfaceBrush"] = Ios26Lin2("#F2FFFFFF", "#E2FFFFFF", null),
                ["DashLiquidBorderBrush"]  = Ios26Sol(false, "#1EFFFFFF", null),

                // Apple ink typography
                ["PrimaryForegroundBrush"]   = Ios26Sol(false, "#1D1D1F", null),
                ["SecondaryForegroundBrush"] = Ios26Sol(false, "#48484D", null),
                ["MutedForegroundBrush"]     = Ios26Sol(false, "#6E6E73", null),
                ["TextPrimaryBrush"]         = Ios26Sol(false, "#1D1D1F", null),
                ["TextSecondaryBrush"]       = Ios26Sol(false, "#5A5A60", null),

                // Hairlines (macOS separators)
                ["DividerBrush"]          = Ios26Sol(false, "#12000000", null),
                ["OutlineBrush"]          = Ios26Sol(false, "#0F000000", null),
                ["OutlineDefaultBrush"]   = Ios26Sol(false, "#14000000", null),
                ["OutlineStrongBrush"]    = Ios26Sol(false, "#24000000", null),

                // Radius scale — iOS continuous-corner language
                ["CardCornerRadius"]      = IosRadius(14),
                ["CardCornerRadiusLarge"] = IosRadius(20),
                ["ControlCornerRadius"]   = IosRadius(10),
                ["Radius.Small"]          = IosRadius(8),
                ["Radius.Medium"]         = IosRadius(12),
                ["Radius.Large"]          = IosRadius(16),
                ["Radius.Hero"]           = IosRadius(22),

                // GLOBAL shadows — small, soft, cheap (perf guard)
                ["ShadowSoft"]                  = Ios26Shadow(false, 10, 2, 0.10),
                ["ShadowElevated"]              = Ios26Shadow(false, 14, 3, 0.14),
                ["ShadowDeep"]                  = Ios26Shadow(false, 14, 4, 0.18),
                ["DashCardShadowEffect"]        = Ios26Shadow(false, 10, 2, 0.10),
                ["DashSubtleShadowEffect"]      = Ios26Shadow(false, 8, 1, 0.08),
                ["DashInteractiveShadowEffect"] = Ios26Shadow(false, 12, 3, 0.16),
                ["GlassShadowPanel"]            = Ios26Shadow(false, 10, 2, 0.10),
                ["GlassShadowSoft"]             = Ios26Shadow(false, 8, 1, 0.08),
                ["GlassShadowSoftButton"]       = Ios26Shadow(false, 8, 1, 0.08),
                ["GlassShadowSoftCheckBox"]     = Ios26Shadow(false, 8, 1, 0.08),
                ["GlassShadowSoftTextBox"]      = Ios26Shadow(false, 8, 1, 0.08),
                ["GlassShadowSoftToggle"]       = Ios26Shadow(false, 8, 1, 0.08),
                ["GlassShadowActive"]           = Ios26Shadow(false, 12, 2, 0.16),
                ["GlassShadowActiveButton"]     = Ios26Shadow(false, 12, 2, 0.16),

                // Liquid/Shell frost vocabulary
                ["LiquidGlassBrush"]        = Ios26Lin2("#E6FFFFFF", "#B8FFFFFF", null),
                ["LiquidGlassFrostedBrush"] = Ios26Lin2("#F2FFFFFF", "#D8FFFFFF", null),
                ["LiquidCard"]              = Ios26Lin2("#F8FFFFFF", "#E8FFFFFF", null),
                ["LiquidOverlayBrush"]      = Ios26Sol(false, "#14000000", null),
                ["ShellLiquidBrush"]        = Ios26Lin2("#EAFFFFFF", "#CCFFFFFF", null),
                ["ShellLiquidBorderBrush"]  = Ios26Sol(false, "#26FFFFFF", null),
                ["ShellSheenBrush"]         = Ios26Lin2("#80FFFFFF", "#00FFFFFF", null),

                // Apple motion curves (strings — theme dicts publish sys:String)
                ["MotionEaseOutKeySpline"] = "0.16,1.0 0.3,1.0",
                ["MotionSpringKeySpline"]  = "0.3,1.2 0.4,1.0",
            };

        private static Dictionary<string, object> Ios26GlobalDark() =>
            new()
            {
                ["AppBackgroundBrush"]      = Ios26Sol(true, "#16171C", null),
                ["WindowBackgroundBrush"]   = Ios26Lin2("#17181D", "#101116", null),
                ["ModuleBackgroundBrush"]   = Ios26Sol(true, "#1B1C22", null),
                ["SideMenuBackgroundBrush"] = Ios26Sol(true, "#131418", null),
                ["ElevatedSurfaceBrush"]    = Ios26Sol(true, "#232429", null),
                ["PopupSurfaceBrush"]       = Ios26Sol(true, "#26272E", null),
                ["DialogSurfaceBrush"]      = Ios26Sol(true, "#26272E", null),
                ["DialogInsetBrush"]        = Ios26Sol(true, "#1E1F25", null),
                ["SidebarCardBrush"]        = Ios26Lin2("#26FFFFFF", "#12FFFFFF", null),

                // Surface.* dark frost (same geometry language)
                ["Surface.Primary.BackgroundBrush"]   = Ios26Lin2("#2EFFFFFF", "#14FFFFFF", null),
                ["Surface.Secondary.BackgroundBrush"] = Ios26Sol(true, "#1FFFFFFF", null),
                ["Surface.Detail.BackgroundBrush"]    = Ios26Sol(true, "#14FFFFFF", null),
                ["Surface.Primary.BorderBrush"]       = Ios26Sol(true, "#2EFFFFFF", null),
                ["Surface.Secondary.BorderBrush"]     = Ios26Sol(true, "#22FFFFFF", null),
                ["Surface.Detail.BorderBrush"]        = Ios26Sol(true, "#1EFFFFFF", null),
                ["Surface.Primary.CornerRadius"]      = IosRadius(16),
                ["Surface.Secondary.CornerRadius"]    = IosRadius(14),
                ["Surface.Detail.CornerRadius"]       = IosRadius(12),

                // icons stay VIVID in dark (iOS rule) — same fixed ramp
                ["LiquidAccentGradient"]   = Ios26Diag3("#0A84FF", "#5E5CE6", "#BF5AF2"),
                ["DashHeroBlueBrush"]      = Ios26Sol(true, "#0A84FF", null),
                ["DashAmbientBlob1Brush"]  = Ios26Sol(true, "#407B5BFF", null),
                ["DashAmbientBlob2Brush"]  = Ios26Sol(true, "#40FF6EC7", null),
                ["DashLiquidSurfaceBrush"] = Ios26Lin2("#24FFFFFF", "#10FFFFFF", null),
                ["DashLiquidBorderBrush"]  = Ios26Sol(true, "#22FFFFFF", null),

                ["PrimaryForegroundBrush"]   = Ios26Sol(true, "#F5F5F7", null),
                ["SecondaryForegroundBrush"] = Ios26Sol(true, "#C7C7CC", null),
                ["MutedForegroundBrush"]     = Ios26Sol(true, "#98989F", null),
                ["TextPrimaryBrush"]         = Ios26Sol(true, "#F5F5F7", null),
                ["TextSecondaryBrush"]       = Ios26Sol(true, "#A9A9B0", null),

                ["DividerBrush"]          = Ios26Sol(true, "#1AFFFFFF", null),
                ["OutlineBrush"]          = Ios26Sol(true, "#14FFFFFF", null),
                ["OutlineDefaultBrush"]   = Ios26Sol(true, "#1AFFFFFF", null),
                ["OutlineStrongBrush"]    = Ios26Sol(true, "#2EFFFFFF", null),

                ["CardCornerRadius"]      = IosRadius(14),
                ["CardCornerRadiusLarge"] = IosRadius(20),
                ["ControlCornerRadius"]   = IosRadius(10),
                ["Radius.Small"]          = IosRadius(8),
                ["Radius.Medium"]         = IosRadius(12),
                ["Radius.Large"]          = IosRadius(16),
                ["Radius.Hero"]           = IosRadius(22),

                ["ShadowSoft"]                  = Ios26Shadow(true, 10, 2, 0.30),
                ["ShadowElevated"]              = Ios26Shadow(true, 14, 3, 0.36),
                ["ShadowDeep"]                  = Ios26Shadow(true, 14, 4, 0.42),
                ["DashCardShadowEffect"]        = Ios26Shadow(true, 10, 2, 0.30),
                ["DashSubtleShadowEffect"]      = Ios26Shadow(true, 8, 1, 0.24),
                ["DashInteractiveShadowEffect"] = Ios26Shadow(true, 12, 3, 0.34),
                ["GlassShadowPanel"]            = Ios26Shadow(true, 10, 2, 0.30),
                ["GlassShadowSoft"]             = Ios26Shadow(true, 8, 1, 0.24),
                ["GlassShadowSoftButton"]       = Ios26Shadow(true, 8, 1, 0.24),
                ["GlassShadowSoftCheckBox"]     = Ios26Shadow(true, 8, 1, 0.24),
                ["GlassShadowSoftTextBox"]      = Ios26Shadow(true, 8, 1, 0.24),
                ["GlassShadowSoftToggle"]       = Ios26Shadow(true, 8, 1, 0.24),
                ["GlassShadowActive"]           = Ios26Shadow(true, 12, 2, 0.36),
                ["GlassShadowActiveButton"]     = Ios26Shadow(true, 12, 2, 0.36),

                ["LiquidGlassBrush"]        = Ios26Lin2("#2EFFFFFF", "#14FFFFFF", null),
                ["LiquidGlassFrostedBrush"] = Ios26Lin2("#33FFFFFF", "#1FFFFFFF", null),
                ["LiquidCard"]              = Ios26Lin2("#2BFFFFFF", "#12FFFFFF", null),
                ["LiquidOverlayBrush"]      = Ios26Sol(true, "#33000000", null),
                ["ShellLiquidBrush"]        = Ios26Lin2("#24FFFFFF", "#10FFFFFF", null),
                ["ShellLiquidBorderBrush"]  = Ios26Sol(true, "#22FFFFFF", null),
                ["ShellSheenBrush"]         = Ios26Lin2("#40FFFFFF", "#00FFFFFF", null),

                ["MotionEaseOutKeySpline"] = "0.16,1.0 0.3,1.0",
                ["MotionSpringKeySpline"]  = "0.3,1.2 0.4,1.0",
            };

        // ── NORMAL RESTORE (live Surface.* aliases) ───────────────────
        /// <summary>Copies the CURRENT Surface.* values into the About glass
        /// keys. Called by Part1 on every family apply BEFORE the switch
        /// (ERR-50472F: no absence window). Indexer sets — live views
        /// re-evaluate immediately.</summary>
        internal static void SeedNeutralAboutTokens(ResourceDictionary r)
        {
            r["AboutGlassSlabBrush"]      = r["Surface.Primary.BackgroundBrush"];
            r["AboutGlassPanelBrush"]     = r["Surface.Secondary.BackgroundBrush"];
            r["AboutGlassSlabStroke"]     = r["Surface.Primary.BorderBrush"];
            r["AboutGlassHairlineStroke"] = r["Surface.Secondary.BorderBrush"];
            r["AboutGlassCapsuleBrush"]   = r["Surface.Secondary.BackgroundBrush"];
            r["AboutGlassCaptionBrush"]   = r["MutedForegroundBrush"];
            r["AboutCardCornerRadius"]    = r["Surface.Primary.CornerRadius"];
            r["AboutPanelCornerRadius"]   = r["Surface.Secondary.CornerRadius"];
        }

        // ── brush/effect helpers (frozen — safe to share) ─────────────
        /// <summary>Dual-variant vertical gradient (About layer: one call
        /// carries both the light and the dark stop set).</summary>
        private static LinearGradientBrush Ios26Lin(bool dark,
            string l0, string l1, string? l2, string d0, string d1, string? d2)
            => dark ? Ios26Lin2(d0, d1, d2) : Ios26Lin2(l0, l1, l2);

        /// <summary>Single-variant vertical gradient. Null-safe: a null
        /// stop string is skipped (ERR-760BA3 — ConvertFromString(null)
        /// returns null and the (Color) unbox throws NRE).</summary>
        private static LinearGradientBrush Ios26Lin2(string c0, string c1, string? c2)
        {
            var b = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            b.GradientStops.Add(new GradientStop(
                (Color)ColorConverter.ConvertFromString(c0), 0));
            b.GradientStops.Add(new GradientStop(
                (Color)ColorConverter.ConvertFromString(c1), c2 == null ? 1 : 0.55));
            if (c2 != null)
                b.GradientStops.Add(new GradientStop(
                    (Color)ColorConverter.ConvertFromString(c2), 1));
            b.Freeze();
            return b;
        }

        /// <summary>Diagonal (0,0 → 1,1) three-stop gradient — the iOS
        /// app-icon ramp orientation.</summary>
        private static LinearGradientBrush Ios26Diag3(string c0, string c1, string c2)
        {
            var b = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(c0), 0));
            b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(c1), 0.5));
            b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(c2), 1));
            b.Freeze();
            return b;
        }

        private static SolidColorBrush Ios26Sol(bool dark, string light, string? darkHex)
        {
            var b = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(dark ? darkHex ?? light : light));
            b.Freeze();
            return b;
        }

        /// <summary>Soft iOS shadow. Opacity is passed EXPLICITLY per table
        /// (no implicit dark multiplier — an earlier heuristic silently
        /// produced 0.66 opacity in dark).</summary>
        private static System.Windows.Media.Effects.DropShadowEffect Ios26Shadow(
            bool dark, double blur, double depth, double opacity)
        {
            var e = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = blur,
                ShadowDepth = depth,
                Direction = 270,
                RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance,
                Opacity = opacity,
                Color = dark ? Colors.Black
                    : (Color)ColorConverter.ConvertFromString("#1E293B")
            };
            e.Freeze();
            return e;
        }
    }
}
