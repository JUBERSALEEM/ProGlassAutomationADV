using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.Common;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.Reports.CompanyReport
{
    public class CompanyReportViewModel : ReportBaseViewModel
    {
        public CompanyReportViewModel(IReportingDataService? service = null) : base(service ?? new ReportingDataService())
        {
            InitCommands();
            ShowCustomers = true;
            _ = LoadAsync();
        }

        private bool _showCustomers = true;
        public bool ShowCustomers { get => _showCustomers; set { if (_showCustomers == value) return; _showCustomers = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowCompanies)); _ = LoadAsync(); } }
        public bool ShowCompanies => !_showCustomers;

        private ObservableCollection<CustomerReportRow> _customerRows = new();
        public ObservableCollection<CustomerReportRow> CustomerRows { get => _customerRows; set { _customerRows = value; OnPropertyChanged(); } }

        private ObservableCollection<CompanyReportRow> _companyRows = new();
        public ObservableCollection<CompanyReportRow> CompanyRows { get => _companyRows; set { _companyRows = value; OnPropertyChanged(); } }

        private CustomerReportRow? _selectedCustomer;
        public CustomerReportRow? SelectedCustomer { get => _selectedCustomer; set { _selectedCustomer = value; OnPropertyChanged(); if (value != null) _ = LoadCustomerDetailAsync(value.CustomerName); } }

        private CompanyReportRow? _selectedCompany;
        public CompanyReportRow? SelectedCompany { get => _selectedCompany; set { _selectedCompany = value; OnPropertyChanged(); } }

        // Detail: lifecycle PI→JO→DN→TI for selected customer
        private ObservableCollection<PiReportRow> _piLifecycle = new();
        public ObservableCollection<PiReportRow> PiLifecycle { get => _piLifecycle; set { _piLifecycle = value; OnPropertyChanged(); } }
        private ObservableCollection<JoReportRow> _joLifecycle = new();
        public ObservableCollection<JoReportRow> JoLifecycle { get => _joLifecycle; set { _joLifecycle = value; OnPropertyChanged(); } }

        public override async Task LoadAsync()
        {
            CancelPrevious();
            var token = LoadToken;
            IsLoading = true; HasError = false;
            try
            {
                var filter = BuildFilter();
                if (ShowCustomers)
                {
                    var paged = await _service.GetCustomerRowsAsync(filter, token);
                    if (token.IsCancellationRequested) return;
                    CustomerRows = new ObservableCollection<CustomerReportRow>(paged.Items);
                    TotalCount = paged.TotalCount;
                    TotalPages = paged.TotalPages == 0 ? 1 : paged.TotalPages;
                }
                else
                {
                    var paged = await _service.GetCompanyRowsAsync(filter, token);
                    if (token.IsCancellationRequested) return;
                    CompanyRows = new ObservableCollection<CompanyReportRow>(paged.Items);
                    TotalCount = paged.TotalCount;
                    TotalPages = paged.TotalPages == 0 ? 1 : paged.TotalPages;
                }
                IsLoaded = true;
            }
            catch (System.Exception ex) when (!(ex is System.OperationCanceledException))
            { HasError = true; ErrorMessage = ex.Message; }
            finally { IsLoading = false; }
        }

        private async Task LoadCustomerDetailAsync(string customer)
        {
            try
            {
                var pi = await _service.GetPiRowsAsync(new ReportingFilter { Customer = customer, PageSize = 20 });
                PiLifecycle = new ObservableCollection<PiReportRow>(pi.Items);
                var jo = await _service.GetJoRowsAsync(new ReportingFilter { Customer = customer, PageSize = 20 });
                JoLifecycle = new ObservableCollection<JoReportRow>(jo.Items);
            }
            catch { }
        }

        protected override async Task ExportAsync(ExportFormat format)
        {
            IsExporting = true;
            try
            {
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"CompanyCustomer_Report_{System.DateTime.Now:yyyyMMdd}" };
                ExportResult res;
                if (ShowCustomers)
                {
                    if (CustomerRows.Count == 0) return;
                    if (format == ExportFormat.Csv) res = await ReportingExportService.ExportCsvAsync(CustomerRows.ToList(), "Customer Report", ReportingExportService.CustomerHeaders, ReportingExportService.CustomerMap, req);
                    else if (format == ExportFormat.Excel) res = await ReportingExportService.ExportExcelAsync(CustomerRows.ToList(), "Customer Report", ReportingExportService.CustomerHeaders, ReportingExportService.CustomerMapExcel, req);
                    else res = await ReportingExportService.ExportPdfAsync(CustomerRows.ToList(), "Customer Report", ReportingExportService.CustomerHeaders, ReportingExportService.CustomerMap, req);
                }
                else
                {
                    if (CompanyRows.Count == 0) return;
                    if (format == ExportFormat.Csv) res = await ReportingExportService.ExportCsvAsync(CompanyRows.ToList(), "Company Report", ReportingExportService.CompanyHeaders, ReportingExportService.CompanyMap, req);
                    else if (format == ExportFormat.Excel) res = await ReportingExportService.ExportExcelAsync(CompanyRows.ToList(), "Company Report", ReportingExportService.CompanyHeaders, ReportingExportService.CompanyMapExcel, req);
                    else res = await ReportingExportService.ExportPdfAsync(CompanyRows.ToList(), "Company Report", ReportingExportService.CompanyHeaders, ReportingExportService.CompanyMap, req);
                }
                if (!res.Success && !res.Cancelled) System.Windows.MessageBox.Show(res.ErrorMessage ?? "Export failed.", "Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }
    }
}
