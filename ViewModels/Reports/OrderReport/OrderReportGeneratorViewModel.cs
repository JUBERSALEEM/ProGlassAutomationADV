// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/OrderReport/OrderReportGeneratorViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Report Generator — 4-step wizard (2026-09-06)
//
// PURPOSE:
//   The "Overall Report" generator. Same 4-step pattern as the Statement
//   Generator (Filter → Preview → Pipeline → Review) with the reconciliation
//   and attachment-completeness engine underneath:
//
//     1. Filter   — company / customer / project / salesman / date range
//     2. Preview  — PI list, select ONE (single report) or MANY (multi-PI)
//     3. Pipeline — runs OrderReportEngine.BuildBatchAsync + saves + PDF
//     4. Review   — header / PI rows / DN weights / TI balance /
//                   reconciliation / attachment completeness
//
//   Persists each run as a SavedOrderReport (orderreports.db) so reports
//   can be reopened, re-exported and deleted from the module list.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.MasterData;
using ProGlassAutomation.Models.OrderReport;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.OrderReport;
using ProGlassAutomation.Services.OrderReport.Pdf;
using ProGlassAutomation.Services.OrderReport.Repository;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.Reports.StatementGenerator;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProGlassAutomation.ViewModels.Reports.OrderReport
{
    public class OrderReportGeneratorViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogs;
        private readonly IMasterDataRepository _masterData;
        private readonly IProformaInvoiceRepository _invoices;
        private readonly IOrderReportRepository _repo;
        private readonly OrderReportEngine _engine;

        public OrderReportPreviewViewModel PreviewVM { get; } = new();

        // ── Debounce for auto-preview ─────────────────────────────────────
        private readonly DispatcherTimer _debounceTimer;
        private const int DebounceMs = 400;

        // ── Callbacks (wired by MainViewModel) ────────────────────────────
        public Func<Task>? OnReportSaved { get; set; }
        public Action? OnCancelled { get; set; }

        public Action<string>? OnOpenProformaByNumber { get; set; }
        public Action<string>? OnOpenJobOrderByNumber { get; set; }
        public Action<string>? OnOpenTaxInvoiceByNumber { get; set; }

        public ICommand OpenWarningActionCommand { get; private set; } = null!;

        // ═════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═════════════════════════════════════════════════════════════════

        public OrderReportGeneratorViewModel(
            IDialogService? dialogs,
            IMasterDataRepository? masterData,
            IProformaInvoiceRepository? invoices,
            IOrderReportRepository? repo)
        {
            _dialogs = dialogs ?? new DialogService();
            _masterData = masterData ?? App.MasterData;
            _invoices = invoices ?? App.Invoices;
            _repo = repo ?? new OrderReportRepository();
            _engine = new OrderReportEngine(invoices, null, null, null, null, null);

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceMs) };
            _debounceTimer.Tick += async (s, e) =>
            {
                _debounceTimer.Stop();
                await RunPreviewAsync();
            };

            foreach (var s in new[] { "Draft", "Approved", "Rejected", "Cancelled", "Expired" })
            {
                var item = new StatusCheckItem(s, false);
                item.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(StatusCheckItem.IsChecked))
                    {
                        NotifyFilterDependents();
                        SchedulePreview();
                    }
                };
                StatusItems.Add(item);
            }

            PreviewVM.SelectionChanged += () =>
            {
                OnPropertyChanged(nameof(IsStep2Complete));
                OnPropertyChanged(nameof(CanNavigateToStep3));
                AsyncUtility.InvalidateCommandsThrottled();
            };

            PreviewCommand = new RelayCommand(
                async _ => await RunPreviewAsync(),
                _ => !_isLoading && !_isGenerating && HasFilter);

            GenerateCommand = new RelayCommand(
                async _ => await RunGenerateAsync(),
                _ => !_isLoading && !_isGenerating && HasFilter && PreviewVM.HasSelection);

            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());

            CancelCommand = new RelayCommand(_ => OnCancelled?.Invoke());

            SelectAllCommand = new RelayCommand(_ => PreviewVM.SelectAll(), _ => PreviewVM.Rows.Count > 0);
            DeselectAllCommand = new RelayCommand(_ => PreviewVM.DeselectAll(), _ => PreviewVM.Rows.Count > 0);

            DownloadLastPdfCommand = new RelayCommand(_ => OrderReportPdfExporter.OpenPdf(LastPdfPath), _ => HasLastPdf);

            OpenWarningActionCommand = new RelayCommand(param =>
            {
                if (param is not OrderReportWarning w) return;
                switch (w.ActionKind)
                {
                    case "OpenProforma": OnOpenProformaByNumber?.Invoke(w.TargetId); break;
                    case "OpenJobOrder": OnOpenJobOrderByNumber?.Invoke(w.TargetId); break;
                    case "OpenInvoice": OnOpenTaxInvoiceByNumber?.Invoke(w.TargetId); break;
                }
            });

            NextStepCommand = new RelayCommand(_ =>
            {
                if (_currentStep == 1 && HasFilter)
                {
                    _ = RunPreviewAsync();
                    CurrentStep = 2;
                }
                else if (_currentStep == 2 && PreviewVM.HasSelection)
                {
                    CurrentStep = 3;
                    _ = RunGenerateAsync();
                }
                else if (_currentStep == 4)
                {
                    var saved = OnReportSaved;
                    if (saved != null) _ = saved.Invoke();
                    else OnCancelled?.Invoke();
                }
            }, _ => CanGoNext);

            PreviousStepCommand = new RelayCommand(_ =>
            {
                if (_currentStep > 1) CurrentStep = _currentStep - 1;
            }, _ => CanGoPrevious);

            GoToStepCommand = new RelayCommand(param =>
            {
                if (_isGenerating) return;
                if (!int.TryParse(param?.ToString(), out int target)) return;
                if (target < 1 || target > 4 || target == _currentStep) return;

                bool allowed = target switch
                {
                    1 => CanNavigateToStep1,
                    2 => CanNavigateToStep2,
                    3 => CanNavigateToStep3,
                    4 => CanNavigateToStep4,
                    _ => false
                };

                if (!allowed)
                {
                    StatusMessage = target switch
                    {
                        2 => "Configure at least one filter before viewing Preview.",
                        3 => "Select at least one proforma invoice before generating.",
                        4 => "Generate a report first before viewing Review.",
                        _ => ""
                    };
                    return;
                }

                CurrentStep = target;
                StatusMessage = "";
            });

            _ = LoadMasterDataAsync();
        }

        // ═════════════════════════════════════════════════════════════════
        //  WIZARD STATE
        // ═════════════════════════════════════════════════════════════════

        private int _currentStep = 1;
        public int CurrentStep
        {
            get => _currentStep;
            set
            {
                if (_currentStep == value) return;
                _currentStep = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(ShowNextButton));
                OnPropertyChanged(nameof(ShowGenerateButton));
                OnPropertyChanged(nameof(ShowFinishButton));
                OnPropertyChanged(nameof(IsStep1Active));
                OnPropertyChanged(nameof(IsStep2Active));
                OnPropertyChanged(nameof(IsStep3Active));
                OnPropertyChanged(nameof(IsStep4Active));
                AsyncUtility.InvalidateCommandsThrottled();
            }
        }

        public bool IsStep1Complete => HasFilter;
        public bool IsStep2Complete => PreviewVM.HasSelection;
        public bool IsStep3Complete => HasReport;
        public bool IsStep4Complete => HasLastPdf;

        public bool IsStep1Active => _currentStep == 1;
        public bool IsStep2Active => _currentStep == 2;
        public bool IsStep3Active => _currentStep == 3;
        public bool IsStep4Active => _currentStep == 4;

        public bool CanNavigateToStep1 => !_isGenerating;
        public bool CanNavigateToStep2 => HasFilter && !_isGenerating;
        public bool CanNavigateToStep3 => PreviewVM.HasSelection && !_isGenerating;
        public bool CanNavigateToStep4 => HasReport && !_isGenerating;

        public bool CanGoPrevious => _currentStep > 1 && !_isGenerating;
        public bool CanGoNext => _currentStep < 4 && !_isGenerating;

        public bool ShowNextButton => _currentStep == 1 && !_isGenerating;
        public bool ShowGenerateButton => _currentStep == 2 && !_isGenerating;
        public bool ShowFinishButton => _currentStep == 4;

        public ICommand NextStepCommand { get; private set; } = null!;
        public ICommand PreviousStepCommand { get; private set; } = null!;
        public ICommand GoToStepCommand { get; private set; } = null!;

        // ═════════════════════════════════════════════════════════════════
        //  FILTER STATE
        // ═════════════════════════════════════════════════════════════════

        private string _filterCompany = "";
        public string FilterCompany
        {
            get => _filterCompany;
            set { if (_filterCompany == value) return; _filterCompany = value; OnPropertyChanged(); NotifyFilterDependents(); SchedulePreview(); }
        }

        private string _filterCustomer = "";
        public string FilterCustomer
        {
            get => _filterCustomer;
            set { if (_filterCustomer == value) return; _filterCustomer = value; OnPropertyChanged(); NotifyFilterDependents(); SchedulePreview(); }
        }

        private string _filterProject = "";
        public string FilterProject
        {
            get => _filterProject;
            set { if (_filterProject == value) return; _filterProject = value; OnPropertyChanged(); NotifyFilterDependents(); SchedulePreview(); }
        }

        private string _filterSalesman = "";
        public string FilterSalesman
        {
            get => _filterSalesman;
            set { if (_filterSalesman == value) return; _filterSalesman = value; OnPropertyChanged(); NotifyFilterDependents(); SchedulePreview(); }
        }

        private DateTime? _dateFrom;
        public DateTime? DateFrom
        {
            get => _dateFrom;
            set { if (_dateFrom == value) return; _dateFrom = value; OnPropertyChanged(); NotifyFilterDependents(); SchedulePreview(); }
        }

        private DateTime? _dateTo;
        public DateTime? DateTo
        {
            get => _dateTo;
            set { if (_dateTo == value) return; _dateTo = value; OnPropertyChanged(); NotifyFilterDependents(); SchedulePreview(); }
        }

        public bool HasFilter =>
            !string.IsNullOrWhiteSpace(_filterCompany) ||
            !string.IsNullOrWhiteSpace(_filterCustomer) ||
            !string.IsNullOrWhiteSpace(_filterProject) ||
            !string.IsNullOrWhiteSpace(_filterSalesman) ||
            _dateFrom.HasValue ||
            _dateTo.HasValue ||
            StatusItems.Any(s => s.IsChecked);

        public ObservableCollection<StatusCheckItem> StatusItems { get; } = new();
        public ObservableCollection<string> Companies { get; } = new();
        public ObservableCollection<string> Customers { get; } = new();
        public ObservableCollection<string> Projects { get; } = new();
        public ObservableCollection<string> Salesmen { get; } = new();

        // ═════════════════════════════════════════════════════════════════
        //  CORE STATE
        // ═════════════════════════════════════════════════════════════════

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { if (_isLoading == value) return; _isLoading = value; OnPropertyChanged(); AsyncUtility.InvalidateCommandsThrottled(); }
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
                NotifyAllNavigationStates();
                AsyncUtility.InvalidateCommandsThrottled();
            }
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set { if (_statusMessage == value) return; _statusMessage = value; OnPropertyChanged(); }
        }

        // ── Pipeline / review result ──────────────────────────────────────
        private OrderReportBundle? _lastBundle;
        public OrderReportBundle? LastBundle
        {
            get => _lastBundle;
            private set { _lastBundle = value; OnPropertyChanged(); OnPropertyChanged(nameof(SectionCount)); HasReport = value != null; }
        }

        public int SectionCount => _lastBundle?.SectionCount ?? 0;

        private bool _hasReport;
        public bool HasReport
        {
            get => _hasReport;
            private set { if (_hasReport == value) return; _hasReport = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsStep3Complete)); OnPropertyChanged(nameof(CanNavigateToStep4)); }
        }

        private string _lastPdfPath = "";
        public string LastPdfPath
        {
            get => _lastPdfPath;
            private set { _lastPdfPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasLastPdf)); OnPropertyChanged(nameof(IsStep4Complete)); }
        }

        public bool HasLastPdf => !string.IsNullOrWhiteSpace(_lastPdfPath);

        /// <summary>Flattened warnings across all sections (for Review).</summary>
        public ObservableCollection<OrderReportWarning> Warnings { get; } = new();

        public ICommand PreviewCommand { get; }
        public ICommand GenerateCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand DeselectAllCommand { get; }
        public ICommand DownloadLastPdfCommand { get; }

        // ═════════════════════════════════════════════════════════════════
        //  PREPARE
        // ═════════════════════════════════════════════════════════════════

        public void PrepareNew()
        {
            CurrentStep = 1;
            ClearFilters();
            PreviewVM.UpdateFromList(new List<ProformaListRow>());
            ResetResult();
            StatusMessage = "";
            OnPropertyChanged(string.Empty);
        }

        public void PrepareEdit(OrderReportListItem item)
        {
            CurrentStep = 1;
            _debounceTimer.Stop();

            _filterCompany = item.FilterCompany ?? "";
            _filterCustomer = item.FilterCustomer ?? "";
            _filterProject = item.FilterProject ?? "";
            _filterSalesman = item.FilterSalesman ?? "";
            _dateFrom = item.FilterDateFrom;
            _dateTo = item.FilterDateTo;

            PreviewVM.UpdateFromList(new List<ProformaListRow>());
            ResetResult();
            StatusMessage = $"Opening {item.ReportNumber} — click Preview to regenerate.";
            OnPropertyChanged(string.Empty);
        }

        private void ResetResult()
        {
            LastBundle = null;
            Warnings.Clear();
            LastPdfPath = "";
            StatusMessage = "";
        }

        private void NotifyFilterDependents()
        {
            OnPropertyChanged(nameof(HasFilter));
            OnPropertyChanged(nameof(IsStep1Complete));
            OnPropertyChanged(nameof(CanNavigateToStep2));
        }

        private void NotifyAllNavigationStates()
        {
            OnPropertyChanged(nameof(IsStep1Complete));
            OnPropertyChanged(nameof(IsStep2Complete));
            OnPropertyChanged(nameof(IsStep3Complete));
            OnPropertyChanged(nameof(IsStep4Complete));
            OnPropertyChanged(nameof(CanNavigateToStep1));
            OnPropertyChanged(nameof(CanNavigateToStep2));
            OnPropertyChanged(nameof(CanNavigateToStep3));
            OnPropertyChanged(nameof(CanNavigateToStep4));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(ShowNextButton));
            OnPropertyChanged(nameof(ShowGenerateButton));
            OnPropertyChanged(nameof(ShowFinishButton));
        }

        private void SchedulePreview()
        {
            _debounceTimer.Stop();
            if (!HasFilter)
            {
                PreviewVM.UpdateFromList(new List<ProformaListRow>());
                StatusMessage = "";
                return;
            }
            _debounceTimer.Start();
        }

        private void ClearFilters()
        {
            _debounceTimer.Stop();
            _filterCompany = "";
            _filterCustomer = "";
            _filterProject = "";
            _filterSalesman = "";
            _dateFrom = null;
            _dateTo = null;
            foreach (var s in StatusItems) s.IsChecked = false;

            OnPropertyChanged(nameof(FilterCompany));
            OnPropertyChanged(nameof(FilterCustomer));
            OnPropertyChanged(nameof(FilterProject));
            OnPropertyChanged(nameof(FilterSalesman));
            OnPropertyChanged(nameof(DateFrom));
            OnPropertyChanged(nameof(DateTo));
            NotifyFilterDependents();
            PreviewVM.UpdateFromList(new List<ProformaListRow>());
        }

        // ═════════════════════════════════════════════════════════════════
        //  PREVIEW
        // ═════════════════════════════════════════════════════════════════

        private async Task RunPreviewAsync()
        {
            if (!HasFilter) return;
            IsLoading = true;
            StatusMessage = "Searching proforma invoices…";

            try
            {
                var all = await Task.Run(() => _invoices.GetListRowsAsync(CancellationToken.None));

                var filtered = all.Where(MatchesFilter).ToList();
                PreviewVM.UpdateFromList(filtered);

                StatusMessage = filtered.Count == 0
                    ? "No proforma invoices match the filters."
                    : $"{filtered.Count} proforma invoice(s) match.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Preview failed: {ExceptionMessages.Deep(ex)}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool MatchesFilter(ProformaListRow row)
        {
            if (!string.IsNullOrWhiteSpace(_filterCompany) &&
                !Contains(row.CompanyName, _filterCompany)) return false;
            if (!string.IsNullOrWhiteSpace(_filterCustomer) &&
                !Contains(row.CustomerName, _filterCustomer)) return false;
            if (!string.IsNullOrWhiteSpace(_filterProject) &&
                !Contains(row.ProjectName, _filterProject)) return false;

            if (_dateFrom.HasValue && row.CreatedAt < _dateFrom.Value.Date) return false;
            if (_dateTo.HasValue && row.CreatedAt > _dateTo.Value.Date.AddDays(1).AddTicks(-1)) return false;

            if (StatusItems.Any(s => s.IsChecked) &&
                !StatusItems.Any(s => s.IsChecked && string.Equals(s.Status, row.Status, StringComparison.OrdinalIgnoreCase)))
                return false;

            return true;
        }

        private static bool Contains(string? source, string term) =>
            source?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

        // ═════════════════════════════════════════════════════════════════
        //  GENERATE (pipeline)
        // ═════════════════════════════════════════════════════════════════

        private async Task RunGenerateAsync()
        {
            if (_isGenerating) return;
            var selectedIds = PreviewVM.GetSelectedProformaIds();
            if (selectedIds.Count == 0) return;

            IsGenerating = true;
            StatusMessage = "Building report…";

            try
            {
                var sections = await _engine.BuildBatchAsync(selectedIds);
                var bundle = new OrderReportBundle
                {
                    ReportNumber = await Task.Run(() => _repo.GetNextReportNumberAsync()),
                    Title = BuildTitle(selectedIds.Count),
                    GeneratedAt = DateTime.Now,
                    Sections = sections
                };

                // ── Persist ───────────────────────────────────────────────
                var filter = new OrderReportFilter
                {
                    CompanyName = NullIfEmpty(_filterCompany),
                    CustomerName = NullIfEmpty(_filterCustomer),
                    ProjectName = NullIfEmpty(_filterProject),
                    SalesmanName = NullIfEmpty(_filterSalesman),
                    DateFrom = _dateFrom,
                    DateTo = _dateTo,
                    Statuses = StatusItems.Where(s => s.IsChecked).Select(s => s.Status).ToList()
                };

                var entity = new SavedOrderReport
                {
                    ReportNumber = bundle.ReportNumber,
                    Title = bundle.Title,
                    FilterCompany = _filterCompany,
                    FilterCustomer = _filterCustomer,
                    FilterProject = _filterProject,
                    FilterSalesman = _filterSalesman,
                    FilterDateFrom = _dateFrom,
                    FilterDateTo = _dateTo,
                    ProformaIds = string.Join(",", selectedIds),
                    PiCount = bundle.SectionCount,
                    TotalOrderedQty = bundle.TotalOrderedQty,
                    TotalDeliveredQty = bundle.TotalDeliveredQty,
                    TotalInvoicedQty = bundle.TotalInvoicedQty,
                    MissingDocuments = bundle.TotalMissingDocuments,
                    TotalInvoicedValue = bundle.TotalInvoicedValue,
                    FullSnapshotJson = JsonSerializer.Serialize(bundle),
                    FilterJson = JsonSerializer.Serialize(filter)
                };

                await Task.Run(() => _repo.InsertAsync(entity));

                // ── PDF ───────────────────────────────────────────────────
                string pdfPath = await Task.Run(() => OrderReportPdfExporter.Generate(bundle));
                await Task.Run(() => _repo.UpdatePdfPathAsync(entity.Id, pdfPath));

                // ── Populate review ────────────────────────────────────────
                Warnings.Clear();
                foreach (var w in bundle.Sections.SelectMany(s => s.Warnings))
                    Warnings.Add(w);

                LastBundle = bundle;
                LastPdfPath = pdfPath;
                StatusMessage = $"Report {bundle.ReportNumber} generated ({bundle.SectionCount} order(s)).";

                ProGlassAutomation.ViewModels.Shared.UiDispatch.Run(() =>
                {
                    if (CurrentStep == 3) CurrentStep = 4;
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Generation failed: {ExceptionMessages.Deep(ex)}";
                AppLogService.Error("[OrderReport] generation failed", ex);
            }
            finally
            {
                IsGenerating = false;
            }
        }

        private string BuildTitle(int piCount)
        {
            if (!string.IsNullOrWhiteSpace(_filterCustomer))
                return $"Order Report — {_filterCustomer}";
            if (!string.IsNullOrWhiteSpace(_filterProject))
                return $"Order Report — {_filterProject}";
            if (!string.IsNullOrWhiteSpace(_filterCompany))
                return $"Order Report — {_filterCompany}";
            if (!string.IsNullOrWhiteSpace(_filterSalesman))
                return $"Order Report — {_filterSalesman}";
            return piCount == 1 ? "Single Order Report" : $"{piCount}-Order Report";
        }

        private static string? NullIfEmpty(string s) =>
            string.IsNullOrWhiteSpace(s) ? null : s;

        // ═════════════════════════════════════════════════════════════════
        //  MASTER DATA
        // ═════════════════════════════════════════════════════════════════

        private async Task LoadMasterDataAsync()
        {
            try
            {
                var companies = await Task.Run(() => _masterData.GetAllCompaniesAsync());
                var clients = await Task.Run(() => _masterData.GetAllClientsAsync());
                var projects = await Task.Run(() => _masterData.GetAllProjectsAsync());
                var salesmen = await Task.Run(() => _masterData.GetAllSalesmenAsync());

                ProGlassAutomation.ViewModels.Shared.UiDispatch.Run(() =>
                {
                    Companies.Clear();
                    foreach (var c in companies.Select(c => c.CompanyName)
                        .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n))
                        Companies.Add(c);

                    Customers.Clear();
                    foreach (var c in clients.Select(c => c.CustomerName)
                        .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n))
                        Customers.Add(c);

                    Projects.Clear();
                    foreach (var p in projects.Select(p => p.ProjectIdentifierName)
                        .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n))
                        Projects.Add(p);

                    Salesmen.Clear();
                    foreach (var s in salesmen.Select(s => s.Name)
                        .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n))
                        Salesmen.Add(s);
                });
            }
            catch (Exception ex)
            {
                AppLogService.Error("[OrderReport] master data load failed", ex);
            }
        }
    }

    /// <summary>Filter criteria persisted into SavedOrderReport.FilterJson.</summary>
    public class OrderReportFilter
    {
        public string? CompanyName { get; set; }
        public string? CustomerName { get; set; }
        public string? ProjectName { get; set; }
        public string? SalesmanName { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public List<string> Statuses { get; set; } = new();
    }
}
