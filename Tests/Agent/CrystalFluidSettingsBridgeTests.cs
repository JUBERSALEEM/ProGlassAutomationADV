// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/CrystalFluidSettingsBridgeTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// IOS-17PM P3 — settings/engine bridge pins (2026-09-05).
//
// The authored CrystalFluidSettings surface (incoming file 19) is
// integrated under D31: NEVER a second source of truth. These tests pin
// the contract:
//   · Baseline* consts hold the AUTHORED numbers and are the single
//     number home consumed by the engine baseline block,
//   · the façade exposes the authored API read-live (resource-backed
//     axes) + exactly one two-way property (ReducedMotion, config truth),
//   · the CrystalWaveAmplitude axis is end-to-end (token → engine
//     baseline → style setter → cleanup list),
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
                    "engine family paint, never a parallel setter.");

            Assert.True(t.GetProperty(nameof(CrystalFluidSettings.ReducedMotion))!
                .CanWrite, "ReducedMotion is the two-way config-backed axis.");
        }

        // ── 3b. CS8612 freeze: INPC nullability matches the interface ──
        [Fact]
        public void Inpc_declarations_match_the_annotated_interface()
        {
            // repo convention (LocalizationService, StatCardControl, all
            // FeatureVisibility classes): both the event and the helper
            // parameter are nullable — CS8612 must never return.
            string fac = Read("Services/Theme/CrystalFluidSettings.cs");
            Assert.Contains(
                "public event PropertyChangedEventHandler? PropertyChanged;",
                fac, StringComparison.Ordinal);
            Assert.Contains(
                "private void OnPropertyChanged([CallerMemberName] string? name = null)",
                fac, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "public event PropertyChangedEventHandler PropertyChanged;",
                fac, StringComparison.Ordinal);
        }

        // ── 4. SINGLE NUMBER HOME: customization defaults consume the consts ──
        [Fact]
        public void Engine_baseline_block_consumes_the_baseline_consts()
        {
            // 2026-09-06 theme customization: the Crystal axis defaults
            // moved to ThemeCustomization (the engine delegates via
            // ThemeCustomization.Apply). The Baseline* consts stay the
            // single number home — now consumed by ThemeCustomization.
            string customization = Read("Services/Theme/ThemeCustomization.cs");
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
            {
                Assert.Contains(c, customization, StringComparison.Ordinal);
            }

            Assert.Contains("ThemeCustomization.Apply(resources)",
                engine, StringComparison.Ordinal);
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
            // multiplier math is gone — the token/engine express the
            // authored peak opacity directly
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

        // ── 8. ENGINE-WRITTEN OPACITIES BIND DYNAMICALLY IN TEMPLATES ───
        [Fact]
        public void Template_opacities_follow_engine_writes_live()
        {
            string controls = Read("Themes/CrystalFluid/CrystalFluidControls.xaml");

            // markupextension allows a newline between class and member;
            // the XAML writers legitimately wrap these attributes
            Assert.Single(Regex.Matches(controls,
                @"\{DynamicResource\s+CrystalCausticOpacity\}"));
            Assert.Equal(2, Regex.Matches(controls,
                @"\{DynamicResource\s+CrystalReflectionOpacity\}").Count);

            // zero StaticResource forms of either engine-written axis
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
            // non-crystal families surface their identity verbatim
            Assert.Equal(VisualThemeEngine.Normal,
                VisualThemeEngine.FamilyDisplayName(VisualThemeEngine.Normal));
            Assert.Equal(VisualThemeEngine.Monet,
                VisualThemeEngine.FamilyDisplayName(VisualThemeEngine.Monet));

            string scenes = Read("Services/Theme/BuiltinWallpaperScenes.cs");
            Assert.DoesNotContain("(VisualThemeEngine.CrystalFluid, \"Crystal Fluid\")",
                scenes, StringComparison.Ordinal);
            string wallpaper = Read(
                "ViewModels/Settings/SettingsViewModel.ThemeWallpaper.cs");
            Assert.Contains("FamilyDisplayName(key)", wallpaper,
                StringComparison.Ordinal);
        }
    }
}
