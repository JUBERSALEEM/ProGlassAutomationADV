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
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Reporting;
using System;
using System.Collections.Generic;
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

        protected ReportBaseViewModel(IReportingDataService service)
        {
            _service = service ?? new ReportingDataService();
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
            set { if (_dateFrom == value) return; _dateFrom = value; OnPropertyChanged(); _ = DebouncedReloadAsync(); }
        }

        private DateTime? _dateTo;
        public DateTime? DateTo
        {
            get => _dateTo;
            set { if (_dateTo == value) return; _dateTo = value; OnPropertyChanged(); _ = DebouncedReloadAsync(); }
        }

        private string _selectedCompany = "";
        public string SelectedCompany { get => _selectedCompany; set { if (_selectedCompany == value) return; _selectedCompany = value; OnPropertyChanged(); } }

        private string _selectedCustomer = "";
        public string SelectedCustomer { get => _selectedCustomer; set { if (_selectedCustomer == value) return; _selectedCustomer = value; OnPropertyChanged(); } }

        private string _selectedProject = "";
        public string SelectedProject { get => _selectedProject; set { if (_selectedProject == value) return; _selectedProject = value; OnPropertyChanged(); } }

        private string _selectedSalesman = "";
        public string SelectedSalesman { get => _selectedSalesman; set { if (_selectedSalesman == value) return; _selectedSalesman = value; OnPropertyChanged(); } }

        private string _selectedStatus = "";
        public string SelectedStatus { get => _selectedStatus; set { if (_selectedStatus == value) return; _selectedStatus = value; OnPropertyChanged(); } }

        // ── Pagination ────────────────────────────────────────────────────
        private int _page = 1;
        public int Page { get => _page; set { if (_page == value) return; _page = Math.Max(1, value); OnPropertyChanged(); OnPropertyChanged(nameof(PageDisplay)); _ = LoadAsync(); } }
        private int _pageSize = 50;
        public int PageSize { get => _pageSize; set { if (_pageSize == value) return; _pageSize = Math.Max(10, value); OnPropertyChanged(); _ = LoadAsync(); } }
        private int _totalCount;
        public int TotalCount { get => _totalCount; protected set { if (_totalCount == value) return; _totalCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(PageDisplay)); OnPropertyChanged(nameof(HasNextPage)); OnPropertyChanged(nameof(HasPrevPage)); } }
        private int _totalPages = 1;
        public int TotalPages { get => _totalPages; protected set { if (_totalPages == value) return; _totalPages = value; OnPropertyChanged(); OnPropertyChanged(nameof(PageDisplay)); } }
        public string PageDisplay => $"Page {Page} of {TotalPages}  •  {TotalCount} record(s)";
        public bool HasNextPage => Page < TotalPages;
        public bool HasPrevPage => Page > 1;

        // ── State ─────────────────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading { get => _isLoading; protected set { if (_isLoading == value) return; _isLoading = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(IsContentVisible)); } }
        private bool _hasError;
        public bool HasError { get => _hasError; protected set { if (_hasError == value) return; _hasError = value; OnPropertyChanged(); } }
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

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand ApplyFilterCommand { get; protected set; } = null!;
        public ICommand ClearFilterCommand { get; protected set; } = null!;
        public ICommand RefreshCommand { get; protected set; } = null!;
        public ICommand NextPageCommand { get; protected set; } = null!;
        public ICommand PrevPageCommand { get; protected set; } = null!;
        public ICommand ExportCsvCommand { get; protected set; } = null!;
        public ICommand ExportExcelCommand { get; protected set; } = null!;
        public ICommand ExportPdfCommand { get; protected set; } = null!;

        protected virtual void InitCommands()
        {
            ApplyFilterCommand = new RelayCommand(_ => { Page = 1; _ = LoadAsync(); });
            ClearFilterCommand = new RelayCommand(_ => ClearFilters());
            RefreshCommand = new RelayCommand(async _ => await LoadAsync());
            NextPageCommand = new RelayCommand(_ => { if (HasNextPage) Page++; }, _ => HasNextPage);
            PrevPageCommand = new RelayCommand(_ => { if (HasPrevPage) Page--; }, _ => HasPrevPage);
            ExportCsvCommand = new RelayCommand(async _ => await ExportAsync(ProGlassAutomation.Services.Export.Core.ExportFormat.Csv));
            ExportExcelCommand = new RelayCommand(async _ => await ExportAsync(ProGlassAutomation.Services.Export.Core.ExportFormat.Excel));
            ExportPdfCommand = new RelayCommand(async _ => await ExportAsync(ProGlassAutomation.Services.Export.Core.ExportFormat.Pdf));
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
                Page = Page,
                PageSize = PageSize
            };
        }

        private async Task DebouncedReloadAsync()
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;
            try
            {
                await Task.Delay(350, token);
                if (token.IsCancellationRequested) return;
                Page = 1;
                await LoadAsync();
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
        }

        public abstract Task LoadAsync();

        protected abstract Task ExportAsync(ProGlassAutomation.Services.Export.Core.ExportFormat format);

        protected void CancelPrevious()
        {
            try { _cts?.Cancel(); _cts?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
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

        protected async Task LoadFilterOptionsAsync()
        {
            try
            {
                CompanyOptions = await _service.GetCompanyOptionsAsync();
                CustomerOptions = await _service.GetCustomerOptionsAsync();
                ProjectOptions = await _service.GetProjectOptionsAsync();
                SalesmanOptions = await _service.GetSalesmanOptionsAsync();
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
        }
    }
}
