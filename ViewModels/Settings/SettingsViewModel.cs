// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings (Chat 25 v1.1 — CS0272 fix)
//
// CHANGES vs Chat 25 v1.0:
//   ✓ FIX CS0272: App.SpeechToText setter is now public (fixed in
//     App.xaml.cs). All three assignment sites now compile correctly:
//       App.SpeechToText = new WhisperSpeechService()  ← line 151
//       App.SpeechToText?.Dispose()                    ← line 196
//       App.SpeechToText = new SpeechToTextService()   ← line 197
//   ✓ FIX CS0428: WhisperModelManager.IsModelDownloaded now resolves
//     as bool property — line 498 compiles correctly.
//   ✓ All other code preserved exactly from Chat 25 v1.0.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIVoice;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Fonts;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace ProGlassAutomation.ViewModels.Settings
{
    public sealed class SettingsViewModel : ViewModelBase, IDisposable
    {
        // Phase 4: Named handlers for static events — detachable
        private EventHandler? _themeChangedHandler;
        private EventHandler? _uiScaleChangedHandler;
        private bool _disposed;

        private string _geminiTestStatus = string.Empty;
        private string _groqTestStatus = string.Empty;
        private string _cohereTestStatus = string.Empty;
        private bool _apiKeyJustSaved;
        private bool _isDownloadingModel;
        private int _downloadProgress;
        private string _voiceStatus = string.Empty;
        private CancellationTokenSource _downloadCts;

        // ══════════════════════════════════════════════════════════════
        //  v2 — SETTINGS PAGE NAVIGATION (shell nav rail)
        //  Same proven pattern as the TI/DN/JO detail tab strips:
        //  one string key + Is{Page}Page flags + switch command.
        // ══════════════════════════════════════════════════════════════

        private string _settingsPage = "Appearance";

        /// <summary>Active settings page key (nav rail selection).</summary>
        public string SettingsPage
        {
            get => _settingsPage;
            private set
            {
                // Guard: an unknown key would collapse every section and
                // leave the content pane empty — refuse it outright.
                if (value != "Appearance" && value != "AI" &&
                    value != "Agent" && value != "Voice" &&
                    value != "Updates" && value != "Data" &&
                    value != "Demo" &&
                    value != "About")
                    return;

                if (_settingsPage == value) return;
                _settingsPage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAppearancePage));
                OnPropertyChanged(nameof(IsAiPage));
                OnPropertyChanged(nameof(IsAgentPage));   // PHASE F
                OnPropertyChanged(nameof(IsVoicePage));
                OnPropertyChanged(nameof(IsUpdatesPage));
                OnPropertyChanged(nameof(IsDataPage));
                OnPropertyChanged(nameof(IsAboutPage));
                OnPropertyChanged(nameof(IsDemoPage));
            }
        }

        public bool IsAppearancePage => SettingsPage == "Appearance";
        public bool IsAiPage         => SettingsPage == "AI";

        /// <summary>PHASE F — ProGlass Agent privacy / teaching page.</summary>
        public bool IsAgentPage      => SettingsPage == "Agent";

        public bool IsVoicePage      => SettingsPage == "Voice";
        public bool IsUpdatesPage    => SettingsPage == "Updates";
        public bool IsDataPage       => SettingsPage == "Data";
        public bool IsAboutPage      => SettingsPage == "About";
        public bool IsDemoPage       => SettingsPage == "Demo";

        /// <summary>Switches the active settings page (nav rail).</summary>
        public ICommand SwitchSettingsPageCommand { get; private set; }

        public string ConfigFilePath { get; }

        // ══════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════

        public SettingsViewModel()
        {
            // v2 shell: navigation between the settings pages.
            SwitchSettingsPageCommand = new RelayCommand(
                p => SettingsPage = p as string ?? "Appearance");

            ConfigFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ProGlassAutomation",
                "config.json");

            // Build model options list for UI picker
            foreach (var kvp in WhisperModelManager.AvailableModels)
                WhisperModelOptions.Add(kvp.Value);

            // ── Per-provider test commands ─────────────────────────────
            TestGeminiCommand = new RelayCommand(async _ =>
            {
                GeminiTestStatus = "Testing…";
                var svc = AiProviderFactory.CreateForProvider(
                    AiProvider.Gemini, App.Config.Ai);
                var result = await svc.TestConnectionAsync();
                GeminiTestStatus = result.Success
                    ? "✓ " + result.Message
                    : "✗ " + result.Message;
            });

            TestGroqCommand = new RelayCommand(async _ =>
            {
                GroqTestStatus = "Testing…";
                var svc = AiProviderFactory.CreateForProvider(
                    AiProvider.Groq, App.Config.Ai);
                var result = await svc.TestConnectionAsync();
                GroqTestStatus = result.Success
                    ? "✓ " + result.Message
                    : "✗ " + result.Message;
            });

            TestCohereCommand = new RelayCommand(async _ =>
            {
                CohereTestStatus = "Testing…";
                var svc = AiProviderFactory.CreateForProvider(
                    AiProvider.Cohere, App.Config.Ai);
                var result = await svc.TestConnectionAsync();
                CohereTestStatus = result.Success
                    ? "✓ " + result.Message
                    : "✗ " + result.Message;
            });

            // ── Provider selection ─────────────────────────────────────
            SelectGeminiCommand = new RelayCommand(_ =>
                ActiveProvider = AiProvider.Gemini);
            SelectGroqCommand = new RelayCommand(_ =>
                ActiveProvider = AiProvider.Groq);
            SelectCohereCommand = new RelayCommand(_ =>
                ActiveProvider = AiProvider.Cohere);

            // ── Whisper download ───────────────────────────────────────
            DownloadWhisperModelCommand = new RelayCommand(async _ =>
            {
                if (IsDownloadingModel) return;

                var modelKey = SelectedWhisperModel;
                var modelInfo = WhisperModelManager.GetModelInfo(modelKey);

                IsDownloadingModel = true;
                DownloadProgress = 0;
                VoiceStatus =
                    $"Downloading {modelInfo.DisplayName} ({modelInfo.SizeMB} MB)…";

                _downloadCts = new CancellationTokenSource();

                var success = await WhisperModelManager.DownloadModelAsync(
                    modelKey,
                    (WhisperModelManager.DownloadProgressInfo info) =>
                    {
                        // InvokeAsync, not Invoke: Invoke blocks the download
                        // thread until the UI thread is free, so a busy UI
                        // throttled the transfer itself.
                        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                        {
                            DownloadProgress  = info.Percent;
                            DownloadSpeedText = info.SpeedText;
                            DownloadSizeText  = info.SizeText;
                            DownloadEtaText   = info.RemainingText;

                            VoiceStatus =
                                $"Downloading {modelInfo.DisplayName} — "
                                + $"{info.Percent}%  ·  {info.SizeText}"
                                + (info.BytesPerSecond > 0
                                    ? $"  ·  {info.SpeedText}" : "")
                                + (string.IsNullOrEmpty(info.RemainingText)
                                    ? "" : $"  ·  {info.RemainingText}");
                        });
                    },
                    _downloadCts.Token);

                IsDownloadingModel = false;

                // Clear the transient readouts so a finished or cancelled
                // download does not leave a stale speed on screen.
                DownloadSpeedText = "";
                DownloadSizeText  = "";
                DownloadEtaText   = "";

                if (success)
                {
                    VoiceStatus = $"✓ {modelInfo.DisplayName} downloaded successfully!";
                    OnPropertyChanged(nameof(IsWhisperModelReady));
                    OnPropertyChanged(nameof(ModelFileSizeMB));
                    OnPropertyChanged(nameof(SelectedModelInfo));

                    // Refresh or swap STT service so mic button appears without restart
                    if (App.SpeechToText is WhisperSpeechService whisper)
                    {
                        // Already using Whisper — just refresh availability
                        whisper.RefreshAvailability();
                    }
                    else
                    {
                        // Was using System.Speech fallback — swap to Whisper
                        // FIX CS0272: App.SpeechToText now has public set
                        try
                        {
                            App.SpeechToText?.Dispose();
                            App.SpeechToText = new WhisperSpeechService();
                            AppLogService.Debug(
                                "[Settings] Switched STT → Whisper after download.");
                        }
                        catch (Exception ex)
                        {
                            AppLogService.Debug(
                                $"[Settings] Whisper swap failed: {ex.Message}");
                        }
                    }
                }
                else if (_downloadCts.IsCancellationRequested)
                {
                    VoiceStatus = "Download cancelled.";
                    DownloadProgress = 0;
                }
                else
                {
                    VoiceStatus = "✗ Download failed. Check internet connection.";
                    DownloadProgress = 0;
                }

                _downloadCts?.Dispose();
                _downloadCts = null;
            });

            CancelDownloadCommand = new RelayCommand(_ =>
                _downloadCts?.Cancel());

            DeleteWhisperModelCommand = new RelayCommand(_ =>
            {
                var deleted = WhisperModelManager.DeleteModel(SelectedWhisperModel);
                if (deleted)
                {
                    VoiceStatus = "Model deleted. Voice disabled until re-download.";
                    DownloadProgress = 0;
                    OnPropertyChanged(nameof(IsWhisperModelReady));
                    OnPropertyChanged(nameof(ModelFileSizeMB));

                    // Swap back to System.Speech fallback
                    // FIX CS0272: App.SpeechToText now has public set
                    if (App.SpeechToText is WhisperSpeechService w)
                    {
                        w.Dispose();
                        try { App.SpeechToText = new SpeechToTextService(); }
                        catch { App.SpeechToText = null; }
                    }
                }
                else
                {
                    VoiceStatus = "Could not delete model file. It may be in use.";
                }
            });

            ResetUiScaleCommand = new RelayCommand(_ =>
                UiScale = Services.Viewport.ViewportService.DefaultUiScale);

            // Phase 4: Named handlers — no lambda leak
            _themeChangedHandler = (_, __) => OnPropertyChanged(nameof(IsDarkTheme));
            ThemeManager.ThemeChanged += _themeChangedHandler;

            _uiScaleChangedHandler = (_, __) =>
            {
                OnPropertyChanged(nameof(UiScale));
                OnPropertyChanged(nameof(UiScalePercent));
            };
            ThemeManager.UiScaleChanged += _uiScaleChangedHandler;

            // Dynamic Theme Engine — accent presets (Settings UI)
            ApplyAccentCommand = new RelayCommand(param =>
                ApplyAccentPreset(param as string ?? "GlassBlue"));

            // Global font engine (system-wide UI font)
            InitializeFontSettings();

            // v2 sections: security keys, OTA updates, backup & restore
            InitializeV2Sections();
        }

        // ══════════════════════════════════════════════════════════════
        //  APPEARANCE
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Appearance toggle for the strong card/pill definition edge.
        /// Persisted so the choice survives a restart, and re-applied by
        /// ThemeManager after every Light/Dark swap.
        /// </summary>
        public bool UseDefinedOutline
        {
            get => App.Config?.Appearance?.UseDefinedOutline ?? false;
            set
            {
                if (App.Config?.Appearance == null) return;
                if (App.Config.Appearance.UseDefinedOutline == value) return;

                App.Config.Appearance.UseDefinedOutline = value;
                ThemeManager.ApplyOutline(value);
                Services.AI.AIConfig.ConfigService.Save(App.Config);

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Card outline changed",
                    value ? "Defined edge ON" : "Defined edge OFF");

                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Global card border thickness (DIPs). One setting restyles every
        /// card / panel in the app through the CardBorderThickness theme
        /// resource. Persisted so the choice survives a restart.
        /// </summary>
        public double CardBorderThickness
        {
            get => App.Config?.Appearance?.CardBorderThickness ?? 1.5;
            set
            {
                if (App.Config?.Appearance == null) return;
                double v = value < 0 ? 0 : value;
                if (Math.Abs(App.Config.Appearance.CardBorderThickness - v) < 0.01) return;

                App.Config.Appearance.CardBorderThickness = v;
                ThemeManager.ApplyCardBorderThickness(v);
                Services.AI.AIConfig.ConfigService.Save(App.Config);

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Card border thickness changed",
                    $"{v:0.#} DIP");

                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Global card corner radius (DIPs). Default 12. One slider
        /// restyles every card through CardCornerRadius (and the derived
        /// Control / Large tokens). Persisted across restarts.
        /// </summary>
        public List<string> DisplayDensityOptions { get; } = new()
        {
            "Compact", "Normal", "Comfortable"
        };

        public List<string> PdfLayoutOptions { get; } = new()
        {
            "Classic Navy", "Modern Teal", "Editorial Ink",
            "Industrial Steel", "Minimal Ivory"
        };

        public string PdfLayout
        {
            get
            {
                var id = App.Config?.Appearance?.PdfLayoutId ?? "ClassicNavy";
                return ProGlassAutomation.Services.Export.Pdf.PdfThemeCatalog
                    .Resolve(id).DisplayName;
            }
            set
            {
                if (App.Config?.Appearance == null) return;
                var theme = ProGlassAutomation.Services.Export.Pdf.PdfThemeCatalog
                    .Resolve(value);
                if (App.Config.Appearance.PdfLayoutId == theme.Id) return;

                App.Config.Appearance.PdfLayoutId = theme.Id;
                ProGlassAutomation.Services.Export.Pdf.DocumentStyle.Apply(theme);
                Services.AI.AIConfig.ConfigService.Save(App.Config);

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "PDF layout changed", theme.DisplayName);

                OnPropertyChanged();
                OnPropertyChanged(nameof(PdfLayoutHint));
            }
        }

        public string PdfLayoutHint =>
            ProGlassAutomation.Services.Export.Pdf.PdfThemeCatalog
                .Resolve(App.Config?.Appearance?.PdfLayoutId).Description;

        /// <summary>
        /// Compact / Normal / Comfortable. One setting shrinks shared
        /// chrome so laptop screens can see the data grid.
        /// </summary>
        public string DisplayDensity
        {
            get => App.Config?.Appearance?.DisplayDensity ?? "Compact";
            set
            {
                if (App.Config?.Appearance == null) return;
                string v = string.IsNullOrWhiteSpace(value) ? "Compact" : value.Trim();
                if (App.Config.Appearance.DisplayDensity == v) return;

                App.Config.Appearance.DisplayDensity = v;
                ThemeManager.ApplyDisplayDensity(v);
                Services.AI.AIConfig.ConfigService.Save(App.Config);

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Display density changed", v);

                OnPropertyChanged();
            }
        }

        /// <summary>Global UI zoom 80–125%. Ctrl+Wheel also drives this.</summary>
        public double UiScale
        {
            get => App.Config?.Appearance?.UiScale ?? 1.0;
            set
            {
                if (App.Config?.Appearance == null) return;
                double v = Services.Viewport.ViewportService.ClampUiScale(value);
                if (Math.Abs(App.Config.Appearance.UiScale - v) < 0.001) return;

                App.Config.Appearance.UiScale = v;
                ThemeManager.ApplyUiScale(v);
                Services.AI.AIConfig.ConfigService.Save(App.Config);
                OnPropertyChanged();
                OnPropertyChanged(nameof(UiScalePercent));
            }
        }

        public int UiScalePercent => (int)Math.Round(UiScale * 100);

        public ICommand ResetUiScaleCommand { get; private set; } = null!;

        public double CardCornerRadius
        {
            get => App.Config?.Appearance?.CardCornerRadius ?? 12;
            set
            {
                if (App.Config?.Appearance == null) return;
                double v = value < 0 ? 0 : value;
                if (Math.Abs(App.Config.Appearance.CardCornerRadius - v) < 0.05) return;

                App.Config.Appearance.CardCornerRadius = v;
                ThemeManager.ApplyCardCornerRadius(v);
                Services.AI.AIConfig.ConfigService.Save(App.Config);

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Card corner radius changed",
                    $"{v:0} px");

                OnPropertyChanged();
            }
        }

        public bool IsDarkTheme
        {
            get => ThemeManager.CurrentTheme == AppTheme.Dark;
            set
            {
                ThemeManager.ApplyTheme(value ? AppTheme.Dark : AppTheme.Light);

                // Rebuild the visual family (Monet glass, Professional
                // surfaces, wallpaper layers) against the fresh theme
                // colours — otherwise derived surfaces stay stale.
                ApplyVisualTheme();

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Theme changed",
                    value ? "Dark theme" : "Light theme");

                OnPropertyChanged();
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  ACCENT COLOR (Dynamic Theme Engine)
        //
        //  One-click presets feed the adaptive accent engine
        //  (ThemeManager.ApplyAccent) which derives hover/tint/contrast
        //  brushes automatically. "Glass Blue" resets to the default.
        // ══════════════════════════════════════════════════════════════

        public ICommand ApplyAccentCommand { get; private set; }

        /// <summary>Applies a named accent preset. Parameter = preset key.</summary>
        public void ApplyAccentPreset(string presetKey)
        {
            var color = ProGlassAutomation.Services.Theme.AccentPresets
                .ColorFor(presetKey);

            ThemeManager.ApplyAccent(color);

            // Persist so the chosen colour survives app restarts.
            if (App.Config?.Appearance != null)
            {
                App.Config.Appearance.AccentPreset =
                    string.IsNullOrEmpty(presetKey) ? "GlassBlue" : presetKey;
                SaveConfig();
            }

            ProGlassAutomation.Services.Audit.AuditService.Log(
                ProGlassAutomation.Services.Audit.AuditSections.Settings,
                "Accent changed",
                presetKey == "GlassBlue" ? "Glass Blue (default)" : presetKey);
        }

        // ══════════════════════════════════════════════════════════════
        //  GLOBAL FONT (FontService) — system-wide UI typography
        //
        //  Lets the user pick any installed or imported font, weight
        //  (Regular/Bold) and style (Normal/Italic). The choice is applied
        //  app-wide through the AppFontFamily / AppFontWeight / AppFontStyle
        //  resources and persisted in the appearance config.
        // ══════════════════════════════════════════════════════════════

        public ObservableCollection<FontOption> FontOptions { get; } = new();

        public IReadOnlyList<string> FontWeightChoices { get; } =
            new[] { "Regular", "Bold" };

        public IReadOnlyList<string> FontStyleChoices { get; } =
            new[] { "Normal", "Italic" };

        private FontOption? _selectedFont;
        public FontOption? SelectedFont
        {
            get => _selectedFont;
            set
            {
                if (value == null || ReferenceEquals(_selectedFont, value)) return;
                _selectedFont = value;
                OnPropertyChanged();
                ApplyFontSelection();
            }
        }

        private string _selectedFontWeight = "Regular";
        public string SelectedFontWeight
        {
            get => _selectedFontWeight;
            set
            {
                if (_selectedFontWeight == value || string.IsNullOrWhiteSpace(value)) return;
                _selectedFontWeight = value;
                OnPropertyChanged();
                ApplyFontSelection();
            }
        }

        private string _selectedFontStyle = "Normal";
        public string SelectedFontStyle
        {
            get => _selectedFontStyle;
            set
            {
                if (_selectedFontStyle == value || string.IsNullOrWhiteSpace(value)) return;
                _selectedFontStyle = value;
                OnPropertyChanged();
                ApplyFontSelection();
            }
        }

        public ICommand ImportFontCommand { get; private set; } = null!;
        public ICommand ResetFontCommand { get; private set; } = null!;

        public string FontStatus =>
            string.IsNullOrWhiteSpace(FontService.CurrentFamily)
                ? "Segoe UI (default)"
                : FontService.CurrentFamily +
                  (FontService.CurrentWeight == FontWeights.Bold ? " · Bold" : "") +
                  (FontService.CurrentStyle == FontStyles.Italic ? " · Italic" : "");

        private void InitializeFontSettings()
        {
            ReloadFontOptions();

            var current = App.Config?.Appearance;
            _selectedFontWeight = string.Equals(current?.FontWeightName, "Bold", StringComparison.OrdinalIgnoreCase)
                ? "Bold" : "Regular";
            _selectedFontStyle = string.Equals(current?.FontStyleName, "Italic", StringComparison.OrdinalIgnoreCase)
                ? "Italic" : "Normal";

            string family = current?.FontFamilyName ?? FontService.DefaultFontFamily;
            _selectedFont = FontOptions.FirstOrDefault(f =>
                string.Equals(f.Name, family, StringComparison.OrdinalIgnoreCase))
                ?? FontOptions.FirstOrDefault(f =>
                    string.Equals(f.Name, FontService.DefaultFontFamily, StringComparison.OrdinalIgnoreCase));

            ImportFontCommand = new RelayCommand(_ => ImportFontDialog());
            ResetFontCommand = new RelayCommand(_ =>
            {
                FontService.ResetToDefault();
                ReloadFontOptions();
                var d = FontOptions.FirstOrDefault(f =>
                    string.Equals(f.Name, FontService.DefaultFontFamily, StringComparison.OrdinalIgnoreCase));
                if (d != null) { _selectedFont = d; OnPropertyChanged(nameof(SelectedFont)); }
                _selectedFontWeight = "Regular"; OnPropertyChanged(nameof(SelectedFontWeight));
                _selectedFontStyle = "Normal"; OnPropertyChanged(nameof(SelectedFontStyle));
                OnPropertyChanged(nameof(FontStatus));
            });
        }

        private void ReloadFontOptions()
        {
            FontOptions.Clear();
            foreach (var f in FontService.GetAvailableFonts())
                FontOptions.Add(f);
        }

        private void ApplyFontSelection()
        {
            if (_selectedFont == null) return;

            var weight = string.Equals(_selectedFontWeight, "Bold", StringComparison.OrdinalIgnoreCase)
                ? FontWeights.Bold : FontWeights.Normal;
            var style = string.Equals(_selectedFontStyle, "Italic", StringComparison.OrdinalIgnoreCase)
                ? FontStyles.Italic : FontStyles.Normal;

            FontService.ApplyFont(_selectedFont.Name, weight, style);
            OnPropertyChanged(nameof(FontStatus));
        }

        private void ImportFontDialog()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import font",
                Filter = "Font files (*.ttf;*.otf;*.ttc;*.fon)|*.ttf;*.otf;*.ttc;*.fon|All files (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;

            var result = FontService.ImportFont(dialog.FileName);
            if (!result.IsSuccess)
            {
                try
                {
                    System.Windows.MessageBox.Show(
                        result.Error, "Import font",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch { /* status still updated */ }
                return;
            }

            ReloadFontOptions();
            var imported = FontOptions.FirstOrDefault(f =>
                string.Equals(f.Name, result.Value, StringComparison.OrdinalIgnoreCase));
            if (imported != null)
            {
                _selectedFont = imported;
                OnPropertyChanged(nameof(SelectedFont));
                ApplyFontSelection();
            }

            try
            {
                System.Windows.MessageBox.Show(
                    "Font imported: " + result.Value + "\nIt is now applied app-wide.",
                    "Import font", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch { /* status still updated */ }
        }

        /// <summary>Detected display tier for the Performance section.</summary>
        public string PerformanceTierDisplay =>
            $"{ProGlassAutomation.Services.Performance.PerformanceProfileService.TargetFps} fps profile" +
            (ProGlassAutomation.Services.Performance.PerformanceProfileService.RichEffectsEnabled
                ? "  •  rich effects on"
                : "  •  effects optimized");

        // ══════════════════════════════════════════════════════════════
        //  AI MASTER TOGGLE
        // ══════════════════════════════════════════════════════════════

        public bool IsAiEnabled
        {
            get => App.Config?.Ai?.Enabled ?? false;
            set
            {
                if (App.Config?.Ai != null)
                {
                    App.Config.Ai.Enabled = value;
                    SaveConfig();
                    OnPropertyChanged();
                }
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  ACTIVE PROVIDER
        // ══════════════════════════════════════════════════════════════

        public AiProvider ActiveProvider
        {
            get => App.Config?.Ai?.ActiveProvider ?? AiProvider.Gemini;
            set
            {
                if (App.Config?.Ai != null &&
                    App.Config.Ai.ActiveProvider != value)
                {
                    App.Config.Ai.ActiveProvider = value;
                    SaveConfig();
                    App.Ai = AiProviderFactory.CreateActive(App.Config.Ai);
                    App.RaiseAiProviderChanged();

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsGeminiActive));
                    OnPropertyChanged(nameof(IsGroqActive));
                    OnPropertyChanged(nameof(IsCohereActive));
                    OnPropertyChanged(nameof(AiModel));
                    OnPropertyChanged(nameof(ActiveProviderName));
                }
            }
        }

        public bool IsGeminiActive => ActiveProvider == AiProvider.Gemini;
        public bool IsGroqActive => ActiveProvider == AiProvider.Groq;
        public bool IsCohereActive => ActiveProvider == AiProvider.Cohere;

        public string ActiveProviderName => ActiveProvider switch
        {
            AiProvider.Gemini => "Google Gemini",
            AiProvider.Groq => "Groq",
            AiProvider.Cohere => "Cohere",
            _ => "Unknown"
        };

        // ══════════════════════════════════════════════════════════════
        //  API KEYS
        // ══════════════════════════════════════════════════════════════

        public string GeminiApiKey
        {
            get => App.Config?.Ai?.GeminiApiKey ?? string.Empty;
            set
            {
                if (App.Config?.Ai != null &&
                    App.Config.Ai.GeminiApiKey != value)
                {
                    App.Config.Ai.GeminiApiKey = value;
                    SaveConfig();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsGeminiConfigured));
                    ApiKeyJustSaved = true;
                }
            }
        }

        public string GroqApiKey
        {
            get => App.Config?.Ai?.GroqApiKey ?? string.Empty;
            set
            {
                if (App.Config?.Ai != null &&
                    App.Config.Ai.GroqApiKey != value)
                {
                    App.Config.Ai.GroqApiKey = value;
                    SaveConfig();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsGroqConfigured));
                    ApiKeyJustSaved = true;
                }
            }
        }

        public string CohereApiKey
        {
            get => App.Config?.Ai?.CohereApiKey ?? string.Empty;
            set
            {
                if (App.Config?.Ai != null &&
                    App.Config.Ai.CohereApiKey != value)
                {
                    App.Config.Ai.CohereApiKey = value;
                    SaveConfig();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCohereConfigured));
                    ApiKeyJustSaved = true;
                }
            }
        }

        public string AiApiKey
        {
            get => App.Config?.Ai?.ApiKey ?? string.Empty;
            set
            {
                if (App.Config?.Ai != null)
                {
                    App.Config.Ai.ApiKey = value;
                    SaveConfig();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasSavedApiKey));
                    ApiKeyJustSaved = true;
                }
            }
        }

        public bool IsGeminiConfigured =>
            !string.IsNullOrWhiteSpace(App.Config?.Ai?.GeminiApiKey);
        public bool IsGroqConfigured =>
            !string.IsNullOrWhiteSpace(App.Config?.Ai?.GroqApiKey);
        public bool IsCohereConfigured =>
            !string.IsNullOrWhiteSpace(App.Config?.Ai?.CohereApiKey);
        public bool HasSavedApiKey =>
            !string.IsNullOrWhiteSpace(App.Config?.Ai?.ApiKey);

        public bool ApiKeyJustSaved
        {
            get => _apiKeyJustSaved;
            set
            {
                if (_apiKeyJustSaved == value) return;
                _apiKeyJustSaved = value;
                OnPropertyChanged();
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  TEST STATUS
        // ══════════════════════════════════════════════════════════════

        public string GeminiTestStatus
        {
            get => _geminiTestStatus;
            set
            {
                if (_geminiTestStatus == value) return;
                _geminiTestStatus = value;
                OnPropertyChanged();
            }
        }

        public string GroqTestStatus
        {
            get => _groqTestStatus;
            set
            {
                if (_groqTestStatus == value) return;
                _groqTestStatus = value;
                OnPropertyChanged();
            }
        }

        public string CohereTestStatus
        {
            get => _cohereTestStatus;
            set
            {
                if (_cohereTestStatus == value) return;
                _cohereTestStatus = value;
                OnPropertyChanged();
            }
        }

        public string AiTestStatus
        {
            get => ActiveProvider switch
            {
                AiProvider.Gemini => _geminiTestStatus,
                AiProvider.Groq => _groqTestStatus,
                AiProvider.Cohere => _cohereTestStatus,
                _ => string.Empty
            };
            set
            {
                switch (ActiveProvider)
                {
                    case AiProvider.Gemini: GeminiTestStatus = value; break;
                    case AiProvider.Groq: GroqTestStatus = value; break;
                    case AiProvider.Cohere: CohereTestStatus = value; break;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  RAG TOGGLE
        // ══════════════════════════════════════════════════════════════

        public bool UseRealData
        {
            get => App.Config?.Ai?.UseRealData ?? false;
            set
            {
                if (App.Config?.Ai == null) return;

                // Consent gate — P-1. Enabling real-data access is a privacy
                // decision: a real-time summary containing real customer names,
                // project names and AED totals will leave the machine to the
                // configured AI provider (Gemini / Groq / Cohere). It is opt-in,
                // so turning it ON must require an explicit confirmation. The
                // toggle stays off unless the user confirms.
                if (value && !App.Config.Ai.UseRealData)
                {
                    var confirm = System.Windows.MessageBox.Show(
                        "Turning on \"Use Real Business Data\" sends a live summary " +
                        "of your invoices, job orders, deliveries, tax invoices and " +
                        "balances — including real customer names, project names and " +
                        "AED totals — over the internet to your AI provider " +
                        "(Gemini, Groq or Cohere) as part of every chat message.\n\n" +
                        "Those companies process the data under their own privacy " +
                        "terms. Turn it off to keep the assistant to general " +
                        "questions only.\n\nDo you want to allow this?",
                        "Allow AI to access your business data?",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning,
                        System.Windows.MessageBoxResult.No);

                    if (confirm != System.Windows.MessageBoxResult.Yes)
                    {
                        // User declined — keep it off and reflect that on the toggle.
                        OnPropertyChanged(nameof(UseRealData));
                        return;
                    }
                }

                App.Config.Ai.UseRealData = value;
                SaveConfig();
                OnPropertyChanged();
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  MODEL & TUNING
        // ══════════════════════════════════════════════════════════════

        public string AiModel => App.Config?.Ai?.GetModelForProvider(ActiveProvider) ?? "—";
        public int AiMaxTokens => App.Config?.Ai?.MaxTokens ?? 1024;
        public double AiTemperature => App.Config?.Ai?.Temperature ?? 0.7;

        // ══════════════════════════════════════════════════════════════
        //  COMMANDS
        // ══════════════════════════════════════════════════════════════

        public ICommand TestGeminiCommand { get; }
        public ICommand TestGroqCommand { get; }
        public ICommand TestCohereCommand { get; }

        public ICommand TestAiConnectionCommand => ActiveProvider switch
        {
            AiProvider.Gemini => TestGeminiCommand,
            AiProvider.Groq => TestGroqCommand,
            AiProvider.Cohere => TestCohereCommand,
            _ => TestGeminiCommand
        };

        public ICommand SelectGeminiCommand { get; }
        public ICommand SelectGroqCommand { get; }
        public ICommand SelectCohereCommand { get; }

        // ══════════════════════════════════════════════════════════════
        //  WHISPER MODEL MANAGEMENT
        // ══════════════════════════════════════════════════════════════

        public ObservableCollection<WhisperModelInfo> WhisperModelOptions { get; }
            = new ObservableCollection<WhisperModelInfo>();

        public string SelectedWhisperModel
        {
            get => App.Config?.Ai?.SelectedWhisperModel ?? "base";
            set
            {
                if (App.Config?.Ai != null &&
                    App.Config.Ai.SelectedWhisperModel != value)
                {
                    App.Config.Ai.SelectedWhisperModel = value;
                    SaveConfig();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedModelInfo));
                    OnPropertyChanged(nameof(IsWhisperModelReady));
                    OnPropertyChanged(nameof(ModelFileSizeMB));
                    OnPropertyChanged(nameof(ModelFilePath));

                    if (App.SpeechToText is WhisperSpeechService whisper)
                        whisper.RefreshAvailability();
                }
            }
        }

        public WhisperModelInfo SelectedModelInfo =>
            WhisperModelManager.GetModelInfo(SelectedWhisperModel);

        // FIX CS0428: IsModelDownloaded is now a clean bool property
        public bool IsWhisperModelReady => WhisperModelManager.IsModelDownloaded;
        public long ModelFileSizeMB => WhisperModelManager.ModelFileSizeMB;
        public string ModelFilePath => WhisperModelManager.DefaultModelPath;

        public bool IsDownloadingModel
        {
            get => _isDownloadingModel;
            set
            {
                if (_isDownloadingModel == value) return;
                _isDownloadingModel = value;
                OnPropertyChanged();
            }
        }

        public int DownloadProgress
        {
            get => _downloadProgress;
            set
            {
                if (_downloadProgress == value) return;
                _downloadProgress = value;
                OnPropertyChanged();
            }
        }

        // ── Whisper download detail (speed / size / ETA) ─────────────
        private string _downloadSpeedText = "";
        /// <summary>e.g. "4.2 MB/s" — empty when not downloading.</summary>
        public string DownloadSpeedText
        {
            get => _downloadSpeedText;
            set
            {
                if (_downloadSpeedText == value) return;
                _downloadSpeedText = value;
                OnPropertyChanged();
            }
        }

        private string _downloadSizeText = "";
        /// <summary>e.g. "63 of 142 MB".</summary>
        public string DownloadSizeText
        {
            get => _downloadSizeText;
            set
            {
                if (_downloadSizeText == value) return;
                _downloadSizeText = value;
                OnPropertyChanged();
            }
        }

        private string _downloadEtaText = "";
        /// <summary>e.g. "1m 12s left". Empty when not estimable.</summary>
        public string DownloadEtaText
        {
            get => _downloadEtaText;
            set
            {
                if (_downloadEtaText == value) return;
                _downloadEtaText = value;
                OnPropertyChanged();
            }
        }

        public string VoiceStatus
        {
            get => _voiceStatus;
            set
            {
                if (_voiceStatus == value) return;
                _voiceStatus = value;
                OnPropertyChanged();
            }
        }

        public ICommand DownloadWhisperModelCommand { get; }
        public ICommand CancelDownloadCommand { get; }
        public ICommand DeleteWhisperModelCommand { get; }

        // ══════════════════════════════════════════════════════════════
        //  v2 — SECURITY (API key storage)
        //
        //  Keys are DPAPI-encrypted in the SecretVault, never plain
        //  text. Turning "keep keys" off makes them session-only.
        // ══════════════════════════════════════════════════════════════

        public bool KeepApiKeysSaved
        {
            get => App.Config?.Ai?.RememberApiKeys ?? true;
            set
            {
                if (App.Config == null ||
                    App.Config.Ai.RememberApiKeys == value)
                    return;

                App.Config.Ai.RememberApiKeys = value;
                SaveConfig();   // wipes the vault when value is false
                OnPropertyChanged();
            }
        }

        public ICommand RemoveSavedKeysCommand { get; private set; }
        public ICommand ClearAiMemoryCommand { get; private set; }

        /// <summary>Seeds the first-run demo dataset (all modules).</summary>
        public ICommand LoadSampleDataCommand { get; private set; }

        /// <summary>Wipes every record across all modules (e.g. after
        /// evaluating the sample data) so real data entry starts clean.</summary>
        public ICommand ClearAllDataCommand { get; private set; }

        // ══════════════════════════════════════════════════════════════
        //  v2 — DATABASE HEALTH (diagnostics + emergency rebuild)
        //  SettingsViewModel is the only intended caller of
        //  SchemaHealthService; the service is destructive so we gate
        //  it behind a Manager+ role check.
        // ══════════════════════════════════════════════════════════════

        /// <summary>Row per ProGlass Automation database file.
        /// Populated by <see cref="RefreshDatabaseHealthCommand"/>.</summary>
        public ObservableCollection<DatabaseHealthRow> DatabaseHealthRows { get; } =
            new();

        /// <summary>
        /// Live license status. The same singleton
        /// instance is used by the About page's license
        /// card, so changes propagate everywhere. The
        /// VM subscribes to LicenseService.StatusChanged
        /// via its own constructor and calls Refresh()
        /// automatically.
        /// </summary>
        public ProGlassAutomation.ViewModels.Licensing.LicenseStatusViewModel
            LicenseStatus =>
            ProGlassAutomation.ViewModels.Licensing.LicenseStatusViewModel.Instance;

        private string _databaseHealthStatus = "Press REFRESH to probe the live databases.";
        public string DatabaseHealthStatus
        {
            get => _databaseHealthStatus;
            private set { _databaseHealthStatus = value; OnPropertyChanged(); }
        }

        private bool _isDiagnosing;
        public bool IsDiagnosing
        {
            get => _isDiagnosing;
            private set
            {
                if (_isDiagnosing == value) return;
                _isDiagnosing = value;
                OnPropertyChanged();
            }
        }

        private bool _isRebuilding;
        public bool IsRebuilding
        {
            get => _isRebuilding;
            private set
            {
                if (_isRebuilding == value) return;
                _isRebuilding = value;
                OnPropertyChanged();
            }
        }

        public ICommand RefreshDatabaseHealthCommand { get; private set; }
        public ICommand RebuildAllDatabasesCommand { get; private set; }

        // ══════════════════════════════════════════════════════════════
        //  v2 — VISUAL THEME & WALLPAPER (Normal / Monet / Professional)
        // ══════════════════════════════════════════════════════════════

        public List<string> VisualThemeOptions { get; } = new()
        {
            ProGlassAutomation.Services.Theme.VisualThemeEngine.Normal,
            ProGlassAutomation.Services.Theme.VisualThemeEngine.Monet,
            ProGlassAutomation.Services.Theme.VisualThemeEngine.Professional,
            ProGlassAutomation.Services.Theme.VisualThemeEngine.Glass,
            ProGlassAutomation.Services.Theme.VisualThemeEngine.Ocean
        };

        public string SelectedVisualTheme
        {
            get => App.Config?.Appearance?.VisualTheme ?? "Normal";
            set
            {
                if (App.Config == null || string.IsNullOrEmpty(value))
                    return;

                App.Config.Appearance.VisualTheme = value;
                SaveConfig();
                ApplyVisualTheme();
                OnPropertyChanged();

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Visual theme changed", value);
            }
        }

        public double WallpaperOpacity
        {
            get => App.Config?.Appearance?.WallpaperOpacity ?? 0.85;
            set
            {
                if (App.Config == null) return;

                App.Config.Appearance.WallpaperOpacity = value;
                SaveConfig();
                ApplyVisualTheme();
                OnPropertyChanged();
            }
        }

        public string WallpaperStatus
        {
            get
            {
                var path = App.Config?.Appearance?.WallpaperPath ?? "";
                return string.IsNullOrEmpty(path)
                    ? "No wallpaper set"
                    : $"Wallpaper: {System.IO.Path.GetFileName(path)}";
            }
        }

        public ICommand SetWallpaperCommand { get; private set; }
        public ICommand ClearWallpaperCommand { get; private set; }

        // ── Per-module wallpapers (§9) ─────────────────────────────────

        public ObservableCollection<ModuleWallpaperRow>
            ModuleWallpaperRows { get; } = new();

        public ICommand SetModuleWallpaperCommand { get; private set; }
        public ICommand ClearModuleWallpaperCommand { get; private set; }

        private void ApplyVisualTheme()
        {
            var appearance = App.Config?.Appearance;
            ProGlassAutomation.Services.Theme.VisualThemeEngine.Apply(
                appearance?.VisualTheme ?? "Normal",
                appearance?.WallpaperPath ?? "",
                appearance?.WallpaperOpacity ?? 0.85);

            // Re-layer the module on screen (its own wallpaper override
            // or the system-wide wallpaper) so changes apply instantly.
            ProGlassAutomation.Services.Theme.VisualThemeEngine.ApplyModule(
                ProGlassAutomation.Services.Theme.VisualThemeEngine
                    .LastRequestedModule,
                appearance);

            // The engine rewrites the surface brushes, and the keyline is
            // derived FROM a surface, so re-derive the accent family
            // against the new surfaces before restoring the user's
            // Defined Card Edges choice on top. Order matters: accent
            // first (writes OutlineDefaultBrush), toggle second (reads
            // it).
            ProGlassAutomation.Services.ThemeManager.ApplyAccent(
                ProGlassAutomation.Services.ThemeManager.AccentColor,
                raiseEvent: false);
            ProGlassAutomation.Services.ThemeManager
                .ReapplyOutlinePreference();
        }

        // ══════════════════════════════════════════════════════════════
        //  v2 — UPDATES (OTA check via GitHub releases)
        // ══════════════════════════════════════════════════════════════

        private string _updateStatus =
            $"Current version: v{Services.Updates.AppInfo.Version}";
        public string UpdateStatus
        {
            get => _updateStatus;
            private set { _updateStatus = value; OnPropertyChanged(); }
        }

        private bool _isUpdateAvailable;
        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            private set { _isUpdateAvailable = value; OnPropertyChanged(); }
        }

        private int _updateDownloadProgress;
        public int UpdateDownloadProgress
        {
            get => _updateDownloadProgress;
            private set
            {
                _updateDownloadProgress = value;
                OnPropertyChanged();
            }
        }

        private bool _isDownloadingUpdate;
        public bool IsDownloadingUpdate
        {
            get => _isDownloadingUpdate;
            private set
            {
                _isDownloadingUpdate = value;
                OnPropertyChanged();
            }
        }

        private Services.Updates.UpdateCheckResult? _lastUpdateCheck;

        // ── v2 §34: release info display ─────────────────────────────

        /// <summary>Version this build is running.</summary>
        public string CurrentVersionDisplay =>
            Services.Updates.AppInfo.Version;

        private string _latestVersionDisplay = "";
        public string LatestVersionDisplay
        {
            get => _latestVersionDisplay;
            private set { _latestVersionDisplay = value; OnPropertyChanged(); }
        }

        private string _releaseNameDisplay = "";
        public string ReleaseNameDisplay
        {
            get => _releaseNameDisplay;
            private set { _releaseNameDisplay = value; OnPropertyChanged(); }
        }

        private string _releasePublishedDisplay = "";
        public string ReleasePublishedDisplay
        {
            get => _releasePublishedDisplay;
            private set { _releasePublishedDisplay = value; OnPropertyChanged(); }
        }

        private string _releaseNotesPreview = "";
        public string ReleaseNotesPreview
        {
            get => _releaseNotesPreview;
            private set { _releaseNotesPreview = value; OnPropertyChanged(); }
        }

        private bool _hasReleaseInfo;
        public bool HasReleaseInfo
        {
            get => _hasReleaseInfo;
            private set { _hasReleaseInfo = value; OnPropertyChanged(); }
        }

        // ── v3 OTA §7: state-driven display ──────────────────────────

        /// <summary>True when a downloaded update can be installed.</summary>
        private bool _isUpdateReady;
        public bool IsUpdateReady
        {
            get => _isUpdateReady;
            private set { _isUpdateReady = value; OnPropertyChanged(); }
        }

        /// <summary>True while a check is in flight (disables buttons).</summary>
        private bool _isCheckingUpdate;
        public bool IsCheckingUpdate
        {
            get => _isCheckingUpdate;
            private set { _isCheckingUpdate = value; OnPropertyChanged(); }
        }

        /// <summary>"14 Aug 2026 • 10:42 AM" style last-check stamp.</summary>
        private string _lastCheckedDisplay = "";
        public string LastCheckedDisplay
        {
            get => _lastCheckedDisplay;
            private set { _lastCheckedDisplay = value; OnPropertyChanged(); }
        }

        // ── v2 §34: update history ledger ────────────────────────────

        public System.Collections.ObjectModel.ObservableCollection
            <Services.Updates.UpdateHistoryEntry> UpdateHistory { get; } =
            new();

        private bool _hasUpdateHistory;
        public bool HasUpdateHistory
        {
            get => _hasUpdateHistory;
            private set { _hasUpdateHistory = value; OnPropertyChanged(); }
        }

        /// <summary>Loads the persisted update-event ledger.</summary>
        /// <summary>Max history rows shown (keeps the card compact).</summary>
        private const int MaxDisplayedHistory = 6;

        public async Task LoadUpdateHistoryAsync()
        {
            var entries = await Services.Updates.UpdateHistoryService
                .ReadAllAsync();

            ProGlassAutomation.ViewModels.Shared.UiDispatch.Run(() =>
            {
                UpdateHistory.Clear();
                foreach (var e in entries.Take(MaxDisplayedHistory))
                    UpdateHistory.Add(e);
                HasUpdateHistory = UpdateHistory.Count > 0;
            });
        }

        /// <summary>
        /// v3 OTA: projects UpdateService state changes onto the UI.
        /// Raised from a background thread — marshals to the dispatcher.
        /// </summary>
        private void OnUpdateServiceStateChanged()
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var s = Services.Updates.UpdateService.State;
                var d = Services.Updates.UpdateService.StateDetail;

                IsCheckingUpdate =
                    s == Services.Updates.UpdateState.Checking;
                IsDownloadingUpdate =
                    s == Services.Updates.UpdateState.Downloading;
                IsUpdateReady =
                    s == Services.Updates.UpdateState.DownloadCompleted;
                UpdateDownloadProgress =
                    Services.Updates.UpdateService.DownloadPercent;

                switch (s)
                {
                    case Services.Updates.UpdateState.Checking:
                        UpdateStatus = "Checking for updates…";
                        break;
                    case Services.Updates.UpdateState.UpToDate:
                        UpdateStatus =
                            $"✓ You're up to date (v{AppVersionSafe()}).";
                        break;
                    case Services.Updates.UpdateState.UpdateAvailable:
                        UpdateStatus = $"🚀 Update available: v{d}";
                        break;
                    case Services.Updates.UpdateState.Downloading:
                        UpdateStatus =
                            $"Downloading… {Services.Updates.UpdateService.DownloadPercent}%";
                        break;
                    case Services.Updates.UpdateState.DownloadCompleted:
                        UpdateStatus =
                            "Update ready. Click Install & restart.";
                        break;
                    case Services.Updates.UpdateState.Installing:
                        UpdateStatus = "Installing — restarting soon…";
                        break;
                    case Services.Updates.UpdateState.Failed:
                        UpdateStatus = $"❌ Update failed: {d}";
                        break;
                    case Services.Updates.UpdateState.Offline:
                        UpdateStatus =
                            "No internet connection. Update check unavailable.";
                        break;
                    case Services.Updates.UpdateState.Cancelled:
                        UpdateStatus = "Update cancelled.";
                        break;
                }
            });
        }

        private static string AppVersionSafe()
        {
            try { return Services.Updates.AppInfo.Version; }
            catch { return ""; }
        }

        private void ApplyReleaseInfo(
            Services.Updates.UpdateCheckResult result)
        {
            LatestVersionDisplay = result.LatestVersion;
            ReleaseNameDisplay = string.IsNullOrEmpty(result.ReleaseName)
                ? $"Release v{result.LatestVersion}"
                : result.ReleaseName;
            ReleasePublishedDisplay = result.PublishedAtUtc.HasValue
                ? result.PublishedAtUtc.Value.ToLocalTime()
                    .ToString("dd MMM yyyy")
                : "";
            ReleaseNotesPreview = (result.ReleaseNotes ?? "").Length > 400
                ? result.ReleaseNotes[..400] + "…"
                : (result.ReleaseNotes ?? "");
            HasReleaseInfo = !string.IsNullOrEmpty(result.LatestVersion);
        }

        public ICommand CheckForUpdatesCommand { get; private set; }
        public ICommand DownloadUpdateCommand { get; private set; }

        /// <summary>v3 OTA: launches the downloaded installer & exits.</summary>
        public ICommand InstallUpdateCommand { get; private set; }

        // ══════════════════════════════════════════════════════════════
        //  v2 — BACKUP & RESTORE (full dataset sets)
        // ══════════════════════════════════════════════════════════════

        public ObservableCollection<
            Services.Database.DatabaseBackup.BackupSetInfo> BackupSets
        { get; } = new();

        private Services.Database.DatabaseBackup.BackupSetInfo? _selectedBackup;
        public Services.Database.DatabaseBackup.BackupSetInfo? SelectedBackup
        {
            get => _selectedBackup;
            set { _selectedBackup = value; OnPropertyChanged(); }
        }

        private string _backupStatus = "";
        public string BackupStatus
        {
            get => _backupStatus;
            private set { _backupStatus = value; OnPropertyChanged(); }
        }

        public ICommand CreateBackupNowCommand { get; private set; }
        public ICommand RestoreSelectedBackupCommand { get; private set; }
        public ICommand RefreshBackupsCommand { get; private set; }
        public ICommand OpenBackupsFolderCommand { get; private set; }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        private static void SaveConfig()
        {
            if (App.Config != null)
                ConfigService.Save(App.Config);
        }

        private void InitializeV2Sections()
        {
            // ── Security commands ─────────────────────────────────────
            RemoveSavedKeysCommand = new RelayCommand(_ =>
            {
                var confirm = System.Windows.MessageBox.Show(
                    "Remove all saved API keys from this device?\n\n" +
                    "They will also be cleared from memory — AI stops " +
                    "working until keys are entered again.",
                    "Remove Saved Keys",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm != System.Windows.MessageBoxResult.Yes) return;

                Services.Security.SecretVault.RemoveAll();

                if (App.Config?.Ai != null)
                {
                    App.Config.Ai.GeminiApiKey = "";
                    App.Config.Ai.GroqApiKey = "";
                    App.Config.Ai.CohereApiKey = "";
                    SaveConfig();
                }

                OnPropertyChanged(nameof(GeminiApiKey));
                OnPropertyChanged(nameof(GroqApiKey));
                OnPropertyChanged(nameof(CohereApiKey));

                Services.Audit.AuditService.Log(
                    Services.Audit.AuditSections.Settings,
                    "API keys removed",
                    "All stored AI keys deleted from device");
            });

            LoadSampleDataCommand = new RelayCommand(async _ =>
            {
                try
                {
                    var summary = await ProGlassAutomation.Services
                        .SampleData.SampleDataService.SeedAsync();
                    System.Windows.MessageBox.Show(
                        summary, "Sample Data",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        "Could not load sample data: " + ex.Message,
                        "Sample Data", System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            });

            ClearAllDataCommand = new RelayCommand(async _ =>
            {
                string folder = Services.Database.AppDataRoot.IsDemo
                    ? Services.Database.AppDataRoot.DemoFolder
                    : Services.Database.AppDataRoot.ProductionFolder;
                string which = Services.Database.AppDataRoot.IsDemo
                    ? "the isolated DEMO folder (production files stay closed)"
                    : "the PRODUCTION folder";

                var confirm = System.Windows.MessageBox.Show(
                    "This will PERMANENTLY DELETE every record in " +
                    which + ":\n\n" +
                    folder + "\n\n" +
                    "  • Glass sheets\n" +
                    "  • Proforma invoices\n" +
                    "  • Job orders\n" +
                    "  • Delivery notes\n" +
                    "  • Tax invoices\n\n" +
                    "This cannot be undone (take a backup first if in " +
                    "doubt).\n\nContinue?",
                    "Clear All Data",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm != System.Windows.MessageBoxResult.Yes) return;

                try
                {
                    var summary = await ProGlassAutomation.Services
                        .SampleData.SampleDataService.ClearAllDataAsync();
                    System.Windows.MessageBox.Show(
                        summary, "Clear All Data",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        "Could not clear data: " + ex.Message,
                        "Clear All Data", System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            });

                        ClearAiMemoryCommand = new RelayCommand(async _ =>
            {
                await Services.AI.AIMemory.AiMemoryService.ClearAsync();
                Services.Audit.AuditService.Log(
                    Services.Audit.AuditSections.Settings,
                    "AI memory cleared", "");
            });

            // ── Visual theme & wallpaper commands ─────────────────────
            SetWallpaperCommand = new RelayCommand(_ =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Choose wallpaper image",
                    Filter = "Images|*.jpg;*.jpeg;*.png;*.webp;*.bmp|" +
                             "All files|*.*"
                };

                if (dialog.ShowDialog() != true) return;

                if (App.Config == null) return;
                App.Config.Appearance.WallpaperPath = dialog.FileName;
                SaveConfig();
                ApplyVisualTheme();

                // Re-layer the module on screen so the new system-wide
                // wallpaper is visible immediately.
                ProGlassAutomation.Services.Theme.VisualThemeEngine
                    .ApplyModule(
                        ProGlassAutomation.Services.Theme.VisualThemeEngine
                            .LastRequestedModule,
                        App.Config.Appearance);

                OnPropertyChanged(nameof(WallpaperStatus));
                OnPropertyChanged(nameof(WallpaperOpacity));

                Services.Audit.AuditService.Log(
                    Services.Audit.AuditSections.Settings,
                    "Wallpaper set",
                    System.IO.Path.GetFileName(dialog.FileName));
            });

            ClearWallpaperCommand = new RelayCommand(_ =>
            {
                if (App.Config == null) return;
                App.Config.Appearance.WallpaperPath = "";
                SaveConfig();
                ApplyVisualTheme();

                // Re-layer the module on screen so the removal of the
                // system-wide wallpaper is visible immediately.
                ProGlassAutomation.Services.Theme.VisualThemeEngine
                    .ApplyModule(
                        ProGlassAutomation.Services.Theme.VisualThemeEngine
                            .LastRequestedModule,
                        App.Config.Appearance);

                OnPropertyChanged(nameof(WallpaperStatus));
            });

            // Apply the persisted visual theme once Settings loads.
            ApplyVisualTheme();

            // v2 §34: show the persisted update history on open.
            _ = LoadUpdateHistoryAsync();

            // ── Module wallpaper rows (§9) ────────────────────────────
            foreach (var (key, label) in
                ProGlassAutomation.Services.Theme.WallpaperModules.All)
            {
                ModuleWallpaperRows.Add(new ModuleWallpaperRow(key, label));
            }

            SetModuleWallpaperCommand = new RelayCommand(param =>
            {
                if (param is not ModuleWallpaperRow row) return;

                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = $"Wallpaper for {row.Label}",
                    Filter = "Images|*.jpg;*.jpeg;*.png;*.webp;*.bmp|" +
                             "All files|*.*"
                };

                if (dialog.ShowDialog() != true) return;

                row.SetPath(dialog.FileName);

                Services.Audit.AuditService.Log(
                    Services.Audit.AuditSections.Settings,
                    "Module wallpaper set",
                    $"{row.Label} → " +
                    System.IO.Path.GetFileName(dialog.FileName));
            });

            ClearModuleWallpaperCommand = new RelayCommand(param =>
            {
                if (param is not ModuleWallpaperRow row) return;
                row.ClearPath();

                Services.Audit.AuditService.Log(
                    Services.Audit.AuditSections.Settings,
                    "Module wallpaper cleared", row.Label);
            });

            // ── Update commands (v3 OTA orchestrator, spec §6-§8) ─────
            // All heavy lifting lives in Services/Updates/UpdateService;
            // this VM only projects its state machine onto the UI.

            Services.Updates.UpdateService.StateChanged +=
                OnUpdateServiceStateChanged;

            CheckForUpdatesCommand = new RelayCommand(async _ =>
            {
                if (IsCheckingUpdate) return;
                IsCheckingUpdate = true;
                UpdateStatus = "Checking for updates…";

                // Installed builds use the Velopack OTA path.
                if (Services.Updates.VelopackUpdateService.IsInstalled)
                {
                    var latest = await Services.Updates.VelopackUpdateService
                        .CheckAsync();
                    IsCheckingUpdate = false;
                    LastCheckedDisplay = "Last checked: " +
                        DateTime.Now.ToString("dd MMM yyyy • h:mm tt");
                    if (!string.IsNullOrEmpty(latest))
                    {
                        IsUpdateAvailable = true;
                        IsUpdateReady = false;
                        UpdateStatus = "Update available: v" + latest;
                    }
                    else
                    {
                        IsUpdateAvailable = false;
                        UpdateStatus = "You are up to date (v" +
                            Services.Updates.AppInfo.Version + ").";
                    }
                    await LoadUpdateHistoryAsync();
                    return;
                }

                var result = await Services.Updates.UpdateService
                    .CheckNowAsync();

                _lastUpdateCheck = result;

                if (result.Success)
                {
                    IsUpdateAvailable = result.UpdateAvailable;
                    ApplyReleaseInfo(result);

                    var checkedAt = Services.Updates.UpdateService
                        .LastCheckUtc;
                    LastCheckedDisplay = checkedAt.HasValue
                        ? "Last checked: " + checkedAt.Value.ToLocalTime()
                            .ToString("dd MMM yyyy • h:mm tt")
                        : "";

                    if (!result.UpdateAvailable)
                        UpdateStatus =
                            $"✓ You're up to date " +
                            $"(v{Services.Updates.AppInfo.Version}).";
                    // UpdateAvailable status text arrives via the
                    // StateChanged handler ("🚀 Update available…").
                }
                else
                {
                    UpdateStatus = Services.Updates.UpdateService.State ==
                        Services.Updates.UpdateState.Offline
                        ? "No internet connection. Update check unavailable."
                        : $"Could not check for updates: {result.ErrorMessage}";
                }

                IsCheckingUpdate = false;
                await LoadUpdateHistoryAsync();
            });

            DownloadUpdateCommand = new RelayCommand(async _ =>
            {
                // R-2: this command has no canExecute, and the lambda is
                // async void, so a second click during the download would
                // start a concurrent download of the same package.
                if (IsDownloadingUpdate) return;

                // Installed builds download via Velopack (delta packages).
                if (Services.Updates.VelopackUpdateService.IsInstalled)
                {
                    if (!Services.Updates.VelopackUpdateService.HasPendingUpdate)
                    {
                        UpdateStatus = "Check for updates first.";
                        return;
                    }
                    IsDownloadingUpdate = true;
                    UpdateDownloadProgress = 0;
                    UpdateStatus = "Downloading update…";
                    bool okV;
                    try
                    {
                        okV = await Services.Updates.VelopackUpdateService
                            .DownloadAsync(p2 => System.Windows.Application.Current
                                ?.Dispatcher.InvokeAsync(
                                    () => UpdateDownloadProgress = p2));
                    }
                    finally
                    {
                        // R-2: without this, a throwing download would leave
                        // the flag set and the button permanently disabled.
                        IsDownloadingUpdate = false;
                    }
                    if (okV)
                    {
                        IsUpdateReady = true;
                        UpdateStatus = "Update ready. Click Install and restart.";
                    }
                    else
                    {
                        UpdateStatus = "Download failed. Try again.";
                    }
                    return;
                }

                var update = _lastUpdateCheck;
                if (update == null || IsDownloadingUpdate) return;

                IsDownloadingUpdate = true;
                UpdateDownloadProgress = 0;
                UpdateStatus = "Downloading update…";

                bool ok; string error;
                try
                {
                    (ok, _, error) = await Services.Updates.UpdateService
                        .DownloadNowAsync(
                            update,
                            progress => System.Windows.Application.Current
                                ?.Dispatcher.InvokeAsync(
                                    () => UpdateDownloadProgress = progress));
                }
                finally
                {
                    // R-2: release on the exception path too.
                    IsDownloadingUpdate = false;
                }

                if (!ok && string.IsNullOrEmpty(error))
                    UpdateStatus = "Update cancelled.";
                else if (!ok)
                    UpdateStatus = $"Download failed: {error}";

                // Success status ("Update ready.") arrives via the
                // StateChanged handler.
                await LoadUpdateHistoryAsync();
            });

            InstallUpdateCommand = new RelayCommand(_ =>
            {
                // Installed builds: apply delta update + restart in place.
                if (Services.Updates.VelopackUpdateService.IsInstalled)
                {
                    UpdateStatus =
                        "Applying update — ProGlass will restart. " +
                        "Your data stays safe.";
                    Services.Updates.VelopackUpdateService.ApplyAndRestart();
                    return;
                }

                if (!Services.Updates.UpdateService.CanInstall) return;

                UpdateStatus =
                    "Launching installer — ProGlass will close so the " +
                    "update can be applied. Your data stays safe.";

                Services.Updates.UpdateService.InstallAndRestart();
            });

            // ── Backup commands ───────────────────────────────────────
            RefreshBackupsCommand = new RelayCommand(_ =>
            {
                // Visible feedback so the click never feels dead.
                BackupStatus = "Refreshing backup list…";
                RefreshBackupSets();
                BackupStatus = BackupSets.Count > 0
                    ? $"✓ {BackupSets.Count} backup set(s) listed."
                    : "No backup sets found yet.";
            });

            CreateBackupNowCommand = new RelayCommand(_ =>
            {
                // BackupService is a STATIC class — call members directly
                // (a static type cannot be assigned to a variable).

                // Professional behaviour: warn if a backup was made very
                // recently so the list isn't flooded by repeated clicks.
                var sinceLast = Services.Database.DatabaseBackup.BackupService
                    .TimeSinceLastBackup();
                if (sinceLast.HasValue &&
                    sinceLast.Value < Services.Database.DatabaseBackup.BackupService
                        .ManualWarnWindow)
                {
                    var mins = (int)sinceLast.Value.TotalMinutes;
                    var confirm = System.Windows.MessageBox.Show(
                        $"A backup was created {mins} minute(s) ago.\n\n" +
                        "Create another backup now?",
                        "Backup Already Recent",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);
                    if (confirm != System.Windows.MessageBoxResult.Yes)
                        return;
                }

                var set = Services.Database.DatabaseBackup.BackupService
                    .CreateFullBackupSet("manual (Settings)");

                BackupStatus = set.Length > 0
                    ? $"✓ Backup created: {System.IO.Path.GetFileName(set)}"
                    : "✗ Backup failed — see Logs & Activity for details.";

                RefreshBackupSets();
            });

            RestoreSelectedBackupCommand = new RelayCommand(_ =>
            {
                if (Services.Database.AppDataRoot.IsDemo)
                {
                    BackupStatus =
                        "Restore is blocked in Demo Mode. Exit Demo Mode first — " +
                        "restore writes the production folder.";
                    System.Windows.MessageBox.Show(
                        "Demo Mode is on. Restore would overwrite production files, " +
                        "so it is blocked. Exit Demo Mode, then restore.",
                        "Restore blocked",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                var selected = SelectedBackup;
                if (selected == null)
                {
                    // Never fail silently — tell the user what to do.
                    BackupStatus =
                        "Select a backup set from the list first.";
                    return;
                }

                var confirm = System.Windows.MessageBox.Show(
                    $"Restore backup from {selected.CreatedAt:dd MMM yyyy HH:mm}?\n\n" +
                    "This OVERWRITES all current data with the backup.\n" +
                    "A safety snapshot of the current data is taken first.",
                    "Restore Backup",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm != System.Windows.MessageBoxResult.Yes) return;

                BackupStatus = "Restoring backup — please wait…";

                var (ok, message) = Services.Database.DatabaseBackup
                    .BackupService.RestoreBackupSet(selected.FolderPath);

                BackupStatus = (ok ? "✓ " : "✗ ") + message;
                RefreshBackupSets();

                if (!ok) return;

                // The restored database only loads after a restart —
                // surface that explicitly instead of a tiny status line.
                var restart = System.Windows.MessageBox.Show(
                    "Backup restored successfully.\n\n" +
                    "ProGlass must restart to load the restored data.\n" +
                    "Restart now?",
                    "Restart Required",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (restart == System.Windows.MessageBoxResult.Yes)
                    RestartApplication();
            });

            OpenBackupsFolderCommand = new RelayCommand(_ =>
            {
                try
                {
                    var folder = System.IO.Path.Combine(
                        System.Environment.GetFolderPath(
                            System.Environment.SpecialFolder.ApplicationData),
                        "ProGlassAutomation", "Backups");

                    System.IO.Directory.CreateDirectory(folder);
                                ProcessStartGuard.TryOpenFolder(folder, CorrelationContext.NewId());
                }
                catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            });

            // ── Database health (diagnostics + emergency rebuild) ─────
            //
            // Diagnostics is read-only and safe for any signed-in user.
            // Rebuild is destructive: it MOVES every .db file to
            // MigrationBackups/_rebuild_<timestamp>/, then recreates the
            // files at the current schema. It is gated behind a Manager+
            // role check (the same gate ClearAllData uses) and a typed
            // confirmation so a misclick cannot destroy data.
            RefreshDatabaseHealthCommand = new RelayCommand(
                async _ => await RunDatabaseDiagnosticsAsync(),
                _ => !IsDiagnosing && !IsRebuilding);

            RebuildAllDatabasesCommand = new RelayCommand(
                async _ => await RebuildAllDatabasesAsync(),
                _ => !IsDiagnosing && !IsRebuilding);

            RefreshBackupSets();
        }

        private async Task RunDatabaseDiagnosticsAsync()
        {
            if (IsDiagnosing || IsRebuilding) return;
            IsDiagnosing = true;
            DatabaseHealthStatus = "Probing live databases…";
            try
            {
                var rows = await SchemaHealthService.DiagnoseAsync();
                ProGlassAutomation.ViewModels.Shared.UiDispatch.Run(() =>
                {
                    DatabaseHealthRows.Clear();
                    foreach (var r in rows) DatabaseHealthRows.Add(r);
                });
                int populated = rows.Count(r => r.FileExists && r.TotalRows > 0);
                int drift = rows.Sum(r => r.DriftNotes.Count);
                DatabaseHealthStatus =
                    $"✓ {rows.Count} database(s) probed — " +
                    $"{populated} contain data, " +
                    $"{drift} schema note(s). " +
                    "If a note lists \"missing\" columns, run the schema " +
                    "migration on next app start (it runs automatically).";
            }
            catch (Exception ex)
            {
                DatabaseHealthStatus = "✗ Diagnostics failed: " + ex.Message;
                AppLogService.Error(
                    "Database diagnostics failed", ex,
                    new Services.Logging.LogContext
                    {
                        Module = "Database",
                        Class = nameof(SettingsViewModel),
                        Operation = "RunDatabaseDiagnostics"
                    });
            }
            finally
            {
                IsDiagnosing = false;
            }
        }

        private async Task RebuildAllDatabasesAsync()
        {
            if (IsDiagnosing || IsRebuilding) return;

            // Gate 1: Manager+ only. Same role the ClearAllData
            // command uses, so a non-manager can never reach this
            // destructive path even by typing the wrong key.
            string role = Services.Audit.AuditService.CurrentUser?.Role ?? "";
            bool isManagerOrAbove =
                string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Administrator", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
            if (!isManagerOrAbove)
            {
                System.Windows.MessageBox.Show(
                    "Rebuilding all databases is restricted to Managers " +
                    "and Administrators. Your role: " +
                    (string.IsNullOrEmpty(role) ? "none" : role) + ".",
                    "Permission Denied",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Gate 2: confirm the user knows what they're doing.
            var proceed = System.Windows.MessageBox.Show(
                "EMERGENCY DATABASE REBUILD\n\n" +
                "This will:\n" +
                "  1. Move every .db file to MigrationBackups/_rebuild_<timestamp>/\n" +
                "  2. Recreate each one at the CURRENT schema\n" +
                "  3. Re-run the schema migration on inquiries.db\n\n" +
                "All transactional data (inquiries, PIs, JOs, DNs, TIs, " +
                "statements, sheet store) will be LOST. Master data, audit " +
                "log and AI memory are also wiped.\n\n" +
                "The originals are MOVED (not deleted) and can be copied " +
                "back by hand from the MigrationBackups folder.\n\n" +
                "ProGlass must be restarted afterwards.\n\n" +
                "Continue?",
                "Emergency Database Rebuild",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No);

            if (proceed != System.Windows.MessageBoxResult.Yes) return;

            IsRebuilding = true;
            DatabaseHealthStatus = "Rebuilding — this can take a few seconds…";
            try
            {
                var progress = new Progress<string>(msg =>
                    DatabaseHealthStatus = msg);

                var summary = await SchemaHealthService
                    .RebuildAllAsync(progress);

                AppLogService.Info(
                    "[Rebuild] " + summary,
                    new Services.Logging.LogContext
                    {
                        Module = "Database",
                        Class = nameof(SettingsViewModel),
                        Operation = "RebuildAllDatabases"
                    });

                // Refresh the row-count table so the user sees the
                // freshly-empty databases immediately.
                await RunDatabaseDiagnosticsAsync();

                // Offer the restart. The user can defer it (e.g. they
                // want to copy files out of MigrationBackups first) but
                // the app's pooled connections still point at the
                // moved files until restart.
                var restart = System.Windows.MessageBox.Show(
                    "✓ All databases were rebuilt at the current schema.\n\n" +
                    "The originals are saved in:\n" +
                    AppDataRoot.CurrentFolder + "\\MigrationBackups\\\n\n" +
                    "ProGlass must be restarted to load the new databases.\n\n" +
                    "Restart now?",
                    "Rebuild complete",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information,
                    System.Windows.MessageBoxResult.Yes);

                if (restart == System.Windows.MessageBoxResult.Yes)
                    RestartApplication();
            }
            catch (Exception ex)
            {
                DatabaseHealthStatus = "✗ Rebuild failed: " + ex.Message;
                AppLogService.Error(
                    "Emergency database rebuild failed", ex,
                    new Services.Logging.LogContext
                    {
                        Module = "Database",
                        Class = nameof(SettingsViewModel),
                        Operation = "RebuildAllDatabases"
                    });

                System.Windows.MessageBox.Show(
                    "Rebuild failed: " + ex.Message +
                    "\n\nThe original databases are still in " +
                    "MigrationBackups and have NOT been deleted.",
                    "Rebuild failed",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsRebuilding = false;
            }
        }

        private void RefreshBackupSets()
        {
            BackupSets.Clear();
            foreach (var set in Services.Database.DatabaseBackup
                .BackupService.GetBackupSets())
            {
                BackupSets.Add(set);
            }
        }

        // Phase 4: Event lifetime — detach static events
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { ThemeManager.ThemeChanged -= _themeChangedHandler; } catch { }
            try { ThemeManager.UiScaleChanged -= _uiScaleChangedHandler; } catch { }
            try { Services.Updates.UpdateService.StateChanged -= OnUpdateServiceStateChanged; } catch { }
            GC.SuppressFinalize(this);
        }

        /// <summary>Relaunches the exe and closes this instance (used
        /// after a backup restore, which only loads after a restart).</summary>
        private static void RestartApplication()
        {
            try
            {
                var exe = System.Diagnostics.Process
                    .GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exe)) return;

                            // Phase 8: Restart via guard — validate exe is inside app folder
            if (ProcessStartGuard.IsPathInsideAllowedRoot(exe)) Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = exe, UseShellExecute = true });

                System.Windows.Application.Current.Shutdown();
            }
            catch
            {
                // Restart is a convenience — never crash for it.
            }
        }
    }
}