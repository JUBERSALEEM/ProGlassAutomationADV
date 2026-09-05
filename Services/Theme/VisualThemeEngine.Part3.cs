// ═══════════════════════════════════════════════════════════════════════
// FILE: VisualThemeEngine.Part3.cs (Phase 3.8 split — member census verified)
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
            private static void ApplyCrystalFluid(
            ResourceDictionary resources)
        {
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            var accent = ThemeManager.AccentColor;

            // ══════════════════════════════════════════════════════════
            //  CRYSTAL FLUID (2026-09-05) — iOS water-glass design system.
            //  REPLACES "Ultra Transparent" (retired this commit). Light-
            //  only family (ModePairFor → false); the `dark` branches are
            //  defensive fallbacks for mid-mode-switch paints only.
            //
            //  Spec = the frozen inline reference (CRYSTAL_FLUID_DESIGN.md):
            //    · ink #102938 / muted #68808F / accent #55C9EE
            //    · glass = white .43→.14 @135° (never opaque)
            //    · 1px bright-white crystal border + inner hairline
            //    · pale luminous environment stage (#F8FDFF/#EDFAFF/#DFF5FF)
            //
            //  Same resource-key contract as the retired painter — the
            //  64-key ManagedKeys guarantee and the registry parity tests
            //  keep working unchanged; only VALUES (and this header)
            //  changed: new visual identity, proven plumbing.
            // ══════════════════════════════════════════════════════════

            // ── 1) WINDOW — pale luminous environment stage (§22 base).
            // The wallpaper scene ("Crystal Fluid") paints the radial
            // light choreography on top; the stage underneath stays calm:
            // white highlight upper-left → pale cyan lower-right. ──────
            var stage = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop(dark
                        ? Color.FromRgb(0x0E, 0x22, 0x30)
                        : Color.FromRgb(0xF8, 0xFD, 0xFF), 0.0),
                    new GradientStop(dark
                        ? Color.FromRgb(0x0C, 0x1D, 0x2A)
                        : Color.FromRgb(0xED, 0xFA, 0xFF), 0.45),
                    new GradientStop(dark
                        ? Color.FromRgb(0x09, 0x16, 0x22)
                        : Color.FromRgb(0xDF, 0xF5, 0xFF), 1.0),
                }
            };
            stage.Freeze();
            resources["WindowBackgroundBrush"] = stage;

            // ── 2) SHADOWS — water-weight shadows flow from the theme
            // XAML via ClearOverrides (unchanged mechanism).

            // ── 3) CRYSTAL BORDER (§11) — bright translucent white edge
            // with a soft falloff; never solid, never neon. ────────────
            var crystalBorder = new SolidColorBrush(
                Color.FromArgb(0x8F, 0xFF, 0xFF, 0xFF));
            crystalBorder.Freeze();

            var crystalBorderGradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF), 0.0),
                    new GradientStop(Color.FromArgb(0x8F, 0xFF, 0xFF, 0xFF), 0.55),
                    new GradientStop(Color.FromArgb(0x61, 0xFF, 0xFF, 0xFF), 1.0)
                }
            };
            crystalBorderGradient.Freeze();
            resources["GlassBorderGradientCrystal"] = crystalBorderGradient;

            resources["OutlineBrush"] = crystalBorder;
            resources["OutlineDefaultBrush"] = crystalBorder;
            resources["BorderBrush"] = crystalBorder;
            resources["DividerBrush"] = new SolidColorBrush(
                Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
            resources["GlassBorderSoft"] = new SolidColorBrush(
                Color.FromArgb(0x5C, 0xFF, 0xFF, 0xFF));

            // ── 4) TYPOGRAPHY (§23) — the frozen spec palette:
            // primary ink #102938, muted blue-gray #68808F. ────────────
            resources["PrimaryForegroundBrush"] = dark
                ? FrozenLiquidText("LiquidSnow",
                    Color.FromRgb(0xF2, 0xFA, 0xFE))
                : FrozenSolid(Color.FromRgb(0x10, 0x29, 0x38));
            resources["SecondaryForegroundBrush"] = dark
                ? FrozenLiquidText("LiquidCerulean",
                    Color.FromRgb(0x9C, 0xC3, 0xD9))
                : FrozenSolid(Color.FromRgb(0x3D, 0x55, 0x66));
            resources["MutedForegroundBrush"] = dark
                ? FrozenLiquidText("LiquidMuted",
                    Color.FromRgb(0x7F, 0xA4, 0xBD))
                : FrozenSolid(Color.FromRgb(0x68, 0x80, 0x8F));

            // ── 5) SHAPE (§24) — big iOS radii, centralized: cards 30/32,
            // controls 15 (buttons 14–16 band). ────────────────────────
            resources["CardCornerRadius"] = new CornerRadius(30);
            resources["CardCornerRadiusLarge"] = new CornerRadius(32);
            resources["ControlCornerRadius"] = new CornerRadius(15);

            // ── 6) STATUS TINTS — barely-visible hue (unchanged
            // mechanism; tint color feeds status pills, §36). ──────────
            resources["TintIndigoBrush"] = HueFrost("TintIndigoBrush", 0.10, 0.05);
            resources["TintBlueBrush"] = HueFrost("TintBlueBrush", 0.10, 0.05);
            resources["TintGreenBrush"] = HueFrost("TintGreenBrush", 0.10, 0.05);
            resources["TintRedBrush"] = HueFrost("TintRedBrush", 0.10, 0.05);
            resources["TintAmberBrush"] = HueFrost("TintAmberBrush", 0.10, 0.05);
            resources["TintPurpleBrush"] = HueFrost("TintPurpleBrush", 0.10, 0.05);
            resources["TintGrayBrush"] = HueFrost("TintGrayBrush", 0.10, 0.05);

            // ── 6b) CARD/SURFACE FROST (§10/§39/§81) — layered alpha
            // white; the environment must stay perceptible, so module
            // surfaces are a whisper while elevated sheets (dialogs,
            // dropdown popups) sit one shelf higher for readability.
            resources["ModuleBackgroundBrush"] =
                FrostFill(accent, dark ? 0.16 : 0.30, dark ? 0.08 : 0.12);

            resources["SideMenuBackgroundBrush"] =
                FrostFill(accent, dark ? 0.22 : 0.55, dark ? 0.12 : 0.30);
            resources["ElevatedSurfaceBrush"] =
                FrostFill(accent, dark ? 0.28 : 0.62, dark ? 0.16 : 0.40);

            // Sections adopt the crystal material — shared glass set
            // mirrors the bright white edge.
            PublishSectionGlass(resources, Colors.White, dark);

            // ── 7) HOVER (§26) — soft translucent white veil ──────────
            var hoverFill = new SolidColorBrush(
                Color.FromArgb(dark ? (byte)0x1A : (byte)0x2E, 0xFF, 0xFF, 0xFF));
            hoverFill.Freeze();
            resources["MenuHoverBrush"] = hoverFill;

            // ── 8) LIQUID TOKENS — crystal edge + §13 specular sheen ──
            resources["LiquidEdgeBrush"] = CrystalEdge(accent, dark);
            resources["LiquidInnerHighlightBrush"] =
                HorizontalSheen(dark ? (byte)0x70 : (byte)0xE0);
            resources["LiquidReflectionBrush"] =
                DiagonalSheen(dark ? (byte)0x2E : (byte)0x4D);
            resources["LiquidControlHoverBrush"] = hoverFill;

            var pressed = new SolidColorBrush(
                Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
            pressed.Freeze();
            resources["LiquidControlPressedBrush"] = pressed;

            var disabled = new SolidColorBrush(
                Color.FromArgb(0x0C, 0xFF, 0xFF, 0xFF));
            disabled.Freeze();
            resources["LiquidControlDisabledBrush"] = disabled;

            resources["LiquidFocusBrush"] = FrozenSolid(Color.FromArgb(
                0x5C, accent.R, accent.G, accent.B));
            resources["LiquidSelectionBrush"] =
                AccentCrystal(accent, dark ? (0x28, 0x18) : (0x33, 0x1F));
            resources["LiquidPrimaryButtonBrush"] =
                AccentCrystal(accent, dark ? (0x70, 0x45) : (0x99, 0x5E));

            // Dialog scrim: icy ink veil (§39) — calmer than the old
            // deep-blue overlay on the pale environment.
            resources["LiquidOverlayBrush"] = FrozenSolid(dark
                ? Color.FromArgb(0x99, 0x03, 0x10, 0x1F)
                : Color.FromArgb(0x47, 0x10, 0x29, 0x38));
            // §12: soft cyan/blue-gray shadow ink, low opacity.
            resources["LiquidShadowColor"] = dark
                ? Color.FromArgb(0xFF, 0x00, 0x04, 0x10)
                : Color.FromArgb(0x55, 0x1E, 0x5F, 0x7E);
        }

        // ── OCEAN — deep teal corporate ───────────────────────────────

        private static void ApplyOcean(ResourceDictionary resources)
        {
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            var window = new LinearGradientBrush(
                dark ? Color.FromRgb(0x06, 0x23, 0x30)
                     : Color.FromRgb(0xE4, 0xF4, 0xF1),
                dark ? Color.FromRgb(0x09, 0x2E, 0x40)
                     : Color.FromRgb(0xCB, 0xE9, 0xE2),
                new Point(0, 0), new Point(1, 1));
            window.Freeze();
            resources["WindowBackgroundBrush"] = window;

            Color teal = dark
                ? Color.FromRgb(0x10, 0x34, 0x43)
                : Color.FromRgb(0xC6, 0xE5, 0xDD);

            Color moduleBg = ThemeManager.GetThemeColor(
                "ModuleBackgroundBrush", Colors.White);
            var moduleBrush = new SolidColorBrush(
                ColorMath.Mix(moduleBg, teal, 0.60));
            moduleBrush.Freeze();
            resources["ModuleBackgroundBrush"] = moduleBrush;

            Color elevated = ThemeManager.GetThemeColor(
                "ElevatedSurfaceBrush", Colors.White);
            var elevatedBrush = new SolidColorBrush(
                ColorMath.Mix(elevated, teal, 0.50));
            elevatedBrush.Freeze();
            resources["ElevatedSurfaceBrush"] = elevatedBrush;

            Color sideMenu = ThemeManager.GetThemeColor(
                "SideMenuBackgroundBrush", Colors.White);
            var sideMenuBrush = new SolidColorBrush(
                ColorMath.Mix(sideMenu, teal, 0.45));
            sideMenuBrush.Freeze();
            resources["SideMenuBackgroundBrush"] = sideMenuBrush;

            resources["CardCornerRadius"] = new CornerRadius(12);
            resources["CardCornerRadiusLarge"] = new CornerRadius(16);
            resources["ControlCornerRadius"] = new CornerRadius(8);

            var hover = new SolidColorBrush(dark
                ? Color.FromRgb(0x14, 0x35, 0x47)
                : Color.FromRgb(0xE4, 0xF2, 0xF6));
            hover.Freeze();
            resources["MenuHoverBrush"] = hover;

            // Sections accept Ocean too — shared glass set mirrors the
            // deep-teal material.
            PublishSectionGlass(resources, teal, dark);

            ApplyKeyline(resources, dark
                ? Color.FromRgb(0x2E, 0x5A, 0x6E)
                : Color.FromRgb(0x7F, 0xB4, 0xC4));
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static void ClearOverrides(ResourceDictionary resources)
        {
            foreach (var key in ManagedKeys)
                resources.Remove(key);
        }

        private static void SetTranslucent(
            ResourceDictionary resources, string key,
            Color baseColor, double opacity)
        {
            var brush = new SolidColorBrush(baseColor)
            {
                Opacity = opacity
            };
            brush.Freeze();
            resources[key] = brush;
        }

        private static string Normalize(string family)
        {
            return family?.Trim() switch
            {
                Monet => Monet,
                Professional => Professional,
                LiquidGlass => LiquidGlass,
                CrystalFluid => CrystalFluid,
                // Legacy families removed in the 2026-08-29 rewrite —
                // saved configs pointing at them resolve to the new
                // iOS Liquid Glass family instead of breaking.
                "Glass" => LiquidGlass,
                "Glassy" => LiquidGlass,
                // 2026-09-05: every retired transparent-glass name
                // — including the literal old display string — lands
                // on Crystal Fluid, never on a missing family.
                "Ultra Transparent" => CrystalFluid,
                "Transparent" => CrystalFluid,
                "Clear" => CrystalFluid,
                "Crystal" => CrystalFluid,
                "Crystal Sea" => CrystalFluid,
                Ocean => Ocean,
                // Industrial family (2026-09-05): three explicit shade
                // layouts; bare "Industrial" from older saves → Grey.
                IndustrialWhite => IndustrialWhite,
                IndustrialGrey  => IndustrialGrey,
                IndustrialDark  => IndustrialDark,
                "Industrial"    => IndustrialGrey,
                "Factory"       => IndustrialGrey,
                _ => Normal
            };
        }

        private static double ClampOpacity(double value) =>
            value < 0.3 ? 0.3 : value > 1.0 ? 1.0 : value;
    }
}
