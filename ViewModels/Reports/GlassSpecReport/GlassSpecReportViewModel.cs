using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.Common;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Reports.GlassSpecReport
{
    public class GlassSpecReportViewModel : ReportBaseViewModel
    {
        public GlassSpecReportViewModel(IReportingDataService? service = null) : base(service ?? new ReportingDataService())
        {
            InitCommands();
            _ = LoadFilterOptionsAsync();
            _ = LoadAsync();
        }

        // ── Mode: Detail (rows) vs Aggregation (spec groups) ────────────
        private bool _showAggregation;
        public bool ShowAggregation { get => _showAggregation; set { if (_showAggregation == value) return; _showAggregation = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowDetail)); _ = LoadAsync(); } }
        public bool ShowDetail => !_showAggregation;

        // ── Detail rows ──────────────────────────────────────────────────
        private ObservableCollection<GlassSpecReportRow> _rows = new();
        public ObservableCollection<GlassSpecReportRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        private GlassSpecReportRow? _selectedRow;
        public GlassSpecReportRow? SelectedRow
        {
            get => _selectedRow;
            set { _selectedRow = value; OnPropertyChanged(); if (value != null) _ = LoadTraceAsync(value.RowId); }
        }

        // ── Aggregations ─────────────────────────────────────────────────
        private ObservableCollection<SpecAggregation> _aggregations = new();
        public ObservableCollection<SpecAggregation> Aggregations { get => _aggregations; set { _aggregations = value; OnPropertyChanged(); } }

        // ── Trace ────────────────────────────────────────────────────────
        private RowTrace? _trace;
        public RowTrace? Trace { get => _trace; set { _trace = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasTrace)); } }
        public bool HasTrace => Trace != null;

        // ── Glass type filter ────────────────────────────────────────────
        private string _selectedGlassType = "";
        public string SelectedGlassType { get => _selectedGlassType; set { if (_selectedGlassType == value) return; _selectedGlassType = value; OnPropertyChanged(); _ = LoadAsync(); } }

        private string _glassRefFilter = "";
        public string GlassRefFilter { get => _glassRefFilter; set { _glassRefFilter = value; OnPropertyChanged(); } }

        private string _specNameFilter = "";
        public string SpecNameFilter { get => _specNameFilter; set { _specNameFilter = value; OnPropertyChanged(); } }

        public ICommand ToggleModeCommand { get; private set; } = null!;
        public ICommand TraceCommand { get; private set; } = null!;

        protected override void InitCommands()
        {
            base.InitCommands();
            ToggleModeCommand = new RelayCommand(_ => ShowAggregation = !ShowAggregation);
            TraceCommand = new RelayCommand(async p =>
            {
                if (p is string rowId) await LoadTraceAsync(rowId);
                else if (p is GlassSpecReportRow r) await LoadTraceAsync(r.RowId);
            });
        }

        protected override ReportingFilter BuildFilter()
        {
            var f = base.BuildFilter();
            if (!string.IsNullOrWhiteSpace(SelectedGlassType)) f.GlassType = SelectedGlassType;
            if (!string.IsNullOrWhiteSpace(GlassRefFilter)) f.GlassRef = GlassRefFilter;
            if (!string.IsNullOrWhiteSpace(SpecNameFilter)) f.Specification = SpecNameFilter;
            // Also map generic search etc handled via base
            return f;
        }

        public override async Task LoadAsync()
        {
            CancelPrevious();
            var token = LoadToken;
            IsLoading = true; HasError = false;
            try
            {
                var filter = BuildFilter();
                if (ShowAggregation)
                {
                    var aggs = await _service.GetSpecAggregationsAsync(filter, token);
                    if (token.IsCancellationRequested) return;
                    Aggregations = new ObservableCollection<SpecAggregation>(aggs);
                    TotalCount = aggs.Count;
                    TotalPages = 1;
                }
                else
                {
                    var paged = await _service.GetGlassSpecRowsAsync(filter, token);
                    if (token.IsCancellationRequested) return;
                    Rows = new ObservableCollection<GlassSpecReportRow>(paged.Items);
                    TotalCount = paged.TotalCount;
                    TotalPages = paged.TotalPages == 0 ? 1 : paged.TotalPages;
                }
                IsLoaded = true;
            }
            catch (System.Exception ex) when (!(ex is System.OperationCanceledException))
            { HasError = true; ErrorMessage = ex.Message; }
            finally { IsLoading = false; }
        }

        private async Task LoadTraceAsync(string rowId)
        {
            try
            {
                Trace = await _service.GetRowTraceAsync(rowId);
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
        }

        protected override async Task ExportAsync(ExportFormat format)
        {
            if (!ShowAggregation && Rows.Count == 0) return;
            if (ShowAggregation && Aggregations.Count == 0) return;
            IsExporting = true;
            try
            {
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"GlassSpec_Report_{System.DateTime.Now:yyyyMMdd}" };
                ExportResult res;
                if (ShowAggregation)
                {
                    string[] headers = new[] { "Spec", "Type", "GlassRef", "Dimensions", "Rows", "Qty", "SQM", "Value", "Avg Rate", "Delivered", "Balance", "Customers", "Projects" };
                    if (format == ExportFormat.Csv) res = await ReportingExportService.ExportCsvAsync(Aggregations.ToList(), "Glass Specification Aggregation", headers, a => new[] { a.SpecName, a.GlassType, a.GlassRef, a.DimensionsKey, a.TotalRows.ToString(), a.TotalQty.ToString(), a.TotalSqm.ToString("N3"), a.TotalValue.ToString("N2"), a.AvgRate.ToString("N2"), a.DeliveredQty.ToString(), a.BalanceQty.ToString(), a.DistinctCustomers.ToString(), a.DistinctProjects.ToString() }, req);
                    else if (format == ExportFormat.Excel) res = await ReportingExportService.ExportExcelAsync(Aggregations.ToList(), "Glass Specification Aggregation", headers, a => new object[] { a.SpecName, a.GlassType, a.GlassRef, a.DimensionsKey, a.TotalRows, a.TotalQty, a.TotalSqm, a.TotalValue, a.AvgRate, a.DeliveredQty, a.BalanceQty, a.DistinctCustomers, a.DistinctProjects }, req);
                    else res = await ReportingExportService.ExportPdfAsync(Aggregations.ToList(), "Glass Specification Aggregation", headers, a => new[] { a.SpecName, a.GlassType, a.GlassRef, a.DimensionsKey, a.TotalRows.ToString(), a.TotalQty.ToString(), a.TotalSqm.ToString("N3"), a.TotalValue.ToString("N2"), a.AvgRate.ToString("N2"), a.DeliveredQty.ToString(), a.BalanceQty.ToString(), a.DistinctCustomers.ToString(), a.DistinctProjects.ToString() }, req);
                }
                else
                {
                    if (format == ExportFormat.Csv) res = await ReportingExportService.ExportCsvAsync(Rows.ToList(), "Glass Specification Report", ReportingExportService.GlassHeaders, ReportingExportService.GlassMap, req);
                    else if (format == ExportFormat.Excel) res = await ReportingExportService.ExportExcelAsync(Rows.ToList(), "Glass Specification Report", ReportingExportService.GlassHeaders, ReportingExportService.GlassMapExcel, req);
                    else res = await ReportingExportService.ExportPdfAsync(Rows.ToList(), "Glass Specification Report", ReportingExportService.GlassHeaders, ReportingExportService.GlassMap, req);
                }
                if (!res.Success && !res.Cancelled) System.Windows.MessageBox.Show(res.ErrorMessage ?? "Export failed.", "Export", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }
    }
}
