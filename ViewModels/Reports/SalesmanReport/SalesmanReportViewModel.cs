using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.Common;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.Reports.SalesmanReport
{
    public class SalesmanReportViewModel : ReportBaseViewModel
    {
        public SalesmanReportViewModel(IReportingDataService? service = null) : base(service ?? new ReportingDataService())
        {
            InitCommands();
            _ = LoadAsync();
        }

        private ObservableCollection<SalesmanReportRow> _rows = new();
        public ObservableCollection<SalesmanReportRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        public override async Task LoadAsync()
        {
            CancelPrevious();
            var token = LoadToken;
            IsLoading = true; HasError = false;
            try
            {
                var filter = BuildFilter();
                var paged = await _service.GetSalesmanRowsAsync(filter, token);
                if (token.IsCancellationRequested) return;
                Rows = new ObservableCollection<SalesmanReportRow>(paged.Items);
                TotalCount = paged.TotalCount;
                TotalPages = paged.TotalPages == 0 ? 1 : paged.TotalPages;
                IsLoaded = true;
            }
            catch (System.Exception ex) when (!(ex is System.OperationCanceledException))
            { HasError = true; ErrorMessage = ex.Message; }
            finally { IsLoading = false; }
        }

        protected override async Task ExportAsync(ExportFormat format)
        {
            if (Rows.Count == 0) return;
            IsExporting = true;
            try
            {
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"Salesman_Report_{System.DateTime.Now:yyyyMMdd}" };
                ExportResult res;
                if (format == ExportFormat.Csv) res = await ReportingExportService.ExportCsvAsync(Rows.ToList(), "Salesman Report", ReportingExportService.SalesmanHeaders, ReportingExportService.SalesmanMap, req);
                else if (format == ExportFormat.Excel) res = await ReportingExportService.ExportExcelAsync(Rows.ToList(), "Salesman Report", ReportingExportService.SalesmanHeaders, ReportingExportService.SalesmanMapExcel, req);
                else res = await ReportingExportService.ExportPdfAsync(Rows.ToList(), "Salesman Report", ReportingExportService.SalesmanHeaders, ReportingExportService.SalesmanMap, req);
                if (!res.Success && !res.Cancelled) System.Windows.MessageBox.Show(res.ErrorMessage ?? "Export failed.", "Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }
    }
}
