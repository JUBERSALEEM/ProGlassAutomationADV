// ═══════════════════════════════════════════════════════════════════════
// FILE: Diagnostics/AppLogPanelViewModel.cs
// PROJECT: ProGlassAutomation
//
// AppLogPanelViewModel extracted from DiagnosticsViewModel.cs (Phase 11/12
// God-VM split) — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.ViewModels.Shared;
using System.Collections.Generic;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Diagnostics
{
    public class AppLogPanelViewModel : ViewModelBase
    {
        public System.Collections.ObjectModel.ObservableCollection
            <Services.Logging.LogEntry> Entries { get; } = new();

        public List<string> LevelOptions { get; } = new()
        {
            "All levels", "Warnings & errors", "Errors only"
        };

        private string _selectedLevel = "All levels";
        public string SelectedLevel
        {
            get => _selectedLevel;
            set
            {
                _selectedLevel = value;
                OnPropertyChanged();
                Rebuild();
            }
        }

        private List<Services.Logging.LogEntry> _all = new();

        public ICommand OpenLogsFolderCommand { get; }

        public AppLogPanelViewModel()
        {
            OpenLogsFolderCommand = new RelayCommand(_ => OpenFolder());

            Services.Logging.AppLogService.LogWritten += (_, entry) =>
            {
                _all.Add(entry);
                if (PassesFilter(entry))
                    Entries.Add(entry);

                while (Entries.Count > 500)
                    Entries.RemoveAt(0);
            };
        }

        public void Load()
        {
            _all = Services.Logging.AppLogService.GetRecent(500);
            Rebuild();
        }

        private void Rebuild()
        {
            Entries.Clear();
            foreach (var e in _all)
                if (PassesFilter(e))
                    Entries.Add(e);
        }

        private bool PassesFilter(Services.Logging.LogEntry e) =>
            SelectedLevel switch
            {
                "Errors only" => e.Level >= Services.Logging.LogLevel.Error,
                "Warnings & errors" => e.Level >= Services.Logging.LogLevel.Warning,
                _ => true
            };

        private static void OpenFolder()
        {
            try
            {
                var folder = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(
                        System.Environment.SpecialFolder.ApplicationData),
                    "ProGlassAutomation", "logs");

                System.IO.Directory.CreateDirectory(folder);
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = folder,
                        UseShellExecute = true
                    });
            }
            catch { }
        }
    }
}
