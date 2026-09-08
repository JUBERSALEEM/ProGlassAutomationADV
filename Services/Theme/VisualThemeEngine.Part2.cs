// ═══════════════════════════════════════════════════════════════════════
// FILE: VisualThemeEngine.Part2.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProGlassAutomation.Services.Theme
{
        public static partial class VisualThemeEngine
    {
        private static void SetLiquidTextPalette(
            ResourceDictionary resources, bool dark)
        {
            // Dark mode reads the dark About text palette keys
            // (LiquidSnow / LiquidCerulean / LiquidMuted). LIGHT mode uses
            // fixed WARM ink/stone constants — decoupled from the Liquid*
            // SURFACE keys (2026-08-29) so the white water-glass
            // surfaces can be recoloured freely without ever washing
            // the text out (previously light text read LiquidFrost, a
            // surface-named key, so re-colouring the frost would have
            // made light text white-on-white).
            // 2026-09-01 cream pass: light glass text was cool slate
            // (#334155) with a BLUE secondary (#1D4ED8) — captions read
            // blue across the whole light UI. Now the same warm ink as
            // the Normal canvas: #1C1917 / #57534E / #78716C.
            resources["PrimaryForegroundBrush"] = dark
                ? FrozenLiquidText("LiquidSnow",
                    Color.FromRgb(0xF8, 0xFA, 0xFC))
                : FrozenSolid(Color.FromRgb(0x1C, 0x19, 0x17));
            resources["SecondaryForegroundBrush"] = dark
                ? FrozenLiquidText("LiquidCerulean",
                    Color.FromRgb(0x93, 0xC5, 0xFD))
                : FrozenSolid(Color.FromRgb(0x57, 0x53, 0x4E));
            resources["MutedForegroundBrush"] = dark
                ? FrozenLiquidText("LiquidMuted",
                    Color.FromRgb(0x8F, 0xB3, 0xE6))
                : FrozenSolid(Color.FromRgb(0x78, 0x71, 0x6C));
        }

        /// <summary>Frozen solid brush from a constant colour.</summary>
        private static Brush FrozenSolid(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Brush FrostFill(
            Color accent, double topAlpha, double bottomAlpha)
        {
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            return FrostBrush(accent, Colors.White, dark,
                topAlpha, bottomAlpha);
        }

        private static Brush HueFrost(
            string key, double topAlpha, double bottomAlpha)
        {
            Color hue = ThemeManager.GetThemeColor(
                key, Colors.SlateGray);

            byte topA = (byte)Math.Round(topAlpha * 255);
            byte bottomA = (byte)Math.Round(bottomAlpha * 255);

            if (ThemeManager.UseGradients)
            {
                var gradient = new LinearGradientBrush(
                    Color.FromArgb(topA, hue.R, hue.G, hue.B),
                    Color.FromArgb(bottomA, hue.R, hue.G, hue.B),
                    new Point(0, 0), new Point(1, 1));
                gradient.Freeze();
                return gradient;
            }

            var flat = new SolidColorBrush(Color.FromArgb(
                (byte)((topA + bottomA) / 2), hue.R, hue.G, hue.B));
            flat.Freeze();
            return flat;
        }

        /// <summary>
        /// Core frost builder shared by every frosted surface: base
        /// colour tinted toward the accent, then the iOS liquid ramp
        /// (top-lit meniscus curve). Flat average alpha when Gradient
        /// Surfaces is off.
        /// </summary>
        private static Brush FrostBrush(
            Color accent, Color baseColor, bool dark,
            double topAlpha, double bottomAlpha)
        {
            // Light: an ACCENT KISS over warm white (0.30) — the white
            // water-glass material keeps its identity but visibly
            // belongs to the chosen colour, so the frost reads
            // saturated BLUE instead of flat white (2026-09-06 richness
            // pass: 0.16 → 0.30 — cards must read as glass, not paper).
            // Dark: 0.38 keeps the deep-frost hue rich against the
            // navy stage.
            Color tinted = ColorMath.Tint(accent, baseColor,
                dark ? 0.38 : 0.30);

            byte topA = (byte)Math.Round(topAlpha * 255);
            byte bottomA = (byte)Math.Round(bottomAlpha * 255);

            if (ThemeManager.UseGradients)
            {
                // iOS LIQUID RAMP — a real droplet's light curve, not a
                // linear fade: crisp top edge → short dip → bright
                // MENISCUS band (surface tension catches the light) →
                // long transparent center → light gathering again at the
                // bottom rim.
                byte dip = (byte)(topA * 0.86);
                byte meniscus = (byte)Math.Min(0xFA, topA * 0.98 + 0x12);
                byte mid = (byte)(topA * 0.72 + bottomA * 0.28);
                byte low = (byte)Math.Min(0xFF, bottomA * 1.05);
                byte rim = (byte)Math.Min(0xF2, bottomA * 1.28 + 0x0A);

                var gradient = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops =
                    {
                        new GradientStop(
                            Color.FromArgb(topA, tinted.R, tinted.G, tinted.B), 0.00),
                        new GradientStop(
                            Color.FromArgb(dip, tinted.R, tinted.G, tinted.B), 0.07),
                        new GradientStop(
                            Color.FromArgb(meniscus, tinted.R, tinted.G, tinted.B), 0.16),
                        new GradientStop(
                            Color.FromArgb(mid, tinted.R, tinted.G, tinted.B), 0.45),
                        new GradientStop(
                            Color.FromArgb(low, tinted.R, tinted.G, tinted.B), 0.80),
                        new GradientStop(
                            Color.FromArgb(bottomA, tinted.R, tinted.G, tinted.B), 0.93),
                        new GradientStop(
                            Color.FromArgb(rim, tinted.R, tinted.G, tinted.B), 1.00)
                    }
                };
                gradient.Freeze();
                return gradient;
            }

            var flat = new SolidColorBrush(
                Color.FromArgb((byte)((topA + bottomA) / 2),
                    tinted.R, tinted.G, tinted.B));
            flat.Freeze();
            return flat;
        }

        private static void PublishSectionGlass(
            ResourceDictionary resources, Color borderTint, bool dark)
        {
            resources["GlassSurfaceBrush"] = resources["ModuleBackgroundBrush"];
            resources["GlassUltraClearBrush"] = resources["ModuleBackgroundBrush"];
            resources["GlassCrystalBrush"] = resources["ModuleBackgroundBrush"];
            resources["GlassFrostedBrush"] = resources["ModuleBackgroundBrush"];
            resources["GlassSurfaceStandard"] = resources["MenuHoverBrush"];
            resources["GlassSurfaceUltra"] = resources["SideMenuBackgroundBrush"];
            resources["GlassSurfaceElevated"] = resources["ElevatedSurfaceBrush"];

            // Crystal keyline edges — white glass mixed toward the
            // family's own colour so the edge belongs to the material.
            Color edge = ColorMath.Mix(
                Color.FromRgb(0xFF, 0xFF, 0xFF), borderTint,
                dark ? 0.35 : 0.22);
            byte softA = dark ? (byte)0x40 : (byte)0x66;
            byte primaryA = dark ? (byte)0x59 : (byte)0x8C;
            byte brightA = dark ? (byte)0x73 : (byte)0xB3;

            resources["GlassBorderSoft"] = FrozenSolid(
                ColorMath.WithAlpha(edge, softA));
            resources["GlassBorderPrimary"] = FrozenSolid(
                ColorMath.WithAlpha(edge, primaryA));
            resources["GlassBorderBright"] = FrozenSolid(
                ColorMath.WithAlpha(edge, brightA));

            PublishDialogSurface(resources, borderTint, dark);
        }

        /// <summary>
        /// Opaque dialog card material. A dialog is a floating Window
        /// with no app stage behind it, so ModuleBackgroundBrush
        /// (often translucent) shows the desktop through the card.
        /// </summary>
        private static void PublishDialogSurface(
            ResourceDictionary resources, Color familyTint, bool dark)
        {
            Color baseColor = ThemeManager.GetThemeColor(
                "WindowBackgroundBrush",
                dark ? Color.FromRgb(0x16, 0x1A, 0x22)
                     : Color.FromRgb(0xFB, 0xF9, 0xF3));

            Color card = dark
                ? ColorMath.Mix(baseColor, Color.FromRgb(0xFF, 0xFF, 0xFF), 0.06)
                : ColorMath.Mix(baseColor, Color.FromRgb(0xFF, 0xFF, 0xFF), 0.55);

            card = ColorMath.Mix(card, familyTint, dark ? 0.10 : 0.05);

            resources["DialogSurfaceBrush"] =
                FrozenSolid(ColorMath.WithAlpha(card, 0xFF));

            Color inset = dark
                ? ColorMath.Mix(card, Color.FromRgb(0xFF, 0xFF, 0xFF), 0.07)
                : ColorMath.Mix(card, Color.FromRgb(0x00, 0x00, 0x00), 0.05);

            resources["DialogInsetBrush"] =
                FrozenSolid(ColorMath.WithAlpha(inset, 0xFF));
        }

        /// <summary>
        /// Crystal edge gradient: bright white top → soft
        /// accent-mixed mid → slightly deeper lower edge. Frozen.
        /// </summary>
        private static Brush CrystalEdge(Color accent, bool dark)
        {
            // 2026-09-01 UHD pass: mid/edge bands lifted so the droplet
            // meniscus stays readable at a distance (was 0x59/0x73 light,
            // 0x26/0x33 dark).
            byte topA = dark ? (byte)0x8C : (byte)0xFF;
            byte midA = dark ? (byte)0x2E : (byte)0x66;
            byte botA = dark ? (byte)0x3C : (byte)0x80;

            Color mid = ColorMath.Mix(
                Color.FromRgb(0xFF, 0xFF, 0xFF), accent,
                dark ? 0.25 : 0.18);
            Color bot = ColorMath.Mix(
                Color.FromRgb(0xFF, 0xFF, 0xFF), accent,
                dark ? 0.15 : 0.10);

            var brush = new LinearGradientBrush(
                Color.FromArgb(topA, 0xFF, 0xFF, 0xFF),
                Color.FromArgb(botA, bot.R, bot.G, bot.B),
                new Point(0, 0), new Point(0, 1));
            // Condensation nuance: right below the crisp top edge the
            // light dips slightly, then the meniscus band swells at
            // mid-edge before fading to the deeper lower edge — the
            // curve of a droplet seen from the side.
            brush.GradientStops.Insert(1, new GradientStop(
                Color.FromArgb((byte)(topA * 0.78), 0xFF, 0xFF, 0xFF), 0.22));
            brush.GradientStops.Insert(2, new GradientStop(
                Color.FromArgb(midA, mid.R, mid.G, mid.B), 0.55));
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Horizontal top-reflection line: transparent ends, glowing
        /// white center. Frozen.
        /// </summary>
        private static Brush HorizontalSheen(byte centerAlpha)
        {
            var brush = new LinearGradientBrush(
                Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF),
                Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF),
                new Point(0, 0), new Point(1, 0));
            brush.GradientStops.Insert(1, new GradientStop(
                Color.FromArgb(centerAlpha, 0xFF, 0xFF, 0xFF), 0.5));
            brush.Freeze();
            return brush;
        }

        /// <summary>Diagonal specular sweep, fading out. Frozen.</summary>
        private static Brush DiagonalSheen(byte alpha)
        {
            var brush = new LinearGradientBrush(
                Color.FromArgb(alpha, 0xFF, 0xFF, 0xFF),
                Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF),
                new Point(0, 0), new Point(1, 1));
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Accent-tinted crystal fill (top-lit vertical gradient at
        /// the given alphas) — selection and primary fills. Frozen.
        /// </summary>
        private static Brush AccentCrystal(
            Color accent, (int top, int bottom) alphas)
        {
            var brush = new LinearGradientBrush(
                Color.FromArgb((byte)alphas.top, accent.R, accent.G, accent.B),
                Color.FromArgb((byte)alphas.bottom, accent.R, accent.G, accent.B),
                new Point(0, 0), new Point(0, 1));
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Frozen solid brush for one of the About page's text palette
        /// keys (LiquidSnow / LiquidFrost / LiquidCerulean / LiquidMuted)
        /// read from the ACTIVE theme dictionary — falling back to the
        /// given colour if the key is ever missing.
        /// </summary>
        private static Brush FrozenLiquidText(string key, Color fallback)
        {
            var brush = new SolidColorBrush(
                ThemeManager.GetThemeColor(key, fallback));
            brush.Freeze();
            return brush;
        }

        // ── GLOBAL FONT COLOUR (Settings → Appearance → Font Color) ──
        //
        // One user-chosen text colour drives the WHOLE UI's
        // Primary/Secondary/Muted foregrounds, in EVERY visual family.
        // Secondary and muted are derived by mixing the chosen colour
        // toward the theme's neutral slate, so the heading/caption/hint
        // contrast order survives whatever colour is picked — this is
        // the tool that guarantees text visibility when iOS Liquid
        // Glass makes every card transparent.
        //
        // Called at the END of Apply and ApplyModule, so it runs after
        // the family baseline AND after any wallpaper typography
        // adaptation: user intent wins last.

        /// <summary>
        /// Applies (or removes) the persisted global font colour
        /// preference. Auto (""/null) restores ownership: iOS Liquid
        /// Glass gets its About-page palette, every other family gets
        /// the Light/
        /// Dark theme defaults (with the wallpaper contrast adaptation
        /// re-applied when a wallpaper is on screen).
        /// </summary>
        public static void ApplyFontPreference(
            ResourceDictionary resources, bool wallpaperActive)
        {
            try
            {
                string hex = App.Config?.Appearance?.FontColor ?? "";

                if (string.IsNullOrWhiteSpace(hex))
                {
                    if (Current == CrystalFluid)
                    {
                        bool dark = ThemeManager.CurrentTheme ==
                            AppTheme.Dark;
                        SetLiquidTextPalette(resources, dark);
                    }
                    else
                    {
                        // Normal and Monet let the theme dictionary own
                        // their ink, so the app-level foregrounds are
                        // removed and the theme value wins. Normal
                        // (Industrial) paints its OWN navy ink in
                        // ApplyIndustrial — removing it here would wipe
                        // the family's professional slate text back to the
                        // warm cream ink (the 2026-09-07 "industrial text
                        // isn't navy" finding). Keep it.
                        if (Current != NormalIndustrial)
                        {
                            resources.Remove("PrimaryForegroundBrush");
                            resources.Remove("SecondaryForegroundBrush");
                            resources.Remove("MutedForegroundBrush");
                        }

                        // The wallpaper layer sets stronger secondary/
                        // muted for readability over pictures — if we
                        // just deleted them and a wallpaper is showing,
                        // restore that adaptation.
                        if (wallpaperActive)
                            AdaptTypographyForWallpaper(resources);
                    }
                    return;
                }

                var chosen = AccentPresets.TryParseHex(hex);
                if (chosen == null) return; // invalid persisted hex — keep current

                // (isDark, not dark — that name is already taken by the
                // Crystal Fluid branch above; overlapping scopes may not
                // redeclare a local name in C#.)
                Color primary = chosen.Value;
                bool isDark = ThemeManager.CurrentTheme == AppTheme.Dark;

                // 2026-09-01: Light fallbacks were cool slate
                // (#475569 / #64748B) — the "industrial blue" that crept
                // into Normal whenever a theme key was missing. Warm
                // stone keeps even the fallback path blue-free.
                Color secNeutral = ThemeManager.GetThemeColor(
                    "SecondaryForegroundColor",
                    isDark ? Color.FromRgb(0x94, 0xA3, 0xB8)
                           : Color.FromRgb(0x57, 0x53, 0x4E));
                Color mutNeutral = ThemeManager.GetThemeColor(
                    "MutedForegroundColor",
                    isDark ? Color.FromRgb(0x64, 0x74, 0x8B)
                           : Color.FromRgb(0x78, 0x71, 0x6C));

                FrozenAndSet(resources, "PrimaryForegroundBrush", primary);
                FrozenAndSet(resources, "SecondaryForegroundBrush",
                    ColorMath.Mix(primary, secNeutral, 0.35));
                FrozenAndSet(resources, "MutedForegroundBrush",
                    ColorMath.Mix(primary, mutNeutral, 0.55));
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Font colour preference apply failed: {ex.Message}",
                    new LogContext
                    {
                        Module = "Themes",
                        Class = nameof(VisualThemeEngine)
                    });
            }
        }

        // WINDOW SURFACE — painted entirely in-window by the active
        // family (pale crystal stage for Crystal Fluid / Ios-17pm). No
        // OS-level chrome service is required or attached.

        /// <summary>Frozen SolidColorBrush helper for the font palette.</summary>
        private static void FrozenAndSet(
            ResourceDictionary resources, string key, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            resources[key] = brush;
        }

    }
}
