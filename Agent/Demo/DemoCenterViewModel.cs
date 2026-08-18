using ProGlassAutomation.Agent.Demo.Data;
using ProGlassAutomation.Agent.Demo.Video;
using ProGlassAutomation.Agent.Tools.Demo;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace ProGlassAutomation.Agent.Demo
{
    public sealed class DemoCenterViewModel : ViewModelBase
    {
        public DemoCenterViewModel()
        {
            var d = CreateDemoPackageTool.SharedDirector;
            d.Changed += () =>
            {
                var disp = System.Windows.Application.Current?.Dispatcher;
                if (disp == null) return;
                disp.InvokeAsync(RaiseStatus);
            };

            GenerateSelectedCommand = new RelayCommand(async _ =>
            {
                if (IsBusy) return;
                await DemoJobQueue.Shared.EnqueueAsync(SelectedModule).ConfigureAwait(true);
                RaiseStatus();
            });

            GenerateAllCommand = new RelayCommand(async _ =>
            {
                if (IsBusy) return;
                await DemoJobQueue.Shared.EnqueueAsync("All").ConfigureAwait(true);
                RaiseStatus();
            });

            RetryCommand = new RelayCommand(async _ =>
            {
                if (IsBusy) return;
                string arg = CreateDemoPackageTool.SharedDirector.LastArgument
                    ?? SelectedModule;
                await DemoJobQueue.Shared.EnqueueAsync(arg).ConfigureAwait(true);
                RaiseStatus();
            });

            CancelCommand = new RelayCommand(_ =>
                CreateDemoPackageTool.SharedDirector.Cancel());

            OpenOutputCommand = new RelayCommand(_ =>
            {
                string? file = CreateDemoPackageTool.SharedDirector.LastMp4Path;
                string folder = CreateDemoPackageTool.SharedDirector.LastOutputFolder
                    ?? Path.Combine(AppDataRoot.DemoFolder, "DemoVideos");
                string target = file != null && File.Exists(file) ? file : folder;
                try
                {
                    if (Directory.Exists(target) || File.Exists(target))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = target,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        Directory.CreateDirectory(folder);
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = folder,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    StatusText = "Could not open output: " + ex.Message;
                    OnPropertyChanged(nameof(StatusText));
                }
            });

            EnterDemoCommand = new RelayCommand(async _ =>
            {
                var r = await DemoModeService.EnterAndSeedAsync(default).ConfigureAwait(true);
                StatusText = r.Message;
                RaiseStatus();
            });

            ExitDemoCommand = new RelayCommand(_ =>
            {
                DemoModeService.Exit();
                StatusText = "Demo Mode off. Next ERP open uses the production folder.";
                RaiseStatus();
            });

            ResetDemoCommand = new RelayCommand(_ =>
            {
                StatusText = DemoModeService.ResetDemoFolder();
                RaiseStatus();
            });
        }

        public string[] ModuleOptions { get; } =
        {
            "ProformaInvoice", "Dashboard", "SheetStore", "JobOrder",
            "DeliveryNote", "TaxInvoice", "SalesReport", "JobProfitability",
            "CompanyCenter"
        };

        private string _selectedModule = "ProformaInvoice";
        public string SelectedModule
        {
            get => _selectedModule;
            set { if (_selectedModule == value) return; _selectedModule = value; OnPropertyChanged(); }
        }

        public string StatusText { get; private set; } =
            "A demo is successful only when a valid MP4 is written. " +
            "HTML storyboards and AVI files are not accepted as done. " +
            "Work happens in %AppData%/ProGlassAutomation.Demo — production files stay closed.";

        public string StateText =>
            CreateDemoPackageTool.SharedDirector.State.ToString();

        public bool IsBusy =>
            CreateDemoPackageTool.SharedDirector.State
                is DemoJobState.Analyzing or DemoJobState.Preparing
                or DemoJobState.Recording or DemoJobState.Narrating
                or DemoJobState.Rendering or DemoJobState.Validating
                or DemoJobState.Queued;

        public string FfmpegStatus
        {
            get
            {
                string hint = CreateDemoPackageTool.SharedDirector.FfmpegHint;
                if (!string.IsNullOrEmpty(hint)) return hint;
                return FfmpegMp4Composer.Find() == null
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
                string? p = CreateDemoPackageTool.SharedDirector.LastMp4Path;
                if (string.IsNullOrEmpty(p))
                    return CreateDemoPackageTool.SharedDirector.State == DemoJobState.Completed
                        ? "MP4 path missing — treat as failed."
                        : "No MP4 yet. Missing MP4 means the job failed.";
                return p;
            }
        }

        public string VerifiedText =>
            "Verified events last run: " +
            CreateDemoPackageTool.SharedDirector.VerifiedEventCount;

        public ObservableCollection<DemoJobRecord> History =>
            CreateDemoPackageTool.SharedDirector.History;

        public ICommand GenerateSelectedCommand { get; }
        public ICommand GenerateAllCommand { get; }
        public ICommand RetryCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand OpenOutputCommand { get; }
        public ICommand EnterDemoCommand { get; }
        public ICommand ExitDemoCommand { get; }
        public ICommand ResetDemoCommand { get; }

        private void RaiseStatus()
        {
            StatusText = CreateDemoPackageTool.SharedDirector.Status;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(FfmpegStatus));
            OnPropertyChanged(nameof(IsolationText));
            OnPropertyChanged(nameof(LastMp4Text));
            OnPropertyChanged(nameof(VerifiedText));
        }
    }
}
