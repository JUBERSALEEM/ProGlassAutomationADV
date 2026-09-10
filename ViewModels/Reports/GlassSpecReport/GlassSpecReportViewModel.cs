// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/GlassSpecReport/GlassSpecReportViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — Glass Specifications & Row-Level Traceability
//
// SURFACES the row-level intelligence the hub has always advertised
// ("row-level traceability") but never exposed in the UI — only the AI
// agent consumed it. Every glass row from every proforma is flattened and
// carried end-to-end (PI → JO → DN → TI) with its dimensions, quantity,
// value and delivery/invoice status.
//
// DATA SOURCING:
//   • Detail grid  — GetGlassSpecRowsAsync (server-paged; the engine caps
//     the PI scan at 1000 for performance, which we surface as a hint).
//   • KPI strip + top-spec mix — GetSpecAggregationsAsync over the SAME
//     filter, so the mix always reconciles to the grid beneath it. The
//     aggregation is an ENHANCEMENT: its failure never blanks the grid.
//
//   No new query written; export reuses ReportingExportService.GlassHeaders
//   / GlassMap / GlassMapExcel.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.DependencyInjection;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.Reports.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Reports.GlassSpecReport
{
    public class GlassSpecReportViewModel : ReportBaseViewModel
    {
        private readonly IDialogService _dialogs = null!;

        public GlassSpecReportViewModel() : this(
            App.ServiceProvider?.GetService<IDialogService>() ?? new DialogService(),
            App.ServiceProvider?.GetService<IReportingDataService>(),
            App.ServiceProvider?.GetService<IDatabaseServices>())
        {
        }

        public GlassSpecReportViewModel(
            IDialogService? dialogs,
            IReportingDataService? service = null,
            IDatabaseServices? database = null)
            : base(service ?? new ReportingDataService(), database)
        {
            _dialogs = dialogs!;
            InitCommands();
            SetSortOptions(new[] { "Date", "Amount", "Customer", "Company", "Qty", "Area", "Proforma" }, "Date");
            _ = LoadFilterOptionsAsync();
            _ = LoadAsync();
        }

        // ── Detail grid (server-paged) ────────────────────────────────
        private ObservableCollection<GlassSpecReportRow> _rows = new();
        public ObservableCollection<GlassSpecReportRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        // ── Spec mix (top specifications by value) ─────────────────────
        private ObservableCollection<SpecAggregation> _topSpecs = new();
        public ObservableCollection<SpecAggregation> TopSpecs
        {
            get => _topSpecs;
            set { _topSpecs = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSpecMix)); }
        }
        public bool HasSpecMix => _topSpecs.Count > 0;

        // ── Summary KPIs (from aggregations over the filtered set) ─────
        private int _distinctSpecs;
        public int DistinctSpecs { get => _distinctSpecs; private set { _distinctSpecs = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private int _totalQty;
        public int TotalQty { get => _totalQty; private set { _totalQty = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private decimal _totalSqm;
        public decimal TotalSqm { get => _totalSqm; private set { _totalSqm = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private decimal _totalValue;
        public decimal TotalValue { get => _totalValue; private set { _totalValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private int _balanceQty;
        public int BalanceQty { get => _balanceQty; private set { _balanceQty = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        // Hint shown when the engine's 1000-PI scan cap may have truncated.
        private bool _isCapped;
        public bool IsCapped { get => _isCapped; private set { _isCapped = value; OnPropertyChanged(); } }

        public ObservableCollection<KpiItem> SummaryCards => new()
        {
            new KpiItem("GLASS ROWS", TotalCount.ToString("N0"), $"{DistinctSpecs} distinct specs", "AccentBrush"),
            new KpiItem("QUANTITY", TotalQty.ToString("N0"), $"{TotalSqm:N1} m² total", "InfoBrush"),
            new KpiItem("VALUE", $"AED {TotalValue:N0}", "across all glass rows", "SuccessBrush"),
            new KpiItem("BALANCE", BalanceQty.ToString("N0"), "pcs awaiting delivery", "WarningBrush"),
        };

        protected override void InitCommands()
        {
            base.InitCommands();
            ClearFilterCommand = new RelayCommand(_ => ClearFilters());
        }

        public override async Task LoadAsync()
        {
            CancelPrevious();
            var token = LoadToken;
            IsLoading = true; HasError = false;
            try
            {
                var filter = BuildFilter();
                var paged = await _service.GetGlassSpecRowsAsync(filter, token);
                if (token.IsCancellationRequested) return;

                Rows = new ObservableCollection<GlassSpecReportRow>(paged.Items);
                TotalCount = paged.TotalCount;
                TotalPages = paged.TotalPages == 0 ? 1 : paged.TotalPages;
                // The engine scans up to 1000 PIs when no ProformaId is set;
                // a full page at that ceiling means the view may be partial.
                IsCapped = string.IsNullOrWhiteSpace(filter.ProformaId) && paged.TotalCount >= 1000;

                // Aggregations drive the KPI strip + spec mix. They are an
                // ENHANCEMENT — a failure here must never blank the grid.
                try
                {
                    var aggs = await _service.GetSpecAggregationsAsync(filter, token);
                    if (token.IsCancellationRequested) return;
                    RecomputeSummary(aggs);
                    TopSpecs = new ObservableCollection<SpecAggregation>(
                        aggs.OrderByDescending(a => a.TotalValue).Take(8));
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    AppLogService.Warning($"GlassSpec aggregation warning: {ExceptionMessages.Deep(ex)}");
                }

                IsLoaded = true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                HasError = true; ErrorMessage = ExceptionMessages.Deep(ex);
                AppLogService.Warning($"GlassSpec LoadAsync error: {ExceptionMessages.Deep(ex)}");
            }
            finally { IsLoading = false; }
        }

        private void RecomputeSummary(List<SpecAggregation> aggs)
        {
            DistinctSpecs = aggs.Count;
            TotalQty = aggs.Sum(a => a.TotalQty);
            TotalSqm = aggs.Sum(a => a.TotalSqm);
            TotalValue = aggs.Sum(a => a.TotalValue);
            BalanceQty = aggs.Sum(a => a.BalanceQty);
        }

        // ── Export / Share (reuse the shared Glass contract) ──────────
        protected override async Task ExportAsync(ExportFormat format)
        {
            if (Rows.Count == 0) return;
            IsExporting = true;
            try
            {
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"Glass_Spec_Report_{DateTime.Now:yyyyMMdd}" };
                ExportResult res = await ExportCoreAsync(format, req);
                if (!res.Success && !res.Cancelled)
                    _dialogs.Show(res.ErrorMessage ?? "Export failed.", LocalizationService.Instance.T("Action.Export"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }

        protected override string ShareReportTitle => "Glass Spec Report";
        protected override IDialogService? ShareDialogs => _dialogs;

        protected override async Task<string?> ExportStagedAsync(ExportFormat format, string folder)
        {
            if (Rows.Count == 0) return null;
            ExportResult res = await ExportCoreAsync(format,
                StagedRequest(folder, $"Glass_Spec_Report_{DateTime.Now:yyyyMMdd}", format));
            return res.Success ? res.SavedPath : null;
        }

        private async Task<ExportResult> ExportCoreAsync(ExportFormat format, ExportRequest req)
        {
            // Export the full filtered set (not just the visible page) so the
            // file matches the KPI totals; cap at the aggregation ceiling.
            var exportFilter = BuildFilter();
            exportFilter.Page = 1;
            exportFilter.PageSize = 5000;
            var all = (await _service.GetGlassSpecRowsAsync(exportFilter, LoadToken)).Items;

            if (format == ExportFormat.Csv)
                return await ReportingExportService.ExportCsvAsync(all, "Glass Spec Report", ReportingExportService.GlassHeaders, ReportingExportService.GlassMap, req);
            else if (format == ExportFormat.Excel)
                return await ReportingExportService.ExportExcelAsync(all, "Glass Spec Report", ReportingExportService.GlassHeaders, ReportingExportService.GlassMapExcel, req);
            else
                return await ReportingExportService.ExportPdfAsync(all, "Glass Spec Report", ReportingExportService.GlassHeaders, ReportingExportService.GlassMap, req);
        }
    }
}
