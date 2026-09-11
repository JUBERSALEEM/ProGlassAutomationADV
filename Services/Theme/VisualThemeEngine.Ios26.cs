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
//   1. GLOBAL layer  — the shared token surface every module already
//      consumes dynamically: window/module/side-menu surfaces, Apple ink
//      text colors, hairline outlines, the Radius.*/Card*/Control radius
//      scale, the GLOBAL effect tokens (Shadow*, Dash*, GlassShadow*),
//      the Liquid*/Shell frost vocabulary, dialog/popup surfaces and the
//      Motion* KeySplines (Apple curves). Views re-skin on the switch
//      with zero per-view edits — the owner's "everything dynamic" bet.
//   2. ABOUT layer   — the About redesign specifics (wallpaper, slabs,
//      chrome gate, card shadow) from the 2026-09-10 milestone.
//
// PERF GUARD: every global effect is BlurRadius ≤ 14 at Depth ≤ 3
// (the 2026-08-25 freeze was r28 across 20 About cards). If any machine
// stutters, dial Opacity/BlurRadius here — one file.
//
// NORMAL RESTORE: SeedNeutralAboutTokens copies the CURRENT Surface.*
// values into the About glass keys on every non-iOS26 apply — live
// aliases, so Normal tracks the user's surface material/opacity at all
// times. No static literals, no cross-file StaticResource (App.xaml
// merges theme dicts BEFORE DesignTokens.xaml — forward references
// would crash at load; fixed 2026-09-11 after shipping that bomb in
// c219dc7).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme
{
    public static partial class VisualThemeEngine
    {
        /// <summary>iOS 26 Liquid Glass — the full-application family.</summary>
        public const string Ios26 = "iOS 26";

        /// <summary>Switch-away fallback for the About stage wallpaper
        /// (frozen, shared). Explicit indexer SET — Remove() does not
        /// reliably notify live DynamicResource consumers.</summary>
        internal static readonly SolidColorBrush Ios26NeutralWallpaper = CreateNeutralWallpaper();

        private static SolidColorBrush CreateNeutralWallpaper()
        {
            var b = new SolidColorBrush(Colors.Transparent);
            b.Freeze();
            return b;
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

        /// <summary>Every key the iOS 26 family publishes (About + global).
        /// Part1 iterates this on switch-away; chrome/wallpaper get explicit
        /// fallback SETS (indexer raises ResourcesChanged — Remove() did not
        /// reliably notify, the CrystalWaterDefaultEnabled lesson).</summary>
        internal static readonly string[] Ios26TokenKeys =
            BuildIos26TokenKeys();

        private static string[] BuildIos26TokenKeys()
        {
            var keys = new List<string>(Ios26AboutKeys);
            foreach (var d in new[] { Ios26GlobalLight(), Ios26GlobalDark() })
                foreach (var k in d.Keys)
                    if (!keys.Contains(k))
                        keys.Add(k);
            return keys.ToArray();
        }

        // ── APPLY ─────────────────────────────────────────────────────
        private static void ApplyIos26(ResourceDictionary resources)
        {
            bool dark = ProGlassAutomation.Services.Theme.ThemeManager
                .CurrentTheme.ToString().Contains("Dark");

            // Gates FIRST (indexer set → live DynamicResource re-evaluation).
            resources["AboutGlassChromeVisibility"] = Visibility.Visible;

            // ── ABOUT layer ───────────────────────────────────────────
            resources["AboutWallpaperBrush"] = Ios26Lin(dark,
                "#C7DBFF", "#E4D6FF", "#FFD9E8", "#0B0D13", "#141322", "#1D1032");
            resources["AboutGlassSlabBrush"] = Ios26Lin(dark,
                "#F8FFFFFF", "#D2FFFFFF", null, "#33FFFFFF", "#17FFFFFF", null);
            resources["AboutGlassPanelBrush"] = Ios26Lin(dark,
                "#EAFFFFFF", "#C8FFFFFF", null, "#2BFFFFFF", "#12FFFFFF", null);
            resources["AboutGlassCapsuleBrush"] = Ios26Lin(dark,
                "#FBFFFFFF", "#E0FFFFFF", null, "#38FFFFFF", "#1FFFFFFF", null);
            resources["AboutGlassSlabStroke"] = Ios26Sol(dark, "#EAFFFFFF", "#3DF2F6FF");
            resources["AboutGlassHairlineStroke"] = Ios26Sol(dark, "#B3F0F4FF", "#24F2F6FF");
            resources["AboutGlassCaptionBrush"] = Ios26Sol(dark, "#6E6E73", "#AEB7C4");
            resources["AboutGlassGlowBrush"] = Ios26Sol(dark, "#66FFFFFF", "#1AFFFFFF");
            resources["AboutCardCornerRadius"] = new CornerRadius(20);
            resources["AboutPanelCornerRadius"] = new CornerRadius(16);
            resources["AboutCardEffect"] = Ios26Shadow(dark, 12, 3, 0.18, 0.45);

            // ── GLOBAL layer ──────────────────────────────────────────
            var tokens = dark ? Ios26GlobalDark() : Ios26GlobalLight();
            foreach (var kv in tokens)
                resources[kv.Key] = kv.Value;
        }

        // ── GLOBAL TOKEN TABLE ────────────────────────────────────────
        private static Dictionary<string, object> Ios26GlobalLight() =>
            new()
            {
                // Window + module + chrome surfaces (macOS light gray)
                ["AppBackgroundBrush"]      = Ios26Sol(false, "#F5F5F7", null),
                ["WindowBackgroundBrush"]   = Ios26Lin(false, "#F7F8FB", "#EFF1F6", null, null, null, null),
                ["ModuleBackgroundBrush"]   = Ios26Sol(false, "#F6F7FA", null),
                ["SideMenuBackgroundBrush"] = Ios26Sol(false, "#F0F1F5", null),
                ["ElevatedSurfaceBrush"]    = Ios26Sol(false, "#FFFFFF", null),
                ["PopupSurfaceBrush"]       = Ios26Sol(false, "#FFFFFF", null),
                ["DialogSurfaceBrush"]      = Ios26Sol(false, "#FFFFFF", null),
                ["DialogInsetBrush"]        = Ios26Sol(false, "#E9E9EE", null),
                ["SidebarCardBrush"]        = Ios26Lin(false, "#D8FFFFFF", "#C0FFFFFF", null, null, null, null),

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
                ["CardCornerRadius"]     = new CornerRadius(14),
                ["CardCornerRadiusLarge"] = new CornerRadius(20),
                ["ControlCornerRadius"]  = new CornerRadius(10),
                ["Radius.Small"]         = new CornerRadius(8),
                ["Radius.Medium"]        = new CornerRadius(12),
                ["Radius.Large"]         = new CornerRadius(16),
                ["Radius.Hero"]          = new CornerRadius(22),

                // GLOBAL shadows — small, soft, cheap (perf guard)
                ["ShadowSoft"]                  = Ios26Shadow(false, 10, 2, 0.10, 0.0),
                ["ShadowElevated"]              = Ios26Shadow(false, 14, 3, 0.14, 0.0),
                ["ShadowDeep"]                  = Ios26Shadow(false, 14, 4, 0.18, 0.0),
                ["DashCardShadowEffect"]        = Ios26Shadow(false, 10, 2, 0.10, 0.0),
                ["DashSubtleShadowEffect"]      = Ios26Shadow(false, 8, 1, 0.08, 0.0),
                ["DashInteractiveShadowEffect"] = Ios26Shadow(false, 12, 3, 0.16, 0.0),
                ["GlassShadowPanel"]            = Ios26Shadow(false, 10, 2, 0.10, 0.0),
                ["GlassShadowSoft"]             = Ios26Shadow(false, 8, 1, 0.08, 0.0),
                ["GlassShadowSoftButton"]       = Ios26Shadow(false, 8, 1, 0.08, 0.0),
                ["GlassShadowSoftCheckBox"]     = Ios26Shadow(false, 8, 1, 0.08, 0.0),
                ["GlassShadowSoftTextBox"]      = Ios26Shadow(false, 8, 1, 0.08, 0.0),
                ["GlassShadowSoftToggle"]       = Ios26Shadow(false, 8, 1, 0.08, 0.0),
                ["GlassShadowActive"]           = Ios26Shadow(false, 12, 2, 0.16, 0.0),
                ["GlassShadowActiveButton"]     = Ios26Shadow(false, 12, 2, 0.16, 0.0),

                // Liquid/Shell frost vocabulary
                ["LiquidGlassBrush"]       = Ios26Lin(false, "#E6FFFFFF", "#B8FFFFFF", null, null, null, null),
                ["LiquidGlassFrostedBrush"] = Ios26Lin(false, "#F2FFFFFF", "#D8FFFFFF", null, null, null, null),
                ["LiquidCard"]             = Ios26Lin(false, "#F8FFFFFF", "#E8FFFFFF", null, null, null, null),
                ["LiquidOverlayBrush"]     = Ios26Sol(false, "#14000000", null),
                ["ShellLiquidBrush"]       = Ios26Lin(false, "#EAFFFFFF", "#CCFFFFFF", null, null, null, null),
                ["ShellLiquidBorderBrush"] = Ios26Sol(false, "#26FFFFFF", null),
                ["ShellSheenBrush"]        = Ios26Lin(false, "#80FFFFFF", "#00FFFFFF", null, null, null, null),

                // Apple motion curves (strings — theme dicts publish sys:String)
                ["MotionEaseOutKeySpline"]  = "0.16,1.0 0.3,1.0",
                ["MotionSpringKeySpline"]   = "0.3,1.2 0.4,1.0",
            };

        private static Dictionary<string, object> Ios26GlobalDark() =>
            new()
            {
                ["AppBackgroundBrush"]      = Ios26Sol(true, "#16171C", null),
                ["WindowBackgroundBrush"]   = Ios26Lin(true, "#17181D", "#101116", null, null, null, null),
                ["ModuleBackgroundBrush"]   = Ios26Sol(true, "#1B1C22", null),
                ["SideMenuBackgroundBrush"] = Ios26Sol(true, "#131418", null),
                ["ElevatedSurfaceBrush"]    = Ios26Sol(true, "#232429", null),
                ["PopupSurfaceBrush"]       = Ios26Sol(true, "#26272E", null),
                ["DialogSurfaceBrush"]      = Ios26Sol(true, "#26272E", null),
                ["DialogInsetBrush"]        = Ios26Sol(true, "#1E1F25", null),
                ["SidebarCardBrush"]        = Ios26Lin(true, "#26FFFFFF", "#12FFFFFF", null, null, null, null),

                ["PrimaryForegroundBrush"]   = Ios26Sol(true, "#F5F5F7", null),
                ["SecondaryForegroundBrush"] = Ios26Sol(true, "#C7C7CC", null),
                ["MutedForegroundBrush"]     = Ios26Sol(true, "#98989F", null),
                ["TextPrimaryBrush"]         = Ios26Sol(true, "#F5F5F7", null),
                ["TextSecondaryBrush"]       = Ios26Sol(true, "#A9A9B0", null),

                ["DividerBrush"]          = Ios26Sol(true, "#1AFFFFFF", null),
                ["OutlineBrush"]          = Ios26Sol(true, "#14FFFFFF", null),
                ["OutlineDefaultBrush"]   = Ios26Sol(true, "#1AFFFFFF", null),
                ["OutlineStrongBrush"]    = Ios26Sol(true, "#2EFFFFFF", null),

                ["CardCornerRadius"]      = new CornerRadius(14),
                ["CardCornerRadiusLarge"] = new CornerRadius(20),
                ["ControlCornerRadius"]   = new CornerRadius(10),
                ["Radius.Small"]          = new CornerRadius(8),
                ["Radius.Medium"]         = new CornerRadius(12),
                ["Radius.Large"]          = new CornerRadius(16),
                ["Radius.Hero"]           = new CornerRadius(22),

                ["ShadowSoft"]                  = Ios26Shadow(true, 10, 2, 0.30, 0.0),
                ["ShadowElevated"]              = Ios26Shadow(true, 14, 3, 0.36, 0.0),
                ["ShadowDeep"]                  = Ios26Shadow(true, 14, 4, 0.42, 0.0),
                ["DashCardShadowEffect"]        = Ios26Shadow(true, 10, 2, 0.30, 0.0),
                ["DashSubtleShadowEffect"]      = Ios26Shadow(true, 8, 1, 0.24, 0.0),
                ["DashInteractiveShadowEffect"] = Ios26Shadow(true, 12, 3, 0.34, 0.0),
                ["GlassShadowPanel"]            = Ios26Shadow(true, 10, 2, 0.30, 0.0),
                ["GlassShadowSoft"]             = Ios26Shadow(true, 8, 1, 0.24, 0.0),
                ["GlassShadowSoftButton"]       = Ios26Shadow(true, 8, 1, 0.24, 0.0),
                ["GlassShadowSoftCheckBox"]     = Ios26Shadow(true, 8, 1, 0.24, 0.0),
                ["GlassShadowSoftTextBox"]      = Ios26Shadow(true, 8, 1, 0.24, 0.0),
                ["GlassShadowSoftToggle"]       = Ios26Shadow(true, 8, 1, 0.24, 0.0),
                ["GlassShadowActive"]           = Ios26Shadow(true, 12, 2, 0.36, 0.0),
                ["GlassShadowActiveButton"]     = Ios26Shadow(true, 12, 2, 0.36, 0.0),

                ["LiquidGlassBrush"]       = Ios26Lin(true, "#2EFFFFFF", "#14FFFFFF", null, null, null, null),
                ["LiquidGlassFrostedBrush"] = Ios26Lin(true, "#33FFFFFF", "#1FFFFFFF", null, null, null, null),
                ["LiquidCard"]             = Ios26Lin(true, "#2BFFFFFF", "#12FFFFFF", null, null, null, null),
                ["LiquidOverlayBrush"]     = Ios26Sol(true, "#33000000", null),
                ["ShellLiquidBrush"]       = Ios26Lin(true, "#24FFFFFF", "#10FFFFFF", null, null, null, null),
                ["ShellLiquidBorderBrush"] = Ios26Sol(true, "#22FFFFFF", null),
                ["ShellSheenBrush"]        = Ios26Lin(true, "#40FFFFFF", "#00FFFFFF", null, null, null, null),

                ["MotionEaseOutKeySpline"]  = "0.16,1.0 0.3,1.0",
                ["MotionSpringKeySpline"]   = "0.3,1.2 0.4,1.0",
            };

        // ── NORMAL RESTORE (live Surface.* aliases) ───────────────────
        /// <summary>Copies the CURRENT Surface.* values into the About glass
        /// keys. Called on every non-iOS26 family apply (Part1). Indexer
        /// sets — live views re-evaluate immediately. No literals: Normal
        /// keeps tracking the user's surface material/opacity choices.</summary>
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
        private static LinearGradientBrush Ios26Lin(bool dark,
            string l0, string l1, string? l2, string d0, string d1, string? d2)
        {
            string c0 = dark ? d0 : l0, c1 = dark ? d1 : l1, c2 = dark ? d2 : l2;
            var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(c0), 0));
            b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(c1), c2 == null ? 1 : 0.55));
            if (c2 != null)
                b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(c2), 1));
            b.Freeze();
            return b;
        }

        private static SolidColorBrush Ios26Sol(bool dark, string light, string? darkHex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? darkHex ?? light : light));
            b.Freeze();
            return b;
        }

        private static System.Windows.Media.Effects.DropShadowEffect Ios26Shadow(
            bool dark, double blur, double depth, double opLight, double opDark)
        {
            var e = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = blur,
                ShadowDepth = depth,
                Direction = 270,
                Opacity = dark ? (opDark > 0 ? opDark : opLight * 2.2) : opLight,
                Color = dark ? Colors.Black
                    : (Color)ColorConverter.ConvertFromString("#1E293B")
            };
            e.Freeze();
            return e;
        }
    }
}
