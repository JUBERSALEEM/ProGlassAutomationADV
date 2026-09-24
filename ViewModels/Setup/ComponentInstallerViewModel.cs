// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Setup/ComponentInstallerViewModel.cs
// Optional component installer. FFmpeg and the offline model are not
// part of the MSI. They download only when the user clicks, through
// the same installers Settings already uses.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Agent.Configuration;
using ProGlassAutomation.Agent.Demo.Video;
using ProGlassAutomation.Agent.LocalAi;
using ProGlassAutomation.Agent.Rules;
using ProGlassAutomation.Commands;
using ProGlassAutomation.ViewModels;

namespace ProGlassAutomation.ViewModels.Setup
{
    public sealed class SetupModelRow
    {
        public SetupModelRow(LocalModelEntry entry, bool recommended)
        {
            Entry = entry;
            IsRecommended = recommended;
        }

        public LocalModelEntry Entry { get; }
        public string Id => Entry.Id;
        public string Title => Entry.Title;
        public string Description => Entry.Description;
        public string LicenseUrl => Entry.LicenseUrl;
        public bool IsRecommended { get; }
        public string Meta =>
            $"{Entry.SizeMb} MB · {Entry.MinRamGb} GB RAM · {Entry.LicenseName}" +
            (IsRecommended ? " · Recommended" : "");
    }

    public sealed class ComponentInstallerViewModel : ViewModelBase
    {
        private readonly CancellationTokenSource _modelCts = new();
        private CancellationTokenSource? _activeModel;

        public ComponentInstallerViewModel()
        {
            var hardware = HardwareProbe.Read();
            var recommended = LocalModelCatalog.RecommendFor(hardware.TotalMemoryGb);
            foreach (var entry in LocalModelCatalog.All)
            {
                Models.Add(new SetupModelRow(entry, entry.Id == recommended.Id));
            }
            SelectedModel = Models.FirstOrDefault(m => m.IsRecommended) ?? Models.FirstOrDefault();

            InstallFfmpegCommand = new RelayCommand(async _ => await InstallFfmpegAsync(), _ => !IsBusy);
            RecheckFfmpegCommand = new RelayCommand(_ => RefreshFfmpeg(), _ => !IsBusy);
            InstallModelCommand = new RelayCommand(async _ => await InstallModelAsync(), _ => !IsBusy && SelectedModel != null);
            CancelModelCommand = new RelayCommand(_ => _activeModel?.Cancel(), _ => IsModelBusy);
            OpenLicenseCommand = new RelayCommand(_ => OpenLicense(), _ => SelectedModel != null);
            ShowFfmpegCommand = new RelayCommand(_ => Page = 0);
            ShowModelCommand = new RelayCommand(_ => Page = 1);

            RefreshFfmpeg();
        }

        public ObservableCollection<SetupModelRow> Models { get; } = new();

        private SetupModelRow? _selectedModel;
        public SetupModelRow? SelectedModel
        {
            get => _selectedModel;
            set { _selectedModel = value; OnPropertyChanged(); }
        }

        private int _page;
        /// <summary>0 = FFmpeg, 1 = model list, 2 = model download.</summary>
        public int Page
        {
            get => _page;
            private set { _page = value; OnPropertyChanged(); }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set { _isBusy = value; OnPropertyChanged(); }
        }

        private bool _isModelBusy;
        public bool IsModelBusy
        {
            get => _isModelBusy;
            private set { _isModelBusy = value; OnPropertyChanged(); }
        }

        private double _percent;
        public double Percent
        {
            get => _percent;
            private set { _percent = value; OnPropertyChanged(); }
        }

        private string _status = "";
        public string Status
        {
            get => _status;
            private set { _status = value; OnPropertyChanged(); }
        }

        private bool _ffmpegReady;
        public bool FfmpegReady
        {
            get => _ffmpegReady;
            private set { _ffmpegReady = value; OnPropertyChanged(); }
        }

        public ICommand InstallFfmpegCommand { get; }
        public ICommand RecheckFfmpegCommand { get; }
        public ICommand InstallModelCommand { get; }
        public ICommand CancelModelCommand { get; }
        public ICommand OpenLicenseCommand { get; }
        public ICommand ShowFfmpegCommand { get; }
        public ICommand ShowModelCommand { get; }

        public void RefreshFfmpeg()
        {
            FfmpegMp4Composer.InvalidateCache();
            FfmpegReady = FfmpegMp4Composer.Find() != null;
            if (!IsBusy)
            {
                Status = FfmpegReady
                    ? "FFmpeg is ready."
                    : "FFmpeg is not installed.";
            }
        }

        private async Task InstallFfmpegAsync()
        {
            IsBusy = true;
            Percent = 0;
            try
            {
                var result = await FfmpegInstaller.DownloadAndInstallAsync(
                    progress: msg => RunOnUi(() => Status = msg),
                    percent: value => RunOnUi(() => Percent = value));
                RefreshFfmpeg();
                Status = result.IsSuccess
                    ? "FFmpeg is ready."
                    : result.Error;
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task InstallModelAsync()
        {
            if (SelectedModel == null) return;
            IsBusy = true;
            IsModelBusy = true;
            Percent = 0;
            Page = 2;
            Status = "Downloading the model.";
            _activeModel?.Dispose();
            _activeModel = CancellationTokenSource.CreateLinkedTokenSource(_modelCts.Token);
            var entry = SelectedModel.Entry;
            try
            {
                bool ok = await LocalModelManager.InstallAsync(
                    entry,
                    new Progress<double>(value => RunOnUi(() => Percent = value)),
                    _activeModel.Token);
                if (ok)
                {
                    AgentConfigurationService.SetLocalModel(entry.Id, true);
                    ProGlassAutomation.Agent.Core.AgentHost.Invalidate();
                    Status = "Installed. Answers from this model stay on this computer.";
                }
                else
                {
                    Status = "The model did not install. Check disk space and the connection, then try again.";
                }
            }
            catch (OperationCanceledException)
            {
                Status = "Download cancelled. No partial file was kept.";
            }
            finally
            {
                IsBusy = false;
                IsModelBusy = false;
                _activeModel?.Dispose();
                _activeModel = null;
                Page = 1;
            }
        }

        private void OpenLicense()
        {
            string? url = SelectedModel?.LicenseUrl;
            if (string.IsNullOrWhiteSpace(url)) return;
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
        }

        private static void RunOnUi(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                action();
            else
                dispatcher.Invoke(action);
        }
    }
}
