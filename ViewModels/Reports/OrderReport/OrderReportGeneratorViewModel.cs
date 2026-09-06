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
using ProGlassAutomation.Models.ProformaInvoice.Common;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.OrderReport;
using ProGlassAutomation.Services.OrderReport.Pdf;
using ProGlassAutomation.Services.OrderReport.Repository;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.Reporting;
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

            // Canonical Proforma statuses (Draft / Sent / Approved / Rejected /
            // Expired / Cancelled) — mirrors ProformaInvoiceStatus.All so the
            // chips cover every state a saved PI can actually be in.
            foreach (var s in ProformaInvoiceStatus.All)
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

            ExportCsvCommand = new RelayCommand(async _ => await ExportSpecsAsync(ExportFormat.Csv),
                _ => HasReport && !_isGenerating);
            ExportExcelCommand = new RelayCommand(async _ => await ExportSpecsAsync(ExportFormat.Excel),
                _ => HasReport && !_isGenerating);
            ExportPdfCommand = new RelayCommand(_ => OrderReportPdfExporter.OpenPdf(LastPdfPath), _ => HasLastPdf);

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
                else if (_currentStep == 3 && HasReport)
                {
                    CurrentStep = 4;
                }
                else if (_currentStep == 4)
                {
                    CurrentStep = 5;
                }
            }, _ => CanGoNext);

            FinishCommand = new RelayCommand(_ =>
            {
                var saved = OnReportSaved;
                if (saved != null) _ = saved.Invoke();
                else OnCancelled?.Invoke();
            }, _ => _currentStep == 5 && !_isGenerating);

            PreviousStepCommand = new RelayCommand(_ =>
            {
                if (_currentStep > 1) CurrentStep = _currentStep - 1;
            }, _ => CanGoPrevious);

            GoToStepCommand = new RelayCommand(param =>
            {
                if (_isGenerating) return;
                if (!int.TryParse(param?.ToString(), out int target)) return;
                if (target < 1 || target > 5 || target == _currentStep) return;

                bool allowed = target switch
                {
                    1 => CanNavigateToStep1,
                    2 => CanNavigateToStep2,
                    3 => CanNavigateToStep3,
                    4 => CanNavigateToStep4,
                    5 => CanNavigateToStep5,
                    _ => false
                };

                if (!allowed)
                {
                    StatusMessage = target switch
                    {
                        2 => "Configure at least one filter before viewing Preview.",
                        3 => "Select at least one proforma invoice before generating.",
                        4 => "Generate a report first before viewing Review.",
                        5 => "Generate a report first before viewing Specifications.",
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
                OnPropertyChanged(nameof(IsStep5Active));
                AsyncUtility.InvalidateCommandsThrottled();
            }
        }

        public bool IsStep1Complete => HasFilter;
        public bool IsStep2Complete => PreviewVM.HasSelection;
        public bool IsStep3Complete => HasReport;
        public bool IsStep4Complete => HasReport;
        public bool IsStep5Complete => HasReport;

        public bool IsStep1Active => _currentStep == 1;
        public bool IsStep2Active => _currentStep == 2;
        public bool IsStep3Active => _currentStep == 3;
        public bool IsStep4Active => _currentStep == 4;
        public bool IsStep5Active => _currentStep == 5;

        public bool CanNavigateToStep1 => !_isGenerating;
        public bool CanNavigateToStep2 => HasFilter && !_isGenerating;
        public bool CanNavigateToStep3 => PreviewVM.HasSelection && !_isGenerating;
        public bool CanNavigateToStep4 => HasReport && !_isGenerating;
        public bool CanNavigateToStep5 => HasReport && !_isGenerating;

        public bool CanGoPrevious => _currentStep > 1 && !_isGenerating;
        public bool CanGoNext => _currentStep < 5 && !_isGenerating;

        // Step 1, Execute-done (only when a report exists) and Review show
        // the "Next Step" button; Step 2 shows "Generate Report"; the last
        // step shows "Finish".
        public bool ShowNextButton =>
            (_currentStep == 1 || (_currentStep == 3 && HasReport) || _currentStep == 4) && !_isGenerating;
        public bool ShowGenerateButton => _currentStep == 2 && !_isGenerating;
        public bool ShowFinishButton => _currentStep == 5;

        public ICommand NextStepCommand { get; private set; } = null!;
        public ICommand PreviousStepCommand { get; private set; } = null!;
        public ICommand GoToStepCommand { get; private set; } = null!;
        public ICommand FinishCommand { get; private set; } = null!;

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
            set
            {
                if (_filterCustomer == value) return;
                _filterCustomer = value;
                OnPropertyChanged();
                // Project dropdown must reflect the chosen customer.
                RefreshProjectsForCustomer();
                NotifyFilterDependents();
                SchedulePreview();
            }
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
            set
            {
                if (_filterSalesman == value) return;
                _filterSalesman = value;
                OnPropertyChanged();
                // Customer dropdown must reflect the chosen salesman.
                RefreshCustomersForSalesman();
                NotifyFilterDependents();
                SchedulePreview();
            }
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

        // ── Live match count (updates as filters change) ──────────────────
        private int _matchCount;
        public int MatchCount
        {
            get => _matchCount;
            private set { if (_matchCount == value) return; _matchCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(MatchCountDisplay)); }
        }

        public string MatchCountDisplay =>
            !HasFilter
                ? "Configure filters to see matching proforma invoices."
                : IsLoading
                    ? "Searching…"
                    : _matchCount == 0
                        ? "0 proforma invoices match"
                        : _matchCount == 1
                            ? "1 proforma invoice matches"
                            : $"{_matchCount:N0} proforma invoices match";

        public ObservableCollection<StatusCheckItem> StatusItems { get; } = new();
        public ObservableCollection<string> Companies { get; } = new();
        public ObservableCollection<string> Customers { get; } = new();
        public ObservableCollection<string> Projects { get; } = new();
        public ObservableCollection<string> Salesmen { get; } = new();

        // Full client / project lists (name + owning salesman / customer) so
        // the Customer and Project dropdowns can be scoped exactly like the
        // Statement Generator. Loaded once in LoadMasterDataAsync.
        private List<SavedClient> _allClients = new();
        private List<SavedProject> _allProjects = new();

        // "Our company" — the legal entity this business issues documents
        // under. Resolved from the Company module records (IsDefault, else
        // the most-used company) and pre-selected so Step 1 is always scoped
        // to our own company by default.
        private string _ourCompany = "";

        // ═════════════════════════════════════════════════════════════════
        //  CORE STATE
        // ═════════════════════════════════════════════════════════════════

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading == value) return;
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MatchCountDisplay));
                AsyncUtility.InvalidateCommandsThrottled();
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
            private set
            {
                _lastBundle = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SectionCount));
                HasReport = value != null;
                RebuildSpecificationGroups();
            }
        }

        public int SectionCount => _lastBundle?.SectionCount ?? 0;

        private bool _hasReport;
        public bool HasReport
        {
            get => _hasReport;
            private set
            {
                if (_hasReport == value) return;
                _hasReport = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsStep3Complete));
                OnPropertyChanged(nameof(IsStep4Complete));
                OnPropertyChanged(nameof(IsStep5Complete));
                OnPropertyChanged(nameof(CanNavigateToStep4));
                OnPropertyChanged(nameof(CanNavigateToStep5));
            }
        }

        private string _lastPdfPath = "";
        public string LastPdfPath
        {
            get => _lastPdfPath;
            private set { _lastPdfPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasLastPdf)); }
        }

        public bool HasLastPdf => !string.IsNullOrWhiteSpace(_lastPdfPath);

        /// <summary>Flattened warnings across all sections (for Review).</summary>
        public ObservableCollection<OrderReportWarning> Warnings { get; } = new();

        /// <summary>
        /// Step 5 "Specifications" breakdown — one "PI n" group per selected
        /// Proforma, each containing "Spec n — specification" groups with row
        /// data and column-aligned footer totals. Rebuilt when a report lands.
        /// </summary>
        public ObservableCollection<OrderReportSpecPiGroup> SpecificationGroups { get; } = new();

        public ICommand PreviewCommand { get; }
        public ICommand GenerateCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand DeselectAllCommand { get; }
        public ICommand DownloadLastPdfCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ExportPdfCommand { get; }

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

            _filterCompany = string.IsNullOrWhiteSpace(item.FilterCompany) ? _ourCompany : item.FilterCompany;
            _filterCustomer = item.FilterCustomer ?? "";
            _filterProject = item.FilterProject ?? "";
            _filterSalesman = item.FilterSalesman ?? "";
            _dateFrom = item.FilterDateFrom;
            _dateTo = item.FilterDateTo;

            // Re-scope dropdowns to the restored salesman/customer.
            RefreshCustomersForSalesman();
            RefreshProjectsForCustomer();

            MatchCount = 0;
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
            OnPropertyChanged(nameof(MatchCountDisplay));
            OnPropertyChanged(nameof(IsStep1Complete));
            OnPropertyChanged(nameof(CanNavigateToStep2));
        }

        private void NotifyAllNavigationStates()
        {
            OnPropertyChanged(nameof(IsStep1Complete));
            OnPropertyChanged(nameof(IsStep2Complete));
            OnPropertyChanged(nameof(IsStep3Complete));
            OnPropertyChanged(nameof(IsStep4Complete));
            OnPropertyChanged(nameof(IsStep5Complete));
            OnPropertyChanged(nameof(CanNavigateToStep1));
            OnPropertyChanged(nameof(CanNavigateToStep2));
            OnPropertyChanged(nameof(CanNavigateToStep3));
            OnPropertyChanged(nameof(CanNavigateToStep4));
            OnPropertyChanged(nameof(CanNavigateToStep5));
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
            // Company is context, not a filter: resetting always returns to
            // OUR company (Step 1 selected by default).
            _filterCompany = _ourCompany;
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

            // Dropdown scoping returns to "everything".
            RefreshCustomersForSalesman();
            RefreshProjectsForCustomer();

            MatchCount = 0;
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

                // Salesman lives inside the PI snapshot (Client.SalesRepresentative),
                // not in the narrow list projection — apply it as a post-filter,
                // mirroring StatementAggregator.ApplySalesmanFilterAsync.
                if (!string.IsNullOrWhiteSpace(_filterSalesman))
                    filtered = await ApplySalesmanFilterAsync(filtered);

                PreviewVM.UpdateFromList(filtered);
                MatchCount = filtered.Count;

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

            // Filter on the real PI issue date; fall back to CreatedAt for
            // legacy rows whose IssueDate was never set.
            DateTime piDate = row.IssueDate == default ? row.CreatedAt : row.IssueDate;
            if (_dateFrom.HasValue && piDate < _dateFrom.Value.Date) return false;
            if (_dateTo.HasValue && piDate > _dateTo.Value.Date.AddDays(1).AddTicks(-1)) return false;

            if (StatusItems.Any(s => s.IsChecked) &&
                !StatusItems.Any(s => s.IsChecked && string.Equals(s.Status, row.Status, StringComparison.OrdinalIgnoreCase)))
                return false;

            return true;
        }

        /// <summary>
        /// Keeps only PIs whose snapshot sales representative (or prepared-by)
        /// matches the chosen salesman. Reads each candidate PI's snapshot
        /// JSON, exactly like StatementAggregator.SearchSalesmanInProformaInvoice.
        /// </summary>
        private async Task<List<ProformaListRow>> ApplySalesmanFilterAsync(List<ProformaListRow> rows)
        {
            if (rows.Count == 0) return rows;
            string needle = _filterSalesman.Trim();

            var result = new List<ProformaListRow>(rows.Count);
            const int chunk = 8;

            for (int i = 0; i < rows.Count; i += chunk)
            {
                var batch = rows.Skip(i).Take(chunk).ToList();
                var tasks = batch.Select(async row =>
                {
                    try
                    {
                        var pi = await _invoices.GetByProformaIdAsync(row.ProformaId);
                        if (pi == null) return (row, false);

                        if (string.IsNullOrWhiteSpace(pi.FullSnapshotJson) || pi.FullSnapshotJson == "{}")
                            return (row, false);

                        using var doc = JsonDocument.Parse(pi.FullSnapshotJson);
                        var root = doc.RootElement;

                        return (row, Contains(TryReadString(root, "Client", "SalesRepresentative"), needle) ||
                                     Contains(TryReadString(root, "PreparedBy", "Name"), needle) ||
                                     Contains(TryReadString(root, "Client", "DocumentPreparedBy"), needle));
                    }
                    catch (Exception ex)
                    {
                        AppLogService.Debug($"[OrderReport] Salesman search error on {row.ProformaId}: {ex.Message}");
                        return (row, false);
                    }
                });

                var chunkResults = await Task.WhenAll(tasks);
                result.AddRange(chunkResults.Where(r => r.Item2).Select(r => r.Item1));
            }

            return result;
        }

        private static string? TryReadString(System.Text.Json.JsonElement root, string obj, string prop)
        {
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
            if (!root.TryGetProperty(obj, out var objEl) || objEl.ValueKind != System.Text.Json.JsonValueKind.Object)
                return null;
            if (!objEl.TryGetProperty(prop, out var propEl) || propEl.ValueKind != System.Text.Json.JsonValueKind.String)
                return null;
            return propEl.GetString();
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
        //  STEP 5 — SPECIFICATIONS (grouped breakdown + export)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Rebuilds the "PI n → Spec n — specification" grouping used by the
        /// Step 5 screen from the current bundle. Each spec group carries
        /// footer totals so the UI/PDF can place the sum row exactly under
        /// the Qty / SQM / Amount columns.
        /// </summary>
        private void RebuildSpecificationGroups()
        {
            SpecificationGroups.Clear();
            if (_lastBundle == null) return;

            int piIndex = 0;
            foreach (var section in _lastBundle.Sections)
            {
                piIndex++;

                var piGroup = new OrderReportSpecPiGroup
                {
                    Index = piIndex,
                    ProformaId = section.Header.ProformaId,
                    PiStatus = section.Header.PiStatus,
                    CustomerName = section.Header.CustomerName,
                    ProjectName = section.Header.ProjectName,
                    IssueDate = section.Header.IssueDate
                };

                foreach (var g in section.SpecRows
                    .GroupBy(r => r.GroupNumber)
                    .OrderBy(g => g.Key))
                {
                    var rows = g.OrderBy(r => r.SerialNumber).ToList();

                    var spec = new OrderReportSpecGroup
                    {
                        GroupNumber = g.Key,
                        GroupName = rows[0].GroupName,
                        SpecName = rows[0].SpecName,
                        GlassType = rows[0].GlassType,
                        Rows = rows,
                        TotalQty = rows.Sum(r => r.Qty),
                        TotalSqm = Math.Round(rows.Sum(r => r.TotalSqm), 3),
                        TotalValue = rows.Sum(r => r.Amount),
                        TotalDeliveredQty = rows.Sum(r => r.DeliveredQty),
                        TotalBalanceQty = rows.Sum(r => r.BalanceQty)
                    };

                    piGroup.Groups.Add(spec);
                }

                piGroup.TotalQty = piGroup.Groups.Sum(g => g.TotalQty);
                piGroup.TotalSqm = Math.Round(piGroup.Groups.Sum(g => g.TotalSqm), 3);
                piGroup.TotalValue = piGroup.Groups.Sum(g => g.TotalValue);
                piGroup.TotalDeliveredQty = piGroup.Groups.Sum(g => g.TotalDeliveredQty);
                piGroup.TotalBalanceQty = piGroup.Groups.Sum(g => g.TotalBalanceQty);

                SpecificationGroups.Add(piGroup);
            }
        }

        /// <summary>Flattens every specification row in the bundle for export.</summary>
        private List<OrderReportSpecExportRow> BuildSpecExportRows()
        {
            var rows = new List<OrderReportSpecExportRow>();
            if (_lastBundle == null) return rows;

            foreach (var section in _lastBundle.Sections)
            {
                foreach (var group in section.SpecRows.GroupBy(r => r.GroupNumber).OrderBy(g => g.Key))
                {
                    var first = group.First();
                    string spec = !string.IsNullOrWhiteSpace(first.SpecName) ? first.SpecName
                                : !string.IsNullOrWhiteSpace(first.GroupName) ? first.GroupName
                                : first.GlassType;

                    foreach (var r in group.OrderBy(r => r.SerialNumber))
                    {
                        rows.Add(new OrderReportSpecExportRow
                        {
                            ProformaId = section.Header.ProformaId,
                            GroupNumber = r.GroupNumber,
                            Specification = spec,
                            SerialNumber = r.SerialNumber,
                            GlassRef = r.GlassRef,
                            GlassType = r.GlassType,
                            W1 = r.W1, H1 = r.H1, W2 = r.W2, H2 = r.H2,
                            Qty = r.Qty,
                            TotalSqm = r.TotalSqm,
                            Rate = r.Rate,
                            Amount = r.Amount,
                            DeliveredQty = r.DeliveredQty,
                            BalanceQty = r.BalanceQty,
                            WeightKg = r.TotalWeight
                        });
                    }
                }
            }

            return rows;
        }

        /// <summary>Exports the Step 5 specifications table as CSV or Excel.</summary>
        private async Task ExportSpecsAsync(ExportFormat format)
        {
            var rows = BuildSpecExportRows();
            string title = _lastBundle?.Title ?? "Order Report Specifications";

            try
            {
                ExportResult result = format == ExportFormat.Csv
                    ? await ReportingExportService.ExportCsvAsync(
                        rows, title, OrderReportSpecExportRow.Headers, r => r.ToCsvRow())
                    : await ReportingExportService.ExportExcelAsync(
                        rows, title, OrderReportSpecExportRow.Headers, r => r.ToExcelRow());

                if (result.Success)
                    StatusMessage = $"Exported {format} — {result.SavedPath}";
                else if (!result.Cancelled)
                    StatusMessage = $"Export failed: {result.ErrorMessage}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ExceptionMessages.Deep(ex)}";
                AppLogService.Error($"[OrderReport] {format} export failed", ex);
            }
        }

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
                    // ── Company (source sequence step 01) ────────────────
                    // Preserve repository order (UsageCount DESC) so OUR
                    // company naturally sits first in the dropdown.
                    var companyNames = companies
                        .Select(c => c.CompanyName?.Trim() ?? "")
                        .Where(n => n.Length > 0)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    Companies.Clear();
                    foreach (var n in companyNames) Companies.Add(n);

                    // Resolve "our company": the default-marked profile, else
                    // the most-used profile (first row from the repository).
                    var ourCompany = companies.FirstOrDefault(c => c.IsDefault)
                                  ?? companies.FirstOrDefault();
                    _ourCompany = !string.IsNullOrWhiteSpace(ourCompany?.CompanyName)
                        ? ourCompany.CompanyName.Trim()
                        : companyNames.FirstOrDefault() ?? "";

                    // Keep the full lists so the Customer dropdown can be
                    // scoped to the chosen salesman and the Project dropdown
                    // to the chosen customer (same as Statement Generator).
                    _allClients = clients ?? new List<SavedClient>();
                    _allProjects = projects ?? new List<SavedProject>();

                    Salesmen.Clear();
                    foreach (var s in salesmen.Select(s => s.Name)
                        .Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n))
                        Salesmen.Add(s);

                    RefreshCustomersForSalesman();
                    RefreshProjectsForCustomer();

                    // Pre-select our company (Step 1 "selected by default").
                    // Only when nothing else was restored yet; the property
                    // setter triggers the dependents + schedules the preview.
                    if (string.IsNullOrWhiteSpace(_filterCompany) && _ourCompany.Length > 0)
                        FilterCompany = _ourCompany;
                });
            }
            catch (Exception ex)
            {
                AppLogService.Error("[OrderReport] master data load failed", ex);
            }
        }

        /// <summary>
        /// Scopes the Customer dropdown to the chosen salesman (all when none),
        /// clearing a now-invalid selection. Mirrors StatementGeneratorViewModel.
        /// </summary>
        private void RefreshCustomersForSalesman()
        {
            string salesman = _filterSalesman?.Trim() ?? "";

            IEnumerable<string> names = _allClients
                .Select(c => c.CustomerName ?? "")
                .Where(n => !string.IsNullOrWhiteSpace(n));

            if (salesman.Length > 0)
            {
                names = _allClients
                    .Where(c => string.Equals(c.SalesRepresentative?.Trim() ?? "", salesman,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.CustomerName ?? "")
                    .Where(n => !string.IsNullOrWhiteSpace(n));
            }

            var ordered = names.Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Customers.Clear();
            foreach (var n in ordered) Customers.Add(n);

            if (salesman.Length > 0 && !string.IsNullOrWhiteSpace(_filterCustomer))
            {
                bool stillValid = _allClients.Any(c =>
                    string.Equals(c.CustomerName?.Trim() ?? "", _filterCustomer.Trim(),
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.SalesRepresentative?.Trim() ?? "", salesman,
                        StringComparison.OrdinalIgnoreCase));

                if (!stillValid)
                {
                    _filterCustomer = "";
                    OnPropertyChanged(nameof(FilterCustomer));
                }
            }
        }

        /// <summary>
        /// Scopes the Project dropdown to the chosen customer (all when none),
        /// clearing a now-invalid selection. Project display matches what the
        /// PI stores in its ProjectName column (identifier, then reference).
        /// </summary>
        private void RefreshProjectsForCustomer()
        {
            string customer = _filterCustomer?.Trim() ?? "";

            IEnumerable<string> names = _allProjects
                .Select(ProjectDisplayName)
                .Where(n => !string.IsNullOrWhiteSpace(n));

            if (customer.Length > 0)
            {
                names = _allProjects
                    .Where(p => string.Equals(p.CustomerName?.Trim() ?? "", customer,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(ProjectDisplayName)
                    .Where(n => !string.IsNullOrWhiteSpace(n));
            }

            var ordered = names.Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Projects.Clear();
            foreach (var n in ordered) Projects.Add(n);

            if (customer.Length > 0 && !string.IsNullOrWhiteSpace(_filterProject))
            {
                string sel = _filterProject.Trim();
                bool stillValid = _allProjects.Any(p =>
                    string.Equals(ProjectDisplayName(p), sel, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.CustomerName?.Trim() ?? "", customer, StringComparison.OrdinalIgnoreCase));

                if (!stillValid)
                {
                    _filterProject = "";
                    OnPropertyChanged(nameof(FilterProject));
                }
            }
        }

        private static string ProjectDisplayName(SavedProject p) =>
            !string.IsNullOrWhiteSpace(p.ProjectIdentifierName)
                ? p.ProjectIdentifierName.Trim()
                : (p.ProjectReference?.Trim() ?? "");
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
