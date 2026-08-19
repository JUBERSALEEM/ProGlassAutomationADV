using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.Common;
using ProGlassAutomation.ViewModels.Reports.PiReport;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.Reports.DeliveryReport
{
    public class DeliveryReportViewModel : ReportBaseViewModel
    {
        public DeliveryReportViewModel(IReportingDataService? service = null) : base(service ?? new ReportingDataService())
        {
            InitCommands();
            _ = LoadFilterOptionsAsync();
            _ = LoadAsync();
        }

        private DeliveryReportSummary _summary = new();
        public DeliveryReportSummary Summary { get => _summary; set { _summary = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); OnPropertyChanged(nameof(CompletionText)); } }

        public string CompletionText => $"{Summary.CompletionPct:0.0}% delivered ({Summary.TotalDeliveredQty}/{Summary.TotalOrderedQty})";

        public ObservableCollection<KpiItem> SummaryCards => new()
        {
            new KpiItem("DELIVERY NOTES", Summary.TotalDn.ToString("N0"), CompletionText, "AccentBrush"),
            new KpiItem("PENDING", Summary.Pending.ToString("N0"), "Not yet delivered", "DangerBrush"),
            new KpiItem("PARTIAL", Summary.Partial.ToString("N0"), "In progress", "WarningBrush"),
            new KpiItem("COMPLETED", Summary.Completed.ToString("N0"), "Fully delivered", "SuccessBrush"),
        };

        private ObservableCollection<DeliveryReportRow> _rows = new();
        public ObservableCollection<DeliveryReportRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        public override async Task LoadAsync()
        {
            CancelPrevious();
            var token = LoadToken;
            IsLoading = true; HasError = false;
            try
            {
                var filter = BuildFilter();
                var s = await _service.GetDeliverySummaryAsync(filter, token);
                var paged = await _service.GetDeliveryRowsAsync(filter, token);
                if (token.IsCancellationRequested) return;
                Summary = s;
                Rows = new ObservableCollection<DeliveryReportRow>(paged.Items);
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
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"Delivery_Report_{System.DateTime.Now:yyyyMMdd}" };
                ExportResult res;
                if (format == ExportFormat.Csv) res = await ReportingExportService.ExportCsvAsync(Rows.ToList(), "Delivery Report", ReportingExportService.DnHeaders, ReportingExportService.DnMap, req);
                else if (format == ExportFormat.Excel) res = await ReportingExportService.ExportExcelAsync(Rows.ToList(), "Delivery Report", ReportingExportService.DnHeaders, ReportingExportService.DnMapExcel, req);
                else res = await ReportingExportService.ExportPdfAsync(Rows.ToList(), "Delivery Report", ReportingExportService.DnHeaders, ReportingExportService.DnMap, req);
                if (!res.Success && !res.Cancelled) System.Windows.MessageBox.Show(res.ErrorMessage ?? "Export failed.", "Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }
    }
}
