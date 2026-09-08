// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/ProformaInvoice/Step9PriceDecisionReportTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// PINS THE STEP 9 EXPORT PRICE-DECISION PROJECTION
// ─────────────────────────────────────────────────────────────────────────
// These tests deliberately exercise the canonical target calculator first,
// then verify that the report projection only exposes its already-calculated
// values. They protect the distinction between:
//   • the Step 5 customer quote (the source control calls it Base Price), and
//   • the normal pre-profit formula base used by the BASE result.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.ProformaInvoice.TargetPricing;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.ProformaInvoice
{
    public sealed class Step9PriceDecisionReportTests
    {
        [Fact]
        public void Per_spec_price_decision_exports_the_customer_dgu_vector_without_mixing_price_bases()
        {
            var spec = CreateDguSpec(
                specNumber: 1,
                outer: 48m,
                inner: 25m,
                asp: 45m,
                given: 160m,
                area: 1m);

            var rows = Step9ReportExport.BuildPriceDecisionRows(spec);

            Assert.Equal(8, rows.Count);
            Assert.All(rows, row => Assert.Equal("SPEC 1 · PRICE DECISION", row.Section));

            var normalFormulaBase = rows.Single(row =>
                row.Item == "Normal formula base price — no profit");
            Assert.Equal("AED 130.88/SQM", normalFormulaBase.Value);
            Assert.Contains("(48.00 + 25.00) ÷ 0.85 + 45.00 = AED 130.88/SQM",
                normalFormulaBase.Detail);
            Assert.Contains("not the Step 5 given / quoted price", normalFormulaBase.Detail);

            var step5Quote = rows.Single(row => row.Item == "Step 5 given / quoted price");
            Assert.Equal("AED 160.00/SQM", step5Quote.Value);
            Assert.Contains("customer quoted rate from Step 5", step5Quote.Detail);

            var baseResult = rows.Single(row =>
                row.Item == "BASE result — against normal formula base");
            Assert.Contains("AED 29.12/m²", baseResult.Value);
            Assert.Contains("+22.25%", baseResult.Value);
            Assert.Contains("Given judged AED 160.00/SQM − normal formula base AED 130.88/SQM",
                baseResult.Detail);

            var requiredTarget = rows.Single(row =>
                row.Item == "Required target price — formula base + profit");
            Assert.Equal("AED 150.51/SQM", requiredTarget.Value);
            Assert.Contains("AED 130.88/SQM × 1.15 (15% profit) = AED 150.51/SQM",
                requiredTarget.Detail);

            var targetResult = rows.Single(row =>
                row.Item == "TARGET result — against required target");
            Assert.Contains("AED 9.49/m²", targetResult.Value);
            Assert.Contains("6.31%", targetResult.Value);
            Assert.Contains("given judged AED 160.00/SQM against required target AED 150.51/SQM",
                targetResult.Detail);
        }

        [Fact]
        public void Target_comparison_columns_keep_formula_base_given_base_result_and_target_result_separate()
        {
            var spec = CreateDguSpec(
                specNumber: 1,
                outer: 48m,
                inner: 25m,
                asp: 45m,
                given: 160m,
                area: 1m);

            string[] cells = Step9ReportExport.TargetMap(spec);
            object[] excelCells = Step9ReportExport.TargetMapExcel(spec);

            Assert.Equal(9, Step9ReportExport.TargetHeaders.Length);
            Assert.Equal("SECTION", Step9ReportExport.TargetHeaders[0]);
            Assert.Equal("SECTION", Step9ReportExport.OverviewHeaders[0]);
            Assert.Contains("NORMAL FORMULA BASE (NO PROFIT)", Step9ReportExport.TargetHeaders);
            Assert.Contains("STEP 5 GIVEN / QUOTED PRICE", Step9ReportExport.TargetHeaders);
            Assert.Contains("BASE RESULT", Step9ReportExport.TargetHeaders);
            Assert.Contains("REQUIRED TARGET (+ PROFIT)", Step9ReportExport.TargetHeaders);
            Assert.Contains("TARGET RESULT / STATUS", Step9ReportExport.TargetHeaders);

            Assert.Equal("SPEC 1 · PRICE DECISION", cells[0]);
            Assert.Contains("AED 130.88/SQM", cells[3]);
            Assert.Contains("AED 160.00/SQM", cells[4]);
            Assert.Contains("+22.25%", cells[5]);
            Assert.Contains("AED 150.51/SQM", cells[6]);
            Assert.Contains("Normal formula base AED 130.88/SQM × 1.15 = required target AED 150.51/SQM", cells[6]);
            Assert.Contains("6.31%", cells[7]);
            Assert.Equal(cells, excelCells.Cast<string>());
        }

        [Fact]
        public void Every_specification_gets_its_own_price_decision_section()
        {
            var first = CreateDguSpec(1, 48m, 25m, 45m, 160m, 1m);
            var second = CreateDguSpec(2, 25m, 25m, 45m, 125m, 1m);

            var exportedRows = new List<Step9ReportExport.OverviewReportRow>();
            exportedRows.AddRange(Step9ReportExport.BuildPriceDecisionRows(first));
            exportedRows.AddRange(Step9ReportExport.BuildPriceDecisionRows(second));

            string[] sections = exportedRows
                .Select(row => row.Section)
                .Distinct()
                .ToArray();

            Assert.Equal(new[]
            {
                "SPEC 1 · PRICE DECISION",
                "SPEC 2 · PRICE DECISION"
            }, sections);
            Assert.Equal(16, exportedRows.Count);
        }

        [Fact]
        public void Overview_starts_with_sheet_operations_then_spacer_asp_detail_without_recalculating_costs()
        {
            var vm = new ProfitLossStepViewModel();
            try
            {
                vm.Rows.Add(new ProfitLossRow
                {
                    Kind = "Glass",
                    SpecNumber = 1,
                    SpecName = "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass",
                    Description = "6mm HD Grey FT Glass",
                    SheetWidthMm = 3210,
                    SheetHeightMm = 2250,
                    SheetSqm = 7.2225m,
                    Quantity = 2m,
                    PricePerSqm = 48m,
                    Amount = 693.36m,
                    SheetSource = "from Sheet Store"
                });
                vm.Rows.Add(new ProfitLossRow
                {
                    Kind = "Spacer",
                    SpecNumber = 1,
                    SpecName = "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass",
                    Description = "12mm ASP",
                    Quantity = 3.5m,
                    UnitLabel = "SQM",
                    PricePerSqm = 45m,
                    Amount = 157.50m
                });

                var rows = Step9ReportExport.BuildOverviewRows(vm).ToList();

                var sheet = rows.Single(row => row.Section == "SHEET OPERATIONAL DETAIL");
                var spacer = rows.Single(row => row.Section == "SPACER / ASP PRODUCTION DETAIL");
                var sheetPricing = rows.Single(row => row.Section == "SHEET PRICING SUMMARY");
                int sheetIndex = rows.IndexOf(sheet);
                int spacerIndex = rows.IndexOf(spacer);
                int sheetPricingIndex = rows.IndexOf(sheetPricing);
                int overviewIndex = rows.FindIndex(row => row.Section == "OVERVIEW");

                Assert.Equal(0, sheetIndex);
                Assert.True(sheetIndex < spacerIndex);
                Assert.True(spacerIndex < sheetPricingIndex);
                Assert.True(sheetPricingIndex < overviewIndex);
                Assert.Equal("AED 693.36", sheetPricing.Value);
                Assert.Contains("SHEET OPERATIONAL DETAIL", sheetPricing.Detail);
                Assert.Equal("AED 693.36", sheet.Value);
                Assert.Contains("Sheet size 3210 × 2250 mm · 7.2225 SQM", sheet.Detail);
                Assert.Contains("sheet quantity 2 sheet(s)", sheet.Detail);
                Assert.Contains("rate AED 48.00/SQM", sheet.Detail);
                Assert.Contains("amount AED 693.36", sheet.Detail);
                Assert.Contains("source from Sheet Store", sheet.Detail);
                Assert.Contains("7.2225 sheet SQM × 2 sheet(s) × AED 48.00/SQM", sheet.Detail);

                Assert.Equal("AED 157.50", spacer.Value);
                Assert.Contains("Spacer / ASP production service", spacer.Detail);
                Assert.Contains("quantity 3.5 SQM", spacer.Detail);
                Assert.Contains("rate AED 45.00/SQM", spacer.Detail);
                Assert.Contains("amount AED 157.50", spacer.Detail);
                Assert.Contains("Spacer service quantity 3.5 SQM × AED 45.00/SQM", spacer.Detail);
            }
            finally
            {
                vm.Dispose();
            }
        }

        [Fact]
        public void Included_u_insert_keeps_the_required_target_normal_and_explains_the_judged_given_price()
        {
            var result = SpecificationTargetCalculator.Calculate(
                4,
                "6mm Clear FT + 12mm ASP + 6mm Clear FT",
                "DGU",
                GlassBuildType.Dgu,
                new SpecificationRateCard
                {
                    BuildType = GlassBuildType.Dgu,
                    OuterGlassRatePerSqm = 25m,
                    InnerGlassRatePerSqm = 25m,
                    AspRatePerSqm = 45m,
                    YieldFactor = 0.85m,
                    TargetUpliftPercent = 15m
                },
                givenPricePerSqm: 125m,
                areaSqm: 1m);
            SpecificationTargetCalculator.ApplyUInsert(result, new UInsertResolution
            {
                Treatment = UInsertTreatment.IncludedInStep5Price,
                RatePerSqm = 10m
            });

            string[] cells = Step9ReportExport.TargetMap(
                SpecTargetRowViewModel.FromResult(result));

            Assert.Contains("AED 125.00/SQM", cells[4]);
            Assert.Contains("judged AED 115.00/SQM", cells[4]);
            Assert.Contains("AED 11.18/m²", cells[5]);
            Assert.Contains("+10.77%", cells[5]);
            Assert.Contains("AED 119.39/SQM", cells[6]);
            Assert.DoesNotContain("129.39", cells[6]);
            Assert.Contains("Included in Step 5 price", cells[8]);
        }

        private static SpecTargetRowViewModel CreateDguSpec(
            int specNumber,
            decimal outer,
            decimal inner,
            decimal asp,
            decimal given,
            decimal area)
        {
            var result = SpecificationTargetCalculator.Calculate(
                specNumber,
                $"DGU specification {specNumber}",
                "DGU",
                GlassBuildType.Dgu,
                new SpecificationRateCard
                {
                    BuildType = GlassBuildType.Dgu,
                    OuterGlassRatePerSqm = outer,
                    InnerGlassRatePerSqm = inner,
                    AspRatePerSqm = asp,
                    YieldFactor = 0.85m,
                    TargetUpliftPercent = 15m
                },
                given,
                area);
            SpecificationTargetCalculator.ApplyUInsert(result, new UInsertResolution
            {
                Treatment = UInsertTreatment.NotApplicable
            });

            return SpecTargetRowViewModel.FromResult(result);
        }
    }
}
