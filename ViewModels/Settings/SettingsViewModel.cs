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
using System.Collections.ObjectModel;
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
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        private static void SaveConfig()
        {
            if (App.Config != null)
                ConfigService.Save(App.Config);
        }
    }
}