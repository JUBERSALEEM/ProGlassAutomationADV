// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/ProfitabilityReport/ProfitabilityReportViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — Profitability / P&L
//
// SURFACES the per-proforma profit-and-loss the engine already computes
// (Step 9 glass + production costing) but which the Reports Hub never
// exposed — only the AI agent consumed it.
//
// DATA SOURCING:
//   • Detail grid — GetProfitabilityRowsAsync (server-paged; revenue net
//     of VAT, cost = glass + production, margin, and a cost-basis health
//     signal: HasCostData / IsStaleCost / CostSource).
//   • KPI strip   — GetProfitabilitySummaryAsync over the SAME filter.
//
//   Cost integrity is first-class here: rows with no Step-9 costing are
//   flagged, and rows whose Sheet-Store basis moved after costing are
//   flagged STALE, so a margin is never trusted blindly.
//
//   No new query written; export reuses ReportingExportService.ProfitHeaders
//   / ProfitMap / ProfitMapExcel.
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

namespace ProGlassAutomation.ViewModels.Reports.ProfitabilityReport
{
    public class ProfitabilityReportViewModel : ReportBaseViewModel
    {
        private readonly IDialogService _dialogs = null!;

        public ProfitabilityReportViewModel() : this(
            App.ServiceProvider?.GetService<IDialogService>() ?? new DialogService(),
            App.ServiceProvider?.GetService<IReportingDataService>(),
            App.ServiceProvider?.GetService<IDatabaseServices>())
        {
        }

        public ProfitabilityReportViewModel(
            IDialogService? dialogs,
            IReportingDataService? service = null,
            IDatabaseServices? database = null)
            : base(service ?? new ReportingDataService(), database)
        {
            _dialogs = dialogs!;
            InitCommands();
            SetSortOptions(new[] { "Created", "Date", "Amount", "Customer", "Company", "Project", "Status" }, "Created");
            _ = LoadFilterOptionsAsync();
            _ = LoadAsync();
        }

        // ── Detail grid (server-paged) ────────────────────────────────
        private ObservableCollection<ProfitabilityRow> _rows = new();
        public ObservableCollection<ProfitabilityRow> Rows { get => _rows; set { _rows = value; OnPropertyChanged(); } }

        // ── Summary KPIs ──────────────────────────────────────────────
        private decimal _totalRevenue;
        public decimal TotalRevenue { get => _totalRevenue; private set { _totalRevenue = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private decimal _totalCost;
        public decimal TotalCost { get => _totalCost; private set { _totalCost = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private decimal _totalProfit;
        public decimal TotalProfit { get => _totalProfit; private set { _totalProfit = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private decimal _avgMargin;
        public decimal AvgMargin { get => _avgMargin; private set { _avgMargin = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); } }

        private int _withCost;
        public int WithCost { get => _withCost; private set { _withCost = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); OnPropertyChanged(nameof(HasCostGaps)); } }

        private int _withoutCost;
        public int WithoutCost { get => _withoutCost; private set { _withoutCost = value; OnPropertyChanged(); OnPropertyChanged(nameof(SummaryCards)); OnPropertyChanged(nameof(HasCostGaps)); OnPropertyChanged(nameof(CostCoverageNote)); } }

        /// <summary>True when at least one PI in view has no Step-9 costing —
        /// drives the "profit is understated" banner so a margin is never
        /// read as complete when it is not.</summary>
        public bool HasCostGaps => _withoutCost > 0;

        public string CostCoverageNote =>
            _withoutCost == 0
                ? "All proformas in view have cost data."
                : $"{_withoutCost} proforma(s) have no Step-9 costing — profit is understated until they are costed.";

        public ObservableCollection<KpiItem> SummaryCards => new()
        {
            new KpiItem("REVENUE", $"AED {TotalRevenue:N0}", "net of VAT, in view", "AccentBrush"),
            new KpiItem("COST", $"AED {TotalCost:N0}", "glass + production", "InfoBrush"),
            new KpiItem("PROFIT", $"AED {TotalProfit:N0}", $"{AvgMargin:0.#}% avg margin", TotalProfit >= 0 ? "SuccessBrush" : "DangerBrush"),
            new KpiItem("COST COVERAGE", $"{WithCost}/{WithCost + WithoutCost}", $"{WithoutCost} missing costing", WithoutCost > 0 ? "WarningBrush" : "SuccessBrush"),
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
                var paged = await _service.GetProfitabilityRowsAsync(filter, token);
                if (token.IsCancellationRequested) return;

                Rows = new ObservableCollection<ProfitabilityRow>(paged.Items);
                TotalCount = paged.TotalCount;
                TotalPages = paged.TotalPages == 0 ? 1 : paged.TotalPages;

                // Summary is an ENHANCEMENT — a failure never blanks the grid.
                try
                {
                    var s = await _service.GetProfitabilitySummaryAsync(filter, token);
                    if (token.IsCancellationRequested) return;
                    TotalRevenue = s.TotalRevenue;
                    TotalCost = s.TotalCost;
                    TotalProfit = s.TotalProfit;
                    AvgMargin = s.AvgMargin;
                    WithCost = s.InvoicesWithCost;
                    WithoutCost = s.InvoicesWithoutCost;
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    AppLogService.Warning($"Profitability summary warning: {ExceptionMessages.Deep(ex)}");
                }

                IsLoaded = true;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                HasError = true; ErrorMessage = ExceptionMessages.Deep(ex);
                AppLogService.Warning($"Profitability LoadAsync error: {ExceptionMessages.Deep(ex)}");
            }
            finally { IsLoading = false; }
        }

        // ── Export / Share (reuse the shared Profit contract) ─────────
        protected override async Task ExportAsync(ExportFormat format)
        {
            if (Rows.Count == 0) return;
            IsExporting = true;
            try
            {
                var req = new ExportRequest { PromptForLocation = true, OpenAfterSave = true, SuggestedFileName = $"Profitability_Report_{DateTime.Now:yyyyMMdd}" };
                ExportResult res = await ExportCoreAsync(format, req);
                if (!res.Success && !res.Cancelled)
                    _dialogs.Show(res.ErrorMessage ?? "Export failed.", LocalizationService.Instance.T("Action.Export"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally { IsExporting = false; }
        }

        protected override string ShareReportTitle => "Profitability Report";
        protected override IDialogService? ShareDialogs => _dialogs;

        protected override async Task<string?> ExportStagedAsync(ExportFormat format, string folder)
        {
            if (Rows.Count == 0) return null;
            ExportResult res = await ExportCoreAsync(format,
                StagedRequest(folder, $"Profitability_Report_{DateTime.Now:yyyyMMdd}", format));
            return res.Success ? res.SavedPath : null;
        }

        private async Task<ExportResult> ExportCoreAsync(ExportFormat format, ExportRequest req)
        {
            // Export the full filtered set (not just the visible page).
            var exportFilter = BuildFilter();
            exportFilter.Page = 1;
            exportFilter.PageSize = 5000;
            var all = (await _service.GetProfitabilityRowsAsync(exportFilter, LoadToken)).Items;

            if (format == ExportFormat.Csv)
                return await ReportingExportService.ExportCsvAsync(all, "Profitability Report", ReportingExportService.ProfitHeaders, ReportingExportService.ProfitMap, req);
            else if (format == ExportFormat.Excel)
                return await ReportingExportService.ExportExcelAsync(all, "Profitability Report", ReportingExportService.ProfitHeaders, ReportingExportService.ProfitMapExcel, req);
            else
                return await ReportingExportService.ExportPdfAsync(all, "Profitability Report", ReportingExportService.ProfitHeaders, ReportingExportService.ProfitMap, req);
        }
    }
}
