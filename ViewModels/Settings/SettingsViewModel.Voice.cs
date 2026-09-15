// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.Voice.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.1 — SettingsViewModel partial split. This part owns the Voice
// members; the class declaration, fields and constructor live in
// SettingsViewModel.cs.
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
using System.Windows.Media;
using Microsoft.Win32;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace ProGlassAutomation.ViewModels.Settings
{
    public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
    {
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
