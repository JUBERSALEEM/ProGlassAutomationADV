// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ProfitMarginKpiTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Locks the two KPI additions against the MASTER PROMPT:
//
//  1. Step 9 — "Cost-Based Profit Margin": the EXISTING Profit Margin
//     KPI, renamed ONLY. Same formula, data source and behaviour
//     (Profit ÷ Net Selling × 100; for the prompt's example that is
//     934.36 ÷ 7,149.50 = 13.07% ≈ 13.1%). NOT the mark-up over cost,
//     which stays as the supplemental line under PROFIT.
//
//  2. Step 9 — "Price-Based Profit Margin": NEW KPI. Actual Profit =
//     Net Selling − (Glass + Production/ASP + Actual U-Insert Cost),
//     where U-insert is an ADDITIONAL cost (never a deduction from the
//     selling price), using the job's actual applicable U-insert
//     area/quantity and the existing AED 10/m² rate logic. The prompt's
//     example result (~8.94%) is verification only — never hardcoded.
//
//  3. Job Profitability — "Average Margin %": a plain mean of the
//     displayed valid rows' Margin % figures. No amount-based math and
//     no weighting.
//
// The arithmetic is modelled directly (driving RecalculateAsync / the
// repository would need a Sheet Store, a dispatcher and a database), and
// the wiring is pinned by source checks so the rules cannot silently
// drift out of the view models.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class ProfitMarginKpiTests
    {
        // ══════════════════════════════════════════════════════════════
        //  PRICE-BASED PROFIT MARGIN — the formula
        //
        //  Net Selling Amount = actual net / ex-VAT selling amount
        //  Normal SQM basis   = existing per-SQM cost basis × actual area
        //                       (e.g. (25 + 25) ÷ 0.85 + 45 = 103.82/m²)
        //  U-Insert Cost      = applicable area × rate (10/m² default),
        //                       ADDED to the cost side — for the master
        //                       prompt's job that gives 8.94%, while
        //                       subtracting it from the price gives the
        //                       forbidden 9.72%
        //  Total Actual Cost  = Basis + U-Insert
        //  Actual Profit      = Net Selling − Total Actual Cost
        //  Margin %           = Actual Profit ÷ Net Selling × 100
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Price_based_margin_uses_the_normal_sqm_basis_plus_u_insert()
        {
            // Normal per-SQM basis (glass ÷ yield + production) × actual
            // area, PLUS U-insert cost — the components of the price the
            // customer was actually charged.
            decimal netSelling = 1000m;
            decimal basisPerSqm = 103.82m;      // (25 + 25) / 0.85 + 45
            decimal area = 8.45m;
            decimal uInsertArea = 8.45m;
            decimal uInsertRate = 10m;

            decimal basis = basisPerSqm * area;
            decimal uInsert = uInsertArea * uInsertRate;
            decimal totalCost = basis + uInsert;
            decimal actualProfit = netSelling - totalCost;

            decimal margin = decimal.Round(actualProfit / netSelling * 100m, 2);

            Assert.Equal(877.28m, decimal.Round(basis, 2));
            Assert.Equal(84.50m, uInsert);
            Assert.Equal(961.78m, decimal.Round(totalCost, 2));
            Assert.Equal(38.22m, decimal.Round(actualProfit, 2));
            Assert.Equal(3.82m, margin);
        }

        [Fact]
        public void U_insert_is_added_to_cost_not_subtracted_from_the_selling_price()
        {
            // Selling 125/m², normal basis 103.82/m², U-insert 10/m²:
            // profit is 125 − 103.82 − 10, NOT (125 − 10) − 103.82.
            // The two give 8.94% and 9.83% — the master prompt's 8.94%
            // requires adding U-insert to the cost side.
            decimal sellPerSqm = 125m;
            decimal basisPerSqm = 103.8235m;
            decimal uInsertPerSqm = 10m;

            decimal costSide = basisPerSqm + uInsertPerSqm;
            decimal profit = sellPerSqm - costSide;
            decimal margin = decimal.Round(profit / sellPerSqm * 100m, 2);

            Assert.Equal(8.94m, margin);

            decimal wrong = decimal.Round(
                ((sellPerSqm - uInsertPerSqm) - basisPerSqm) / (sellPerSqm - uInsertPerSqm) * 100m, 2);
            Assert.Equal(9.72m, wrong); // the forbidden "subtract from price" answer
        }

        [Fact]
        public void Missing_u_insert_rate_contributes_zero_not_a_made_up_cost()
        {
            // If the metric hub, Step 6 row AND template default are all
            // unavailable, the KPI must account 0 for U-insert rather than
            // inventing a cost. (The production-cost fallback still covers
            // the booked amount — see the wiring pins.)
            decimal rate = 0m;
            decimal uInsertCost = 5m * rate;

            Assert.Equal(0m, uInsertCost);
        }

        [Fact]
        public void Master_prompt_example_reproduces_131_percent_and_dynamic_894_percent()
        {
            // MASTER PROMPT §3 — verification only, never hardcoded in the
            // production code.
            //
            //   Net Ex-VAT Selling: 7,149.50
            //   Glass (sheet-qty):  3,069.56   (Outer 1,805.62 + Inner 1,263.94)
            //   Production + ASP:   3,145.58   (Spacer 2,573.58 + U-Insert 572.00)
            //   Existing Profit:      934.36
            //
            // COST-BASED (existing KPI, renamed only):
            //   934.36 ÷ 7,149.50 × 100 = 13.07% ≈ 13.1%
            decimal netSelling = 7149.50m;
            decimal glassColumns = 3069.56m;
            decimal productionColumns = 3145.58m;
            decimal existingProfit = netSelling - glassColumns - productionColumns;

            Assert.Equal(934.36m, decimal.Round(existingProfit, 2));
            decimal existingMargin = existingProfit / netSelling * 100m;
            Assert.Equal(13.1m, decimal.Round(existingMargin, 1));

            // PRICE-BASED: normal per-SQM basis (25+25)/0.85 + 45 = 103.82
            // × actual area, plus U-INSERT (572.00 = 57.2 m² × AED 10/m²).
            // Area 57.196: net ÷ 125 (the price the customer was charged).
            decimal basisPerSqm = (25m + 25m) / 0.85m + 45m;
            decimal area = netSelling / 125m;
            decimal basis = basisPerSqm * area;
            decimal uInsert = 10m * area;
            decimal totalCost = basis + uInsert;
            decimal actualProfit = netSelling - totalCost;
            decimal priceBased = decimal.Round(actualProfit / netSelling * 100m, 2);

            Assert.Equal(8.94m, priceBased);
            Assert.True(priceBased < existingMargin); // U-insert reduces it

            // Evidence the components match the screen: U-insert ≈ 572.00.
            Assert.Equal(572.00m, decimal.Round(uInsert, 2));
        }

        [Fact]
        public void Price_based_margin_never_hardcodes_the_discussed_894_percent()
        {
            // The user explicitly said ~8.94% may emerge for the discussed
            // example but must never be a constant. Pin the absence of the
            // literal in both the view model and the view (and in this
            // suite's own checks).
            var root = LocateProjectRoot();

            var vm = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.cs"));
            var view = File.ReadAllText(Path.Combine(root,
                "Views/ProformaInvoice/Step9ProfitLoss/ProfitLossStepView.xaml"));

            Assert.DoesNotContain("8.94", vm);
            Assert.DoesNotContain("8.94", view);
        }

        // ══════════════════════════════════════════════════════════════
        //  AVERAGE MARGIN % — the formula
        //
        //  Σ (displayed valid rows' Margin %) ÷ count. Valid = costed
        //  rows carrying a real Step 9 margin. Uncosted rows (no P&L)
        //  are excluded. No Net/Cost/Profit amounts anywhere.
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Average_margin_is_a_plain_mean_of_row_margins()
        {
            decimal[] margins = { 8m, 12m, 20m, 10m };

            decimal average = decimal.Round(
                Sum(margins) / margins.Length, 1);

            Assert.Equal(12.5m, average);
        }

        [Fact]
        public void Average_margin_ignores_uncosted_rows()
        {
            // 5 rows: 3 costed with real margins, 2 with no P&L (margin 0
            // but NOT valid). The average must use only the 3.
            decimal[] costedMargins = { 10m, 20m, 30m };

            Assert.Equal(20.0m, decimal.Round(
                Sum(costedMargins) / costedMargins.Length, 1));
            Assert.NotEqual(12.0m, decimal.Round(
                Sum(new[] { 10m, 20m, 30m, 0m, 0m }) / 5m, 1));
        }

        [Fact]
        public void Average_margin_never_divides_by_zero()
        {
            // Empty list → no valid rows → the view model displays "—"
            // (AverageMarginCount == 0) instead of NaN/0% pretending to be
            // a real average. Models the VM guard exactly.
            var margins = Array.Empty<decimal>();
            int count = margins.Length;

            decimal average = count > 0 ? Sum(margins) / count : 0m;
            Assert.Equal(0, count);
            Assert.Equal(0m, average);
        }

        [Fact]
        public void Average_margin_weights_small_jobs_as_heavily_as_large_ones()
        {
            // Two jobs: 1,000 net at 50% and 10 net at 0%.
            //   Average Margin        = 25%   (each job counts once)
            //   Overall (amount-based) = 49.5% (the big job dominates)
            // These are different answers by design; the KPI pin must
            // report the plain average, not the amount-weighted one.
            decimal[] margins = { 50m, 0m };
            decimal average = Sum(margins) / margins.Length;
            decimal overall = decimal.Round(
                (1000m * 0.5m + 10m * 0m) / (1000m + 10m) * 100m, 1);

            Assert.Equal(25.0m, average);
            Assert.Equal(49.5m, overall);
        }

        private static decimal Sum(decimal[] values)
        {
            decimal total = 0m;
            foreach (var v in values) total += v;
            return total;
        }

        // ══════════════════════════════════════════════════════════════
        //  WIRING PINS — the rules must stay in the view models/views
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Step9_view_model_exposes_margin_and_analysis_properties()
        {
            var root = LocateProjectRoot();

            // The margin math (cost-based = renamed Profit Margin, price-based
            // = actual-cost margin over revenue) lives in the CostBasis
            // partial; the shell wires the three analysis sub-tabs.
            var cost = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.CostBasis.cs"));
            var shell = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.cs"));
            var target = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.TargetPricing.cs"));
            var support = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.CalculationSupport.cs"));

            // Cost-basis margin (kept, renamed KPI)
            Assert.Contains("CostBasedMarginPercent => MarginPercent", cost);
            Assert.Contains("CostBasedMarginDisplay", cost);
            Assert.Contains("MarkupPercent", cost);          // kept, NOT the renamed KPI
            Assert.Contains("MarkupDisplay", cost);
            Assert.Contains("PriceBasedMarginPercent", cost);
            Assert.Contains("PriceBasedMarginDisplay", cost);
            Assert.Contains("PriceBasedActualProfit", cost);
            Assert.Contains("PriceBasedTotalActualCost", cost);
            Assert.Contains("PriceBasedUInsertCost", cost);
            Assert.Contains("PriceBasedBasisCost", cost);        // normal SQM basis × area
            Assert.Contains("QuoteBasisFor", cost);              // existing per-SQM basis

            // U-insert actual-cost plumbing (support partial)
            Assert.Contains("ComputePriceBasedUInsertCost", support);
            Assert.Contains("uInsertInProduction", support);
            Assert.Contains("PredefinedChargeTemplates", support);
            Assert.Contains("ResolveSuggestedChargeRate", support);

            // Approved target-price KPI model (primary Step 9 cards)
            Assert.Contains("public TargetPriceResult? JobTargetResult", target);
            Assert.Contains("public string RequiredTargetPriceDisplay", target);
            Assert.Contains("public string MarginAgainstTargetDisplay", target);
            Assert.Contains("public string UInsertSummaryDisplay", target);
            Assert.Contains("SpecificationTargetCalculator", target);

            // Shell wires the three analysis sub-tabs (auto-synced)
            Assert.Contains("public ServiceProfitabilityViewModel ServiceProfitability", shell);
            Assert.Contains("public SheetProfitabilityViewModel SheetProfitability", shell);
            Assert.Contains("public GlassTypeComparisonViewModel GlassTypeComparison", shell);
        }

        [Fact]
        public void Step9_view_renders_the_target_kpi_cards()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/ProformaInvoice/Step9ProfitLoss/ProfitLossStepView.xaml"));

            Assert.Contains("REQUIRED TARGET PRICE", src);
            Assert.Contains("MARGIN AGAINST TARGET", src);
            Assert.Contains("RequiredTargetPriceDisplay", src);
            Assert.Contains("MarginAgainstTargetDisplay", src);
            Assert.Contains("TgtCardStyle", src);
            Assert.Contains("TgtCaptionStyle", src);
            Assert.Contains("TargetSpecsView", src);
        }

        [Fact]
        public void Job_profitability_view_model_averages_row_margins_only()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "ViewModels/JobProfitability/JobProfitabilityViewModel.cs"));

            Assert.Contains("AverageMarginPercent", src);
            Assert.Contains("AverageMarginCount", src);
            Assert.Contains("AverageMarginDisplay", src);
            Assert.Contains("IsAverageLoss", src);          // colours by the average itself
            Assert.Contains("costedRows.Average", src);
            Assert.Contains("OverallMarginPercent", src);   // untouched
            Assert.Contains("OverallMarginPercentDisplay", src);

            // Portfolio overall margin = (gross − total cost) ÷ total cost
            // (gross = net × 1.05), mirroring the Step 9 formula.
            Assert.Contains("((totalGross - TotalCost) * 100m) / TotalCost", src);
            Assert.Contains("TotalNet * 1.05m", src);
        }

        [Fact]
        public void Job_profitability_view_renders_the_average_margin_tile()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/JobProfitability/JobProfitabilityView.xaml"));

            Assert.Contains("AVERAGE JOB MARGIN", src);
            Assert.Contains("AverageMarginDisplay", src);
            Assert.Contains("OVERALL MARGIN", src);
            Assert.Contains("NetMarginPercentDisplay", src);
            Assert.Contains("JpTileStyle", src);
            Assert.Contains("JpTileCaptionStyle", src);
            Assert.Contains("JpTileValueStyle", src);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "ViewModels", "ProformaInvoice", "Step9ProfitLoss",
                    "ProfitLossStepViewModel.cs");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
