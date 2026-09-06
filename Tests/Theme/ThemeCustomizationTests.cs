// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Theme/ThemeCustomizationTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Theme customization (2026-09-06) — the bounded, family-aware
//         Settings surface that replaces the removed Theme Studio.
//
// Pins the P1/P2 contract:
//   · the persisted block defaults are byte-identical to the authored
//     CrystalFluidSettings.Baseline* consts (single number home),
//   · the named presets are real (Default == baselines) and every preset
//     value sits inside its published axis bounds (so the Settings
//     sliders and the clamp can never disagree),
//   · the clamp is a hard floor/ceiling,
//   · the engine delegates the six live Crystal tokens to
//     ThemeCustomization.Apply (not a second set of literals).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Theme;
using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    public sealed class ThemeCustomizationTests
    {
        // ── 1. PERSISTED DEFAULTS == AUTHORED BASELINES ──────────────────
        [Fact]
        public void Persisted_defaults_equal_authored_baselines()
        {
            var c = new ProGlassAutomation.Services.AI.AIConfig
                .ThemeCustomizationSettings();

            Assert.Equal(CrystalFluidSettings.BaselineWaterOpacity,      c.WaterOpacity);
            Assert.Equal(CrystalFluidSettings.BaselineWaterSpeed,        c.WaterSpeed);
            Assert.Equal(CrystalFluidSettings.BaselineWaveAmplitude,     c.WaveAmplitude);
            Assert.Equal(CrystalFluidSettings.BaselineCausticIntensity,  c.CausticIntensity);
            Assert.Equal(CrystalFluidSettings.BaselineBubbleIntensity,   c.BubbleIntensity);
            Assert.Equal(CrystalFluidSettings.BaselineReflectionOpacity, c.ReflectionOpacity);
            Assert.Equal("Default", c.CrystalPreset);
        }

        // ── 2. PRESETS: Default == baselines; all within bounds ─────────
        [Fact]
        public void Default_preset_is_byte_identical_to_baselines()
        {
            var d = ThemeCustomization.FindPreset("Default");
            Assert.NotNull(d);

            Assert.Equal(CrystalFluidSettings.BaselineWaterOpacity,      d!.WaterOpacity);
            Assert.Equal(CrystalFluidSettings.BaselineWaterSpeed,        d.WaterSpeed);
            Assert.Equal(CrystalFluidSettings.BaselineWaveAmplitude,     d.WaveAmplitude);
            Assert.Equal(CrystalFluidSettings.BaselineCausticIntensity,  d.CausticIntensity);
            Assert.Equal(CrystalFluidSettings.BaselineBubbleIntensity,   d.BubbleIntensity);
            Assert.Equal(CrystalFluidSettings.BaselineReflectionOpacity, d.ReflectionOpacity);
        }

        [Fact]
        public void Every_preset_stays_within_published_bounds()
        {
            foreach (var p in ThemeCustomization.Presets)
            {
                Assert.InRange(p.WaterOpacity,      ThemeCustomization.MinWaterOpacity,      ThemeCustomization.MaxWaterOpacity);
                Assert.InRange(p.WaterSpeed,        ThemeCustomization.MinWaterSpeed,        ThemeCustomization.MaxWaterSpeed);
                Assert.InRange(p.WaveAmplitude,     ThemeCustomization.MinWaveAmplitude,     ThemeCustomization.MaxWaveAmplitude);
                Assert.InRange(p.CausticIntensity,  ThemeCustomization.MinCausticIntensity,  ThemeCustomization.MaxCausticIntensity);
                Assert.InRange(p.BubbleIntensity,   ThemeCustomization.MinBubbleIntensity,   ThemeCustomization.MaxBubbleIntensity);
                Assert.InRange(p.ReflectionOpacity, ThemeCustomization.MinReflectionOpacity, ThemeCustomization.MaxReflectionOpacity);
            }
        }

        [Fact]
        public void Bounds_cover_the_authored_baselines()
        {
            Assert.InRange(CrystalFluidSettings.BaselineWaterOpacity,      ThemeCustomization.MinWaterOpacity,      ThemeCustomization.MaxWaterOpacity);
            Assert.InRange(CrystalFluidSettings.BaselineWaterSpeed,        ThemeCustomization.MinWaterSpeed,        ThemeCustomization.MaxWaterSpeed);
            Assert.InRange(CrystalFluidSettings.BaselineWaveAmplitude,     ThemeCustomization.MinWaveAmplitude,     ThemeCustomization.MaxWaveAmplitude);
            Assert.InRange(CrystalFluidSettings.BaselineCausticIntensity,  ThemeCustomization.MinCausticIntensity,  ThemeCustomization.MaxCausticIntensity);
            Assert.InRange(CrystalFluidSettings.BaselineBubbleIntensity,   ThemeCustomization.MinBubbleIntensity,   ThemeCustomization.MaxBubbleIntensity);
            Assert.InRange(CrystalFluidSettings.BaselineReflectionOpacity, ThemeCustomization.MinReflectionOpacity, ThemeCustomization.MaxReflectionOpacity);
        }

        // ── 3. CLAMP IS A HARD FLOOR/CEILING ────────────────────────────
        [Fact]
        public void Clamp_enforces_floor_and_ceiling()
        {
            Assert.Equal(0.35, ThemeCustomization.Clamp(-1.0, 0.35, 1.25));
            Assert.Equal(1.25, ThemeCustomization.Clamp(99.0, 0.35, 1.25));
            Assert.Equal(0.80, ThemeCustomization.Clamp(0.80, 0.35, 1.25));
            Assert.Equal(0.35, ThemeCustomization.Clamp(0.35, 0.35, 1.25));
            Assert.Equal(1.25, ThemeCustomization.Clamp(1.25, 0.35, 1.25));
        }

        // ── 4. ENGINE DELEGATES TO THE SINGLE WRITER ────────────────────
        [Fact]
        public void Engine_applies_customization_through_the_single_writer()
        {
            // The six live Crystal tokens must be written by
            // ThemeCustomization.Apply — never a second set of literals in
            // the family painter (that drift is exactly the bug class the
            // removed Theme Studio caused).
            string engine = Read("Services/Theme/VisualThemeEngine.Part3.cs");
            Assert.Contains("ThemeCustomization.Apply(resources);",
                engine, StringComparison.Ordinal);

            // ReducedMotion stays a separate config-backed §43 flag.
            Assert.Contains("\"CrystalReducedMotion\"",
                engine, StringComparison.Ordinal);
        }

        // ── 5. PERSISTED BLOCK IS WIRED INTO AppearanceSettings ─────────
        [Fact]
        public void AppearanceSettings_owns_the_customization_block()
        {
            string config = Read("Services/AI/AIConfig/AppConfig.cs");
            Assert.Contains("public ThemeCustomizationSettings ThemeCustomization",
                config, StringComparison.Ordinal);
            Assert.Contains("= new();", config, StringComparison.Ordinal);
        }

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
    }
}
