using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.Csv;
using ProGlassAutomation.Services.Export.Excel;
using ProGlassAutomation.Services.Export.Pdf;
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

        public SavedInvoicesListViewModel()
        {
            RefreshCommand = new RelayCommand(async _ => await LoadInvoicesAsync());
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

            _ = LoadInvoicesAsync();
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

        // ══════════════════════════════════════════════════════════════════
        //  PHASE 9.5b — Export from saved list via SHARED ENGINE
        //  BUG-75/76: success feedback + CSV parity
        // ══════════════════════════════════════════════════════════════════

        private async Task ExportPdfFromListAsync(SavedInvoiceListItem item)
        {
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