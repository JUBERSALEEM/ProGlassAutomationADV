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

namespace ProGlassAutomation.ViewModels.Reports.StatementReport
{
    /// <summary>
    /// Statement FINANCIAL report (Phase 8). One row per real statement
    /// line item (a Tax Invoice inside a saved statement) rendered as a
    /// ledger document:
    ///   Date | Document # | Type | Description | Debit | Credit | Balance
    /// Statistics: Opening Balance · Total Debit · Total Credit ·
    /// Closing Balance (Opening + Debit − Credit). All figures come from
    /// stored StatementLineItem data — nothing is fabricated.
    /// </summary>
    public class StatementReportViewModel : ReportBaseViewModel
    {
        public StatementReportViewModel(IReportingDataService? service = null) : base(service ?? new ReportingDataService())
        {
            InitCommands();

            // Open the Tax Invoice behind a ledger line — same "open the
            // original document" affordance the other reports provide. The row
            // carries the invoice number (string); the hub resolves it to a DB
            // id and navigates.
            OpenTaxInvoiceCommand = new RelayCommand(p =>
            {
                if (p is StatementLineRow r && !string.IsNullOrWhiteSpace(r.InvoiceNumber))
                    OpenTaxInvoiceRequested?.Invoke(r.InvoiceNumber);
            });

            _ = LoadFilterOptionsAsync();
            _ = LoadAsync();
        }

        /// <summary>Raised with the line's invoice number when the user clicks
        /// "Open Tax Invoice". Wired by the hub to MainViewModel.</summary>
        public Action<string>? OpenTaxInvoiceRequested { get; set; }
        public ICommand OpenTaxInvoiceCommand { get; private set; } = null!;

        private StatementLineSummary _summary = new();
        public StatementLineSummary Summary
        {
            get => _summary;
            set { _summary = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); }
        }

        public ObservableCollection<KpiItem> SummaryCards => new()
        {
            new KpiItem("OPENING", $"AED {Summary.OpeningBalance:N0}", "Outstanding before period", "NeutralBrush"),
            new KpiItem("TOTAL DEBIT", $"AED {Summary.TotalDebit:N0}", "Billed in period", "DangerBrush"),
            new KpiItem("TOTAL CREDIT", $"AED {Summary.TotalCredit:N0}", "Paid in period", "SuccessBrush"),
            new KpiItem("CLOSING", $"AED {Summary.ClosingBalance:N0}", "Opening + Debit − Credit", "AccentBrush"),
        };

        private ObservableCollection<StatementLineRow> _rows = new();
        public ObservableCollection<StatementLineRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        public override async Task LoadAsync()
        {
            CancelPrevious();
            var token = LoadToken;
            IsLoading = true; HasError = false;
            try
            {
                var filter = BuildFilter();
                var s = await _service.GetStatementLineSummaryAsync(filter, token);
                var paged = await _service.GetStatementLineRowsAsync(filter, token);
                if (token.IsCancellationRequested) return;
                Summary = s;
                Rows = new ObservableCollection<StatementLineRow>(paged.Items);
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
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"Statement_Ledger_{System.DateTime.Now:yyyyMMdd}" };
                ExportResult res;
                if (format == ExportFormat.Csv) res = await ReportingExportService.ExportCsvAsync(Rows.ToList(), "Statement Ledger", ReportingExportService.StatementLineHeaders, ReportingExportService.StatementLineMap, req);
                else if (format == ExportFormat.Excel) res = await ReportingExportService.ExportExcelAsync(Rows.ToList(), "Statement Ledger", ReportingExportService.StatementLineHeaders, ReportingExportService.StatementLineMapExcel, req);
                else res = await ReportingExportService.ExportPdfAsync(Rows.ToList(), "Statement Ledger", ReportingExportService.StatementLineHeaders, ReportingExportService.StatementLineMap, req);
                if (!res.Success && !res.Cancelled) System.Windows.MessageBox.Show(res.ErrorMessage ?? "Export failed.", "Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }
    }
}
