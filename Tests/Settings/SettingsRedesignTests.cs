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
        [InlineData("SettingsDataSection.xaml")]
        [InlineData("SettingsDemoSection.xaml")]
        [InlineData("SettingsAgentSection.xaml")]
        [InlineData("SettingsFeaturesSection.xaml")]
        [InlineData("SettingsUpdatesSection.xaml")]
        [InlineData("SettingsLicenseSection.xaml")]
        [InlineData("SettingsAboutSection.xaml")]
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
        /// The Features page re-scopes DataContext to two separate flags VMs
        /// (ProformaFeatures and InquiryFeatures) so the descendant field
        /// toggles bind cleanly, and reaches the parent SettingsViewModel via
        /// ElementName for the session commands. Lock both scopes plus the
        /// count of PiToggleSwitch field toggles so a future edit cannot
        /// accidentally collapse them into one scope.
        /// </summary>
        [Fact]
        public void Features_section_preserves_both_data_context_scopes()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/Settings/Sections/SettingsFeaturesSection.xaml"));

            Assert.Contains("DataContext=\"{Binding ProformaFeatures}\"", src);
            Assert.Contains("DataContext=\"{Binding DataContext.InquiryFeatures,", src);
            Assert.Contains("x:Name=\"SettingsFeaturesRoot\"", src);
            Assert.Contains("DataContext.ForgetThisDeviceCommand,", src);
            Assert.Contains("DataContext.SignOutCommand,", src);
            // 30 field toggles: Step1 10 + Step2 7 + Step3 5 + Inquiry 8.
            Assert.Equal(30, Regex.Matches(src, "<ToggleButton").Count);
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
        public void Appearance_wallpapers_are_combined_and_three_column()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/Settings/Sections/SettingsAppearanceSection.xaml"));

            // Global wallpaper + visibility are combined into one tile: the
            // card that holds SetWallpaperCommand must also hold the opacity
            // slider. If they were split again into two tiles, "Wallpaper
            // Visibility" would reappear as a separate title.
            Assert.Contains("Global Wallpaper", src);
            Assert.DoesNotContain("Wallpaper Visibility", src);
            var start = src.IndexOf("Global Wallpaper", StringComparison.Ordinal);
            var end = src.IndexOf("Per-Module Wallpapers", StringComparison.Ordinal);
            var globalBlock = src.Substring(
                start, end > start ? end - start : Math.Max(0, 800));
            Assert.Contains("SetWallpaperCommand", globalBlock);
            Assert.Contains("WallpaperOpacity", globalBlock);

            // Per-module list is a 3-column UniformGrid under a single card.
            Assert.Contains("Per-Module Wallpapers", src);
            Assert.Contains("<UniformGrid Columns=\"3\"", src);
            Assert.Contains("ItemsSource=\"{Binding ModuleWallpaperRows}\"", src);
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
