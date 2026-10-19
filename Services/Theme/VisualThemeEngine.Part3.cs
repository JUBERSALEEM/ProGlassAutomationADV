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
            resources["BackgroundBrush"] = stage;

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
            // Crystal Fluid = the water family: KPI / StatCard hosts
            // bind CrystalWaterDefaultEnabled. Part1 publishes FALSE
            // before every family switch so water cannot leak; this
            // painter is the only place that turns it back on — which
            // is what makes iPhone 17 Pro Max distinct from iOS 26
            // (frosted grouped-grey, no water).
            resources["CrystalWaterDefaultEnabled"] = true;
            resources["CrystalAmbientHostVisibility"] = Visibility.Visible;
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

        /// <summary>
        /// NORMAL (persisted as "Normal (Industrial)") — the 25 August
        /// cream SaaS look, restored in full. Warm canvas #F6F1E6,
        /// paper cards #FEFCF8/#FEFDFA, warm stone ink #1C1917 /
        /// #57534E / #78716C, hairline #D8D0C2, SaaS radii 12/16/10.
        /// Solid opaque surfaces, never frosted glass, never industrial
        /// slate. Identity string stays "Normal (Industrial)" because it
        /// is a persisted key.
        /// </summary>
        private static void ApplyIndustrial(ResourceDictionary resources)
        {
            bool dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            // ── 1) WINDOW — cream studio stage (August-25 LightTheme).
            resources["WindowBackgroundBrush"] = FrozenSolid(dark
                ? Color.FromRgb(0x0A, 0x12, 0x26)
                : Color.FromRgb(0xF6, 0xF1, 0xE6));
            resources["BackgroundBrush"] = resources["WindowBackgroundBrush"];

            Color surface = dark
                ? Color.FromRgb(0x10, 0x1A, 0x2E)
                : Color.FromRgb(0xFE, 0xFC, 0xF8);
            Color card = dark
                ? Color.FromRgb(0x16, 0x22, 0x38)
                : Color.FromRgb(0xFE, 0xFD, 0xFA);
            Color elevated = dark
                ? Color.FromRgb(0x16, 0x22, 0x38)
                : Color.FromRgb(0xFE, 0xFD, 0xFA);
            Color side = dark
                ? Color.FromRgb(0x10, 0x1A, 0x2E)
                : Color.FromRgb(0xFB, 0xF8, 0xF1);
            Color hover = dark
                ? Color.FromRgb(0x16, 0x22, 0x38)
                : Color.FromRgb(0xF2, 0xED, 0xE3);

            resources["ModuleBackgroundBrush"] = FrozenSolid(surface);
            resources["ElevatedSurfaceBrush"] = FrozenSolid(elevated);
            resources["SideMenuBackgroundBrush"] = FrozenSolid(side);
            resources["MenuHoverBrush"] = FrozenSolid(hover);

            // ── 2) CARD EDGE — warm cream hairline, never cool slate.
            Color edge = dark
                ? Color.FromRgb(0x5C, 0x57, 0x4C)
                : Color.FromRgb(0xD8, 0xD0, 0xC2);
            Color edgeStrong = dark
                ? Color.FromRgb(0x7C, 0x76, 0x68)
                : Color.FromRgb(0xC4, 0xB8, 0xA6);
            resources["GlassBorderGradientCrystal"] = FrozenSolid(edge);
            resources["GlassBorderSoft"] = FrozenSolid(edge);
            resources["GlassBorderPrimary"] = FrozenSolid(edgeStrong);
            resources["GlassBorderBright"] = FrozenSolid(edgeStrong);

            resources["GlassUltraClearBrush"] = FrozenSolid(card);
            resources["GlassSurfaceBrush"] = FrozenSolid(card);
            resources["GlassCrystalBrush"] = FrozenSolid(card);
            resources["GlassFrostedBrush"] = FrozenSolid(card);
            resources["GlassSurfaceStandard"] = FrozenSolid(hover);
            resources["GlassSurfaceUltra"] = FrozenSolid(side);
            resources["GlassSurfaceElevated"] = FrozenSolid(elevated);
            resources["GlassWaterBrush"] = FrozenArgb(0x00, 0x00, 0x00, 0x00);

            resources["ShellLiquidBrush"] = FrozenSolid(side);
            resources["SidebarCardBrush"] = FrozenSolid(hover);
            resources["DashLiquidSurfaceBrush"] = FrozenSolid(card);
            resources["DashAmbientBrush"] = FrozenArgb(dark ? (byte)0x18 : (byte)0x14,
                0x2A, 0x21, 0x18);
            resources["DashAmbientBlob1Brush"] = FrozenArgb(0x10, 0x2A, 0x21, 0x18);
            resources["DashAmbientBlob2Brush"] = FrozenArgb(0x0C, 0x57, 0x53, 0x4E);

            // ── 3) TYPOGRAPHY — warm ink of the August-25 cream canvas.
            resources["PrimaryForegroundBrush"] = FrozenSolid(dark
                ? Color.FromRgb(0xF5, 0xF5, 0xF7)
                : Color.FromRgb(0x1C, 0x19, 0x17));
            resources["SecondaryForegroundBrush"] = FrozenSolid(dark
                ? Color.FromRgb(0xA9, 0xB8, 0xCC)
                : Color.FromRgb(0x57, 0x53, 0x4E));
            resources["MutedForegroundBrush"] = FrozenSolid(dark
                ? Color.FromRgb(0x7C, 0x8C, 0xA3)
                : Color.FromRgb(0x78, 0x71, 0x6C));
            resources["TextPrimaryBrush"] = FrozenSolid(dark
                ? Color.FromRgb(0xF5, 0xF5, 0xF7)
                : Color.FromRgb(0x1C, 0x19, 0x17));
            resources["TextSecondaryBrush"] = FrozenSolid(dark
                ? Color.FromArgb(0xB4, 0xE2, 0xEA, 0xF4)
                : Color.FromArgb(0x99, 0x57, 0x53, 0x4E));

            // ── 4) SHAPE — SaaS radii (Appearance default 12), not the
            // dense industrial 8/12/6 set.
            resources["CardCornerRadius"] = new CornerRadius(12);
            resources["CardCornerRadiusLarge"] = new CornerRadius(16);
            resources["ControlCornerRadius"] = new CornerRadius(10);

            // ── 5) STATUS TINTS — warm neutrals (Normal must never read
            // industrial blue).
            resources["TintBlueBrush"] = FrozenSolid(dark
                ? Color.FromRgb(0x23, 0x2B, 0x34)
                : Color.FromRgb(0xF2, 0xED, 0xE3));
            resources["TintIndigoBrush"] = FrozenSolid(dark
                ? Color.FromRgb(0x27, 0x2F, 0x3A)
                : Color.FromRgb(0xED, 0xE7, 0xDA));
            resources["TintGreenBrush"] = FrozenSolid(dark
                ? Color.FromRgb(0x14, 0x30, 0x1F)
                : Color.FromRgb(0xE8, 0xF6, 0xEC));
            resources["TintRedBrush"] = FrozenSolid(dark
                ? Color.FromRgb(0x3B, 0x1F, 0x22)
                : Color.FromRgb(0xFE, 0xF2, 0xF2));
            resources["TintAmberBrush"] = FrozenSolid(dark
                ? Color.FromRgb(0x3B, 0x2F, 0x1E)
                : Color.FromRgb(0xFF, 0xFB, 0xEB));
            resources["TintPurpleBrush"] = FrozenSolid(dark
                ? Color.FromRgb(0x2A, 0x25, 0x40)
                : Color.FromRgb(0xF5, 0xF3, 0xFF));
            resources["TintGrayBrush"] = FrozenSolid(dark
                ? Color.FromRgb(0x24, 0x2B, 0x3A)
                : Color.FromRgb(0xF6, 0xF3, 0xEE));

            // ── 6) SHADOWS — warm cream cast (#2A2118), never navy.
            resources["ShadowSoft"] = IndustrialShadow(dark
                ? Color.FromRgb(0x00, 0x04, 0x10)
                : Color.FromRgb(0x2A, 0x21, 0x18), dark ? 0.28 : 0.10, 20, 4);
            resources["ShadowElevated"] = IndustrialShadow(dark
                ? Color.FromRgb(0x00, 0x04, 0x10)
                : Color.FromRgb(0x2A, 0x21, 0x18), dark ? 0.36 : 0.14, 32, 8);

            var softShadow = IndustrialShadow(dark
                ? Color.FromRgb(0x00, 0x04, 0x10)
                : Color.FromRgb(0x2A, 0x21, 0x18), dark ? 0.10 : 0.05, 8, 2);
            resources["GlassShadowSoft"] = softShadow;
            resources["GlassShadowSoftBorder"] = softShadow;
            resources["GlassShadowSoftButton"] = softShadow;
            resources["GlassShadowSoftCheckBox"] = softShadow;
            resources["GlassShadowSoftTextBox"] = softShadow;
            resources["GlassShadowSoftToggle"] = softShadow;

            resources["LiquidGlassBrush"] = FrozenSolid(card);
            resources["LiquidGlassSubtleBrush"] = FrozenSolid(hover);
            resources["LiquidGlassStrongBrush"] = FrozenSolid(elevated);
        }

        /// <summary>Frozen drop-shadow helper used by the cream Normal
        /// painter (warm #2A2118 light / near-black dark).</summary>
        private static System.Windows.Media.Effects.DropShadowEffect
            IndustrialShadow(Color color, double opacity, double blur, double depth)
        {
            var fx = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = color,
                Opacity = opacity,
                BlurRadius = blur,
                ShadowDepth = depth,
                Direction = 270,
            };
            fx.Freeze();
            return fx;
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
            catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("TokenColor suppressed");
            }
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
            catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("TokenBrush suppressed");
            }
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
            // Runtime identity is intentionally limited to the eight selectable
            // families. Old saved profiles and imported theme packs are mapped
            // to the nearest current material instead of reviving hidden themes.
            return family?.Trim() switch
            {
                BentoGrid => BentoGrid,
                SpatialImmersive => SpatialImmersive,
                SwissInternational => SwissInternational,
                HolographicIridescent => HolographicIridescent,
                Neumorphism => Neumorphism,
                Claymorphism => Claymorphism,
                LiquidGlass => LiquidGlass,
                Glassmorphism => Glassmorphism,

                // Compatibility mappings for configurations/theme packs created
                // before the eight-family contract.
                Monet => Claymorphism,
                CrystalFluid or Ios26 => LiquidGlass,
                Normal or NormalIndustrial => BentoGrid,
                "Ultra Transparent" or "Transparent" or "Clear" or
                "Crystal" or "Crystal Sea" or "Ios-17pm" or
                "iPhone 17 Pro Max" => LiquidGlass,
                "Professional" or "Industrial" or "Industrial White" or
                "Industrial Grey" or "Industrial Dark" => BentoGrid,
                _ => LiquidGlass
            };
        }

        private static double ClampOpacity(double value) =>
            value < 0.3 ? 0.3 : value > 1.0 ? 1.0 : value;
    }
}
