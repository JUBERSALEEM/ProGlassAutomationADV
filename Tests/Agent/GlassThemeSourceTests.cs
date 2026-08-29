// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/GlassThemeSourceTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Source-level invariants for the white water-glass theme
//         (2026-08-29, transparency pass 2)
//
// Pins the DESIGN of the Glass/Glassy families so a future edit cannot
// silently revert the light glass to opaque slabs or re-apply the dark
// ramp to light mode (the two defects that made the glass "completely
// broken").
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class GlassThemeSourceTests
    {
        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(Root(), relative));

        private static string Engine => Read(
            "Services/Theme/VisualThemeEngine.cs");

        private static string LightTheme => Read("Themes/LightTheme.xaml");

        [Fact]
        public void Liquid_glass_uses_white_water_glass_in_light_and_deep_frost_in_dark()
        {
            // The original defect: the light mode used the dark ramp —
            // cards vanished on the pale stage. The single iOS Liquid
            // Glass family keeps an intentional ramp per mode: white
            // water glass in Light, deep frost in Dark.
            Assert.Contains("0.84, 0.54", Engine, StringComparison.Ordinal);
            Assert.Contains("0.94, 0.72", Engine, StringComparison.Ordinal);
            Assert.Contains("0.88, 0.58", Engine, StringComparison.Ordinal);
            Assert.Contains("0.20, 0.06", Engine, StringComparison.Ordinal); // dark
            Assert.Contains("0.32, 0.11", Engine, StringComparison.Ordinal); // dark
        }

        [Fact]
        public void Liquid_glass_window_stage_is_opaque_and_self_contained()
        {
            // 2026-08-29 white-fade regression: an alpha veil over an
            // opaque WPF window surface blends toward the white clear
            // color on Windows 11 / transparency-off machines — the
            // whole app faded to a white sheet. The stage must be
            // opaque and visibly more colourful than the white panes.
            string engine = Engine;
            Assert.Contains("BuildLiquidStageBrush(", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("WithAlpha(baseA", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("WithAlpha(baseB", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("WithAlpha(stageA", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("WithAlpha(stageB", engine, StringComparison.Ordinal);
            Assert.Contains("0xD7, 0xE5, 0xF9", engine, StringComparison.Ordinal);
            Assert.Contains("0xAE, 0xCB, 0xF0", engine, StringComparison.Ordinal);
        }

        [Fact]
        public void Glass_and_glassy_are_removed_and_ios_liquid_glass_is_the_default_family()
        {
            // 2026-08-29 rewrite: the two confusing "Glass"/"Glassy"
            // families are GONE; one iOS Liquid Glass family replaces
            // them, adapts to Light/Dark, and is the app-wide default.
            string engine = Engine;
            Assert.DoesNotContain("public const string Glass =", engine, StringComparison.Ordinal);
            Assert.DoesNotContain("public const string Glassy =", engine, StringComparison.Ordinal);
            Assert.Contains("public const string LiquidGlass = \"iOS Liquid Glass\";", engine, StringComparison.Ordinal);
            Assert.Contains("\"Glass\" => LiquidGlass", engine, StringComparison.Ordinal);
            Assert.Contains("\"Glassy\" => LiquidGlass", engine, StringComparison.Ordinal);

            string config = Read("Services/AI/AIConfig/AppConfig.cs");
            Assert.Contains("= \"iOS Liquid Glass\";", config, StringComparison.Ordinal);

            string appCs = Read("App.xaml.cs");
            Assert.Contains("?? \"iOS Liquid Glass\"", appCs, StringComparison.Ordinal);

            string vm = Read("ViewModels/Settings/SettingsViewModel.cs");
            Assert.Contains("VisualThemeEngine.LiquidGlass,", vm, StringComparison.Ordinal);
            Assert.DoesNotContain("VisualThemeEngine.Glass,", vm, StringComparison.Ordinal);
            Assert.DoesNotContain("VisualThemeEngine.Glassy,", vm, StringComparison.Ordinal);
        }

        [Fact]
        public void Light_glass_card_is_translucent_white_not_opaque()
        {
            // The second original defect: GlassCard was #FFFFFFFF
            // (opaque white) — a solid slab, not glass.
            Assert.Contains("#DBFFFFFF", LightTheme, StringComparison.Ordinal);
            Assert.Contains("#BDFFFFFF", LightTheme, StringComparison.Ordinal);
            Assert.Contains("#9EFFFFFF", LightTheme, StringComparison.Ordinal);
            Assert.Contains("#8CFFFFFF", LightTheme, StringComparison.Ordinal); // specular sheen
        }

        [Fact]
        public void Light_orb_stops_stay_verbatim_identical_between_theme_and_engine()
        {
            // The engine's BuildLiquidStageBrush copies the About
            // orbs' stops; they must match the theme file exactly.
            Assert.Contains("#6660A5FA", LightTheme, StringComparison.Ordinal);
            Assert.Contains("#7A3B82F6", LightTheme, StringComparison.Ordinal);
            Assert.Contains("0x66, 0x60, 0xA5, 0xFA", Engine, StringComparison.Ordinal);
            Assert.Contains("0x7A, 0x3B, 0x82, 0xF6", Engine, StringComparison.Ordinal);
        }

        [Fact]
        public void Light_glass_text_is_decoupled_from_surface_keys()
        {
            // Light glass text uses fixed ink/slate constants; it must
            // never read the surface-named Glass* keys again (that
            // coupling would white-out text when surfaces are
            // recoloured).
            Assert.Contains("FrozenSolid(Color.FromRgb(0x33, 0x41, 0x55))", Engine, StringComparison.Ordinal);
            Assert.Contains("FrozenSolid(Color.FromRgb(0x1D, 0x4E, 0xD8))", Engine, StringComparison.Ordinal);
            Assert.Contains("FrozenSolid(Color.FromRgb(0x47, 0x55, 0x69))", Engine, StringComparison.Ordinal);
        }

        [Fact]
        public void Surface_keys_are_consumed_via_dynamic_resource_so_family_overrides_reach_cards()
        {
            // Runtime overrides (Application.Resources) only reach
            // consumers bound with DynamicResource. The card styles
            // must never switch back to StaticResource, or the Glass/
            // Glassy surfaces would silently stay opaque.
            string cardStyles = Read("Themes/Styles/CardStyles.xaml");
            string settings = Read("Views/Settings/SettingsSectionStyles.xaml");

            Assert.Contains("{DynamicResource ModuleBackgroundBrush}", cardStyles, StringComparison.Ordinal);
            Assert.Contains("{DynamicResource ModuleBackgroundBrush}", settings, StringComparison.Ordinal);
            Assert.DoesNotContain("{StaticResource ModuleBackgroundBrush}", cardStyles, StringComparison.Ordinal);
            Assert.DoesNotContain("{StaticResource ModuleBackgroundBrush}", settings, StringComparison.Ordinal);
        }

        [Fact]
        public void Liquid_glass_design_system_is_merged_and_tokens_are_defined_in_both_themes()
        {
            // The global liquid glass system (2026-08-29): one shared
            // dictionary merged in App.xaml; every token it consumes is
            // defined in BOTH theme files so Light/Dark switching and
            // the Glass/Glassy family overrides always resolve.
            string app = Read("App.xaml");
            string light = LightTheme;
            string dark = Read("Themes/DarkTheme.xaml");
            string styles = Read("Themes/Styles/LiquidGlassStyles.xaml");

            Assert.Contains("LiquidGlassStyles.xaml", app, StringComparison.Ordinal);
            Assert.Contains("Controls/LiquidGlassCard", styles, StringComparison.Ordinal);

            string[] tokens =
            {
                "LiquidGlassBrush", "LiquidGlassSubtleBrush", "LiquidGlassStrongBrush",
                "GlassEdgeBrush", "GlassInnerHighlightBrush", "GlassReflectionBrush",
                "GlassControlHoverBrush", "GlassControlPressedBrush", "GlassControlDisabledBrush",
                "GlassFocusBrush", "GlassSelectionBrush", "GlassPrimaryButtonBrush",
                "GlassOverlayBrush", "GlassShadowColor"
            };
            foreach (string token in tokens)
            {
                Assert.Contains("x:Key=\"" + token + "\"", light, StringComparison.Ordinal);
                Assert.Contains("x:Key=\"" + token + "\"", dark, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Liquid_glass_styles_never_pin_theme_colours_with_static_resource()
        {
            // Dynamic switching depends on DynamicResource — the design
            // system must never StaticResource the liquid glass tokens.
            string styles = Read("Themes/Styles/LiquidGlassStyles.xaml");
            // Style-key references (e.g. {StaticResource
            // LiquidGlassDataGridColumnHeaderStyle}) are legitimate;
            // the COLOUR/BRUSH tokens must never be static.
            foreach (string token in new[]
                     {
                         "LiquidGlassBrush", "LiquidGlassSubtleBrush", "LiquidGlassStrongBrush",
                         "GlassEdgeBrush", "GlassInnerHighlightBrush", "GlassReflectionBrush",
                         "GlassControlHoverBrush", "GlassControlPressedBrush", "GlassControlDisabledBrush",
                         "GlassFocusBrush", "GlassSelectionBrush", "GlassPrimaryButtonBrush",
                         "GlassOverlayBrush", "GlassShadowColor"
                     })
            {
                Assert.DoesNotContain("{StaticResource " + token + "}", styles, StringComparison.Ordinal);
            }
            Assert.Contains("{DynamicResource LiquidGlassBrush}", styles, StringComparison.Ordinal);
            Assert.Contains("{DynamicResource GlassEdgeBrush}", styles, StringComparison.Ordinal);
        }

        [Fact]
        public void Engine_publishes_the_liquid_glass_tokens_for_glass_families()
        {
            string engine = Engine;
            Assert.Contains("ApplyLiquidGlassTokens", engine, StringComparison.Ordinal);
            Assert.Contains("\"GlassEdgeBrush\"", engine, StringComparison.Ordinal);
            Assert.Contains("\"GlassSelectionBrush\"", engine, StringComparison.Ordinal);
            Assert.Contains("\"GlassPrimaryButtonBrush\"", engine, StringComparison.Ordinal);
            Assert.Contains("CrystalEdge", engine, StringComparison.Ordinal);
        }

        [Fact]
        public void Liquid_glass_card_control_exposes_intensity_and_radius()
        {
            string control = Read("Controls/LiquidGlassCard.cs");
            Assert.Contains("GlassIntensityProperty", control, StringComparison.Ordinal);
            Assert.Contains("enum GlassSurface", control, StringComparison.Ordinal);
            Assert.Contains("CornerRadiusProperty", control, StringComparison.Ordinal);
        }

        [Fact]
        public void Dark_glass_ramp_untouched_by_the_light_pass()
        {
            // Dark deep-frost materials are the original About look;
            // the light-transparency pass must never change them.
            string dark = Read("Themes/DarkTheme.xaml");
            Assert.Contains("#2EFFFFFF", dark, StringComparison.Ordinal);
            Assert.Contains("#0DFFFFFF", dark, StringComparison.Ordinal);
        }

        private static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Themes", "LightTheme.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate repository root.");
        }
    }
}
