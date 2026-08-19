using ProGlassAutomation.Agent.Demo.Data;
using ProGlassAutomation.Agent.Demo.Video;
using ProGlassAutomation.Agent.Tools.Demo;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.Agent.Demo
{
    public sealed class DemoCenterViewModel : ViewModelBase
    {
        private readonly DemoDirector _director;

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
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = FfmpegMp4Composer.ToolsFolder,
                        UseShellExecute = true
                    });
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

        public string WorkflowHint =>
            "Generate Selected records that module. Generate All queues every catalog module, one MP4 each. A job fails if ffmpeg, TTS or frames are missing.";

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
        public ICommand OpenFfmpegPageCommand { get; }
        public ICommand CopyFfmpegLinkCommand { get; }
        public ICommand RecheckFfmpegCommand { get; }
        public ICommand OpenToolsFolderCommand { get; }
        public ICommand EnterDemoCommand { get; }
        public ICommand ExitDemoCommand { get; }
        public ICommand ResetDemoCommand { get; }

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

            FfmpegMp4Composer.InvalidateCache();
            RaiseFlags();

            if (IsFfmpegMissing)
            {
                Alert("ffmpeg is missing",
                    "Generate cannot run — ffmpeg is not installed, so no MP4 can be written.\n\n" +
                    "1. Click DOWNLOAD FFMPEG (WINDOWS ZIP) on this page.\n" +
                    "2. Unzip it and copy ffmpeg.exe and ffprobe.exe into:\n" +
                    FfmpegToolsPath + "\n" +
                    "3. Click RECHECK, then Generate Selected again.\n\n" +
                    "Direct link:\n" + FfmpegDownloadUrl,
                    MessageBoxImage.Warning);
                return;
            }

            SetLocalStatus("Starting " + label + "…");
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

            if (_director.State == DemoJobState.Failed)
            {
                Alert("Demo failed",
                    string.IsNullOrWhiteSpace(_director.Status)
                        ? "The walk did not produce a valid MP4."
                        : _director.Status,
                    MessageBoxImage.Warning);
            }
            else if (_director.State == DemoJobState.Completed)
            {
                Alert("Demo finished",
                    _director.Status +
                    (string.IsNullOrEmpty(_director.LastMp4Path)
                        ? ""
                        : "\n\n" + _director.LastMp4Path),
                    MessageBoxImage.Information);
            }
            else if (_director.State == DemoJobState.Cancelled)
            {
                Alert("Demo cancelled", "Recording was cancelled. No MP4 was written.",
                    MessageBoxImage.Information);
            }
        }

        private void Alert(string title, string message, MessageBoxImage icon)
        {
            SetLocalStatus(message.Replace("\n", " ").Trim());
            RaiseFlags();
            try
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, icon);
            }
            catch { /* status line still updated */ }
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
                Process.Start(new ProcessStartInfo
                {
                    FileName = File.Exists(target) || Directory.Exists(target) ? target : folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                SetLocalStatus("Could not open output: " + ex.Message);
            }
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
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
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
