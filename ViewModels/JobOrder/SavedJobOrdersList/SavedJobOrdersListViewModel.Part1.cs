// ═══════════════════════════════════════════════════════════════════════
// FILE: SavedJobOrdersListViewModel.Part1.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus;
using ProGlassAutomation.Models.JobOrder.Common.JobPriority;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Excel;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Documents;
using ProGlassAutomation.Services.JobOrder.Export;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.ProformaInvoice.Attachments;
using System;
using ProGlassAutomation.Utilities;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using Microsoft.Extensions.DependencyInjection;
using ProGlassAutomation.Views.JobOrder.SavedJobOrdersList.ProformaPicker;
using ProGlassAutomation.Views.JobOrder.SavedJobOrdersList;

namespace ProGlassAutomation.ViewModels.JobOrder.SavedJobOrdersList
{
        public partial class SavedJobOrdersListViewModel : ViewModelBase, IDisposable
    {
            private readonly IDialogService _dialogs = null!;
        private readonly IDatabaseServices _database = null!;
        private readonly IProformaInvoiceRepository _invoices = null!;
        private readonly IJobOrderRepository _jobs = null!;

        public SavedJobOrdersListViewModel(IDialogService? dialogs, IProformaInvoiceRepository? invoices, IJobOrderRepository? jobs, IDatabaseServices? database)
        {
            _dialogs = dialogs!;
            _invoices = invoices!;
            _jobs = jobs!;
            _database = database!;
        
            // ERR-633EEB: run shared initialization on EVERY construction
            // path. DI selects this (greediest) ctor, so without this call the
            // parameterless ctor's setup never ran and commands stayed null.
            InitializeCore();
        }

        // Phase 4: Named handler for static event — detachable
        private Func<Task>? _dataSeededHandler;
        private bool _disposed;

        // ── reentrancy guard ──────────────────────────────────────────────
        private int _loadingFlag = 0;
        private int _reloadQueued = 0;

        // ── callback wired by shell VM ────────────────────────────────────
        public Action<int>? OnJobOrderOpenRequested { get; set; }

        /// <summary>
        /// v2 §24: fired when the user restores a version from the
        /// history dialog. Payload = version row id. Shell opens the
        /// detail and applies the snapshot.
        /// </summary>
        public Action<int>? OnVersionRestoreRequested { get; set; }

        // ── data: full list + filtered display list ───────────────────────

        private List<JobOrderRowViewModel> _allJobs = new();

        private ObservableCollection<JobOrderRowViewModel> _displayedJobs = new();
        public ObservableCollection<JobOrderRowViewModel> DisplayedJobs
        {
            get => _displayedJobs;
            private set
            {
                if (_displayedJobs == value) return;
                _displayedJobs = value;
                OnPropertyChanged();
            }
        }

        // ── KPI aggregates ────────────────────────────────────────────────

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            private set { if (_totalCount == value) return; _totalCount = value; OnPropertyChanged(); }
        }

        private int _inProductionCount;
        public int InProductionCount
        {
            get => _inProductionCount;
            private set { if (_inProductionCount == value) return; _inProductionCount = value; OnPropertyChanged(); }
        }

        private int _completedCount;
        public int CompletedCount
        {
            get => _completedCount;
            private set { if (_completedCount == value) return; _completedCount = value; OnPropertyChanged(); }
        }

        private int _overdueCount;
        public int OverdueCount
        {
            get => _overdueCount;
            private set { if (_overdueCount == value) return; _overdueCount = value; OnPropertyChanged(); }
        }

        // ── loading + error state ─────────────────────────────────────────

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { if (_isLoading == value) return; _isLoading = value; OnPropertyChanged(); }
        }

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            private set { if (_hasError == value) return; _hasError = value; OnPropertyChanged(); }
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            private set { if (_errorMessage == value) return; _errorMessage = value; OnPropertyChanged(); }
        }

        // ── filter + search + sort ────────────────────────────────────────

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public List<string> StatusFilterOptions { get; } = new()
        {
            "All Statuses", "Not delivered", "Partially delivered", "Fully delivered"
        };

        private string _selectedStatusFilter = "All Statuses";
        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set
            {
                if (_selectedStatusFilter == value) return;
                _selectedStatusFilter = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        // ERP lifecycle hardening (Phase 1): voided job orders are hidden by
        // default; flip this to include them.
        private bool _showVoided;
        public bool ShowVoided
        {
            get => _showVoided;
            set
            {
                if (_showVoided == value) return;
                _showVoided = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public List<string> PriorityFilterOptions { get; } = new()
        {
            "All Priorities", "High", "Medium", "Low"
        };

        private string _selectedPriorityFilter = "All Priorities";
        public string SelectedPriorityFilter
        {
            get => _selectedPriorityFilter;
            set
            {
                if (_selectedPriorityFilter == value) return;
                _selectedPriorityFilter = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public List<string> SortOptions { get; } = new()
        {
            "Newest first",
            "Oldest first",
            "Delivery date (soonest)",
            "Delivery date (latest)",
            "Priority (high first)",
            "Priority (low first)",
            "Grand total (highest)",
            "Grand total (lowest)"
        };

        private string _selectedSort = "Newest first";
        public string SelectedSort
        {
            get => _selectedSort;
            set
            {
                if (_selectedSort == value) return;
                _selectedSort = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        // ── commands ──────────────────────────────────────────────────────

        public ICommand OpenJobOrderCommand { get; private set; }
        public ICommand DeleteJobOrderCommand { get; private set; }
        public ICommand ChangeStatusCommand { get; private set; }
        public ICommand ChangePriorityCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }
        public ICommand ClearFiltersCommand { get; private set; }

        /// <summary>v2 §24: shows the version history dialog for a row.</summary>
        public ICommand ShowVersionsCommand { get; private set; }

        /// <summary>
        /// Raises a new Job Order from an approved Proforma Invoice.
        /// <para>
        /// The module had no create entry point at all — job orders could
        /// only appear via sample data or code paths outside the UI, even
        /// though JobOrderMapper.BuildFromProformaInvoice already existed.
        /// </para>
        /// </summary>
        public ICommand CreateJobOrderCommand { get; private set; }

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public SavedJobOrdersListViewModel()
         : this(
            App.ServiceProvider?.GetService<IDialogService>()?? new DialogService(),
            App.ServiceProvider?.GetService<IProformaInvoiceRepository>()!,
            App.ServiceProvider?.GetService<IJobOrderRepository>()!,
            App.ServiceProvider?.GetService<IDatabaseServices>()!){ }

        // ── Shared initialization (ERR-633EEB) ─────────────────────
        // Extracted verbatim from the parameterless ctor so both it
        // and the DI-selected parameterized ctor initialize fully.
        private void InitializeCore()
        {
            OpenJobOrderCommand = new RelayCommand(param =>
            {
                if (param is JobOrderRowViewModel row)
                    OnJobOrderOpenRequested?.Invoke(row.Id);
            });

            DeleteJobOrderCommand = new RelayCommand(async param =>
            {
                if (param is JobOrderRowViewModel row)
                    await DeleteJobOrderAsync(row);
            });

            // Mirrors the Proforma Invoice module's BUG-72 fix: the list's
            // code-behind context menu passes Tuple.Create(row, status) as the
            // command parameter, so accept both Tuple and ValueTuple shapes.
            ChangeStatusCommand = new RelayCommand(async param =>
            {
                switch (param)
                {
                    case Tuple<JobOrderRowViewModel, string> tuple
                        when tuple.Item1 != null && !string.IsNullOrWhiteSpace(tuple.Item2):
                        await ChangeStatusAsync(tuple.Item1, tuple.Item2);
                        break;
                    case ValueTuple<JobOrderRowViewModel, string> vtuple
                        when vtuple.Item1 != null && !string.IsNullOrWhiteSpace(vtuple.Item2):
                        await ChangeStatusAsync(vtuple.Item1, vtuple.Item2);
                        break;
                }
            });

            ChangePriorityCommand = new RelayCommand(async param =>
            {
                switch (param)
                {
                    case Tuple<JobOrderRowViewModel, string> tuple
                        when tuple.Item1 != null && !string.IsNullOrWhiteSpace(tuple.Item2):
                        await ChangePriorityAsync(tuple.Item1, tuple.Item2);
                        break;
                    case ValueTuple<JobOrderRowViewModel, string> vtuple
                        when vtuple.Item1 != null && !string.IsNullOrWhiteSpace(vtuple.Item2):
                        await ChangePriorityAsync(vtuple.Item1, vtuple.Item2);
                        break;
                }
            });

            RefreshCommand = new RelayCommand(async _ => await LoadJobOrdersAsync());

            // Instant refresh when sample data is seeded.
                        _dataSeededHandler = async () => { try { await LoadJobOrdersAsync(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ExceptionMessages.Deep(ex)}"); } };
            ProGlassAutomation.Services.SampleData.SampleDataService.DataSeeded += _dataSeededHandler;

            // A DN / TI delete in another module must refresh this list
            // immediately so derived delivery status matches remaining docs.
            _database.SubscribeDocumentsChanged(LoadJobOrdersAsync);

            // v2 §24: version history dialog (shared with Proforma).
            ShowVersionsCommand = new RelayCommand(
                async param => await ShowVersionsAsync(
                    param as JobOrderRowViewModel));
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());

            CreateJobOrderCommand = new RelayCommand(
                async _ => await CreateJobOrderAsync());

            // Load on construction
            _ = LoadJobOrdersAsync();
        }

        // ═════════════════════════════════════════════════════════════════
        //  LOAD
        // ═════════════════════════════════════════════════════════════════

        public async Task LoadJobOrdersAsync()
        {
            if (Interlocked.CompareExchange(ref _loadingFlag, 1, 0) != 0)
            {
                Interlocked.Exchange(ref _reloadQueued, 1);
                return;
            }

            try
            {
                IsLoading = true;
                HasError = false;
                ErrorMessage = string.Empty;

                if (_jobs == null)
                {
                    HasError = true;
                    ErrorMessage = "Job Order database not initialised.";
                    return;
                }

                // PERF (Aug 2026): use the narrow list projection so we never
                // transfer each row's FullSnapshotJson blob just to build the
                // list columns. JobOrderRowViewModel takes JobOrderListRow.
                var rows = await _jobs.GetListRowsAsync();

                // Master ordered + confirmed delivered (includes fully
                // delivered jobs — pending-qty map used to drop those).
                var deliveryMap = await LoadDeliveryQtyMapAsync(rows);

                _allJobs = rows
                    .Select(e =>
                    {
                        deliveryMap.TryGetValue(
                            e.JobOrderId ?? "", out var dq);
                        return new JobOrderRowViewModel(
                            e, dq.Ordered, dq.Delivered);
                    })
                    .ToList();

                RecalculateKpis();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Failed to load job orders: {ExceptionMessages.Deep(ex)}";
            }
            finally
            {
                IsLoading = false;
                Interlocked.Exchange(ref _loadingFlag, 0);
                if (Interlocked.CompareExchange(ref _reloadQueued, 0, 1) == 1)
                    _ = LoadJobOrdersAsync();
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  KPI + FILTER
        // ═════════════════════════════════════════════════════════════════

        private void RecalculateKpis()
        {
            TotalCount = _allJobs.Count;
            InProductionCount = _allJobs.Count(j => Eq(j.StatusRaw, "InProduction"));
            CompletedCount = _allJobs.Count(j => Eq(j.StatusRaw, "Completed"));

            var today = DateTime.Today;
            OverdueCount = _allJobs.Count(j =>
                j.DeliveryDate.HasValue &&
                j.DeliveryDate.Value.Date < today &&
                !Eq(j.StatusRaw, "Completed") &&
                !Eq(j.StatusRaw, "Delivered"));
        }

        private void ApplyFilters()
        {
            IEnumerable<JobOrderRowViewModel> query = _allJobs;

            // ERP lifecycle hardening (Phase 1): hide voided job orders unless
            // the user explicitly asks to see them.
            if (!_showVoided)
                query = query.Where(j => !j.IsVoid);

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                string q = _searchText.Trim().ToLowerInvariant();
                query = query.Where(j =>
                    (j.JobOrderId?.ToLowerInvariant().Contains(q) ?? false) ||
                    (j.CustomerName?.ToLowerInvariant().Contains(q) ?? false) ||
                    (j.ProjectName?.ToLowerInvariant().Contains(q) ?? false) ||
                    (j.SourceProformaId?.ToLowerInvariant().Contains(q) ?? false));
            }

            if (_selectedStatusFilter != "All Statuses")
            {
                string sel = _selectedStatusFilter;
                query = query.Where(j =>
                    string.Equals(j.DisplayStatus, sel,
                        StringComparison.OrdinalIgnoreCase));
            }

            if (_selectedPriorityFilter != "All Priorities")
            {
                query = query.Where(j => Eq(j.PriorityRaw, _selectedPriorityFilter));
            }

            query = _selectedSort switch
            {
                "Newest first" => query.OrderByDescending(j => j.UpdatedAt),
                "Oldest first" => query.OrderBy(j => j.UpdatedAt),
            "Delivery date (soonest)" => query.OrderBy(j => j.DeliveryDate ?? DateTime.MaxValue),
                "Delivery date (latest)" => query.OrderByDescending(j => j.DeliveryDate ?? DateTime.MinValue),
                "Priority (high first)" => query.OrderByDescending(j => PriorityRank(j.PriorityRaw)),
                "Priority (low first)" => query.OrderBy(j => PriorityRank(j.PriorityRaw)),
                "Grand total (highest)" => query.OrderByDescending(j => j.GrandTotal),
                "Grand total (lowest)" => query.OrderBy(j => j.GrandTotal),
                _ => query.OrderByDescending(j => j.UpdatedAt)
            };

            DisplayedJobs = new ObservableCollection<JobOrderRowViewModel>(query);
        }

        private void ClearFilters()
        {
            _searchText = string.Empty;
            _selectedStatusFilter = "All Statuses";
            _selectedPriorityFilter = "All Priorities";
            _selectedSort = "Newest first";

            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SelectedStatusFilter));
            OnPropertyChanged(nameof(SelectedPriorityFilter));
            OnPropertyChanged(nameof(SelectedSort));

            ApplyFilters();
        }

        // ═════════════════════════════════════════════════════════════════
        //  MUTATIONS
        // ═════════════════════════════════════════════════════════════════

        // ═════════════════════════════════════════════════════════════
        //  v2 §24 — VERSION HISTORY
        // ═════════════════════════════════════════════════════════════

        /// <summary>
        /// Shows the shared version-history dialog for one job order.
        /// A chosen restore is handed to the shell via
        /// OnVersionRestoreRequested (opens detail + applies snapshot).
        /// </summary>
        private async Task ShowVersionsAsync(JobOrderRowViewModel? row)
        {
            if (row == null) return;

            var versions = await VersionHistoryService
                .GetVersionsAsync("JO", row.JobOrderId);

            var restoreId = Views.ProformaInvoice.SavedInvoicesList
                .VersionsDialog.Show(
                    Application.Current.MainWindow,
                    row.JobOrderId, versions);

            if (restoreId == null) return;
            OnVersionRestoreRequested?.Invoke(restoreId.Value);
        }

        private async Task DeleteJobOrderAsync(JobOrderRowViewModel row)
        {

            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.JobOrderDelete, "delete a job order"))
                return;

            // Cross-database dependency check (real Delivery Notes / Tax
            // Invoices under this job order). ERP hardening (Phase 3, G-4): a
            // posted job order (not hard-deletable) uses STRICT mode so a
            // failed verification fails closed rather than orphan its children.
            bool strict = !Services.Workflow.DocumentLifecyclePolicy.CanHardDelete(
                Services.Workflow.StatusTransitions.Kind.JobOrder, row.StatusRaw, hasChildren: false);
            var guard = await _database.CheckDependencyAsync(
                DependencyGuard.Kind.JobOrder,
                row.JobOrderId, strict);
            bool hasChildren = guard.HasDependents;

            // ERP lifecycle hardening (Phase 1): three-way decision.
            var decision = Services.Workflow.DocumentLifecyclePolicy.DecideDelete(
                Services.Workflow.StatusTransitions.Kind.JobOrder, row.StatusRaw, hasChildren);

            if (decision == Services.Workflow.DocumentLifecyclePolicy.DeleteDecision.Blocked)
            {
                _dialogs.Show(guard.Message, LocalizationService.Instance.T("Msg.CapCannotDelete"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (decision == Services.Workflow.DocumentLifecyclePolicy.DeleteDecision.Void)
            {
                await VoidJobOrderAsync(row);
                return;
            }

            var result = _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Msg.DeleteJobOrderXCustomer"), row.JobOrderId, row.CustomerName),
                LocalizationService.Instance.T("Msg.CapConfirmDelete"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            // ── Related-attachments gate ─────────────────────────────────
            // If the source proforma invoice still has Step 8 attachments,
            // ask the user explicitly: delete them together with the job
            // order, or keep them. Both choices proceed with the delete.
            bool deleteAttachments = false;
            string? proformaId =
                string.IsNullOrWhiteSpace(row.SourceProformaId)
                    ? null
                    : row.SourceProformaId;

            if (proformaId != null)
            {
                try
                {
                    var attachments = await new ProformaAttachmentService()
                        .LoadManifestAsync(proformaId);

                    if (attachments.Count > 0)
                    {
                        var warn = _dialogs.Create<JobOrderAttachmentWarningDialog>(
                            row.JobOrderId, proformaId, attachments.Count);

                        // Both buttons resolve true; a window-close (X) keeps
                        // the default (keep attachments — never destroy
                        // documents by accident).
                        if (warn.ShowDialog() == true)
                            deleteAttachments = warn.DeleteAttachments;
                    }
                }
                catch (Exception ex)
                {
                    // A failed attachment read must never block the job
                    // order delete — log and continue with KEEP.
                    AppLogService.Warning(
                        $"Could not inspect attachments for {proformaId}: {ExceptionMessages.Deep(ex)}",
                        new LogContext
                        {
                            Module = "JobOrder",
                            Class = nameof(SavedJobOrdersListViewModel),
                            Operation = "DeleteJobOrder"
                        });
                }
            }

            try
            {
                await _jobs.DeleteAsync(row.Id);

                // Honor the user's choice only AFTER the job order is gone,
                // so a failed delete never destroys attachments.
                if (deleteAttachments && proformaId != null)
                    await ClearProformaAttachmentsAsync(proformaId);

                AuditService.Log(
                    AuditSections.JobOrder, "Job order deleted",
                    row.JobOrderId, "JO", row.JobOrderId);

                await LoadJobOrdersAsync();
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToDeleteJobOrder"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapDeleteFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// ERP lifecycle hardening (Phase 1): soft-close (void) a job order
        /// that may not be hard-deleted (posted or has dependents). Explains
        /// why, captures a mandatory reason, voids via the repository, audits.
        /// Attachments are deliberately left intact — the JO still exists.
        /// </summary>
        private async Task VoidJobOrderAsync(JobOrderRowViewModel row)
        {
            if (row == null || _jobs == null) return;

            var proceed = _dialogs.Show(
                string.Format(LocalizationService.Instance.T("Void.MsgConfirmVoidX"), row.JobOrderId),
                LocalizationService.Instance.T("Void.Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (proceed != MessageBoxResult.Yes) return;

            var dlg = _dialogs.Create<ProGlassAutomation.Views.Shared.VoidReasonDialog>(row.JobOrderId);
            if (dlg.ShowDialog() != true) return;
            string reason = dlg.ResultReason;

            try
            {
                await _jobs.VoidAsync(row.Id, reason);

                AuditService.Log(
                    AuditSections.JobOrder, "Job order voided",
                    row.JobOrderId, "JO", row.JobOrderId, reason);

                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Void.MsgVoidedX"), row.JobOrderId),
                    LocalizationService.Instance.T("Void.CapVoided"),
                    MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadJobOrdersAsync();
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Void.MsgVoidFailedX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Void.CapVoidFailed"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Best-effort removal of a proforma invoice's Step 8 attachments
        /// (files + manifest) after the user chose DELETE ATTACHMENTS in the
        /// delete-job-order gate. Failures are logged, never thrown.
        /// </summary>
        private async Task ClearProformaAttachmentsAsync(string proformaId)
        {
            try
            {
                int cleared = await new ProformaAttachmentService()
                    .ClearAllAsync(proformaId);

                AuditService.Log(
                    AuditSections.Proforma, "Attachments deleted with job order",
                    $"{proformaId} — {cleared} Step 8 attachment(s) removed " +
                    "when its job order was deleted",
                    "PI", proformaId);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Could not clear attachments for {proformaId}: {ExceptionMessages.Deep(ex)}",
                    new LogContext
                    {
                        Module = "Proforma",
                        Class = nameof(SavedJobOrdersListViewModel),
                        Operation = "DeleteJobOrder"
                    });
            }
        }

        /// <summary>
        /// Opens the Proforma picker and converts the chosen quote into a
        /// new Job Order.
        /// <para>
        /// Reuses JobOrderMapper.BuildFromProformaInvoice and
        /// BuildEntityFromSnapshot, which already existed and were already
        /// exercised by the sample-data path — this only supplies the UI
        /// entry point that was missing, so there is no second conversion
        /// implementation to keep in step.
        /// </para>
        /// </summary>
        private async Task CreateJobOrderAsync()
        {
            // Same guard the repository enforces, checked here so the user
            // is told before filling in a dialog rather than after.
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.JobOrderEdit,
                "create a job order"))
                return;

            if (_invoices == null || _jobs == null)
            {
                _dialogs.Show(
                    LocalizationService.Instance.T("Msg.DatabasesAreStillInitialisingTry"),
                    LocalizationService.Instance.T("Msg.CapReady"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var dialog = _dialogs.Create<JobOrderProformaPickerDialog>();

            if (dialog.ShowDialog() != true) return;

            string proformaId = dialog.SelectedProformaId;
            if (string.IsNullOrWhiteSpace(proformaId)) return;

            try
            {
                // Phase 2: GetByProformaIdAsync already existed (and is
                // already used by ProformaLifecycleSync) but was not used
                // here. This loaded every proforma invoice to find one by id.
                //
                // Note the repository matches with SQL `=`, which on SQLite is
                // case-sensitive, where this call site used OrdinalIgnoreCase.
                // ProformaId is system-generated with consistent casing, and
                // the existing method is already trusted elsewhere, so the
                // match is unchanged in practice.
                var saved = await _invoices.GetByProformaIdAsync(proformaId);

                if (saved == null)
                {
                    _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.ProformaInvoiceXCouldNot"), proformaId),
                        LocalizationService.Instance.T("Msg.CapCreateFailed"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // A voided quote must never be converted into a Job Order —
                // the picker now hides voided quotes, but this hard guard
                // catches every other entry point (a stale dialog, a direct
                // call) so a soft-closed PI-2026-002 can never masquerade as
                // the live PI-2026-001 it shares a base number with.
                if (saved.IsVoid)
                {
                    _dialogs.Show(
                        $"Proforma Invoice {proformaId} is void and cannot be converted to a Job Order.",
                        "Voided Proforma Invoice",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // A quote can legitimately be built twice (a revised order,
                // a split delivery), so this warns rather than blocks - but
                // it has to warn, or a double-click silently duplicates a
                // production job.
                var existing = await _jobs
                    .GetBySourceProformaIdAsync(proformaId);

                if (existing != null && existing.Count > 0)
                {
                    string ids = string.Join(", ",
                        existing.Select(j => j.JobOrderId));

                    var again = _dialogs.Show(
                        string.Format(LocalizationService.Instance.T("Msg.XAlreadyHasYJob"), proformaId, existing.Count, ids),
                        LocalizationService.Instance.T("Msg.CapAlreadyConverted"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning,
                        MessageBoxResult.No);

                    if (again != MessageBoxResult.Yes) return;
                }

                var snapshot = JobOrderMapper
                    .BuildFromProformaInvoice(saved);

                // The mapper derives JOB-YYYY-NNN from the PI number, which
                // collides as soon as a quote is converted twice. Ask the
                // repository for the next free id in that series.
                snapshot.JobOrderId = await _jobs
                    .SuggestNextIdAsync(snapshot.JobOrderId);

                var entity = JobOrderMapper.BuildEntityFromSnapshot(
                    snapshot, saved.GrandTotalVatInclusive);

                var inserted = await _jobs.InsertAsync(entity);

                try
                {
                    await ProGlassAutomation.Services.DeliveryNote
                        .MasterDeliveryNoteService
                        .EnsureForJobOrderAsync(inserted);
                }
                catch { /* first real DN will retry */ }

                AuditService.Log(
                    AuditSections.JobOrder, "Job order created",
                    $"{inserted.JobOrderId} from {proformaId}"
                    + (dialog.ProceededWithoutApproval
                        ? " — RELEASED WITHOUT CONFIRMED APPROVAL"
                        : ""),
                    "JO", inserted.JobOrderId);

                await LoadJobOrdersAsync();

                OnJobOrderOpenRequested?.Invoke(inserted.Id);
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToCreateJobOrder"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapCreateFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task ChangeStatusAsync(JobOrderRowViewModel row, string newStatus)
        {
            try
            {
                var entity = await _jobs.GetByIdAsync(row.Id);
                if (entity == null) return;

                // Phase 3: enforce the centralized status-transition rules at
                // the business boundary (not just hiding it in the UI).
                var transition = ProGlassAutomation.Services.Workflow.StatusTransitions
                    .Validate(
                        ProGlassAutomation.Services.Workflow.StatusTransitions.Kind.JobOrder,
                        entity.Status, newStatus);
                if (!transition.IsValid)
                {
                    _dialogs.Show(transition.Reason ?? "Invalid status change.",
                        LocalizationService.Instance.T("Msg.CapStatusChangeNotAllowed"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                entity.Status = newStatus;
                entity.UpdatedAt = DateTime.Now;

                // Keep the embedded snapshot's Production.Status in sync.
                // The detail view, its save path and the PDF/Excel production
                // sheet all read the status from the snapshot, so a list-side
                // change must be reflected there too — otherwise reopening
                // (and saving) the order would silently revert this change.
                var snapshot = JobOrderMapper.DeserializeSnapshot(entity);
                if (snapshot?.Production != null)
                {
                    snapshot.Production.Status = newStatus;
                    snapshot.Production.LastEditedAt = DateTime.Now;
                    snapshot.Production.LastEditedBy = Environment.UserName;
                    entity.FullSnapshotJson = JobOrderMapper.SerializeSnapshot(snapshot);
                }

                await _jobs.UpdateAsync(entity);

                AuditService.Log(
                    AuditSections.JobOrder, "Job order status changed",
                    $"{entity.JobOrderId} → {newStatus}", "JO", entity.JobOrderId);

                await LoadJobOrdersAsync();
            }
            catch (Exception ex)
            {
                _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.FailedToChangeStatusX"), ExceptionMessages.Deep(ex)),
                    LocalizationService.Instance.T("Msg.CapUpdateFailed"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
