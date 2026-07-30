// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/StatementGeneratorViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 17 — Workflow Integration)
//
// CHAT 17 FINAL INTEGRATION:
//   RunGenerateAsync() now opens WorkflowProgressDialog which runs
//   the 8-step validated pipeline and returns a WorkflowResult with
//   all pre-collected data (signed PI + signed DNs + regenerated
//   invoice PDFs). Then calls ExportFromWorkflowAsync to merge.
//
//   This gives:
//     - Live progress dialog with 8 steps
//     - Warning dialog when missing invoices detected
//     - Complete combined PDF (statement + all invoices + all attachments)
//     - Images auto-converted to PDF pages
//     - Placeholder pages for any invoice regen failures
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Services.Statement.Aggregation;
using ProGlassAutomation.Services.Statement.Pdf;
using ProGlassAutomation.Services.Statement.Repository;
using ProGlassAutomation.Views.Reports.Workflow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProGlassAutomation.ViewModels.Reports.StatementGenerator
{
    public class StatementGeneratorViewModel : ViewModelBase
    {
        private readonly IStatementRepository _repo;
        private readonly StatementAggregator _aggregator;

        // ── Preview VM (Part 2) ──────────────────────────────────────────
        public StatementPreviewViewModel PreviewVM { get; } = new();

        // ── Debounce timer for auto-preview ──────────────────────────────
        private readonly DispatcherTimer _debounceTimer;
        private const int DebounceMs = 400;

        // ── Cancellation token for in-flight preview requests ────────────
        private CancellationTokenSource? _previewCts;

        // ── Callbacks ────────────────────────────────────────────────────
        public Func<Task>? OnStatementSaved { get; set; }
        public Action? OnCancelled { get; set; }

        // ── Edit mode ────────────────────────────────────────────────────
        private int _editId = 0;
        private string _editNumber = "";
        public bool IsEditMode => _editId > 0;
        public string PageTitle => IsEditMode
            ? $"Edit Statement — {_editNumber}"
            : "New Account Statement";

        // ═════════════════════════════════════════════════════════════════
        //  FILTER FIELDS — all setters trigger SchedulePreview()
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
                SchedulePreview();
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
                SchedulePreview();
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
                SchedulePreview();
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
                SchedulePreview();
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
                SchedulePreview();
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
                SchedulePreview();
            }
        }

        // ── PDF options ──────────────────────────────────────────────────
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

        // ── Status checkboxes ────────────────────────────────────────────
        public ObservableCollection<StatusCheckItem> StatusItems { get; } = new();

        // ── Dropdown sources ─────────────────────────────────────────────
        public ObservableCollection<string> Companies { get; } = new();
        public ObservableCollection<string> Customers { get; } = new();
        public ObservableCollection<string> Projects { get; } = new();
        public ObservableCollection<string> Salesmen { get; } = new();

        // ── State ────────────────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading == value) return;
                _isLoading = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
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
                CommandManager.InvalidateRequerySuggested();
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

        // ── Preview snapshot ─────────────────────────────────────────────
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

        // ═════════════════════════════════════════════════════════════════
        //  COMMANDS
        // ═════════════════════════════════════════════════════════════════

        public ICommand PreviewCommand { get; }
        public ICommand GenerateCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand DeselectAllCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        public StatementGeneratorViewModel(IStatementRepository repo)
        {
            _repo = repo;
            _aggregator = new StatementAggregator();

            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DebounceMs)
            };
            _debounceTimer.Tick += async (s, e) =>
            {
                _debounceTimer.Stop();
                await RunPreviewAsync();
            };

            foreach (var s in new[]
            {
                "Draft", "Issued", "Paid", "PartiallyPaid",
                "Overdue", "Cancelled", "Void"
            })
            {
                var item = new StatusCheckItem(s,
                    s is "Issued" or "Paid" or "PartiallyPaid" or "Overdue");
                item.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(StatusCheckItem.IsChecked))
                    {
                        OnPropertyChanged(nameof(HasFilter));
                        SchedulePreview();
                    }
                };
                StatusItems.Add(item);
            }

            PreviewVM.SelectionChanged += () =>
                CommandManager.InvalidateRequerySuggested();

            PreviewCommand = new RelayCommand(
                async _ => await RunPreviewAsync(),
                _ => !_isLoading && !_isGenerating && HasFilter);

            GenerateCommand = new RelayCommand(
                async _ => await RunGenerateAsync(),
                _ => !_isLoading && !_isGenerating &&
                     _hasPreview && PreviewVM.HasSelection);

            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
            CancelCommand = new RelayCommand(_ => OnCancelled?.Invoke());

            SelectAllCommand = new RelayCommand(
                _ => PreviewVM.SelectAll(),
                _ => PreviewVM.Rows.Count > 0);

            DeselectAllCommand = new RelayCommand(
                _ => PreviewVM.DeselectAll(),
                _ => PreviewVM.Rows.Count > 0);

            _ = LoadMasterDataAsync();
        }

        // ═════════════════════════════════════════════════════════════════
        //  DEBOUNCE
        // ═════════════════════════════════════════════════════════════════

        private void SchedulePreview()
        {
            _debounceTimer.Stop();

            if (!HasFilter)
            {
                PreviewSnapshot = null;
                PreviewVM.UpdateFromSnapshot(null);
                StatusMessage = "";
                return;
            }

            _debounceTimer.Start();
        }

        // ═════════════════════════════════════════════════════════════════
        //  PREPARE
        // ═════════════════════════════════════════════════════════════════

        public void PrepareNew()
        {
            _editId = 0;
            _editNumber = "";
            ClearFilters();
            PreviewSnapshot = null;

            Application.Current?.Dispatcher.Invoke(() =>
                PreviewVM.UpdateFromSnapshot(null));

            StatusMessage = "";
            OnPropertyChanged(string.Empty);
        }

        public void PrepareEdit(StatementListItem item)
        {
            _editId = item.Id;
            _editNumber = item.StatementNumber;

            _debounceTimer.Stop();

            _filterCompany = item.FilterCompany ?? "";
            _filterCustomer = item.FilterCustomer ?? "";
            _filterProject = item.FilterProject ?? "";
            _filterSalesman = item.FilterSalesman ?? "";
            _dateFrom = item.FilterDateFrom;
            _dateTo = item.FilterDateTo;

            PreviewSnapshot = null;

            Application.Current?.Dispatcher.Invoke(() =>
                PreviewVM.UpdateFromSnapshot(null));

            StatusMessage = $"Editing {item.StatementNumber} — " +
                             "loading matching invoices...";

            OnPropertyChanged(string.Empty);

            SchedulePreview();
        }

        // ═════════════════════════════════════════════════════════════════
        //  MASTER DATA
        // ═════════════════════════════════════════════════════════════════

        private async Task LoadMasterDataAsync()
        {
            try
            {
                var companies = await Task.Run(
                    () => App.MasterData.GetAllCompaniesAsync());
                var clients = await Task.Run(
                    () => App.MasterData.GetAllClientsAsync());
                var projects = await Task.Run(
                    () => App.MasterData.GetAllProjectsAsync());
                var salesmen = await Task.Run(
                    () => App.MasterData.GetAllSalesmenAsync());

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    Companies.Clear();
                    foreach (var c in companies
                        .Select(c => c.CompanyName)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct()
                        .OrderBy(n => n))
                        Companies.Add(c);

                    Customers.Clear();
                    foreach (var c in clients
                        .Select(c => c.CustomerName)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct()
                        .OrderBy(n => n))
                        Customers.Add(c);

                    Projects.Clear();
                    foreach (var p in projects
                        .Select(p => !string.IsNullOrWhiteSpace(p.ProjectContractNo)
                            ? p.ProjectContractNo.Trim()
                            : (p.ProjectIdentifierName?.Trim() ?? ""))
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct()
                        .OrderBy(n => n))
                        Projects.Add(p);

                    Salesmen.Clear();
                    foreach (var s in salesmen
                        .Select(s => s.Name)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct()
                        .OrderBy(n => n))
                        Salesmen.Add(s);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[StatementGenerator] Master data load failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Runs an async data load with isolated error handling.
        /// Returns null on failure — caller checks null before using.
        /// Never throws — ensures other concurrent loads complete.
        /// </summary>
        private static async Task<List<T>?> SafeLoadAsync<T>(
            Func<Task<List<T>>> loader)
        {
            try
            {
                return await loader();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[StatementGenerator] Master data load failed: {ex.Message}");
                return null;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  PREVIEW
        // ═════════════════════════════════════════════════════════════════

        private async Task RunPreviewAsync()
        {
            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;

            IsLoading = true;
            StatusMessage = "Loading matching invoices…";
            PreviewSnapshot = null;

            Application.Current?.Dispatcher.Invoke(() =>
                PreviewVM.UpdateFromSnapshot(null));

            try
            {
                var filter = BuildFilter();

                var number = await Task.Run(
                    () => _repo.GetNextStatementNumberAsync(), token);

                if (token.IsCancellationRequested) return;

                var snapshot = await Task.Run(
                    () => _aggregator.BuildAsync(filter, number), token);

                if (token.IsCancellationRequested) return;

                PreviewSnapshot = snapshot;

                Application.Current?.Dispatcher.Invoke(() =>
                    PreviewVM.UpdateFromSnapshot(snapshot));

                if (snapshot == null)
                {
                    StatusMessage = "No invoices match the selected filters. " +
                                    "Try adjusting your criteria.";
                }
                else
                {
                    int count = snapshot.InvoiceRows.Count;
                    StatusMessage =
                        $"Found {count} invoice{(count == 1 ? "" : "s")}  ·  " +
                        $"Total: AED {snapshot.Financial.TotalInvoiced:N2}  ·  " +
                        $"Outstanding: AED {snapshot.Financial.TotalOutstanding:N2}";
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                StatusMessage = $"Preview failed: {ex.Message}";
                PreviewSnapshot = null;

                Application.Current?.Dispatcher.Invoke(() =>
                    PreviewVM.UpdateFromSnapshot(null));
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  ★★★ GENERATE — CHAT 17 WORKFLOW INTEGRATION ★★★
        //  Opens WorkflowProgressDialog which runs 8-step engine
        //  and returns WorkflowResult with all pre-collected data.
        //  Then calls ExportFromWorkflowAsync to merge into final PDF.
        // ═════════════════════════════════════════════════════════════════

        private async Task RunGenerateAsync()
        {
            if (_previewSnapshot == null) return;

            var selectedNumbers = PreviewVM.GetSelectedInvoiceNumbers();
            if (selectedNumbers.Count == 0)
            {
                StatusMessage = "No invoices selected. " +
                                "Please select at least one invoice.";
                return;
            }

            IsGenerating = true;
            StatusMessage = "Opening workflow dialog…";

            try
            {
                // ── Build filter + get next statement number ──────────────
                var filter = BuildFilter();
                var number = await Task.Run(
                    () => _repo.GetNextStatementNumberAsync());

                string customerName = _previewSnapshot.CustomerInfo?
                    .CustomerName ?? "Unknown Customer";

                // ── OPEN WORKFLOW DIALOG (blocking modal) ─────────────────
                var dialog = new WorkflowProgressDialog(
                    filter, number, customerName);

                dialog.Owner = Application.Current?.MainWindow;
                dialog.ShowDialog();

                var wfResult = dialog.Result;

                // ── Check workflow outcome ────────────────────────────────
                if (wfResult == null)
                {
                    StatusMessage = "Workflow was cancelled.";
                    return;
                }

                if (!wfResult.ShouldGeneratePdf)
                {
                    StatusMessage = wfResult.Summary;
                    return;
                }

                if (wfResult.Snapshot == null)
                {
                    StatusMessage = "Workflow returned no snapshot.";
                    return;
                }

                // ── Filter snapshot to selected invoices only ─────────────
                var selectedSet = selectedNumbers.ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

                var filteredRows = wfResult.Snapshot.InvoiceRows
                    .Where(r => selectedSet.Contains(r.InvoiceNumber))
                    .ToList();

                if (filteredRows.Count == 0)
                {
                    StatusMessage = "No selected invoices found in workflow result.";
                    return;
                }

                // Rebuild snapshot with only selected rows
                wfResult.Snapshot = BuildFilteredSnapshot(
                    wfResult.Snapshot, selectedNumbers);

                // Also filter the TaxInvoicePdfs to selected only
                wfResult.TaxInvoicePdfs = wfResult.TaxInvoicePdfs
                    .Where(p => selectedSet.Contains(p.InvoiceNumber))
                    .ToList();

                // ── Generate final combined PDF ───────────────────────────
                StatusMessage = "Generating final PDF...";

                string pdfPath = await StatementPdfExporter
                    .ExportFromWorkflowAsync(wfResult);

                // ── Save statement to DB ──────────────────────────────────
                StatusMessage = "Saving statement record…";

                var entity = new SavedStatement
                {
                    StatementNumber = wfResult.Snapshot.Header.StatementNumber,
                    Title = wfResult.Snapshot.Header.Title,
                    FilterCompany = filter.CompanyName ?? "",
                    FilterCustomer = filter.CustomerName ?? "",
                    FilterProject = filter.ProjectName ?? "",
                    FilterSalesman = filter.SalesmanName ?? "",
                    FilterDateFrom = filter.DateFrom,
                    FilterDateTo = filter.DateTo,
                    InvoiceCount = wfResult.Snapshot.Financial.TotalInvoiceCount,
                    TotalInvoiced = wfResult.Snapshot.Financial.TotalInvoiced,
                    TotalOutstanding = wfResult.Snapshot.Financial.TotalOutstanding,
                    OverdueAmount = wfResult.Snapshot.Financial.OverdueAmount,
                    PdfPath = pdfPath,
                    HasAttachments = true,
                    FullSnapshotJson = JsonSerializer.Serialize(
                        wfResult.Snapshot),
                    FilterJson = JsonSerializer.Serialize(filter)
                };

                foreach (var row in wfResult.Snapshot.InvoiceRows)
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

                if (_editId > 0)
                {
                    entity.Id = _editId;
                    await Task.Run(() => _repo.UpdateAsync(entity));
                }
                else
                {
                    await Task.Run(() => _repo.InsertAsync(entity));
                }

                StatusMessage = wfResult.AllWarnings.Count > 0
                    ? $"✓ Statement generated with " +
                      $"{wfResult.AllWarnings.Count} warning(s)."
                    : "✓ Statement generated successfully.";

                StatementPdfExporter.OpenPdf(pdfPath);

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

        private static StatementSnapshot BuildFilteredSnapshot(
            StatementSnapshot original,
            List<string> selectedInvoiceNumbers)
        {
            var selectedSet = selectedInvoiceNumbers.ToHashSet(
                StringComparer.OrdinalIgnoreCase);

            var filteredRows = original.InvoiceRows
                .Where(r => selectedSet.Contains(r.InvoiceNumber))
                .ToList();

            decimal overdue = filteredRows
                .Where(r => r.IsOverdue)
                .Sum(r => r.Balance);

            var financial = new StatementFinancialSummary
            {
                TotalInvoiceCount = filteredRows.Count,
                TotalSubtotal = Math.Round(
                    filteredRows.Sum(r => r.Subtotal), 2),
                TotalVat = Math.Round(
                    filteredRows.Sum(r => r.VatAmount), 2),
                TotalInvoiced = Math.Round(
                    filteredRows.Sum(r => r.GrandTotal), 2),
                TotalPaid = Math.Round(
                    filteredRows.Sum(r => r.PaidAmount), 2),
                TotalOutstanding = Math.Round(
                    filteredRows.Sum(r => r.Balance), 2),
                OverdueAmount = Math.Round(overdue, 2),
                TotalQty = filteredRows.Sum(r => r.TotalQty),
                TotalSqm = Math.Round(
                    filteredRows.Sum(r => r.TotalSqm), 3)
            };

            var today = DateTime.Today;
            var aging = new StatementAgingBuckets();
            foreach (var row in filteredRows)
            {
                decimal balance = row.Balance;
                if (balance <= 0m) continue;

                if (row.DueDate == default || row.DueDate.Date >= today)
                {
                    aging.Current += balance;
                    continue;
                }

                int days = row.DaysOverdue;
                if (days <= 30) aging.Days0To30 += balance;
                else if (days <= 60) aging.Days31To60 += balance;
                else if (days <= 90) aging.Days61To90 += balance;
                else aging.Days90Plus += balance;
            }
            aging.Current = Math.Round(aging.Current, 2);
            aging.Days0To30 = Math.Round(aging.Days0To30, 2);
            aging.Days31To60 = Math.Round(aging.Days31To60, 2);
            aging.Days61To90 = Math.Round(aging.Days61To90, 2);
            aging.Days90Plus = Math.Round(aging.Days90Plus, 2);

            return new StatementSnapshot
            {
                Header = original.Header,
                CustomerInfo = original.CustomerInfo,
                Financial = financial,
                Aging = aging,
                InvoiceRows = filteredRows
            };
        }

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
            _debounceTimer.Stop();

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

            PreviewSnapshot = null;
            Application.Current?.Dispatcher.Invoke(() =>
                PreviewVM.UpdateFromSnapshot(null));

            StatusMessage = "Filters cleared.";
        }

        private static string? NullIfEmpty(string s) =>
            string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    // ═════════════════════════════════════════════════════════════════════
    //  STATUS CHECKBOX ITEM
    // ═════════════════════════════════════════════════════════════════════

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