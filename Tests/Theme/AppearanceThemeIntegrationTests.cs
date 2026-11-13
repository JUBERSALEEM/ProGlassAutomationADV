using System;
using System.IO;
using System.Xml;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    /// <summary>
    /// Source-level acceptance contract for the Appearance page as the
    /// first end-to-end test surface for the nine-family WPF theme engine.
    /// These tests intentionally avoid constructing WPF controls so they
    /// can run in the existing Windows test project without UI automation.
    /// </summary>
    public sealed class AppearanceThemeIntegrationTests
    {
        private static string Root()
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

        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(Root(),
                relative.Replace('/', Path.DirectorySeparatorChar)));

        [Fact]
        public void Appearance_page_hosts_the_live_family_preview_and_real_theme_picker()
        {
            string section = Read("Views/Settings/Sections/SettingsAppearanceSection.xaml");
            string layout = Read("Views/Settings/Sections/Cards/AppearanceThemeLayoutCard.xaml");

            var document = new XmlDocument();
            document.LoadXml(section);
            document.LoadXml(layout);

            Assert.Contains("AppearanceThemeLayoutCard", section, StringComparison.Ordinal);
            Assert.Contains("Settings.AppearanceThemeEngine", layout, StringComparison.Ordinal);
            Assert.Contains("ItemsSource=\"{Binding VisualThemeOptions}\"", layout, StringComparison.Ordinal);
            Assert.Contains("SelectedValue=\"{Binding SelectedVisualTheme, Mode=TwoWay}\"", layout, StringComparison.Ordinal);
            Assert.Contains("LIVE FAMILY PREVIEW", layout, StringComparison.Ordinal);
            Assert.Contains("Text=\"{Binding SelectedVisualTheme}\"", layout, StringComparison.Ordinal);
        }

        [Fact]
        public void Appearance_picker_and_palette_engine_cover_all_nine_families_in_both_modes()
        {
            string vm = Read("ViewModels/Settings/SettingsViewModel.ThemeWallpaper.cs");
            string engine = Read("Services/Theme/VisualThemeEngine.EightThemes.cs");
            string paletteResolver = engine.Substring(
                engine.IndexOf("ResolveEightThemePalette", StringComparison.Ordinal));

            foreach (var family in new[]
            {
                "BentoGrid", "SpatialImmersive", "SwissInternational",
                "HolographicIridescent", "Neumorphism", "Claymorphism",
                "LiquidGlass", "Glassmorphism", "FluentAcrylic"
            })
            {
                Assert.Contains($"new(ProGlassAutomation.Services.Theme.VisualThemeEngine.{family})",
                    vm, StringComparison.Ordinal);
                Assert.Contains($"{family} => dark", paletteResolver, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Family_selection_persists_and_runs_the_canonical_family_pipeline()
        {
            string vm = Read("ViewModels/Settings/SettingsViewModel.ThemeWallpaper.cs");
            string pipeline = Read("Services/Theme/ThemePipeline.cs");

            Assert.Contains("public string SelectedVisualTheme", vm, StringComparison.Ordinal);
            Assert.Contains("_config.Appearance.VisualTheme = value;", vm, StringComparison.Ordinal);
            Assert.Contains("ApplyVisualTheme();", vm, StringComparison.Ordinal);
            Assert.Contains("ThemePipeline.RebuildFamily(", vm, StringComparison.Ordinal);

            int modulePass = pipeline.IndexOf("VisualThemeEngine.ApplyModule(", StringComparison.Ordinal);
            int designSystemPublish = pipeline.IndexOf("ThemeManager.PublishProGlassDesignSystem();", StringComparison.Ordinal);
            int notification = pipeline.IndexOf("ThemeManager.NotifyThemeChanged();", StringComparison.Ordinal);
            Assert.True(modulePass >= 0 && designSystemPublish > modulePass && notification > designSystemPublish,
                "Family rebuild must repaint the module, publish the semantic design system, then notify live consumers.");
        }

        [Fact]
        public void Appearance_preview_tokens_are_published_by_the_family_engine()
        {
            string engine = Read("Services/Theme/VisualThemeEngine.EightThemes.cs");
            foreach (var key in new[]
            {
                "SettingsHeroBrush", "SettingsOnAccentBrush", "SettingsOnAccentMutedBrush",
                "SettingsHeroOverlayBrush", "SettingsHeroOverlayEdgeBrush",
                "SettingsCardFaceBrush", "SettingsCardEdgeBrush", "SettingsInnerEdgeBrush",
                "SettingsCardShadow", "ThemeCardFaceBrush", "ThemeCardEdgeBrush",
                "ThemeControlFaceBrush", "ThemeControlEdgeBrush", "ThemeCardHighlightBrush",
                "ThemeNavigationSelectedBrush", "ThemeTableBorderBrush"
            })
                Assert.Contains($"resources[\"{key}\"]", engine, StringComparison.Ordinal);
        }

        [Fact]
        public void Settings_hero_does_not_shadow_application_level_live_family_resources()
        {
            string styles = Read("Views/Settings/SettingsCardStyles.xaml");

            foreach (var key in new[]
            {
                "SettingsHeroBrush", "SettingsOnAccentBrush", "SettingsOnAccentMutedBrush"
            })
                Assert.DoesNotContain($"x:Key=\"{key}\"", styles, StringComparison.Ordinal);

            Assert.Contains("{DynamicResource SettingsHeroBrush}", styles, StringComparison.Ordinal);
            Assert.Contains("{DynamicResource SettingsOnAccentBrush}", styles, StringComparison.Ordinal);
            Assert.Contains("{DynamicResource SettingsOnAccentMutedBrush}", styles, StringComparison.Ordinal);
            Assert.Contains("{DynamicResource SettingsHeroOverlayBrush}", styles, StringComparison.Ordinal);
            Assert.Contains("{DynamicResource SettingsHeroOverlayEdgeBrush}", styles, StringComparison.Ordinal);
        }

        [Fact]
        public void Live_family_preview_uses_the_same_tokens_as_erp_components()
        {
            string layout = Read("Views/Settings/Sections/Cards/AppearanceThemeLayoutCard.xaml");

            foreach (var key in new[]
            {
                "ThemeCardFaceBrush", "ThemeCardEdgeBrush",
                "ThemeControlFaceBrush", "ThemeControlEdgeBrush",
                "ThemeCardHighlightBrush", "ThemeNavigationSelectedBrush",
                "ThemeNavigationSelectedForegroundBrush", "ThemeTableBorderBrush"
            })
                Assert.Contains($"{{DynamicResource {key}}}", layout, StringComparison.Ordinal);
        }
    }
}
