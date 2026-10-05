// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Architecture/MoneyRoundingRatchetTests.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Tests.Architecture
//
// PURPOSE
// ───────
// Holds the line on Phase 2 (docs/MONEY_ROUNDING_POLICY.md).
//
// Money must round through Services/Reporting/MoneyRounding so the whole
// pipeline uses one rule. It does not yet: 334 call sites in the money
// namespaces still call Math.Round(value, 2), whose default is
// MidpointRounding.ToEven, while the invoice and VAT layers round
// AwayFromZero.
//
// WHY THIS IS A RATCHET AND NOT A CONVERSION
// ──────────────────────────────────────────
// The same two-argument Math.Round call rounds money to 2dp in one place,
// square metres to 3-4dp in another, and counts to integers in a third — often
// in the same file. ProfitLossStepViewModel.Calculation.cs has 31 of them and
// mixes money with Quantity fields. Converting them without a compiler to catch
// a misclassification would silently corrupt a measurement to fix a rounding
// mode.
//
// So the migration is one file at a time with a build between each. This test
// makes that safe: it fails if the count grows anywhere, and fails again if a
// converted file is left in the allow-list, so the list cannot rot.
//
// HOW TO MIGRATE A FILE
// ─────────────────────
//   1. Read every Math.Round in it and classify: money, quantity, percentage,
//      or display. The policy document lists which precisions belong to which.
//   2. Route the money ones through MoneyRounding.Round. Leave the rest, and
//      say why in a comment so the next reader does not redo the analysis.
//   3. Build. Confirm no figure moved except on exact half-fil inputs.
//   4. Lower this file's entry, or delete it when it reaches zero.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.Architecture
{
    public sealed class MoneyRoundingRatchetTests
    {
        /// <summary>
        /// Directories whose rounding is monetary in at least some places.
        /// Anything under these that calls Math.Round without an explicit mode
        /// is counted here.
        /// </summary>
        private static readonly string[] MoneyNamespaces =
        {
            "Services/Reporting",
            "Services/Statement",
            "Services/TaxInvoice",
            "Services/ProformaInvoice",
            "ViewModels/ProformaInvoice",
            "ViewModels/JobProfitability",
            "Services/Dashboard"
        };

        /// <summary>
        /// Baseline measured 2026-09-09 was 334 call sites across 62 files.
        /// Invoice, statement, Step 5/7, and target-rate money now routes
        /// through MoneyRounding, so those entries are lowered or removed.
        /// Remaining entries are unclassified area, quantity, or percent
        /// calls, or money not yet migrated. Remove an entry as soon as its
        /// file is migrated — the stale-entry assertion fails until you do.
        /// </summary>
        private static readonly Dictionary<string, int> AllowList =
            new(StringComparer.Ordinal)
        {
            ["Services/ProformaInvoice/Export/ProformaChargeMetricsCalculator.cs"] = 10,
            ["Services/ProformaInvoice/Export/ProformaDossierExcelLayout.cs"] = 1,
            ["Services/ProformaInvoice/Export/ProformaDossierPdfLayout.cs"] = 1,
            ["Services/ProformaInvoice/Export/ProformaInvoiceCsvLayout.cs"] = 1,
            ["Services/ProformaInvoice/Export/ProformaInvoiceExcelLayout.cs"] = 2,
            ["Services/ProformaInvoice/Export/ProformaInvoicePdfLayout.cs"] = 1,
            ["Services/ProformaInvoice/SpecGroupService.cs"] = 1,
            ["Services/ProformaInvoice/TargetPricing/SpecificationTargetCalculator.cs"] = 2,
            ["Services/ProformaInvoice/TargetPricing/Step5PriceSource.cs"] = 1,
            ["Services/ProformaInvoice/TargetPricing/TargetPriceModels.cs"] = 4,
            ["Services/ProformaInvoice/TargetPricing/TargetSnapshotRecompute.cs"] = 3,
            ["Services/Reporting/ExecutiveSummary.cs"] = 1,
            ["Services/Reporting/MoneyRounding.cs"] = 2,
            ["Services/Reporting/ReportingAnalyticsBuilder.cs"] = 7,
            ["Services/Reporting/ReportingAnalyticsModels.cs"] = 1,
            ["Services/Reporting/ReportingDataService.DrillDown.cs"] = 1,
            ["Services/Reporting/ReportingDataService.Executive.cs"] = 2,
            ["Services/Reporting/ReportingDataService.Jo.cs"] = 1,
            ["Services/Reporting/ReportingDataService.Pi.cs"] = 2,
            ["Services/Reporting/ReportingDataService.Profitability.cs"] = 3,
            ["Services/Reporting/ReportingModels.cs"] = 3,
            ["Services/Reporting/RowTraceabilityService.cs"] = 1,
            ["Services/Reporting/SpecificationIntelligenceService.cs"] = 1,
            ["Services/Statement/Aggregation/StatementAggregator.Part1.cs"] = 1,
            ["Services/Statement/Pdf/StatementPdfDocument.cs"] = 1,
            ["Services/Statement/Workflow/StatementSnapshotProjector.cs"] = 1,
            ["Services/TaxInvoice/Mapper/TaxInvoiceMapper.cs"] = 5,
            ["Services/TaxInvoice/Pdf/TaxInvoicePdfDocument.cs"] = 2,
            ["Services/TaxInvoice/TaxInvoiceLineService.cs"] = 1,
            ["ViewModels/JobProfitability/JobDetailViewModel.cs"] = 3,
            ["ViewModels/JobProfitability/JobProfitabilityRow.cs"] = 2,
            ["ViewModels/JobProfitability/JobProfitabilityViewModel.cs"] = 16,
            ["ViewModels/JobProfitability/PortfolioGlassTypeComparisonViewModel.cs"] = 2,
            ["ViewModels/JobProfitability/PortfolioServiceProfitabilityViewModel.cs"] = 2,
            ["ViewModels/JobProfitability/PortfolioWastageAnalysisViewModel.cs"] = 2,
            ["ViewModels/ProformaInvoice/Step4PricingEstimator/DguLamParameters/DguLamParametersViewModel.cs"] = 1,
            ["ViewModels/ProformaInvoice/Step5DataGrid/GlassItemRow.cs"] = 12,
            ["ViewModels/ProformaInvoice/Step5DataGrid/GrandTotalSummary/GrandTotalSummaryViewModel.cs"] = 2,
            ["ViewModels/ProformaInvoice/Step5DataGrid/SpecGroup.Part2.cs"] = 4,
            ["ViewModels/ProformaInvoice/Step6OtherCharges/MetricDiscoveryHubViewModel.cs"] = 15,
            ["ViewModels/ProformaInvoice/Step6OtherCharges/OtherChargesViewModel.cs"] = 1,
            ["ViewModels/ProformaInvoice/Step7PrintPreview/InvoiceMainSummary/InvoiceMainSummaryViewModel.cs"] = 1,
            ["ViewModels/ProformaInvoice/Step7PrintPreview/PrintableInvoice/PrintableInvoiceViewModel.Part1.cs"] = 1,
            ["ViewModels/ProformaInvoice/Step9ProfitLoss/GlassTypeComparisonViewModel.cs"] = 3,
            ["ViewModels/ProformaInvoice/Step9ProfitLoss/PnLMeasurement.cs"] = 1,
            ["ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.Calculation.cs"] = 31,
            ["ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.CalculationSupport.cs"] = 7,
            ["ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.CostBasis.cs"] = 4,
            ["ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.QuoteBasis.cs"] = 9,
            ["ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.TargetPricing.cs"] = 5,
            ["ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.cs"] = 1,
            ["ViewModels/ProformaInvoice/Step9ProfitLoss/ServiceProfitabilityViewModel.cs"] = 9,
            ["ViewModels/ProformaInvoice/Step9ProfitLoss/SheetProfitabilityViewModel.cs"] = 14,
            ["ViewModels/ProformaInvoice/Step9ProfitLoss/SpecCostGroup.cs"] = 4,
            ["ViewModels/ProformaInvoice/Step9ProfitLoss/SpecTargetRowViewModel.cs"] = 1,
            ["ViewModels/ProformaInvoice/Step9ProfitLoss/Step6SheetAllocation.cs"] = 1,
            ["ViewModels/ProformaInvoice/Step9ProfitLoss/Step9ReportExport.cs"] = 10,
            ["ViewModels/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionViewModel.cs"] = 1,
        };

        [Fact]
        public void No_money_namespace_gains_an_unrouted_MathRound()
        {
            var measured = Measure();

            var grown = AllowList
                .Where(kv => measured.TryGetValue(kv.Key, out var now) && now > kv.Value)
                .Select(kv => $"  {kv.Key}: {kv.Value} -> {measured[kv.Key]}")
                .ToList();

            Assert.True(
                grown.Count == 0,
                "New Math.Round(value, 2) calls appeared in the money namespaces. " +
                "Money must round through MoneyRounding.Round so the pipeline uses " +
                "one rule (docs/MONEY_ROUNDING_POLICY.md). If the call is not " +
                "monetary — a quantity, an area, a percentage — say so in a " +
                "comment at the call site and raise the allow-list entry here.\n" +
                string.Join("\n", grown));
        }

        [Fact]
        public void No_new_file_in_a_money_namespace_rounds_without_a_mode()
        {
            var measured = Measure();

            var unlisted = measured.Keys
                .Where(f => !AllowList.ContainsKey(f))
                .OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => $"  {f}: {measured[f]}")
                .ToList();

            Assert.True(
                unlisted.Count == 0,
                "A file in the money namespaces rounds without an explicit mode " +
                "and is not in the allow-list. Either route its monetary values " +
                "through MoneyRounding.Round, or add it here with its count and a " +
                "note explaining why the calls are not monetary.\n" +
                string.Join("\n", unlisted));
        }

        [Fact]
        public void Allow_list_has_no_stale_entries()
        {
            var measured = Measure();

            var stale = AllowList
                .Where(kv => !measured.TryGetValue(kv.Key, out var now) || now < kv.Value)
                .Select(kv => measured.ContainsKey(kv.Key)
                    ? $"  {kv.Key}: allow-list {kv.Value}, now {measured[kv.Key]}"
                    : $"  {kv.Key}: allow-list {kv.Value}, file no longer rounds this way")
                .ToList();

            Assert.True(
                stale.Count == 0,
                "These allow-list entries are out of date. A migrated file must be " +
                "lowered or removed, otherwise the ratchet stops protecting the " +
                "files that still need work.\n" +
                string.Join("\n", stale));
        }

        /// <summary>
        /// The files that may name the rounding mode directly.
        ///
        /// Exactly one of them is money: <c>Services/Reporting/MoneyRounding.cs</c>,
        /// the money home and the whole point of Phase 2. Every other entry is a
        /// QUANTITY — a piece count, a frame count, an area in m², a length, or a
        /// display total — where away-from-zero is simply "round half up" and
        /// routing it through MoneyRounding would be wrong, because that would
        /// assert a currency precision on a measurement.
        ///
        /// 2026-10-05: this list was pinned at two files while the production and
        /// optimisation modules were still being written, so the assertion below
        /// (an exact collection equality, not a subset check) went red as soon as
        /// they declared a mode. The seven additions are all quantities; none is
        /// a money path. Adding a file here is the correct action ONLY when the
        /// value being rounded is not money — the allow-list in this same file is
        /// what tracks money still awaiting migration.
        ///
        /// ORDER MATTERS: the assertion is <c>Assert.Equal</c> against a list
        /// sorted with <see cref="StringComparer.Ordinal"/>, so this array must
        /// stay in ordinal order.
        ///
        ///   file                                         what it rounds
        //   WindowScreenRecorder.cs                      frames: fps x 1.5 -> integer frame count
        //   VideoEncoder.cs                              frames: fps x keyframe interval -> integer count
        //   OptimizationModels.cs                        area to 4 dp and utilisation percentage
        //   ProductionDistributionEngine.cs              length to 3 dp
        //   ProductionRepository.cs                      SQM to 3 dp on flow and arrival totals
        //   CutListImportParser.cs                       parsed piece count -> integer (the original carve-out)
        //   MoneyRounding.cs                             the money home - the whole point of Phase 2
        //   OptimizationViewModel.Preview.cs             piece area m2 to 3 dp
        //   ProductionStageViewModelBase.Detail.cs       SQM/LM display totals to 2 dp
        /// </summary>
        private static readonly string[] AllowedModeDeclarations =
        {
            "Agent/Demo/Recording/WindowScreenRecorder.cs",
            "Agent/Demo/Video/VideoEncoder.cs",
            "Services/Optimization/OptimizationModels.cs",
            "Services/Production/ProductionDistributionEngine.cs",
            "Services/Production/ProductionRepository.cs",
            "Services/ProformaInvoice/Step5Grid/CutListImportParser.cs",
            "Services/Reporting/MoneyRounding.cs",
            "ViewModels/Optimization/OptimizationViewModel.Preview.cs",
            "ViewModels/Production/ProductionStageViewModelBase.Detail.cs"
        };

        [Fact]
        public void Only_the_money_home_may_declare_the_rounding_mode()
        {
            var root = Root();
            var declaring = Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}"))
                .Where(f => File.ReadAllText(f).Contains("MidpointRounding.AwayFromZero"))
                .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(AllowedModeDeclarations, declaring);
        }

        private static Dictionary<string, int> Measure()
        {
            var root = Root();
            var result = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(root, path).Replace('\\', '/');

                if (rel.StartsWith("Tests/", StringComparison.Ordinal)) continue;
                if (rel.Contains("/obj/", StringComparison.Ordinal)) continue;
                if (!MoneyNamespaces.Any(ns => rel.StartsWith(ns + "/", StringComparison.Ordinal)))
                    continue;

                int count = 0;
                foreach (var raw in File.ReadLines(path))
                {
                    var line = raw.TrimStart();
                    if (line.StartsWith("//", StringComparison.Ordinal)) continue;
                    if (!line.Contains("Math.Round(", StringComparison.Ordinal)) continue;
                    // An explicit mode means the author made a decision; the
                    // ratchet only counts the calls that inherit the default.
                    if (line.Contains("MidpointRounding", StringComparison.Ordinal)) continue;
                    count++;
                }

                if (count > 0) result[rel] = count;
            }

            return result;
        }

        private static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ProGlassAutomation.csproj")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }
}
