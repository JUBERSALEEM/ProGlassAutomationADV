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
        public void Light_glassy_uses_the_white_water_glass_ramp_not_the_dark_ramp()
        {
            // The original defect: ApplyGlassy applied the dark ramp
            // (0.18→0.05 white) to light mode — cards vanished on the
            // pale stage. Light must keep its own high-alpha ramp, and
            // the dark ramp must remain untouched for dark mode.
            Assert.Contains("0.82, 0.48", Engine, StringComparison.Ordinal);
            Assert.Contains("0.90, 0.62", Engine, StringComparison.Ordinal);
            Assert.Contains("0.86, 0.55", Engine, StringComparison.Ordinal);
            Assert.Contains("0.18, 0.05", Engine, StringComparison.Ordinal); // dark kept
            Assert.Contains("0.30, 0.10", Engine, StringComparison.Ordinal); // dark kept
        }

        [Fact]
        public void Light_glassy_veil_is_thinner_than_dark_for_clear_blur()
        {
            // Light windows let the DWM blur read through more clearly
            // (0x30 vs dark 0x40) — the white water-glass identity.
            Assert.Contains("dark ? (byte)0x40 : (byte)0x30", Engine, StringComparison.Ordinal);
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
            // The engine's BuildGlassStageBrush copies the About
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
