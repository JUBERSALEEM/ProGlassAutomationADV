// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/PiReport/PiReportViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — PI Report
//
// SHOWS:
//   Total PI, Draft, Released, Confirmed, Rejected, Pending,
//   Converted to JO, Not converted, Value, Company, Customer,
//   Salesman, Project, PI date, actual specification rows
//   Drill-down: PI → rows → JO → Delivery → TI → Profitability
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.ViewModels.Reports.Common;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;

using ProGlassAutomation.Services.Dialogs;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.Reports.PiReport
{
    public class PiReportViewModel : ReportBaseViewModel
    {
        private readonly IDialogService _dialogs = null!;

        public PiReportViewModel() : this(
            App.ServiceProvider?.GetService<IDialogService>()?? new DialogService())
        {
        }

        public PiReportViewModel(IDialogService? dialogs)
        {
            _dialogs = dialogs!;
        }

        public PiReportViewModel(IReportingDataService? service = null) : base(service ?? new ReportingDataService())
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
            try { SetStatusOptions(await _service.GetPiStatusOptionsAsync()); }
            catch (System.Exception ex) { AppLogService.Warning($"PI status options warning: {ex.Message}"); }
        }

        /// <summary>Maps the "All" sentinel back to no status filter.</summary>
        protected override ReportingFilter BuildFilter()
        {
            var filter = base.BuildFilter();
            if (string.Equals(filter.Status, AllStatusesOption, StringComparison.OrdinalIgnoreCase))
                filter.Status = null;
            return filter;
        }

        // ── Summary KPIs ──────────────────────────────────────────────────
        private PiReportSummary _summary = new();
        public PiReportSummary Summary { get => _summary; set { _summary = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        public ObservableCollection<KpiItem> SummaryCards => new()
        {
            new KpiItem("TOTAL PI", Summary.TotalPi.ToString("N0"), $"AED {Summary.TotalValue:N0}", "AccentBrush"),
            new KpiItem("CONFIRMED", Summary.Confirmed.ToString("N0"), $"{Summary.Confirmed} approved/issued", "SuccessBrush"),
            new KpiItem("PENDING", Summary.Pending.ToString("N0"), $"{Summary.Draft} draft", "WarningBrush"),
            new KpiItem("CONVERTED", Summary.Converted.ToString("N0"), $"{Summary.NotConverted} not yet", "InfoBrush"),
        };

        // ── Rows ──────────────────────────────────────────────────────────
        private ObservableCollection<PiReportRow> _rows = new();
        public ObservableCollection<PiReportRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        /// <summary>Raised with the PI's DB id when the user clicks
        /// "Open Proforma Invoice". Wired by the hub to MainViewModel.</summary>
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
                if (p is PiReportRow r)
                    OpenDocumentRequested?.Invoke(r.Id);
            });
        }

        /// <summary>
        /// Lazy-loads the itemized specification rows + quantity
        /// reconciliation for one PI into the row (revealed inline on
        /// expand — single grid, no side panel).
        /// </summary>
        public async Task LoadRowDetailAsync(PiReportRow row)
        {
            if (row == null || row.HasLoadedDetail) return;

            try
            {
                var rows = await _service.GetPiRowsDetailAsync(row.ProformaId);
                row.DetailRows.Clear();
                foreach (var d in rows ?? new System.Collections.Generic.List<GlassSpecReportRow>())
                    row.DetailRows.Add(d);
                row.Reconciliation = await _service.GetQuantityReconciliationAsync(row.ProformaId);
                row.HasLoadedDetail = true;
            }
            catch (Exception ex)
            {
                row.DetailRows.Clear();
                row.HasLoadedDetail = true;
                AppLogService.Warning($"Suppressed exception: {ex.Message}");
            }
        }

        public override async Task LoadAsync()
        {
            CancelPrevious();
            var token = LoadToken;
            IsLoading = true; HasError = false;
            try
            {
                var filter = BuildFilter();
                var summary = await _service.GetPiSummaryAsync(filter, token);
                if (token.IsCancellationRequested) return;
                var paged = await _service.GetPiRowsAsync(filter, token);
                if (token.IsCancellationRequested) return;
                Summary = summary;
                Rows = new ObservableCollection<PiReportRow>(paged.Items);
                TotalCount = paged.TotalCount;
                TotalPages = paged.TotalPages == 0 ? 1 : paged.TotalPages;
                IsLoaded = true;
            }
            catch (System.Exception ex) when (!(ex is System.OperationCanceledException))
            {
                HasError = true; ErrorMessage = ex.Message;
                AppLogService.Warning($"PiReport LoadAsync error: {ex.Message}");
            }
            finally { IsLoading = false; }
        }

        protected override async Task ExportAsync(ExportFormat format)
        {
            if (Rows.Count == 0) return;
            IsExporting = true;
            try
            {
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"PI_Report_{System.DateTime.Now:yyyyMMdd}" };
                ExportResult res;
                if (format == ExportFormat.Csv)
                    res = await ReportingExportService.ExportCsvAsync(Rows.ToList(), "Proforma Invoice Report", ReportingExportService.PiHeaders, ReportingExportService.PiMap, req);
                else if (format == ExportFormat.Excel)
                    res = await ReportingExportService.ExportExcelAsync(Rows.ToList(), "Proforma Invoice Report", ReportingExportService.PiHeaders, ReportingExportService.PiMapExcel, req);
                else
                    res = await ReportingExportService.ExportPdfAsync(Rows.ToList(), "Proforma Invoice Report", ReportingExportService.PiHeaders, ReportingExportService.PiMap, req);
                if (!res.Success && !res.Cancelled)
                    _dialogs.Show(res.ErrorMessage ?? "Export failed.", LocalizationService.Instance.T("Action.Export"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }
    }
}
