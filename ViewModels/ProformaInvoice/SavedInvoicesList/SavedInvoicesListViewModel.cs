using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
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
        public int TotalCount { get => _totalCount; private set { _totalCount = value; OnPropertyChanged(); } }

        private int _filteredCount;
        public int FilteredCount { get => _filteredCount; private set { _filteredCount = value; OnPropertyChanged(); } }

        private decimal _totalValueSum;
        public decimal TotalValueSum { get => _totalValueSum; private set { _totalValueSum = value; OnPropertyChanged(); } }

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

        // ── Commands ────────────────────────────────────────────────────────

        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand LoadInvoiceCommand { get; }
        public ICommand DuplicateInvoiceCommand { get; }
        public ICommand DeleteInvoiceCommand { get; }
        public ICommand ChangeStatusCommand { get; }
        public ICommand ExportPdfFromListCommand { get; }
        public ICommand ExportExcelFromListCommand { get; }

        public SavedInvoicesListViewModel()
        {
            RefreshCommand = new RelayCommand(async _ => await LoadInvoicesAsync());
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
            LoadInvoiceCommand = new RelayCommand(p => RequestLoad(p as SavedInvoiceListItem));
            DuplicateInvoiceCommand = new RelayCommand(async p => await DuplicateAsync(p as SavedInvoiceListItem));
            DeleteInvoiceCommand = new RelayCommand(async p => await DeleteAsync(p as SavedInvoiceListItem));
            ChangeStatusCommand = new RelayCommand(async p =>
            {
                if (p is object[] arr && arr.Length == 2
                    && arr[0] is SavedInvoiceListItem item
                    && arr[1] is string newStatus)
                {
                    await ChangeStatusAsync(item, newStatus);
                }
            });

            ExportPdfFromListCommand = new RelayCommand(async p => await ExportPdfFromListAsync(p as SavedInvoiceListItem));
            ExportExcelFromListCommand = new RelayCommand(async p => await ExportExcelFromListAsync(p as SavedInvoiceListItem));

            _ = LoadInvoicesAsync();
        }

        // ── Data loading ────────────────────────────────────────────────────

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
                MessageBox.Show($"Failed to load saved invoices:\n\n{ex.Message}",
                    "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

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

            if (!string.IsNullOrWhiteSpace(_selectedStatusFilter) && _selectedStatusFilter != "All Statuses")
                query = query.Where(x => string.Equals(x.Status, _selectedStatusFilter, StringComparison.OrdinalIgnoreCase));

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

        // ── Actions ─────────────────────────────────────────────────────────

        private void RequestLoad(SavedInvoiceListItem item)
        {
            if (item == null) return;
            OnInvoiceLoadRequested?.Invoke(item.Id);
        }

        private async Task DuplicateAsync(SavedInvoiceListItem item)
        {
            if (item == null || App.Invoices == null) return;

            var confirm = MessageBox.Show(
                $"Duplicate invoice '{item.ProformaId}'?\n\nA new copy will be created with an auto-incremented ID.",
                "Confirm Duplicate", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var original = await App.Invoices.GetByIdAsync(item.Id);
                if (original == null) return;

                string newId = await App.Invoices.SuggestNextIdAsync(original.ProformaId);

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

                MessageBox.Show($"Duplicated as '{newId}'.", "Duplicate Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to duplicate: {ex.Message}", "Duplicate Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteAsync(SavedInvoiceListItem item)
        {
            if (item == null || App.Invoices == null) return;

            var confirm = MessageBox.Show(
                $"Delete invoice '{item.ProformaId}' permanently?\n\nThis cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                await App.Invoices.DeleteAsync(item.Id);
                await LoadInvoicesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete: {ex.Message}", "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ChangeStatusAsync(SavedInvoiceListItem item, string newStatus)
        {
            if (item == null || string.IsNullOrWhiteSpace(newStatus) || App.Invoices == null) return;

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
                MessageBox.Show($"Failed to change status: {ex.Message}", "Status Change Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  PHASE 9.5b — Export PDF/Excel from saved list via SHARED ENGINE
        //  Uses snapshot-based path (gotcha #7: no wizard VMs needed)
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
                if (!result.Success && !result.Cancelled)
                    MessageBox.Show(
                        $"PDF export failed: {result.ErrorMessage}",
                        "Export Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
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
                if (!result.Success && !result.Cancelled)
                    MessageBox.Show(
                        $"Excel export failed: {result.ErrorMessage}",
                        "Export Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Excel export failed: {ex.Message}",
                    "Export Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// LEGACY — BuildExportContextFromSnapshot is retained for backward
        /// compatibility during strangler pattern Round 1. It is no longer
        /// called by any active code path. Will be deleted in Round 2 after
        /// confirming no remaining references.
        /// </summary>
        private static Services.ProformaInvoice.PdfExport.PdfExportContext
            BuildExportContextFromSnapshot(
                ProformaInvoiceSnapshot snapshot,
                SavedProformaInvoice entity)
        {
            var ctx = new Services.ProformaInvoice.PdfExport.PdfExportContext
            {
                CompanyName = snapshot.Company?.CompanyName ?? "",
                CompanyTrn = snapshot.Company?.CompanyTrn ?? "",
                CompanyLocation = snapshot.Company?.CompanyLocation ?? "",
                ProformaId = snapshot.Company?.ProformaId ?? entity.ProformaId,
                IssueDate = snapshot.Company?.IssueDate.ToString("yyyy-MM-dd") ?? "",
                ValidUntil = snapshot.Company?.ValidUntil.ToString("yyyy-MM-dd") ?? "",
                ProformaStatus = snapshot.Company?.ProformaStatus ?? entity.Status,
                CustomerName = snapshot.Client?.CustomerName ?? entity.CustomerName,
                CustomerTrn = snapshot.Client?.CustomerTrn ?? "",
                AttentionName = snapshot.Client?.AttentionExecutiveName ?? "",
                ContactPhone = snapshot.Client?.ClientContactPhone ?? "",
                BillingAddress = snapshot.Client?.BillingAddress ?? "",
                CustomerReference = snapshot.Client?.CustomerReferenceCode ?? "",
                SalesRepresentative = snapshot.Client?.SalesRepresentative ?? "",
                ProjectName = snapshot.Project?.ProjectIdentifierName ?? entity.ProjectName,
                ProjectContractNo = snapshot.Project?.ProjectContractNo ?? "",
                SiteDelivery = snapshot.Project?.SiteDeliveryLocation ?? "",
                LpoNumber = snapshot.Project?.LpoNumber ?? "",
                GlassMaterialsTotal = entity.GlassMaterialsSubtotal,
                OtherChargesTotal = entity.OtherChargesSubtotal,
                NetSubtotal = entity.GlassMaterialsSubtotal + entity.OtherChargesSubtotal - entity.DiscountAmount,
                DiscountAmount = entity.DiscountAmount,
                IsVatApplicable = entity.VatAmount > 0,
                VatPercent = entity.VatAmount > 0 && (entity.GlassMaterialsSubtotal + entity.OtherChargesSubtotal - entity.DiscountAmount) > 0
                    ? Math.Round(entity.VatAmount / (entity.GlassMaterialsSubtotal + entity.OtherChargesSubtotal - entity.DiscountAmount) * 100, 2)
                    : 0m,
                VatAmount = entity.VatAmount,
                GrandTotal = entity.GrandTotalVatInclusive,
                FooterDisclaimer = snapshot.PreparedBy?.FooterDisclaimer ?? "Confidential Document generated automatically by ProGlass Automation ERP platform.",
                TermsText = snapshot.PreparedBy?.TermsText ?? "",
                PreparedByName = snapshot.PreparedBy?.PreparedByName ?? "",
                PreparedByDept = snapshot.PreparedBy?.PreparedByDept ?? "",
                PreparedByStatus = snapshot.PreparedBy?.PreparedByStatus ?? ""
            };

            if (snapshot.SpecGroups != null)
            {
                foreach (var g in snapshot.SpecGroups)
                {
                    var pdfGroup = new Services.ProformaInvoice.PdfExport.PdfSpecGroup
                    {
                        GroupNumber = g.GroupNumber,
                        GroupName = !string.IsNullOrWhiteSpace(g.SelectedSpecName)
                            ? g.SelectedSpecName
                            : (g.GroupName ?? $"Spec Group {g.GroupNumber}")
                    };

                    if (g.ItemRows != null)
                    {
                        foreach (var r in g.ItemRows)
                        {
                            decimal area = Math.Max(Math.Round((decimal)r.W1 * r.H1 / 1_000_000m, 3), 0.5m);
                            decimal totalArea = Math.Round(area * r.Qty, 3);
                            decimal value = Math.Round(totalArea * r.Rate, 2);

                            pdfGroup.Rows.Add(new Services.ProformaInvoice.PdfExport.PdfGlassRow
                            {
                                Serial = r.SerialNumber,
                                GlassRef = r.GlassRef ?? "",
                                W1 = r.W1,
                                H1 = r.H1,
                                W2 = r.W2,
                                H2 = r.H2,
                                SizeW1H1 = $"{r.W1}×{r.H1}",
                                SizeW2H2 = $"{r.W2}×{r.H2}",
                                Qty = r.Qty,
                                Area = totalArea,
                                Rate = r.Rate,
                                Total = value
                            });

                            pdfGroup.TotalQty += r.Qty;
                            pdfGroup.TotalSqm += totalArea;
                            pdfGroup.TotalValue += value;
                        }
                    }

                    ctx.SpecGroups.Add(pdfGroup);
                }
            }

            if (snapshot.OtherCharges?.PredefinedCharges != null)
            {
                foreach (var c in snapshot.OtherCharges.PredefinedCharges.Where(c => c.IsActive))
                {
                    ctx.OtherCharges.Add(new Services.ProformaInvoice.PdfExport.PdfChargeLine
                    {
                        Name = c.TemplateKey ?? "",
                        QtyLabel = "",
                        RateLabel = $"Rate: AED {c.Rate:N2}",
                        Amount = 0
                    });
                }
            }

            if (snapshot.TermClauses != null)
            {
                foreach (var c in snapshot.TermClauses.Where(c => c.IsActive))
                {
                    var clause = new Services.ProformaInvoice.PdfExport.PdfTermClause
                    {
                        Order = c.Order,
                        Title = c.Title ?? "",
                        Body = c.Body ?? ""
                    };
                    if (c.SubLines != null)
                        foreach (var l in c.SubLines)
                            clause.SubLines.Add(l);
                    ctx.TermClauses.Add(clause);
                }
            }

            return ctx;
        }
    }
}