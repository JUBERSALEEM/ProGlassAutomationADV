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
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Diagnostics
{
    /// <summary>
    /// Technical log panel.
    ///
    /// AUDIT M-1 — WHY THIS IS IDisposable
    /// ───────────────────────────────────
    /// This VM subscribes to the STATIC event AppLogService.LogWritten.
    /// A static event holds a strong reference to every subscriber for the
    /// lifetime of the process, so without an unsubscribe this instance —
    /// and the up-to-500 LogEntry objects it retains in Entries and _all —
    /// can never be collected. DiagnosticsViewModel builds a fresh panel
    /// each time it is constructed, so every visit leaked another copy,
    /// and each surviving copy kept appending to its own list on every log
    /// line written anywhere in the app: the leak grows on its own after
    /// the view is gone.
    /// </summary>
    public class AppLogPanelViewModel : ViewModelBase, IDisposable
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

            // Named handler, not a lambda: an anonymous delegate cannot be
            // detached later, which is what made this a permanent leak.
            Services.Logging.AppLogService.LogWritten += OnLogWritten;
        }

        private void OnLogWritten(object? sender, Services.Logging.LogEntry entry)
        {
            _all.Add(entry);

            // _all was unbounded. GetRecent() only ever seeds 500 entries,
            // but this list then grew for the whole session, so a long run
            // with a chatty log held every entry in memory and made
            // Rebuild() progressively slower on each filter change.
            while (_all.Count > MaxRetained)
                _all.RemoveAt(0);

            if (PassesFilter(entry))
                Entries.Add(entry);

            while (Entries.Count > MaxRetained)
                Entries.RemoveAt(0);
        }

        /// <summary>Upper bound on entries held in memory by this panel.</summary>
        private const int MaxRetained = 500;

        private bool _disposed;

        /// <summary>
        /// Detaches from the static log event so this panel can be
        /// collected. Safe to call more than once.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Services.Logging.AppLogService.LogWritten -= OnLogWritten;

            _all.Clear();
            Entries.Clear();

            GC.SuppressFinalize(this);
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
