// ═══════════════════════════════════════════════════════════════
// FILE: OptimizationViewModel.Report.cs
// MODULE: Optimization — REPORTS tab — layout / parts / stocks summaries + CSV export (partial of OptimizationViewModel)
// ═══════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.Optimization;
using ProGlassAutomation.Services.Production;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;

namespace ProGlassAutomation.ViewModels.Optimization
{
    public partial class OptimizationViewModel : ViewModels.ViewModelBase
    {

        // ── REPORTS tab ───────────────────────────────────────────────
        // Three industry summaries over the current session, each with a
        // CSV export through the app's shared export engine (UTF-8 BOM,
        // formula-injection safe, SaveFileDialog).

        public ICommand ExportLayoutReportCommand { get; }
        public ICommand ExportPartsReportCommand { get; }
        public ICommand ExportStocksReportCommand { get; }

        private void ExportLayoutReport()
        {
            if (!HasResult || SheetRows.Count == 0)
            {
                RunMessage = "Run a cut first — the layout summary has no sheets yet.";
                return;
            }
            AsyncUtility.SafeFireAndForget(
                async () => await ReportingExportService.ExportCsvAsync(
                    SheetRows.ToList(),
                    "Optimization — Layout Summary",
                    new[] { "Sheet", "Glass", "Stock (mm)", "Pieces", "Utilization", "Offcut (m²)" },
                    r => new[]
                    {
                        r.SheetNo, r.Composition, r.StockLabel, r.PieceCount, r.Utilization, r.WasteLabel
                    },
                    new ExportRequest { SuggestedFileName = "Optimization_Layout_Summary" }),
                "Optimization layout report export");
        }

        private void ExportPartsReport()
        {
            if (!HasPieces || SizeGroups.Count == 0)
            {
                RunMessage = "The cutting list is empty — add pieces before exporting the parts summary.";
                return;
            }
            AsyncUtility.SafeFireAndForget(
                async () => await ReportingExportService.ExportCsvAsync(
                    SizeGroups.ToList(),
                    "Optimization — Parts Summary",
                    new[] { "Glass Composition", "Thickness", "Pieces", "Area (m²)" },
                    g => new[] { g.Composition, g.Thickness, g.PieceCount, g.AreaM2 },
                    new ExportRequest { SuggestedFileName = "Optimization_Parts_Summary" }),
                "Optimization parts report export");
        }

        private void ExportStocksReport()
        {
            if (Stocks.Count == 0)
            {
                RunMessage = "The stock pool is empty — select a job order to import its Step 4 sheets.";
                return;
            }
            AsyncUtility.SafeFireAndForget(
                async () => await ReportingExportService.ExportCsvAsync(
                    Stocks.ToList(),
                    "Optimization — Stocks Summary",
                    new[] { "Sheet (mm)", "Glass / Group", "Available", "Used this run", "Source", "In run" },
                    s => new[]
                    {
                        s.WidthText + "×" + s.HeightText,
                        s.CompositionLabel,
                        s.AvailableText,
                        s.UsedLabel,
                        s.SourceLabel,
                        s.IsUsedInRun ? "Yes" : "No"
                    },
                    new ExportRequest { SuggestedFileName = "Optimization_Stocks_Summary" }),
                "Optimization stocks report export");
        }
    }
}
