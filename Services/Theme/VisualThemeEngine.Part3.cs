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
            //  REPLACES "Ultra Transparent" (retired). Light-only family
            //  (ModePairFor → false); the `dark` branches are defensive
            //  fallbacks for mid-mode-switch paints only.
            //
            //  Spec = the frozen inline reference (CRYSTAL_FLUID_DESIGN.md):
            //    · ink #102938 / muted #68808F / accent #55C9EE
            //    · glass = white .43→.14 @135° (never opaque)
            //    · 1px bright-white crystal border + inner hairline
            //    · blue luminous environment stage (#EEF6FF/#DBEDFF/#C6E4FF)
            //      (2026-09-06 richness pass — deeper blue than the pale
            //      #F8FDFF/#EDFAFF/#DFF5FF stage, for a "glass ERP" read)
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
                    // Ios-17pm P4a: stage colours are TOKENIZED
                    // (CrystalFluidBrushes.xaml, single number home);
                    // the literals stay as byte-exact fallbacks.
                    new GradientStop(dark
                        ? Color.FromRgb(0x0E, 0x22, 0x30)
                        : TokenColor(resources, "CrystalStageTopBrush",
                            Color.FromRgb(0xEE, 0xF6, 0xFF)), 0.0),
                    new GradientStop(dark
                        ? Color.FromRgb(0x0C, 0x1D, 0x2A)
                        : TokenColor(resources, "CrystalStageLightBrush",
                            Color.FromRgb(0xDB, 0xED, 0xFF)), 0.45),
                    new GradientStop(dark
                        ? Color.FromRgb(0x09, 0x16, 0x22)
                        : TokenColor(resources, "CrystalStageBaseBrush",
                            Color.FromRgb(0xC6, 0xE4, 0xFF)), 1.0),
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
            // Ios-17pm P4d: grid-line identity adopts the AUTHORED grid
            // row-border token (single home in CrystalFluidBrushes.xaml).
            // DividerBrush feeds ModuleList row lines and every section
            // divider; it is repainted from the neutral baseline by the
            // critical-guarantee pass on every family switch — no
            // authored grid tint can leak across families. Fallback is
            // the byte-exact pre-P4d literal.
            resources["DividerBrush"] = TokenBrush(resources,
                "CrystalGridRowBorderBrush")
                ?? FrozenArgb(0x33, 0xFF, 0xFF, 0xFF);
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
            // Ios-17pm P4c: the sidebar chip/accent tint adopts the
            // authored nav ACTIVE-fill identity (single home); fallback
            // is the pre-P4c frost formula, unchanged.
            resources["TintIndigoBrush"] = TokenBrush(resources,
                "CrystalNavActiveBgBrush")
                ?? HueFrost("TintIndigoBrush", 0.10, 0.05);
            resources["TintBlueBrush"] = HueFrost("TintBlueBrush", 0.10, 0.05);
            resources["TintGreenBrush"] = HueFrost("TintGreenBrush", 0.10, 0.05);
            resources["TintRedBrush"] = HueFrost("TintRedBrush", 0.10, 0.05);
            resources["TintAmberBrush"] = HueFrost("TintAmberBrush", 0.10, 0.05);
            resources["TintPurpleBrush"] = HueFrost("TintPurpleBrush", 0.10, 0.05);
            resources["TintGrayBrush"] = HueFrost("TintGrayBrush", 0.10, 0.05);

            // ── 6b) CARD FROST (§10/§39/§81) — layered alpha
            // white; the environment must stay perceptible, so module
            // surfaces are a whisper while elevated sheets (dialogs,
            // dropdown popups) sit one shelf higher for readability.
            // 2026-09-06 richness pass: alphas lifted and the FrostBrush
            // accent kiss deepened (Part2) so cards read as BLUE glass,
            // not pale white — the "glass ERP" identity.
            resources["ModuleBackgroundBrush"] =
                FrostFill(accent, dark ? 0.20 : 0.36, dark ? 0.12 : 0.18);

            resources["SideMenuBackgroundBrush"] =
                FrostFill(accent, dark ? 0.28 : 0.58, dark ? 0.16 : 0.32);
            resources["ElevatedSurfaceBrush"] =
                FrostFill(accent, dark ? 0.34 : 0.64, dark ? 0.20 : 0.42);

            // Sections adopt the crystal material — shared glass set
            // mirrors the bright white edge.
            PublishSectionGlass(resources, Colors.White, dark);

            // ── 7) HOVER (§26) — soft translucent veil.
            // Ios-17pm P4c: adopts the AUTHORED nav hover identity
            // (CrystalNavHoverBgBrush) — the single authored number
            // home for hover surfaces; the literal below is the
            // byte-exact pre-P4c fallback. MenuHoverBrush stays a
            // ManagedKey: every family switch removes + repaints it,
            // no authored nav tint can leak across families. ──────
            var hoverFill = TokenBrush(resources,
                "CrystalNavHoverBgBrush")
                ?? FrozenArgb(dark ? (byte)0x1A : (byte)0x2E,
                    0xFF, 0xFF, 0xFF);
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
            // Crystal Fluid = the water family: card/KPI surfaces that
            // bind CrystalWaterDefaultEnabled render CrystalWater.
            resources["CrystalWaterDefaultEnabled"] = true;
            // Ios-17pm P4a: the SHELL ambient host in MainWindow (the
            // authored drifting-light layer) is family-gated through
            // this resource — Visible under Crystal Fluid, REMOVED at
            // every family switch (Part1 ×2, beside the water gate) so
            // no crystal clocks tick under another family.
            resources["CrystalAmbientHostVisibility"] = Visibility.Visible;
            // Ios-17pm P4e: the settings preview's authored file-20
            // composition rides the same switch-removal contract.
            resources["CrystalPreviewHostVisibility"] = Visibility.Visible;

            // Token baselines (root-level): a family repaint ALWAYS
            // restores the crystal water tokens. 2026-09-06 theme
            // customization (Settings → Themes) replaces the Theme
            // Studio: the six live material axes now come from the
            // persisted, CLAMPED ThemeCustomization block (defaults =
            // the authored CrystalFluidSettings.Baseline* numbers, so an
            // uncustomized install paints byte-identical to before).
            // Reduced motion stays its own config-backed §43 flag.
            ThemeCustomization.Apply(resources);
            resources["CrystalReducedMotion"] =
                App.Config?.Appearance?.ReducedMotion == true;

            CrystalFluidSettings.NotifyRepainted();

            resources["LiquidShadowColor"] = dark
                ? Color.FromArgb(0xFF, 0x00, 0x04, 0x10)
                : Color.FromArgb(0x55, 0x1E, 0x5F, 0x7E);
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static void ClearOverrides(ResourceDictionary resources)
        {
            foreach (var key in ManagedKeys)
                resources.Remove(key);
        }

        /// <summary>Reads a tokenized brush colour with the authored
        /// literal as fallback (Ios-17pm P4a). The crystal stage/scene
        /// gradients live as single-home SolidColorBrush tokens in
        /// CrystalFluidBrushes.xaml; engine paints consume those tokens
        /// so future environment recalibrations are ONE XAML edit,
        /// never a code redeploy. The literal fallback is pinned
        /// byte-for-byte to the pre-P4a colour, so hosts without merged
        /// resources render the IDENTICAL stage.</summary>
        private static Color TokenColor(ResourceDictionary resources,
            string key, Color fallback)
        {
            try
            {
                if (resources?.Contains(key) == true &&
                    resources[key] is SolidColorBrush brush)
                    return brush.Color;
            }
            catch { /* a token lookup must never break a paint */ }
            return fallback;
        }

        /// <summary>P4c — the full-brush companion to TokenColor: hands
        /// the AUTHORED frozen brush instance through for role keys
        /// (MenuHoverBrush/Tint…), or null so the caller can keep its
        /// proven fallback. Authored brushes live in merged dictionaries
        /// and are inherently frozen at merge; sharing the instance for
        /// a runtime role key is the standard WPF alias pattern.</summary>
        private static SolidColorBrush TokenBrush(
            ResourceDictionary resources, string key)
        {
            try
            {
                if (resources?.Contains(key) == true &&
                    resources[key] is SolidColorBrush brush)
                    return brush;
            }
            catch { /* a token lookup must never break a paint */ }
            return null;
        }

        private static SolidColorBrush FrozenArgb(
            byte a, byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
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
                CrystalFluid => CrystalFluid,
                // Legacy transparent-glass names keep landing on Crystal
                // Fluid (Ios-17pm), never on a missing family.
                "Ultra Transparent" => CrystalFluid,
                "Transparent" => CrystalFluid,
                "Clear" => CrystalFluid,
                "Crystal" => CrystalFluid,
                "Crystal Sea" => CrystalFluid,
                // 2026-09-06 theme reduction (owner decision: keep Normal,
                // Monet, Ios-17pm): every removed family — Professional,
                // iOS Liquid Glass, Ocean, Industrial White/Grey/Dark — and
                // any unknown saved value resets to Normal (the new default).
                _ => Normal
            };
        }

        private static double ClampOpacity(double value) =>
            value < 0.3 ? 0.3 : value > 1.0 ? 1.0 : value;
    }
}
