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
            private static void ApplyUltraTransparent(
            ResourceDictionary resources)
        {
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            var accent = ThemeManager.AccentColor;

            // ══════════════════════════════════════════════════════════
            //  ULTRA TRANSPARENT — Crystal Clear Glass
            //
            //  PRINCIPLE: The theme XAML already defines the exact
            //  transparent glass values. ClearOverrides() above removed
            //  any application-level overrides, so those theme values
            //  are now active. PublishAboutPalette is also skipped for
            //  this family (see ThemeManager.ApplyAccent guard).
            //
            //  This method ONLY sets what the theme XAML doesn't:
            //    1. Aurora stage (window background)
            //    2. Crystal shadows
            //    3. Crystal borders (OutlineBrush)
            //    4. Typography
            //    5. Corner radii
            //    6. Status tints
            //    7. Hover + interaction states
            //    8. Liquid glass edge/reflection tokens
            //
            //  Everything else — ModuleBackground, GlassUltraClear,
            //  LiquidCard, AboutInteractive, ElevatedSurface — flows
            //  through from the theme XAML untouched.
            // ══════════════════════════════════════════════════════════

            // ── 1) WINDOW — aurora stage (blue liquid wallpaper) ───
            resources["WindowBackgroundBrush"] =
                BuildLiquidStageBrush(dark, accent);

            // ── 2) SHADOWS — theme XAML defines water-weight shadows ──
            // (Deep, spread shadows that simulate water weight inside glass.
            //  Defined in theme XAML, flow through via ClearOverrides.)

            // ── 3) CRYSTAL BORDER — 36% white (was 24%): with the
            // wallpaper showing through, a stronger crystal edge is
            // what keeps every card's shape readable. ────────────────
            var crystalBorder = new SolidColorBrush(
                Color.FromArgb(0x5C, 0xFF, 0xFF, 0xFF));
            crystalBorder.Freeze();

            var crystalBorderGradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0x6E, 0xFF, 0xFF, 0xFF), 0.0),
                    new GradientStop(Color.FromArgb(0x5C, 0xFF, 0xFF, 0xFF), 0.5),
                    new GradientStop(Color.FromArgb(0x4A, 0xFF, 0xFF, 0xFF), 1.0)
                }
            };
            crystalBorderGradient.Freeze();
            resources["GlassBorderGradientCrystal"] = crystalBorderGradient;

            resources["OutlineBrush"] = crystalBorder;
            resources["OutlineDefaultBrush"] = crystalBorder;
            resources["BorderBrush"] = crystalBorder;
            resources["DividerBrush"] = new SolidColorBrush(
                Color.FromArgb(0x2C, 0xFF, 0xFF, 0xFF));
            resources["GlassBorderSoft"] = crystalBorder;

            // ── 4) TYPOGRAPHY ──────────────────────────────────────
            // 2026-09-01 cream pass: Ultra light text was cool slate
            // (#1E293B / #334155 / #475569) — now the same warm ink as
            // the Normal canvas over the crystal.
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

            // ── 5) SHAPE — larger radii ────────────────────────────
            resources["CardCornerRadius"] = new CornerRadius(16);
            resources["CardCornerRadiusLarge"] = new CornerRadius(20);
            resources["ControlCornerRadius"] = new CornerRadius(12);

            // ── 6) STATUS TINTS — barely visible hue ───────────────
            resources["TintIndigoBrush"] = HueFrost("TintIndigoBrush", 0.10, 0.05);
            resources["TintBlueBrush"] = HueFrost("TintBlueBrush", 0.10, 0.05);
            resources["TintGreenBrush"] = HueFrost("TintGreenBrush", 0.10, 0.05);
            resources["TintRedBrush"] = HueFrost("TintRedBrush", 0.10, 0.05);
            resources["TintAmberBrush"] = HueFrost("TintAmberBrush", 0.10, 0.05);
            resources["TintPurpleBrush"] = HueFrost("TintPurpleBrush", 0.10, 0.05);
            resources["TintGrayBrush"] = HueFrost("TintGrayBrush", 0.10, 0.05);

            // ── 6b) CARD FROST — a whisper of white over the stage so
            // text on every card stays readable against a bright or
            // busy wallpaper (the family stays see-through: the
            // frost is 6–12% alpha, the crystal edge does the shape).
            resources["ModuleBackgroundBrush"] =
                FrostFill(accent, dark ? 0.14 : 0.12, dark ? 0.07 : 0.06);

            // Sidebar / dialogs need their own whisper of frost too —
            // they used the base-theme fills, which were OPAQUE cream
            // in Light, so the Ultra family never actually reached the
            // settings rail and popups (2026-09-01 section adoption).
            resources["SideMenuBackgroundBrush"] =
                FrostFill(accent, dark ? 0.18 : 0.16, dark ? 0.10 : 0.08);
            resources["ElevatedSurfaceBrush"] =
                FrostFill(accent, dark ? 0.24 : 0.22, dark ? 0.14 : 0.12);

            // Sections accept Ultra Transparent: the shared glass set
            // mirrors the crystal whisper (pure white crystal edges).
            PublishSectionGlass(resources, Colors.White, dark);

            // ── 7) HOVER — 14% white ───────────────────────────────
            var hoverFill = new SolidColorBrush(
                Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF));
            hoverFill.Freeze();
            resources["MenuHoverBrush"] = hoverFill;

            // ── 8) LIQUID GLASS TOKENS — edge + reflection only ────
            resources["LiquidEdgeBrush"] = CrystalEdge(accent, dark);
            resources["LiquidInnerHighlightBrush"] =
                HorizontalSheen(dark ? (byte)0x70 : (byte)0xDD);
            resources["LiquidReflectionBrush"] =
                DiagonalSheen(dark ? (byte)0x30 : (byte)0x66);
            resources["LiquidControlHoverBrush"] = hoverFill;

            var pressed = new SolidColorBrush(
                Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF));
            pressed.Freeze();
            resources["LiquidControlPressedBrush"] = pressed;

            var disabled = new SolidColorBrush(
                Color.FromArgb(0x0C, 0xFF, 0xFF, 0xFF));
            disabled.Freeze();
            resources["LiquidControlDisabledBrush"] = disabled;

            resources["LiquidFocusBrush"] = FrozenSolid(Color.FromArgb(
                0x50, accent.R, accent.G, accent.B));
            resources["LiquidSelectionBrush"] =
                AccentCrystal(accent, dark ? (0x28, 0x18) : (0x30, 0x1A));
            resources["LiquidPrimaryButtonBrush"] =
                AccentCrystal(accent, dark ? (0x70, 0x45) : (0x90, 0x55));

            resources["LiquidOverlayBrush"] = FrozenSolid(dark
                ? Color.FromArgb(0x99, 0x03, 0x10, 0x26)
                : Color.FromArgb(0x59, 0x06, 0x17, 0x24));
            resources["LiquidShadowColor"] = dark
                ? Color.FromArgb(0xFF, 0x00, 0x04, 0x10)
                : Color.FromArgb(0x4D, 0x0B, 0x1B, 0x2E);
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
                UltraTransparent => UltraTransparent,
                // Legacy families removed in the 2026-08-29 rewrite —
                // saved configs pointing at them resolve to the new
                // iOS Liquid Glass family instead of breaking.
                "Glass" => LiquidGlass,
                "Glassy" => LiquidGlass,
                "Transparent" => UltraTransparent,
                "Clear" => UltraTransparent,
                "Crystal" => UltraTransparent,
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
