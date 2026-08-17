using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Csv;
using ProGlassAutomation.Services.Export.Excel;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.ProformaInvoice.Attachments;
using ProGlassAutomation.Services.ProformaInvoice.Export;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.SavedInvoicesList
{
    /// <summary>
    /// ViewModel for the Saved Invoices list page.
    ///
    /// BUG-72 [HIGH]: ChangeStatusCommand accepts Tuple parameter matching
    ///                what the view code-behind actually passes.
    /// BUG-73 [HIGH]: Filter changes no longer trigger LoadInvoicesAsync().
    ///                Filtering is 100% in-memory over _allItems.
    /// BUG-74 [MEDIUM]: ApplyFilters uses standard clear+refill pattern.
    /// BUG-75 [MEDIUM]: Export success shows LastExportStatus feedback.
    /// BUG-76 [LOW]: Added ExportCsvFromListCommand for detail view parity.
    /// </summary>
    public class SavedInvoicesListViewModel : ViewModelBase
    {
        public string Title { get; } = "SAVED PROFORMA INVOICES";
        public string Subtitle { get; } = "Browse, load, duplicate, or delete previously saved invoices.";

        public Action<int> OnInvoiceLoadRequested { get; set; }

        /// <summary>
        /// Fired when approving and the invoice has no Step 8 attachments:
        /// the shell opens that invoice in the wizard AT Step 8 (Attachments)
        /// so the user can upload the signed PI or LPO (jpg/pdf) before the
        /// approval is allowed.
        /// </summary>
        public Action<int> OnInvoiceUploadRequested { get; set; }

        private List<SavedInvoiceListItem> _allItems = new();
        public ObservableCollection<SavedInvoiceListItem> FilteredItems { get; } = new();

        public List<string> StatusFilterOptions { get; } = new()
        {
            "All Statuses", "Draft", "Sent", "Approved", "Rejected", "Expired"
        };

        public List<string> SortPresetOptions { get; } = new()
        {
            "Newest First", "Oldest First", "Highest Value", "Lowest Value",
            "Company A-Z", "Company Z-A"
        };

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { if (_searchText == value) return; _searchText = value; OnPropertyChanged(); ApplyFilters(); }
        }

        private string _selectedStatusFilter = "All Statuses";
        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set { if (_selectedStatusFilter == value) return; _selectedStatusFilter = value; OnPropertyChanged(); ApplyFilters(); }
        }

        private DateTime? _fromDate;
        public DateTime? FromDate
        {
            get => _fromDate;
            set { if (_fromDate == value) return; _fromDate = value; OnPropertyChanged(); ApplyFilters(); }
        }

        private DateTime? _toDate;
        public DateTime? ToDate
        {
            get => _toDate;
            set { if (_toDate == value) return; _toDate = value; OnPropertyChanged(); ApplyFilters(); }
        }

        private string _selectedSortPreset = "Newest First";
        public string SelectedSortPreset
        {
            get => _selectedSortPreset;
            set { if (_selectedSortPreset == value) return; _selectedSortPreset = value; OnPropertyChanged(); ApplyFilters(); }
        }

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            private set { _totalCount = value; OnPropertyChanged(); }
        }

        private int _filteredCount;
        public int FilteredCount
        {
            get => _filteredCount;
            private set { _filteredCount = value; OnPropertyChanged(); }
        }

        private decimal _totalValueSum;
        public decimal TotalValueSum
        {
            get => _totalValueSum;
            private set { _totalValueSum = value; OnPropertyChanged(); }
        }

        private SavedInvoiceListItem _selectedItem;
        public SavedInvoiceListItem SelectedItem
        {
            get => _selectedItem;
            set { if (_selectedItem == value) return; _selectedItem = value; OnPropertyChanged(); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(); }
        }

        // BUG-75: user feedback for export operations
        private string _lastExportStatus = "";
        public string LastExportStatus
        {
            get => _lastExportStatus;
            private set
            {
                if (_lastExportStatus == value) return;
                _lastExportStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasExportStatus));
            }
        }
        public bool HasExportStatus => !string.IsNullOrEmpty(_lastExportStatus);

        // ── Commands ─────────────────────────────────────────────────────────

        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand LoadInvoiceCommand { get; }
        public ICommand DuplicateInvoiceCommand { get; }
        public ICommand DeleteInvoiceCommand { get; }
        public ICommand ChangeStatusCommand { get; }
        public ICommand ExportPdfFromListCommand { get; }
        public ICommand ExportExcelFromListCommand { get; }
        public ICommand ExportCsvFromListCommand { get; }   // BUG-76

        // v2: quick "today" filter + share the filtered list as Excel
        public ICommand SetTodayFilterCommand { get; }
        public ICommand ExportFilteredExcelCommand { get; }

        // v2: version history (§24)
        public ICommand ShowVersionsCommand { get; }

        /// <summary>
        /// v2: fired when the user restores an old version — payload is
        /// the version's snapshot JSON. The wizard shell opens it.
        /// </summary>
        public Action<string>? OnSnapshotRestoreRequested { get; set; }

        public SavedInvoicesListViewModel()
        {
            RefreshCommand = new RelayCommand(async _ => await LoadInvoicesAsync());

            // Instant refresh when sample data is seeded.
            ProGlassAutomation.Services.SampleData.SampleDataService.DataSeeded +=
                async () => { try { await LoadInvoicesAsync(); } catch { } };
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
            LoadInvoiceCommand = new RelayCommand(p => RequestLoad(p as SavedInvoiceListItem));

            DuplicateInvoiceCommand = new RelayCommand(
                async p => await DuplicateAsync(p as SavedInvoiceListItem));
            DeleteInvoiceCommand = new RelayCommand(
                async p => await DeleteAsync(p as SavedInvoiceListItem));

            // BUG-72 [HIGH]: accept Tuple parameter matching code-behind
            ChangeStatusCommand = new RelayCommand(async p =>
            {
                switch (p)
                {
                    // View code-behind CreateStatusItem returns MenuItem
                    // that fires an event capturing the tuple pattern:
                    case Tuple<SavedInvoiceListItem, string> tuple
                        when tuple.Item1 != null && !string.IsNullOrWhiteSpace(tuple.Item2):
                        await ChangeStatusAsync(tuple.Item1, tuple.Item2);
                        break;

                    case ValueTuple<SavedInvoiceListItem, string> vtuple
                        when vtuple.Item1 != null && !string.IsNullOrWhiteSpace(vtuple.Item2):
                        await ChangeStatusAsync(vtuple.Item1, vtuple.Item2);
                        break;

                    // Backward-compat: original object[] shape
                    case object[] arr when arr.Length == 2
                        && arr[0] is SavedInvoiceListItem item
                        && arr[1] is string newStatus:
                        await ChangeStatusAsync(item, newStatus);
                        break;
                }
            });

            ExportPdfFromListCommand = new RelayCommand(
                async p => await ExportPdfFromListAsync(p as SavedInvoiceListItem));
            ExportExcelFromListCommand = new RelayCommand(
                async p => await ExportExcelFromListAsync(p as SavedInvoiceListItem));

            // BUG-76: CSV export from list for parity with detail view
            ExportCsvFromListCommand = new RelayCommand(
                async p => await ExportCsvFromListAsync(p as SavedInvoiceListItem));

            // v2: one tap → show today's invoices (FromDate/ToDate both today)
            SetTodayFilterCommand = new RelayCommand(_ =>
            {
                SelectedStatusFilter = "All Statuses";
                FromDate = DateTime.Today;
                ToDate = DateTime.Today;
            });

            // v2: version history per invoice
            ShowVersionsCommand = new RelayCommand(
                async p => await ShowVersionsAsync(p as SavedInvoiceListItem));

            // v2: share the CURRENT FILTERED list as one styled Excel table
            ExportFilteredExcelCommand = new RelayCommand(
                async _ => await ExportFilteredExcelAsync());

            _ = LoadInvoicesAsync();
        }

        /// <summary>
        /// v2: version history dialog. Restore hands the snapshot JSON
        /// to the wizard shell via OnSnapshotRestoreRequested.
        /// </summary>
        private async Task ShowVersionsAsync(SavedInvoiceListItem? item)
        {
            if (item == null) return;

            var versions = await VersionHistoryService
                .GetVersionsAsync("PI", item.ProformaId);

            var restoreId = Views.ProformaInvoice.SavedInvoicesList
                .VersionsDialog.Show(
                    Application.Current.MainWindow,
                    item.ProformaId, versions);

            if (restoreId == null) return;

            var full = await VersionHistoryService
                .GetByIdAsync(restoreId.Value);

            if (full == null || string.IsNullOrEmpty(full.SnapshotJson))
                return;

            OnSnapshotRestoreRequested?.Invoke(full.SnapshotJson);

            AuditService.Log(
                AuditSections.Proforma, "Version restored",
                $"{item.ProformaId} → version {full.VersionNo} " +
                $"(by {full.Username})",
                "PI", item.ProformaId);
        }

        /// <summary>
        /// v2: exports whatever the filter currently shows (e.g. today's
        /// prepared invoices) as a single Excel register using the shared
        /// export engine + WorkbookStyle.
        /// </summary>
        private async Task ExportFilteredExcelAsync()
        {
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.ProformaExport, "export the invoice register"))
                return;

            try
            {
                var items = FilteredItems.ToList();

                if (items.Count == 0)
                {
                    MessageBox.Show(
                        "Nothing to export — the current filter shows no invoices.",
                        "Export Excel",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                bool isToday = FromDate == DateTime.Today &&
                               ToDate == DateTime.Today;

                string title = isToday
                    ? $"PROFORMA INVOICES PREPARED {DateTime.Today:dd MMM yyyy}"
                    : "PROFORMA INVOICE REGISTER";

                var layout = new ProformaInvoiceListExcelLayout(items, title);
                var engine = new ExcelExportEngine();

                var result = await engine.ExportAsync(layout,
                    new ExportRequest
                    {
                        PromptForLocation = true,
                        OpenAfterSave = true
                    });

                LastExportStatus = result.Success
                    ? $"✓ Exported {items.Count} invoices to Excel"
                    : result.Cancelled ? "" : "✗ Excel export failed";

                if (result.Success)
                    ProGlassAutomation.Services.Audit.AuditService.Log(
                        ProGlassAutomation.Services.Audit.AuditSections.Proforma,
                        "Invoice list exported",
                        $"{items.Count} invoices → Excel ({title})",
                        "PI", "REGISTER");
            }
            catch (Exception ex)
            {
                LastExportStatus = $"✗ Excel export failed: {ex.Message}";
                MessageBox.Show(
                    $"Excel export failed: {ex.Message}",
                    "Export Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ── Data loading ──────────────────────────────────────────────────────

        /// <summary>
        /// BUG-73 [HIGH]: Only called on explicit Refresh, initial load, and
        /// after mutations (delete/duplicate/status change). Filter changes
        /// no longer trigger this — filtering is now in-memory only.
        /// </summary>
        public async Task LoadInvoicesAsync()
        {
            if (App.Invoices == null) return;
            IsLoading = true;

            try
            {
                var entities = await App.Invoices.GetAllAsync();

                _allItems = entities.Select(e => new SavedInvoiceListItem
                {
                    Id = e.Id,
                    ProformaId = e.ProformaId,
                    CompanyName = e.CompanyName,
                    CustomerName = e.CustomerName,
                    ProjectName = e.ProjectName,
                    Status = e.Status,
                    GrandTotal = e.GrandTotalVatInclusive,
                    CreatedAt = e.CreatedAt,
                    UpdatedAt = e.UpdatedAt
                }).ToList();

                TotalCount = _allItems.Count;
                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load saved invoices:\n\n{ex.Message}",
                    "Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// BUG-73/74: Pure in-memory filter over _allItems.
        /// Uses standard Clear() + AddRange pattern which is what WPF
        /// virtualization expects. Incremental diff would be premature
        /// optimization at typical invoice counts (dozens to low hundreds).
        /// </summary>
        private void ApplyFilters()
        {
            IEnumerable<SavedInvoiceListItem> query = _allItems;

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                string s = _searchText.Trim().ToLowerInvariant();
                query = query.Where(x =>
                    (x.ProformaId ?? "").ToLowerInvariant().Contains(s)
                    || (x.CompanyName ?? "").ToLowerInvariant().Contains(s)
                    || (x.CustomerName ?? "").ToLowerInvariant().Contains(s)
                    || (x.ProjectName ?? "").ToLowerInvariant().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(_selectedStatusFilter)
                && _selectedStatusFilter != "All Statuses")
                query = query.Where(x => string.Equals(
                    x.Status, _selectedStatusFilter,
                    StringComparison.OrdinalIgnoreCase));

            if (_fromDate.HasValue)
                query = query.Where(x => x.CreatedAt.Date >= _fromDate.Value.Date);
            if (_toDate.HasValue)
                query = query.Where(x => x.CreatedAt.Date <= _toDate.Value.Date);

            query = _selectedSortPreset switch
            {
                "Oldest First" => query.OrderBy(x => x.CreatedAt),
                "Highest Value" => query.OrderByDescending(x => x.GrandTotal),
                "Lowest Value" => query.OrderBy(x => x.GrandTotal),
                "Company A-Z" => query.OrderBy(x => x.CompanyName),
                "Company Z-A" => query.OrderByDescending(x => x.CompanyName),
                _ => query.OrderByDescending(x => x.CreatedAt)
            };

            var result = query.ToList();

            // BUG-74: standard clear + refill pattern
            FilteredItems.Clear();
            foreach (var item in result) FilteredItems.Add(item);

            FilteredCount = result.Count;
            TotalValueSum = result.Sum(x => x.GrandTotal);
        }

        private void ClearFilters()
        {
            _searchText = "";
            _selectedStatusFilter = "All Statuses";
            _fromDate = null;
            _toDate = null;
            _selectedSortPreset = "Newest First";

            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SelectedStatusFilter));
            OnPropertyChanged(nameof(FromDate));
            OnPropertyChanged(nameof(ToDate));
            OnPropertyChanged(nameof(SelectedSortPreset));

            ApplyFilters();
        }

        // ── Actions ───────────────────────────────────────────────────────────

        private void RequestLoad(SavedInvoiceListItem item)
        {
            if (item == null) return;
            OnInvoiceLoadRequested?.Invoke(item.Id);
        }

        private async Task DuplicateAsync(SavedInvoiceListItem item)
        {
            // A-1 (UI layer): tell the user BEFORE the confirm
            // dialog. The repository guard is the real boundary.
            if (!PermissionService.Require(
                    AppPermissions.ProformaCreate, "duplicate a proforma invoice"))
                return;

            if (item == null || App.Invoices == null) return;

            var confirm = MessageBox.Show(
                $"Duplicate invoice '{item.ProformaId}'?\n\n" +
                "A new copy will be created with an auto-incremented ID.",
                "Confirm Duplicate",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var original = await App.Invoices.GetByIdAsync(item.Id);
                if (original == null) return;

                string newId = await App.Invoices.SuggestNextIdAsync(
                    original.ProformaId);

                var copy = new SavedProformaInvoice
                {
                    ProformaId = newId,
                    CompanyName = original.CompanyName,
                    CustomerName = original.CustomerName,
                    ProjectName = original.ProjectName,
                    Status = "Draft",
                    IssueDate = DateTime.Now,
                    ValidUntil = DateTime.Now.AddDays(7),
                    GlassMaterialsSubtotal = original.GlassMaterialsSubtotal,
                    OtherChargesSubtotal = original.OtherChargesSubtotal,
                    DiscountAmount = original.DiscountAmount,
                    VatAmount = original.VatAmount,
                    GrandTotalVatInclusive = original.GrandTotalVatInclusive,
                    FullSnapshotJson = original.FullSnapshotJson
                };

                await App.Invoices.InsertAsync(copy);
                await LoadInvoicesAsync();

                MessageBox.Show(
                    $"Duplicated as '{newId}'.",
                    "Duplicate Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to duplicate: {ex.Message}",
                    "Duplicate Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task DeleteAsync(SavedInvoiceListItem item)
        {
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.ProformaDelete, "delete an invoice"))
                return;

            if (item == null || App.Invoices == null) return;

            var confirm = MessageBox.Show(
                $"Delete invoice '{item.ProformaId}' permanently?\n\n" +
                "This cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                await App.Invoices.DeleteAsync(item.Id);

                AuditService.Log(
                    AuditSections.Proforma, "Invoice deleted",
                    item.ProformaId, "PI", item.ProformaId);

                await LoadInvoicesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to delete: {ex.Message}",
                    "Delete Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task ChangeStatusAsync(
            SavedInvoiceListItem item, string newStatus)
        {
            if (item == null
                || string.IsNullOrWhiteSpace(newStatus)
                || App.Invoices == null) return;

            try
            {
                var entity = await App.Invoices.GetByIdAsync(item.Id);
                if (entity == null) return;

                // Already approved — nothing to do (approval also converts once).
                if (string.Equals(newStatus, "Approved", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(entity.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                {
                    await LoadInvoicesAsync();
                    return;
                }

                // Approving is gated: the customer must have signed the PI OR
                // provided an LPO. On confirmation the invoice is approved and
                // automatically converted to a Job Order.
                if (string.Equals(newStatus, "Approved", StringComparison.OrdinalIgnoreCase))
                {
                    await ApproveAsync(entity, item);
                    return;   // ApproveAsync reloads the list
                }

                entity.Status = newStatus;
                await App.Invoices.UpdateAsync(entity);
                await LoadInvoicesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to change status: {ex.Message}",
                    "Status Change Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Approve gate, grounded in Step 8 ATTACHMENTS:
        ///   • An invoice with NO attachments cannot be approved — the user
        ///     must first attach the signed Proforma Invoice or the LPO
        ///     document in Step 8. This is a real check, not a self-confirmation.
        ///   • With attachments present, the user picks how it was confirmed:
        ///     "Signed PI" (no LPO needed) or "LPO" (LPO number required and
        ///     saved into the invoice snapshot for invoices / statement gen).
        ///   • On confirmation the invoice is Approved and auto-converted to a
        ///     Job Order.
        /// </summary>
        private async Task ApproveAsync(
            SavedProformaInvoice entity, SavedInvoiceListItem item)
        {
            if (entity == null || App.Invoices == null) return;

            // Step 8 gate — must have real attachments (signed PI or LPO).
            // If none, take the user to Step 8 to upload instead of approving.
            var attachments = await new ProformaAttachmentService()
                .LoadManifestAsync(entity.ProformaId ?? "");
            if (attachments == null || attachments.Count == 0)
            {
                var go = MessageBox.Show(
                    $"Invoice '{entity.ProformaId}' has no Step 8 attachments.\n\n" +
                    "To approve it you must upload the signed Proforma Invoice " +
                    "or the LPO document (jpg / pdf) in Step 8.\n\n" +
                    "Go to Step 8 (Attachments) now?",
                    "Approval Needs Attachments",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes);

                if (go == MessageBoxResult.Yes)
                {
                    // Load this invoice into the wizard and jump to Step 8 so
                    // the user can upload right away.
                    OnInvoiceUploadRequested?.Invoke(entity.Id);
                }
                return;   // status unchanged until the upload is done
            }

            // 1. Customer-confirmation gate (signed PI OR LPO), grounded on the
            //    attachments actually present.
            var dlg =
                new ProGlassAutomation.Views.ProformaInvoice.SavedInvoicesList
                    .InvoiceApprovalDialog(attachments.Count)
                {
                    Owner = Application.Current?.MainWindow
                };
            if (dlg.ShowDialog() != true || !dlg.Approved)
                return;   // cancelled — status unchanged

            // 2. Persist the LPO number into the snapshot (Step 3
            //    Project.LpoNumber) so invoices / statement generator carry it.
            //    • LPO choice → the dialog already requires a number.
            //    • Signed PI   → save the number only if the user entered one
            //      (it is sometimes written on the signed PI).
            string lpoToSave = dlg.LpoNumber?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(lpoToSave))
            {
                var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(entity);
                snapshot.Project.LpoNumber = lpoToSave;
                entity.FullSnapshotJson =
                    ProformaInvoiceMapper.SerializeSnapshot(snapshot);
            }

            // 3. Approve + persist.
            entity.Status = "Approved";
            entity.UpdatedAt = DateTime.Now;
            await App.Invoices.UpdateAsync(entity);

            AuditService.Log(
                AuditSections.Proforma, "Invoice approved",
                $"{entity.ProformaId} approved " +
                $"({(dlg.IsSignedPi ? "signed PI" : "LPO " + dlg.LpoNumber)})",
                "PI", entity.ProformaId);

            // 4. Auto-convert to a Job Order.
            await ConvertToJobOrderAsync(entity);

            await LoadInvoicesAsync();

            MessageBox.Show(
                $"Invoice '{entity.ProformaId}' approved and converted to a " +
                "Job Order.",
                "Approved",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        /// <summary>
        /// Auto-converts an approved Proforma Invoice to a Job Order using the
        /// same mapper/repository the old manual "Convert to Job Order" action
        /// used. Skips when a Job Order already exists for the proforma, so a
        /// re-approval can never duplicate it.
        /// </summary>
        private async Task ConvertToJobOrderAsync(SavedProformaInvoice piEntity)
        {
            if (piEntity == null || App.Jobs == null) return;

            try
            {
                var existing = await App.Jobs.GetBySourceProformaIdAsync(
                    piEntity.ProformaId ?? "");
                if (existing != null && existing.Count > 0) return;   // already converted

                var jobSnapshot = JobOrderMapper.BuildFromProformaInvoice(piEntity);
                if (await App.Jobs.ExistsByJobOrderIdAsync(jobSnapshot.JobOrderId))
                {
                    jobSnapshot.JobOrderId = await App.Jobs.SuggestNextIdAsync(
                        jobSnapshot.JobOrderId);
                }

                var jobEntity = JobOrderMapper.BuildEntityFromSnapshot(
                    jobSnapshot, piEntity.GrandTotalVatInclusive);
                await App.Jobs.InsertAsync(jobEntity);

                AuditService.Log(
                    AuditSections.JobOrder, "Job Order auto-created on approval",
                    $"{jobEntity.JobOrderId} ← {piEntity.ProformaId}",
                    "JO", jobEntity.JobOrderId);
            }
            catch (Exception ex)
            {
                // A Job Order failure must not roll back the approval itself.
                ProGlassAutomation.Services.Logging.AppLogService.Error(
                    "Auto-convert to Job Order on approval failed.", ex,
                    new ProGlassAutomation.Services.Logging.LogContext
                    {
                        Module = "Proforma",
                        Class = nameof(SavedInvoicesListViewModel),
                        Operation = "ApproveAutoConvert"
                    });
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PHASE 9.5b — Export from saved list via SHARED ENGINE
        //  BUG-75/76: success feedback + CSV parity
        // ══════════════════════════════════════════════════════════════════

        private async Task ExportPdfFromListAsync(SavedInvoiceListItem item)
        {
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.ProformaExport, "export an invoice PDF"))
                return;

            if (item == null || App.Invoices == null) return;

            try
            {
                var entity = await App.Invoices.GetByIdAsync(item.Id);
                if (entity == null) return;

                var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(entity);
                var layout = new ProformaInvoicePdfLayout(entity, snapshot);
                var engine = new PdfExportEngine();
                var request = new ExportRequest
                {
                    PromptForLocation = true,
                    OpenAfterSave = true
                };

                var result = await engine.ExportAsync(layout, request);

                // BUG-75: success feedback
                LastExportStatus = result.Success
                    ? $"✓ PDF exported: {entity.ProformaId}"
                    : result.Cancelled ? "" : $"✗ PDF export failed";

                if (!result.Success && !result.Cancelled)
                    MessageBox.Show(
                        $"PDF export failed: {result.ErrorMessage}",
                        "Export Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                LastExportStatus = $"✗ PDF export failed: {ex.Message}";
                MessageBox.Show(
                    $"PDF export failed: {ex.Message}",
                    "Export Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task ExportExcelFromListAsync(SavedInvoiceListItem item)
        {
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.ProformaExport, "export an invoice to Excel"))
                return;

            if (item == null || App.Invoices == null) return;

            try
            {
                var entity = await App.Invoices.GetByIdAsync(item.Id);
                if (entity == null) return;

                var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(entity);
                var layout = new ProformaInvoiceExcelLayout(entity, snapshot);
                var engine = new ExcelExportEngine();
                var request = new ExportRequest
                {
                    PromptForLocation = true,
                    OpenAfterSave = true
                };

                var result = await engine.ExportAsync(layout, request);

                LastExportStatus = result.Success
                    ? $"✓ Excel exported: {entity.ProformaId}"
                    : result.Cancelled ? "" : $"✗ Excel export failed";

                if (!result.Success && !result.Cancelled)
                    MessageBox.Show(
                        $"Excel export failed: {result.ErrorMessage}",
                        "Export Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                LastExportStatus = $"✗ Excel export failed: {ex.Message}";
                MessageBox.Show(
                    $"Excel export failed: {ex.Message}",
                    "Export Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// BUG-76: CSV export from list — was missing, PDF/Excel both had it.
        /// Now list and detail views have full parity for all 3 formats.
        /// </summary>
        private async Task ExportCsvFromListAsync(SavedInvoiceListItem item)
        {
            if (!ProGlassAutomation.Services.Audit.PermissionService.Require(
                ProGlassAutomation.Services.Audit.AppPermissions.ProformaExport, "export an invoice CSV"))
                return;

            if (item == null || App.Invoices == null) return;

            try
            {
                var entity = await App.Invoices.GetByIdAsync(item.Id);
                if (entity == null) return;

                var snapshot = ProformaInvoiceMapper.DeserializeSnapshot(entity);
                var layout = new ProformaInvoiceCsvLayout(entity, snapshot);
                var engine = new CsvExportEngine();
                var request = new ExportRequest
                {
                    PromptForLocation = true,
                    OpenAfterSave = true
                };

                var result = await engine.ExportAsync(layout, request);

                LastExportStatus = result.Success
                    ? $"✓ CSV exported: {entity.ProformaId}"
                    : result.Cancelled ? "" : $"✗ CSV export failed";

                if (!result.Success && !result.Cancelled)
                    MessageBox.Show(
                        $"CSV export failed: {result.ErrorMessage}",
                        "Export Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                LastExportStatus = $"✗ CSV export failed: {ex.Message}";
                MessageBox.Show(
                    $"CSV export failed: {ex.Message}",
                    "Export Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}