// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/BuiltinWallpaperScenes.cs
// PROJECT: ProGlassAutomation
// MODULE: Wallpaper system — code-drawn 4K+ scenes (2026-09-06;
//         2026-09-07: Normal (Industrial) joins the Normal Studio scene)
//
// DESIGN TRUTH (senior answer to "bundle 50 wallpapers"):
//   Shipping 50 binary wallpapers would bloat the installer by hundreds
//   of MB and they would go stale. Instead:
//     • THREE scenes are DRAWN IN CODE — one per visual family — as pure
//       multi-stop LinearGradientBrushes. They are resolution-independent
//       ("4K and beyond", always crisp) AND — because they are plain
//       gradient brushes — they flow through the ENTIRE theme engine:
//       backdrop visibility, colour richness (saturation of stops), and
//       opacity scaling all apply to them exactly like any downloaded
//       wallpaper scene.
//     • Scenes derive their palette from the ACTIVE accent + Light/Dark
//       base, so the "theme-based wallpaper" literally re-renders when
//       the theme or accent changes (Monet's Golden Hour is 100%
//       accent-derived — that family IS the wallpaper).
//     • Millions of real 4K wallpapers come from the network provider
//       (WallpaperProviderService) and are cached locally.
//
//   Painted behind modules through the engine's existing
//   "ModuleWallpaperBrush" key — zero XAML changes required.
//
// SCENE SIGNATURES (family → mood) — families shipped (2026-09-06;
//  2026-09-07 adds Normal (Industrial) to the Studio scene):
//   Normal (Industrial) → Studio         — cream SaaS sweep (25 August)
//   Monet               → Golden Hour    — dusk derived from the accent hue
//   Crystal Fluid       → Luminous Water — cyan water atmosphere
//   iOS 26              → Grouped Glass  — Apple grouped-grey liquid glass
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme
{
    /// <summary>Code-drawn, accent-aware wallpaper scenes — one per
    /// visual family. Zero assets, razor-sharp at any DPI, and fully
    /// transformable by the theme engine (plain gradient brushes).</summary>
    public static class BuiltinWallpaperScenes
    {
        /// <summary>UI catalogue: key (family name) + scene label. The
        /// scene labels differ from the family display names so the theme
        /// picker never reads "X — X".</summary>
        public static readonly (string Key, string Label)[] All =
        {
            // Industrial theme pattern wallpaper — warm paper with an
            // indigo structural wash. Not the family display name ("Normal").
            (VisualThemeEngine.NormalIndustrial, "Industrial Pattern"),
            (VisualThemeEngine.Monet,            "Golden Hour"),
            // iPhone 17 Pro Max (Crystal Fluid) — cyan water atmosphere.
            (VisualThemeEngine.CrystalFluid,     "Luminous Water"),
            // iOS 26 — Apple grouped-grey liquid glass. MUST NOT share
            // Crystal's scene: that was why iOS 26 and iPhone 17 Pro Max
            // painted identically under ThemeLinked wallpaper.
            (VisualThemeEngine.Ios26,            "Grouped Glass"),
        };

        /// <summary>The eight-family scene catalogue. Kept SEPARATE from
        /// <see cref="All"/> rather than merged into it, because a family
        /// that declares <c>Material.AllowBackdrop == false</c> (swiss,
        /// neumorphism) must never offer a scene tile in the first place.
        /// Offering one and then silently not painting it is worse than
        /// not offering it — the user picks a wallpaper and nothing
        /// happens.</summary>
        public static readonly (string Key, string Label, string Type)[] EightFamilies =
            ProGlassAutomation.Services.Theme.Eight.EightThemeScenes.All;

        /// <summary>True when this key is one of the eight families and that
        /// family permits a backdrop layer.</summary>
        public static bool AllowsBackdropFor(string? key)
        {
            var profile =
                ProGlassAutomation.Services.Theme.Eight.EightThemeCatalog.Resolve(key);
            return ProGlassAutomation.Services.Theme.Eight
                .EightThemeCatalog.IsKnown(key)
                && profile.Material.AllowBackdrop;
        }

        /// <summary>Draws the scene for a family. Unknown/empty family →
        /// the Normal studio sweep (the default family). NEVER throws past
        /// its own guard — a wallpaper must never take the app down.</summary>
        public static LinearGradientBrush For(string? family)
        {
            try
            {
                var accent = ThemeManager.AccentColor;
                bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
                var (h, s, _) = ColorMath.ToHsl(accent);

                string f = string.IsNullOrWhiteSpace(family)
                    ? VisualThemeEngine.NormalIndustrial
                    : family!;

                // ── EIGHT-FAMILY CONTRACT ────────────────────────────
                // Checked FIRST, and deliberately before the legacy
                // families: the eight are the product surface and each
                // has its own ground. Returning a legacy scene here is the
                // bug where Liquid Glass and Bento Grid painted the same
                // warm cream wash and users reported "the new themes all
                // look the same".
                if (Eight.EightThemeCatalog.IsKnown(f))
                    return Eight.EightThemeScenes.For(
                        Eight.EightThemeCatalog.Resolve(f).Identity, dark);

                if (f == VisualThemeEngine.CrystalFluid)
                    return CrystalFluidScene(dark);
                if (f == VisualThemeEngine.Ios26)
                    return Ios26Scene(dark);
                if (f == VisualThemeEngine.Monet)            return GoldenHour(h, s, dark);
                return Studio(dark);
            }
            catch
            {
                return Diagonal((Parse("#0A1A44"), 0.0), (Parse("#04091F"), 1.0));
            }
        }

        // ── Scene painters ────────────────────────────────────────────

        /// <summary>Crystal Fluid (2026-09-05, replaces "Crystal Sea") —
        /// the §22 environment: white light upper-left, soft cyan
        /// atmosphere drifting to the lower-right, pale blue base. The
        /// dark stops are a defensive fallback only — the family pairs
        /// Light via ModePairFor.</summary>
        private static LinearGradientBrush CrystalFluidScene(bool dark)
        {
            return Diagonal(
                (SceneToken("CrystalSceneTopBrush",
                    Parse(dark ? "#0E2230" : "#FDFEFF")), 0.00),
                (SceneToken("CrystalSceneLitBrush",
                    Parse(dark ? "#0C1F2C" : "#F2FAFE")), 0.28),
                (SceneToken("CrystalSceneAtmosphereBrush",
                    Parse(dark ? "#0A1B28" : "#E6F6FD")), 0.55),
                (SceneToken("CrystalSceneBaseBrush",
                    Parse(dark ? "#081722" : "#D5F0FB")), 0.80),
                (SceneToken("CrystalSceneFloorBrush",
                    Parse(dark ? "#061320" : "#DFF5FF")), 1.00));
        }

        /// <summary>Ios-17pm P4a — the crystal scene's light-mode stops
        /// are single-home tokens in CrystalFluidBrushes.xaml; the dark
        /// stops stay defensive literals (the family pairs Light via
        /// ModePairFor). The literal is a byte-exact fallback so the
        /// scene renders identically in hosts without merged resources;
        /// the try/catch mirrors the file's own never-throw contract.
        /// </summary>
        private static System.Windows.Media.Color SceneToken(
            string key, System.Windows.Media.Color fallback)
        {
            try
            {
                var resources = System.Windows.Application.Current?.Resources;
                if (resources?.Contains(key) == true &&
                    resources[key] is System.Windows.Media.SolidColorBrush b)
                    return b.Color;
            }
            catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("CrystalFluidScene suppressed");
            }
            return fallback;
        }

        /// <summary>Monet — Material-You: the whole dusk derives from the
        /// live accent hue; it re-paints on every accent change.</summary>
        private static LinearGradientBrush GoldenHour(double h, double s, bool dark)
        {
            s = Math.Clamp(s, 0.35, 0.80);
            return Diagonal(
                (ColorMath.FromHsl(h, s * 0.90, dark ? 0.10 : 0.92), 0.00),
                (ColorMath.FromHsl(h + 22, s * 0.85, dark ? 0.16 : 0.82), 0.40),
                (ColorMath.FromHsl(h + 44, s * 0.80, dark ? 0.22 : 0.72), 0.62),
                (ColorMath.FromHsl(h + 10, s * 0.85, dark ? 0.12 : 0.80), 0.82),
                (ColorMath.FromHsl(h + 44, s * 0.75, dark ? 0.07 : 0.68), 1.00));
        }

        /// <summary>iOS 26 — Apple grouped-grey liquid glass stage
        /// (#F2F2F7 → #E5E5EA with a quiet system-blue kiss). Distinct
        /// from Crystal's cyan water and from Normal's warm cream.</summary>
        private static LinearGradientBrush Ios26Scene(bool dark)
        {
            return dark
                ? Diagonal(
                    (Parse("#17181D"), 0.00),
                    (Parse("#101116"), 0.35),
                    (Parse("#1B1C22"), 0.62),
                    (Parse("#232429"), 0.82),
                    (Parse("#0A1A33"), 1.00))
                : Diagonal(
                    (Parse("#F2F2F7"), 0.00),
                    (Parse("#EDEDF2"), 0.28),
                    (Parse("#E5E5EA"), 0.55),
                    (Parse("#DCDCE3"), 0.78),
                    (Parse("#C5D8F5"), 1.00));
        }

        /// <summary>Industrial theme pattern wallpaper. Warm paper field
        /// with an indigo structural wash — the same indigo the studio
        /// rail and cards use — so a theme-linked Industrial stage reads
        /// as the industrial pattern, not a plain cream fill.</summary>
        private static LinearGradientBrush Studio(bool dark)
        {
            return dark
                ? Diagonal(
                    (Parse("#161022"), 0.00),
                    (Parse("#241C40"), 0.28),
                    (Parse("#2C2440"), 0.55),
                    (Parse("#3D2E6B"), 0.78),
                    (Parse("#1A1430"), 1.00))
                : Diagonal(
                    (Parse("#FBF8F1"), 0.00),
                    (Parse("#F3EFE6"), 0.22),
                    (Parse("#EEE8F8"), 0.48),
                    (Parse("#E4DCF3"), 0.72),
                    (Parse("#F6F1E6"), 1.00));
        }

        /// <summary>Wallpaper-type caption for a built-in scene. Industrial
        /// is the pattern wallpaper; the other families keep their own type
        /// so two tiles never read as the same scene.</summary>
        public static string WallpaperType(string? key)
        {
            // Eight families carry their own caption, so a Bento Grid tile
            // never reads "Industrial pattern wallpaper".
            if (Eight.EightThemeCatalog.IsKnown(key))
            {
                var scene = Eight.EightThemeScenes.All.FirstOrDefault(
                    s => string.Equals(s.Identity, key,
                        StringComparison.OrdinalIgnoreCase));
                return scene.Type ?? "";
            }

            if (key == VisualThemeEngine.Monet) return "Accent dusk wallpaper";
            if (key == VisualThemeEngine.CrystalFluid) return "Water atmosphere wallpaper";
            if (key == VisualThemeEngine.Ios26) return "Grouped glass wallpaper";
            return "Industrial pattern wallpaper";
        }

        // ── Kit ───────────────────────────────────────────────────────

        private static LinearGradientBrush Diagonal(
            params (Color Color, double Offset)[] stops) =>
            Make(new Point(0, 0), new Point(1, 1), stops);

        private static LinearGradientBrush Make(
            Point p0, Point p1, params (Color Color, double Offset)[] stops)
        {
            var b = new LinearGradientBrush { StartPoint = p0, EndPoint = p1 };
            foreach (var (c, off) in stops)
                b.GradientStops.Add(new GradientStop(c, off));
            return b;
        }

        private static Color Parse(string hex) =>
            (Color)ColorConverter.ConvertFromString(hex);
    }
}
