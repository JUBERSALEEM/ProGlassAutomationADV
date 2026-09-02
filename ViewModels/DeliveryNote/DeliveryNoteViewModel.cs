using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.DeliveryNote;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail;
using ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteList;
using ProGlassAutomation.ViewModels.DeliveryNote.GodView;
using ProGlassAutomation.ViewModels.Shared;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;

using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.JobOrder.Repository;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.DeliveryNote
{
    /// <summary>
    /// Shell ViewModel for the Delivery Note module.
    ///
    /// Workspace NEW DN / CREATE DN:
    ///   Opens or creates a Dispatching DN with remaining balance only.
    ///   Never opens a Dispatched DN — those open from God View.
    /// </summary>
    public class DeliveryNoteViewModel : ViewModelBase,
        ProGlassAutomation.Agent.Context.IAgentContextAware
    {
        private readonly IDeliveryNoteRepository _deliveryNotes = null!;
        private readonly IDialogService _dialogs = null!;
        private readonly IJobOrderRepository _jobs = null!;

        public DeliveryNoteViewModel(IDeliveryNoteRepository? deliveryNotes, IDialogService? dialogs, IJobOrderRepository? jobs)
        {
            _deliveryNotes = deliveryNotes!;
            _dialogs = dialogs!;
            _jobs = jobs!;
        }

        private Func<Task>? _dataSeededHandler;
        private bool _disposed;

        public string Title { get; } = "Delivery Notes";

        public string AgentContextDetail
        {
            get
            {
                try
                {
                    if (!IsDetailView) return "";

                    var id = DetailVM?.DeliveryNoteId;
                    return string.IsNullOrWhiteSpace(id) ? "" : $"editing {id}";
                }
                catch { return ""; }
            }
        }
        public string Subtitle { get; } =
            "Track glass deliveries, partial shipments and balance quantities";

        public const string TabWorkspace = "Workspace";
        public const string TabGodView = "GodView";
        public const string TabActivity = "Activity";

        public DeliveryNoteListViewModel ListVM { get; }
        public DeliveryNoteDetailViewModel DetailVM { get; }

        /// <summary>God View tab — every delivery note in the system.</summary>
        public DeliveryNoteGodViewViewModel GodViewVM { get; }

        /// <summary>Activity tab — live audit feed for this section.</summary>
        public SectionActivityViewModel ActivityVM { get; }

        public Func<string, Task>? OnRequestOpenTaxInvoiceForDeliveryNote
        { get; set; }
        public Func<string, Task>? OnRequestOpenTaxInvoiceForMasterFromProforma
        { get; set; }
        public Func<string, Task<string>>? OnRequestAutoCreateTaxInvoice
        { get; set; }

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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { ProGlassAutomation.Services.SampleData.SampleDataService.DataSeeded -= _dataSeededHandler; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DeliveryNoteViewModel] Dispose: DataSeeded detach failed: " + ex.Message); }
            try { ListVM?.Dispose(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DeliveryNoteViewModel] Dispose: ListVM teardown failed: " + ex.Message); }
            try { ActivityVM?.Dispose(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DeliveryNoteViewModel] Dispose: ActivityVM teardown failed: " + ex.Message); }
            GC.SuppressFinalize(this);
        }

        public DeliveryNoteViewModel()
         : this(
            App.ServiceProvider?.GetService<IDeliveryNoteRepository>()!,
            App.ServiceProvider?.GetService<IDialogService>()?? new DialogService(),
            App.ServiceProvider?.GetService<IJobOrderRepository>()!){
            ListVM = new DeliveryNoteListViewModel();
                        _dataSeededHandler = async () => { try { await ListVM.LoadAsync(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); } };
            ProGlassAutomation.Services.SampleData.SampleDataService.DataSeeded += _dataSeededHandler;
            DetailVM = new DeliveryNoteDetailViewModel();

            ListVM.OnDeliveryNoteOpenRequested = id => AsyncUtility.SafeFireAndForget(
                () => LoadDetailAsync(id), "DeliveryNote");

            ListVM.OnCreateDnFromJobOrder = jo => AsyncUtility.SafeFireAndForget(
                () => CreateDnFromJobOrderAsync(jo), "DeliveryNote");

            ListVM.OnRequestOpenTaxInvoiceForProforma = async piId =>
            {
                try
                {
                    await RelayMasterInvoiceRequestAsync(piId);
                }
                catch (Exception ex)
                {
                    _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.FailedToOpenMasterInvoice"), ex.Message),
                        LocalizationService.Instance.T("Msg.CapNavigationError"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };

            DetailVM.OnRequestBackToList = () => AsyncUtility.SafeFireAndForget(ReturnToList, "DeliveryNote");

            DetailVM.OnRequestCreateDnFromMaster = masterDn => AsyncUtility.SafeFireAndForget(
                () => CreateDnFromJobOrderAsync(
                    new Models.JobOrder.SavedJobOrder.SavedJobOrder
                    {
                        JobOrderId = masterDn.JobOrderId,
                        SourceProformaId = masterDn.ProformaId,
                        CustomerName = masterDn.CustomerName,
                        ProjectName = masterDn.ProjectName,
                        FullSnapshotJson = "{}"
                    }), "DeliveryNote");

            DetailVM.OnRequestAutoCreateTaxInvoice = async dnId =>
            {
                if (OnRequestAutoCreateTaxInvoice == null) return "";
                return await OnRequestAutoCreateTaxInvoice(dnId);
            };

            DetailVM.OnRequestOpenMasterDn = jobOrderId => AsyncUtility.SafeFireAndForget(
                () => OpenMasterForJobOrderAsync(jobOrderId), "DeliveryNote");

            ListVM.OnRequestOpenMaster = jobOrderId => AsyncUtility.SafeFireAndForget(
                () => OpenMasterForJobOrderAsync(jobOrderId), "DeliveryNote");

            DetailVM.OnRequestOpenTaxInvoiceForDeliveryNote = async dnId =>
            {
                try
                {
                    await RelayConvertDnRequestAsync(dnId);
                }
                catch (Exception ex)
                {
                    _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.FailedToConvertDNTo"), ex.Message),
                        LocalizationService.Instance.T("Msg.CapNavigationError"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };

            BackToListCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(ReturnToList, "DeliveryNote"));

            GodViewVM = new DeliveryNoteGodViewViewModel();
            GodViewVM.OnOpenRequested = id =>
                AsyncUtility.SafeFireAndForget(() => LoadDetailAsync(id), "DeliveryNote");

            ActivityVM = new SectionActivityViewModel(
                AuditSections.DeliveryNote);

            SwitchTabCommand = new RelayCommand(param =>
                SwitchTab(param as string ?? TabWorkspace));
        }

        private void SwitchTab(string tabKey)
        {
            ActiveTab = tabKey;

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

        private async Task RelayMasterInvoiceRequestAsync(string proformaId)
        {
            if (string.IsNullOrWhiteSpace(proformaId))
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.CannotCreateMasterInvoiceNo"),
                    LocalizationService.Instance.T("Msg.CapPIReference"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (OnRequestOpenTaxInvoiceForMasterFromProforma == null)
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.TaxInvoiceNavigationIsNot2"),
                    LocalizationService.Instance.T("Msg.CapWired"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            await OnRequestOpenTaxInvoiceForMasterFromProforma(proformaId);
        }

        private async Task RelayConvertDnRequestAsync(string deliveryNoteId)
        {
            if (string.IsNullOrWhiteSpace(deliveryNoteId))
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.CannotConvertToTaxInvoice"),
                    LocalizationService.Instance.T("Msg.CapDNReference"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (OnRequestOpenTaxInvoiceForDeliveryNote == null)
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.TaxInvoiceNavigationIsNot2"),
                    LocalizationService.Instance.T("Msg.CapWired"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            await OnRequestOpenTaxInvoiceForDeliveryNote(deliveryNoteId);
        }

        private async Task LoadDetailAsync(int deliveryNoteId)
        {
            if (IsDetailView &&
                !await ProGlassAutomation.Services.Safety
                    .UnsavedChangesGuard.ConfirmLeaveAsync(
                        () => DetailVM.HasUnsavedChanges,
                        () => DetailVM.TrySaveAsync(),
                        "delivery note"))
                return;

            await DetailVM.LoadAsync(deliveryNoteId);
            IsListView = false;
        }

        private async Task ReturnToList()
        {
            if (!await ProGlassAutomation.Services.Safety
                .UnsavedChangesGuard.ConfirmLeaveAsync(
                    () => DetailVM.HasUnsavedChanges,
                    () => DetailVM.TrySaveAsync(),
                    "delivery note"))
                return;

            IsListView = true;
            await SafeLoadListAsync();
        }

        public async Task OpenDeliveryNoteAsync(int deliveryNoteId)
        {
            await DetailVM.LoadAsync(deliveryNoteId);
            IsListView = false;
            await SafeLoadListAsync();
        }

        /// <summary>
        /// Opens the Master DN created from this Job Order. Remaining
        /// balance is the default grid; the user can toggle the original
        /// full JO set on the Master itself.
        /// </summary>
        public async Task OpenMasterForJobOrderAsync(string jobOrderId)
        {
            if (_deliveryNotes == null || string.IsNullOrWhiteSpace(jobOrderId))
                return;

            var master = await _deliveryNotes
                .GetMasterDnForJobOrderAsync(jobOrderId);

            if (master == null && _jobs != null)
            {
                var jo = await _jobs.GetByJobOrderIdAsync(jobOrderId);
                if (jo != null)
                {
                    try
                    {
                        master = await Services.DeliveryNote
                            .MasterDeliveryNoteService
                            .EnsureForJobOrderAsync(jo);
                    }
                    catch (Exception ex)
                    {
                        _dialogs.Show(
                            "Could not open Master DN.\n\n" + ex.Message,
                            LocalizationService.Instance.T("JobOrder.MasterDN"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                }
            }

            if (master == null)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.MasterDNExistsForX"), jobOrderId),
                    LocalizationService.Instance.T("Msg.CapMasterDNNotFound"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            await DetailVM.LoadAsync(master.Id);
            IsListView = false;
            await SafeLoadListAsync();
        }

        private async Task SafeLoadListAsync()
        {
            try
            {
                await ListVM.LoadAsync();
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[DeliveryNoteViewModel] ListVM.LoadAsync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Workspace NEW DN / CREATE DN. Opens Dispatching with remaining
        /// qty only. Never opens a Dispatched DN.
        /// </summary>
        private async Task CreateDnFromJobOrderAsync(SavedJobOrder jobOrder)
        {
            var result = await DeliveryNoteCreateService
                .CreateOrOpenDispatchingAsync(jobOrder);

            switch (result.Outcome)
            {
                case DnCreateResult.Kind.OpenExisting:
                case DnCreateResult.Kind.Created:
                    ActiveTab = TabWorkspace;
                    await DetailVM.LoadAsync(result.DeliveryNoteDbId);
                    IsListView = false;
                    await SafeLoadListAsync();
                    break;

                case DnCreateResult.Kind.FullyDelivered:
                    _dialogs.Show(
                        result.Message,
                        LocalizationService.Instance.T("Msg.CapFullyDelivered"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    break;

                default:
                    _dialogs.Show(
                        string.IsNullOrWhiteSpace(result.Message)
                            ? "Failed to create delivery note."
                            : result.Message,
                        LocalizationService.Instance.T("Msg.CapCreateFailed"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    break;
            }
        }
    }
}
