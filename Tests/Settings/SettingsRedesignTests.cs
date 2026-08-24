// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Settings/SettingsRedesignTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Settings v2 tile-grid redesign (Aug 2026)
//
// Hermetic tests that lock in the Aug 2026 Settings redesign:
//
//   1. Every one of the 10 Settings section XAML files parses cleanly.
//   2. SettingsSectionStyles.xaml still exposes the styles the sections
//      consume, plus the new tile-grid styles added in this redesign
//      (SettingsTileStyle, SettingsWideTileStyle, SettingsFullRowTileStyle,
//      SettingsGroupHeaderTextStyle, etc.).
//   3. Every rewritten section still binds to the same ViewModel props
//      and commands it used to — a regression on any of these would
//      silently break a settings page.
//   4. Voice + AI code-behind handlers referenced from XAML still line
//      up with the matching x:Name / method name.
//
// These are pure source-file tests. No WPF runtime, no DB, no filesystem
// beyond reading the XAML tree relative to the project. Walks upward
// from AppContext.BaseDirectory the same way the other coverage tests
// in this repo do.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Xunit;

namespace ProGlassAutomation.Tests.Settings
{
    public class SettingsRedesignTests
    {
        private static readonly string[] SectionFiles =
        {
            "Views/Settings/Sections/SettingsAboutSection.xaml",
            "Views/Settings/Sections/SettingsAgentSection.xaml",
            "Views/Settings/Sections/SettingsAiSection.xaml",
            "Views/Settings/Sections/SettingsAppearanceSection.xaml",
            "Views/Settings/Sections/SettingsDataSection.xaml",
            "Views/Settings/Sections/SettingsDemoSection.xaml",
            "Views/Settings/Sections/SettingsFeaturesSection.xaml",
            "Views/Settings/Sections/SettingsLicenseSection.xaml",
            "Views/Settings/Sections/SettingsUpdatesSection.xaml",
            "Views/Settings/Sections/SettingsVoiceSection.xaml",
        };

        private const string StylesPath = "Views/Settings/SettingsSectionStyles.xaml";
        private const string ShellPath = "Views/Settings/SettingsView.xaml";

        [Fact]
        public void All_settings_section_xaml_files_parse()
        {
            var root = LocateProjectRoot();
            foreach (var rel in SectionFiles.Concat(new[] { StylesPath, ShellPath }))
            {
                var path = Path.Combine(root, rel);
                Assert.True(File.Exists(path), $"Missing: {rel}");
                var xdoc = new XmlDocument();
                xdoc.Load(path);
            }
        }

        [Fact]
        public void Shared_styles_dictionary_exposes_the_tile_grid_design_system()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root, StylesPath));
            foreach (var key in new[]
            {
                "SettingsGroupHeaderTextStyle",
                "SettingsGroupCaptionStyle",
                "SettingsTileStyle",
                "SettingsWideTileStyle",
                "SettingsFullRowTileStyle",
                "SettingsTileHeaderPanelStyle",
                "SettingsTileIconStyle",
                "SettingsTileTitleStyle",
                "SettingsTileCaptionStyle",
                "SettingsTileFooterHintStyle",
                "SettingsTileHeroStyle",
                "SettingsGroupSeparatorStyle",
                "PageTitleStyle",
                "PageSubtitleStyle",
                "SectionCardStyle",
                "SectionLabelStyle",
                "RowTitleStyle",
                "HintTextStyle",
                "LabelStyle",
                "ValueStyle",
                "ModernToggleSwitch",
                "RoundedPasswordBoxStyle",
                "AccentChipStyle",
                "ProviderSelectButtonStyle",
                "ProviderCardBaseStyle",
                "DownloadProgressBarStyle",
                "ModelOptionCardStyle",
                "ModernSliderStyle",
            })
            {
                Assert.True(
                    src.Contains($"x:Key=\"{key}\"", StringComparison.Ordinal),
                    $"SettingsSectionStyles.xaml is missing the '{key}' Style.");
            }
        }

        [Theory]
        [InlineData("SettingsAppearanceSection.xaml", "SelectedVisualTheme")]
        [InlineData("SettingsAppearanceSection.xaml", "UseDefinedOutline")]
        [InlineData("SettingsAppearanceSection.xaml", "CardBorderThickness")]
        [InlineData("SettingsAppearanceSection.xaml", "CardCornerRadius")]
        [InlineData("SettingsAppearanceSection.xaml", "DisplayDensity")]
        [InlineData("SettingsAppearanceSection.xaml", "DisplayDensityOptions")]
        [InlineData("SettingsAppearanceSection.xaml", "UiScale")]
        [InlineData("SettingsAppearanceSection.xaml", "UiScalePercent")]
        [InlineData("SettingsAppearanceSection.xaml", "ResetUiScaleCommand")]
        [InlineData("SettingsAppearanceSection.xaml", "PdfLayout")]
        [InlineData("SettingsAppearanceSection.xaml", "PdfLayoutOptions")]
        [InlineData("SettingsAppearanceSection.xaml", "PdfLayoutHint")]
        [InlineData("SettingsAppearanceSection.xaml", "PerformanceTierDisplay")]
        [InlineData("SettingsAppearanceSection.xaml", "SetWallpaperCommand")]
        [InlineData("SettingsAppearanceSection.xaml", "ClearWallpaperCommand")]
        [InlineData("SettingsAppearanceSection.xaml", "WallpaperStatus")]
        [InlineData("SettingsAppearanceSection.xaml", "WallpaperOpacity")]
        [InlineData("SettingsAppearanceSection.xaml", "ApplyAccentCommand")]
        [InlineData("SettingsAppearanceSection.xaml", "FontOptions")]
        [InlineData("SettingsAppearanceSection.xaml", "SelectedFont")]
        [InlineData("SettingsAppearanceSection.xaml", "SelectedFontWeight")]
        [InlineData("SettingsAppearanceSection.xaml", "SelectedFontStyle")]
        [InlineData("SettingsAppearanceSection.xaml", "FontWeightChoices")]
        [InlineData("SettingsAppearanceSection.xaml", "FontStyleChoices")]
        [InlineData("SettingsAppearanceSection.xaml", "FontStatus")]
        [InlineData("SettingsAppearanceSection.xaml", "ImportFontCommand")]
        [InlineData("SettingsAppearanceSection.xaml", "ResetFontCommand")]
        [InlineData("SettingsAppearanceSection.xaml", "ModuleWallpaperRows")]
        [InlineData("SettingsAppearanceSection.xaml", "VisualThemeOptions")]
        [InlineData("SettingsAiSection.xaml", "IsAiEnabled")]
        [InlineData("SettingsAiSection.xaml", "UseRealData")]
        [InlineData("SettingsAiSection.xaml", "AiModel")]
        [InlineData("SettingsAiSection.xaml", "AiMaxTokens")]
        [InlineData("SettingsAiSection.xaml", "AiTemperature")]
        [InlineData("SettingsAiSection.xaml", "IsGeminiActive")]
        [InlineData("SettingsAiSection.xaml", "IsGroqActive")]
        [InlineData("SettingsAiSection.xaml", "IsCohereActive")]
        [InlineData("SettingsAiSection.xaml", "SelectGeminiCommand")]
        [InlineData("SettingsAiSection.xaml", "SelectGroqCommand")]
        [InlineData("SettingsAiSection.xaml", "SelectCohereCommand")]
        [InlineData("SettingsAiSection.xaml", "TestGeminiCommand")]
        [InlineData("SettingsAiSection.xaml", "TestGroqCommand")]
        [InlineData("SettingsAiSection.xaml", "TestCohereCommand")]
        [InlineData("SettingsAiSection.xaml", "GeminiTestStatus")]
        [InlineData("SettingsAiSection.xaml", "GroqTestStatus")]
        [InlineData("SettingsAiSection.xaml", "CohereTestStatus")]
        [InlineData("SettingsAiSection.xaml", "KeepApiKeysSaved")]
        [InlineData("SettingsAiSection.xaml", "RemoveSavedKeysCommand")]
        [InlineData("SettingsAiSection.xaml", "ClearAiMemoryCommand")]
        [InlineData("SettingsAiSection.xaml", "ConfigFilePath")]
        [InlineData("SettingsVoiceSection.xaml", "WhisperModelOptions")]
        [InlineData("SettingsVoiceSection.xaml", "IsWhisperModelReady")]
        [InlineData("SettingsVoiceSection.xaml", "IsDownloadingModel")]
        [InlineData("SettingsVoiceSection.xaml", "DownloadProgress")]
        [InlineData("SettingsVoiceSection.xaml", "DownloadSizeText")]
        [InlineData("SettingsVoiceSection.xaml", "DownloadSpeedText")]
        [InlineData("SettingsVoiceSection.xaml", "DownloadEtaText")]
        [InlineData("SettingsVoiceSection.xaml", "DownloadWhisperModelCommand")]
        [InlineData("SettingsVoiceSection.xaml", "CancelDownloadCommand")]
        [InlineData("SettingsVoiceSection.xaml", "DeleteWhisperModelCommand")]
        [InlineData("SettingsVoiceSection.xaml", "SelectedModelInfo.FileName")]
        [InlineData("SettingsVoiceSection.xaml", "VoiceStatus")]
        [InlineData("SettingsVoiceSection.xaml", "ModelFilePath")]
        public void Rewritten_section_still_binds_to(string sectionFile, string binding)
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/Settings/Sections", sectionFile));
            var pattern = @"\{Binding\s+(?:Path\s*=\s*)?" + Regex.Escape(binding) + @"[\s,}]";
            Assert.True(Regex.IsMatch(src, pattern),
                $"Binding '{binding}' vanished from {sectionFile}. " +
                "The redesign was supposed to be a pure layout change — " +
                "every ViewModel binding must survive byte-for-byte.");
        }

        [Fact]
        public void AiSection_preserves_the_three_password_handler_wirings()
        {
            var root = LocateProjectRoot();
            var xaml = File.ReadAllText(Path.Combine(root,
                "Views/Settings/Sections/SettingsAiSection.xaml"));
            var cs = File.ReadAllText(Path.Combine(root,
                "Views/Settings/Sections/SettingsAiSection.xaml.cs"));

            foreach (var name in new[] { "GeminiKeyBox", "GroqKeyBox", "CohereKeyBox" })
            {
                Assert.True(
                    xaml.Contains($"x:Name=\"{name}\"", StringComparison.Ordinal),
                    $"XAML lost x:Name=\"{name}\" — code-behind cannot push the saved key back into the PasswordBox.");
                Assert.True(
                    xaml.Contains($"PasswordChanged=\"{name}_PasswordChanged\"", StringComparison.Ordinal),
                    $"XAML lost the PasswordChanged handler for {name}.");
                Assert.True(
                    cs.Contains($"{name}_PasswordChanged", StringComparison.Ordinal),
                    $"Code-behind is missing the {name}_PasswordChanged handler.");
            }
        }

        [Fact]
        public void VoiceSection_preserves_the_model_option_click_handler()
        {
            var root = LocateProjectRoot();
            var xaml = File.ReadAllText(Path.Combine(root,
                "Views/Settings/Sections/SettingsVoiceSection.xaml"));
            var cs = File.ReadAllText(Path.Combine(root,
                "Views/Settings/Sections/SettingsVoiceSection.xaml.cs"));

            Assert.Contains("ModelOption_MouseLeftButtonDown", xaml);
            Assert.Contains("ModelOption_MouseLeftButtonDown", cs);
        }

        [Theory]
        [InlineData("SettingsAppearanceSection.xaml")]
        [InlineData("SettingsAiSection.xaml")]
        [InlineData("SettingsVoiceSection.xaml")]
        public void Rewritten_section_uses_the_new_tile_grid(string sectionFile)
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/Settings/Sections", sectionFile));

            Assert.Contains("<WrapPanel", src);
            Assert.True(
                src.Contains("SettingsTileStyle") ||
                src.Contains("SettingsWideTileStyle") ||
                src.Contains("SettingsFullRowTileStyle"),
                $"{sectionFile} does not consume any of the new tile styles.");
            Assert.Contains("SettingsGroupHeaderTextStyle", src);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName, "Views", "Settings",
                    "SettingsSectionStyles.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException(
                "Could not locate the project root (Views/Settings/SettingsSectionStyles.xaml) " +
                "above " + AppContext.BaseDirectory);
        }
    }
}
