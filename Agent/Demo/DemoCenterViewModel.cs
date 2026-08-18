using ProGlassAutomation.Agent.Tools.Demo;
using ProGlassAutomation.Commands;
using ProGlassAutomation.ViewModels;
using System;
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
                await CreateDemoPackageTool.SharedDirector
                    .RunAsync(SelectedModule)
                    .ConfigureAwait(true);
                RaiseStatus();
            });

            GenerateAllCommand = new RelayCommand(async _ =>
            {
                if (IsBusy) return;
                await CreateDemoPackageTool.SharedDirector
                    .RunAsync("All")
                    .ConfigureAwait(true);
                RaiseStatus();
            });

            CancelCommand = new RelayCommand(_ =>
                CreateDemoPackageTool.SharedDirector.Cancel());

            OpenOutputCommand = new RelayCommand(_ =>
            {
                string folder = CreateDemoPackageTool.SharedDirector.LastOutputFolder
                    ?? DemoPackageComposer.RootFolder;
                try
                {
                    Directory.CreateDirectory(folder);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folder,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    StatusText = "Could not open folder: " + ex.Message;
                    OnPropertyChanged(nameof(StatusText));
                }
            });
        }

        public string[] ModuleOptions { get; } =
        {
            "Dashboard", "SheetStore", "ProformaInvoice", "JobOrder",
            "DeliveryNote", "TaxInvoice", "SalesReport", "JobProfitability",
            "CompanyCenter"
        };

        private string _selectedModule = "SheetStore";
        public string SelectedModule
        {
            get => _selectedModule;
            set { if (_selectedModule == value) return; _selectedModule = value; OnPropertyChanged(); }
        }

        public string StatusText { get; private set; } =
            "Demo Mode writes a local storyboard. It never deletes ERP data. " +
            "Sample seeding runs only on an empty database. " +
            "MP4 is created only if ffmpeg is installed.";

        public string StateText =>
            CreateDemoPackageTool.SharedDirector.State.ToString();

        public bool IsBusy =>
            CreateDemoPackageTool.SharedDirector.State
                is DemoJobState.Analyzing or DemoJobState.Preparing
                or DemoJobState.Recording or DemoJobState.Narrating
                or DemoJobState.Rendering or DemoJobState.Validating;

        public string FfmpegStatus =>
            DemoPackageComposer.FindFfmpeg() == null
                ? "ffmpeg not found — HTML storyboard only."
                : "ffmpeg found — MP4 will be attempted.";

        public ICommand GenerateSelectedCommand { get; }
        public ICommand GenerateAllCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand OpenOutputCommand { get; }

        private void RaiseStatus()
        {
            StatusText = CreateDemoPackageTool.SharedDirector.Status;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(IsBusy));
        }
    }
}
