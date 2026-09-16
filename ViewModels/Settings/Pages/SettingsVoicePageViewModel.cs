// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/Pages/SettingsVoicePageViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v3 — per-page ViewModel (phase 3b, promotion #2)
//
// Voice page (Whisper model picker + downloader) promoted out of the god
// class after the Sharing pilot: members verbatim from
// SettingsViewModel.Voice.cs + the download/delete wiring from InitializeCore.
// The one cross-page dependency (the shell's _speechToText swap after
// download/delete) travels as a getter/setter pair lent by the shell.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIVoice;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.Fonts;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services;
using ProGlassAutomation.Utilities;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;
using System;

namespace ProGlassAutomation.ViewModels.Settings.Pages
{
    /// <summary>Voice page state: Whisper model catalog, selection,
    /// download progress and the STT service swap around it.</summary>
    public sealed class SettingsVoicePageViewModel : ViewModelBase
    {
        private readonly AppConfig? _config;

        // Lent by the shell: get/set its _speechToText field.
        private readonly Func<ISpeechToTextService?> _getStt;
        private readonly Action<ISpeechToTextService?> _setStt;

        /// <summary>Shell STT service accessor — keeps every moved member
        /// compiling against the exact original field syntax.</summary>
        private ISpeechToTextService? _speechToText
        {
            get => _getStt();
            set => _setStt(value);
        }

        private bool _isDownloadingModel;
        private int _downloadProgress;
        private string _voiceStatus = string.Empty;
        private CancellationTokenSource? _downloadCts;

        public SettingsVoicePageViewModel(
            AppConfig? config,
            Func<ISpeechToTextService?> getSpeechToText,
            Action<ISpeechToTextService?> setSpeechToText)
        {
            _config = config;
            _getStt = getSpeechToText;
            _setStt = setSpeechToText;

            // Build model options list for UI picker
            foreach (var kvp in WhisperModelManager.AvailableModels)
                WhisperModelOptions.Add(kvp.Value);

            InitVoiceCommands();
        }

        /// <summary>Same write-through the shell uses — the moved
        /// SelectedWhisperModel setter calls this after a model swap.</summary>
        private void SaveConfig()
        {
            if (_config != null)
                ConfigService.SaveDebounced(_config);
        }

        private void InitVoiceCommands()
        {
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
        }

        // ══════════════════════════════════════════════════════════════
        //  WHISPER MODEL MANAGEMENT
        // ══════════════════════════════════════════════════════════════

        public ObservableCollection<WhisperModelInfo> WhisperModelOptions { get; }
            = new ObservableCollection<WhisperModelInfo>();

        public string SelectedWhisperModel
        {
            get => _config?.Ai?.SelectedWhisperModel ?? "base";
            set
            {
                if (_config?.Ai != null &&
                    _config.Ai.SelectedWhisperModel != value)
                {
                    _config.Ai.SelectedWhisperModel = value;
                    SaveConfig();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedModelInfo));
                    OnPropertyChanged(nameof(IsWhisperModelReady));
                    OnPropertyChanged(nameof(WhisperStatusChipBackgroundKey));
                    OnPropertyChanged(nameof(WhisperStatusChipForegroundKey));
                    OnPropertyChanged(nameof(ModelFileSizeMB));
                    OnPropertyChanged(nameof(ModelFilePath));

                    if (_speechToText is WhisperSpeechService whisper)
                        whisper.RefreshAvailability();
                }
            }
        }

        public WhisperModelInfo SelectedModelInfo =>
            WhisperModelManager.GetModelInfo(SelectedWhisperModel);

        // FIX CS0428: IsModelDownloaded is now a clean bool property
        public bool IsWhisperModelReady => WhisperModelManager.IsModelDownloaded;

        /// <summary>
        /// Brush keys for the Whisper install-state chip in the Voice status card
        /// header — accent-soft while the model is installed, the danger surface
        /// while it is missing. Keys, not brushes: BrushKeyConverter resolves them
        /// against the live theme, so the chip reads as state instead of one fixed
        /// tint, and the view holds no colour.
        /// </summary>
        public string WhisperStatusChipBackgroundKey =>
            IsWhisperModelReady ? "Ios26AccentSoftBrush" : "DangerSurfaceBrush";

        /// <summary>Brush key for the install-state chip's text.</summary>
        public string WhisperStatusChipForegroundKey =>
            IsWhisperModelReady ? "Ios26AccentSoftTextBrush" : "DangerBrush";
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

        public ICommand DownloadWhisperModelCommand { get; private set; }
        public ICommand CancelDownloadCommand { get; private set; }
        public ICommand DeleteWhisperModelCommand { get; private set; }

    }
}
