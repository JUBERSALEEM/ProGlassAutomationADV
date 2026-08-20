using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.ViewModels.JobOrder.GodView;
using ProGlassAutomation.ViewModels.JobOrder.JobOrderDetail;
using ProGlassAutomation.ViewModels.JobOrder.SavedJobOrdersList;
using ProGlassAutomation.ViewModels.Shared;
using System;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.JobOrder
{
    /// <summary>
    /// Shell ViewModel for the Job Order module.
    /// Owns the list ↔ detail view toggle and both sub-VMs.
    ///
    /// Phase 10.5: Added OnRequestOpenDeliveryNote callback to propagate
    /// DN navigation requests up to MainViewModel.
    ///
    /// TAB STRIP (new): Workspace | God View | Activity
    ///   • Workspace — the original list ↔ detail flow (unchanged)
    ///   • God View  — all-records overview (JobOrderGodViewViewModel)
    ///   • Activity  — live audit feed for this section
    /// </summary>
    public class JobOrderViewModel : ViewModelBase,
        ProGlassAutomation.Agent.Context.IAgentContextAware
    {
        public string Title { get; } = "Job Orders";

        // ══════════════════════════════════════════════════════════════
        //  AGENT CONTEXT (opt-in, read-only)
        //
        //  Publishes the job order number ONLY while the detail view is
        //  actually open. On the list screen there is no single record in
        //  focus, so it returns empty rather than naming whichever row
        //  happens to be highlighted - the agent would otherwise answer
        //  about a document the user is not looking at.
        //
        //  No customer, project or value: those are ERP data and need an
        //  audited read tool with a permission check.
        // ══════════════════════════════════════════════════════════════
        public string AgentContextDetail
        {
            get
            {
                try
                {
                    if (!IsDetailView) return "";

                    var id = DetailVM?.JobOrderId;
                    return string.IsNullOrWhiteSpace(id) ? "" : $"editing {id}";
                }
                catch { return ""; }
            }
        }
        public string Subtitle { get; } = "Track production status of approved invoices";

        // ── tab keys ──────────────────────────────────────────────────────
        public const string TabWorkspace = "Workspace";
        public const string TabGodView = "GodView";
        public const string TabActivity = "Activity";

        // ── sub-VMs ───────────────────────────────────────────────────────

        public SavedJobOrdersListViewModel ListVM { get; }
        public JobOrderDetailViewModel DetailVM { get; }

        /// <summary>God View tab — every job order in the system.</summary>
        public JobOrderGodViewViewModel GodViewVM { get; }

        /// <summary>Activity tab — live audit feed for this section.</summary>
        public SectionActivityViewModel ActivityVM { get; }

        // ── view state toggle ─────────────────────────────────────────────

        private bool _isListView = true;
        public bool IsListView
        {
            get => _isListView;
            set
            {
                if (_isListView == value) return;
                _isListView = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDetailView));
            }
        }

        public bool IsDetailView => !_isListView;

        // ── tab strip state ───────────────────────────────────────────────

        private string _activeTab = TabWorkspace;

        /// <summary>Which tab is active: Workspace / GodView / Activity.</summary>
        public string ActiveTab
        {
            get => _activeTab;
            set
            {
                if (_activeTab == value) return;
                _activeTab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsWorkspaceTab));
                OnPropertyChanged(nameof(IsGodViewTab));
                OnPropertyChanged(nameof(IsActivityTab));
            }
        }

        public bool IsWorkspaceTab => ActiveTab == TabWorkspace;
        public bool IsGodViewTab => ActiveTab == TabGodView;
        public bool IsActivityTab => ActiveTab == TabActivity;

        // ── commands ──────────────────────────────────────────────────────

        public ICommand BackToListCommand { get; }

        /// <summary>Switches tabs. Parameter = tab key constant.</summary>
        public ICommand SwitchTabCommand { get; }

        // ── callback for main shell (Phase 10.5) ──────────────────────────

        /// <summary>
        /// Fired when JO detail view requests to open a newly created DN.
        /// MainViewModel wires this to navigate to DN module + open detail.
        /// Parameter = DN database Id.
        /// </summary>
        public Action<int>? OnRequestOpenDeliveryNote { get; set; }

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public JobOrderViewModel()
        {
            ListVM = new SavedJobOrdersListViewModel();
            DetailVM = new JobOrderDetailViewModel();

            // When user clicks OPEN on a list row → load detail
            ListVM.OnJobOrderOpenRequested = LoadJobOrderIntoDetail;

            // v2 §24: version restore from history dialog
            ListVM.OnVersionRestoreRequested = RestoreVersion;

            // When user clicks Save/Back in detail → return to list
            DetailVM.OnRequestBackToList = ReturnToList;

            // Phase 10.5: When user clicks Create DN → propagate to shell
            DetailVM.OnRequestOpenDeliveryNote = dnId =>
                OnRequestOpenDeliveryNote?.Invoke(dnId);

            BackToListCommand = new RelayCommand(_ => ReturnToList());

            // ── God View tab: row-open hands off to detail flow ───────
            GodViewVM = new JobOrderGodViewViewModel();
            GodViewVM.OnOpenRequested = LoadJobOrderIntoDetail;

            // ── Activity tab: live audit feed for this section ────────
            ActivityVM = new SectionActivityViewModel(AuditSections.JobOrder);

            SwitchTabCommand = new RelayCommand(param =>
                SwitchTab(param as string ?? TabWorkspace));
        }

        // ═════════════════════════════════════════════════════════════════
        //  TAB SWITCHING
        // ═════════════════════════════════════════════════════════════════

        private void SwitchTab(string tabKey)
        {
            ActiveTab = tabKey;

            // Load lazily when each overview tab is entered.
            switch (tabKey)
            {
                case TabGodView:
                    GodViewVM.LoadAsync();
                    break;
                case TabActivity:
                    ActivityVM.LoadAsync();
                    break;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  NAVIGATION
        // ═════════════════════════════════════════════════════════════════

        private async void LoadJobOrderIntoDetail(int jobOrderId)
            => _ = OpenJobOrderAsync(jobOrderId);

        /// <summary>
        /// Opens a Job Order in its detail workspace — public entry point
        /// used by the Reports module sidebar ("Open Job Order") and the
        /// internal list/detail callbacks. The original JO remains the
        /// single source for production, items and delivery context.
        /// </summary>
        public async Task OpenJobOrderAsync(int jobOrderId)
        {
            // BUG-FIX: previously unguarded — an exception here escaped
            // into async-void territory and could crash the app.
            try
            {
                // §28: don't steamroll unsaved edits when opening another JO.
                if (!await ProGlassAutomation.Services.Safety
                    .UnsavedChangesGuard.ConfirmLeaveAsync(
                        () => DetailVM.HasUnsavedChanges,
                        () => DetailVM.TrySaveAsync(),
                        "job order"))
                    return;

                await DetailVM.LoadAsync(jobOrderId);
                IsListView = false;
                ActiveTab = TabWorkspace;   // open always lands on workspace
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Could not open job order:\n\n{ex.Message}",
                    "Job Orders",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private async void ReturnToList()
        {
            try
            {
                // §28: user decides Cancel / Discard / Save & Continue.
                if (!await ProGlassAutomation.Services.Safety
                    .UnsavedChangesGuard.ConfirmLeaveAsync(
                        () => DetailVM.HasUnsavedChanges,
                        () => DetailVM.TrySaveAsync(),
                        "job order"))
                    return;

                IsListView = true;
                await ListVM.LoadJobOrdersAsync();
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[JobOrderVM] ReturnToList failed: {ex.Message}");
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  v2 §24 — VERSION RESTORE
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Opens the job order that owns the chosen version, then applies
        /// the version snapshot into the editor (pending save).
        /// </summary>
        private async void RestoreVersion(int versionId)
        {
            try
            {
                var version = await ProGlassAutomation.Services.Audit
                    .VersionHistoryService.GetByIdAsync(versionId);

                if (version == null || App.Jobs == null) return;

                var entity = await App.Jobs
                    .GetByJobOrderIdAsync(version.EntityId);

                if (entity == null)
                {
                    System.Windows.MessageBox.Show(
                        $"Job order '{version.EntityId}' no longer exists.",
                        "Restore Version",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                await DetailVM.LoadAsync(entity.Id);
                IsListView = false;
                ActiveTab = TabWorkspace;

                bool ok = await DetailVM.RestoreVersionAsync(versionId);

                if (!ok)
                {
                    System.Windows.MessageBox.Show(
                        "This version could not be applied.",
                        "Restore Version",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[JobOrderVM] RestoreVersion failed: {ex.Message}");
            }
        }
    }
}