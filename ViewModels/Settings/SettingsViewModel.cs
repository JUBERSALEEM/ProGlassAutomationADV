// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings (Chat 25 v1.1 — CS0272 fix)
//
// CHANGES vs Chat 25 v1.0:
//   ✓ FIX CS0272: _speechToText setter is now public (fixed in
//     App.xaml.cs). All three assignment sites now compile correctly:
//       _speechToText = new WhisperSpeechService()  ← line 151
//       _speechToText?.Dispose()                    ← line 196
//       _speechToText = new SpeechToTextService()   ← line 197
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
using ProGlassAutomation.Utilities;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ProGlassAutomation.Services.Localization;

using ProGlassAutomation.Services.Dialogs;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.Settings
{
    public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
    {
        private IAiService _ai = null!;
        private readonly AppConfig _config = null!;
        private readonly IDialogService _dialogs = null!;
        private ISpeechToTextService _speechToText = null!;
        private readonly IDatabaseServices _database = null!;
        private readonly Services.AI.IAiProviderEvents _providerEvents = null!;

        public SettingsViewModel(IAiService? ai, AppConfig? config, IDialogService? dialogs, ISpeechToTextService? speechToText, IDatabaseServices? database, Services.AI.IAiProviderEvents? providerEvents = null)
        {
            _ai = ai!;
            _config = config!;
            _dialogs = dialogs!;
            _speechToText = speechToText!;
            _database = database!;
            _providerEvents = providerEvents ?? new Services.AI.AiProviderEvents();
        
            // ERR-633EEB: run shared initialization on EVERY construction
            // path. DI selects this (greediest) ctor, so without this call the
            // parameterless ctor's setup never ran and commands stayed null.
            InitializeCore();
        }

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
        //  CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════

        public SettingsViewModel()
         : this(
            App.ServiceProvider?.GetService<IAiService>()!,
            App.ServiceProvider?.GetService<AppConfig>()?? App.Config,
            App.ServiceProvider?.GetService<IDialogService>()?? new DialogService(),
            App.ServiceProvider?.GetService<ISpeechToTextService>()!,
            App.ServiceProvider?.GetService<IDatabaseServices>()!){ }

        // ── Shared initialization (ERR-633EEB) ─────────────────────
        // Extracted verbatim from the parameterless ctor so both it
        // and the DI-selected parameterized ctor initialize fully.
        private void InitializeCore()
        {
            _config.DataConnectivity ??= new DataConnectivitySettings();

            // v2 shell: navigation between the settings pages.
            SwitchSettingsPageCommand = new RelayCommand(
                p => SettingsPage = p as string ?? "Appearance");

            // Features page: switch which module's field-toggles are shown.
            SwitchFeaturesModuleCommand = new RelayCommand(
                p => FeaturesModule = p as string ?? "Proforma");

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
                    AiProvider.Gemini, _config.Ai);
                var result = await svc.TestConnectionAsync();
                GeminiTestStatus = result.Success
                    ? "✓ " + result.Message
                    : "✗ " + result.Message;
            });

            TestGroqCommand = new RelayCommand(async _ =>
            {
                GroqTestStatus = "Testing…";
                var svc = AiProviderFactory.CreateForProvider(
                    AiProvider.Groq, _config.Ai);
                var result = await svc.TestConnectionAsync();
                GroqTestStatus = result.Success
                    ? "✓ " + result.Message
                    : "✗ " + result.Message;
            });

            TestCohereCommand = new RelayCommand(async _ =>
            {
                CohereTestStatus = "Testing…";
                var svc = AiProviderFactory.CreateForProvider(
                    AiProvider.Cohere, _config.Ai);
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
                    OnPropertyChanged(nameof(WhisperStatusChipBackgroundKey));
                    OnPropertyChanged(nameof(WhisperStatusChipForegroundKey));
                    OnPropertyChanged(nameof(ModelFileSizeMB));
                    OnPropertyChanged(nameof(SelectedModelInfo));

                    // Refresh or swap STT service so mic button appears without restart
                    if (_speechToText is WhisperSpeechService whisper)
                    {
                        // Already using Whisper — just refresh availability
                        whisper.RefreshAvailability();
                    }
                    else
                    {
                        // Was using System.Speech fallback — swap to Whisper
                        // FIX CS0272: _speechToText now has public set
                        try
                        {
                            _speechToText?.Dispose();
                            _speechToText = new WhisperSpeechService();
                            AppLogService.Debug(
                                "[Settings] Switched STT → Whisper after download.");
                        }
                        catch (Exception ex)
                        {
                            AppLogService.Debug(
                                $"[Settings] Whisper swap failed: {ExceptionMessages.Deep(ex)}");
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
                    OnPropertyChanged(nameof(WhisperStatusChipBackgroundKey));
                    OnPropertyChanged(nameof(WhisperStatusChipForegroundKey));
                    OnPropertyChanged(nameof(ModelFileSizeMB));

                    // Swap back to System.Speech fallback
                    // FIX CS0272: _speechToText now has public set
                    if (_speechToText is WhisperSpeechService w)
                    {
                        w.Dispose();
                        try { _speechToText = new SpeechToTextService(); }
                        catch { _speechToText = null; }
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

            // 2026-08-25 — colour palette expansion: free-pick custom
            // colour + palette generated from AccentPresets.All so the
            // swatches shown ARE the colours applied.
            ApplyCustomAccentCommand = new RelayCommand(_ => ApplyCustomAccent());
            InitializeAccentPalette();

            // 2026-09-07 — card-edge colour plate: the same preset palette
            // (AccentPresets.All) drives the "Define Card Edges" swatches;
            // picking one applies it to every card border app-wide.
            ApplyCardEdgePresetCommand = new RelayCommand(param =>
                ApplyCardEdgePreset(param as string ?? ""));
            InitializeCardEdgePalette();
            ApplyCardFrostTintCommand = new RelayCommand(param =>
                ApplyCardFrostTintPreset(param as string ?? ""));

            // 2026-08-25 — global font colour: one user-chosen text
            // colour for the whole UI (Auto = theme/family palette).
            ApplyFontColorCommand = new RelayCommand(param =>
                ApplyFontColorPreset(param as string ??
                    ProGlassAutomation.Services.Theme.FontColorPresets.Auto));
            ApplyCustomFontColorCommand =
                new RelayCommand(_ => ApplyCustomFontColor());
            InitializeFontColorPalette();

            // 2026-08-25 — one-tap factory reset for everything the
            // Appearance module owns (theme, accent, gradients, card
            // shape, density, zoom, font, wallpapers, PDF layout).
            ResetAppearanceCommand = new RelayCommand(async _ => await ResetAppearanceToDefaultsAsync());

            // Global font engine (system-wide UI font)
            InitializeFontSettings();

            // v2 sections: security keys, OTA updates, backup & restore
            InitializeV2Sections();
        }

    }
}