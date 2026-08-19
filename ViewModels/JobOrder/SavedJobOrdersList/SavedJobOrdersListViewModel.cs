
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
using ProGlassAutomation.Services.JobOrder.Export;
using ProGlassAutomation.Services.JobOrder.Mapper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.JobOrder.SavedJobOrdersList
{
    public class SavedJobOrdersListViewModel : ViewModelBase
    {
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

        public ICommand OpenJobOrderCommand { get; }
        public ICommand DeleteJobOrderCommand { get; }
        public ICommand ChangeStatusCommand { get; }
        public ICommand ChangePriorityCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }

        /// <summary>v2 §24: shows the version history dialog for a row.</summary>
        public ICommand ShowVersionsCommand { get; }

        /// <summary>
        /// Raises a new Job Order from an approved Proforma Invoice.
        /// <para>
        /// The module had no create entry point at all — job orders could
        /// only appear via sample data or code paths outside the UI, even
        /// though JobOrderMapper.BuildFromProformaInvoice already existed.
        /// </para>
        /// </summary>
        public ICommand CreateJobOrderCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public SavedJobOrdersListViewModel()
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
            ProGlassAutomation.Services.SampleData.SampleDataService.DataSeeded +=
                async () => { try { await LoadJobOrdersAsync(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); } };

            // A DN / TI delete in another module must refresh this list
            // immediately so derived delivery status matches remaining docs.
            DocumentLifecycle.Subscribe(LoadJobOrdersAsync);

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

                if (App.Jobs == null)
                {
                    HasError = true;
                    ErrorMessage = "Job Order database not initialised.";
                    return;
                }

                var entities = await App.Jobs.GetAllAsync();

                // Load per-job-order delivery qty from the Delivery Notes
                // repository so the list can show the delivery-derived status.
                var deliveryMap = await LoadDeliveryQtyMapAsync();

                _allJobs = entities
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
                ErrorMessage = $"Failed to load job orders: {ex.Message}";
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

            // Refuse to delete a Job Order that still has real Delivery
            // Notes or Tax Invoices. The message lists their IDs.
            var guard = await ProGlassAutomation.Services.Database
                .DependencyGuard.CheckAsync(
                    ProGlassAutomation.Services.Database.DependencyGuard
                        .Kind.JobOrder,
                    row.JobOrderId);
            if (guard.HasDependents)
            {
                MessageBox.Show(guard.Message, "Cannot Delete",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Delete job order '{row.JobOrderId}'?\n\n" +
                $"Customer: {row.CustomerName}\n" +
                $"This action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await App.Jobs.DeleteAsync(row.Id);

                AuditService.Log(
                    AuditSections.JobOrder, "Job order deleted",
                    row.JobOrderId, "JO", row.JobOrderId);

                await LoadJobOrdersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to delete job order:\n\n{ex.Message}",
                    "Delete Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

            if (App.Invoices == null || App.Jobs == null)
            {
                MessageBox.Show(
                    "Databases are still initialising. Try again in a moment.",
                    "Not Ready",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var dialog = new ProGlassAutomation.Views.JobOrder
                .SavedJobOrdersList.ProformaPicker
                .JobOrderProformaPickerDialog();

            if (dialog.ShowDialog() != true) return;

            string proformaId = dialog.SelectedProformaId;
            if (string.IsNullOrWhiteSpace(proformaId)) return;

            try
            {
                var saved = (await App.Invoices.GetAllAsync())
                    .FirstOrDefault(p => string.Equals(
                        p.ProformaId, proformaId,
                        StringComparison.OrdinalIgnoreCase));

                if (saved == null)
                {
                    MessageBox.Show(
                        $"Proforma Invoice '{proformaId}' could not be loaded.",
                        "Create Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // A quote can legitimately be built twice (a revised order,
                // a split delivery), so this warns rather than blocks - but
                // it has to warn, or a double-click silently duplicates a
                // production job.
                var existing = await App.Jobs
                    .GetBySourceProformaIdAsync(proformaId);

                if (existing != null && existing.Count > 0)
                {
                    string ids = string.Join(", ",
                        existing.Select(j => j.JobOrderId));

                    var again = MessageBox.Show(
                        $"{proformaId} already has {existing.Count} job "
                        + $"order(s): {ids}.\n\n"
                        + "Create another one from the same quote?",
                        "Already Converted",
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
                snapshot.JobOrderId = await App.Jobs
                    .SuggestNextIdAsync(snapshot.JobOrderId);

                var entity = JobOrderMapper.BuildEntityFromSnapshot(
                    snapshot, saved.GrandTotalVatInclusive);

                var inserted = await App.Jobs.InsertAsync(entity);

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
                MessageBox.Show(
                    $"Failed to create job order:\n\n{ex.Message}",
                    "Create Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task ChangeStatusAsync(JobOrderRowViewModel row, string newStatus)
        {
            try
            {
                var entity = await App.Jobs.GetByIdAsync(row.Id);
                if (entity == null) return;

                // Phase 3: enforce the centralized status-transition rules at
                // the business boundary (not just hiding it in the UI).
                var transition = ProGlassAutomation.Services.Workflow.StatusTransitions
                    .Validate(
                        ProGlassAutomation.Services.Workflow.StatusTransitions.Kind.JobOrder,
                        entity.Status, newStatus);
                if (!transition.IsValid)
                {
                    MessageBox.Show(transition.Reason ?? "Invalid status change.",
                        "Status Change Not Allowed",
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

                await App.Jobs.UpdateAsync(entity);

                AuditService.Log(
                    AuditSections.JobOrder, "Job order status changed",
                    $"{entity.JobOrderId} → {newStatus}", "JO", entity.JobOrderId);

                await LoadJobOrdersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to change status:\n\n{ex.Message}",
                    "Update Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task ChangePriorityAsync(JobOrderRowViewModel row, string newPriority)
        {
            try
            {
                var entity = await App.Jobs.GetByIdAsync(row.Id);
                if (entity == null) return;

                entity.Priority = newPriority;
                entity.UpdatedAt = DateTime.Now;

                // Same consistency requirement as status: the production
                // sheet and detail view read priority from the snapshot.
                var snapshot = JobOrderMapper.DeserializeSnapshot(entity);
                if (snapshot?.Production != null)
                {
                    snapshot.Production.Priority = newPriority;
                    snapshot.Production.LastEditedAt = DateTime.Now;
                    snapshot.Production.LastEditedBy = Environment.UserName;
                    entity.FullSnapshotJson = JobOrderMapper.SerializeSnapshot(snapshot);
                }

                await App.Jobs.UpdateAsync(entity);

                AuditService.Log(
                    AuditSections.JobOrder, "Job order priority changed",
                    $"{entity.JobOrderId} → {newPriority}", "JO", entity.JobOrderId);

                await LoadJobOrdersAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to change priority:\n\n{ex.Message}",
                    "Update Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  EXPORT (Phase 9.5a-r3 — called from right-click menu)
        // ═════════════════════════════════════════════════════════════════
        /// <summary>
        /// Loads the full entity + snapshot for the given row and exports
        /// a PDF via the shared export engine. Wired to the "Export PDF"
        /// menu item in SavedJobOrdersListView.xaml.cs OnRowRightClick.
        /// </summary>
        public async Task ExportPdfAsync(JobOrderRowViewModel row)
        {
            var (entity, snapshot) = await LoadEntityAndSnapshotAsync(row);
            if (entity == null || snapshot == null) return;

            var layout = new JobOrderPdfLayout(entity, snapshot);
            var engine = new PdfExportEngine();
            var request = new ExportRequest
            {
                PromptForLocation = true,
                OpenAfterSave = true
            };

            var result = await engine.ExportAsync(layout, request);
            ShowExportResult(result, "PDF");
        }

        /// <summary>
        /// Loads the full entity + snapshot for the given row and exports
        /// an Excel workbook via the shared export engine. Wired to the
        /// "Export Excel" menu item in SavedJobOrdersListView.xaml.cs.
        /// </summary>
        public async Task ExportExcelAsync(JobOrderRowViewModel row)
        {
            var (entity, snapshot) = await LoadEntityAndSnapshotAsync(row);
            if (entity == null || snapshot == null) return;

            var layout = new JobOrderExcelLayout(entity, snapshot);
            var engine = new ExcelExportEngine();
            var request = new ExportRequest
            {
                PromptForLocation = true,
                OpenAfterSave = true
            };

            var result = await engine.ExportAsync(layout, request);
            ShowExportResult(result, "Excel");
        }

        private async Task<(SavedJobOrder? entity, Models.JobOrder.SavedJobOrder.JobOrderSnapshot? snapshot)>
            LoadEntityAndSnapshotAsync(JobOrderRowViewModel row)
        {
            if (row == null) return (null, null);

            try
            {
                var entity = await App.Jobs.GetByIdAsync(row.Id);
                if (entity == null)
                {
                    MessageBox.Show(
                        $"Job order '{row.JobOrderId}' no longer exists.",
                        "Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return (null, null);
                }

                var snapshot = JobOrderMapper.DeserializeSnapshot(entity);
                if (snapshot == null)
                {
                    MessageBox.Show(
                        $"Job order '{row.JobOrderId}' has corrupt snapshot data.",
                        "Corrupt Data",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return (entity, null);
                }

                return (entity, snapshot);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load job order:\n\n{ex.Message}",
                    "Load Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return (null, null);
            }
        }

        private static void ShowExportResult(ExportResult result, string formatLabel)
        {
            if (result.Success || result.Cancelled)
                return;

            MessageBox.Show(
                $"{formatLabel} export failed:\n\n{result.ErrorMessage}",
                "Export Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        // ═════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds a { JobOrderId → (ordered, delivered) } map from the Delivery
        /// Notes repository. Never throws — if DN data is unavailable the rows
        /// simply show "Not delivered".
        /// </summary>
        private async Task<Dictionary<string, (int Ordered, int Delivered)>>
            LoadDeliveryQtyMapAsync()
        {
            var map = new Dictionary<string, (int, int)>(
                StringComparer.OrdinalIgnoreCase);
            try
            {
                if (App.DeliveryNotes == null) return map;

                var summaries = await App.DeliveryNotes.GetPendingQtyJobsAsync();
                if (summaries == null) return map;

                foreach (var s in summaries)
                {
                    if (string.IsNullOrWhiteSpace(s.JobOrderId)) continue;
                    map[s.JobOrderId] = (s.TotalOrdered, s.TotalDelivered);
                }
            }
            catch
            {
                // DN unavailable — rows show "Not delivered".
            }
            return map;
        }

        private static bool Eq(string? a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static int PriorityRank(string? priority) =>
            priority?.ToLowerInvariant() switch
            {
                "high" => 3,
                "medium" => 2,
                "low" => 1,
                _ => 0
            };
    }
}
