// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/VisualThemeEngine.Eight.cs
// PROJECT: ProGlassAutomation
// MODULE: Dynamic Theme Engine — the eight-family dispatch
//
// This partial is the BRIDGE between the eight-theme engine and the
// engine that already existed. It exists as a separate file, rather than
// as edits inside VisualThemeEngine.Part1.cs, for one reason: the switch
// statements in Part1 are the single most delicate code in the theme
// subsystem. They run between ClearOverrides (which tears the managed
// resource set down) and the wallpaper layer (which reads it back), and
// every edit into them is a chance to open the "key absent mid-switch"
// window that crashed a customer's app twice already (ERR-50472F,
// DashAmbientBrush).
//
// Everything this partial does is therefore ADDITIVE: it adds a painter,
// adds the cleanup that runs before the next family paints, and routes
// dispatch through it. It removes nothing.
//
// THE CONTRACT
// ───────────────────────────────────────────────────────────────────────
//  · The eight families are the PRODUCT contract. Settings offers eight
//    cards; the eight strings are what persist.
//  · Legacy identities are translated on READ by EightThemeMigration,
//    once, and the translation is idempotent (a value that is already one
//    of the eight is returned untouched) so a migrated config can never
//    drift a step per launch.
//  · The pre-eight painters (ApplyMonet, ApplyCrystalFluid,
//    ApplyIndustrial, ApplyIos26) are left in place and still compile and
//    still pass their own tests. They are simply no longer what a family
//    switch routes to. Deleting them would be a much larger change with
//    no product benefit and real regression risk, and the eight-family
//    contract is expressed by the new dispatch, not by deleting code.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Theme.Eight;
using System.Windows.Media;
using System.Windows;
using System;

namespace ProGlassAutomation.Services.Theme
{
    public static partial class VisualThemeEngine
    {
        /// <summary>True when the active family is one of the eight.
        /// Every wallpaper/scene decision in the engine branches on this,
        /// so it is a single predicate rather than eight comparisons
        /// scattered through the paint path.</summary>
        public static bool IsEightThemeFamily =>
            EightThemeCatalog.IsKnown(Current);

        /// <summary>The resolved eight-theme recipe for the active family.
        /// Returns null when the active family predates the contract, so
        /// callers can fall back without a try/catch.</summary>
        public static EightThemeProfile? ActiveProfile =>
            IsEightThemeFamily ? EightThemeCatalog.Resolve(Current) : null;

        // ── PAINT ───────────────────────────────────────────────────
        /// <summary>Paints one of the eight families. Never throws — the
        /// caller is already inside a dispatcher turn, and an exception
        /// escaping here would strand the resource dictionary half
        /// rebuilt.</summary>
        private static void ApplyEightTheme(ResourceDictionary resources,
            string identity)
        {
            try
            {
                var profile = EightThemeCatalog.Resolve(identity);
                bool dark = ThemeManager.CurrentTheme
                    .ToString().Contains("Dark",
                        StringComparison.OrdinalIgnoreCase);
                Color accent = ThemeManager.AccentColor;

                // Gate every DynamicResource consumer BEFORE any value is
                // written, exactly as ApplyIos26 does. The indexer raises
                // ResourcesChanged, so a live reference re-evaluates on the
                // write rather than waiting for the next layout pass.
                resources["CrystalWaterDefaultEnabled"] = false;
                resources["AboutGlassChromeVisibility"] = Visibility.Visible;

                EightThemePainter.Apply(resources, profile, dark, accent);
                SeedEightAboutTokens(resources, profile, dark);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Eight-family paint failed for '{identity}': {ex.Message}",
                    new LogContext
                    {
                        Module = "Themes",
                        Operation = "ApplyEightTheme",
                    });
            }
        }

        // ── ABOUT LAYER ─────────────────────────────────────────────
        // The About page runs its own surface vocabulary (slab / panel /
        // capsule / hairline / glow) rather than the global Surface.* keys.
        // Without this the About page keeps the previous family's glass
        // chrome and reads as a second, unrelated application.
        private static void SeedEightAboutTokens(
            ResourceDictionary resources, EightThemeProfile profile, bool dark)
        {
            try
            {
                var ramp = profile.Ramp(dark);
                var mat = profile.Material;

                resources["AboutWallpaperBrush"] = EightThemeScenes.For(
                    profile.Identity, dark);

                var slab = SurfaceFill(
                    ramp.Surface, mat.Kind);
                var panel = SurfaceFill(
                    ramp.SurfaceSecondary, mat.Kind);
                var capsule = SurfaceFill(
                    ramp.SurfaceDetail, mat.Kind);

                resources["AboutGlassSlabBrush"] =
                    EightThemePainter.FrozenSolid(slab);
                resources["AboutGlassPanelBrush"] =
                    EightThemePainter.FrozenSolid(panel);
                resources["AboutGlassCapsuleBrush"] =
                    EightThemePainter.FrozenSolid(capsule);

                // On a light ramp a white rim is invisible, so the rim
                // falls back to the ramp's own ink. That single branch is
                // the difference between "About has defined cards" and
                // "About is one white sheet".
                var rim = ramp.Window.AverageBrightness() > 0.5
                    ? ramp.BorderDefault
                    : mat.RimTint;
                resources["AboutGlassSlabStroke"] =
                    EightThemePainter.FrozenSolid(Scale(rim, mat.RimOpacity));
                resources["AboutGlassHairlineStroke"] =
                    EightThemePainter.FrozenSolid(
                        Scale(rim, mat.RimOpacity * 0.55));

                resources["AboutGlassCaptionBrush"] =
                    EightThemePainter.FrozenSolid(ramp.TextMuted);
                resources["AboutGlassGlowBrush"] =
                    EightThemePainter.FrozenSolid(
                        Scale(ramp.BorderStrong, 0.30));

                resources["AboutCardCornerRadius"] =
                    new CornerRadius(profile.Geometry.Card);
                resources["AboutPanelCornerRadius"] =
                    new CornerRadius(profile.Geometry.Large);
                resources["AboutCardEffect"] = profile.Elevation.Kind
                    == EightElevationKind.None
                    ? new System.Windows.Media.Effects.DropShadowEffect
                    { BlurRadius = 0, ShadowDepth = 0, Opacity = 0 }
                    : new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = Math.Min(profile.Elevation.LowBlur, 40),
                        ShadowDepth = Math.Min(profile.Elevation.LowDepth, 8),
                        Opacity = profile.Elevation.LowOpacity * 0.8,
                        Color = ramp.Shadow,
                    };
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"About tokens for '{profile.Identity}' degraded: {ex.Message}");
            }
        }

        /// <summary>
        /// Mirrors EightThemePainter's alpha rule: opaque families are
        /// forced to 255, translucent families keep the ramp's own alpha.
        /// Kept as a private twin rather than shared so this partial does
        /// not widen the painter's internal surface.
        /// </summary>
        private static Color SurfaceFill(Color c, EightMaterialKind kind)
        {
            if (kind is EightMaterialKind.Solid
                or EightMaterialKind.Extruded
                or EightMaterialKind.Inflated
                or EightMaterialKind.Foil)
                return Color.FromArgb(255, c.R, c.G, c.B);
            return c;
        }

        private static Color Scale(Color c, double factor) =>
            Color.FromArgb(
                (byte)Math.Round(Math.Clamp(factor, 0, 1) * 255),
                c.R, c.G, c.B);
    }

    /// <summary>Small luminance helpers used by the About seeding. Kept
    /// outside the partial so they do not widen its public surface.</summary>
    internal static class EightColorExtensions
    {
        /// <summary>Relative luminance in 0..1. Used to decide whether a
        /// rim has to be drawn in ink or in light.</summary>
        internal static double AverageBrightness(this Color c) =>
            (0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B));

        private static double Linear(byte channel)
        {
            double v = channel / 255.0;
            return v <= 0.03928 ? v / 12.92
                : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
    }
}