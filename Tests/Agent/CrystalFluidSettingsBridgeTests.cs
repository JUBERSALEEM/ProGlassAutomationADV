// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/CrystalFluidSettingsBridgeTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// IOS-17PM P3 — settings/studio bridge pins (2026-09-05).
//
// The authored CrystalFluidSettings surface (incoming file 19) is
// integrated under D31: NEVER a second source of truth. These tests pin
// the contract:
//   · Baseline* consts hold the AUTHORED numbers and are the single
//     number home consumed by the engine baseline block and the studio
//     master mapping,
//   · the façade exposes the authored API read-live (resource-backed
//     axes) + exactly one two-way property (ReducedMotion, config truth),
//   · the studio master mapping is recalibrated so FULL slider ==
//     authored baseline (no more multiplier-vs-baseline drift),
//   · the CrystalWaveAmplitude axis is end-to-end (token → engine
//     baseline → studio map → style setter → cleanup list),
//   · the bubble axis is ABSOLUTE authored intensity (0.70), not a
//     multiplier,
//   · the family IDENTITY "Crystal Fluid" is untouchable (persisted),
//     while the display name is Ios-17pm (view-only).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Tests.TestSupport;
using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class CrystalFluidSettingsBridgeTests
    {
        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(LocateProjectRoot(),
                relative.Replace('/', Path.DirectorySeparatorChar)));

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "ProGlassAutomation.csproj")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new FileNotFoundException("Project root not found");
        }

        // ── 1. AUTHORED BASELINES (real values, executable) ─────────────
        [Fact]
        public void Baseline_consts_hold_the_authored_numbers()
        {
            Assert.Equal(1.0,  CrystalFluidSettings.BaselineGlassOpacity);
            Assert.Equal(0.72, CrystalFluidSettings.BaselineGlassBorderOpacity);
            Assert.Equal(0.80, CrystalFluidSettings.BaselineReflectionOpacity);
            Assert.Equal(1.0,  CrystalFluidSettings.BaselineWaterOpacity);
            Assert.Equal(1.0,  CrystalFluidSettings.BaselineWaterSpeed);
            Assert.Equal(3.0,  CrystalFluidSettings.BaselineWaveAmplitude);
            Assert.Equal(0.70, CrystalFluidSettings.BaselineCausticIntensity);
            Assert.Equal(0.70, CrystalFluidSettings.BaselineBubbleIntensity);
            Assert.Equal(0.35, CrystalFluidSettings.BaselineShadowOpacity);
            Assert.Equal(30.0, CrystalFluidSettings.BaselineCornerRadiusLarge);
            Assert.False(CrystalFluidSettings.BaselineReducedMotion);
            Assert.Equal(1.0,  CrystalFluidSettings.BaselineAnimationIntensity);
        }

        // ── 2. AUTHORED API SURFACE (file-19 property set) ──────────────
        [Fact]
        public void Facade_exposes_the_authored_property_surface()
        {
            var t = typeof(CrystalFluidSettings);
            foreach (var p in new[]
            {
                "GlassOpacity", "GlassBorderOpacity", "ReflectionOpacity",
                "WaterOpacity", "WaterSpeed", "WaveAmplitude",
                "CausticIntensity", "BubbleIntensity",
                "ShadowOpacity", "CornerRadiusLarge",
                "ReducedMotion", "AnimationIntensity",
            })
                Assert.NotNull(t.GetProperty(p));
        }

        // ── 3. D31: resource-backed axes are read-live (get-only);
        //    ReducedMotion is the single writable (config-truth) axis ────
        [Fact]
        public void Slaved_axes_are_get_only_reduced_motion_is_writable()
        {
            var t = typeof(CrystalFluidSettings);
            foreach (var p in new[]
            {
                "WaterOpacity", "WaterSpeed", "WaveAmplitude",
                "CausticIntensity", "BubbleIntensity", "ReflectionOpacity",
            })
                Assert.False(t.GetProperty(p)!.CanWrite,
                    $"{p} must stay read-live — writes belong to the " +
                    "studio master mapping, never a parallel setter.");

            Assert.True(t.GetProperty(nameof(CrystalFluidSettings.ReducedMotion))!
                .CanWrite, "ReducedMotion is the two-way config-backed axis.");
        }

        // ── 4. SINGLE NUMBER HOME: engine baselines consume the consts ──
        [Fact]
        public void Engine_baseline_block_consumes_the_baseline_consts()
        {
            string engine = Read("Services/Theme/VisualThemeEngine.Part3.cs");

            foreach (var c in new[]
            {
                "CrystalFluidSettings.BaselineWaterOpacity",
                "CrystalFluidSettings.BaselineWaterSpeed",
                "CrystalFluidSettings.BaselineCausticIntensity",
                "CrystalFluidSettings.BaselineBubbleIntensity",
                "CrystalFluidSettings.BaselineReflectionOpacity",
                "CrystalFluidSettings.BaselineWaveAmplitude",
            })
                Assert.Contains(c, engine, StringComparison.Ordinal);
        }

        // ── 5. STUDIO RECALIBRATION: full slider = authored baseline ────
        [Fact]
        public void Studio_master_mapping_anchors_on_baseline_consts()
        {
            string studio = Read("Services/Theme/ThemeStudioService.cs");

            Assert.Contains("CrystalWaveAmplitude", studio,
                StringComparison.Ordinal);
            foreach (var c in new[]
            {
                "BaselineWaterOpacity", "BaselineWaterSpeed",
                "BaselineCausticIntensity", "BaselineBubbleIntensity",
                "BaselineReflectionOpacity", "BaselineWaveAmplitude",
                "FloorWaterOpacity", "FloorWaterSpeed",
                "FloorCausticIntensity", "FloorBubbleIntensity",
                "FloorReflectionOpacity", "FloorWaveAmplitude",
            })
                Assert.Contains($"CrystalFluidSettings.{c}", studio,
                    StringComparison.Ordinal);

            // the retired multiplier endpoints must not come back
            Assert.DoesNotContain("0.60 + 0.80 * water", studio,
                StringComparison.Ordinal);
            Assert.DoesNotContain("0.25 + 0.75 * water", studio,
                StringComparison.Ordinal);
            // reflection rides the absolute HighlightStrength axis now;
            // ("0.15 + 0.85 * Clamp01(" legitimately stays — it is the
            // GLOBAL hlScale glow-brush formula, untouched by P3)
            Assert.Contains("* highlight;", studio, StringComparison.Ordinal);
            Assert.Contains("(CrystalFluidSettings.BaselineReflectionOpacity",
                studio, StringComparison.Ordinal);
        }

        // ── 6. BUBBLE AXIS IS ABSOLUTE (0.70 authored intensity) ────────
        [Fact]
        public void Bubble_axis_is_absolute_authored_intensity()
        {
            string tokens = Read("Themes/CrystalFluid/CrystalFluidTokens.xaml");
            string control = Read(
                "Controls/CrystalFluid/Controls/CrystalWaterControl.cs");

            Assert.Contains(
                "<sys:Double x:Key=\"CrystalBubbleOpacity\">0.70</sys:Double>",
                tokens, StringComparison.Ordinal);
            // multiplier math is gone — the token/engine/studio express
            // the authored peak opacity directly
            Assert.DoesNotContain("0.7 *", control, StringComparison.Ordinal);
        }

        // ── 7. WAVE AMPLITUDE AXIS END-TO-END ───────────────────────────
        [Fact]
        public void Wave_amplitude_axis_is_wired_end_to_end()
        {
            string tokens = Read("Themes/CrystalFluid/CrystalFluidTokens.xaml");
            string controls = Read("Themes/CrystalFluid/CrystalFluidControls.xaml");

            Assert.Contains(
                "<sys:Double x:Key=\"CrystalWaveAmplitude\">3.0</sys:Double>",
                tokens, StringComparison.Ordinal);
            Assert.Contains(
                "Value=\"{DynamicResource CrystalWaveAmplitude}\"",
                controls, StringComparison.Ordinal);

            // engine cleanup list covers the new key (reflection, same
            // pattern as ThemeRegistryParityTests)
            var field = typeof(VisualThemeEngine).GetField(
                "CrystalFluidTokenKeys",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static);
            Assert.NotNull(field);
            var keys = (string[])field!.GetValue(null)!;
            Assert.Contains("CrystalWaveAmplitude", keys);
            Assert.Equal(7, keys.Length);
        }

        // ── 8. STUDIO-WRITTEN OPACITIES BIND DYNAMICALLY IN TEMPLATES ───
        [Fact]
        public void Template_opacities_follow_studio_writes_live()
        {
            string controls = Read("Themes/CrystalFluid/CrystalFluidControls.xaml");

            // markupextension allows a newline between class and member;
            // the XAML writers legitimately wrap these attributes
            Assert.Single(Regex.Matches(controls,
                @"\{DynamicResource\s+CrystalCausticOpacity\}"));
            Assert.Equal(2, Regex.Matches(controls,
                @"\{DynamicResource\s+CrystalReflectionOpacity\}").Count);

            // zero StaticResource forms of either studio-written axis
            Assert.Empty(Regex.Matches(controls,
                @"StaticResource\s+CrystalCausticOpacity"));
            Assert.Empty(Regex.Matches(controls,
                @"StaticResource\s+CrystalReflectionOpacity"));
        }

        // ── 9. DISPLAY NAME VS PERSISTED IDENTITY ───────────────────────
        [Fact]
        public void Crystal_fluid_identity_is_stable_display_is_ios17pm()
        {
            // persisted identity can never be renamed in place
            Assert.Equal("Crystal Fluid", VisualThemeEngine.CrystalFluid);

            // view-layer display name is the authored theme name
            Assert.Equal("Ios-17pm",
                VisualThemeEngine.FamilyDisplayName(
                    VisualThemeEngine.CrystalFluid));
            Assert.Equal(VisualThemeEngine.Ocean,
                VisualThemeEngine.FamilyDisplayName(VisualThemeEngine.Ocean));

            string scenes = Read("Services/Theme/BuiltinWallpaperScenes.cs");
            Assert.DoesNotContain("(VisualThemeEngine.CrystalFluid, \"Crystal Fluid\")",
                scenes, StringComparison.Ordinal);
            string wallpaper = Read(
                "ViewModels/Settings/SettingsViewModel.ThemeWallpaper.cs");
            Assert.Contains("FamilyDisplayName(key)", wallpaper,
                StringComparison.Ordinal);
        }

        // ── 10. CRYSTAL STUDIO DEFAULT PROFILE UNTOUCHED ────────────────
        [Fact]
        public void Crystal_default_studio_profile_keeps_its_tuned_sliders()
        {
            var d = ThemeStudioSettings.DefaultsFor(VisualThemeEngine.CrystalFluid);
            Assert.Equal(85, d.WaterFlow);
            Assert.Equal(70, d.HighlightStrength);
        }
    }
}
