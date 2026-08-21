// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/JobOrder/JobOrderViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Job Order — Shell (Phase 3: Async Cleanup)
// VERSION: Phase 3 — 2026-08-21 — Removed async void, uses central AsyncUtility
//
// CHANGES vs Phase 2:
//   ✓ LoadJobOrderIntoDetail was async void => now void + SafeFireAndForget
//   ✓ ReturnToList was async void => now async Task ReturnToListAsync + wrapper
//   ✓ RestoreVersion was async void => now async Task RestoreVersionAsync + wrapper
//   ✓ All call sites use AsyncUtility.SafeFireAndForget (single async void)
//   ✓ VSTHRD100 compliant
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.ViewModels.JobOrder.GodView;
using ProGlassAutomation.ViewModels.JobOrder.JobOrderDetail;
using ProGlassAutomation.ViewModels.JobOrder.SavedJobOrdersList;
using ProGlassAutomation.ViewModels.Shared;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.JobOrder
{
    public class JobOrderViewModel : ViewModelBase,
        ProGlassAutomation.Agent.Context.IAgentContextAware
    {
        public string Title { get; } = "Job Orders";
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

        public const string TabWorkspace = "Workspace";
        public const string TabGodView = "GodView";
        public const string TabActivity = "Activity";

        public SavedJobOrdersListViewModel ListVM { get; }
        public JobOrderDetailViewModel DetailVM { get; }
        public JobOrderGodViewViewModel GodViewVM { get; }
        public SectionActivityViewModel ActivityVM { get; }

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

        private string _activeTab = TabWorkspace;
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

        public ICommand BackToListCommand { get; }
        public ICommand SwitchTabCommand { get; }
        public Action<int>? OnRequestOpenDeliveryNote { get; set; }

        public JobOrderViewModel()
        {
            ListVM = new SavedJobOrdersListViewModel();
            DetailVM = new JobOrderDetailViewModel();

            // Phase 3: No async void — wrap with SafeFireAndForget
            ListVM.OnJobOrderOpenRequested = id =>
                AsyncUtility.SafeFireAndForget(() => OpenJobOrderAsync(id), $"JobOrder Open {id}");

            ListVM.OnVersionRestoreRequested = versionId =>
                AsyncUtility.SafeFireAndForget(() => RestoreVersionAsync(versionId), $"JobOrder Restore {versionId}");

            DetailVM.OnRequestBackToList = () =>
                AsyncUtility.SafeFireAndForget(ReturnToListAsync, "JobOrder BackToList");

            DetailVM.OnRequestOpenDeliveryNote = dnId =>
                OnRequestOpenDeliveryNote?.Invoke(dnId);

            BackToListCommand = new RelayCommand(_ =>
                AsyncUtility.SafeFireAndForget(ReturnToListAsync, "JobOrder BackToList"));

            GodViewVM = new JobOrderGodViewViewModel();
            GodViewVM.OnOpenRequested = id =>
                AsyncUtility.SafeFireAndForget(() => OpenJobOrderAsync(id), $"JobOrder GodView Open {id}");

            ActivityVM = new SectionActivityViewModel(AuditSections.JobOrder);

            SwitchTabCommand = new RelayCommand(param =>
                SwitchTab(param as string ?? TabWorkspace));
        }

        private void SwitchTab(string tabKey)
        {
            ActiveTab = tabKey;
            switch (tabKey)
            {
                case TabGodView:
                    AsyncUtility.SafeFireAndForget(() => GodViewVM.LoadAsync(), "JobOrder GodView Load");
                    break;
                case TabActivity:
                    AsyncUtility.SafeFireAndForget(() => ActivityVM.LoadAsync(), "JobOrder Activity Load");
                    break;
            }
        }

        // Phase 3: Was async void LoadJobOrderIntoDetail — now sync wrapper
        private void LoadJobOrderIntoDetail(int jobOrderId) =>
            AsyncUtility.SafeFireAndForget(() => OpenJobOrderAsync(jobOrderId), $"JobOrder LoadDetail {jobOrderId}");

        public async Task OpenJobOrderAsync(int jobOrderId)
        {
            try
            {
                if (!await ProGlassAutomation.Services.Safety
                    .UnsavedChangesGuard.ConfirmLeaveAsync(
                        () => DetailVM.HasUnsavedChanges,
                        () => DetailVM.TrySaveAsync(),
                        "job order"))
                    return;

                await DetailVM.LoadAsync(jobOrderId);
                IsListView = false;
                ActiveTab = TabWorkspace;
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

        // Phase 3: Was async void — now async Task
        private async Task ReturnToListAsync()
        {
            try
            {
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
                AppLogService.Warning(
                    $"[JobOrderVM] ReturnToList failed: {ex.Message}",
                    new LogContext { Module = "JobOrder", Operation = "ReturnToList" });
            }
        }

        // Phase 3: Was async void — now async Task
        private async Task RestoreVersionAsync(int versionId)
        {
            try
            {
                var version = await ProGlassAutomation.Services.Audit
                    .VersionHistoryService.GetByIdAsync(versionId);

                if (version == null || App.Jobs == null) return;

                var entity = await App.Jobs.GetByJobOrderIdAsync(version.EntityId);

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
                AppLogService.Warning(
                    $"[JobOrderVM] RestoreVersion failed: {ex.Message}",
                    new LogContext { Module = "JobOrder", Operation = "RestoreVersion" });
            }
        }
    }
}
