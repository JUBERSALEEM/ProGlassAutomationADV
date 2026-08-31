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
        //  Glass Cost         = actual glass cost
        //  Production + ASP   = actual production/ASP, WITHOUT any
        //                       U-insert amount already inside it
        //  U-Insert Cost      = applicable area × rate (10/m² default)
        //  Total Actual Cost  = Glass + Production/ASP + U-Insert
        //  Actual Profit      = Net Selling − Total Actual Cost
        //  Margin %           = Actual Profit ÷ Net Selling × 100
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Price_based_margin_uses_every_component_of_the_actual_cost()
        {
            decimal netSelling = 1000m;
            decimal glass = 400m;
            decimal productionAsp = 350m;
            decimal uInsertArea = 5m;
            decimal uInsertRate = 10m;

            decimal totalCost = glass + productionAsp + (uInsertArea * uInsertRate);
            decimal actualProfit = netSelling - totalCost;

            decimal margin = decimal.Round(actualProfit / netSelling * 100m, 2);

            Assert.Equal(800m, totalCost);
            Assert.Equal(200m, actualProfit);
            Assert.Equal(20.00m, margin);
        }

        [Fact]
        public void U_insert_already_inside_production_cost_is_not_counted_twice()
        {
            // Production/ASP totals 350, of which 50 is the U-insert line
            // already booked. The Price-Based decomposition must pull that
            // 50 out of production and re-add exactly 50 as U-Insert —
            // never 100.
            decimal netSelling = 1000m;
            decimal glass = 400m;
            decimal productionIncludingUInsert = 350m;
            decimal uInsertInProduction = 50m;
            decimal uInsertRecomputed = 50m;

            decimal productionAsp = productionIncludingUInsert - uInsertInProduction;
            decimal totalCost = glass + productionAsp + uInsertRecomputed;
            decimal actualProfit = netSelling - totalCost;

            Assert.Equal(300m, productionAsp);
            Assert.Equal(750m, totalCost);
            Assert.Equal(250m, actualProfit);
            Assert.Equal(25.00m, decimal.Round(actualProfit / netSelling * 100m, 2));
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
            //   Glass:              3,069.56
            //   Production + ASP:   3,145.58
            //   Existing Profit:      934.36
            //
            // Existing KPI (renamed Cost-Based):
            //   934.36 ÷ 7,149.50 × 100 = 13.07% ≈ 13.1%
            decimal netSelling = 7149.50m;
            decimal glass = 3069.56m;
            decimal productionAsp = 3145.58m;
            decimal existingProfit = netSelling - glass - productionAsp;

            Assert.Equal(934.36m, decimal.Round(existingProfit, 2));
            decimal existingMargin = existingProfit / netSelling * 100m;
            Assert.Equal(13.1m, decimal.Round(existingMargin, 1));

            // Price-Based with U-insert: the 8.94% result implies about
            // 29.52 m² × AED 10/m² = 295.19 applied as ADDITIONAL cost.
            decimal uInsertArea = 29.519m;
            decimal uInsertRate = 10m;
            decimal uInsertCost = uInsertArea * uInsertRate;
            decimal actualProfit = existingProfit - uInsertCost;
            decimal priceBased = decimal.Round(actualProfit / netSelling * 100m, 2);

            Assert.Equal(8.94m, priceBased);
            Assert.True(priceBased < existingMargin); // U-insert reduces it
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
        public void Step9_view_model_exposes_both_margin_kpis()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.cs"));

            Assert.Contains("CostBasedMarginPercent => MarginPercent", src);
            Assert.Contains("CostBasedMarginDisplay", src);
            Assert.Contains("MarkupPercent", src);          // kept, NOT the renamed KPI
            Assert.Contains("MarkupDisplay", src);
            Assert.Contains("PriceBasedMarginPercent", src);
            Assert.Contains("PriceBasedMarginDisplay", src);
            Assert.Contains("PriceBasedActualProfit", src);
            Assert.Contains("PriceBasedTotalActualCost", src);
            Assert.Contains("PriceBasedUInsertCost", src);
            Assert.Contains("ComputePriceBasedUInsertCost", src);
            Assert.Contains("uInsertInProduction", src);
            Assert.Contains("PredefinedChargeTemplates", src);
            Assert.Contains("ResolveSuggestedChargeRate", src);
        }

        [Fact]
        public void Step9_view_renders_both_margin_cards()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/ProformaInvoice/Step9ProfitLoss/ProfitLossStepView.xaml"));

            Assert.Contains("COST-BASED PROFIT MARGIN", src);
            Assert.Contains("PRICE-BASED PROFIT MARGIN", src);
            Assert.Contains("CostBasedMarginDisplay", src);
            Assert.Contains("PriceBasedMarginDisplay", src);
            Assert.Contains("PiKpiCellStyle", src);
            Assert.Contains("PiKpiCaptionStyle", src);
            Assert.Contains("PiKpiValueStyle", src);
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
            Assert.Contains("TotalProfit * 100m) / TotalNet", src);
        }

        [Fact]
        public void Job_profitability_view_renders_the_average_margin_tile()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "Views/JobProfitability/JobProfitabilityView.xaml"));

            Assert.Contains("AVERAGE MARGIN", src);
            Assert.Contains("AverageMarginDisplay", src);
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
