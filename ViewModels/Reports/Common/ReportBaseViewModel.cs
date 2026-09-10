// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/Common/ReportBaseViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — ViewModel Base
//
// PURPOSE:
//   Shared behavior for all 10 report ViewModels: filtering, pagination,
//   async loading with cancellation, error/empty/loading states, and
//   export commands that respect current filters.
//
// DESIGN:
//   • Filter is a single ReportingFilter — same object across reports.
//   • LoadAsync is abstract — each report calls its own service method.
//   • Pagination is handled by the service; VM only tracks Page/PageSize.
//   • Exports use ReportingExportService with the CURRENT filtered items.
//   • No business logic duplication — all queries go through IReportingDataService.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.FileSystem;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.Services.Sharing;
using System;
using ProGlassAutomation.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Reports.Common
{
    public abstract class ReportBaseViewModel : ViewModelBase
    {
        protected readonly IReportingDataService _service;
        private readonly IDatabaseServices _database = null!;

        protected ReportBaseViewModel(IReportingDataService service, IDatabaseServices? database)
        {
            _service = service ?? new ReportingDataService();
            _database = database!;

            // LIVE DATA: every report screen reloads itself whenever any
            // document changes anywhere in the app (PI / JO / DN / TI /
            // statement writes raise DocumentLifecycle). This is the same
            // cross-module signal the document list pages subscribe to —
            // without it each report was stale until the user pressed
            // Refresh. Debounced so a burst of writes (e.g. a PI save that
            // raises the signal several times) triggers ONE reload, and the
            // reload never lands on the user's critical path.
            _database.SubscribeDocumentsChanged(ReloadOnDataChangedAsync);
        }

        // ── Filter ────────────────────────────────────────────────────────
        private ReportingFilter _filter = new();
        public ReportingFilter Filter
        {
            get => _filter;
            set { if (_filter == value) return; _filter = value ?? new ReportingFilter(); OnPropertyChanged(); }
        }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSearchText));
                // Debounced reload
                _ = DebouncedReloadAsync();
            }
        }
        public bool HasSearchText => !string.IsNullOrWhiteSpace(_searchText);

        private DateTime? _dateFrom;
        public DateTime? DateFrom
        {
            get => _dateFrom;
            set { if (_dateFrom == value) return; _dateFrom = value; OnPropertyChanged(); ReloadNow(); }
        }

        private DateTime? _dateTo;
        public DateTime? DateTo
        {
            get => _dateTo;
            set { if (_dateTo == value) return; _dateTo = value; OnPropertyChanged(); ReloadNow(); }
        }

        private string _selectedCompany = "";
        public string SelectedCompany
        {
            get => _selectedCompany;
            set { if (_selectedCompany == value) return; _selectedCompany = value; OnPropertyChanged(); ReloadNow(); }
        }

        private string _selectedCustomer = "";
        public string SelectedCustomer
        {
            get => _selectedCustomer;
            set { if (_selectedCustomer == value) return; _selectedCustomer = value; OnPropertyChanged(); ReloadNow(); }
        }

        private string _selectedProject = "";
        public string SelectedProject
        {
            get => _selectedProject;
            set { if (_selectedProject == value) return; _selectedProject = value; OnPropertyChanged(); ReloadNow(); }
        }

        private string _selectedSalesman = "";
        public string SelectedSalesman
        {
            get => _selectedSalesman;
            set { if (_selectedSalesman == value) return; _selectedSalesman = value; OnPropertyChanged(); ReloadNow(); }
        }

        private string _selectedStatus = "";
        public string SelectedStatus
        {
            get => _selectedStatus;
            set { if (_selectedStatus == value) return; _selectedStatus = value; OnPropertyChanged(); ReloadNow(); }
        }

        // ── Pagination ────────────────────────────────────────────────────
        private int _page = 1;
        public int Page { get => _page; set { if (_page == value) return; _page = Math.Max(1, value); OnPropertyChanged(); OnPropertyChanged(nameof(PageDisplay)); _ = LoadAsync(); } }
        private int _pageSize = 100;
        public int PageSize { get => _pageSize; set { if (_pageSize == value) return; _pageSize = Math.Max(10, value); OnPropertyChanged(); _ = LoadAsync(); } }
        private int _totalCount;
        public int TotalCount { get => _totalCount; protected set { if (_totalCount == value) return; _totalCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(PageDisplay)); OnPropertyChanged(nameof(HasNextPage)); OnPropertyChanged(nameof(HasPrevPage)); } }
        private int _totalPages = 1;
        public int TotalPages { get => _totalPages; protected set { if (_totalPages == value) return; _totalPages = value; OnPropertyChanged(); OnPropertyChanged(nameof(PageDisplay)); } }
        public string PageDisplay => $"Page {Page} of {TotalPages}  •  {TotalCount} record(s)";
        public bool HasNextPage => Page < TotalPages;
        public bool HasPrevPage => Page > 1;

        // ── Sort ──────────────────────────────────────────────────────────
        // Each concrete report calls SetSortOptions(...) with the user-facing
        // sort keys it supports and a default. Changing the selection or the
        // direction reloads from page 1 (sort is applied server-side for
        // paged reports and in-VM for the in-memory aggregate reports).
        private List<string> _sortOptions = new();
        public List<string> SortOptions { get => _sortOptions; protected set { _sortOptions = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSortOptions)); } }
        public bool HasSortOptions => _sortOptions.Count > 0;

        private string _selectedSort = "";
        public string SelectedSort
        {
            get => _selectedSort;
            set { if (_selectedSort == value) return; _selectedSort = value ?? ""; OnPropertyChanged(); ReloadNow(); }
        }

        private bool _sortDescending = true;
        public bool SortDescending
        {
            get => _sortDescending;
            set { if (_sortDescending == value) return; _sortDescending = value; OnPropertyChanged(); OnPropertyChanged(nameof(SortDirectionGlyph)); OnPropertyChanged(nameof(SortDirectionTooltip)); ReloadNow(); }
        }

        /// <summary>Segoe MDL2 glyph reflecting the current sort direction.</summary>
        public string SortDirectionGlyph => _sortDescending ? "\uE74B" : "\uE74A"; // SortDown / SortUp
        public string SortDirectionTooltip => _sortDescending ? "Descending (click for ascending)" : "Ascending (click for descending)";

        public ICommand ToggleSortDirectionCommand { get; protected set; } = null!;

        /// <summary>
        /// Registers the sort keys this report supports. The first key (or the
        /// supplied default) becomes the active selection without triggering a
        /// reload (setup happens before the first load).
        /// </summary>
        protected void SetSortOptions(IEnumerable<string> options, string? defaultOption = null, bool defaultDescending = true)
        {
            var list = (options ?? Enumerable.Empty<string>()).ToList();
            SortOptions = list;
            _sortDescending = defaultDescending;
            _selectedSort = defaultOption ?? list.FirstOrDefault() ?? "";
            OnPropertyChanged(nameof(SelectedSort));
            OnPropertyChanged(nameof(SortDescending));
            OnPropertyChanged(nameof(SortDirectionGlyph));
            OnPropertyChanged(nameof(SortDirectionTooltip));
        }

        // ── State ─────────────────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading { get => _isLoading; protected set { if (_isLoading == value) return; _isLoading = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(IsContentVisible)); } }
        private bool _hasError;
        public bool HasError { get => _hasError; protected set { if (_hasError == value) return; _hasError = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(IsContentVisible)); } }
        private string _errorMessage = "";
        public string ErrorMessage { get => _errorMessage; protected set { if (_errorMessage == value) return; _errorMessage = value; OnPropertyChanged(); } }
        public bool IsEmpty => !IsLoading && !HasError && TotalCount == 0;
        public bool IsContentVisible => !IsLoading && !HasError && TotalCount > 0;
        private bool _isLoaded;
        public bool IsLoaded { get => _isLoaded; protected set { if (_isLoaded == value) return; _isLoaded = value; OnPropertyChanged(); } }

        // ── Export state ──────────────────────────────────────────────────
        private bool _isExporting;
        public bool IsExporting { get => _isExporting; protected set { if (_isExporting == value) return; _isExporting = value; OnPropertyChanged(); } }

        // ── Cancellation ──────────────────────────────────────────────────
        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _debounceCts;
        private CancellationTokenSource? _lifecycleCts;

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand ApplyFilterCommand { get; protected set; } = null!;
        public ICommand ClearFilterCommand { get; protected set; } = null!;
        public ICommand RefreshCommand { get; protected set; } = null!;
        public ICommand NextPageCommand { get; protected set; } = null!;
        public ICommand PrevPageCommand { get; protected set; } = null!;
        public ICommand ExportCsvCommand { get; protected set; } = null!;
        public ICommand ExportExcelCommand { get; protected set; } = null!;
        public ICommand ExportPdfCommand { get; protected set; } = null!;
        public ICommand ShareCommand { get; protected set; } = null!;

        protected virtual void InitCommands()
        {
            ApplyFilterCommand = new RelayCommand(_ => { Page = 1; _ = LoadAsync(); });
            ClearFilterCommand = new RelayCommand(_ => ClearFilters());
            RefreshCommand = new RelayCommand(async _ => await LoadAsync());
            NextPageCommand = new RelayCommand(_ => { if (HasNextPage) Page++; }, _ => HasNextPage);
            PrevPageCommand = new RelayCommand(_ => { if (HasPrevPage) Page--; }, _ => HasPrevPage);
            ToggleSortDirectionCommand = new RelayCommand(_ => SortDescending = !SortDescending);
            ExportCsvCommand = new RelayCommand(async _ => await ExportAsync(ExportFormat.Csv));
            ExportExcelCommand = new RelayCommand(async _ => await ExportAsync(ExportFormat.Excel));
            ExportPdfCommand = new RelayCommand(async _ => await ExportAsync(ExportFormat.Pdf));
            ShareCommand = new RelayCommand(async _ => await ShareAsync());
        }

        protected void ClearFilters()
        {
            SearchText = "";
            DateFrom = null; DateTo = null;
            SelectedCompany = ""; SelectedCustomer = ""; SelectedProject = ""; SelectedSalesman = ""; SelectedStatus = "";
            Filter = new ReportingFilter();
            Page = 1;
            _ = LoadAsync();
        }

        protected virtual ReportingFilter BuildFilter()
        {
            return new ReportingFilter
            {
                SearchText = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                DateFrom = DateFrom,
                DateTo = DateTo,
                Company = string.IsNullOrWhiteSpace(SelectedCompany) ? null : SelectedCompany.Trim(),
                Customer = string.IsNullOrWhiteSpace(SelectedCustomer) ? null : SelectedCustomer.Trim(),
                Project = string.IsNullOrWhiteSpace(SelectedProject) ? null : SelectedProject.Trim(),
                Salesman = string.IsNullOrWhiteSpace(SelectedSalesman) ? null : SelectedSalesman.Trim(),
                Status = string.IsNullOrWhiteSpace(SelectedStatus) ? null : SelectedStatus.Trim(),
                SortField = string.IsNullOrWhiteSpace(SelectedSort) ? null : SelectedSort.Trim(),
                SortDescending = SortDescending,
                Page = Page,
                PageSize = PageSize
            };
        }

        /// <summary>
        /// Reloads IMMEDIATELY (no debounce) for dropdown and date changes —
        /// these fire once per user action, so there is no need to wait. Only
        /// free-text search is debounced (it fires per keystroke). Cancels any
        /// pending debounce so a stale reload never lands after this one.
        /// </summary>
        protected void ReloadNow()
        {
            _debounceCts?.Cancel();
            Page = 1;
            _ = LoadAsync();
        }

        protected async Task DebouncedReloadAsync()
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;
            try
            {
                await Task.Delay(120, token);
                if (token.IsCancellationRequested) return;
                Page = 1;
                await LoadAsync();
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ExceptionMessages.Deep(ex)}"); }
        }

        /// <summary>
        /// Fired by DocumentLifecycle when any document changes. Debounced
        /// (400ms) so consecutive raises coalesce into one reload. Keeps the
        /// user's current page — identical semantics to pressing Refresh.
        /// The concrete LoadAsync already cancels any in-flight load via its
        /// own token, so overlapping reloads cannot corrupt state.
        /// </summary>
        private async Task ReloadOnDataChangedAsync()
        {
            _lifecycleCts?.Cancel();
            _lifecycleCts = new CancellationTokenSource();
            var token = _lifecycleCts.Token;
            try
            {
                await Task.Delay(200, token);
                if (token.IsCancellationRequested) return;
                await LoadAsync();
            }
            catch (TaskCanceledException)
            {
                // Superseded by a newer change — nothing to do.
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"Report auto-reload failed: {ExceptionMessages.Deep(ex)}");
            }
        }

        public abstract Task LoadAsync();

        protected abstract Task ExportAsync(ExportFormat format);

        // ── Sharing Center ──────────────────────────────────────────
        // Each report keeps its own row mapping in ExportCoreAsync; the
        // base only stages that same core into the share temp folder
        // (rule's default format) and opens the composer. Recipients
        // resolve from the report's current customer/company filters.

        /// <summary>The report's display title (composer + templates).</summary>
        protected abstract string ShareReportTitle { get; }

        /// <summary>Failure dialogs; defaults to the app dialog service.</summary>
        protected virtual IDialogService? ShareDialogs => null;

        /// <summary>
        /// Runs the report's EXISTING export core into the share temp
        /// folder. Returns the staged path, or null when there is nothing
        /// to share. Implemented per report (4 lines each).
        /// </summary>
        protected abstract Task<string?> ExportStagedAsync(
            ExportFormat format, string folder);

        /// <summary>
        /// Builds a no-dialog staged request for the temp folder. The
        /// engine writes OutputPath verbatim, so the extension is added.
        /// </summary>
        protected static ExportRequest StagedRequest(
            string folder, string suggestedBaseName, ExportFormat format)
        {
            return new ExportRequest
            {
                OutputPath = Path.Combine(folder,
                    suggestedBaseName + "." + ExportFileNamer.Extension(format)),
                PromptForLocation = false,
                OpenAfterSave = false
            };
        }

        protected async Task ShareAsync()
        {
            if (IsExporting) return;

            string ruleFormat = App.Config.Sharing
                .RuleFor("Report").DefaultFormat ?? "Pdf";
            ExportFormat format = ruleFormat switch
            {
                "Excel" => ExportFormat.Excel,
                "Csv" => ExportFormat.Csv,
                _ => ExportFormat.Pdf
            };

            IsExporting = true;
            try
            {
                var context = ShareContextFactory.FromListReport(
                    "Report", ShareReportTitle, "Reports",
                    SelectedCustomer, SelectedCompany);
                await ShareLauncher.GenerateAndShareAsync(
                    context,
                    folder => ExportStagedAsync(format, folder),
                    ShareDialogs);
            }
            finally { IsExporting = false; }
        }

        protected void CancelPrevious()
        {
            try { _cts?.Cancel(); _cts?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ExceptionMessages.Deep(ex)}"); }
            _cts = new CancellationTokenSource();
        }

        protected CancellationToken LoadToken => _cts?.Token ?? CancellationToken.None;

        // ── Filter option lists ───────────────────────────────────────────

        private List<string> _companyOptions = new();
        public List<string> CompanyOptions { get => _companyOptions; protected set { _companyOptions = value; OnPropertyChanged(); } }
        private List<string> _customerOptions = new();
        public List<string> CustomerOptions { get => _customerOptions; protected set { _customerOptions = value; OnPropertyChanged(); } }
        private List<string> _projectOptions = new();
        public List<string> ProjectOptions { get => _projectOptions; protected set { _projectOptions = value; OnPropertyChanged(); } }
        private List<string> _salesmanOptions = new();
        public List<string> SalesmanOptions { get => _salesmanOptions; protected set { _salesmanOptions = value; OnPropertyChanged(); } }

        // ── Data-driven status filter ─────────────────────────────────────
        // Populated from DISTINCT statuses actually stored in the DB (never
        // a hardcoded list that drifts from real data). "All" is a sentinel
        // the concrete BuildFilter maps back to null (no status filter).
        public const string AllStatusesOption = "All";

        private List<string> _statusOptions = new();
        public List<string> StatusOptions { get => _statusOptions; protected set { _statusOptions = value; OnPropertyChanged(); } }

        /// <summary>Prepends the "All" sentinel and assigns the options.</summary>
        protected void SetStatusOptions(IEnumerable<string> distinctStatuses)
        {
            var list = new List<string> { AllStatusesOption };
            list.AddRange(distinctStatuses ?? Enumerable.Empty<string>());
            StatusOptions = list;
        }

        protected async Task LoadFilterOptionsAsync()
        {
            try
            {
                CompanyOptions = await _service.GetCompanyOptionsAsync();
                CustomerOptions = await _service.GetCustomerOptionsAsync();
                ProjectOptions = await _service.GetProjectOptionsAsync();
                SalesmanOptions = await _service.GetSalesmanOptionsAsync();
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ExceptionMessages.Deep(ex)}"); }
        }
    }
}
