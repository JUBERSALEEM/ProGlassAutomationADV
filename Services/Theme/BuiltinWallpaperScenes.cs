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
//   Normal           → Studio      — quiet sweep following the base theme
//   Normal (Industrial) → Studio   — same quiet sweep on the white/blue stage
//   Monet            → Golden Hour — dusk derived from the accent hue
//   Crystal Fluid    → Crystal Fluid — pale luminous water atmosphere (§22)
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme
{
    /// <summary>Code-drawn, accent-aware wallpaper scenes — one per
    /// visual family. Zero assets, razor-sharp at any DPI, and fully
    /// transformable by the theme engine (plain gradient brushes).</summary>
    public static class BuiltinWallpaperScenes
    {
        /// <summary>UI catalogue: key (family name) + display label.</summary>
        public static readonly (string Key, string Label)[] All =
        {
            (VisualThemeEngine.Normal,           "Studio"),
            (VisualThemeEngine.NormalIndustrial, "Studio"),
            (VisualThemeEngine.Monet,            "Golden Hour"),
            // Ios-17pm: authored display name for the Crystal Fluid family
            // (identity const unchanged — labels are view-only).
            (VisualThemeEngine.CrystalFluid,
                VisualThemeEngine.FamilyDisplayName(
                    VisualThemeEngine.CrystalFluid)),
        };

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
                    ? VisualThemeEngine.Normal
                    : family!;

                if (f == VisualThemeEngine.CrystalFluid) return CrystalFluidScene(dark);
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
            catch { /* wallpaper must never take the app down */ }
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

        /// <summary>Normal — quiet studio sweep on the base stage.</summary>
        private static LinearGradientBrush Studio(bool dark)
        {
            return dark
                ? Diagonal(
                    (Parse("#0A1226"), 0.0), (Parse("#101E3A"), 0.5), (Parse("#12294E"), 1.0))
                : Diagonal(
                    (Parse("#EDE7DA"), 0.0), (Parse("#F6F1E6"), 0.55), (Parse("#FDFAF3"), 1.0));
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
