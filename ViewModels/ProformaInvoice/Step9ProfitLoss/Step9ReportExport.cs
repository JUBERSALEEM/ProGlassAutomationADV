// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/Step9ProfitLoss/Step9ReportExport.cs
// PROJECT: ProGlassAutomation
// MODULE: Step 9 Profit & Loss — report export (CSV / Excel / PDF)
//
// WHY THIS FILE
//   The four Step 9 analysis reports (Service, Sheet, Glass-type and the
//   per-spec Target comparison) had rich on-screen output but NO way to
//   export — so a profitable analysis could never leave the screen.
//   This static helper maps each row type to column arrays and hands them
//   to the SAME central ReportingExportService used by every Reports-hub
//   screen, so Step 9 exports behave and look identical to the rest of
//   the product (save-dialog, auto-open, {Report}_{date}.ext naming).
//
//   A synthetic "TOTAL" row is appended by the owning ViewModels (their
//   totals are recomputed from the source rows, never summed margins) so
//   the exported artifact carries the same column-aligned totals as the
//   on-screen footer.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Reporting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss
{
    /// <summary>
    /// Shared CSV / Excel / PDF export for the Step 9 Profit &amp; Loss
    /// analysis reports. Row types live in this same namespace, so the
    /// mappers are typed against the real bindable rows.
    /// </summary>
    public static class Step9ReportExport
    {
        /// <summary>
        /// Runs a report export through the central <see cref="ReportingExportService"/>.
        /// </summary>
        public static async Task<ExportResult> RunAsync<T>(
            ExportFormat format,
            IReadOnlyList<T> rows,
            string reportTitle,
            string[] headers,
            Func<T, string[]> textMapper,
            Func<T, object[]> excelMapper,
            string suggestedFileName)
        {
            var request = new ExportRequest
            {
                PromptForLocation = true,
                OpenAfterSave = true,
                SuggestedFileName = suggestedFileName
            };

            return format switch
            {
                ExportFormat.Csv => await ReportingExportService.ExportCsvAsync(
                    rows, reportTitle, headers, textMapper, request),
                ExportFormat.Excel => await ReportingExportService.ExportExcelAsync(
                    rows, reportTitle, headers, excelMapper, request),
                _ => await ReportingExportService.ExportPdfAsync(
                    rows, reportTitle, headers, textMapper, request)
            };
        }

        // ── Formatting helpers ──────────────────────────────────────────
        private static string N(decimal v) => v.ToString("N2", CultureInfo.InvariantCulture);
        private static string N1(decimal v) => v.ToString("N1", CultureInfo.InvariantCulture);
        private static string Rate(decimal? v) => (v ?? 0m) > 0m ? N(v!.Value) : "—";
        private static string Diff(decimal? v) =>
            v is null ? "—" : v.Value < 0m ? "-" + N(Math.Abs(v.Value)) : N(v.Value);

        // ── SERVICE PROFITABILITY ───────────────────────────────────────
        public static readonly string[] ServiceHeaders =
        {
            "SPEC", "SERVICE", "DESCRIPTION", "AREA (SQM)", "CHARGE", "COST", "PROFIT", "MARGIN %"
        };

        public static string[] ServiceMap(ServiceProfitRow r) => new[]
        {
            r.SpecName,
            r.ServiceName,
            r.Description,
            r.Area.ToString("0.##", CultureInfo.InvariantCulture),
            N(r.Charge),
            N(r.Cost),
            N(r.Profit),
            N1(r.MarginPercent)
        };

        public static object[] ServiceMapExcel(ServiceProfitRow r) => new object[]
        {
            r.SpecName, r.ServiceName, r.Description, r.Area, r.Charge, r.Cost, r.Profit, r.MarginPercent
        };

        // ── SHEET PROFITABILITY ─────────────────────────────────────────
        public static readonly string[] SheetHeaders =
        {
            "SPEC", "GLASS TYPE", "PANE", "SHEET SIZE", "SHEET SQM",
            "BUY SHEETS", "USED SHEETS", "WASTE SHEETS",
            "BUY AREA (SQM)", "USED AREA (SQM)", "WASTE AREA (SQM)",
            "BUY PRICE", "USED PRICE", "WASTE VALUE", "STEP 6 CHARGE", "STATUS"
        };

        public static string[] SheetMap(SheetProfitRow r) => new[]
        {
            r.SpecName,
            r.GlassTypeLabel,
            r.PaneSide,
            r.SheetSize,
            r.SheetSqm.ToString("0.####", CultureInfo.InvariantCulture),
            r.BuySheets.ToString("0", CultureInfo.InvariantCulture),
            r.UsedSheets.ToString("0", CultureInfo.InvariantCulture),
            r.WastageSheets.ToString("0", CultureInfo.InvariantCulture),
            r.BuySheetArea.ToString("0.##", CultureInfo.InvariantCulture),
            r.UsedArea.ToString("0.##", CultureInfo.InvariantCulture),
            r.WastageArea.ToString("0.##", CultureInfo.InvariantCulture),
            N(r.BuySheetPrice),
            N(r.UsedSheetPrice),
            N(r.WastageValue),
            N(r.Step6Charge),
            SheetStatus(r)
        };

        public static object[] SheetMapExcel(SheetProfitRow r) => new object[]
        {
            r.SpecName, r.GlassTypeLabel, r.PaneSide, r.SheetSize, r.SheetSqm,
            r.BuySheets, r.UsedSheets, r.WastageSheets,
            r.BuySheetArea, r.UsedArea, r.WastageArea,
            r.BuySheetPrice, r.UsedSheetPrice, r.WastageValue, r.Step6Charge,
            SheetStatus(r)
        };

        // The total row is marked by GlassTypeLabel == "TOTAL"; its computed
        // StatusLabel ("NO CHARGE"/"OK") is meaningless, so it prints "—".
        private static string SheetStatus(SheetProfitRow r) =>
            r.GlassTypeLabel == "TOTAL" ? "—" : r.StatusLabel;

        // ── GLASS TYPE COMPARISON ───────────────────────────────────────
        public static readonly string[] GlassTypeHeaders =
        {
            "GLASS TYPE", "JOBS", "AREA (SQM)", "REVENUE", "COST", "PROFIT", "MARGIN %", "STATUS"
        };

        public static string[] GlassTypeMap(GlassTypeComparisonRow r) => new[]
        {
            r.GlassType,
            r.JobCount.ToString(),
            r.TotalArea.ToString("0.##", CultureInfo.InvariantCulture),
            N(r.TotalRevenue),
            N(r.TotalCost),
            N(r.TotalProfit),
            N1(r.MarginPercent),
            GlassStatus(r)
        };

        public static object[] GlassTypeMapExcel(GlassTypeComparisonRow r) => new object[]
        {
            r.GlassType, r.JobCount, r.TotalArea, r.TotalRevenue, r.TotalCost,
            r.TotalProfit, r.MarginPercent, GlassStatus(r)
        };

        private static string GlassStatus(GlassTypeComparisonRow r) =>
            r.GlassType == "TOTAL" ? "—"
            : r.IsBestPerformer ? "BEST"
            : r.IsWorstPerformer ? "WORST"
            : r.IsProfitable ? "PROFIT"
            : "LOSS";

        // ── MAIN P&L COST BREAKDOWN ────────────────────────────────────
        public static readonly string[] PnlHeaders =
        {
            "SPEC", "KIND", "DESCRIPTION", "SHEET SIZE", "QTY", "PRICE", "AMOUNT"
        };

        public static string[] PnlMap(ProfitLossRow r) => new[]
        {
            r.SpecDisplay,
            r.Kind,
            r.Description,
            r.Kind == "TOTAL"
                ? "—"
                : r.SheetWidthMm > 0
                    ? $"{r.SheetWidthMm} × {r.SheetHeightMm} · {r.SheetSqm.ToString("0.####", CultureInfo.InvariantCulture)} SQM"
                    : "—",
            r.Kind == "TOTAL" ? "—" : r.Quantity.ToString("0.##", CultureInfo.InvariantCulture),
            r.Kind == "TOTAL" ? "—" : $"{r.PricePerSqm.ToString("N2", CultureInfo.InvariantCulture)} / {r.UnitLabel}",
            N(r.Amount)
        };

        public static object[] PnlMapExcel(ProfitLossRow r) => new object[]
        {
            r.SpecDisplay,
            r.Kind,
            r.Description,
            r.Kind == "TOTAL" ? "—" : r.SizeDisplay,
            r.Kind == "TOTAL" ? "—" : r.Quantity,
            r.Kind == "TOTAL" ? "—" : r.PricePerSqm,
            r.Amount
        };

        // ── TARGET COMPARISON ───────────────────────────────────────────
        public static readonly string[] TargetHeaders =
        {
            "SPEC", "GLASS / SPECIFICATION", "BUILD", "AREA (SQM)",
            "GIVEN /SQM", "SPEC TGT /SQM", "U-INSERT /SQM",
            "FINAL TGT /SQM", "DIFF /SQM", "MARGIN %", "STATUS"
        };

        public static string[] TargetMap(SpecTargetRowViewModel r) => new[]
        {
            r.SpecNumber > 0 ? $"SPEC {r.SpecNumber}" : "INVOICE-WIDE",
            r.SpecDisplayName,
            r.BuildTypeLabel,
            r.Result?.AreaSqm.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
            Rate(r.Result?.GivenPricePerSqm),
            Rate(r.Result?.SpecificationTargetPerSqm),
            Rate(r.Result?.UInsertRatePerSqm),
            Rate(r.Result?.FinalRequiredTargetPerSqm),
            Diff(r.Result?.DifferencePerSqm),
            r.Result?.MarginAgainstTargetPercent?.ToString("0.##", CultureInfo.InvariantCulture) ?? "",
            r.StatusLabel
        };

        public static object[] TargetMapExcel(SpecTargetRowViewModel r) => new object[]
        {
            r.SpecNumber, r.SpecDisplayName, r.BuildTypeLabel,
            r.Result?.AreaSqm ?? 0m,
            r.Result?.GivenPricePerSqm ?? 0m,
            r.Result?.SpecificationTargetPerSqm ?? 0m,
            r.Result?.UInsertRatePerSqm ?? 0m,
            r.Result?.FinalRequiredTargetPerSqm ?? 0m,
            r.Result?.DifferencePerSqm ?? 0m,
            r.Result?.MarginAgainstTargetPercent ?? 0m,
            r.StatusLabel
        };
    }
}
