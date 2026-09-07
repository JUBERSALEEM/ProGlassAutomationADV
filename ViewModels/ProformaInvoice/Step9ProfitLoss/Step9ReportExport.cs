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
using System.Linq;
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
            "SECTION", "SPEC", "SERVICE", "DESCRIPTION", "AREA (SQM)",
            "CHARGE", "COST", "PROFIT", "MARGIN %", "STATUS"
        };

        public static string[] ServiceMap(ServiceProfitRow r) => new[]
        {
            r.SpecName == "TOTAL" ? "TOTAL" : "SERVICE DETAIL",
            r.SpecName,
            r.ServiceName,
            r.Description,
            r.Area.ToString("0.##", CultureInfo.InvariantCulture),
            N(r.Charge),
            N(r.Cost),
            N(r.Profit),
            N1(r.MarginPercent),
            r.SpecName == "TOTAL" ? "—" : r.IsProfitable ? "PROFIT" : "LOSS"
        };

        public static object[] ServiceMapExcel(ServiceProfitRow r) => new object[]
        {
            r.SpecName == "TOTAL" ? "TOTAL" : "SERVICE DETAIL",
            r.SpecName, r.ServiceName, r.Description, r.Area, r.Charge, r.Cost, r.Profit, r.MarginPercent,
            r.SpecName == "TOTAL" ? "—" : r.IsProfitable ? "PROFIT" : "LOSS"
        };

        // ── SHEET PROFITABILITY ─────────────────────────────────────────
        public static readonly string[] SheetHeaders =
        {
            "SECTION", "SPEC", "GLASS TYPE", "PANE / GLASS", "SHEET SIZE", "SHEET SQM",
            "BUY SHEETS", "USED SHEETS", "WASTE SHEETS",
            "BUY AREA (SQM)", "USED AREA (SQM)", "WASTE AREA (SQM)",
            "RATE /SQM", "BUY PRICE", "USED PRICE", "WASTE VALUE", "STEP 6 CHARGE",
            "STEP 6 SOURCE", "STEP 6 SOURCE SQM", "STEP 6 SHEET QTY", "STEP 6 RATE", "EFFICIENCY %",
            "USED SHARE %", "WASTAGE SHARE %", "STEP 6 SHARE %", "STATUS", "DIAGNOSTIC"
        };

        public static string[] SheetMap(SheetProfitRow r) => new[]
        {
            r.GlassTypeLabel == "TOTAL" ? "TOTAL" : "SHEET DETAIL",
            r.SpecName,
            r.GlassTypeLabel,
            r.PaneGlassDisplay,
            r.SheetSize,
            r.SheetSqm.ToString("0.####", CultureInfo.InvariantCulture),
            r.BuySheets.ToString("0.##", CultureInfo.InvariantCulture),
            r.UsedSheets.ToString("0.##", CultureInfo.InvariantCulture),
            r.WastageSheets.ToString("0.##", CultureInfo.InvariantCulture),
            r.BuySheetArea.ToString("0.##", CultureInfo.InvariantCulture),
            r.UsedArea.ToString("0.##", CultureInfo.InvariantCulture),
            r.WastageArea.ToString("0.##", CultureInfo.InvariantCulture),
            N(r.PricePerSqm),
            N(r.BuySheetPrice),
            N(r.UsedSheetPrice),
            N(r.WastageValue),
            N(r.Step6Charge),
            r.Step6SourceLabel,
            r.Step6SourceSqm.ToString("0.###", CultureInfo.InvariantCulture),
            r.Step6SheetQuantity > 0 ? r.Step6SheetQuantity.ToString(CultureInfo.InvariantCulture) : "—",
            r.Step6Rate > 0m ? N(r.Step6Rate) : "—",
            N(r.UsageEfficiencyPercent),
            N1(r.UsedPriceSharePercent),
            N1(r.WastageValueSharePercent),
            N1(r.Step6ChargeSharePercent),
            SheetStatus(r),
            r.DiagnosticMessage
        };

        public static object[] SheetMapExcel(SheetProfitRow r) => new object[]
        {
            r.GlassTypeLabel == "TOTAL" ? "TOTAL" : "SHEET DETAIL",
            r.SpecName, r.GlassTypeLabel, r.PaneGlassDisplay, r.SheetSize, r.SheetSqm,
            r.BuySheets, r.UsedSheets, r.WastageSheets,
            r.BuySheetArea, r.UsedArea, r.WastageArea,
            r.PricePerSqm, r.BuySheetPrice, r.UsedSheetPrice, r.WastageValue, r.Step6Charge,
            r.Step6SourceLabel, r.Step6SourceSqm, r.Step6SheetQuantity, r.Step6Rate, r.UsageEfficiencyPercent,
            r.UsedPriceSharePercent, r.WastageValueSharePercent, r.Step6ChargeSharePercent,
            SheetStatus(r), r.DiagnosticMessage
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
            "SECTION", "SPEC", "KIND", "DESCRIPTION", "SHEET SIZE", "SHEET SOURCE",
            "SHEET SQM", "QTY", "UNIT", "PRICE", "AMOUNT"
        };

        public static string[] PnlMap(ProfitLossRow r) => new[]
        {
            r.Kind == "TOTAL" ? "TOTAL" : "ACTUAL COST LINE",
            r.SpecDisplay,
            r.Kind,
            r.Description,
            r.Kind == "TOTAL"
                ? "—"
                : r.SheetWidthMm > 0
                    ? $"{r.SheetWidthMm} × {r.SheetHeightMm}"
                    : "—",
            r.Kind == "TOTAL" ? "—" : r.SheetSource,
            r.Kind == "TOTAL" ? "—" : r.SheetSqm.ToString("0.####", CultureInfo.InvariantCulture),
            r.Kind == "TOTAL" ? "—" : r.Quantity.ToString("0.##", CultureInfo.InvariantCulture),
            r.Kind == "TOTAL" ? "—" : r.UnitLabel,
            r.Kind == "TOTAL" ? "—" : r.PricePerSqm.ToString("N2", CultureInfo.InvariantCulture),
            N(r.Amount)
        };

        public static object[] PnlMapExcel(ProfitLossRow r) => new object[]
        {
            r.Kind == "TOTAL" ? "TOTAL" : "ACTUAL COST LINE",
            r.SpecDisplay,
            r.Kind,
            r.Description,
            r.Kind == "TOTAL" ? "—" : r.SizeDisplay,
            r.Kind == "TOTAL" ? "—" : r.SheetSource,
            r.Kind == "TOTAL" ? 0m : r.SheetSqm,
            r.Kind == "TOTAL" ? 0m : r.Quantity,
            r.Kind == "TOTAL" ? "—" : r.UnitLabel,
            r.Kind == "TOTAL" ? 0m : r.PricePerSqm,
            r.Amount
        };

        // ── TARGET COMPARISON / SPEC DETAIL ───────────────────────────
        public static readonly string[] TargetHeaders =
        {
            "SECTION", "SPEC", "GLASS / SPECIFICATION", "BUILD", "DIMENSIONS", "AREA (SQM)",
            "GIVEN /SQM", "GIVEN VALUE", "OUTER RATE", "INNER RATE", "THIRD RATE", "ASP RATE",
            "PROCESSING RATE", "YIELD", "UPLIFT %", "FORMULA", "SPEC TGT /SQM",
            "U-INSERT TREATMENT", "U-INSERT DETAIL", "U-INSERT SOURCE", "U-INSERT RATE", "APPLIED U-INSERT",
            "BASE PRICE", "BASE DIFF /SQM", "BASE DIFF VALUE", "BASE MARGIN %",
            "FINAL TGT /SQM", "DIFF /SQM", "REQUIRED TARGET VALUE", "DIFF VALUE",
            "MARGIN AGAINST TARGET %", "STATUS", "WARNING"
        };

        public static string[] TargetMap(SpecTargetRowViewModel r) => new[]
        {
            r.SpecNumber > 0 ? "SPEC DETAIL" : "INVOICE-WIDE",
            r.SpecNumberDisplay,
            r.SpecDisplayName,
            r.BuildTypeLabel,
            OneLine(r.DimensionsSummary),
            r.Result?.AreaSqm.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
            Rate(r.Result?.GivenPricePerSqm),
            N(r.Result?.GivenValue ?? 0m),
            Rate(r.Result?.OuterGlassRatePerSqm),
            Rate(r.Result?.InnerGlassRatePerSqm),
            Rate(r.Result?.ThirdGlassRatePerSqm),
            Rate(r.Result?.AspRatePerSqm),
            Rate(r.Result?.ProcessingRatePerSqm),
            r.Result?.YieldFactor.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
            r.Result?.TargetUpliftPercent.ToString("0.##", CultureInfo.InvariantCulture) ?? "",
            r.FormulaText,
            Rate(r.Result?.SpecificationTargetPerSqm),
            r.UInsertTreatmentLabel,
            r.UInsertTreatmentDetail,
            r.UInsertSourceDisplay,
            Rate(r.Result?.UInsertRatePerSqm),
            r.AppliedUInsertDisplay,
            Rate(r.Result?.BasePricePerSqm),
            Diff(r.Result?.ExtraProfitPerSqm),
            r.BaseDifferenceValueDisplay,
            r.Result?.ExtraProfitMarginPercent?.ToString("0.##", CultureInfo.InvariantCulture) ?? "",
            Rate(r.Result?.FinalRequiredTargetPerSqm),
            Diff(r.Result?.DifferencePerSqm),
            N(r.Result?.RequiredTargetValue ?? 0m),
            r.DifferenceValueDisplay,
            r.Result?.MarginAgainstTargetPercent?.ToString("0.##", CultureInfo.InvariantCulture) ?? "",
            r.StatusLabel,
            r.WarningSummary
        };

        public static object[] TargetMapExcel(SpecTargetRowViewModel r) => new object[]
        {
            r.SpecNumber > 0 ? "SPEC DETAIL" : "INVOICE-WIDE",
            r.SpecNumberDisplay, r.SpecDisplayName, r.BuildTypeLabel, OneLine(r.DimensionsSummary),
            r.Result?.AreaSqm ?? 0m,
            r.Result?.GivenPricePerSqm ?? 0m,
            r.Result?.GivenValue ?? 0m,
            r.Result?.OuterGlassRatePerSqm ?? 0m,
            r.Result?.InnerGlassRatePerSqm ?? 0m,
            r.Result?.ThirdGlassRatePerSqm ?? 0m,
            r.Result?.AspRatePerSqm ?? 0m,
            r.Result?.ProcessingRatePerSqm ?? 0m,
            r.Result?.YieldFactor ?? 0m,
            r.Result?.TargetUpliftPercent ?? 0m,
            r.FormulaText,
            r.Result?.SpecificationTargetPerSqm ?? 0m,
            r.UInsertTreatmentLabel,
            r.UInsertTreatmentDetail,
            r.UInsertSourceDisplay,
            r.Result?.UInsertRatePerSqm ?? 0m,
            r.AppliedUInsertDisplay,
            r.Result?.BasePricePerSqm ?? 0m,
            r.Result?.ExtraProfitPerSqm ?? 0m,
            r.BaseDifferenceValueDisplay,
            r.Result?.ExtraProfitMarginPercent ?? 0m,
            r.Result?.FinalRequiredTargetPerSqm ?? 0m,
            r.Result?.DifferencePerSqm ?? 0m,
            r.Result?.RequiredTargetValue ?? 0m,
            r.DifferenceValueDisplay,
            r.Result?.MarginAgainstTargetPercent ?? 0m,
            r.StatusLabel,
            r.WarningSummary
        };

        // ── OVERVIEW DETAILED EXPORT ───────────────────────────────────
        public sealed class OverviewReportRow
        {
            public string Section { get; init; } = "";
            public string Item { get; init; } = "";
            public string Value { get; init; } = "";
            public string Detail { get; init; } = "";
        }

        public static readonly string[] OverviewHeaders =
        {
            "SECTION", "ITEM", "VALUE", "DETAIL"
        };

        public static string[] OverviewMap(OverviewReportRow r) => new[]
        {
            r.Section, r.Item, r.Value, r.Detail
        };

        public static object[] OverviewMapExcel(OverviewReportRow r) => new object[]
        {
            r.Section, r.Item, r.Value, r.Detail
        };

        public static IReadOnlyList<OverviewReportRow> BuildOverviewRows(ProfitLossStepViewModel vm)
        {
            var rows = new List<OverviewReportRow>();
            if (vm == null) return rows;

            void Add(string section, string item, string value, string detail = "") =>
                rows.Add(new OverviewReportRow { Section = section, Item = item, Value = value, Detail = detail });

            Add("01 DECISION", "Step 9 rating", vm.Step9MaintenanceRating, vm.Step9ImprovementSummary);
            Add("01 DECISION", "Target status", vm.TargetJobStatusLabel, vm.MarginAgainstTargetDisplay);
            Add("01 DECISION", "Required target price", vm.RequiredTargetPriceDisplay, vm.TargetFormulaPreview);
            Add("01 DECISION", "Expected profit margin", vm.ExpectedProfitMarginDisplay, vm.ExpectedProfitMarginSourceLabel);
            Add("01 DECISION", "Calculation status", vm.CalculationStateLabel, vm.LastCalculatedAtDisplay);

            Add("02 MONEY", "Gross total including VAT", vm.GrossDisplay, "From Step 7 grand total.");
            Add("02 MONEY", "Net revenue excluding VAT", vm.NetDisplay, "Uses Step 7 subtotal-before-VAT when available.");
            Add("02 MONEY", "Glass cost", vm.GlassCostDisplay, vm.CostBasisLabel);
            Add("02 MONEY", "Production / service cost", vm.ProductionCostDisplay, "Spacer, processing and active Step 6 production rows.");
            Add("02 MONEY", "Total cost", vm.TotalCostDisplay, "Glass + production cost.");
            Add("02 MONEY", vm.ProfitLabel, vm.ProfitDisplay, vm.MarginDisplay);
            Add("02 MONEY", "Overall actual margin", vm.OverallMarginDisplay, vm.PriceBasedMarginDisplay);

            Add("03 TARGET AGGREGATE", "Given price source", vm.GivenPriceSourceLabel, vm.TargetScopeNote);
            Add("03 TARGET AGGREGATE", "Total given value", vm.TotalGivenValueDisplay, "Area-weighted Step 5 given/base price.");
            Add("03 TARGET AGGREGATE", "Total required target value", vm.TotalRequiredTargetValueDisplay, "Target formula result.");
            Add("03 TARGET AGGREGATE", "Total difference", vm.TotalDifferenceValueDisplay, "Positive means above target.");
            Add("03 TARGET AGGREGATE", "Base margin", vm.BaseMarginPercentDisplay, vm.BaseBalanceValueDisplay);
            Add("03 TARGET AGGREGATE", "Target margin", vm.TargetMarginPercentDisplay, vm.TargetBalanceValueDisplay);
            Add("03 TARGET AGGREGATE", "U-insert treatment", vm.UInsertSummaryDisplay, vm.UInsertSummaryDetail);

            Add("04 FORMULA AUDIT", "All spec formulas", vm.TargetFormulaAllSpecsText, vm.CalculationVersionLabel);
            foreach (var warning in vm.CalculationWarnings ?? Array.Empty<string>())
                Add("04 FORMULA AUDIT", "Warning", warning, "Resolve before approval/output if blocking.");

            foreach (var spec in vm.TargetSpecsView ?? Enumerable.Empty<SpecTargetRowViewModel>())
                Add("05 SPEC SUMMARY", spec.SpecNumberDisplay, spec.FinalRequiredTargetDisplay,
                    $"Given {spec.GivenPricePerSqmDisplay}; diff {spec.DifferencePerSqmDisplay}; margin {spec.MarginAgainstTargetDisplay}; status {spec.StatusLabel}; {spec.FormulaSummaryLine}");

            foreach (var line in vm.Rows ?? Enumerable.Empty<ProfitLossRow>())
                Add("06 ACTUAL COST ROWS", line.SpecDisplay, line.AmountDisplay,
                    $"{line.Kind}; {line.Description}; qty {line.Quantity:0.##} {line.UnitLabel}; rate {line.PricePerSqm:N2}; sheet {line.SizeDisplay}; source {line.SheetSource}");

            return rows;
        }

        private static string OneLine(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? ""
                : value.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
