// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/StatementGeneratorViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 16)
//
// PURPOSE:
//   Part 1 of 2 — Filter panel + master data loading + preview trigger.
//
// ADDITIONS vs PREVIOUSLY PASTED VERSION:
//   1. PreviewVM property added (line after _aggregator field).
//      Required by StatementGeneratorView.xaml bindings:
//        {Binding PreviewVM.Rows}
//        {Binding PreviewVM.AgingBars}
//        {Binding PreviewVM.TotalInvoicedDisplay}
//        {Binding PreviewVM.TotalVatDisplay}
//        {Binding PreviewVM.TotalOutstandingDisplay}
//        {Binding PreviewVM.OverdueAmountDisplay}
//   2. RunPreviewAsync() now calls PreviewVM.UpdateFromSnapshot(snapshot)
//      on the UI thread after PreviewSnapshot is set.
//      Required so preview list + aging bars + KPI footer refresh.
//
// ALL OTHER CODE IDENTICAL to the previously pasted version.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Services.Statement.Aggregation;
using ProGlassAutomation.Services.Statement.Pdf;
using ProGlassAutomation.Services.Statement.Repository;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Reports.StatementGenerator
{
    public class StatementGeneratorViewModel : ViewModelBase
    {
        private readonly IStatementRepository _repo;
        private readonly StatementAggregator _aggregator;

        // ── Preview VM (Part 2) — bound by StatementGeneratorView ──────────
        // ADDITION 1: This property was missing from the pasted version.
        // StatementGeneratorView.xaml binds {Binding PreviewVM.Rows},
        // {Binding PreviewVM.AgingBars}, etc. — all require this property.
        public StatementPreviewViewModel PreviewVM { get; } = new();

        // ── Callbacks (set by SalesReportViewModel) ───────────────────────
        public Func<Task>? OnStatementSaved { get; set; }
        public Action? OnCancelled { get; set; }

        // ── Edit mode ─────────────────────────────────────────────────────
        private int _editId = 0;
        private string _editNumber = "";
        public bool IsEditMode => _editId > 0;
        public string PageTitle => IsEditMode
            ? $"Edit Statement — {_editNumber}"
            : "New Account Statement";

        // ═════════════════════════════════════════════════════════════════
        //  FILTER FIELDS
        // ═════════════════════════════════════════════════════════════════

        private string _filterCompany = "";
        public string FilterCompany
        {
            get => _filterCompany;
            set
            {
                if (_filterCompany == value) return;
                _filterCompany = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasFilter));
            }
        }

        private string _filterCustomer = "";
        public string FilterCustomer
        {
            get => _filterCustomer;
            set
            {
                if (_filterCustomer == value) return;
                _filterCustomer = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasFilter));
            }
        }

        private string _filterProject = "";
        public string FilterProject
        {
            get => _filterProject;
            set
            {
                if (_filterProject == value) return;
                _filterProject = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasFilter));
            }
        }

        private string _filterSalesman = "";
        public string FilterSalesman
        {
            get => _filterSalesman;
            set
            {
                if (_filterSalesman == value) return;
                _filterSalesman = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasFilter));
            }
        }

        private DateTime? _dateFrom;
        public DateTime? DateFrom
        {
            get => _dateFrom;
            set
            {
                if (_dateFrom == value) return;
                _dateFrom = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasFilter));
            }
        }

        private DateTime? _dateTo;
        public DateTime? DateTo
        {
            get => _dateTo;
            set
            {
                if (_dateTo == value) return;
                _dateTo = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasFilter));
            }
        }

        // ── PDF options ───────────────────────────────────────────────────
        private bool _includeAttachments;
        public bool IncludeAttachments
        {
            get => _includeAttachments;
            set
            {
                if (_includeAttachments == value) return;
                _includeAttachments = value;
                OnPropertyChanged();
            }
        }

        private bool _includeDnDetails;
        public bool IncludeDnDetails
        {
            get => _includeDnDetails;
            set
            {
                if (_includeDnDetails == value) return;
                _includeDnDetails = value;
                OnPropertyChanged();
            }
        }

        // ── Status checkboxes ─────────────────────────────────────────────
        public ObservableCollection<StatusCheckItem> StatusItems { get; } = new();

        // ── Dropdown sources ──────────────────────────────────────────────
        public ObservableCollection<string> Companies { get; } = new();
        public ObservableCollection<string> Customers { get; } = new();
        public ObservableCollection<string> Projects { get; } = new();
        public ObservableCollection<string> Salesmen { get; } = new();

        // ── State ─────────────────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading == value) return;
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        private bool _isGenerating;
        public bool IsGenerating
        {
            get => _isGenerating;
            set
            {
                if (_isGenerating == value) return;
                _isGenerating = value;
                OnPropertyChanged();
            }
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage == value) return;
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        private bool _hasPreview;
        public bool HasPreview
        {
            get => _hasPreview;
            set
            {
                if (_hasPreview == value) return;
                _hasPreview = value;
                OnPropertyChanged();
            }
        }

        public bool HasFilter =>
            !string.IsNullOrWhiteSpace(_filterCompany) ||
            !string.IsNullOrWhiteSpace(_filterCustomer) ||
            !string.IsNullOrWhiteSpace(_filterProject) ||
            !string.IsNullOrWhiteSpace(_filterSalesman) ||
            _dateFrom.HasValue ||
            _dateTo.HasValue ||
            StatusItems.Any(s => s.IsChecked);

        // ── Preview snapshot (read by Part 2 VM) ──────────────────────────
        private StatementSnapshot? _previewSnapshot;
        public StatementSnapshot? PreviewSnapshot
        {
            get => _previewSnapshot;
            private set
            {
                _previewSnapshot = value;
                OnPropertyChanged();
                HasPreview = value != null;
            }
        }

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand PreviewCommand { get; }
        public ICommand GenerateCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand CancelCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        public StatementGeneratorViewModel(IStatementRepository repo)
        {
            _repo = repo;
            _aggregator = new StatementAggregator();

            // ── Status checkboxes — default active statuses pre-checked ───
            foreach (var s in new[]
            {
                "Draft", "Issued", "Paid", "PartiallyPaid",
                "Overdue", "Cancelled", "Void"
            })
            {
                StatusItems.Add(new StatusCheckItem(s,
                    s is "Issued" or "Paid" or "PartiallyPaid" or "Overdue"));
            }

            // ── Commands ──────────────────────────────────────────────────
            PreviewCommand = new RelayCommand(
                async _ => await RunPreviewAsync(),
                _ => !_isLoading && !_isGenerating && HasFilter);

            GenerateCommand = new RelayCommand(
                async _ => await RunGenerateAsync(),
                _ => !_isLoading && !_isGenerating && _hasPreview);

            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());

            CancelCommand = new RelayCommand(_ => OnCancelled?.Invoke());

            // ── Load master data dropdowns on construction ─────────────────
            _ = LoadMasterDataAsync();
        }

        // ═════════════════════════════════════════════════════════════════
        //  PREPARE (called by SalesReportViewModel)
        // ═════════════════════════════════════════════════════════════════

        public void PrepareNew()
        {
            _editId = 0;
            _editNumber = "";
            ClearFilters();
            PreviewSnapshot = null;

            // Clear preview panel when starting fresh
            Application.Current?.Dispatcher.Invoke(() =>
                PreviewVM.UpdateFromSnapshot(null));

            StatusMessage = "";
            OnPropertyChanged(string.Empty);
        }

        public void PrepareEdit(StatementListItem item)
        {
            _editId = item.Id;
            _editNumber = item.StatementNumber;

            // Restore filter values from saved statement
            _filterCompany = item.FilterCompany;
            _filterCustomer = item.FilterCustomer;
            _filterProject = item.FilterProject;
            _filterSalesman = item.FilterSalesman;
            _dateFrom = item.FilterDateFrom;
            _dateTo = item.FilterDateTo;

            PreviewSnapshot = null;

            // Clear preview panel — user must click Preview to reload
            Application.Current?.Dispatcher.Invoke(() =>
                PreviewVM.UpdateFromSnapshot(null));

            StatusMessage = $"Editing {item.StatementNumber} — " +
                             "click Preview to reload results.";
            OnPropertyChanged(string.Empty);
        }

        // ═════════════════════════════════════════════════════════════════
        //  MASTER DATA — dropdown population
        // ═════════════════════════════════════════════════════════════════

        private async Task LoadMasterDataAsync()
        {
            try
            {
                // GetAllCompaniesAsync → SavedCompany.CompanyName
                var companies = await Task.Run(
                    () => App.MasterData.GetAllCompaniesAsync());

                // GetAllClientsAsync → SavedClient.CustomerName
                var clients = await Task.Run(
                    () => App.MasterData.GetAllClientsAsync());

                // GetAllProjectsAsync → SavedProject.ProjectIdentifierName
                var projects = await Task.Run(
                    () => App.MasterData.GetAllProjectsAsync());

                // GetAllSalesmenAsync → SavedSalesman.Name
                var salesmen = await Task.Run(
                    () => App.MasterData.GetAllSalesmenAsync());

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    Companies.Clear();
                    foreach (var c in companies
                        .Select(c => c.CompanyName)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct())
                        Companies.Add(c);

                    Customers.Clear();
                    foreach (var c in clients
                        .Select(c => c.CustomerName)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct())
                        Customers.Add(c);

                    Projects.Clear();
                    foreach (var p in projects
                        .Select(p => p.ProjectIdentifierName)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct())
                        Projects.Add(p);

                    Salesmen.Clear();
                    foreach (var s in salesmen
                        .Select(s => s.Name)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct())
                        Salesmen.Add(s);
                });
            }
            catch
            {
                // Non-critical — user can type filter values manually
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  PREVIEW
        // ═════════════════════════════════════════════════════════════════

        private async Task RunPreviewAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading matching invoices…";
            PreviewSnapshot = null;

            // Clear preview panel immediately so user sees loading state
            Application.Current?.Dispatcher.Invoke(() =>
                PreviewVM.UpdateFromSnapshot(null));

            try
            {
                var filter = BuildFilter();

                // Get next statement number for preview header
                var number = await Task.Run(
                    () => _repo.GetNextStatementNumberAsync());

                // Build snapshot on background thread
                var snapshot = await Task.Run(
                    () => _aggregator.BuildAsync(filter, number));

                // Set snapshot property (fires HasPreview)
                PreviewSnapshot = snapshot;

                // ADDITION 2: Update Part 2 VM on UI thread.
                // This was missing from the pasted version.
                // Without this, Rows + AgingBars + KPI footer never populate.
                Application.Current?.Dispatcher.Invoke(() =>
                    PreviewVM.UpdateFromSnapshot(snapshot));

                StatusMessage = snapshot == null
                    ? "No invoices match the selected filters. " +
                      "Try adjusting your criteria."
                    : $"Found {snapshot.InvoiceRows.Count} invoice(s)  ·  " +
                      $"Total: AED {snapshot.Financial.TotalInvoiced:N2}  ·  " +
                      $"Outstanding: AED {snapshot.Financial.TotalOutstanding:N2}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Preview failed: {ex.Message}";
                PreviewSnapshot = null;

                // Clear preview on error
                Application.Current?.Dispatcher.Invoke(() =>
                    PreviewVM.UpdateFromSnapshot(null));
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  GENERATE
        // ═════════════════════════════════════════════════════════════════

        private async Task RunGenerateAsync()
        {
            if (_previewSnapshot == null) return;

            IsGenerating = true;
            StatusMessage = "Generating PDF…";

            try
            {
                // ── Generate PDF ──────────────────────────────────────────
                string pdfPath;
                if (_includeAttachments)
                {
                    StatusMessage = "Merging invoices and attachments…";
                    pdfPath = await StatementPdfExporter
                        .ExportCombinedAsync(_previewSnapshot);
                }
                else
                {
                    pdfPath = await StatementPdfExporter
                        .ExportStatementOnlyAsync(_previewSnapshot);
                }

                // ── Build DB entity ───────────────────────────────────────
                StatusMessage = "Saving statement record…";
                var filter = BuildFilter();

                var entity = new SavedStatement
                {
                    StatementNumber = _previewSnapshot.Header.StatementNumber,
                    Title = _previewSnapshot.Header.Title,
                    FilterCompany = filter.CompanyName ?? "",
                    FilterCustomer = filter.CustomerName ?? "",
                    FilterProject = filter.ProjectName ?? "",
                    FilterSalesman = filter.SalesmanName ?? "",
                    FilterDateFrom = filter.DateFrom,
                    FilterDateTo = filter.DateTo,
                    InvoiceCount = _previewSnapshot.Financial.TotalInvoiceCount,
                    TotalInvoiced = _previewSnapshot.Financial.TotalInvoiced,
                    TotalOutstanding = _previewSnapshot.Financial.TotalOutstanding,
                    OverdueAmount = _previewSnapshot.Financial.OverdueAmount,
                    PdfPath = pdfPath,
                    HasAttachments = _includeAttachments,
                    FullSnapshotJson = JsonSerializer.Serialize(
                        _previewSnapshot),
                    FilterJson = JsonSerializer.Serialize(filter)
                };

                // ── Build line items ──────────────────────────────────────
                foreach (var row in _previewSnapshot.InvoiceRows)
                {
                    entity.LineItems.Add(new StatementLineItem
                    {
                        InvoiceNumber = row.InvoiceNumber,
                        ProformaId = row.ProformaId,
                        DeliveryNoteRef = row.DeliveryNoteRef,
                        InvoiceType = row.InvoiceType,
                        Status = row.Status,
                        IssueDate = row.IssueDate,
                        DueDate = row.DueDate,
                        TotalQty = row.TotalQty,
                        TotalSqm = row.TotalSqm,
                        Subtotal = row.Subtotal,
                        VatAmount = row.VatAmount,
                        GrandTotal = row.GrandTotal,
                        PaidAmount = row.PaidAmount,
                        DaysOverdue = row.DaysOverdue
                    });
                }

                // ── Save to DB ────────────────────────────────────────────
                if (_editId > 0)
                {
                    entity.Id = _editId;
                    await Task.Run(() => _repo.UpdateAsync(entity));
                }
                else
                {
                    await Task.Run(() => _repo.InsertAsync(entity));
                }

                StatusMessage = "✓ Statement saved successfully.";

                // ── Open generated PDF ────────────────────────────────────
                StatementPdfExporter.OpenPdf(pdfPath);

                // ── Navigate back to list ─────────────────────────────────
                if (OnStatementSaved != null)
                    await OnStatementSaved.Invoke();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Generation failed: {ex.Message}";
            }
            finally
            {
                IsGenerating = false;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═════════════════════════════════════════════════════════════════

        private StatementFilter BuildFilter() => new()
        {
            CompanyName = NullIfEmpty(_filterCompany),
            CustomerName = NullIfEmpty(_filterCustomer),
            ProjectName = NullIfEmpty(_filterProject),
            SalesmanName = NullIfEmpty(_filterSalesman),
            DateFrom = _dateFrom,
            DateTo = _dateTo,
            Statuses = StatusItems
                                    .Where(s => s.IsChecked)
                                    .Select(s => s.Status)
                                    .ToList(),
            IncludeAttachments = _includeAttachments,
            IncludeDnDetails = _includeDnDetails
        };

        private void ClearFilters()
        {
            _filterCompany = "";
            _filterCustomer = "";
            _filterProject = "";
            _filterSalesman = "";
            _dateFrom = null;
            _dateTo = null;
            _includeAttachments = false;
            _includeDnDetails = false;

            foreach (var s in StatusItems)
                s.IsChecked = s.Status is
                    "Issued" or "Paid" or "PartiallyPaid" or "Overdue";

            OnPropertyChanged(string.Empty);
        }

        private static string? NullIfEmpty(string s) =>
            string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    // ═════════════════════════════════════════════════════════════════════
    //  STATUS CHECKBOX ITEM
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// One status checkbox in the filter panel.
    /// Bound to ItemsControl in StatementGeneratorView.
    /// IsChecked is two-way bound — changes fire OnPropertyChanged.
    /// </summary>
    public class StatusCheckItem : ViewModelBase
    {
        public string Status { get; }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                OnPropertyChanged();
            }
        }

        public StatusCheckItem(string status, bool isChecked = false)
        {
            Status = status;
            _isChecked = isChecked;
        }

        public string StatusLabel => Status switch
        {
            "PartiallyPaid" => "Partially Paid",
            _ => Status
        };

        public string StatusColor => Status switch
        {
            "Paid" => "#047857",
            "PartiallyPaid" => "#047857",
            "Issued" => "#B45309",
            "Overdue" => "#B91C1C",
            "Cancelled" => "#B91C1C",
            "Void" => "#B91C1C",
            _ => "#475569"
        };
    }
}