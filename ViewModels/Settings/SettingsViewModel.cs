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
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIVoice;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Threading;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Settings
{
    public sealed class SettingsViewModel : ViewModelBase
    {
        private string _geminiTestStatus = string.Empty;
        private string _groqTestStatus = string.Empty;
        private string _cohereTestStatus = string.Empty;
        private bool _apiKeyJustSaved;
        private bool _isDownloadingModel;
        private int _downloadProgress;
        private string _voiceStatus = string.Empty;
        private CancellationTokenSource _downloadCts;

        public string ConfigFilePath { get; }

        // ══════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════

        public SettingsViewModel()
        {
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
                    progress =>
                    {
                        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                        {
                            DownloadProgress = progress;
                            VoiceStatus = $"Downloading… {progress}%";
                        });
                    },
                    _downloadCts.Token);

                IsDownloadingModel = false;

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
                            System.Diagnostics.Debug.WriteLine(
                                "[Settings] Switched STT → Whisper after download.");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
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

            ThemeManager.ThemeChanged += (_, __) =>
                OnPropertyChanged(nameof(IsDarkTheme));

            // Dynamic Theme Engine — accent presets (Settings UI)
            ApplyAccentCommand = new RelayCommand(param =>
                ApplyAccentPreset(param as string ?? "GlassBlue"));

            // v2 sections: security keys, OTA updates, backup & restore
            InitializeV2Sections();
        }

        // ══════════════════════════════════════════════════════════════
        //  APPEARANCE
        // ══════════════════════════════════════════════════════════════

        public bool IsDarkTheme
        {
            get => ThemeManager.CurrentTheme == AppTheme.Dark;
            set
            {
                ThemeManager.ApplyTheme(value ? AppTheme.Dark : AppTheme.Light);

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
            var color = presetKey switch
            {
                "Emerald" => (System.Windows.Media.Color?)
                    System.Windows.Media.Color.FromRgb(0x05, 0x96, 0x69),
                "Amber" =>
                    System.Windows.Media.Color.FromRgb(0xD9, 0x77, 0x06),
                "Rose" =>
                    System.Windows.Media.Color.FromRgb(0xE1, 0x1D, 0x48),
                "Violet" =>
                    System.Windows.Media.Color.FromRgb(0x7C, 0x3A, 0xED),
                "Teal" =>
                    System.Windows.Media.Color.FromRgb(0x0D, 0x94, 0x88),
                _ => (System.Windows.Media.Color?)null   // Glass Blue default
            };

            ThemeManager.ApplyAccent(color);

            ProGlassAutomation.Services.Audit.AuditService.Log(
                ProGlassAutomation.Services.Audit.AuditSections.Settings,
                "Accent changed",
                presetKey == "GlassBlue" ? "Glass Blue (default)" : presetKey);
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
            get => App.Config?.Ai?.UseRealData ?? true;
            set
            {
                if (App.Config?.Ai != null)
                {
                    App.Config.Ai.UseRealData = value;
                    SaveConfig();
                    OnPropertyChanged();
                }
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

        // ══════════════════════════════════════════════════════════════
        //  v2 — VISUAL THEME & WALLPAPER (Normal / Monet / Professional)
        // ══════════════════════════════════════════════════════════════

        public List<string> VisualThemeOptions { get; } = new()
        {
            ProGlassAutomation.Services.Theme.VisualThemeEngine.Normal,
            ProGlassAutomation.Services.Theme.VisualThemeEngine.Monet,
            ProGlassAutomation.Services.Theme.VisualThemeEngine.Professional
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

        /// <summary>One configurable row on the Module Wallpapers list.</summary>
        public class ModuleWallpaperRow : ViewModelBase
        {
            public string Key { get; }
            public string Label { get; }

            public ModuleWallpaperRow(string key, string label)
            {
                Key = key;
                Label = label;
            }

            private ModuleWallpaperSettings Settings()
            {
                var appearance = App.Config!.Appearance;
                if (!appearance.ModuleWallpapers.TryGetValue(Key, out var s)
                    || s == null)
                {
                    s = new ModuleWallpaperSettings();
                    appearance.ModuleWallpapers[Key] = s;
                }
                return s;
            }

            public bool Enabled
            {
                get => Settings().Enabled;
                set
                {
                    Settings().Enabled = value;
                    SaveConfig();
                    Refresh();
                    OnPropertyChanged();
                }
            }

            public string FileName
            {
                get
                {
                    var path = Settings().Path;
                    return string.IsNullOrEmpty(path)
                        ? "No image"
                        : System.IO.Path.GetFileName(path);
                }
            }

            public double OpacityValue
            {
                get => Settings().Opacity;
                set
                {
                    Settings().Opacity = value;
                    SaveConfig();
                    Refresh();
                    OnPropertyChanged();
                }
            }

            internal void SetPath(string path)
            {
                var s = Settings();
                s.Path = path;
                s.Enabled = true;
                SaveConfig();
                Refresh();
                OnPropertyChanged(nameof(Enabled));
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(OpacityValue));
            }

            internal void ClearPath()
            {
                var s = Settings();
                s.Path = "";
                s.Enabled = false;
                SaveConfig();
                Refresh();
                OnPropertyChanged(nameof(Enabled));
                OnPropertyChanged(nameof(FileName));
            }

            /// <summary>
            /// Re-layer the wallpaper of whichever module is on screen
            /// right now, so changes are visible immediately.
            /// </summary>
            private static void Refresh() =>
                ProGlassAutomation.Services.Theme.VisualThemeEngine
                    .ApplyModule(
                        ProGlassAutomation.Services.Theme.VisualThemeEngine
                            .CurrentModule,
                        App.Config?.Appearance);
        }

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

        public async void LoadUpdateHistory()
        {
            var entries = await Services.Updates.UpdateHistoryService
                .ReadAllAsync();

            UpdateHistory.Clear();
            foreach (var e in entries.Take(MaxDisplayedHistory))
                UpdateHistory.Add(e);

            HasUpdateHistory = UpdateHistory.Count > 0;
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
                OnPropertyChanged(nameof(WallpaperStatus));
            });

            // Apply the persisted visual theme once Settings loads.
            ApplyVisualTheme();

            // v2 §34: show the persisted update history on open.
            LoadUpdateHistory();

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

            // ── Update commands ───────────────────────────────────────
            CheckForUpdatesCommand = new RelayCommand(async _ =>
            {
                UpdateStatus = "Checking for updates…";

                var result = await Services.Updates.UpdateService
                    .CheckForUpdateAsync();

                _lastUpdateCheck = result;

                if (!result.Success)
                {
                    UpdateStatus =
                        $"Could not check for updates: {result.ErrorMessage}";
                    Services.Updates.UpdateHistoryService.Record(
                        Services.Updates.UpdateEventType.CheckFailed,
                        "", "", result.ErrorMessage);
                    LoadUpdateHistory();
                    return;
                }

                IsUpdateAvailable = result.UpdateAvailable;
                ApplyReleaseInfo(result);

                UpdateStatus = result.UpdateAvailable
                    ? $"Update available: v{result.LatestVersion}" +
                      (string.IsNullOrEmpty(result.ReleaseName)
                          ? "" : $" — {result.ReleaseName}")
                    : $"You are on the latest version " +
                      $"(v{Services.Updates.AppInfo.Version}).";

                if (result.UpdateAvailable)
                {
                    Services.Updates.UpdateHistoryService.Record(
                        Services.Updates.UpdateEventType.Detected,
                        result.LatestVersion, result.ReleaseName);
                }
                LoadUpdateHistory();
            });

            DownloadUpdateCommand = new RelayCommand(async _ =>
            {
                var update = _lastUpdateCheck;
                if (update == null || IsDownloadingUpdate) return;

                IsDownloadingUpdate = true;
                UpdateDownloadProgress = 0;

                var (ok, path, error) = await Services.Updates.UpdateService
                    .DownloadUpdateAsync(
                        update,
                        progress => System.Windows.Application.Current
                            ?.Dispatcher.InvokeAsync(
                                () => UpdateDownloadProgress = progress));

                IsDownloadingUpdate = false;

                if (ok)
                {
                    UpdateStatus =
                        $"Downloaded to {path}. Run the installer manually — " +
                        "the app never replaces its own files.";

                    Services.Updates.UpdateHistoryService.Record(
                        Services.Updates.UpdateEventType.Downloaded,
                        update.LatestVersion, update.ReleaseName, path);

                    Services.Audit.AuditService.Log(
                        Services.Audit.AuditSections.Settings,
                        "Update downloaded",
                        $"v{update.LatestVersion} → {path}");
                }
                else
                {
                    UpdateStatus = $"Download failed: {error}";
                    Services.Updates.UpdateHistoryService.Record(
                        Services.Updates.UpdateEventType.CheckFailed,
                        update.LatestVersion, "", $"Download failed: {error}");
                }
                LoadUpdateHistory();
            });

            // ── Backup commands ───────────────────────────────────────
            RefreshBackupsCommand = new RelayCommand(_ => RefreshBackupSets());

            CreateBackupNowCommand = new RelayCommand(_ =>
            {
                var backup = Services.Database.DatabaseBackup.BackupService;

                // Professional behaviour: warn if a backup was made very
                // recently so the list isn't flooded by repeated clicks.
                var sinceLast = backup.TimeSinceLastBackup();
                if (sinceLast.HasValue &&
                    sinceLast.Value < backup.ManualWarnWindow)
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

                var set = backup.CreateFullBackupSet("manual (Settings)");

                BackupStatus = set.Length > 0
                    ? $"✓ Backup created: {System.IO.Path.GetFileName(set)}"
                    : "✗ Backup failed — see Logs & Activity for details.";

                RefreshBackupSets();
            });

            RestoreSelectedBackupCommand = new RelayCommand(_ =>
            {
                var selected = SelectedBackup;
                if (selected == null) return;

                var confirm = System.Windows.MessageBox.Show(
                    $"Restore backup from {selected.CreatedAt:dd MMM yyyy HH:mm}?\n\n" +
                    "This OVERWRITES all current data with the backup.\n" +
                    "A safety snapshot of the current data is taken first.",
                    "Restore Backup",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm != System.Windows.MessageBoxResult.Yes) return;

                var (ok, message) = Services.Database.DatabaseBackup
                    .BackupService.RestoreBackupSet(selected.FolderPath);

                BackupStatus = (ok ? "✓ " : "✗ ") + message;
                RefreshBackupSets();
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
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = folder,
                            UseShellExecute = true
                        });
                }
                catch { }
            });

            RefreshBackupSets();
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
    }
}