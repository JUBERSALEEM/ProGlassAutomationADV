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

        private static bool IsSummaryKind(string? kind) =>
            string.Equals(kind, "TOTAL", StringComparison.OrdinalIgnoreCase)
            || (kind ?? "").IndexOf("TOTAL", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsSpacerKind(string? kind) =>
            string.Equals(kind, "Spacer", StringComparison.OrdinalIgnoreCase);

        private static bool IsProductionKind(string? kind) =>
            string.Equals(kind, "Production", StringComparison.OrdinalIgnoreCase)
            || (kind ?? "").IndexOf("Production", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool Mentions(string? value, string token) =>
            (value ?? "").IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string CostClass(ProfitLossRow r)
        {
            if (IsSummaryKind(r.Kind)) return "SUMMARY";
            if (IsSpacerKind(r.Kind)) return "PRODUCTION SERVICE · SPACER / ASP";
            if (Mentions(r.Kind, "Argon") || Mentions(r.Description, "Argon")) return "PRODUCTION ADD-ON · ARGON GAS";
            if (IsProductionKind(r.Kind)) return "PRODUCTION SERVICE · CUTTING / TEMPERING / LAMINATION";
            if (string.Equals(r.Kind, "Glass", StringComparison.OrdinalIgnoreCase)) return "MATERIAL · GLASS SHEET";
            return "COST LINE";
        }

        private static string ServiceClass(ServiceProfitRow r)
        {
            var name = r.ServiceName ?? "";
            if (string.Equals(r.SpecName, "TOTAL", StringComparison.OrdinalIgnoreCase)) return "SUMMARY";
            if (name.IndexOf("Argon", StringComparison.OrdinalIgnoreCase) >= 0)
                return "ARGON GAS DGU CAVITY ADD-ON";
            if (name.IndexOf("Spacer", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("ASP", StringComparison.OrdinalIgnoreCase) >= 0)
                return "SPACER / ASP PRODUCTION SERVICE";
            if (name.IndexOf("Cutting", StringComparison.OrdinalIgnoreCase) >= 0)
                return "CUTTING SERVICE";
            if (name.IndexOf("Tempering", StringComparison.OrdinalIgnoreCase) >= 0)
                return "TEMPERING SERVICE";
            if (name.IndexOf("PVB", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Interlayer", StringComparison.OrdinalIgnoreCase) >= 0)
                return "LAMINATION / INTERLAYER SERVICE";
            if (name.IndexOf("U-Insert", StringComparison.OrdinalIgnoreCase) >= 0)
                return "U-INSERT SERVICE";
            if (name.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0)
                return "GLASS MARGIN";
            return "SERVICE";
        }

        private static decimal ExtendedRate(ProfitLossRow r)
        {
            if (r.PricePerSqm <= 0m) return 0m;
            if (string.Equals(r.Kind, "Glass", StringComparison.OrdinalIgnoreCase)
                && r.SheetSqm > 0m
                && r.Quantity == Math.Round(r.Quantity, 0)
                && Math.Abs(r.Amount - Math.Round(r.SheetSqm * r.Quantity * r.PricePerSqm, 2)) <= 0.02m)
                return Math.Round(r.SheetSqm * r.PricePerSqm, 2);
            return r.PricePerSqm;
        }

        private static string CostBasis(ProfitLossRow r)
        {
            if (IsSummaryKind(r.Kind)) return "Subtotal / control total";
            if (IsSpacerKind(r.Kind))
                return $"Spacer service quantity {r.Quantity:0.##} SQM × AED {r.PricePerSqm:N2}/SQM";
            if (Mentions(r.Kind, "Argon") || Mentions(r.Description, "Argon"))
                return $"Argon gas quantity {r.Quantity:0.##} {r.UnitLabel} × AED {r.PricePerSqm:N2}/{r.UnitLabel}";
            if (IsProductionKind(r.Kind))
                return $"Production service quantity {r.Quantity:0.##} {r.UnitLabel} × AED {r.PricePerSqm:N2}/{r.UnitLabel}";
            if (string.Equals(r.Kind, "Glass", StringComparison.OrdinalIgnoreCase) && r.SheetSqm > 0m)
            {
                bool sheetBased = r.Quantity == Math.Round(r.Quantity, 0)
                    && Math.Abs(r.Amount - Math.Round(r.SheetSqm * r.Quantity * r.PricePerSqm, 2)) <= 0.02m;
                return sheetBased
                    ? $"{r.SheetSqm:0.####} sheet SQM × {r.Quantity:0.##} sheet(s) × AED {r.PricePerSqm:N2}/SQM"
                    : $"{r.Quantity:0.##} required SQM × AED {r.PricePerSqm:N2}/SQM";
            }
            return $"{r.Quantity:0.##} {r.UnitLabel} × AED {r.PricePerSqm:N2}/{r.UnitLabel}";
        }

        private static string ExtendedCalculation(ProfitLossRow r)
        {
            if (IsSummaryKind(r.Kind)) return N(r.Amount);
            if (string.Equals(r.Kind, "Glass", StringComparison.OrdinalIgnoreCase)
                && r.SheetSqm > 0m
                && r.Quantity == Math.Round(r.Quantity, 0)
                && Math.Abs(r.Amount - Math.Round(r.SheetSqm * r.Quantity * r.PricePerSqm, 2)) <= 0.02m)
                return $"{r.SheetSqm:0.####} × {r.Quantity:0.##} × {r.PricePerSqm:N2} = {N(r.Amount)}";
            return $"{r.Quantity:0.##} × {r.PricePerSqm:N2} = {N(r.Amount)}";
        }

        private static string ServiceNote(ProfitLossRow r)
        {
            if (IsSpacerKind(r.Kind))
                return "SPACER / ASP production cost — includes DGU spacer work and related cutting/tempering/assembly where configured in Step 4.";
            if (Mentions(r.Kind, "Argon") || Mentions(r.Description, "Argon"))
                return "ARGON GAS add-on — DGU cavity gas filling. Shown separately from glass sheet price and spacer/ASP service cost.";
            if (IsProductionKind(r.Kind))
                return "Production/service charge — cutting, tempering, lamination/interlayer, U-insert or Step 6 production charge.";
            if (string.Equals(r.Kind, "Glass", StringComparison.OrdinalIgnoreCase))
                return "Glass material line — sheet/area basis shown separately from production service charges.";
            return "";
        }

        // ── SERVICE PROFITABILITY ───────────────────────────────────────
        public static readonly string[] ServiceHeaders =
        {
            "CATEGORY", "SPEC", "SERVICE / ADD-ON", "QTY BASIS", "RATE", "CHARGE",
            "COST / PROFIT", "STATUS / NOTE"
        };

        public static string[] ServiceMap(ServiceProfitRow r) => new[]
        {
            ServiceClass(r),
            r.SpecName,
            $"{r.ServiceName} — {r.Description}",
            r.Area > 0m ? $"{r.Area:0.##} SQM" : "—",
            r.Area > 0m ? $"AED {(r.Charge / r.Area):N2}/SQM" : "—",
            $"AED {r.Charge:N2}",
            $"Cost AED {r.Cost:N2} · Profit AED {r.Profit:N2} · Margin {r.MarginPercent:0.#}%",
            r.SpecName == "TOTAL" ? "Control total"
                : r.Cost <= 0m && r.Charge > 0m ? "Charge visible; detailed cost model not allocated"
                : r.IsProfitable ? "Profit" : "Loss"
        };

        public static object[] ServiceMapExcel(ServiceProfitRow r) => new object[]
        {
            ServiceClass(r),
            r.SpecName,
            $"{r.ServiceName} — {r.Description}",
            r.Area > 0m ? $"{r.Area:0.##} SQM" : "—",
            r.Area > 0m ? Math.Round(r.Charge / r.Area, 2) : 0m,
            r.Charge,
            $"Cost {r.Cost:N2} / Profit {r.Profit:N2} / Margin {r.MarginPercent:0.#}%",
            r.SpecName == "TOTAL" ? "Control total"
                : r.Cost <= 0m && r.Charge > 0m ? "Charge visible; detailed cost model not allocated"
                : r.IsProfitable ? "Profit" : "Loss"
        };

        // ── SHEET PROFITABILITY ─────────────────────────────────────────
        public static readonly string[] SheetHeaders =
        {
            "CATEGORY", "SPEC", "PANE / GLASS", "SHEET SIZE", "SHEET QTY / AREA",
            "RATE / SQM", "SHEET PRICE CALC", "STEP 6 / WASTAGE", "EFFICIENCY", "STATUS / NOTE"
        };

        public static string[] SheetMap(SheetProfitRow r) => new[]
        {
            r.GlassTypeLabel == "TOTAL" ? "TOTAL" : "MATERIAL SHEET PRICE",
            r.SpecName,
            $"{r.GlassTypeLabel} · {r.PaneGlassDisplay}",
            r.SheetSize,
            $"Sheet {r.SheetSqm:0.####} SQM · buy {r.BuySheets:0.##} · used {r.UsedSheets:0.##} · waste {r.WastageSheets:0.##}",
            $"AED {r.PricePerSqm:N2}/SQM",
            $"Buy AED {r.BuySheetPrice:N2} · Used AED {r.UsedSheetPrice:N2} · Waste AED {r.WastageValue:N2}",
            r.Step6Charge > 0m
                ? $"Step 6 AED {r.Step6Charge:N2} · {r.Step6SourceLabel} · {r.Step6SourceSqm:0.###} SQM · sheets {r.Step6SheetQuantityDisplay} · rate {r.Step6RateDisplay}"
                : "No active Step 6 charge on this sheet line",
            $"{r.UsageEfficiencyPercent:N2}% used · {r.UsedPriceSharePercent:0.#}% used value · {r.WastageValueSharePercent:0.#}% waste value",
            $"{SheetStatus(r)} · {r.DiagnosticMessage}"
        };

        public static object[] SheetMapExcel(SheetProfitRow r) => new object[]
        {
            r.GlassTypeLabel == "TOTAL" ? "TOTAL" : "MATERIAL SHEET PRICE",
            r.SpecName,
            $"{r.GlassTypeLabel} · {r.PaneGlassDisplay}",
            r.SheetSize,
            $"Sheet {r.SheetSqm:0.####} SQM / buy {r.BuySheets:0.##} / used {r.UsedSheets:0.##} / waste {r.WastageSheets:0.##}",
            r.PricePerSqm,
            $"Buy {r.BuySheetPrice:N2} / Used {r.UsedSheetPrice:N2} / Waste {r.WastageValue:N2}",
            r.Step6Charge > 0m
                ? $"Step 6 {r.Step6Charge:N2} / {r.Step6SourceLabel} / {r.Step6SourceSqm:0.###} SQM / sheets {r.Step6SheetQuantityDisplay} / rate {r.Step6RateDisplay}"
                : "No active Step 6 charge",
            $"{r.UsageEfficiencyPercent:N2}% used / {r.UsedPriceSharePercent:0.#}% used value / {r.WastageValueSharePercent:0.#}% waste value",
            $"{SheetStatus(r)} · {r.DiagnosticMessage}"
        };

        // The total row is marked by GlassTypeLabel == "TOTAL"; its computed
        // StatusLabel ("NO CHARGE"/"OK") is meaningless, so it prints "—".
        private static string SheetStatus(SheetProfitRow r) =>
            r.GlassTypeLabel == "TOTAL" ? "—" : r.StatusLabel;

        // ── GLASS TYPE COMPARISON ───────────────────────────────────────
        public static readonly string[] GlassTypeHeaders =
        {
            "CATEGORY", "BUILD / SPEC TYPE", "VOLUME", "VALUE SUMMARY", "MARGIN RESULT", "STATUS / NOTE"
        };

        public static string[] GlassTypeMap(GlassTypeComparisonRow r) => new[]
        {
            r.GlassType == "TOTAL" ? "OVERALL SPEC STRATEGY TOTAL" : $"{r.GlassType} SPEC STRATEGY",
            r.GlassType,
            $"{r.JobCount:N0} job(s) · {r.TotalArea:0.##} SQM",
            $"Revenue AED {r.TotalRevenue:N2} · Cost AED {r.TotalCost:N2}",
            $"Profit AED {r.TotalProfit:N2} · Margin {r.MarginPercent:0.#}%",
            GlassStatus(r)
        };

        public static object[] GlassTypeMapExcel(GlassTypeComparisonRow r) => new object[]
        {
            r.GlassType == "TOTAL" ? "OVERALL SPEC STRATEGY TOTAL" : $"{r.GlassType} SPEC STRATEGY",
            r.GlassType,
            $"{r.JobCount:N0} job(s) / {r.TotalArea:0.##} SQM",
            $"Revenue {r.TotalRevenue:N2} / Cost {r.TotalCost:N2}",
            $"Profit {r.TotalProfit:N2} / Margin {r.MarginPercent:0.#}%",
            GlassStatus(r)
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
            "CATEGORY", "SPEC", "ITEM", "QTY × RATE BASIS", "SHEET / SOURCE",
            "AMOUNT", "READING NOTE"
        };

        public static string[] PnlMap(ProfitLossRow r) => new[]
        {
            CostClass(r),
            r.SpecDisplay,
            $"{r.Kind}: {r.Description}",
            CostBasis(r),
            IsSummaryKind(r.Kind) ? "—" : $"{r.SizeDisplay} · {r.SheetSource}",
            $"AED {r.Amount:N2}",
            $"{ExtendedCalculation(r)}. {ServiceNote(r)}"
        };

        public static object[] PnlMapExcel(ProfitLossRow r) => new object[]
        {
            CostClass(r),
            r.SpecDisplay,
            $"{r.Kind}: {r.Description}",
            CostBasis(r),
            IsSummaryKind(r.Kind) ? "—" : $"{r.SizeDisplay} · {r.SheetSource}",
            r.Amount,
            $"{ExtendedCalculation(r)}. {ServiceNote(r)}"
        };

        // ── TARGET COMPARISON / SPEC DETAIL ───────────────────────────
        public static readonly string[] TargetHeaders =
        {
            "CATEGORY", "SPEC", "SPECIFICATION", "BUILD / AREA", "NORMAL SPEC PRICE",
            "RATE COMPONENTS", "TARGET RESULT", "STATUS / FORMULA"
        };

        public static string[] TargetMap(SpecTargetRowViewModel r) => new[]
        {
            r.SpecNumber > 0 ? $"{r.SpecNumberDisplay} · SPEC TARGET PRICE" : "INVOICE-WIDE TARGET",
            r.SpecNumberDisplay,
            r.SpecDisplayName,
            $"{r.BuildTypeLabel} · {OneLine(r.DimensionsSummary)} · Area {r.Result?.AreaSqm.ToString("0.###", CultureInfo.InvariantCulture) ?? ""} SQM",
            $"Given {Rate(r.Result?.GivenPricePerSqm)}/SQM · value AED {N(r.Result?.GivenValue ?? 0m)} · base {Rate(r.Result?.BasePricePerSqm)}/SQM",
            $"Outer {Rate(r.Result?.OuterGlassRatePerSqm)} · Inner {Rate(r.Result?.InnerGlassRatePerSqm)} · Third {Rate(r.Result?.ThirdGlassRatePerSqm)} · ASP {Rate(r.Result?.AspRatePerSqm)} · Processing {Rate(r.Result?.ProcessingRatePerSqm)} · Yield {r.Result?.YieldFactor.ToString("0.###", CultureInfo.InvariantCulture) ?? ""}",
            $"Target {Rate(r.Result?.FinalRequiredTargetPerSqm)}/SQM · required AED {N(r.Result?.RequiredTargetValue ?? 0m)} · diff/SQM {Diff(r.Result?.DifferencePerSqm)} · diff value {r.DifferenceValueDisplay} · margin {r.Result?.MarginAgainstTargetPercent?.ToString("0.##", CultureInfo.InvariantCulture) ?? ""}%",
            $"{r.StatusLabel}. {r.FormulaText}. U-insert: {r.UInsertTreatmentLabel} / {r.AppliedUInsertDisplay}. {r.WarningSummary}"
        };

        public static object[] TargetMapExcel(SpecTargetRowViewModel r) => new object[]
        {
            r.SpecNumber > 0 ? $"{r.SpecNumberDisplay} · SPEC TARGET PRICE" : "INVOICE-WIDE TARGET",
            r.SpecNumberDisplay,
            r.SpecDisplayName,
            $"{r.BuildTypeLabel} / {OneLine(r.DimensionsSummary)} / Area {r.Result?.AreaSqm.ToString("0.###", CultureInfo.InvariantCulture) ?? ""} SQM",
            $"Given {Rate(r.Result?.GivenPricePerSqm)}/SQM / value {N(r.Result?.GivenValue ?? 0m)} / base {Rate(r.Result?.BasePricePerSqm)}/SQM",
            $"Outer {Rate(r.Result?.OuterGlassRatePerSqm)} / Inner {Rate(r.Result?.InnerGlassRatePerSqm)} / Third {Rate(r.Result?.ThirdGlassRatePerSqm)} / ASP {Rate(r.Result?.AspRatePerSqm)} / Processing {Rate(r.Result?.ProcessingRatePerSqm)} / Yield {r.Result?.YieldFactor.ToString("0.###", CultureInfo.InvariantCulture) ?? ""}",
            $"Target {Rate(r.Result?.FinalRequiredTargetPerSqm)}/SQM / required {N(r.Result?.RequiredTargetValue ?? 0m)} / diff/SQM {Diff(r.Result?.DifferencePerSqm)} / diff value {r.DifferenceValueDisplay} / margin {r.Result?.MarginAgainstTargetPercent?.ToString("0.##", CultureInfo.InvariantCulture) ?? ""}%",
            $"{r.StatusLabel}. {r.FormulaText}. U-insert: {r.UInsertTreatmentLabel} / {r.AppliedUInsertDisplay}. {r.WarningSummary}"
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


        private sealed class Step6ExportCharge
        {
            public string Key { get; init; } = "";
            public string Name { get; init; } = "";
            public string Category { get; init; } = "";
            public decimal Qty { get; init; }
            public string Unit { get; init; } = "";
            public decimal Rate { get; init; }
            public decimal Amount { get; init; }
            public string Formula { get; init; } = "";
            public string TargetSummary { get; init; } = "";
        }

        private static IReadOnlyList<Step6ExportCharge> ActiveStep6Charges(ProfitLossStepViewModel vm)
        {
            var result = new List<Step6ExportCharge>();
            var charges = vm?._otherChargesVm?.ChargeRows;
            if (charges == null) return result;

            foreach (var c in charges.Where(x => x.IsActive && x.ResultAmount > 0m))
            {
                var key = (c.TemplateKey ?? "").Trim().ToUpperInvariant();
                result.Add(new Step6ExportCharge
                {
                    Key = key,
                    Name = c.ChargeName ?? key,
                    Category = key == "ARGON_GAS" ? "ARGON GAS DGU CAVITY ADD-ON" : "STEP 6 PRODUCTION / ADD-ON",
                    Qty = c.MetricQty,
                    Unit = c.Unit.ToString().ToUpperInvariant(),
                    Rate = c.Rate,
                    Amount = c.ResultAmount,
                    Formula = c.FormulaDisplay,
                    TargetSummary = c.TargetSelection?.SummaryText ?? ""
                });
            }

            return result;
        }

        public static IReadOnlyList<OverviewReportRow> BuildOverviewRows(ProfitLossStepViewModel vm)
        {
            var rows = new List<OverviewReportRow>();
            if (vm == null) return rows;

            void Add(string section, string item, string value, string detail = "") =>
                rows.Add(new OverviewReportRow { Section = section, Item = item, Value = value, Detail = detail });

            Add("OVERVIEW", "Step 9 rating", vm.Step9MaintenanceRating, vm.Step9ImprovementSummary);
            Add("OVERVIEW", "Target status", vm.TargetJobStatusLabel, vm.MarginAgainstTargetDisplay);
            Add("OVERVIEW", "Required target price", vm.RequiredTargetPriceDisplay, vm.TargetFormulaPreview);
            Add("OVERVIEW", "Expected profit margin", vm.ExpectedProfitMarginDisplay, vm.ExpectedProfitMarginSourceLabel);
            Add("OVERVIEW", "Calculation status", vm.CalculationStateLabel, vm.LastCalculatedAtDisplay);

            Add("SHEET PRICE / MATERIAL", "Gross total including VAT", vm.GrossDisplay, "From Step 7 grand total.");
            Add("SHEET PRICE / MATERIAL", "Net revenue excluding VAT", vm.NetDisplay, "Uses Step 7 subtotal-before-VAT when available.");
            Add("SHEET PRICE / MATERIAL", "Glass sheet cost", vm.GlassCostDisplay, vm.CostBasisLabel);
            Add("PRODUCTION SERVICES", "Production / service cost", vm.ProductionCostDisplay, "Spacer/ASP, cutting, tempering, doubling/lamination and active Step 6 production rows.");

            var spacerRows = (vm.Rows ?? Enumerable.Empty<ProfitLossRow>())
                .Where(r => IsSpacerKind(r.Kind))
                .ToList();
            if (spacerRows.Count > 0)
            {
                Add("PRODUCTION SERVICES", "Spacer / ASP production total",
                    $"AED {spacerRows.Sum(r => r.Amount):N2}",
                    $"{spacerRows.Sum(r => r.Quantity):0.##} SQM service quantity across {spacerRows.Count} spacer line(s). Formula: service SQM × Step 4 ASP rate.");

                foreach (var specSpacer in spacerRows.GroupBy(r => r.SpecDisplay))
                {
                    decimal amount = specSpacer.Sum(r => r.Amount);
                    decimal qty = specSpacer.Sum(r => r.Quantity);
                    decimal weightedRate = qty > 0m ? amount / qty : 0m;
                    Add("SPACER / ASP BY SPECIFICATION", specSpacer.Key,
                        $"AED {amount:N2}",
                        $"Spacer/ASP service: {qty:0.##} SQM × AED {weightedRate:N2}/SQM. This is production/service cost, not a normal glass sheet line.");
                }
            }

            foreach (var charge in ActiveStep6Charges(vm))
            {
                Add(charge.Key == "ARGON_GAS" ? "ARGON GAS / STEP 6 ADD-ONS" : "STEP 6 ADD-ONS",
                    charge.Name,
                    $"AED {charge.Amount:N2}",
                    $"{charge.Category}: {charge.Qty:0.##} {charge.Unit} × AED {charge.Rate:N2} = AED {charge.Amount:N2}. {charge.TargetSummary}. {charge.Formula}");
            }

            Add("PROFIT RESULT", "Total cost", vm.TotalCostDisplay, "Glass + production cost.");
            Add("PROFIT RESULT", vm.ProfitLabel, vm.ProfitDisplay, vm.MarginDisplay);
            Add("PROFIT RESULT", "Overall actual margin", vm.OverallMarginDisplay, vm.PriceBasedMarginDisplay);

            Add("SPEC TARGET PRICE", "Given price source", vm.GivenPriceSourceLabel, vm.TargetScopeNote);
            Add("SPEC TARGET PRICE", "Total given value", vm.TotalGivenValueDisplay, "Area-weighted Step 5 given/base price.");
            Add("SPEC TARGET PRICE", "Total required target value", vm.TotalRequiredTargetValueDisplay, "Target formula result.");
            Add("SPEC TARGET PRICE", "Total difference", vm.TotalDifferenceValueDisplay, "Positive means above target.");
            Add("SPEC TARGET PRICE", "Base margin", vm.BaseMarginPercentDisplay, vm.BaseBalanceValueDisplay);
            Add("SPEC TARGET PRICE", "Target margin", vm.TargetMarginPercentDisplay, vm.TargetBalanceValueDisplay);
            Add("SPEC TARGET PRICE", "U-insert treatment", vm.UInsertSummaryDisplay, vm.UInsertSummaryDetail);

            Add("CALCULATION FORMULA AUDIT", "All spec formulas", vm.TargetFormulaAllSpecsText, vm.CalculationVersionLabel);
            foreach (var warning in vm.CalculationWarnings ?? Array.Empty<string>())
                Add("CALCULATION FORMULA AUDIT", "Warning", warning, "Resolve before approval/output if blocking.");

            foreach (var spec in vm.TargetSpecsView ?? Enumerable.Empty<SpecTargetRowViewModel>())
                Add("SPECIFICATION SUMMARY", spec.SpecNumberDisplay, spec.FinalRequiredTargetDisplay,
                    $"Given {spec.GivenPricePerSqmDisplay}; diff {spec.DifferencePerSqmDisplay}; margin {spec.MarginAgainstTargetDisplay}; status {spec.StatusLabel}; {spec.FormulaSummaryLine}");

            foreach (var line in vm.Rows ?? Enumerable.Empty<ProfitLossRow>())
                Add("ACTUAL COST LEDGER", line.SpecDisplay, line.AmountDisplay,
                    $"{CostClass(line)}; {line.Description}; basis {CostBasis(line)}; calc {ExtendedCalculation(line)}; sheet {line.SizeDisplay}; source {line.SheetSource}; {ServiceNote(line)}");

            return rows;
        }

        private static string OneLine(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? ""
                : value.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
