using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.Common;
using ProGlassAutomation.ViewModels.Reports.PiReport;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Reports.TaxInvoiceReport
{
    public class TaxInvoiceReportViewModel : ReportBaseViewModel
    {
        public TaxInvoiceReportViewModel(IReportingDataService? service = null) : base(service ?? new ReportingDataService())
        {
            InitCommands();
            SelectedStatus = AllStatusesOption;
            _ = LoadFilterOptionsAsync();
            _ = LoadStatusOptionsAsync();
            _ = LoadAsync();
        }

        /// <summary>Populates the status dropdown from DISTINCT statuses
        /// actually stored (never a hardcoded list that drifts).</summary>
        private async Task LoadStatusOptionsAsync()
        {
            try { SetStatusOptions(await _service.GetTiStatusOptionsAsync()); }
            catch (System.Exception ex) { Services.Logging.AppLogService.Warning($"TI status options warning: {ex.Message}"); }
        }

        /// <summary>Maps the "All" sentinel back to no status filter.</summary>
        protected override ReportingFilter BuildFilter()
        {
            var filter = base.BuildFilter();
            if (string.Equals(filter.Status, AllStatusesOption, StringComparison.OrdinalIgnoreCase))
                filter.Status = null;
            return filter;
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

        /// <summary>Raised with the TI's DB id when the user clicks
        /// "Open Tax Invoice". Wired by the hub to MainViewModel.</summary>
        public Action<int>? OpenDocumentRequested { get; set; }
        public ICommand OpenDocumentCommand { get; private set; } = null!;

        protected override void InitCommands()
        {
            base.InitCommands();
            ClearFilterCommand = new RelayCommand(_ =>
            {
                ClearFilters();
                SelectedStatus = AllStatusesOption;
            });
            OpenDocumentCommand = new RelayCommand(p =>
            {
                if (p is TaxInvoiceReportRow r)
                    OpenDocumentRequested?.Invoke(r.Id);
            });
        }

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
