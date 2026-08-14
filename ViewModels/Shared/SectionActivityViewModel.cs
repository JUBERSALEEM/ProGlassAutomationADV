// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Shared/SectionActivityViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Shared — per-section Activity tab
//
// Live activity feed for ONE section. Used by the Activity tab inside
// each module's tab strip (Delivery Note / Job Order / Tax Invoice),
// so every section shows its own user-action trail as it happens.
//
// • Loads recent entries from AuditService on activation.
// • Subscribes to AuditService.ActivityLogged → new rows appear live
//   (marshalled to the UI thread by the service).
// • Capped at 200 rows in memory; older rows stay in audit.db.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Audit;
using ProGlassAutomation.Services.Audit;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ProGlassAutomation.Commands;

namespace ProGlassAutomation.ViewModels.Shared
{
    /// <summary>One rendered activity row.</summary>
    public class ActivityRowViewModel
    {
        public string TimeDisplay { get; set; } = "";
        public string Username { get; set; } = "";
        public string Action { get; set; } = "";
        public string Details { get; set; } = "";
        public string Section { get; set; } = "";

        /// <summary>Raw before/after change set JSON (empty = no diff).</summary>
        public string ChangesJson { get; set; } = "";

        /// <summary>True when a GitHub-style diff can be shown.</summary>
        public bool HasChanges =>
            !string.IsNullOrWhiteSpace(ChangesJson);
    }

    /// <summary>
    /// Activity feed filtered to a single section.
    /// Create one per module shell and bind the Activity tab to it.
    /// Pass section = null for an ALL-sections feed (Diagnostics module).
    /// </summary>
    public class SectionActivityViewModel : ViewModelBase
    {
        private readonly string? _section;
        private const int MaxRows = 200;

        public ObservableCollection<ActivityRowViewModel> Rows { get; } = new();

        public string Section => _section ?? "All sections";

        private bool _isEmpty = true;
        public bool IsEmpty
        {
            get => _isEmpty;
            private set { _isEmpty = value; OnPropertyChanged(); }
        }

        public ICommand RefreshCommand { get; }

        /// <summary>Shows the GitHub-style before/after diff for a row.</summary>
        public ICommand ShowChangesCommand { get; }

        public SectionActivityViewModel(string? section)
        {
            _section = section;

            RefreshCommand = new RelayCommand(_ => LoadAsync());

            ShowChangesCommand = new RelayCommand(param =>
            {
                if (param is ActivityRowViewModel row && row.HasChanges)
                {
                    Views.Shared.ChangesDialog.Show(
                        System.Windows.Application.Current?.MainWindow,
                        $"{row.Section} — {row.Action} — {row.Username}",
                        AuditService_ChangesFromRow(row));
                }
            });

            // Live updates — the service raises this on the UI thread.
            AuditService.ActivityLogged += OnActivityLogged;
        }

        /// <summary>Reloads the latest entries for this section.</summary>
        public async void LoadAsync()
        {
            var entries = await AuditService.GetRecentAsync(
                MaxRows, string.IsNullOrEmpty(_section) ? null : _section);

            Rows.Clear();
            foreach (var e in entries)
                Rows.Add(ToRow(e));

            IsEmpty = Rows.Count == 0;
        }

        private void OnActivityLogged(object? sender, ActivityEventArgs e)
        {
            if (!string.IsNullOrEmpty(_section) &&
                e.Entry.Section != _section)
                return;

            Rows.Insert(0, ToRow(e.Entry));

            while (Rows.Count > MaxRows)
                Rows.RemoveAt(Rows.Count - 1);

            IsEmpty = Rows.Count == 0;
        }

        private static ActivityRowViewModel ToRow(ActivityLogEntry e)
        {
            return new ActivityRowViewModel
            {
                TimeDisplay = e.TimestampUtc.ToLocalTime()
                    .ToString("dd MMM yyyy  HH:mm:ss"),
                Username = e.Username,
                Action = e.Action,
                Details = e.Details,
                Section = e.Section,
                ChangesJson = e.ChangesJson ?? ""
            };
        }

        private static List<Services.Audit.AuditChange>
            AuditService_ChangesFromRow(ActivityRowViewModel row) =>
            Services.Audit.AuditChangeSerializer.FromJson(row.ChangesJson);
    }
}
