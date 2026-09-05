// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ThemeStudioSourceTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// THEME STUDIO (2026-09-02) — source-level regression pins.
//
// The Theme Studio is a live 0–100 appearance layer over the existing
// VisualThemeEngine. These tests pin the architecture so a future
// refactor cannot silently:
//   · drop the studio's managed key groups (surfaces would stop
//     responding to Transparency/Frostness),
//   · unpublish the density/animation tokens the sliders scale,
//   · remove the startup restore or the family-switch re-apply,
//   · reintroduce a StaticResource binding on a runtime-painted key
//     (the SheetStore selected-tile bug class — "not accepting theme").
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Tests.TestSupport;
using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class ThemeStudioSourceTests
    {
        private static string Read(string relative) =>
            SourceFamily.Read(LocateProjectRoot(), relative);

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

        [Fact]
        public void Studio_service_owns_the_four_surface_groups()
        {
            string svc = Read("Services/Theme/ThemeStudioService.cs");

            Assert.Contains("SurfaceKeys", svc);
            Assert.Contains("\"ModuleBackgroundBrush\"", svc);
            Assert.Contains("\"SideMenuBackgroundBrush\"", svc);
            Assert.Contains("\"ShellLiquidBrush\"", svc);

            Assert.Contains("GlassKeys", svc);
            Assert.Contains("\"LiquidGlassBrush\"", svc);

            Assert.Contains("ShadowKeys", svc);
            Assert.Contains("\"ShadowElevated\"", svc);

            Assert.Contains("BorderKeys", svc);
            Assert.Contains("\"OutlineBrush\"", svc);
        }

        [Fact]
        public void Studio_applies_through_the_existing_theme_manager_tokens()
        {
            string svc = Read("Services/Theme/ThemeStudioService.cs");

            // Density + animation sliders scale the SAME tokens the
            // card styles and GodViews consume.
            Assert.Contains("\"DensityCardPadding\"", svc);
            Assert.Contains("\"MotionDurationFast\"", svc);
            Assert.Contains("\"MotionDurationNorm\"", svc);
            Assert.Contains("\"MotionDurationSlow\"", svc);

            // Border/corner sliders reuse the classic manager calls so
            // the studio can never disagree with the classic sliders.
            Assert.Contains("ApplyCardBorderThickness", svc);
            Assert.Contains("ApplyCardCornerRadius", svc);
        }

        [Fact]
        public void Studio_profile_is_persisted_inside_appearance_settings()
        {
            string config = Read("Services/AI/AIConfig/AppConfig.cs");
            Assert.Contains("public ThemeStudioSettings? Studio", config);

            string restore = Read("App.xaml.cs");
            Assert.Contains("ThemeStudioService.Restore()", restore);
        }

        [Fact]
        public void Family_and_theme_switches_reapply_the_studio_layer()
        {
            string appearanceService = Read("Services/Settings/AppearanceService.cs");
            Assert.Contains("ThemeStudioService.OnFamilyApplied()", appearanceService);

            string wallpaper = Read("ViewModels/Settings/SettingsViewModel.ThemeWallpaper.cs");
            Assert.Contains("ThemeStudioService", wallpaper);
            Assert.Contains("OnFamilyApplied", wallpaper);
        }

        [Fact]
        public void ViewModel_exposes_all_sixteen_studio_sliders()
        {
            string vm = Read("ViewModels/Settings/SettingsViewModel.ThemeStudio.cs");

            Assert.Contains("public double StudioFrostness", vm);
            Assert.Contains("public double StudioTransparency", vm);
            Assert.Contains("public double StudioBlurIntensity", vm);
            Assert.Contains("public double StudioGlassTint", vm);
            Assert.Contains("public double StudioCornerRoundness", vm);
            Assert.Contains("public double StudioCardPadding", vm);
            Assert.Contains("public double StudioShadowIntensity", vm);
            Assert.Contains("public double StudioShadowSoftness", vm);
            Assert.Contains("public double StudioShadowDepth", vm);
            Assert.Contains("public double StudioBorderVisibility", vm);
            Assert.Contains("public double StudioBorderOpacity", vm);
            Assert.Contains("public double StudioBorderThickness", vm);
            Assert.Contains("public double StudioBackgroundVisibility", vm);
            Assert.Contains("public double StudioBackgroundGlow", vm);
            Assert.Contains("public double StudioHighlightStrength", vm);
            Assert.Contains("public double StudioAnimationIntensity", vm);

            Assert.Contains("ResetStudioThemeCommand", vm);
            Assert.Contains("ResetStudioAllCommand", vm);
            Assert.Contains("StudioPresetOptions", vm);
        }

        [Fact]
        public void SheetStore_selected_tile_binds_live_brushes_dynamically()
        {
            // Regression pin: these two keys are repainted at runtime
            // (accent manager + theme engine). StaticResource here froze
            // the selected sheet tile at load time — the reported
            // "not accepting theme" defect class.
            string sheetStore = Read("Views/SheetStore/SheetStoreView.xaml");
            Assert.Contains(
                "{DynamicResource LiquidSelectionBrush}", sheetStore);
            Assert.Contains(
                "{DynamicResource AccentBrush}", sheetStore);
            Assert.DoesNotContain(
                "{StaticResource LiquidSelectionBrush}", sheetStore);
        }

        [Fact]
        public void Per_family_defaults_and_presets_are_centralized()
        {
            string settings = Read("Services/Theme/ThemeStudioSettings.cs");

            Assert.Contains("DefaultsFor", settings);
            Assert.Contains("VisualThemeEngine.LiquidGlass", settings);
            Assert.Contains("VisualThemeEngine.CrystalFluid", settings);
            Assert.Contains("VisualThemeEngine.Ocean", settings);
            Assert.Contains("VisualThemeEngine.Monet", settings);
            Assert.Contains("VisualThemeEngine.Professional", settings);

            Assert.Contains("public static ThemeStudioSettings Preset", settings);
            Assert.Contains("\"Crystal\"", settings);
            Assert.Contains("\"LightGlass\"", settings);
            Assert.Contains("\"Balanced\"", settings);
            Assert.Contains("\"Frosted\"", settings);
            Assert.Contains("\"DenseGlass\"", settings);
        }
    }
}
