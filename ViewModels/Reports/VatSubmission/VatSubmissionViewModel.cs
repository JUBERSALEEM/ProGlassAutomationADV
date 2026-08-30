using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Reports.VatSubmission
{
    /// <summary>
    /// ViewModel for VAT Submission Report — shows 5% VAT breakdown for UAE FTA compliance.
    /// Supports 4 view modes: Detail, BySalesman, ByCompany, ByProject.
    /// </summary>
    public class VatSubmissionViewModel : ReportBaseViewModel
    {
        public VatSubmissionViewModel(IReportingDataService? service = null) 
            : base(service ?? new ReportingDataService())
        {
            InitCommands();
            // Don't load in constructor - let navigation trigger it
            // This prevents race conditions and unhandled exceptions
        }

        private VatSubmissionSummary _summary = new();
        public VatSubmissionSummary Summary 
        { 
            get => _summary; 
            set { _summary = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } 
        }

        public ObservableCollection<KpiItem> SummaryCards => new()
        {
            new KpiItem("TOTAL VAT COLLECTED", 
                $"AED {Summary.TotalVatCollected:N2}", 
                $"{Summary.TotalInvoices} invoices", 
                "AccentBrush"),
            new KpiItem("TOTAL REVENUE", 
                $"AED {Summary.TotalGrandTotal:N2}", 
                $"AED {Summary.TotalSubtotal:N2} ex-VAT", 
                "SuccessBrush"),
            new KpiItem("SALESMEN", 
                Summary.BySalesman.Count.ToString(), 
                "Active salesmen", 
                "InfoBrush"),
            new KpiItem("COMPANIES", 
                Summary.ByCompany.Count.ToString(), 
                "Active companies", 
                "WarningBrush"),
        };

        private ObservableCollection<VatSubmissionRow> _rows = new();
        public ObservableCollection<VatSubmissionRow> Rows 
        { 
            get => _rows; 
            set { _rows = value; OnPropertyChanged(); } 
        }

        // Grouped Views
        private ObservableCollection<SalesmanVatSummary> _salesmanSummary = new();
        public ObservableCollection<SalesmanVatSummary> SalesmanSummary 
        { 
            get => _salesmanSummary; 
            set { _salesmanSummary = value; OnPropertyChanged(); } 
        }

        private ObservableCollection<CompanyVatSummary> _companySummary = new();
        public ObservableCollection<CompanyVatSummary> CompanySummary 
        { 
            get => _companySummary; 
            set { _companySummary = value; OnPropertyChanged(); } 
        }

        private ObservableCollection<ProjectVatSummary> _projectSummary = new();
        public ObservableCollection<ProjectVatSummary> ProjectSummary 
        { 
            get => _projectSummary; 
            set { _projectSummary = value; OnPropertyChanged(); } 
        }

        private string _viewMode = "Detail"; // Detail, BySalesman, ByCompany, ByProject
        public string ViewMode 
        { 
            get => _viewMode; 
            set { _viewMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsDetailView)); OnPropertyChanged(nameof(IsSalesmanView)); OnPropertyChanged(nameof(IsCompanyView)); OnPropertyChanged(nameof(IsProjectView)); } 
        }

        public bool IsDetailView => ViewMode == "Detail";
        public bool IsSalesmanView => ViewMode == "BySalesman";
        public bool IsCompanyView => ViewMode == "ByCompany";
        public bool IsProjectView => ViewMode == "ByProject";

        // Filter Options
        private string _companyFilter = AllFilter;
        public string CompanyFilter
        {
            get => _companyFilter;
            set { if (_companyFilter == value) return; _companyFilter = value ?? AllFilter; OnPropertyChanged(); ReloadNow(); }
        }

        private string _salesmanFilter = AllFilter;
        public string SalesmanFilter
        {
            get => _salesmanFilter;
            set { if (_salesmanFilter == value) return; _salesmanFilter = value ?? AllFilter; OnPropertyChanged(); ReloadNow(); }
        }

        private string _projectFilter = AllFilter;
        public string ProjectFilter
        {
            get => _projectFilter;
            set { if (_projectFilter == value) return; _projectFilter = value ?? AllFilter; OnPropertyChanged(); ReloadNow(); }
        }

        public const string AllFilter = "All";

        private List<string> _companyOptions = new() { AllFilter };
        public List<string> CompanyOptions { get => _companyOptions; private set { _companyOptions = value; OnPropertyChanged(); } }

        private List<string> _salesmanOptions = new() { AllFilter };
        public List<string> SalesmanOptions { get => _salesmanOptions; private set { _salesmanOptions = value; OnPropertyChanged(); } }

        private List<string> _projectOptions = new() { AllFilter };
        public List<string> ProjectOptions { get => _projectOptions; private set { _projectOptions = value; OnPropertyChanged(); } }

        protected override ReportingFilter BuildFilter()
        {
            var filter = base.BuildFilter();
            filter.Company = string.Equals(CompanyFilter, AllFilter, StringComparison.OrdinalIgnoreCase) ? null : CompanyFilter;
            filter.Salesman = string.Equals(SalesmanFilter, AllFilter, StringComparison.OrdinalIgnoreCase) ? null : SalesmanFilter;
            filter.Project = string.Equals(ProjectFilter, AllFilter, StringComparison.OrdinalIgnoreCase) ? null : ProjectFilter;
            return filter;
        }

        public async Task LoadFilterOptionsAsync()
        {
            try
            {
                var companies = await _service.GetCompanyOptionsAsync();
                CompanyOptions = new List<string> { AllFilter }.Concat(companies).ToList();

                var salesmen = await _service.GetSalesmanOptionsAsync();
                SalesmanOptions = new List<string> { AllFilter }.Concat(salesmen).ToList();

                var projects = await _service.GetProjectOptionsAsync();
                ProjectOptions = new List<string> { AllFilter }.Concat(projects).ToList();
            }
            catch (Exception ex)
            {
                Services.Logging.AppLogService.Warning($"VAT filter options warning: {ex.Message}");
                // Set default options on error
                CompanyOptions = new List<string> { AllFilter };
                SalesmanOptions = new List<string> { AllFilter };
                ProjectOptions = new List<string> { AllFilter };
            }
        }

        public override async Task LoadAsync()
        {
            CancelPrevious();
            var token = LoadToken;
            IsLoading = true; 
            HasError = false;
            
            try
            {
                var filter = BuildFilter();
                var summary = await _service.GetVatSubmissionSummaryAsync(filter, token);
                var paged = await _service.GetVatSubmissionRowsAsync(filter, token);
                
                if (token.IsCancellationRequested) return;
                
                Summary = summary;
                Rows = new ObservableCollection<VatSubmissionRow>(paged.Items);
                SalesmanSummary = new ObservableCollection<SalesmanVatSummary>(summary.BySalesman);
                CompanySummary = new ObservableCollection<CompanyVatSummary>(summary.ByCompany);
                ProjectSummary = new ObservableCollection<ProjectVatSummary>(summary.ByProject);
                
                TotalCount = paged.TotalCount;
                TotalPages = paged.TotalPages == 0 ? 1 : paged.TotalPages;
                IsLoaded = true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            { 
                HasError = true; 
                ErrorMessage = ex.Message; 
            }
            finally { IsLoading = false; }
        }

        protected override void InitCommands()
        {
            base.InitCommands();
            ClearFilterCommand = new RelayCommand(_ =>
            {
                CompanyFilter = AllFilter;
                SalesmanFilter = AllFilter;
                ProjectFilter = AllFilter;
                ClearFilters();
            });
        }

        protected override async Task ExportAsync(ExportFormat format)
        {
            if (Rows.Count == 0) return;
            IsExporting = true;
            
            try
            {
                var req = new ExportRequest 
                { 
                    PromptForLocation = true, 
                    OpenAfterSave = true, 
                    SuggestedFileName = $"VAT_Submission_{DateTime.Now:yyyyMMdd}" 
                };
                
                ExportResult res;
                if (format == ExportFormat.Csv) 
                    res = await ReportingExportService.ExportCsvAsync(
                        Rows.ToList(), "VAT Submission Report", 
                        VatHeaders, VatMap, req);
                else if (format == ExportFormat.Excel) 
                    res = await ReportingExportService.ExportExcelAsync(
                        Rows.ToList(), "VAT Submission Report", 
                        VatHeaders, VatMapExcel, req);
                else 
                    res = await ReportingExportService.ExportPdfAsync(
                        Rows.ToList(), "VAT Submission Report", 
                        VatHeaders, VatMap, req);
                
                if (!res.Success && !res.Cancelled) 
                    System.Windows.MessageBox.Show(res.ErrorMessage ?? "Export failed.", 
                        "Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }

        // Export mappings - CSV/PDF (string values)
        private static readonly string[] VatHeaders = 
        {
            "PI Number", "Company", "Customer", "Project", "Salesman",
            "Issue Date", "Status", "Subtotal (ex-VAT)", "VAT (5%)", "Total (inc-VAT)"
        };

        private static readonly Func<VatSubmissionRow, string[]> VatMap = row => new string[]
        {
            row.ProformaId,
            row.CompanyName,
            row.CustomerName,
            row.ProjectName,
            row.Salesman,
            row.IssueDateDisplay,
            row.Status,
            row.SubtotalBeforeVat.ToString("N2"),
            row.VatAmount.ToString("N2"),
            row.GrandTotalVatInclusive.ToString("N2")
        };

        // Export mappings - Excel (native types for better formatting)
        private static readonly Func<VatSubmissionRow, string[]> VatMapExcel = row => new string[]
        {
            row.ProformaId,
            row.CompanyName,
            row.CustomerName,
            row.ProjectName,
            row.Salesman,
            row.IssueDate.ToString("yyyy-MM-dd"),
            row.Status,
            row.SubtotalBeforeVat.ToString("N2"),
            row.VatAmount.ToString("N2"),
            row.GrandTotalVatInclusive.ToString("N2")
        };
    }
}
