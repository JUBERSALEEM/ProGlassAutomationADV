using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.Common;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.Reports.ProfitabilityReport
{
    public class ProfitabilityReportViewModel : ReportBaseViewModel
    {
        public ProfitabilityReportViewModel(IReportingDataService? service = null) : base(service ?? new ReportingDataService())
        {
            InitCommands();
            _ = LoadFilterOptionsAsync();
            _ = LoadAsync();
        }

        private ProfitabilitySummary _summary = new();
        public ProfitabilitySummary Summary { get => _summary; set { _summary = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasMissing)); } }
        public bool HasMissing => Summary.InvoicesWithoutCost > 0;

        private ObservableCollection<ProfitabilityRow> _rows = new();
        public ObservableCollection<ProfitabilityRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        public override async Task LoadAsync()
        {
            CancelPrevious();
            var token = LoadToken;
            IsLoading = true; HasError = false;
            try
            {
                var filter = BuildFilter();
                var s = await _service.GetProfitabilitySummaryAsync(filter, token);
                var paged = await _service.GetProfitabilityRowsAsync(filter, token);
                if (token.IsCancellationRequested) return;
                Summary = s;
                Rows = new ObservableCollection<ProfitabilityRow>(paged.Items);
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
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"Profitability_Report_{System.DateTime.Now:yyyyMMdd}" };
                ExportResult res;
                if (format == ExportFormat.Csv) res = await ReportingExportService.ExportCsvAsync(Rows.ToList(), "Profitability Report", ReportingExportService.ProfitHeaders, ReportingExportService.ProfitMap, req);
                else if (format == ExportFormat.Excel) res = await ReportingExportService.ExportExcelAsync(Rows.ToList(), "Profitability Report", ReportingExportService.ProfitHeaders, ReportingExportService.ProfitMapExcel, req);
                else res = await ReportingExportService.ExportPdfAsync(Rows.ToList(), "Profitability Report", ReportingExportService.ProfitHeaders, ReportingExportService.ProfitMap, req);
                if (!res.Success && !res.Cancelled) System.Windows.MessageBox.Show(res.ErrorMessage ?? "Export failed.", "Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }
    }
}
