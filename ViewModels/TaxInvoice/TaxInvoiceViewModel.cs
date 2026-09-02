using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.ViewModels.Shared;
using ProGlassAutomation.ViewModels.TaxInvoice.GodView;
using ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceDetail;
using ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceList;
using ProGlassAutomation.Views.TaxInvoice.TaxInvoiceList;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;

using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.TaxInvoice
{
    /// <summary>
    /// Shell ViewModel for the Tax Invoice module.
    ///
    /// Manages list ↔ detail toggle. Same pattern as DeliveryNoteViewModel.
    ///
    /// ENTRY POINTS:
    ///   • Navigation sidebar          → constructor fires
    ///   • ListVM.OnInvoiceOpenRequested   → LoadDetailAsync(id)
    ///   • ListVM.OnCreateInvoiceRequested → OpenProformaPickerAsync()
    ///   • BackToListCommand           → ReturnToList()
    ///
    /// CROSS-MODULE ENTRY POINTS (called by MainViewModel from DN module):
    ///   • OpenInvoiceAsync(id)                    — open specific saved invoice
    ///   • CreateFromProformaAsync(proformaId)     — Master Invoice from DN List
    ///                                                (imports all delivered qty
    ///                                                 across sibling DNs)
    ///   • CreateFromDeliveryNoteAsync(dnId)       — Convert from DN Detail
    ///                                                (imports only THIS DN's
    ///                                                 delivered qty)
    ///
    /// PARTIAL INVOICING RULE:
    ///   Full PI import (all rows regardless of delivery) is only used when
    ///   creating an invoice from the PI picker dialog directly — before
    ///   any deliveries exist. All DN-driven flows use delivered qty only.
    /// </summary>
    public class TaxInvoiceViewModel : ViewModelBase, IDisposable,
        ProGlassAutomation.Agent.Context.IAgentContextAware
    {
        private readonly IDeliveryNoteRepository _deliveryNotes = null!;
        private readonly IDialogService _dialogs = null!;
        private readonly IProformaInvoiceRepository _invoices = null!;
        private readonly ITaxInvoiceRepository _taxInvoices = null!;

        // ── ERR-633EEB CLASS FIX (2026-09-02) ──────────────────────
        // Initialization used to live in the PARAMETERLESS ctor while
        // DI selected this parameterized overload (MS.DI always picks
        // the greediest satisfiable ctor). Every DI-resolved instance
        // was therefore half-built — null child view models / null
        // commands — and threw NullReferenceException on first use.
        // The body was moved HERE so all construction routes (DI,
        // Activator fallback, XAML design-time) initialize fully.
        public TaxInvoiceViewModel(IDeliveryNoteRepository? deliveryNotes, IDialogService? dialogs, IProformaInvoiceRepository? invoices, ITaxInvoiceRepository? taxInvoices)
        {
            _deliveryNotes = deliveryNotes!;
            _dialogs = dialogs!;
            _invoices = invoices!;
            _taxInvoices = taxInvoices!;

            ListVM = new TaxInvoiceListViewModel();
            // Phase 4: Named handler — detachable, no lambda leak
            _dataSeededHandler = async () => { try { await ListVM.LoadAsync(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); } };
            ProGlassAutomation.Services.SampleData.SampleDataService.DataSeeded += _dataSeededHandler;
            DetailVM = new TaxInvoiceDetailViewModel();

            // Wire list → detail navigation
            ListVM.OnInvoiceOpenRequested = id =>
                AsyncUtility.SafeFireAndForget(() => LoadDetailAsync(id), "TaxInvoice");

            // Shell opens ProformaPickerDialog — ListVM never touches Views
            ListVM.OnCreateInvoiceRequested = () =>
                AsyncUtility.SafeFireAndForget(OpenProformaPickerAsync, "TaxInvoice");

            // Wire detail → list back navigation
            DetailVM.OnRequestBackToList = () =>
                AsyncUtility.SafeFireAndForget(ReturnToList, "TaxInvoice");

            BackToListCommand = new RelayCommand(
                _ => AsyncUtility.SafeFireAndForget(ReturnToList, "TaxInvoice"));

            // ── God View tab: row-open hands off to detail flow ───────
            GodViewVM = new TaxInvoiceGodViewViewModel();
            GodViewVM.OnOpenRequested = id =>
                AsyncUtility.SafeFireAndForget(() => LoadDetailAsync(id), "TaxInvoice");

            // ── Activity tab: live audit feed for this section ────────
            ActivityVM = new SectionActivityViewModel(
                AuditSections.TaxInvoice);

            SwitchTabCommand = new RelayCommand(param =>
                SwitchTab(param as string ?? TabWorkspace));

            // Initial list load
            AsyncUtility.SafeFireAndForget(SafeLoadListAsync, "TaxInvoice");
        }

        // Phase 4: Named handler for static event — detachable
        private Func<Task>? _dataSeededHandler;
        private bool _disposed;

        public string Title { get; } = "Tax Invoices";

        // ══════════════════════════════════════════════════════════════
        //  AGENT CONTEXT (opt-in, read-only) — see JobOrderViewModel for
        //  the reasoning. Detail view only; identifier only, never the
        //  invoice value or the customer.
        // ══════════════════════════════════════════════════════════════
        public string AgentContextDetail
        {
            get
            {
                try
                {
                    if (!IsDetailView) return "";

                    var id = DetailVM?.EditingInvoiceNumber;
                    return string.IsNullOrWhiteSpace(id) ? "" : $"editing {id}";
                }
                catch { return ""; }
            }
        }
        public string Subtitle { get; } =
            "Create and manage UAE VAT-compliant tax invoices";

        // ── Tab strip keys (Workspace | God View | Activity) ─────────────

        public const string TabWorkspace = "Workspace";
        public const string TabGodView = "GodView";
        public const string TabActivity = "Activity";

        // ── Child VMs ─────────────────────────────────────────────────────

        public TaxInvoiceListViewModel ListVM { get; }
        public TaxInvoiceDetailViewModel DetailVM { get; }

        /// <summary>God View tab — every tax invoice in the system.</summary>
        public TaxInvoiceGodViewViewModel GodViewVM { get; }

        /// <summary>Activity tab — live audit feed for this section.</summary>
        public SectionActivityViewModel ActivityVM { get; }

        // ── View toggle ───────────────────────────────────────────────────

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

        // ── Tab strip state ───────────────────────────────────────────────

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

        // ── Commands ──────────────────────────────────────────────────────

        public ICommand BackToListCommand { get; }

        /// <summary>Switches tabs. Parameter = tab key constant.</summary>
        public ICommand SwitchTabCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public TaxInvoiceViewModel()
         : this(
            App.ServiceProvider?.GetService<IDeliveryNoteRepository>()!,
            App.ServiceProvider?.GetService<IDialogService>()?? new DialogService(),
            App.ServiceProvider?.GetService<IProformaInvoiceRepository>()!,
            App.ServiceProvider?.GetService<ITaxInvoiceRepository>()!){
        }

        // ═════════════════════════════════════════════════════════════════
        //  TAB SWITCHING
        // ═════════════════════════════════════════════════════════════════

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

        // ═════════════════════════════════════════════════════════════════
        //  SAFE ASYNC FIRE
        // ═════════════════════════════════════════════════════════════════

        

        // ═════════════════════════════════════════════════════════════════
        //  NAVIGATION
        // ═════════════════════════════════════════════════════════════════

        private async Task LoadDetailAsync(int invoiceId)
        {
            // v2 §28: switching records while edits are pending → confirm.
            if (IsDetailView &&
                !await ProGlassAutomation.Services.Safety
                    .UnsavedChangesGuard.ConfirmLeaveAsync(
                        () => DetailVM.IsDirtyNow(),
                        () => DetailVM.TrySaveAsync(),
                        "tax invoice"))
                return;

            await DetailVM.LoadAsync(invoiceId);
            IsListView = false;
        }

        private async Task ReturnToList()
        {
            // v2 §28: confirm before discarding unsaved invoice edits.
            if (!await ProGlassAutomation.Services.Safety
                .UnsavedChangesGuard.ConfirmLeaveAsync(
                    () => DetailVM.IsDirtyNow(),
                    () => DetailVM.TrySaveAsync(),
                    "tax invoice"))
                return;

            IsListView = true;
            await SafeLoadListAsync();
        }

        // ═════════════════════════════════════════════════════════════════
        //  PUBLIC ENTRY POINTS — cross-module navigation
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called by MainViewModel when navigating directly to a saved invoice.
        /// e.g. clicking "Open Invoice" from DN detail history sidebar.
        /// </summary>
        public async Task OpenInvoiceAsync(int invoiceId)
        {
            await DetailVM.LoadAsync(invoiceId);
            IsListView = false;
            await SafeLoadListAsync();
        }

        /// <summary>
        /// MASTER INVOICE from DN List (per-JO group row).
        ///
        /// Imports ALL delivered qty across ALL sibling DNs for this PI.
        /// Only rows with total delivered > 0 are included.
        /// Rate from PI, Qty from aggregated DN deliveries.
        ///
        /// If a draft invoice already exists for this PI, prompts user to
        /// open the existing draft or create a new one.
        /// </summary>
        public async Task CreateFromProformaAsync(string proformaId)
        {
            if (string.IsNullOrWhiteSpace(proformaId)) return;

            try
            {
                var existingDraft = await CheckForExistingDraftAsync(proformaId);
                if (existingDraft.OpenExisting) return;

                if (_deliveryNotes != null)
                {
                    var siblings = await _deliveryNotes.GetAllAsync();
                    bool hasDelivered = siblings.Any(d =>
                        string.Equals(d.ProformaId, proformaId,
                            StringComparison.OrdinalIgnoreCase)
                        && d.TotalDeliveredQty > 0
                        && !string.Equals(d.Status, "Dispatching",
                            StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(d.Status, "Draft",
                            StringComparison.OrdinalIgnoreCase));
                    if (!hasDelivered)
                    {
                        _dialogs.Show(
                            LocalizationService.Instance.T("Msg.DispatchedDeliveryYetForThis"),
                            LocalizationService.Instance.T("Msg.CapNothingToInvoice"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        return;
                    }
                }

                // Master Invoice — aggregate all sibling DNs' delivered qty
                await DetailVM.InitFromDeliveryNoteFamilyAsync(proformaId);
                IsListView = false;
                await SafeLoadListAsync();
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToCreateMasterInvoice"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapCreateFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// CONVERT SINGLE DN → Tax Invoice (from DN Detail view).
        ///
        /// Imports ONLY this DN's delivered qty. Rate from PI, Qty from DN.
        /// Rows with 0 delivered in this DN are excluded.
        ///
        /// Prompts if a draft invoice already exists for the same PI.
        /// </summary>
        /// <summary>
        /// Auto-path after DN dispatch: persist a Draft tax invoice for
        /// THIS DN's delivered qty (PI rates). Does not steal an existing
        /// family draft. Skips if this DN is already invoiced.
        /// Returns the new invoice number, or empty if skipped.
        /// </summary>
        public async Task<string> CreateAndSaveDraftFromDeliveryNoteAsync(
            string deliveryNoteId)
        {
            if (string.IsNullOrWhiteSpace(deliveryNoteId)) return "";
            if (_deliveryNotes == null) return "";

            var dn = await _deliveryNotes
                .GetByDeliveryNoteIdAsync(deliveryNoteId);
            if (dn == null || dn.IsMasterDn) return "";
            if (dn.HasTaxInvoice) return dn.TaxInvoiceId ?? "";
            if (dn.TotalDeliveredQty <= 0) return "";

            await DetailVM.InitFromDeliveryNoteAsync(deliveryNoteId);
            await DetailVM.TrySaveAsync();

            string number = DetailVM.EditingInvoiceNumber ?? "";
            if (!string.IsNullOrWhiteSpace(number))
                await SafeLoadListAsync();
            return number;
        }

        public async Task CreateFromDeliveryNoteAsync(string deliveryNoteId)
        {
            if (string.IsNullOrWhiteSpace(deliveryNoteId)) return;
            if (_deliveryNotes == null) return;

            try
            {
                // Look up the DN's ProformaId first — needed for draft check
                var dn = await _deliveryNotes
                    .GetByDeliveryNoteIdAsync(deliveryNoteId);

                if (dn == null)
                {
                    _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.DeliveryNoteXNotFound2"), deliveryNoteId),
                        LocalizationService.Instance.T("Msg.CapFound"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(dn.ProformaId))
                {
                    _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.DNXHasNoLinked"), deliveryNoteId),
                        LocalizationService.Instance.T("Msg.CapPIReference"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var existingDraft = await CheckForExistingDraftAsync(dn.ProformaId);
                if (existingDraft.OpenExisting) return;

                // Convert single DN — only its delivered qty
                await DetailVM.InitFromDeliveryNoteAsync(deliveryNoteId);
                IsListView = false;
                await SafeLoadListAsync();
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToConvertDNTo2"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapConvertFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  PROFORMA PICKER — opens dialog for direct PI-only invoicing
        //  (rare case — normally invoices are created from DN context)
        // ═════════════════════════════════════════════════════════════════

        private async Task OpenProformaPickerAsync()
        {
            if (_invoices == null)
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.ProformaInvoiceDatabaseNotInitialized"),
                    LocalizationService.Instance.T("Msg.CapError"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            await Task.Yield();

            var dialog = _dialogs.Create<ProformaPickerDialog>();

            if (dialog.ShowDialog() != true) return;

            string piId = dialog.SelectedProformaId;
            if (string.IsNullOrWhiteSpace(piId)) return;

            // From picker → try Master Invoice first (uses delivered qty).
            // If no deliveries exist, falls back to full PI import.
            await CreateFromProformaOrFullPiAsync(piId);
        }

        /// <summary>
        /// From PI picker: prefer Master Invoice (delivered qty).
        /// If no deliveries → full PI import as fallback.
        /// </summary>
        private async Task CreateFromProformaOrFullPiAsync(string proformaId)
        {
            try
            {
                var existingDraft = await CheckForExistingDraftAsync(proformaId);
                if (existingDraft.OpenExisting) return;

                // Check if any DN exists for this PI
                bool hasDeliveries = false;
                if (_deliveryNotes != null)
                {
                    var allDns = await _deliveryNotes.GetAllAsync();
                    hasDeliveries = allDns.Any(d =>
                        string.Equals(d.ProformaId, proformaId,
                            StringComparison.OrdinalIgnoreCase)
                        && d.TotalDeliveredQty > 0);
                }

                if (hasDeliveries)
                {
                    // Master Invoice path — sum delivered across sibling DNs
                    await DetailVM.InitFromDeliveryNoteFamilyAsync(proformaId);
                }
                else
                {
                    // No deliveries yet — full PI import (advance invoice case)
                    var choice = _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.DeliveredItemsFoundForX"), proformaId),
                        LocalizationService.Instance.T("Msg.CapDeliveriesYet"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (choice != MessageBoxResult.Yes) return;

                    await DetailVM.InitNewAsync(proformaId);
                }

                IsListView = false;
                await SafeLoadListAsync();
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToCreateInvoiceX"), ex.Message),
                    LocalizationService.Instance.T("Msg.CapCreateFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  EXISTING DRAFT CHECK — shared by all creation paths
        // ═════════════════════════════════════════════════════════════════

        private async Task<(bool OpenExisting, bool ProceedWithNew)>
            CheckForExistingDraftAsync(string proformaId)
        {
            if (_taxInvoices == null)
                return (false, true);

            var existing = await _taxInvoices
                .GetByProformaIdAsync(proformaId);

            var existingDraft = existing.Count > 0
                ? existing.Find(i => string.Equals(
                    i.Status, "Draft",
                    StringComparison.OrdinalIgnoreCase))
                : null;

            if (existingDraft == null)
                return (false, true);

            var choice = _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Msg.DraftInvoiceAlreadyExistsFor"), proformaId, existingDraft.InvoiceNumber, existingDraft.IssueDate.ToString("dd MMM yyyy", LocalizationService.Instance.CurrentCulture), existingDraft.GrandTotal.ToString("N2", LocalizationService.Instance.CurrentCulture)),
                LocalizationService.Instance.T("Msg.CapDraftInvoiceExists"),
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (choice == MessageBoxResult.Cancel)
                return (true, false);  // "Open existing" flag set to skip further work

            if (choice == MessageBoxResult.Yes)
            {
                await DetailVM.LoadAsync(existingDraft.Id);
                IsListView = false;
                await SafeLoadListAsync();
                return (true, false);
            }

            // No → proceed to create new
            return (false, true);
        }

        // ═════════════════════════════════════════════════════════════════
        //  SAFE LIST RELOAD
        // ═════════════════════════════════════════════════════════════════

        private async Task SafeLoadListAsync()
        {
            try
            {
                await ListVM.LoadAsync();
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"[TaxInvoiceViewModel] ListVM.LoadAsync failed: {ex.Message}",
                    new LogContext { Module = "TaxInvoice", Operation = "SafeLoadList" });
            }
        }

        // Phase 4: Event lifetime — detach static events
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { ProGlassAutomation.Services.SampleData.SampleDataService.DataSeeded -= _dataSeededHandler; } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[TaxInvoiceViewModel] Dispose: DataSeeded detach failed: " + ex.Message); }
            GC.SuppressFinalize(this);
        }
    }
}