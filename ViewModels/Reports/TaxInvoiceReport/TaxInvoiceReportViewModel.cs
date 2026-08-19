using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.Common;
using ProGlassAutomation.ViewModels.Reports.PiReport;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.Reports.TaxInvoiceReport
{
    public class TaxInvoiceReportViewModel : ReportBaseViewModel
    {
        public TaxInvoiceReportViewModel(IReportingDataService? service = null) : base(service ?? new ReportingDataService())
        {
            InitCommands();
            _ = LoadFilterOptionsAsync();
            _ = LoadAsync();
        }

        private TaxInvoiceReportSummary _summary = new();
        public TaxInvoiceReportSummary Summary { get => _summary; set { _summary = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        public ObservableCollection<KpiItem> SummaryCards => new()
        {
            new KpiItem("TAX INVOICES", Summary.TotalInvoices.ToString("N0"), $"AED {Summary.TotalAmount:N0}", "AccentBrush"),
            new KpiItem("OUTSTANDING", $"AED {Summary.Outstanding:N0}", $"{Summary.Issued} issued", "WarningBrush"),
            new KpiItem("OVERDUE", Summary.Overdue.ToString("N0"), "Needs follow-up", "DangerBrush"),
            new KpiItem("PENDING INVOICE", Summary.PendingInvoice.ToString("N0"), "Confirmed PI without TI", "InfoBrush"),
        };

        private ObservableCollection<TaxInvoiceReportRow> _rows = new();
        public ObservableCollection<TaxInvoiceReportRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        private ObservableCollection<TaxInvoiceReportRow> _pendingRows = new();
        public ObservableCollection<TaxInvoiceReportRow> PendingRows { get => _pendingRows; set { _pendingRows = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasPending)); } }
        public bool HasPending => PendingRows.Count > 0;

        public override async Task LoadAsync()
        {
            CancelPrevious();
            var token = LoadToken;
            IsLoading = true; HasError = false;
            try
            {
                var filter = BuildFilter();
                var s = await _service.GetTaxInvoiceSummaryAsync(filter, token);
                var paged = await _service.GetTaxInvoiceRowsAsync(filter, token);
                var pending = await _service.GetPendingInvoicesAsync(filter, token);
                if (token.IsCancellationRequested) return;
                Summary = s;
                Rows = new ObservableCollection<TaxInvoiceReportRow>(paged.Items);
                PendingRows = new ObservableCollection<TaxInvoiceReportRow>(pending);
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
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"TaxInvoice_Report_{System.DateTime.Now:yyyyMMdd}" };
                ExportResult res;
                if (format == ExportFormat.Csv) res = await ReportingExportService.ExportCsvAsync(Rows.ToList(), "Tax Invoice Report", ReportingExportService.TiHeaders, ReportingExportService.TiMap, req);
                else if (format == ExportFormat.Excel) res = await ReportingExportService.ExportExcelAsync(Rows.ToList(), "Tax Invoice Report", ReportingExportService.TiHeaders, ReportingExportService.TiMapExcel, req);
                else res = await ReportingExportService.ExportPdfAsync(Rows.ToList(), "Tax Invoice Report", ReportingExportService.TiHeaders, ReportingExportService.TiMap, req);
                if (!res.Success && !res.Cancelled) System.Windows.MessageBox.Show(res.ErrorMessage ?? "Export failed.", "Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }
    }
}
