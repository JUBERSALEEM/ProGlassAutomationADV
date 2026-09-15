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
            "Views/Settings/Sections/SettingsSharingSection.xaml",
            "Views/Settings/Sections/SettingsThemesSection.xaml",
            "Views/Settings/Sections/SettingsUpdatesSection.xaml",
            "Views/Settings/Sections/SettingsVoiceSection.xaml",
        };

        private const string StylesPath = "Views/Settings/SettingsSectionStyles.xaml";
        private const string GrammarPath = "Views/Settings/SettingsCardStyles.xaml";
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

        /// <summary>
        /// The module grammar (Views/Settings/SettingsCardStyles.xaml) is the ONE
        /// place a Settings surface is described: page card → card title + header
        /// note → row (title + meta) → inner tile, plus the chip / status dot
        /// styles the approved sheets call for. Views/Settings/SettingsSectionStyles.xaml
        /// keeps only behaviour (password box, provider select, sliders, nav rail,
        /// action button) — chrome that does not describe the card system.
        ///
        /// This replaces the Aug-2026 tile-grid lock. SettingsTileStyle /
        /// SettingsWideTileStyle / SettingsTileIconStyle / SettingsTileCaptionStyle
        /// and friends were deleted with the tile grammar, so 23 of that test's 28
        /// keys no longer existed anywhere: it could only ever fail. The card
        /// grammar is what ships, so that is what gets locked.
        /// </summary>
        [Fact]
        public void Settings_module_grammar_exposes_the_card_design_system()
        {
            var root = LocateProjectRoot();
            var grammar = File.ReadAllText(Path.Combine(root, GrammarPath));

            foreach (var key in new[]
            {
                // RULE 1 — the page is a stack of top-level cards.
                "SettingsPageCardStyle",
                "SettingsPageTitleStyle",
                "SettingsPagePadding",

                // RULE 2 — the card header IS the old group heading.
                "SettingsCardTitleStyle",
                "SettingsCardHeaderNoteStyle",
                "SettingsGroupCaptionStyle",

                // RULE 3 — a row: title + meta, sitting directly in its card.
                "SettingsRowTitleStyle",
                "SettingsRowMetaStyle",
                "SettingsRowSeparatorStyle",

                // RULE 4 — a surface inside a card is a tile, never another card.
                "SettingsInnerTileStyle",
                "SettingsIconTileStyle",
                "SettingsIconGlyphStyle",
                "SettingsWashTileStyle",

                // A destructive action owns a danger card, not a third button.
                "SettingsDangerCardStyle",
                "SettingsDangerCardTitleStyle",
                "SettingsDangerCardNoteStyle",

                // State without a source is decoration — the chip grammar.
                "SettingsChipStyle",
                "SettingsChipTextStyle",
                "SettingsStatusDotStyle",
            })
            {
                Assert.True(
                    grammar.Contains($"x:Key=\"{key}\"", StringComparison.Ordinal),
                    $"{GrammarPath} is missing the '{key}' entry.");
            }

            var behaviour = File.ReadAllText(Path.Combine(root, StylesPath));
            foreach (var key in new[]
            {
                "RoundedPasswordBoxStyle",
                "AccentChipStyle",
                "ProviderSelectButtonStyle",
                "DownloadProgressBarStyle",
                "ModernSliderStyle",
                "SettingsNavButtonStyle",
                "SettingsNavFocusStyle",
                "SettingsActionButtonStyle",
            })
            {
                Assert.True(
                    behaviour.Contains($"x:Key=\"{key}\"", StringComparison.Ordinal),
                    $"{StylesPath} is missing the '{key}' entry.");
            }
        }

        [Theory]
        [InlineData("SettingsAppearanceSection.xaml", "SelectedVisualTheme")]
        [InlineData("SettingsAppearanceSection.xaml", "UseDefinedOutline")]
        [InlineData("SettingsAppearanceSection.xaml", "CardEdgeColor")]
        [InlineData("SettingsAppearanceSection.xaml", "CardEdgeThickness")]
        [InlineData("SettingsAppearanceSection.xaml", "CardEdgePalette")]
        [InlineData("SettingsAppearanceSection.xaml", "ApplyCardEdgePresetCommand")]
        [InlineData("SettingsAppearanceSection.xaml", "CardCornerRadius")]
        [InlineData("SettingsAppearanceSection.xaml", "DisplayDensity")]
        [InlineData("SettingsAppearanceSection.xaml", "ThemeMode")]
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
        [InlineData("SettingsAppearanceSection.xaml", "ReducedMotion")]
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

        /// <summary>
        /// Every Settings page that has had its card pass is built from the card
        /// grammar: a page card per subject, a card header, rows inside it, inner
        /// tiles for surfaces that sit inside a card. This replaces the Aug-2026
        /// "tile grid" lock — SettingsTileStyle / SettingsGroupHeaderTextStyle no
        /// longer exist, so that test failed on 8 of its 10 pages — while keeping
        /// the page-level fact it protected: a full-width host.
        ///
        /// Voice is deliberately absent: it is still on the retired shims and is
        /// tracked by Only_the_voice_page_still_uses_the_retired_tile_shims.
        /// </summary>
        [Theory]
        [InlineData("SettingsAppearanceSection.xaml")]
        [InlineData("SettingsAiSection.xaml")]
        [InlineData("SettingsDataSection.xaml")]
        [InlineData("SettingsDemoSection.xaml")]
        [InlineData("SettingsAgentSection.xaml")]
        [InlineData("SettingsFeaturesSection.xaml")]
        [InlineData("SettingsUpdatesSection.xaml")]
        [InlineData("SettingsLicenseSection.xaml")]
        [InlineData("SettingsAboutSection.xaml")]
        [InlineData("SettingsThemesSection.xaml")]
        [InlineData("SettingsSharingSection.xaml")]
        public void Rewritten_section_uses_the_card_grammar(string sectionFile)
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/Settings/Sections", sectionFile));

            Assert.True(
                src.Contains("Style=\"{StaticResource SettingsPageCardStyle}\"",
                    StringComparison.Ordinal),
                $"{sectionFile} has no top-level page card (SettingsPageCardStyle) — "
                + "the page must be a stack of cards, not bands over raw rows.");
            Assert.True(
                src.Contains("<UniformGrid") || src.Contains("<StackPanel"),
                $"{sectionFile} uses no full-width host (UniformGrid/StackPanel).");
            Assert.True(
                src.Contains("SettingsCardTitleStyle")
                || src.Contains("SettingsCardHeaderStyle"),
                $"{sectionFile} has a page card with no card header — the group "
                + "heading belongs in the card (SettingsCardTitleStyle + "
                + "SettingsCardHeaderNoteStyle), not on a band above it.");
        }

        /// <summary>
        /// Burn-down lock for the pre-redesign shims. Themes/Styles/GlobalDesignSystem.xaml
        /// still carries SettingsTileStyle's surviving shims (SettingsFullRowTileStyle,
        /// SettingsTileHeaderPanelStyle, SettingsGroupSeparatorStyle) for exactly one
        /// consumer: the Voice page, which has not had its card pass yet — its field
        /// tiles are still the Aug-2026 shims inside the card shell.
        ///
        /// When Voice is converted this test FAILS. That is the signal, not a
        /// regression: drop Voice from the list, delete the shims from
        /// GlobalDesignSystem.xaml, and the module is fully on its own grammar.
        /// </summary>
        [Fact]
        public void Only_the_voice_page_still_uses_the_retired_tile_shims()
        {
            var root = LocateProjectRoot();
            var retired = new[]
            {
                "SettingsTileStyle",
                "SettingsWideTileStyle",
                "SettingsFullRowTileStyle",
                "SettingsTileHeaderPanelStyle",
                "SettingsGroupSeparatorStyle",
                "SettingsGroupHeaderTextStyle",
            };

            var stillUsing = new List<string>();
            foreach (var rel in SectionFiles)
            {
                var src = File.ReadAllText(Path.Combine(root, rel));
                // A real consumption is a StaticResource lookup, not a sentence in
                // a file-header comment describing the design that was replaced.
                if (retired.Any(k => Regex.IsMatch(src, "StaticResource " + k + "\\b")))
                    stillUsing.Add(Path.GetFileName(rel));
            }

            Assert.Equal(new[] { "SettingsVoiceSection.xaml" }, stillUsing);
        }

        /// <summary>
        /// The user repeatedly reported "ugly empty space" across every
        /// Settings section. Root cause: tiles lived in a WrapPanel with a
        /// hard MaxWidth (320/380), so a WrapPanel never stretched them to
        /// fill the leftover row width — every row left a big blank right
        /// region. The fix makes the tile host a UniformGrid and the tiles
        /// Stretch so they always fill the full content width. This locks the
        /// container choice + the tile restyle so the gap cannot return.
        /// </summary>
        [Fact]
        /// <summary>
        /// The owner repeatedly reported "ugly empty space" across every Settings
        /// section. Root cause: tiles lived in a WrapPanel with a hard MaxWidth
        /// (320/380), so nothing stretched them to fill the leftover row width —
        /// every row left a big blank right region. The fix removed the cap and
        /// stretched the hosts; this locks the real invariant: the module chrome
        /// ships no fixed-width surface, so a card or tile is always free to fill
        /// its column.
        ///
        /// It no longer forbids WrapPanel. That blanket ban was wrong and was
        /// failing on two pages: a WrapPanel is the correct host for a chip
        /// palette (accent / font colour / card-edge colour), for a button
        /// cluster (import + reset font) and for the Features tab strip —
        /// wrapping is exactly what those want. What the old assertion was really
        /// guarding (a *tile* host that leaves a gap) cannot come back: the pages
        /// are a StackPanel of cards, and the grammar caps no width.
        /// </summary>
        [Fact]
        public void Settings_module_chrome_is_never_width_capped()
        {
            var root = LocateProjectRoot();
            foreach (var rel in new[] { GrammarPath, StylesPath })
            {
                var styles = File.ReadAllText(Path.Combine(root, rel));
                Assert.DoesNotContain("MaxWidth", styles);
            }

            var behaviour = File.ReadAllText(Path.Combine(root, StylesPath));
            Assert.Contains("HorizontalAlignment\" Value=\"Stretch\"", behaviour);
        }

        /// <summary>
        /// A UniformGrid gives EVERY cell the same size. When a short card
        /// (Global Wallpaper) is stacked above a tall card (Per-Module list)
        /// in a UniformGrid Columns="1", the short card is forced up to the
        /// tall card's height — the huge empty space the user reported. The
        /// fix is that any single-column host that stacks cards must be a
        /// StackPanel (natural height), while full-width is preserved by the
        /// tile Stretch. This locks that rule across every Settings section so
        /// a future edit cannot silently reintroduce a Columns="1" UniformGrid.
        /// </summary>
        [Fact]
        public void No_settings_section_forces_equal_card_heights()
        {
            var root = LocateProjectRoot();
            foreach (var rel in SectionFiles)
            {
                var src = File.ReadAllText(Path.Combine(root, rel));
                // Assert.DoesNotContain has no xUnit overload that takes a
                // custom message string, so wrap the condition in Assert.True
                // to keep the diagnostic message.
                var culprit = $"{rel} uses UniformGrid Columns=1, which forces " +
                              "every card to the same height and creates empty " +
                              "space when a short card is stacked over a tall " +
                              "one. Use a StackPanel instead.";
                Assert.True(!src.Contains("<UniformGrid Columns=\"1\""), culprit);
            }
        }

        /// <summary>
        /// Settings action buttons must be full width and share one modern
        /// 12px-rounded shape. Locks the SettingsActionButtonStyle template +
        /// the Shared_styles exposure of it so a future edit cannot revert the
        /// buttons to the old pill look or silently stop stretching.
        /// </summary>
        [Fact]
        public void Settings_action_buttons_are_modern_and_full_width()
        {
            var root = LocateProjectRoot();
            var styles = File.ReadAllText(Path.Combine(root, StylesPath));

            Assert.Contains("x:Key=\"SettingsActionButtonStyle\"", styles);
            Assert.Contains("CornerRadius=\"{DynamicResource CardCornerRadius}\"", styles);
            Assert.Contains("<Setter Property=\"HorizontalAlignment\" Value=\"Stretch\"/>", styles);
            // The modern template uses a ControlTemplate (real shape + hover).
            Assert.Contains("<ControlTemplate TargetType=\"Button\">", styles);
        }

        /// <summary>
        /// The seven sections redesigned in the second Settings pass (Data,
        /// Demo, Agent, Features, Updates, License, About) must still bind to
        /// every ViewModel property and command they bound to before the tile
        /// rewrite. A pure layout change must never drop a binding.
        /// </summary>
        [Theory]
        [InlineData("SettingsDataSection.xaml", "CreateBackupNowCommand")]
        [InlineData("SettingsDataSection.xaml", "RefreshBackupsCommand")]
        [InlineData("SettingsDataSection.xaml", "OpenBackupsFolderCommand")]
        [InlineData("SettingsDataSection.xaml", "RestoreSelectedBackupCommand")]
        [InlineData("SettingsDataSection.xaml", "BackupSets")]
        [InlineData("SettingsDataSection.xaml", "SelectedBackup")]
        [InlineData("SettingsDataSection.xaml", "BackupStatus")]
        [InlineData("SettingsDataSection.xaml", "CreatedAt")]
        [InlineData("SettingsDataSection.xaml", "FileCount")]
        [InlineData("SettingsDataSection.xaml", "SizeDisplay")]
        [InlineData("SettingsDataSection.xaml", "LoadSampleDataCommand")]
        [InlineData("SettingsDataSection.xaml", "ClearAllDataCommand")]
        [InlineData("SettingsDataSection.xaml", "DatabaseHealthRows")]
        [InlineData("SettingsDataSection.xaml", "RefreshDatabaseHealthCommand")]
        [InlineData("SettingsDataSection.xaml", "RebuildAllDatabasesCommand")]
        [InlineData("SettingsDataSection.xaml", "DatabaseHealthStatus")]
        [InlineData("SettingsDataSection.xaml", "DisplayName")]
        [InlineData("SettingsDataSection.xaml", "Status")]
        [InlineData("SettingsDataSection.xaml", "TotalRowsDisplay")]
        [InlineData("SettingsDataSection.xaml", "FileSizeDisplay")]
        [InlineData("SettingsDataSection.xaml", "DriftDisplay")]
        [InlineData("SettingsDataSection.xaml", "LicenseStatus.StatusBadge")]
        [InlineData("SettingsDataSection.xaml", "LicenseStatus.StatusDescription")]
        [InlineData("SettingsDemoSection.xaml", "StartSessionRecordCommand")]
        [InlineData("SettingsDemoSection.xaml", "StopSessionRecordCommand")]
        [InlineData("SettingsDemoSection.xaml", "OpenRecordingsCommand")]
        [InlineData("SettingsDemoSection.xaml", "SessionRecordStatus")]
        [InlineData("SettingsDemoSection.xaml", "Qualities")]
        [InlineData("SettingsDemoSection.xaml", "SelectedQuality")]
        [InlineData("SettingsDemoSection.xaml", "FpsChoices")]
        [InlineData("SettingsDemoSection.xaml", "SelectedFps")]
        [InlineData("SettingsDemoSection.xaml", "CaptureLiveAudio")]
        [InlineData("SettingsDemoSection.xaml", "CaptureMicrophone")]
        [InlineData("SettingsDemoSection.xaml", "Modules")]
        [InlineData("SettingsDemoSection.xaml", "SelectedWorkflow")]
        [InlineData("SettingsDemoSection.xaml", "GenerateSelectedCommand")]
        [InlineData("SettingsDemoSection.xaml", "GenerateAllCommand")]
        [InlineData("SettingsDemoSection.xaml", "RetryCommand")]
        [InlineData("SettingsDemoSection.xaml", "CancelCommand")]
        [InlineData("SettingsDemoSection.xaml", "OpenOutputCommand")]
        [InlineData("SettingsDemoSection.xaml", "EnterDemoCommand")]
        [InlineData("SettingsDemoSection.xaml", "ExitDemoCommand")]
        [InlineData("SettingsDemoSection.xaml", "ResetDemoCommand")]
        [InlineData("SettingsDemoSection.xaml", "StateText")]
        [InlineData("SettingsDemoSection.xaml", "StatusText")]
        [InlineData("SettingsDemoSection.xaml", "VerifiedText")]
        [InlineData("SettingsDemoSection.xaml", "LastMp4Text")]
        [InlineData("SettingsDemoSection.xaml", "FfmpegStatus")]
        [InlineData("SettingsDemoSection.xaml", "History")]
        [InlineData("SettingsDemoSection.xaml", "Module")]
        [InlineData("SettingsDemoSection.xaml", "State")]
        [InlineData("SettingsDemoSection.xaml", "Status")]
        [InlineData("SettingsDemoSection.xaml", "HasHistory")]
        [InlineData("SettingsAgentSection.xaml", "LocalOnlyLabel")]
        [InlineData("SettingsAgentSection.xaml", "ToggleLocalOnlyCommand")]
        [InlineData("SettingsAgentSection.xaml", "CloudConsentLabel")]
        [InlineData("SettingsAgentSection.xaml", "ToggleCloudConsentCommand")]
        [InlineData("SettingsAgentSection.xaml", "PrivacySummary")]
        [InlineData("SettingsAgentSection.xaml", "AllowedHostsDisplay")]
        [InlineData("SettingsAgentSection.xaml", "HardwareAdvice")]
        [InlineData("SettingsAgentSection.xaml", "ProviderOptions")]
        [InlineData("SettingsAgentSection.xaml", "SelectedProvider")]
        [InlineData("SettingsAgentSection.xaml", "KeyStatus")]
        [InlineData("SettingsAgentSection.xaml", "RemoveKeyCommand")]
        [InlineData("SettingsAgentSection.xaml", "Rules")]
        [InlineData("SettingsAgentSection.xaml", "Display")]
        [InlineData("SettingsAgentSection.xaml", "StatusDisplay")]
        [InlineData("SettingsAgentSection.xaml", "IsApproved")]
        [InlineData("SettingsAgentSection.xaml", "RefreshRulesCommand")]
        [InlineData("SettingsAgentSection.xaml", "ClearTeachingCommand")]
        [InlineData("SettingsAgentSection.xaml", "ExportKnowledgeCommand")]
        [InlineData("SettingsAgentSection.xaml", "ImportKnowledgeCommand")]
        [InlineData("SettingsAgentSection.xaml", "ClearMemoryCommand")]
        [InlineData("SettingsAgentSection.xaml", "ClearKnowledgeCommand")]
        [InlineData("SettingsAgentSection.xaml", "AgentDatabasePath")]
        [InlineData("SettingsAgentSection.xaml", "SettingsFilePath")]
        [InlineData("SettingsAgentSection.xaml", "HasRules")]
        [InlineData("SettingsFeaturesSection.xaml", "DataContext.ForgetThisDeviceCommand")]
        [InlineData("SettingsFeaturesSection.xaml", "DataContext.SignOutCommand")]
        [InlineData("SettingsFeaturesSection.xaml", "ProformaFeatures")]
        [InlineData("SettingsFeaturesSection.xaml", "DataContext.InquiryFeatures")]
        [InlineData("SettingsFeaturesSection.xaml", "EnableAllCommand")]
        [InlineData("SettingsFeaturesSection.xaml", "DisableAllOptionalCommand")]
        [InlineData("SettingsFeaturesSection.xaml", "ResetToDefaultsCommand")]
        [InlineData("SettingsFeaturesSection.xaml", "ShowCompanyTrn")]
        [InlineData("SettingsFeaturesSection.xaml", "ShowRepresentative")]
        [InlineData("SettingsFeaturesSection.xaml", "ShowAccountNumber")]
        [InlineData("SettingsFeaturesSection.xaml", "ShowValidUntil")]
        [InlineData("SettingsFeaturesSection.xaml", "ShowCustomerTrn")]
        [InlineData("SettingsFeaturesSection.xaml", "ShowBillingAddress")]
        [InlineData("SettingsFeaturesSection.xaml", "ShowProjectName")]
        [InlineData("SettingsFeaturesSection.xaml", "ShowSiteDeliveryLocation")]
        [InlineData("SettingsFeaturesSection.xaml", "ShowInquiryReference")]
        [InlineData("SettingsFeaturesSection.xaml", "ShowPriority")]
        [InlineData("SettingsFeaturesSection.xaml", "ShowNotes")]
        [InlineData("SettingsUpdatesSection.xaml", "CheckForUpdatesCommand")]
        [InlineData("SettingsUpdatesSection.xaml", "CurrentVersionDisplay")]
        [InlineData("SettingsUpdatesSection.xaml", "LatestVersionDisplay")]
        [InlineData("SettingsUpdatesSection.xaml", "DownloadUpdateCommand")]
        [InlineData("SettingsUpdatesSection.xaml", "InstallUpdateCommand")]
        [InlineData("SettingsUpdatesSection.xaml", "UpdateStatus")]
        [InlineData("SettingsUpdatesSection.xaml", "IsUpdateAvailable")]
        [InlineData("SettingsUpdatesSection.xaml", "IsUpdateReady")]
        [InlineData("SettingsUpdatesSection.xaml", "IsDownloadingUpdate")]
        [InlineData("SettingsUpdatesSection.xaml", "UpdateDownloadProgress")]
        [InlineData("SettingsUpdatesSection.xaml", "HasReleaseInfo")]
        [InlineData("SettingsUpdatesSection.xaml", "ReleaseNameDisplay")]
        [InlineData("SettingsUpdatesSection.xaml", "ReleasePublishedDisplay")]
        [InlineData("SettingsUpdatesSection.xaml", "ReleaseNotesPreview")]
        [InlineData("SettingsUpdatesSection.xaml", "HasUpdateHistory")]
        [InlineData("SettingsUpdatesSection.xaml", "UpdateHistory")]
        [InlineData("SettingsUpdatesSection.xaml", "TimestampUtc")]
        [InlineData("SettingsUpdatesSection.xaml", "EventType")]
        [InlineData("SettingsUpdatesSection.xaml", "Version")]
        [InlineData("SettingsUpdatesSection.xaml", "ReleaseName")]
        [InlineData("SettingsUpdatesSection.xaml", "Detail")]
        [InlineData("SettingsLicenseSection.xaml", "StatusBadge")]
        [InlineData("SettingsLicenseSection.xaml", "EditionDisplay")]
        [InlineData("SettingsLicenseSection.xaml", "StatusDescription")]
        [InlineData("SettingsLicenseSection.xaml", "DaysRemainingDisplay")]
        [InlineData("SettingsAboutSection.xaml", "CurrentVersionDisplay")]
        public void Second_pass_redesigned_sections_still_bind_to(string sectionFile, string binding)
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/Settings/Sections", sectionFile));
            var pattern = @"\{Binding\s+(?:Path\s*=\s*)?(" + Regex.Escape(binding) + @")\s*[,}\s]";
            Assert.True(Regex.IsMatch(src, pattern),
                $"Binding '{binding}' vanished from {sectionFile}. " +
                "The tile redesign must not drop a ViewModel binding.");
        }

        /// <summary>
        /// The Features page re-scopes DataContext to five separate flags
        /// VMs (ProformaFeatures, InquiryFeatures, JobOrderFeatures,
        /// DeliveryNoteFeatures, TaxInvoiceFeatures) so the descendant field
        /// toggles bind cleanly, and reaches the parent SettingsViewModel via
        /// ElementName for the session commands. Lock every scope plus the
        /// count of PiToggleSwitch field toggles so a future edit cannot
        /// accidentally collapse them into one scope.
        /// </summary>
        [Fact]
        public void Features_section_preserves_all_data_context_scopes()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/Settings/Sections/SettingsFeaturesSection.xaml"));

            Assert.Contains("DataContext=\"{Binding ProformaFeatures}\"", src);
            Assert.Contains("DataContext=\"{Binding DataContext.InquiryFeatures,", src);
            Assert.Contains("DataContext=\"{Binding DataContext.JobOrderFeatures,", src);
            Assert.Contains("DataContext=\"{Binding DataContext.DeliveryNoteFeatures,", src);
            Assert.Contains("DataContext=\"{Binding DataContext.TaxInvoiceFeatures,", src);
            Assert.Contains("x:Name=\"SettingsFeaturesRoot\"", src);
            Assert.Contains("DataContext.ForgetThisDeviceCommand,", src);
            Assert.Contains("DataContext.SignOutCommand,", src);
            // 47 field toggles: Proforma 22 + Inquiry 8 + Job Order 7 +
            // Delivery Note 5 + Tax Invoice 5.
            Assert.Equal(47, Regex.Matches(src, "<ToggleButton").Count);
        }

        /// <summary>
        /// The Features page is now a per-module tab switch (Aug 2026). Each
        /// module has a tab button and its content panel only shows when the
        /// matching IsXFeaturesModule flag is true. Lock the five tab buttons,
        /// the SwitchFeaturesModuleCommand wiring, and the per-module
        /// Visibility bindings so the page cannot silently collapse back into
        /// one long stack of all five modules.
        /// </summary>
        [Fact]
        public void Features_section_uses_a_per_module_tab_switch()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/Settings/Sections/SettingsFeaturesSection.xaml"));

            Assert.Contains("SwitchFeaturesModuleCommand", src);
            foreach (var module in new[]
            {
                "Proforma", "Inquiry", "JobOrder", "DeliveryNote", "TaxInvoice"
            })
            {
                Assert.True(src.Contains($"Is{module}FeaturesModule"),
                    $"Features page missing the '{module}' tab/visibility binding.");
            }
            Assert.Contains("Proforma Invoice", src);
            Assert.Contains("Delivery Note", src);
            Assert.Contains("Tax Invoice", src);
        }

        /// <summary>
        /// The Agent page hands its DataContext to the code-behind via the
        /// root Grid x:Name="AgentRoot", and exposes the AgentKeyBox + the
        /// StoreKey_Click handler. Without these the credentials card would
        /// render on top of every other settings page (is-Agent-page binding
        /// resolves against the wrong VM) and the STORE KEY button would break.
        /// </summary>
        [Fact]
        public void Agent_section_preserves_the_code_behind_contract()
        {
            var root = LocateProjectRoot();
            var xaml = File.ReadAllText(Path.Combine(root,
                "Views/Settings/Sections/SettingsAgentSection.xaml"));
            var cs = File.ReadAllText(Path.Combine(root,
                "Views/Settings/Sections/SettingsAgentSection.xaml.cs"));

            Assert.Contains("x:Name=\"AgentRoot\"", xaml);
            Assert.Contains("x:Name=\"AgentKeyBox\"", xaml);
            Assert.Contains("Click=\"StoreKey_Click\"", xaml);
            Assert.Contains("StoreKey_Click", cs);
        }

        /// <summary>
        /// The WALLPAPERS group was redesigned (Aug 2026) so it stops wasting
        /// vertical space:
        ///   • Global Wallpaper and its visibility slider are now ONE card
        ///     with the controls on a single line (previously two separate
        ///     tiles = two rows of height).
        ///   • Per-Module Wallpapers render the 17 module rows in THREE
        ///     columns under one card, instead of one full-width stacked list
        ///     (~6 rows instead of 17).
        /// Lock the shape so a future edit cannot silently bring back the tall
        /// stack or split the global card back into two tiles.
        /// </summary>
        [Fact]
        /// <summary>
        /// The WALLPAPERS group was redesigned (Aug 2026) so it stops wasting
        /// vertical space:
        ///   • Global Wallpaper and its visibility slider are now ONE card
        ///     with the controls on a single line (previously two separate
        ///     tiles = two rows of height).
        ///   • Per-Module Wallpapers render the module rows in THREE columns
        ///     under one card, instead of one full-width stacked list.
        ///
        /// Asserted on the parsed tree, not on string offsets: the previous
        /// version searched between the words "Global Wallpaper" and
        /// "Per-Module Wallpapers", which stopped meaning anything the moment
        /// those words moved into a comment. A card is now found by the binding
        /// it owns and its extent is walked as XML.
        /// </summary>
        [Fact]
        public void Appearance_wallpapers_are_combined_and_three_column()
        {
            var root = LocateProjectRoot();
            var rel = "Views/Settings/Sections/SettingsAppearanceSection.xaml";
            var src = File.ReadAllText(Path.Combine(root, rel));

            var xdoc = new XmlDocument();
            xdoc.Load(Path.Combine(root, rel));
            var doc = xdoc.DocumentElement!;

            // Global wallpaper + visibility: one card, both bindings.
            var setter = FindElementBinding(doc, "SetWallpaperCommand");
            Assert.True(setter != null,
                "No element in the Appearance page binds SetWallpaperCommand.");
            var globalCard = NearestCard(setter!);
            Assert.True(globalCard != null,
                "The global wallpaper button is not inside a card.");
            Assert.True(FindElementBinding(globalCard, "WallpaperOpacity") != null,
                "The global wallpaper card no longer holds its visibility slider — "
                + "SET/CLEAR and visibility must stay on one line of one card "
                + "(two tiles put them two rows apart).");

            // Per-module wallpaper rows: one card, a three-column grid.
            var rows = FindElementBinding(doc, "ModuleWallpaperRows");
            Assert.True(rows != null,
                "No element in the Appearance page binds ModuleWallpaperRows.");
            var moduleCard = NearestCard(rows!);
            Assert.True(moduleCard != null,
                "The per-module wallpaper list is not inside a card.");
            Assert.True(HasDescendantElement(moduleCard, "UniformGrid", "Columns", "3"),
                "The per-module wallpaper list is no longer a three-column "
                + "UniformGrid, so the page grows by one row per module again.");

            Assert.DoesNotContain("<UniformGrid Columns=\"1\"", src);
        }

        /// <summary>First descendant that binds <paramref name="binding"/>.</summary>
        private static XmlElement? FindElementBinding(XmlElement? elem, string binding)
        {
            if (elem == null) return null;
            foreach (XmlAttribute attr in elem.Attributes)
            {
                if (attr.Value.Contains("{Binding " + binding, StringComparison.Ordinal))
                    return elem;
            }
            foreach (XmlNode child in elem.ChildNodes)
            {
                if (child is XmlElement ce)
                {
                    var hit = FindElementBinding(ce, binding);
                    if (hit != null) return hit;
                }
            }
            return null;
        }

        /// <summary>
        /// Nearest ancestor (or self) that carries the card grammar — a page card
        /// or an inner tile. Used to prove two controls share one card.
        /// </summary>
        private static XmlElement? NearestCard(XmlElement elem)
        {
            for (XmlNode? node = elem; node is XmlElement e; node = node.ParentNode)
            {
                var style = e.GetAttribute("Style");
                if (style.Contains("SettingsPageCardStyle", StringComparison.Ordinal)
                    || style.Contains("SettingsInnerTileStyle", StringComparison.Ordinal)
                    || style.Contains("SettingsDangerCardStyle", StringComparison.Ordinal))
                {
                    return e;
                }
            }
            return null;
        }

        /// <summary>Does any descendant carry the given element + attribute value?</summary>
        private static bool HasDescendantElement(
            XmlElement root, string localName, string attribute, string value)
        {
            foreach (XmlNode child in root.ChildNodes)
            {
                if (child is not XmlElement ce) continue;
                if (string.Equals(ce.LocalName, localName, StringComparison.Ordinal)
                    && string.Equals(ce.GetAttribute(attribute), value, StringComparison.Ordinal))
                {
                    return true;
                }
                if (HasDescendantElement(ce, localName, attribute, value)) return true;
            }
            return false;
        }

        /// <summary>
        /// XLS0501 / MC3024 regression lock. WPF's XAML compiler refuses
        /// any element that sets the same property twice — once as an
        /// inline attribute (Style="…") AND once as a property-element
        /// child (&lt;TextBlock.Style&gt;…&lt;/TextBlock.Style&gt;), even
        /// when both target the same resource. The Voice section shipped
        /// exactly that pattern in the Aug-2026 redesign; this test
        /// walks every Settings XAML and fails the build if it ever
        /// comes back on any element / any property (not just Style).
        /// </summary>
        [Fact]
        public void No_settings_xaml_sets_the_same_property_twice()
        {
            var root = LocateProjectRoot();
            var failures = new List<string>();

            foreach (var rel in SectionFiles.Concat(new[] { StylesPath, ShellPath }))
            {
                var path = Path.Combine(root, rel);
                var xdoc = new XmlDocument();
                xdoc.Load(path);
                if (xdoc.DocumentElement != null)
                    WalkForDoubleSet(xdoc.DocumentElement, rel, failures);
            }

            Assert.True(failures.Count == 0,
                "XAML property-set-twice regression (XLS0501 / MC3024). Every " +
                "listed element sets a property BOTH as an inline attribute AND " +
                "as a property-element child — WPF rejects that at build time." +
                Environment.NewLine +
                "Fix by removing the inline attribute and keeping only the " +
                "property-element form:" + Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        private static void WalkForDoubleSet(
            XmlElement elem, string file, List<string> failures)
        {
            var localName = elem.LocalName;
            // Which property-element children own a "Foo.Property" tag
            // whose owner equals this element's local name?
            foreach (XmlNode child in elem.ChildNodes)
            {
                if (child is not XmlElement ce) continue;
                var cname = ce.LocalName;
                var idx = cname.IndexOf('.');
                if (idx <= 0) continue;
                var owner = cname.Substring(0, idx);
                var prop = cname.Substring(idx + 1);
                if (!string.Equals(owner, localName, StringComparison.Ordinal)) continue;
                // Owner matches — is the same property also set inline?
                if (elem.HasAttribute(prop))
                {
                    failures.Add(
                        $"{file}: <{localName}> sets '{prop}' both inline and " +
                        $"as <{localName}.{prop}> — remove the inline attribute.");
                }
            }
            // Recurse.
            foreach (XmlNode child in elem.ChildNodes)
                if (child is XmlElement ce)
                    WalkForDoubleSet(ce, file, failures);
        }

        /// <summary>
        /// 2026-09-16 pass: every page's group heading moved INTO its card — the
        /// band caption and its recap line are now the card's title and helper
        /// line. A page that grows a floating band caption back has undone the
        /// redesign, and no binding check would notice.
        ///
        /// Features is the one page that keeps bands on purpose: a card there is
        /// a MODULE and its bands are the wizard-step sub-groups inside it
        /// (sheet 10 keeps cap sub-groups inside a card).
        /// </summary>
        [Theory]
        [InlineData("SettingsAboutSection.xaml", 0)]
        [InlineData("SettingsAgentSection.xaml", 0)]
        [InlineData("SettingsAiSection.xaml", 0)]
        [InlineData("SettingsAppearanceSection.xaml", 0)]
        [InlineData("SettingsDataSection.xaml", 0)]
        [InlineData("SettingsDemoSection.xaml", 0)]
        [InlineData("SettingsFeaturesSection.xaml", 8)]
        [InlineData("SettingsLicenseSection.xaml", 0)]
        [InlineData("SettingsSharingSection.xaml", 0)]
        [InlineData("SettingsThemesSection.xaml", 0)]
        [InlineData("SettingsUpdatesSection.xaml", 0)]
        [InlineData("SettingsVoiceSection.xaml", 0)]
        public void Settings_pages_head_their_groups_with_cards_not_bands(
            string file, int allowedBands)
        {
            var root = LocateProjectRoot();
            var text = File.ReadAllText(
                Path.Combine(root, "Views/Settings/Sections", file));

            var bands = Regex.Matches(text, "SettingsGroupCaptionStyle").Count;
            Assert.True(bands == allowedBands,
                file + " has " + bands + " band caption(s), expected " + allowedBands +
                ". A group heading belongs in its card's header (SettingsCardTitleStyle " +
                "+ SettingsCardHeaderNoteStyle), not on a caption above it.");
        }

        /// <summary>
        /// A page card may not sit inside another page card. Both carry the card
        /// radius, the card fill and the module shadow, so nesting them reads as
        /// card-in-card and flattens the page into a stack of identical boxes —
        /// the inner container is a tile (SettingsInnerTileStyle) by definition.
        /// Walked on the parsed tree, because these files' indentation has been
        /// through several scripted restyles and no longer reflects nesting.
        /// </summary>
        [Theory]
        [InlineData("SettingsView.xaml", true)]
        [InlineData("Sections/SettingsAboutSection.xaml", false)]
        [InlineData("Sections/SettingsAgentSection.xaml", false)]
        [InlineData("Sections/SettingsAiSection.xaml", false)]
        [InlineData("Sections/SettingsAppearanceSection.xaml", false)]
        [InlineData("Sections/SettingsDataSection.xaml", false)]
        [InlineData("Sections/SettingsDemoSection.xaml", false)]
        [InlineData("Sections/SettingsFeaturesSection.xaml", false)]
        [InlineData("Sections/SettingsLicenseSection.xaml", false)]
        [InlineData("Sections/SettingsSharingSection.xaml", false)]
        [InlineData("Sections/SettingsThemesSection.xaml", false)]
        [InlineData("Sections/SettingsUpdatesSection.xaml", false)]
        [InlineData("Sections/SettingsVoiceSection.xaml", false)]
        public void Settings_page_cards_are_never_nested(string relative, bool isShell)
        {
            var root = LocateProjectRoot();
            var path = Path.Combine(root, "Views/Settings", relative);
            var xdoc = new XmlDocument();
            xdoc.Load(path);

            var offenders = new List<string>();
            WalkForNestedCard(xdoc.DocumentElement, 0, relative, offenders);

            Assert.True(offenders.Count == 0,
                "Nested page card(s) in " + relative + ":" + Environment.NewLine +
                string.Join(Environment.NewLine, offenders) + Environment.NewLine +
                "Use SettingsInnerTileStyle for a surface inside a card." +
                (isShell ? " (shell)" : string.Empty));
        }

        private static void WalkForNestedCard(
            XmlElement elem, int cardDepth, string file, List<string> offenders)
        {
            var style = elem.GetAttribute("Style");
            var isPageCard = string.Equals(elem.LocalName, "Border", StringComparison.Ordinal)
                && (style.Contains("SettingsPageCardStyle")
                    || style.Contains("SettingsDangerCardStyle"));

            if (isPageCard && cardDepth > 0)
            {
                offenders.Add("  <Border Style=\"" + style +
                    "\"> sits inside another page card");
            }

            var childDepth = isPageCard ? cardDepth + 1 : cardDepth;
            foreach (XmlNode child in elem.ChildNodes)
            {
                if (child is XmlElement ce)
                    WalkForNestedCard(ce, childDepth, file, offenders);
            }
        }

        /// <summary>
        /// The destructive action on the Demo page is its own red card (sheet
        /// 08's danger card), not the third of three identical buttons: a reset
        /// that wipes the demo folder must not sit one slip away from
        /// "Enter demo mode".
        /// </summary>
        [Fact]
        public void Settings_destructive_reset_is_its_own_danger_card()
        {
            var root = LocateProjectRoot();
            var text = File.ReadAllText(Path.Combine(root,
                "Views/Settings/Sections/SettingsDemoSection.xaml"));

            Assert.Contains("Style=\"{StaticResource SettingsDangerCardStyle}\"", text);
            Assert.Contains("Style=\"{StaticResource SettingsDangerCardTitleStyle}\"", text);
            Assert.Contains("Style=\"{StaticResource SettingsDangerCardNoteStyle}\"", text);
            Assert.Contains("Command=\"{Binding ResetDemoCommand}\"", text);
        }

        /// <summary>
        /// Sheet 02's MODE control and sheet 03's DISPLAY DENSITY control are the
        /// same grammar: a ListBox track + ListBoxItem segments. Neither existed
        /// before this pass — nothing on the page bound IsDarkTheme at all, and
        /// density was a ComboBox that showed one option and hid the rest in a
        /// drop-down. Lock the shapes and the bindings that make them live.
        /// </summary>
        [Fact]
        public void Appearance_density_and_mode_are_segmented_controls()
        {
            var root = LocateProjectRoot();
            var grammar = File.ReadAllText(Path.Combine(root, GrammarPath));
            foreach (var key in new[]
            {
                "SettingsSegmentItemStyle",
                "SettingsSegmentTrackStyle",
                "SettingsSegmentTrackFillStyle",
            })
            {
                Assert.True(grammar.Contains($"x:Key=\"{key}\"", StringComparison.Ordinal),
                    $"{GrammarPath} is missing the '{key}' entry.");
            }

            // One implementation, not two. The retired ToggleButton segment style
            // could never drive N options: WPF gives every button its own
            // IsChecked, so 4 densities would have needed 4 bools + 4 commands and
            // nothing would uncheck the others.
            Assert.DoesNotContain("x:Key=\"SettingsSegmentStyle\"", grammar);
            Assert.Contains("<UniformGrid Rows=\"1\"/>", grammar);

            var src = File.ReadAllText(Path.Combine(root,
                "Views/Settings/Sections/SettingsAppearanceSection.xaml"));

            // Density — data-driven segments over the VM's own option list, so the
            // option set keeps one source.
            Assert.Contains("Style=\"{StaticResource SettingsSegmentTrackFillStyle}\"", src);
            Assert.Contains("ItemsSource=\"{Binding DisplayDensityOptions}\"", src);
            Assert.Contains("SelectedItem=\"{Binding DisplayDensity, Mode=TwoWay}\"", src);
            Assert.DoesNotContain("<ComboBox ItemsSource=\"{Binding DisplayDensityOptions}\"", src);

            // Mode — Light/Dark, the token _config.Appearance.Theme already stores.
            Assert.Contains("Style=\"{StaticResource SettingsSegmentTrackStyle}\"", src);
            Assert.Contains("SelectedValuePath=\"Tag\"", src);
            Assert.Contains("SelectedValue=\"{Binding ThemeMode, Mode=TwoWay}\"", src);
            Assert.Contains("Tag=\"Light\"", src);
            Assert.Contains("Tag=\"Dark\"", src);
        }

        /// <summary>
        /// Sheet 02 puts the live base colour mode beside the page name, on every
        /// page. A chip is only worth having if it is state-driven: this one reads
        /// a brush KEY from the ViewModel (ThemeModeChipBackgroundKey /
        /// ForegroundKey) and resolves it through BrushKeyConverter, so the view
        /// holds no colour and a theme switch repaints it for free. The two
        /// badges are text-bound to the language files, not hard-coded LIGHT/DARK.
        /// </summary>
        [Fact]
        public void Shell_page_header_chip_reports_the_live_colour_mode()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root, ShellPath));

            Assert.Contains("Style=\"{StaticResource SettingsChipStyle}\"", src);
            Assert.Contains("Style=\"{StaticResource SettingsChipTextStyle}\"", src);
            Assert.Contains("Converter={StaticResource BrushKeyConverter}", src);
            Assert.Contains("ThemeModeChipBackgroundKey", src);
            Assert.Contains("ThemeModeChipForegroundKey", src);
            Assert.Contains("Settings.ModeBadgeLight", src);
            Assert.Contains("Settings.ModeBadgeDark", src);
            Assert.Contains("Converter={StaticResource BoolToVis}", src);
            Assert.Contains("Converter={StaticResource InverseBoolToVis}", src);
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
