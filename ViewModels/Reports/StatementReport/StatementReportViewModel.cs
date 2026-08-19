using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.Common;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.Reports.StatementReport
{
    public class StatementReportViewModel : ReportBaseViewModel
    {
        public StatementReportViewModel(IReportingDataService? service = null) : base(service ?? new ReportingDataService())
        {
            InitCommands();
            _ = LoadFilterOptionsAsync();
            _ = LoadAsync();
        }

        private ObservableCollection<StatementReportRow> _rows = new();
        public ObservableCollection<StatementReportRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        private decimal _totalInvoiced;
        public decimal TotalInvoiced { get => _totalInvoiced; set { _totalInvoiced = value; OnPropertyChanged(); } }
        private decimal _totalOutstanding;
        public decimal TotalOutstanding { get => _totalOutstanding; set { _totalOutstanding = value; OnPropertyChanged(); } }
        private decimal _totalOverdue;
        public decimal TotalOverdue { get => _totalOverdue; set { _totalOverdue = value; OnPropertyChanged(); } }

        public override async Task LoadAsync()
        {
            CancelPrevious();
            var token = LoadToken;
            IsLoading = true; HasError = false;
            try
            {
                var filter = BuildFilter();
                var paged = await _service.GetStatementRowsAsync(filter, token);
                if (token.IsCancellationRequested) return;
                Rows = new ObservableCollection<StatementReportRow>(paged.Items);
                TotalCount = paged.TotalCount;
                TotalPages = paged.TotalPages == 0 ? 1 : paged.TotalPages;
                // Sum for KPI strip (over current page or total? Use page for now)
                decimal inv = 0, outst = 0, over = 0;
                foreach (var r in paged.Items) { inv += r.TotalInvoiced; outst += r.TotalOutstanding; over += r.OverdueAmount; }
                TotalInvoiced = inv; TotalOutstanding = outst; TotalOverdue = over;
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
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"Statement_Report_{System.DateTime.Now:yyyyMMdd}" };
                ExportResult res;
                if (format == ExportFormat.Csv) res = await ReportingExportService.ExportCsvAsync(Rows.ToList(), "Statement Report", ReportingExportService.StatementHeaders, ReportingExportService.StatementMap, req);
                else if (format == ExportFormat.Excel) res = await ReportingExportService.ExportExcelAsync(Rows.ToList(), "Statement Report", ReportingExportService.StatementHeaders, ReportingExportService.StatementMapExcel, req);
                else res = await ReportingExportService.ExportPdfAsync(Rows.ToList(), "Statement Report", ReportingExportService.StatementHeaders, ReportingExportService.StatementMap, req);
                if (!res.Success && !res.Cancelled) System.Windows.MessageBox.Show(res.ErrorMessage ?? "Export failed.", "Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }
    }
}
