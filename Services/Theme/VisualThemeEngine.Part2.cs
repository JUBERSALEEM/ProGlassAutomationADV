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
            // ── iOS LIQUID GLASS — the default family (2026-08-29) ────────
        //
        // ONE coherent crystal design system, adaptive to Light/Dark.
        // The previous "Glass" and "Glassy" families were two
        // overlapping, inconsistent attempts — both deleted. This
        // family merges their best parts into a single scene:
        //
        //   * WINDOW — an OPAQUE, self-contained aurora stage: deep
        //     navy + orbs in Dark, sky-ice + orbs in Light. Painting
        //     the scene INSIDE the window (instead of veiling the
        //     surface and hoping the DWM backdrop shows through an
        //     opaque WPF surface) fixed the "full fade white" bug —
        //     the look is identical on every Windows 10/11 machine.
        //   * SURFACES — translucent white panes with a top-lit fade:
        //     high-alpha white water glass in Light, deep frost in
        //     Dark. The stage must stay noticeably more colourful
        //     than the panes — that contrast is what makes glass
        //     read as crystal instead of white-on-white.
        //   * STATUS TINTS — hue kept, fill made frost.
        //   * TYPOGRAPHY — About-page text palette in Dark, fixed
        //     ink/slate in Light (decoupled from surface keys).
        //   * LIQUID GLASS TOKENS — crystal edge, reflections and
        //     control states published last so the whole
        //     LiquidGlassStyles.xaml control system re-tints live.

        private static void ApplyLiquidGlass(ResourceDictionary resources)
        {
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            var accent = ThemeManager.AccentColor;

            // ── 1) WINDOW — the aurora stage (always opaque) ────────
            resources["WindowBackgroundBrush"] =
                BuildLiquidStageBrush(dark, accent);

            // ── 2) CARD SURFACES — one material ramp per mode ───────
            if (dark)
            {
                resources["ModuleBackgroundBrush"] = FrostFill(accent, 0.30, 0.12);
                resources["ElevatedSurfaceBrush"] = FrostFill(accent, 0.36, 0.16);
                resources["SideMenuBackgroundBrush"] = FrostFill(accent, 0.44, 0.20);
                resources["MenuHoverBrush"] = FrostFill(accent, 0.32, 0.18);
                resources["SidebarCardBrush"] = FrostFill(accent, 0.36, 0.16);
                resources["ShellLiquidBrush"] = FrostFill(accent, 0.42, 0.20);
                resources["DashLiquidSurfaceBrush"] = FrostFill(accent, 0.32, 0.15);

                // ── 3) DASHBOARD AMBIENT — soft glow under the frost ─
                resources["DashAmbientBrush"] = FrostFill(accent, 0.16, 0.08);
                resources["DashAmbientBlob1Brush"] = SolidFrost(accent, 0.12);
                resources["DashAmbientBlob2Brush"] = SolidFrost(accent, 0.10);
            }
            else
            {
                // White water glass: translucent white panes, densest
                // at the top (iOS light materials catch the light),
                // see-through toward the lower edge.
                resources["ModuleBackgroundBrush"] = FrostFill(accent, 0.90, 0.64);
                resources["ElevatedSurfaceBrush"] = FrostFill(accent, 0.94, 0.70);
                resources["SideMenuBackgroundBrush"] = FrostFill(accent, 0.90, 0.62);
                resources["MenuHoverBrush"] = FrostFill(accent, 0.62, 0.42);
                resources["SidebarCardBrush"] = FrostFill(accent, 0.92, 0.70);
                resources["ShellLiquidBrush"] = FrostFill(accent, 0.95, 0.80);
                resources["DashLiquidSurfaceBrush"] = FrostFill(accent, 0.88, 0.64);

                // ── 3) DASHBOARD AMBIENT — blue glow under the panes ─
                resources["DashAmbientBrush"] = FrostFill(accent, 0.24, 0.10);
                resources["DashAmbientBlob1Brush"] = SolidFrost(accent, 0.18);
                resources["DashAmbientBlob2Brush"] = SolidFrost(accent, 0.14);
            }

            // ── 2b) SECTION ADOPTION — every shared glass surface /
            // border key now mirrors this family's material, so
            // Settings cards, wizard tiles, sidebars and every section
            // finally render iOS Liquid Glass instead of the base-theme
            // glass (2026-09-01).
            PublishSectionGlass(resources, accent, dark);

            // ── 4) STATUS TINTS — hue kept, fill made glass ─────────
            resources["TintIndigoBrush"] = HueFrost("TintIndigoBrush", 0.36, 0.18);
            resources["TintBlueBrush"] = HueFrost("TintBlueBrush", 0.36, 0.18);
            resources["TintGreenBrush"] = HueFrost("TintGreenBrush", 0.36, 0.18);
            resources["TintRedBrush"] = HueFrost("TintRedBrush", 0.36, 0.18);
            resources["TintAmberBrush"] = HueFrost("TintAmberBrush", 0.36, 0.18);
            resources["TintPurpleBrush"] = HueFrost("TintPurpleBrush", 0.36, 0.18);
            resources["TintGrayBrush"] = HueFrost("TintGrayBrush", 0.36, 0.18);

            // ── 5) TYPOGRAPHY — per-mode text palette ───────────────
            SetLiquidTextPalette(resources, dark);

            // ── 6) SHAPE + KEYLINE ──────────────────────────────────
            resources["CardCornerRadius"] = new CornerRadius(14);
            resources["CardCornerRadiusLarge"] = new CornerRadius(18);
            resources["ControlCornerRadius"] = new CornerRadius(10);
            // Watery keyline (2026-08-29): the hairline that outlines
            // every card is tinted with the accent at a whisper, so
            // edges catch the ambient colour like wet glass instead of
            // sitting as a foreign grey.
            Color keylineBase = dark
                ? Color.FromRgb(0x6E, 0x8C, 0xC4)
                : Color.FromRgb(0x94, 0xB4, 0xDE);
            Color tintedKeyline = ColorMath.Mix(
                keylineBase, ColorMath.Tint(accent, keylineBase, 0.45), 0.35);
            ApplyKeyline(resources, dark
                ? ColorMath.Lighten(tintedKeyline, 0.06)
                : ColorMath.Lighten(tintedKeyline, -0.04));

            // ── 7) LIQUID GLASS TOKENS — design-system controls ─────
            ApplyLiquidGlassTokens(resources, dark);
        }

        /// <summary>
        /// One frosted card fill: accent-tinted white glass with a
        /// top-lit vertical alpha fade (denser at the top — the
        /// "lit from above" iOS sheen). Flat average alpha when
        /// Gradient Surfaces is off.
        /// </summary>
        private static Brush FrostFill(
            Color accent, double topAlpha, double bottomAlpha)
        {
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            return FrostBrush(accent, Colors.White, dark,
                topAlpha, bottomAlpha);
        }

        /// <summary>
        /// A flat, accent-tinted glass at one fixed alpha (decorative
        /// glows such as the dashboard ambient blobs).
        /// </summary>
        private static Brush SolidFrost(Color accent, double alpha)
        {
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            return FrostBrush(accent, Colors.White, dark,
                alpha, alpha);
        }

        /// <summary>
        /// Hue-preserved glass: the ACTIVE theme's colour of the key
        /// rendered at a low alpha — status cards/chips stay coloured
        /// (green = success, red = danger, ...) but the fill becomes
        /// transparent. Gradient when Gradient Surfaces is on.
        /// </summary>
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
        /// The About page's text palette, applied as the app's
        /// Primary/Secondary/Muted foreground: primary = LiquidSnow
        /// (near-white on the navy stage), secondary = LiquidCerulean,
        /// muted = LiquidMuted — the exact colours the About page's
        /// headings and captions use, read from the ACTIVE theme's
        /// Liquid* keys so Light/Dark swaps auto-select the match.
        /// </summary>
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

        // ── LIQUID GLASS DESIGN SYSTEM TOKENS (2026-08-29) ────────────
        //
        // The iOS Liquid Glass family publishes the full crystal material
        // here: edge, reflections, control states, selection, focus,
        // primary crystal — accent-tinted and per-mode. The
        // LiquidGlassStyles.xaml dictionary consumes every token via
        // DynamicResource, so one family switch re-tints the entire
        // card/button/switch/input/grid/dialog system live.

        /// <summary>
        /// Writes the design-system tokens for the active glass
        /// family. Center surfaces mirror the family's own ramp
        /// (already written into ModuleBackgroundBrush /
        /// MenuHoverBrush / ElevatedSurfaceBrush) so cards and
        /// controls stay one material with the window stage.
        /// </summary>
        private static void ApplyLiquidGlassTokens(
            ResourceDictionary resources, bool dark)
        {
            var accent = ThemeManager.AccentColor;

            // Center surfaces — same material as the family's cards.
            resources["LiquidGlassBrush"] =
                resources["ModuleBackgroundBrush"];
            resources["LiquidGlassSubtleBrush"] =
                resources["MenuHoverBrush"];
            resources["LiquidGlassStrongBrush"] =
                resources["ElevatedSurfaceBrush"];

            // Crystal edges + reflections.
            resources["LiquidEdgeBrush"] = CrystalEdge(accent, dark);
            resources["LiquidInnerHighlightBrush"] =
                HorizontalSheen(dark ? (byte)0x59 : (byte)0xB3);
            resources["LiquidReflectionBrush"] =
                DiagonalSheen(dark ? (byte)0x26 : (byte)0x59);

            // Interaction states — white glass at state alphas.
            resources["LiquidControlHoverBrush"] =
                SolidFrost(accent, dark ? 0.36 : 0.66);
            resources["LiquidControlPressedBrush"] =
                SolidFrost(accent, dark ? 0.28 : 0.76);
            resources["LiquidControlDisabledBrush"] =
                SolidFrost(accent, dark ? 0.18 : 0.42);

            // Focus / selection / primary — accent-tinted crystal.
            resources["LiquidFocusBrush"] = FrozenSolid(Color.FromArgb(
                dark ? (byte)0x99 : (byte)0xB3,
                accent.R, accent.G, accent.B));
            resources["LiquidSelectionBrush"] =
                AccentCrystal(accent, dark ? (0x4D, 0x33) : (0x66, 0x40));
            resources["LiquidPrimaryButtonBrush"] =
                AccentCrystal(accent, dark ? (0xA6, 0x73) : (0xE8, 0xA6));

            // Dialog veil + soft shadow tint.
            resources["LiquidOverlayBrush"] = FrozenSolid(dark
                ? Color.FromArgb(0x99, 0x03, 0x10, 0x26)
                : Color.FromArgb(0x59, 0x06, 0x17, 0x24));
            resources["LiquidShadowColor"] = dark
                ? Color.FromArgb(0xFF, 0x00, 0x04, 0x10)
                : Color.FromArgb(0x4D, 0x0B, 0x1B, 0x2E);
        }

        /// <summary>
        /// PUBLISHES THE SHARED GLASS MATERIAL SET for the active family
        /// (2026-09-01 — sections must accept the theme).
        ///
        /// The view layer (Settings section cards &amp; tiles, wizard tiles,
        /// sidebars, info cards, forms, stat/canvas cards) consumes the
        /// canonical shared glass surface and border keys. All of it.
        /// The engine used to override only Module/Elevated/SideMenu, so
        /// the Settings sections, wizards and every other screen kept
        /// showing the BASE THEME glass regardless of the selected
        /// family — the "sections don't accept the theme" symptom. Every
        /// family now mirrors its own material into the shared glass
        /// keys; Normal restores the base-theme values through
        /// ClearOverrides.
        /// </summary>
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
        /// The ONE dialog card material (2026-09-06).
        ///
        /// DEFECT this closes ("approve popup card is transparent, multi
        /// cards mismatch"): dialog roots were painted with
        /// ModuleBackgroundBrush, which ApplyModule deliberately routes
        /// through SetTranslucent (0.62-0.80) whenever a wallpaper or an
        /// OS backdrop is live. That is right for a module drawn on the
        /// app's own stage and WRONG for a dialog: a dialog is its own
        /// top-level Window with AllowsTransparency, so there is no app
        /// stage behind it and the alpha falls through to the desktop.
        /// Half the modal family used ModuleBackgroundBrush and half used
        /// WindowBackgroundBrush, so cards in one flow did not even match
        /// each other.
        ///
        /// Contract: ALWAYS OPAQUE (alpha forced to 0xFF), never passed
        /// through SetTranslucent, derived from the family's own surface
        /// so it still reads as that theme. Called from
        /// PublishSectionGlass, which every family painter already runs —
        /// that is what guarantees parity instead of six copies.
        /// Both keys are ManagedKeys, so switching family clears them.
        /// </summary>
        private static void PublishDialogSurface(
            ResourceDictionary resources, Color familyTint, bool dark)
        {
            // Base the card on the theme's own window colour so each
            // family keeps its identity, then force it fully opaque.
            Color baseColor = ThemeManager.GetThemeColor(
                "WindowBackgroundBrush",
                dark ? Color.FromRgb(0x16, 0x1A, 0x22)
                     : Color.FromRgb(0xFB, 0xF9, 0xF3));

            // A dialog floats ABOVE the app, so it must read slightly
            // lifted from the window plane, not identical to it.
            Color card = dark
                ? ColorMath.Mix(baseColor, Color.FromRgb(0xFF, 0xFF, 0xFF), 0.06)
                : ColorMath.Mix(baseColor, Color.FromRgb(0xFF, 0xFF, 0xFF), 0.55);

            // Whisper of the family tint so Ocean/Crystal/Industrial
            // dialogs are recognisably themselves.
            card = ColorMath.Mix(card, familyTint, dark ? 0.10 : 0.05);

            resources["DialogSurfaceBrush"] =
                FrozenSolid(ColorMath.WithAlpha(card, 0xFF));

            // Inset shelf (footers/info strips) — one step off the card,
            // also opaque.
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
                    if (Current == LiquidGlass || Current == CrystalFluid)
                    {
                        bool dark = ThemeManager.CurrentTheme ==
                            AppTheme.Dark;
                        SetLiquidTextPalette(resources, dark);
                    }
                    else
                    {
                        resources.Remove("PrimaryForegroundBrush");
                        resources.Remove("SecondaryForegroundBrush");
                        resources.Remove("MutedForegroundBrush");

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
                // iOS Liquid Glass branch above; overlapping scopes may not
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
        // family (opaque aurora stage for iOS Liquid Glass). No OS-
        // level chrome service is required or attached.

        /// <summary>Frozen SolidColorBrush helper for the font palette.</summary>
        private static void FrozenAndSet(
            ResourceDictionary resources, string key, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            resources[key] = brush;
        }

        /// <summary>
        /// Builds the About page's stage as a single window brush:
        /// diagonal base gradient + two soft radial light orbs, in one
        /// DrawingBrush so the whole window paints one scene.
        /// <para>
        /// The colours are the About page's own — the stage gradient
        /// that preceded the transparent-stage refactor and the exact
        /// LiquidOrbA / LiquidOrbB stops from the theme files — so the whole app
        /// reads as the About scene instead of only
        /// one page. The mid-stop carries a light accent tint, which is
        /// what lets an accent change re-tint the stage (the engine
        /// re-applies on AccentChanged).
        /// </para>
        /// </summary>
        private static Brush BuildLiquidStageBrush(
            bool dark, Color accent)
        {
            // Base stage: the About page's diagonal A → B → A gradient.
            Color baseA, baseB;
            if (dark)
            {
                baseA = Color.FromRgb(0x0B, 0x10, 0x26);           // deep navy
                baseB = ColorMath.Tint(
                    accent, Color.FromRgb(0x10, 0x2A, 0x56), 0.30);
            }
            else
            {
                // White water-ice stage: ice blue with an accent
                // under-tint — deliberately NOT near-white (2026-08-29
                // fix): white glass panes on a near-white stage read
                // as one flat white sheet. A visible sky-ice stage is
                // what makes the white frost read as water glass.
                baseA = Color.FromRgb(0xD7, 0xE5, 0xF9);           // ice
                baseB = ColorMath.Tint(
                    accent, Color.FromRgb(0xAE, 0xCB, 0xF0), 0.42);
            }

            if (!ThemeManager.UseGradients)
            {
                // Flat preference: one solid tinted stage colour.
                var flat = new SolidColorBrush(baseB);
                flat.Freeze();
                return flat;
            }

            var stage = new DrawingGroup();

            // All geometry shares one 0..1 design space; the orbs
            // intentionally extend past it, anchored at the corners the
            // way the About page places them. The base rectangle is
            // sized to the UNION of the bounds (orbs included) so that
            // Stretch.Fill maps the whole scene — and only the scene —
            // onto the window, giving full coverage at any size.
            //   Orb A: (0.87, 0.10) r 0.45 → x 0.42..1.32, y -0.35..0.55
            //   Orb B: (0.06, 0.94) r 0.36 → x -0.30..0.42, y 0.58..1.30
            const double baseLeft = -0.30, baseTop = -0.35;
            const double baseWidth = 1.62, baseHeight = 1.65;

            // Diagonal base gradient (top-left → bottom-right), frozen.
            var baseBrush = new LinearGradientBrush(
                baseA, baseB, new Point(0, 0), new Point(1, 1));
            baseBrush.Freeze();
            stage.Children.Add(new GeometryDrawing(
                baseBrush, null,
                new RectangleGeometry(
                    new Rect(baseLeft, baseTop, baseWidth, baseHeight))));

            // Orb A — top-right, the About page's larger orb.
            stage.Children.Add(new GeometryDrawing(
                LiquidOrb(dark, orbA: true), null,
                new EllipseGeometry(new Point(0.87, 0.10), 0.45, 0.45)));

            // Orb B — bottom-left, the About page's smaller orb.
            stage.Children.Add(new GeometryDrawing(
                LiquidOrb(dark, orbA: false), null,
                new EllipseGeometry(new Point(0.06, 0.94), 0.36, 0.36)));

            // Orb C — small accent bloom, center-top: the third light
            // source that gives water its layered depth.
            stage.Children.Add(new GeometryDrawing(
                LiquidOrbC(dark, accent), null,
                new EllipseGeometry(new Point(0.36, 0.08), 0.30, 0.30)));

            // Water caustics — three broad, nearly-invisible pools of
            // light, like sun refracting through a lens of water.
            // Static and frozen: depth with zero runtime cost.
            stage.Children.Add(new GeometryDrawing(
                LiquidCaustic(dark, accent, 0.060, 0.045),
                null, new EllipseGeometry(new Point(0.62, 0.42), 0.42, 0.30)));
            stage.Children.Add(new GeometryDrawing(
                LiquidCaustic(dark, accent, 0.045, 0.035),
                null, new EllipseGeometry(new Point(0.30, 0.72), 0.36, 0.26)));
            stage.Children.Add(new GeometryDrawing(
                LiquidCaustic(dark, accent, 0.040, 0.030),
                null, new EllipseGeometry(new Point(0.78, 0.82), 0.30, 0.22)));

            stage.Freeze();

            // Deliberately NO Viewport / ViewportUnits here: with the
            // default (empty) viewport a TileBrush stretches its
            // drawing over the ENTIRE area of whatever element it fills
            // — the window, at whatever size it has. Stretch.Fill maps
            // the scene's bounds onto that whole area, so coverage is
            // complete and the orbs stay anchored at the corners.
            // (This also keeps us off the BrushMappingMode enum, whose
            // members do not compile in this project's reference set.)
            var brush = new DrawingBrush(stage)
            {
                Stretch = Stretch.Fill
            };
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// One About-stage orb: the exact radial stops the About page
        /// uses (LiquidOrbA / LiquidOrbB in the theme files), frozen.
        /// </summary>
    private static Brush LiquidOrb(bool dark, bool orbA)
    {
        // Stops are the XAML values verbatim (AARRGGBB in the files):
        //   Dark  A: #4D82F6FF / #1D4ED84D / #1D4ED800
        //   Dark  B: #60A5FA66 / #3B82F633 / #3B82F600
        //   Light A: #6660A5FA / #332563EB / #002563EB   (2026-08-29 boost)
        //   Light B: #7A3B82F6 / #3D2563EB / #002563EB   (2026-08-29 boost)
        var (c0, c1, c2) = dark
                ? orbA
                    ? (Color.FromArgb(0x4D, 0x82, 0xF6, 0xFF),
                       Color.FromArgb(0x1D, 0x4E, 0xD8, 0x4D),
                       Color.FromArgb(0x00, 0x4E, 0xD8, 0x4D))
                    : (Color.FromArgb(0x60, 0xA5, 0xFA, 0x66),
                       Color.FromArgb(0x33, 0x82, 0xF6, 0x33),
                       Color.FromArgb(0x00, 0x82, 0xF6, 0x33))
                : orbA
                    ? (Color.FromArgb(0x66, 0x60, 0xA5, 0xFA),
                       Color.FromArgb(0x33, 0x25, 0x63, 0xEB),
                       Color.FromArgb(0x00, 0x25, 0x63, 0xEB))
                    : (Color.FromArgb(0x7A, 0x3B, 0x82, 0xF6),
                       Color.FromArgb(0x3D, 0x25, 0x63, 0xEB),
                       Color.FromArgb(0x00, 0x25, 0x63, 0xEB));

            var orb = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5,
                GradientStops =
                {
                    new GradientStop(c0, 0.0),
                    new GradientStop(c1, 0.55),
                    new GradientStop(c2, 1.0)
                }
            };
            orb.Freeze();
            return orb;
        }

        /// <summary>
        /// Orb C — the third, smaller accent bloom anchored center-top
        /// (2026-08-29 watery upgrade). Same shape language as the
        /// About orbs, accent-tinted, kept deliberately soft so it
        /// reads as refraction, not a spotlight.
        /// </summary>
        private static Brush LiquidOrbC(bool dark, Color accent)
        {
            var (a0, a1) = dark ? ((byte)0x38, (byte)0x14) : ((byte)0x5A, (byte)0x24);
            var orb = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5,
                GradientStops =
                {
                    new GradientStop(
                        Color.FromArgb(a0, accent.R, accent.G, accent.B), 0.0),
                    new GradientStop(
                        Color.FromArgb(a1, accent.R, accent.G, accent.B), 0.55),
                    new GradientStop(
                        Color.FromArgb(0x00, accent.R, accent.G, accent.B), 1.0)
                }
            };
            orb.Freeze();
            return orb;
        }

        /// <summary>
        /// One water-caustic light pool: a faint accent-tinted bloom
        /// (no center — light only at the rim of the pool, like real
        /// caustics) at a soft alpha. Frozen.
        /// </summary>
        private static Brush LiquidCaustic(
            bool dark, Color accent, double innerAlpha, double edgeAlpha)
        {
            var pool = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5,
                GradientStops =
                {
                    new GradientStop(
                        Color.FromArgb(0x00, accent.R, accent.G, accent.B), 0.0),
                    new GradientStop(
                        Color.FromArgb((byte)Math.Round(innerAlpha * 255),
                            accent.R, accent.G, accent.B), 0.55),
                    new GradientStop(
                        Color.FromArgb((byte)Math.Round(edgeAlpha * 255),
                            accent.R, accent.G, accent.B), 1.0)
                }
            };
            pool.Freeze();
            return pool;
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
            // Light: an ACCENT KISS over warm white (0.16) — the white
            // water-glass material keeps its identity but visibly
            // belongs to the chosen colour, so the frost reads
            // saturated instead of flat white. Dark: 0.32 keeps the
            // deep-frost hue rich against the navy stage.
            Color tinted = ColorMath.Tint(accent, baseColor,
                dark ? 0.32 : 0.16);

            byte topA = (byte)Math.Round(topAlpha * 255);
            byte bottomA = (byte)Math.Round(bottomAlpha * 255);

            if (ThemeManager.UseGradients)
            {
                // iOS LIQUID RAMP (2026-08-29 watery upgrade) — a real
                // droplet's light curve, not a linear fade: crisp top
                // edge → short dip → bright MENISCUS band (surface
                // tension catches the light) → long transparent center
                // → light gathering again at the bottom rim. Border has
                // no Template property, so the curve is baked into the
                // brush itself — every surface that consumes these keys
                // gets the watery identity for free.
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

        // ── ULTRA TRANSPARENT — fully see-through glass (iPhone 110%) ──
        //
        // Every surface is nearly invisible. The aurora stage shows
        // through cards, sidebars, dashboards — everything. Only the
        // crystal edges (white border + specular highlight) define the
        // shape. Text stays dark/light for readability. This is what
        // real iPhone liquid glass looks like: you see the wallpaper
        // behind every element.
    }
}
