using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.Common;
using ProGlassAutomation.ViewModels.Reports.PiReport;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.Reports.JoReport
{
    public class JoReportViewModel : ReportBaseViewModel
    {
        public JoReportViewModel(IReportingDataService? service = null) : base(service ?? new ReportingDataService())
        {
            InitCommands();
            _ = LoadFilterOptionsAsync();
            _ = LoadAsync();
        }

        private JoReportSummary _summary = new();
        public JoReportSummary Summary { get => _summary; set { _summary = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        public ObservableCollection<KpiItem> SummaryCards => new()
        {
            new KpiItem("TOTAL JO", Summary.TotalJo.ToString("N0"), $"Value AED {Summary.TotalValue:N0}", "AccentBrush"),
            new KpiItem("AWAITING JO", Summary.AwaitingJo.ToString("N0"), "PI not yet converted", "WarningBrush"),
            new KpiItem("IN PRODUCTION", GetStatusCount("In Production", "InProduction").ToString("N0"), "Active jobs", "InfoBrush"),
            new KpiItem("COMPLETED", GetStatusCount("Completed").ToString("N0"), "Finished", "SuccessBrush"),
        };

        private int GetStatusCount(params string[] statusKeys)
        {
            int sum = 0;
            foreach (var key in statusKeys)
            {
                if (Summary.ByStatus.TryGetValue(key, out var count))
                    sum += count;
            }
            return sum;
        }

        private ObservableCollection<JoReportRow> _rows = new();
        public ObservableCollection<JoReportRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        public override async Task LoadAsync()
        {
            CancelPrevious();
            var token = LoadToken;
            IsLoading = true; HasError = false;
            try
            {
                var filter = BuildFilter();
                var s = await _service.GetJoSummaryAsync(filter, token);
                var paged = await _service.GetJoRowsAsync(filter, token);
                if (token.IsCancellationRequested) return;
                Summary = s;
                Rows = new ObservableCollection<JoReportRow>(paged.Items);
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
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"JO_Report_{System.DateTime.Now:yyyyMMdd}" };
                ExportResult res;
                if (format == ExportFormat.Csv) res = await ReportingExportService.ExportCsvAsync(Rows.ToList(), "Job Order Report", ReportingExportService.JoHeaders, ReportingExportService.JoMap, req);
                else if (format == ExportFormat.Excel) res = await ReportingExportService.ExportExcelAsync(Rows.ToList(), "Job Order Report", ReportingExportService.JoHeaders, ReportingExportService.JoMapExcel, req);
                else res = await ReportingExportService.ExportPdfAsync(Rows.ToList(), "Job Order Report", ReportingExportService.JoHeaders, ReportingExportService.JoMap, req);
                if (!res.Success && !res.Cancelled) System.Windows.MessageBox.Show(res.ErrorMessage ?? "Export failed.", "Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }
    }
}
