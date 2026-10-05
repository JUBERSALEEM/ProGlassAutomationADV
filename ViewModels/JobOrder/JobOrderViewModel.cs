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
using ProGlassAutomation.Services.Localization;

using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.JobOrder.Repository;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.JobOrder
{
    public class JobOrderViewModel : ViewModelBase,
        ProGlassAutomation.Agent.Context.IAgentContextAware,
        System.IDisposable
    {
        private readonly IDialogService _dialogs = null!;
        private readonly IJobOrderRepository _jobs = null!;

        public JobOrderViewModel(IDialogService? dialogs, IJobOrderRepository? jobs)
        {
            _dialogs = dialogs!;
            _jobs = jobs!;
        
            // ERR-633EEB: run shared initialization on EVERY construction
            // path. DI selects this (greediest) ctor, so without this call the
            // parameterless ctor's setup never ran and commands stayed null.
            InitializeCore();
        }

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

        public SavedJobOrdersListViewModel ListVM { get; private set; }
        public JobOrderDetailViewModel DetailVM { get; private set; }
        public JobOrderGodViewViewModel GodViewVM { get; private set; }
        public SectionActivityViewModel ActivityVM { get; private set; }

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

        public ICommand BackToListCommand { get; private set; }
        public ICommand SwitchTabCommand { get; private set; }
        public Action<int>? OnRequestOpenDeliveryNote { get; set; }

        public JobOrderViewModel()
         : this(
            App.ServiceProvider?.GetService<IDialogService>()?? new DialogService(),
            App.ServiceProvider?.GetService<IJobOrderRepository>()!){ }

        // ── Shared initialization (ERR-633EEB) ─────────────────────
        // Extracted verbatim from the parameterless ctor so both it
        // and the DI-selected parameterized ctor initialize fully.
        private void InitializeCore()
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
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.CouldNotOpenJobOrder"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Dashboard.JobOrders"),
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
                    $"[JobOrderVM] ReturnToList failed: {ExceptionMessages.Deep(ex)}",
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

                if (version == null || _jobs == null) return;

                var entity = await _jobs.GetByJobOrderIdAsync(version.EntityId);

                if (entity == null)
                {
                    _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.JobOrderXNoLonger"), version.EntityId),
                        LocalizationService.Instance.T("Msg.CapRestoreVersion"),
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
                    _dialogs.Show(
                        LocalizationService.Instance.T("Msg.VersionCouldNotBeApplied"),
                        LocalizationService.Instance.T("Msg.CapRestoreVersion"),
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"[JobOrderVM] RestoreVersion failed: {ExceptionMessages.Deep(ex)}",
                    new LogContext { Module = "JobOrder", Operation = "RestoreVersion" });
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  DISPOSE — detach static-event subscribers under this VM so it and
        //  its children can be collected.
        //  • ListVM subscribes DocumentLifecycle + SampleDataService.DataSeeded.
        //  • ActivityVM subscribes AuditService.ActivityLogged.
        //  Without this, switching modules or closing the window leaves these
        //  static handlers holding the VMs alive (a real memory leak) and the
        //  reload/feed handlers keep running for the whole session.
        // ═════════════════════════════════════════════════════════════════
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            ListVM?.Dispose();
            ActivityVM?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
