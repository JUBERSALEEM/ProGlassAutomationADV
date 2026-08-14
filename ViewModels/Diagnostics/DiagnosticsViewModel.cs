// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Diagnostics/DiagnosticsViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Logs & Activity (Diagnostics)
//
// One stylish page, many tabs:
//   • App Log       — live technical log (errors, warnings, crashes)
//   • All Activity  — audit trail across every section
//   • One tab per module (PI / JO / DN / TI / Sheet Store / Statement
//     / AI / Users) showing that section's live activity.
//
// Every tab reuses existing VMs (AppLogPanelViewModel +
// SectionActivityViewModel) — no duplicated feed logic.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.ViewModels.Shared;
using System.Collections.Generic;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Diagnostics
{
    /// <summary>Live technical log tab content.</summary>
    public class AppLogPanelViewModel : ViewModelBase
    {
        public global::System.Collections.ObjectModel.ObservableCollection
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
                var folder = global::System.IO.Path.Combine(
                    global::System.Environment.GetFolderPath(
                        global::System.Environment.SpecialFolder.ApplicationData),
                    "ProGlassAutomation", "logs");

                global::System.IO.Directory.CreateDirectory(folder);
                global::System.Diagnostics.Process.Start(
                    new global::System.Diagnostics.ProcessStartInfo
                    {
                        FileName = folder,
                        UseShellExecute = true
                    });
            }
            catch { }
        }
    }

    /// <summary>One tab in the Diagnostics page.</summary>
    public class DiagnosticsTab : ViewModelBase
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Icon { get; set; } = "";
        public ViewModelBase Content { get; set; } = null!;

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }
    }

    /// <summary>Shell VM for the Logs & Activity page.</summary>
    public class DiagnosticsViewModel : ViewModelBase
    {
        public List<DiagnosticsTab> Tabs { get; } = new();

        private DiagnosticsTab _activeTab = null!;
        public DiagnosticsTab ActiveTab
        {
            get => _activeTab;
            private set
            {
                _activeTab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActiveContent));
            }
        }

        public ViewModelBase ActiveContent => ActiveTab?.Content!;

        public ICommand SwitchTabCommand { get; }

        // ── v2 §11: header statistics (shared StatCardControl) ─────────

        private string _totalEntries = "0";
        public string TotalEntries
        {
            get => _totalEntries;
            private set { _totalEntries = value; OnPropertyChanged(); }
        }

        private string _errorCount = "0";
        public string ErrorCount
        {
            get => _errorCount;
            private set { _errorCount = value; OnPropertyChanged(); }
        }

        private string _warningCount = "0";
        public string WarningCount
        {
            get => _warningCount;
            private set { _warningCount = value; OnPropertyChanged(); }
        }

        private string _activeUser = "";
        public string ActiveUser
        {
            get => _activeUser;
            private set { _activeUser = value; OnPropertyChanged(); }
        }

        private void RefreshStats()
        {
            var entries = Services.Logging.AppLogService.GetRecent(500);

            int errors = 0, warnings = 0;
            foreach (var e in entries)
            {
                if (e.Level == Services.Logging.LogLevel.Error ||
                    e.Level == Services.Logging.LogLevel.Critical)
                    errors++;
                else if (e.Level == Services.Logging.LogLevel.Warning)
                    warnings++;
            }

            TotalEntries = entries.Count.ToString("N0");
            ErrorCount = errors.ToString("N0");
            WarningCount = warnings.ToString("N0");
            ActiveUser = AuditService.CurrentUser?.DisplayName
                ?? AuditService.CurrentUsername;
        }

        public DiagnosticsViewModel()
        {
            Tabs.Add(MakeTab("applog", "App Log", "🛠",
                new AppLogPanelViewModel()));

            Tabs.Add(MakeTab("all", "All Activity", "📜",
                new SectionActivityViewModel(null)));

            Tabs.Add(MakeTab("pi", "Proforma Invoice", "📝",
                new SectionActivityViewModel(AuditSections.Proforma)));

            Tabs.Add(MakeTab("jo", "Job Order", "📋",
                new SectionActivityViewModel(AuditSections.JobOrder)));

            Tabs.Add(MakeTab("dn", "Delivery Note", "🚚",
                new SectionActivityViewModel(AuditSections.DeliveryNote)));

            Tabs.Add(MakeTab("ti", "Tax Invoice", "🧾",
                new SectionActivityViewModel(AuditSections.TaxInvoice)));

            Tabs.Add(MakeTab("ss", "Sheet Store", "📦",
                new SectionActivityViewModel(AuditSections.SheetStore)));

            Tabs.Add(MakeTab("st", "Statement", "📊",
                new SectionActivityViewModel(AuditSections.Statement)));

            Tabs.Add(MakeTab("ai", "AI Assistant", "🤖",
                new SectionActivityViewModel(AuditSections.Ai)));

            Tabs.Add(MakeTab("users", "Users & Security", "👥",
                new SectionActivityViewModel(AuditSections.Users)));

            _activeTab = Tabs[0];
            _activeTab.IsActive = true;

            SwitchTabCommand = new RelayCommand(param =>
            {
                if (param is DiagnosticsTab tab)
                    Activate(tab);
            });
        }

        /// <summary>Called whenever the module becomes visible.</summary>
        public void OnShown()
        {
            RefreshStats();
            Activate(ActiveTab);
        }

        private void Activate(DiagnosticsTab tab)
        {
            if (tab == null) return;

            foreach (var t in Tabs)
                t.IsActive = ReferenceEquals(t, tab);

            ActiveTab = tab;

            // Lazy-load the selected feed.
            switch (tab.Content)
            {
                case AppLogPanelViewModel log:
                    log.Load();
                    break;
                case SectionActivityViewModel activity:
                    activity.LoadAsync();
                    break;
            }
        }

        private static DiagnosticsTab MakeTab(
            string key, string label, string icon, ViewModelBase content) =>
            new() { Key = key, Label = label, Icon = icon, Content = content };
    }
}
