// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/OrderReport/OrderReportStep9PriceDecisionTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Keeps the saved Order Report's Step 9 projection aligned with the live
// Step 9 report: normal formula base, Step 5 quote, BASE result and TARGET
// result are intentionally separate.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.OrderReport;
using ProGlassAutomation.ViewModels.Reports.OrderReport;
using System;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace ProGlassAutomation.Tests.OrderReport
{
    public sealed class OrderReportStep9PriceDecisionTests
    {
        [Fact]
        public void Saved_target_row_keeps_formula_base_given_and_both_results_explicit()
        {
            var row = CreateCanonicalTargetRow();

            Assert.Contains("Outer AED 48.00/SQM", row.FormulaInputsSummary);
            Assert.Contains("Yield 0.85", row.FormulaInputsSummary);
            Assert.Contains("Profit uplift 15%", row.FormulaInputsSummary);
            Assert.Contains("(48.00 + 25.00) ÷ 0.85 + 45.00", row.NormalFormulaBaseExpression);
            Assert.Contains("AED 130.88/SQM", row.NormalFormulaBaseSummary);
            Assert.Contains("AED 160.00/SQM", row.Step5GivenSummary);
            Assert.Contains("judged AED 160.00/SQM", row.Step5GivenSummary);
            Assert.Contains("AED 29.12/SQM", row.BaseResultSummary);
            Assert.Contains("+22.25%", row.BaseResultSummary);
            Assert.Contains("AED 150.51/SQM", row.RequiredTargetSummary);
            Assert.Contains("Normal formula base AED 130.88/SQM × 1.15 = required target AED 150.51/SQM", row.RequiredTargetSummary);
            Assert.Contains("AED 9.49/SQM", row.TargetResultSummary);
            Assert.Contains("+6.31%", row.TargetResultSummary);
        }

        [Fact]
        public void Saved_report_export_builds_a_complete_distinct_section_for_every_specification()
        {
            var first = CreateCanonicalTargetRow();
            var second = CreateCanonicalTargetRow();
            second.SpecNumber = 2;
            second.SpecName = "Second DGU specification";

            var rows = OrderReportGeneratorViewModel.BuildTargetDecisionExportRows(
                "PI-2026-001",
                new[] { first, second });

            Assert.Equal(16, rows.Count);
            Assert.Equal(new[]
            {
                "STEP 9 · PI-2026-001 · SPEC 1 PRICE DECISION",
                "STEP 9 · PI-2026-001 · SPEC 2 PRICE DECISION"
            }, rows.Select(r => r.Section).Distinct().ToArray());
            Assert.All(rows.GroupBy(r => r.Section), section => Assert.Equal(8, section.Count()));

            var firstBaseResult = rows.Single(r =>
                r.Section.EndsWith("SPEC 1 PRICE DECISION", StringComparison.Ordinal)
                && r.Item == "BASE result — against normal formula base");
            var firstTargetResult = rows.Single(r =>
                r.Section.EndsWith("SPEC 1 PRICE DECISION", StringComparison.Ordinal)
                && r.Item == "TARGET result — against required target");

            Assert.Contains("+22.25%", firstBaseResult.Basis);
            Assert.Equal(29.12m, firstBaseResult.Amount!.Value);
            Assert.Contains("+6.31%", firstTargetResult.Basis);
            Assert.Equal(9.49m, firstTargetResult.Amount!.Value);
        }

        [Fact]
        public void Older_saved_report_row_marks_missing_additive_values_instead_of_making_them_zero()
        {
            var oldRow = new OrderReportStep9TargetRow
            {
                SpecNumber = 1,
                BuildType = "Dgu",
                AreaSqm = 10m,
                GivenPricePerSqm = 160m,
                BasePricePerSqm = 130.88m,
                FinalRequiredTargetPerSqm = 150.51m,
                MarginAgainstTargetPercent = 6.31m,
                Status = "AboveTarget"
            };

            Assert.Contains("not saved", oldRow.Step5GivenSummary);
            Assert.Contains("not saved", oldRow.RequiredTargetSummary);
            Assert.Contains("not saved", oldRow.TargetResultSummary);
            Assert.Contains("Profit uplift unavailable", oldRow.FormulaInputsSummary);
            Assert.Null(oldRow.GivenValueForExport);
            Assert.Null(oldRow.RequiredTargetValueForExport);
            Assert.Null(oldRow.DifferenceValueForExport);

            var exportedRows = OrderReportGeneratorViewModel.BuildTargetDecisionExportRows(
                "PI-LEGACY", new[] { oldRow });
            Assert.Null(exportedRows.Single(r => r.Item == "Step 5 given / quoted price").Amount);
            Assert.Null(exportedRows.Single(r => r.Item == "Required target price — formula base + profit").Amount);
            Assert.Null(exportedRows.Single(r => r.Item == "TARGET result — against required target").Amount);
        }

        [Fact]
        public void A_saved_zero_percent_target_uplift_is_shown_as_a_valid_zero_not_as_missing_data()
        {
            var row = CreateCanonicalTargetRow();
            row.TargetUpliftPercent = 0m;
            row.SpecificationTargetPerSqm = 130.88m;
            row.FinalRequiredTargetPerSqm = 130.88m;
            row.RequiredTargetValue = 130.88m;

            Assert.Contains("Profit uplift 0%", row.FormulaInputsSummary);
            Assert.Contains("× 1 = required target AED 130.88/SQM", row.RequiredTargetSummary);
            Assert.Contains("uplift 0%", row.RequiredTargetSummary);
            Assert.DoesNotContain("not saved", row.RequiredTargetSummary);
        }

        private static OrderReportStep9TargetRow CreateCanonicalTargetRow() => new()
        {
            SpecNumber = 1,
            SpecName = "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass",
            BuildType = "Dgu",
            AreaSqm = 1m,
            GivenPricePerSqm = 160m,
            GivenValue = 160m,
            OuterGlassRatePerSqm = 48m,
            InnerGlassRatePerSqm = 25m,
            AspRatePerSqm = 45m,
            YieldFactor = 0.85m,
            TargetUpliftPercent = 15m,
            SpecificationTargetPerSqm = 150.51m,
            BasePricePerSqm = 130.88m,
            GivenPriceJudgedPerSqm = 160m,
            ExtraProfitPerSqm = 29.12m,
            BaseDifferenceValue = 29.12m,
            ExtraProfitMarginPercent = 22.25m,
            FinalRequiredTargetPerSqm = 150.51m,
            RequiredTargetValue = 150.51m,
            DifferencePerSqm = 9.49m,
            DifferenceValue = 9.49m,
            MarginAgainstTargetPercent = 6.31m,
            Status = "AboveTarget",
            FormulaText = "((48 + 25) ÷ 0.85 + 45) × 1.15"
        };

        [Fact]
        public void Decision_audit_export_lines_leave_unknown_money_blank_instead_of_printing_zero()
        {
            var exportRow = new OrderReportStep9ExportRow
            {
                Section = "STEP 9 · PI-1 · SPEC 1 PRICE DECISION",
                Item = "Formula inputs"
            };

            Assert.Equal("—", exportRow.ToCsvRow()[6]);
            Assert.Equal("", exportRow.ToExcelRow()[6]);
        }

        [Fact]
        public void Computed_presentation_text_is_not_serialized_into_a_saved_order_report()
        {
            var row = new OrderReportStep9TargetRow
            {
                SpecNumber = 1,
                BuildType = "Dgu",
                GivenPricePerSqm = 160m,
                GivenValue = 160m,
                BasePricePerSqm = 130.88m,
                FinalRequiredTargetPerSqm = 150.51m,
                RequiredTargetValue = 150.51m
            };

            string json = JsonSerializer.Serialize(row);

            Assert.Contains("GivenValue", json);
            Assert.Contains("BasePricePerSqm", json);
            Assert.DoesNotContain("FormulaInputsSummary", json);
            Assert.DoesNotContain("NormalFormulaBaseSummary", json);
            Assert.DoesNotContain("TargetResultSummary", json);
            Assert.DoesNotContain("GivenValueForExport", json);
            Assert.DoesNotContain("RequiredTargetValueForExport", json);
            Assert.DoesNotContain("DifferenceValueForExport", json);
            Assert.DoesNotContain("AuditSummary", json);
        }
    }
}
