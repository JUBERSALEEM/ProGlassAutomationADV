using ProGlassAutomation.Agent.Demo.Data;
using ProGlassAutomation.Agent.Demo.Recording;
using ProGlassAutomation.Agent.Demo.Video;
using ProGlassAutomation.Agent.Tools.Demo;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Recording;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Dialogs;

namespace ProGlassAutomation.Agent.Demo
{
    public sealed class DemoCenterViewModel : ViewModelBase
    {
        private readonly DemoDirector _director;

        // Guards re-entry while the one-click ffmpeg install is running;
        // also drives InstallFfmpegCommand's CanExecute.
        private bool _installingFfmpeg;

        private double _ffmpegDownloadPercent;

        /// <summary>0–100 download progress shown on the FFMPEG card.</summary>
        public double FfmpegDownloadPercent
        {
            get => _ffmpegDownloadPercent;
            private set
            {
                _ffmpegDownloadPercent = value;
                OnPropertyChanged();
            }
        }

        /// <summary>True while the one-click installer is running.</summary>
        public bool IsInstallingFfmpeg
        {
            get => _installingFfmpeg;
            private set
            {
                _installingFfmpeg = value;
                OnPropertyChanged();
            }
        }

        public DemoCenterViewModel()
        {
            _director = CreateDemoPackageTool.SharedDirector;
            _director.Changed += () =>
            {
                var disp = Application.Current?.Dispatcher;
                if (disp == null)
                {
                    RaiseStatus();
                    return;
                }
                disp.InvokeAsync(RaiseStatus);
            };

            SelectedWorkflow = DemoWorkflowCatalog.Find("ProformaInvoice")
                ?? DemoWorkflowCatalog.All[0];
            // 2026-08-25: hydrate from the persisted DemoRecording slot.
            // Previously these were hard-coded defaults on every VM
            // construction, and the Settings page rebuilds the VM — so
            // the user's quality/fps/audio choices kept resetting.
            var demoPrefs = App.Config?.DemoRecording;
            // Fallbacks track the shipped default (4K/60 — see
            // ScreenRecordSettings): explicit stored choices always win,
            // these only apply when the config has no recorded choice.
            _selectedQuality = ScreenRecordSettings.Qualities.FirstOrDefault(q =>
                demoPrefs != null && (int)q.Quality == demoPrefs.QualityHeight)
                ?? ScreenRecordSettings.Qualities[^1];
            _selectedFps = ScreenRecordSettings.FpsOptions.FirstOrDefault(f =>
                demoPrefs != null && f.Fps == demoPrefs.Fps)
                ?? ScreenRecordSettings.FpsOptions[^1];
            _captureLiveAudio = demoPrefs?.CaptureLiveAudio ?? true;
            _captureMicrophone = demoPrefs?.CaptureMicrophone ?? true;

            GenerateSelectedCommand = new RelayCommand(
                async _ => await StartAsync(SelectedModule).ConfigureAwait(true),
                _ => !IsBusy);

            GenerateAllCommand = new RelayCommand(
                async _ => await StartAsync("All").ConfigureAwait(true),
                _ => !IsBusy);

            RetryCommand = new RelayCommand(
                async _ => await StartAsync(_director.LastArgument ?? SelectedModule)
                    .ConfigureAwait(true),
                _ => !IsBusy);

            CancelCommand = new RelayCommand(_ =>
            {
                _director.Cancel();
                SetLocalStatus("Cancel requested.");
            });

            OpenOutputCommand = new RelayCommand(_ => OpenOutput());

            OpenFfmpegDownloadCommand = new RelayCommand(_ =>
                OpenUrl(FfmpegDownloadUrl));

            // 2026-08-25: one-click installer — the old button only opened
            // the zip URL in the system browser, which failed on locked-down
            // machines and still required a manual unzip + copy. This one
            // downloads, verifies, extracts and installs by itself.
            InstallFfmpegCommand = new RelayCommand(
                async _ => await InstallFfmpegAsync().ConfigureAwait(true),
                _ => !IsInstallingFfmpeg);

            OpenFfmpegPageCommand = new RelayCommand(_ =>
                OpenUrl(FfmpegOfficialUrl));

            CopyFfmpegLinkCommand = new RelayCommand(_ =>
            {
                try
                {
                    Clipboard.SetText(FfmpegDownloadUrl);
                    SetLocalStatus("Copied the Windows ffmpeg zip link.");
                }
                catch (Exception ex)
                {
                    SetLocalStatus("Could not copy the link: " + ex.Message);
                }
            });

            RecheckFfmpegCommand = new RelayCommand(_ =>
            {
                FfmpegMp4Composer.InvalidateCache();
                RaiseFlags();
                SetLocalStatus(IsFfmpegMissing
                    ? "ffmpeg is still missing. Download the zip, extract ffmpeg.exe and ffprobe.exe into the tools folder, then Recheck."
                    : "ffmpeg found. Generate can encode a real MP4.");
            });

            OpenToolsFolderCommand = new RelayCommand(_ =>
            {
                try
                {
                    Directory.CreateDirectory(FfmpegMp4Composer.ToolsFolder);
                                    ProcessStartGuard.TryOpenFolder(FfmpegMp4Composer.ToolsFolder, CorrelationContext.NewId());
                }
                catch (Exception ex)
                {
                    SetLocalStatus("Could not open the tools folder: " + ex.Message);
                }
            });

            EnterDemoCommand = new RelayCommand(async _ =>
            {
                SetLocalStatus("Entering isolated Demo Mode…");
                var r = await DemoModeService.EnterAndSeedAsync(default).ConfigureAwait(true);
                SetLocalStatus(r.Message);
                RaiseFlags();
            });

            ExitDemoCommand = new RelayCommand(_ =>
            {
                DemoModeService.Exit();
                SetLocalStatus("Demo Mode off. Next ERP open uses the production folder.");
                RaiseFlags();
            });

            ResetDemoCommand = new RelayCommand(_ =>
            {
                SetLocalStatus(DemoModeService.ResetDemoFolder());
                RaiseFlags();
            });

            StartSessionRecordCommand = new RelayCommand(
                _ => StartSessionRecord(),
                _ => !IsBusy && !UserScreenRecordService.Shared.IsBusy);

            StopSessionRecordCommand = new RelayCommand(
                async _ => await UserScreenRecordService.Shared.StopAndSaveAsync()
                    .ConfigureAwait(true),
                _ => UserScreenRecordService.Shared.IsRecording);

            OpenRecordingsCommand = new RelayCommand(
                _ => UserScreenRecordService.Shared.OpenOutput());

            UserScreenRecordService.Shared.Changed += () =>
            {
                var disp = Application.Current?.Dispatcher;
                if (disp == null) { RaiseFlags(); return; }
                disp.InvokeAsync(RaiseFlags);
            };
        }

        public IReadOnlyList<DemoWorkflow> Modules => DemoWorkflowCatalog.All;

        private DemoWorkflow _selectedWorkflow = null!;
        public DemoWorkflow SelectedWorkflow
        {
            get => _selectedWorkflow;
            set
            {
                if (value == null || ReferenceEquals(_selectedWorkflow, value)) return;
                _selectedWorkflow = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedModule));
                OnPropertyChanged(nameof(SelectedPurpose));
            }
        }

        public string SelectedModule => SelectedWorkflow?.Id ?? "ProformaInvoice";

        public string SelectedPurpose =>
            SelectedWorkflow == null
                ? ""
                : SelectedWorkflow.Title + " — " + SelectedWorkflow.Purpose;

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
                OnPropertyChanged();
                OnPropertyChanged(nameof(RecordHint));
                PersistRecordingChoices();
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
                OnPropertyChanged();
                OnPropertyChanged(nameof(RecordHint));
                PersistRecordingChoices();
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
                OnPropertyChanged();
                OnPropertyChanged(nameof(RecordHint));
                PersistRecordingChoices();
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
                OnPropertyChanged();
                OnPropertyChanged(nameof(RecordHint));
                PersistRecordingChoices();
            }
        }

        /// <summary>
        /// Writes the current demo recording choices into
        /// AppConfig.DemoRecording so they survive Settings-page rebuilds
        /// and app restarts. Best-effort — a failed save never gates the
        /// in-memory choice for this session.
        /// </summary>
        private void PersistRecordingChoices()
        {
            try
            {
                if (App.Config == null) return;
                App.Config.DemoRecording ??=
                    new ProGlassAutomation.Services.AI.AIConfig.RecordingPreferences();
                App.Config.DemoRecording.QualityHeight =
                    (int)(SelectedQuality?.Quality ?? ScreenRecordQuality.FullHd);
                App.Config.DemoRecording.Fps = SelectedFps?.Fps ?? 30;
                App.Config.DemoRecording.CaptureLiveAudio = _captureLiveAudio;
                App.Config.DemoRecording.CaptureMicrophone = _captureMicrophone;
                ProGlassAutomation.Services.AI.AIConfig.ConfigService.Save(App.Config);
            }
            catch (Exception ex)
            {
                ProGlassAutomation.Services.Logging.AppLogService.Warning(
                    "Could not persist demo recording settings: " + ex.Message);
            }
        }

        public string RecordHint =>
            "Records only this app window — not the desktop — at " +
            (SelectedQuality?.Title ?? "1080p") + " / " +
            (SelectedFps?.Title ?? "30 fps") +
            ". A REC bar with duration and STOP appears on the live window." +
            (CaptureLiveAudio ? " Internal app audio is captured." : "") +
            (CaptureMicrophone ? " Microphone is captured." : "") +
            (!CaptureLiveAudio && !CaptureMicrophone
                ? " Live audio is off; generated TTS is muxed after the walk."
                : "");

        public string WorkflowHint =>
            "Generate Selected opens the live module, waits for the page to paint, then narrates in sync with the screen. Generate All queues every catalog module, one MP4 each. A job fails if ffmpeg or frames are missing.";

        public string StatusText { get; private set; } =
            "A demo is successful only when a valid MP4 is written. " +
            "HTML storyboards and AVI files are not accepted as done. " +
            "Work happens in %AppData%/ProGlassAutomation.Demo — production files stay closed.";

        public string StateText => _director.State.ToString();

        public bool IsBusy => _director.IsRunning;

        public bool IsFfmpegMissing => FfmpegMp4Composer.Find() == null;

        public bool IsFfmpegReady => !IsFfmpegMissing;

        public string FfmpegDownloadUrl => FfmpegMp4Composer.WindowsEssentialsZipUrl;

        public string FfmpegOfficialUrl => FfmpegMp4Composer.OfficialDownloadPageUrl;

        public string FfmpegToolsPath => FfmpegMp4Composer.ToolsFolder;

        public string FfmpegInstallHint =>
            "Download the Windows essentials zip, unzip it, and copy ffmpeg.exe and ffprobe.exe into:\n" +
            FfmpegToolsPath +
            "\nOr put ffmpeg on PATH. Recheck after you drop the files.";

        public string FfmpegStatus
        {
            get
            {
                string hint = _director.FfmpegHint;
                if (!string.IsNullOrEmpty(hint)) return hint;
                return IsFfmpegMissing
                    ? "ffmpeg not found — generate will fail. AVI is not an MP4."
                    : "ffmpeg found — MP4 encode will be attempted.";
            }
        }

        public string IsolationText =>
            AppDataRoot.IsDemo
                ? "Demo Mode ON — ERP files: " + AppDataRoot.DemoFolder
                : "Demo Mode OFF. Generate will switch to " + AppDataRoot.DemoFolder;

        public string LastMp4Text
        {
            get
            {
                string? p = _director.LastMp4Path;
                if (string.IsNullOrEmpty(p))
                    return _director.State == DemoJobState.Completed
                        ? "MP4 path missing — treat as failed."
                        : "No MP4 yet. Missing MP4 means the job failed.";
                return p;
            }
        }

        public string VerifiedText =>
            "Verified events last run: " + _director.VerifiedEventCount;

        public bool HasHistory => _director.History.Count > 0;

        public ObservableCollection<DemoJobRecord> History => _director.History;

        public ICommand GenerateSelectedCommand { get; }
        public ICommand GenerateAllCommand { get; }
        public ICommand RetryCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand OpenOutputCommand { get; }
        public ICommand OpenFfmpegDownloadCommand { get; }
        public ICommand InstallFfmpegCommand { get; }
        public ICommand OpenFfmpegPageCommand { get; }
        public ICommand CopyFfmpegLinkCommand { get; }
        public ICommand RecheckFfmpegCommand { get; }
        public ICommand OpenToolsFolderCommand { get; }
        public ICommand EnterDemoCommand { get; }
        public ICommand ExitDemoCommand { get; }
        public ICommand ResetDemoCommand { get; }
        public ICommand StartSessionRecordCommand { get; }
        public ICommand StopSessionRecordCommand { get; }
        public ICommand OpenRecordingsCommand { get; }

        public bool IsSessionRecording => UserScreenRecordService.Shared.IsRecording;
        public bool IsSessionIdle => !UserScreenRecordService.Shared.IsBusy;

        public string SessionRecordStatus =>
            UserScreenRecordService.Shared.Status;

        public string SessionRecordHint =>
            "Independent of Generate. Records whatever you do in this window. " +
            "Does not enter Demo Mode and does not walk a wizard. " +
            "Same engine as the REC button in the top bar.";

        private void StartSessionRecord()
        {
            if (IsBusy)
            {
                Alert("Demo already running",
                    "A demo walk is recording. Cancel it first.",
                    MessageBoxImage.Information);
                return;
            }

            var settings = new ScreenRecordSettings
            {
                Quality = SelectedQuality?.Quality ?? ScreenRecordQuality.FullHd,
                Fps = SelectedFps?.Fps ?? 30,
                CaptureLiveAudio = CaptureLiveAudio,
                CaptureMicrophone = CaptureMicrophone
            }.Clamp();

            string? why = UserScreenRecordService.Shared.TryStart(settings);
            if (why != null)
            {
                Alert("Screen recording", why, MessageBoxImage.Warning);
                return;
            }
            SetLocalStatus(UserScreenRecordService.Shared.Status);
            RaiseFlags();
        }

        private async System.Threading.Tasks.Task StartAsync(string argument)
        {
            string label = string.IsNullOrWhiteSpace(argument)
                ? "Proforma Invoice"
                : argument;

            if (_director.IsRunning)
            {
                Alert("Demo already running",
                    "A demo is already recording. Press Cancel first, then Generate again.",
                    MessageBoxImage.Information);
                return;
            }

            if (UserScreenRecordService.Shared.IsBusy)
            {
                Alert("Screen recording is running",
                    "Stop the manual screen recording first. Generate and REC cannot share this window.",
                    MessageBoxImage.Information);
                return;
            }

            FfmpegMp4Composer.InvalidateCache();
            RaiseFlags();

            if (IsFfmpegMissing)
            {
                Alert("ffmpeg is missing",
                    "Generate cannot run — ffmpeg is not installed, so no MP4 can be written.\n\n" +
                    "1. Click DOWNLOAD & INSTALL (AUTOMATIC) on this page and watch the progress bar.\n" +
                    "2. When it finishes, click RECHECK.\n" +
                    "3. Press Generate again.\n\n" +
                    "Direct link (manual fallback):\n" + FfmpegDownloadUrl,
                    MessageBoxImage.Warning);
                return;
            }

            _director.RecordSettings = new ScreenRecordSettings
            {
                Quality = SelectedQuality?.Quality ?? ScreenRecordQuality.FullHd,
                Fps = SelectedFps?.Fps ?? 30,
                CaptureLiveAudio = CaptureLiveAudio,
                CaptureMicrophone = CaptureMicrophone
            }.Clamp();

            SetLocalStatus("Starting " + label + " at " + _director.RecordSettings.Summary + "…");
            RaiseFlags();
            try
            {
                await DemoJobQueue.Shared.EnqueueAsync(argument).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Alert("Generate failed", ex.Message, MessageBoxImage.Error);
                return;
            }

            RaiseStatus();
        }

        private void Alert(string title, string message, MessageBoxImage icon)
        {
            SetLocalStatus(message.Replace("\n", " ").Trim());
            RaiseFlags();
            try
            {
                Prompts.Show(message, title, MessageBoxButton.OK, icon);
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DemoCenterViewModel] Alert: status line still updated: " + ex.Message); }
        }

        private void OpenOutput()
        {
            string? file = _director.LastMp4Path;
            string folder = _director.LastOutputFolder
                ?? Path.Combine(AppDataRoot.DemoFolder, "DemoVideos");
            string target = file != null && File.Exists(file) ? file : folder;
            try
            {
                Directory.CreateDirectory(
                    Directory.Exists(target) ? target : (Path.GetDirectoryName(target) ?? folder));
                                ProcessStartGuard.TryOpenFolder(File.Exists(target) || Directory.Exists(target) ? target : folder, CorrelationContext.NewId());
            }
            catch (Exception ex)
            {
                SetLocalStatus("Could not open output: " + ex.Message);
            }
        }

        // 2026-08-25 FIX: the DOWNLOAD FFMPEG button opens the direct
        // Windows essentials zip, which lives on gyan.dev — the build
        // source that ffmpeg.org/download.html itself lists. The old
        // allow-list (substring "ffmpeg.org" or "github.com") did not
        // include it, so the direct-download button was silently
        // blocked with "Blocked untrusted URL" and users could not get
        // ffmpeg for demo recording from Settings.
        //
        // Exact-host matching also closes the old substring hole where
        // a URL such as "https://evil.example/ffmpeg.org" would have
        // passed the Phase-8 check.
        private static readonly System.Collections.Generic.HashSet<string> AllowedDownloadHosts =
            new(System.StringComparer.OrdinalIgnoreCase)
            {
                "ffmpeg.org", "www.ffmpeg.org",   // official project page
                "gyan.dev", "www.gyan.dev",       // ffmpeg.org-listed Windows builds (direct zip)
                "github.com", "www.github.com",   // BtbN / GyanD release pages
            };

        // 2026-08-25: one-click ffmpeg install — download the essentials
        // zip, verify it, extract it and place ffmpeg.exe + ffprobe.exe
        // into the tools folder. The user clicks once, then RECHECK (or
        // waits for the automatic flag refresh below) and is ready.
        private async Task InstallFfmpegAsync()
        {
            if (_installingFfmpeg) return;
            IsInstallingFfmpeg = true;
            FfmpegDownloadPercent = 0;
            ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();

            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                void OnUi(Action action)
                {
                    // FfmpegInstaller reports from background threads.
                    if (dispatcher != null && !dispatcher.CheckAccess())
                        dispatcher.BeginInvoke(action);
                    else
                        action();
                }

                Action<string> progress = message =>
                    OnUi(() => SetLocalStatus(message));

                Action<double> percent = value =>
                    OnUi(() => FfmpegDownloadPercent = Math.Clamp(value, 0, 100));

                var result = await FfmpegInstaller.DownloadAndInstallAsync(progress, percent)
                    .ConfigureAwait(true);

                if (result.IsSuccess)
                {
                    SetLocalStatus(
                        "ffmpeg installed into " + result.Value +
                        " — MP4 generation is ready.");
                }
                else
                {
                    SetLocalStatus(
                        "ffmpeg install failed: " + result.Error +
                        " You can still use COPY LINK / OFFICIAL PAGE for a manual install.");
                }
            }
            catch (Exception ex)
            {
                SetLocalStatus("ffmpeg install failed: " + ex.Message);
            }
            finally
            {
                IsInstallingFfmpeg = false;
                // Whatever happened, re-probe so the FFMPEG card flips to
                // the ready state automatically when the files landed.
                FfmpegMp4Composer.InvalidateCache();
                RaiseFlags();
                ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
            }
        }

        private void OpenUrl(string url)
        {
            try
            {
                // Phase 8: Validate URL before opening — HTTPS + exact-host allow-list
                bool allowed =
                    System.Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                    uri.Scheme == System.Uri.UriSchemeHttps &&
                    AllowedDownloadHosts.Contains(uri.Host);

                if (allowed)
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                else
                    SetLocalStatus($"Blocked untrusted URL: {url}");
            }
            catch (Exception ex)
            {
                SetLocalStatus("Could not open the download page: " + ex.Message);
            }
        }

        private void SetLocalStatus(string text)
        {
            StatusText = text ?? "";
            OnPropertyChanged(nameof(StatusText));
        }

        private void RaiseStatus()
        {
            if (!string.IsNullOrWhiteSpace(_director.Status))
                StatusText = _director.Status;
            OnPropertyChanged(nameof(StatusText));
            RaiseFlags();
        }

        private void RaiseFlags()
        {
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(FfmpegStatus));
            OnPropertyChanged(nameof(IsFfmpegMissing));
            OnPropertyChanged(nameof(IsFfmpegReady));
            OnPropertyChanged(nameof(IsolationText));
            OnPropertyChanged(nameof(LastMp4Text));
            OnPropertyChanged(nameof(VerifiedText));
            OnPropertyChanged(nameof(HasHistory));
            OnPropertyChanged(nameof(IsSessionRecording));
            OnPropertyChanged(nameof(IsSessionIdle));
            OnPropertyChanged(nameof(SessionRecordStatus));
            ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
        }
    }
}
