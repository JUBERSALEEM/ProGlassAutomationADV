// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Eight/EightThemeScenes.cs
// PROJECT: ProGlassAutomation
// MODULE: Eight-theme engine — code-drawn built-in scenes
//
// WHY CODE-DRAWN AND NOT BUNDLED IMAGES
// ───────────────────────────────────────────────────────────────────────
// Fifty binary wallpapers would add hundreds of megabytes to the installer
// and would go stale the first time a family changed. A multi-stop
// LinearGradientBrush costs a few hundred bytes, is crisp at 4K and
// beyond, and — crucially — flows through the ENTIRE theme engine:
// backdrop visibility, colour richness and opacity scaling all apply to
// it exactly as they would to a downloaded image.
//
// A SCENE IS PART OF THE MATERIAL, NOT DECORATION. Glassmorphism without
// a busy backdrop is a grey smear; neumorphism with a photo behind it has
// no extrude at all. Every scene below is chosen to make its own family's
// physics legible, and the families that need a controlled ground are
// given an almost featureless one.
//
// They are LinearGradientBrushes on purpose: anything more exotic
// (DrawingBrush, Path geometry) is expensive to rasterise per repaint and
// this file runs on the UI thread during a family switch.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme.Eight
{
    /// <summary>One code-drawn backdrop per family.</summary>
    public static class EightThemeScenes
    {
        /// <summary>UI catalogue: (identity, label, type caption) for the
        /// Theme Center's scene row. Labels differ from family display names
        /// so a tile never reads "Bento Grid — Bento Grid".</summary>
        public static readonly (string Identity, string Label, string Type)[] All =
        {
            (EightThemeNames.BentoGrid,      "Cell Tray",       "Compartment field wallpaper"),
            (EightThemeNames.Spatial,       "Depth Field",     "Volumetric depth wallpaper"),
            (EightThemeNames.Swiss,         "Column Grid",     "Ruled paper wallpaper"),
            (EightThemeNames.Holographic,   "Foil Aurora",     "Iridescent spectrum wallpaper"),
            (EightThemeNames.Neumorphism,   "Soft Sheet",      "Flat mid-tone wallpaper"),
            (EightThemeNames.Claymorphism,  "Pastel Studio",   "Tinted pastel wallpaper"),
            (EightThemeNames.LiquidGlass,   "Refracted Field", "Luminous refraction wallpaper"),
            (EightThemeNames.Glassmorphism, "Prism Wash",      "Chromatic gradient wallpaper"),
        };

        /// <summary>Draws the scene for a family. Never throws — a
        /// wallpaper must never take the application down, and a family's
        /// scene falling back to the default is a cosmetic failure, not a
        /// fault worth surfacing.</summary>
        public static LinearGradientBrush For(string? identity, bool dark)
        {
            try
            {
                return identity switch
                {
                    EightThemeNames.BentoGrid => BentoTray(dark),
                    EightThemeNames.Spatial => DepthField(dark),
                    EightThemeNames.Swiss => ColumnGrid(dark),
                    EightThemeNames.Holographic => FoilAurora(dark),
                    EightThemeNames.Neumorphism => SoftSheet(dark),
                    EightThemeNames.Claymorphism => PastelStudio(dark),
                    EightThemeNames.LiquidGlass => RefractedField(dark),
                    EightThemeNames.Glassmorphism => PrismWash(dark),
                    _ => BentoTray(dark),
                };
            }
            catch (Exception ex)
            {
                ProGlassAutomation.Services.Logging.AppLogService.Debug(
                    $"Eight-theme scene fell back for '{identity}': {ex.Message}");
                return BentoTray(dark);
            }
        }

        // ── 1 · BENTO GRID · Cell Tray ──────────────────────────────
        // Nearly flat. A bento tray is ONE object and the cells must not
        // compete with a busy backdrop — the only structure the scene is
        // allowed to imply is the orthogonal banding that echoes the grid.
        private static LinearGradientBrush BentoTray(bool dark) => dark
            ? Diagonal("#101014", "#15151A", "#0C0C10", "#18181E", "#0A0A0E")
            : Diagonal("#FFFFFF", "#FAFAFA", "#F2F3F5", "#FAFAFA", "#FFFFFF");

        // ── 2 · SPATIAL · Depth Field ───────────────────────────────
        // A cool light source in the upper-left falling to a deep indigo
        // floor, with two wide soft blooms placed at different offsets so
        // the blur has real parallax to work against. Symmetric light
        // would flatten the very depth hierarchy the family encodes.
        private static LinearGradientBrush DepthField(bool dark) => dark
            ? Diagonal("#12172C", "#0C1020", "#080A14", "#141A33", "#05070E")
            : Diagonal("#EAF0FF", "#DCE6FA", "#CBD9F2", "#E3EAF8", "#C9D6EE");

        // ── 3 · SWISS · Column Grid ─────────────────────────────────
        // Paper, and almost nothing else. A 45° sweep from pure white to
        // the faintest warm grey — the gradient exists only to stop the
        // page reading as a dead flat bitmap, and it is the ONLY gradient
        // this family is allowed anywhere.
        private static LinearGradientBrush ColumnGrid(bool dark) => dark
            ? Diagonal("#0D0D0D", "#000000", "#141414", "#000000", "#0A0A0A")
            : Diagonal("#FFFFFF", "#FAFAFA", "#FFFFFF", "#F4F4F4", "#FFFFFF");

        // ── 4 · HOLOGRAPHIC · Foil Aurora ──────────────────────────
        // NON-UNIFORM spectral banding, which is the researched rule. Real
        // foil clusters bright around blue→cyan and goes dark around
        // red→orange; a evenly-spaced rainbow stops reading as a material
        // and starts reading as a gradient. The stops below are
        // deliberately bunched at cyan and spread at orange.
        private static LinearGradientBrush FoilAurora(bool dark) => dark
            ? Diagonal(
                "#140B2B", "#2A0F3F", "#0B2A45", "#06333F", "#123A2C", "#2B1038", "#0A0A18")
            : Diagonal(
                "#EDE6FF", "#F2E4FF", "#E4F2FF", "#E0FFF6", "#FFF3E0", "#FFE8F6", "#F2ECFF");

        // ── 5 · NEUMORPHISM · Soft Sheet ────────────────────────────
        // Deliberately FEATURELESS. The style's defining rule is that a
        // surface's colour must equal its page's, because the light shadow
        // needs a surface darker than itself to read against. Put a
        // gradient behind a neumorphic card and the extrude dies. This is
        // one of only two scenes in the catalogue that is effectively a
        // single flat colour.
        private static LinearGradientBrush SoftSheet(bool dark) => dark
            ? Diagonal("#2A2D32", "#2A2D32", "#2E3136", "#2A2D32", "#2A2D32")
            : Diagonal("#E0E5EC", "#E0E5EC", "#E3E8EF", "#E0E5EC", "#E0E5EC");

        // ── 6 · CLAYMORPHISM · Pastel Studio ────────────────────────
        // A warm tinted field, explicitly NOT pure white. A white page
        // erases the soft coloured glow the three-shadow recipe depends
        // on, and the clay surfaces then look pasted onto a void.
        private static LinearGradientBrush PastelStudio(bool dark) => dark
            ? Diagonal("#221B3A", "#2A2048", "#1C1633", "#302552", "#191430")
            : Diagonal("#F8F3FD", "#F1EAFB", "#EAE1F8", "#F4ECFB", "#EFE6F9");

        // ── 7 · LIQUID GLASS · Refracted Field ─────────────────────
        // The most chromatic ground in the catalogue on purpose. Liquid
        // Glass bends and blurs what is behind it, so the material only
        // looks like a material when there is strong colour and light
        // structure to bend. A grey stage produces a grey card.
        private static LinearGradientBrush RefractedField(bool dark) => dark
            ? Diagonal(
                "#0B1A3A", "#12255C", "#0A2C4A", "#0E2350",
                "#1A0F3A", "#0A1636", "#071023")
            : Diagonal(
                "#D8E6FF", "#CFE0FF", "#E3D6FF", "#D4F0FF",
                "#FFE8D6", "#E6E0FF", "#DCEBFF");

        // ── 8 · GLASSMORPHISM · Prism Wash ─────────────────────────
        // The other chromatic ground. Glassmorphism's uniform blur only
        // earns its cost over high-chroma content; against flat paper it
        // is a grey wash. This scene is what makes the family work, and
        // switching it off is why users report "the glass cards just look
        // grey".
        private static LinearGradientBrush PrismWash(bool dark) => dark
            ? Diagonal(
                "#1B0F3D", "#0E2450", "#062F3D", "#2A0F3D",
                "#0F1D42", "#3A0F33", "#0A0A1E")
            : Diagonal(
                "#F0E4FF", "#DFE8FF", "#D8FFF2", "#FFE6F4",
                "#FFF0DA", "#E6E0FF", "#F6ECFF");

        // ── KIT ─────────────────────────────────────────────────────

        private static LinearGradientBrush Diagonal(params string[] stops) =>
            Make(new Point(0, 0), new Point(1, 1), stops);

        private static LinearGradientBrush Make(
            Point p0, Point p1, params string[] stops)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = p0,
                EndPoint = p1,
            };
            for (int i = 0; i < stops.Length; i++)
            {
                // Even distribution. Irregular offsets are deliberately NOT
                // used: they read as banding artefacts, and the research is
                // explicit that the banding has to come from the HUES being
                // unevenly distributed, not from the stop positions.
                double offset = stops.Length <= 1
                    ? 0.0
                    : (double)i / (stops.Length - 1);
                brush.GradientStops.Add(new GradientStop(
                    (Color)ColorConverter.ConvertFromString(stops[i]), offset));
            }
            if (brush.CanFreeze) brush.Freeze();
            return brush;
        }
    }
}