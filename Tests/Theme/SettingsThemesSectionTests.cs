// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Theme/SettingsThemesSectionTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Settings "Themes" section (2026-09-06) — P3/P4 regression pins.
//
// Pins the P3 surface contract:
//   · the rail gains a "Themes" page keyed to Settings.SecThemes,
//   · SettingsView hosts SettingsThemesSection for the Themes page,
//   · the VM exposes the SIX live Crystal axes (and only those — the
//     parked glass/glass-border/shadow opacity axes must NOT surface),
//     plus Default/Calm/Intense presets and a per-family Reset,
//   · family awareness: the Crystal material group is gated on
//     IsCrystalThemeActive (Ios-17pm), and the non-Crystal hint uses the
//     inverse gate,
//   · the live preview reuses CrystalWaterControl (no second surface),
//   · every localization catalogue carries the new keys (en-US parity),
//   · the VM never writes the Crystal* tokens itself — it delegates to
//     ThemeCustomization.Apply (single writer, see ThemeCustomizationTests).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    public sealed class SettingsThemesSectionTests
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

        private static readonly string[] LiveAxes =
        {
            "CrystalWaterOpacity",
            "CrystalWaterSpeed",
            "CrystalWaveAmplitude",
            "CrystalCausticIntensity",
            "CrystalBubbleIntensity",
            "CrystalReflectionOpacity",
        };

        private static readonly string[] ParkedAxes =
        {
            "CrystalGlassOpacity",
            "CrystalGlassBorderOpacity",
            "CrystalShadowOpacity",
        };

        // ── 1. NAV: the rail gains a Themes page ─────────────────────────
        [Fact]
        public void Navigation_registry_adds_the_themes_page()
        {
            string nav = Read("ViewModels/Settings/SettingsViewModel.Navigation.cs");
            Assert.Contains("new(\"Themes\"", nav, StringComparison.Ordinal);
            Assert.Contains("\"Settings.SecThemes\"", nav, StringComparison.Ordinal);
        }

        [Fact]
        public void SettingsView_hosts_the_themes_section_for_the_themes_page()
        {
            string shell = Read("Views/Settings/SettingsView.xaml");
            Assert.Contains("sections:SettingsThemesSection", shell, StringComparison.Ordinal);
            Assert.Contains("ConverterParameter=Themes", shell, StringComparison.Ordinal);
        }

        // ── 2. VM SURFACE: six live axes, named presets, per-family reset ─
        [Fact]
        public void ViewModel_exposes_exactly_the_six_live_crystal_axes()
        {
            string vm = Read("ViewModels/Settings/SettingsViewModel.ThemeCustomization.cs");

            foreach (var axis in LiveAxes)
                Assert.Contains($"public double {axis}", vm, StringComparison.Ordinal);

            foreach (var parked in ParkedAxes)
                Assert.DoesNotContain($"public double {parked}", vm, StringComparison.Ordinal);
        }

        [Fact]
        public void ViewModel_exposes_named_presets_and_per_family_reset()
        {
            string vm = Read("ViewModels/Settings/SettingsViewModel.ThemeCustomization.cs");
            Assert.Contains("ApplyThemePresetCommand", vm, StringComparison.Ordinal);
            Assert.Contains("ResetThemeFamilyCommand", vm, StringComparison.Ordinal);

            // per-family: Crystal → baselines; Normal/Monet → default card shape
            Assert.Contains("ThemeCustomization.Reset()", vm, StringComparison.Ordinal);
            Assert.Contains("CardCornerRadius = 12;", vm, StringComparison.Ordinal);
            Assert.Contains("CardBorderThickness = 1.5;", vm, StringComparison.Ordinal);
            Assert.Contains("UseGradientSurfaces = true;", vm, StringComparison.Ordinal);
        }

        [Fact]
        public void Family_gate_is_crystal_fluid_only()
        {
            string vm = Read("ViewModels/Settings/SettingsViewModel.ThemeCustomization.cs");
            Assert.Contains("public bool IsCrystalThemeActive", vm, StringComparison.Ordinal);
            Assert.Contains("SelectedVisualTheme == VisualThemeEngine.CrystalFluid",
                vm, StringComparison.Ordinal);
        }

        // ── 3. XAML: axes gated + live preview reuses CrystalWaterControl ─
        [Fact]
        public void Section_gates_the_crystal_group_on_the_family_flag()
        {
            string xaml = Read("Views/Settings/Sections/SettingsThemesSection.xaml");
            Assert.Contains("IsCrystalThemeActive", xaml, StringComparison.Ordinal);
            Assert.Contains("BoolToVisibilityConverter", xaml, StringComparison.Ordinal);
            Assert.Contains("InverseBoolToVisibilityConverter", xaml, StringComparison.Ordinal);
        }

        [Fact]
        public void Section_surfaces_only_the_six_live_axes()
        {
            string xaml = Read("Views/Settings/Sections/SettingsThemesSection.xaml");
            foreach (var axis in LiveAxes)
                Assert.Contains($"AutomationProperties.AutomationId=\"Settings.{axis}\"",
                    xaml, StringComparison.Ordinal);

            foreach (var parked in ParkedAxes)
                Assert.DoesNotContain(parked, xaml, StringComparison.Ordinal);
        }

        [Fact]
        public void Section_reuses_the_crystal_water_control_for_live_preview()
        {
            string xaml = Read("Views/Settings/Sections/SettingsThemesSection.xaml");
            Assert.Contains("crystal:CrystalWaterControl", xaml, StringComparison.Ordinal);
        }

        // ── 4. LOCALIZATION: en-US parity across every shipped catalogue ─
        [Fact]
        public void Every_language_catalogue_carries_the_new_themes_keys()
        {
            var root = Path.Combine(LocateProjectRoot(), "Themes", "Languages");
            var files = Directory.GetFiles(root, "*.xaml");
            Assert.Equal(15, files.Length);

            foreach (var file in files)
            {
                string xaml = File.ReadAllText(file);
                Assert.Contains("Settings.SecThemes", xaml, StringComparison.Ordinal);
                Assert.Contains("Settings.ThemePresetDefault", xaml, StringComparison.Ordinal);
                Assert.Contains("Settings.ThemePresetCalm", xaml, StringComparison.Ordinal);
                Assert.Contains("Settings.ThemePresetIntense", xaml, StringComparison.Ordinal);
                Assert.Contains("Settings.CrystalWaterOpacity", xaml, StringComparison.Ordinal);
                Assert.Contains("Settings.CrystalReflectionOpacity", xaml, StringComparison.Ordinal);
            }
        }

        // ── 5. SINGLE WRITER: the VM delegates, never paints directly ────
        [Fact]
        public void ViewModel_delegates_token_writes_to_the_single_writer()
        {
            string vm = Read("ViewModels/Settings/SettingsViewModel.ThemeCustomization.cs");
            Assert.Contains("ThemeCustomization.Apply(", vm, StringComparison.Ordinal);
            foreach (var axis in LiveAxes)
                Assert.DoesNotContain($"[\"{axis}\"]", vm, StringComparison.Ordinal);
        }

        // ── 6. BOUNDS: the six Min/Max pairs mirror the single number home ─
        [Fact]
        public void Axis_bound_properties_delegate_to_theme_customization_consts()
        {
            string vm = Read("ViewModels/Settings/SettingsViewModel.ThemeCustomization.cs");
            foreach (var axis in LiveAxes)
            {
                // "CrystalWaterOpacity" → const suffix "WaterOpacity"
                string suffix = axis.Substring("Crystal".Length);
                Assert.Contains($"{axis}Min => ThemeCustomization.Min{suffix}", vm,
                    StringComparison.Ordinal);
                Assert.Contains($"{axis}Max => ThemeCustomization.Max{suffix}", vm,
                    StringComparison.Ordinal);
            }
        }
    }
}
