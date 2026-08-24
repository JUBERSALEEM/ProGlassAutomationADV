using ProGlassAutomation.Agent.Demo.Recording;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Recording;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Shared
{
    /// <summary>
    /// Top-bar screen-record popup. Shares the same engine as Demo
    /// Center's "Record this session" card.
    /// </summary>
    public sealed class ScreenRecordPanelViewModel : ViewModelBase
    {
        public static ScreenRecordPanelViewModel Shared { get; } = new();

        private readonly UserScreenRecordService _svc = UserScreenRecordService.Shared;

        public ScreenRecordPanelViewModel()
        {
            SelectedQuality = ScreenRecordSettings.Qualities
                .FirstOrDefault(q => q.Quality == _svc.Settings.Quality)
                ?? ScreenRecordSettings.Qualities[1];
            SelectedFps = ScreenRecordSettings.FpsOptions
                .FirstOrDefault(f => f.Fps == _svc.Settings.Fps)
                ?? ScreenRecordSettings.FpsOptions[0];
            _captureLiveAudio = _svc.Settings.CaptureLiveAudio;
            _captureMicrophone = _svc.Settings.CaptureMicrophone;

            StartCommand = new RelayCommand(
                _ => Start(),
                _ => !_svc.IsBusy);
            StopCommand = new RelayCommand(
                async _ => await _svc.StopAndSaveAsync().ConfigureAwait(true),
                _ => _svc.IsRecording);
            PauseCommand = new RelayCommand(
                _ => _svc.TogglePause(),
                _ => _svc.IsRecording);
            OpenOutputCommand          = new RelayCommand(_ => _svc.OpenOutput());
            OpenRecordingsFolderCommand = new RelayCommand(_ => _svc.OpenRecordingsFolder());

            _svc.Changed += RaiseStatus;
        }

        public IReadOnlyList<ScreenRecordQualityOption> Qualities =>
            ScreenRecordSettings.Qualities;

        public IReadOnlyList<ScreenRecordFpsOption> FpsChoices =>
            ScreenRecordSettings.FpsOptions;

        private ScreenRecordQualityOption _selectedQuality = ScreenRecordSettings.Qualities[1];
        public ScreenRecordQualityOption SelectedQuality
        {
            get => _selectedQuality;
            set
            {
                if (value == null || ReferenceEquals(_selectedQuality, value)) return;
                _selectedQuality = value;
                PushSettings();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Hint));
            }
        }

        private ScreenRecordFpsOption _selectedFps = ScreenRecordSettings.FpsOptions[0];
        public ScreenRecordFpsOption SelectedFps
        {
            get => _selectedFps;
            set
            {
                if (value == null || ReferenceEquals(_selectedFps, value)) return;
                _selectedFps = value;
                PushSettings();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Hint));
            }
        }

        private bool _captureLiveAudio = true;
        public bool CaptureLiveAudio
        {
            get => _captureLiveAudio;
            set
            {
                if (_captureLiveAudio == value) return;
                _captureLiveAudio = value;
                PushSettings();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Hint));
            }
        }

        private bool _captureMicrophone = true;
        public bool CaptureMicrophone
        {
            get => _captureMicrophone;
            set
            {
                if (_captureMicrophone == value) return;
                _captureMicrophone = value;
                PushSettings();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Hint));
            }
        }

        public bool IsRecording => _svc.IsRecording;
        public bool IsPaused => _svc.IsPaused;
        public bool IsEncoding => _svc.IsEncoding;
        public bool IsBusy => _svc.IsBusy;
        public bool IsIdle => !_svc.IsBusy;
        public string Status => _svc.Status;
        public string LastMp4Text =>
            string.IsNullOrEmpty(_svc.LastMp4Path)
                ? "No recording yet."
                : _svc.LastMp4Path!;

        public string Hint =>
            "Records only this app window at " +
            (SelectedQuality?.Title ?? "1080p") + " / " +
            (SelectedFps?.Title ?? "30 fps") +
            ". A REC bar with duration and STOP appears on the window." +
            (CaptureLiveAudio ? " Internal audio on." : "") +
            (CaptureMicrophone ? " Microphone on." : "");

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand OpenOutputCommand { get; }

        /// <summary>
        /// Opens %AppData%\ProGlassAutomation\Recordings in Explorer
        /// regardless of whether a recording has finished yet. Bound
        /// to the "Open recordings folder" button in the top-bar
        /// recorder popup (Aug 2026 senior audit — the user asked
        /// for a visible "where is my file" affordance).
        /// </summary>
        public ICommand OpenRecordingsFolderCommand { get; }

        private void PushSettings()
        {
            _svc.Settings = new ScreenRecordSettings
            {
                Quality = SelectedQuality?.Quality ?? ScreenRecordQuality.FullHd,
                Fps = SelectedFps?.Fps ?? 30,
                CaptureLiveAudio = CaptureLiveAudio,
                CaptureMicrophone = CaptureMicrophone
            }.Clamp();
        }

        private void Start()
        {
            PushSettings();
            string? why = _svc.TryStart(_svc.Settings);
            if (why != null)
            {
                try
                {
                    MessageBox.Show(why, "Screen recording",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch { /* status still updated */ }
            }
            RaiseStatus();
        }

        private void RaiseStatus()
        {
            OnPropertyChanged(nameof(IsRecording));
            OnPropertyChanged(nameof(IsEncoding));
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(LastMp4Text));
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
