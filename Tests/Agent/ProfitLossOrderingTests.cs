// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ProfitLossOrderingTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Two things this file pins.
//
// 1. ROW ORDER. The Profit & Loss report must list each specification in
//    the order the glass is physically assembled:
//
//        DGU      outer · air spacer · inner
//        LAM      outer · interlayer · inner
//        DGU+LAM  outer · air spacer · lam outer · interlayer · lam inner
//
//    The costing code produces every glass pane first and splices the
//    middle layers in afterwards, so the splice indices are where this
//    goes wrong. The DGU+LAM case had an off-by-one: the interlayer index
//    was captured before the spacer was inserted ahead of it, which
//    printed the interlayer one position too early.
//
// 2. MARGIN vs MARK-UP. Step 4 sets a mark-up over cost. Step 9 reports
//    an accounting margin over net revenue. They are different numbers
//    and a discount moves them by different amounts, which is what makes
//    a price cut look cheaper than it is.
//
// The ordering tests model the splice arithmetic rather than driving the
// view model, because RecalculateAsync needs a Sheet Store, a dispatcher
// and a populated Step 5 grid. The arithmetic is the part that broke.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class ProfitLossOrderingTests
    {
        /// <summary>
        /// Mirrors the local AddLayerRow function in ProfitLossStepViewModel:
        /// splice at the recorded index, or append when it was never set.
        /// </summary>
        private static int AddLayer(List<string> rows, string row, int at)
        {
            if (at >= 0 && at <= rows.Count)
            {
                rows.Insert(at, row);
                return at + 1;
            }

            rows.Add(row);
            return at;
        }

        // ══════════════════════════════════════════════════════════════
        //  TWO-PANE BUILDS
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Dgu_lists_outer_then_spacer_then_inner()
        {
            var rows = new List<string>();

            rows.Add("Glass Outer");
            int spacerAt = rows.Count;      // boundary after the outer pane
            rows.Add("Glass Inner");
            AddLayer(rows, "Spacer ASP", spacerAt);

            Assert.Equal(
                new[] { "Glass Outer", "Spacer ASP", "Glass Inner" },
                rows);
        }

        [Fact]
        public void Lam_lists_outer_then_interlayer_then_inner()
        {
            var rows = new List<string>();

            rows.Add("Glass Outer");
            int interlayerAt = rows.Count;
            rows.Add("Glass Inner");
            AddLayer(rows, "Production PVB", interlayerAt);

            Assert.Equal(
                new[] { "Glass Outer", "Production PVB", "Glass Inner" },
                rows);
        }

        // ══════════════════════════════════════════════════════════════
        //  THREE-PANE HYBRID — where the off-by-one lived
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void DguLam_lists_all_five_layers_in_build_order()
        {
            var rows = new List<string>();
            int spacerAt = -1, interlayerAt = -1;

            var panes = new[] { "Glass Outer", "Glass LamOuter", "Glass LamInner" };

            for (int i = 0; i < panes.Length; i++)
            {
                rows.Add(panes[i]);

                if (i == 0) spacerAt = rows.Count;

                // +1 leaves room for the spacer, which is spliced in first
                // and shifts everything after it down by one.
                if (i == 1) interlayerAt = rows.Count + 1;
            }

            AddLayer(rows, "Spacer ASP", spacerAt);
            AddLayer(rows, "Production PVB", interlayerAt);

            Assert.Equal(
                new[]
                {
                    "Glass Outer",
                    "Spacer ASP",
                    "Glass LamOuter",
                    "Production PVB",
                    "Glass LamInner"
                },
                rows);
        }

        [Fact]
        public void DguLam_interlayer_sits_between_the_two_laminated_panes()
        {
            // The property that actually matters, stated directly: the
            // interlayer bonds lam-outer to lam-inner, so it must appear
            // after the first and before the second.
            var rows = new List<string>();
            int spacerAt = -1, interlayerAt = -1;

            var panes = new[] { "Glass Outer", "Glass LamOuter", "Glass LamInner" };
            for (int i = 0; i < panes.Length; i++)
            {
                rows.Add(panes[i]);
                if (i == 0) spacerAt = rows.Count;
                if (i == 1) interlayerAt = rows.Count + 1;
            }

            AddLayer(rows, "Spacer ASP", spacerAt);
            AddLayer(rows, "Production PVB", interlayerAt);

            int lamOuter = rows.IndexOf("Glass LamOuter");
            int interlayer = rows.IndexOf("Production PVB");
            int lamInner = rows.IndexOf("Glass LamInner");

            Assert.True(lamOuter < interlayer);
            Assert.True(interlayer < lamInner);
        }

        [Fact]
        public void DguLam_spacer_sits_between_outer_and_the_laminated_pair()
        {
            var rows = new List<string>();
            int spacerAt = -1, interlayerAt = -1;

            var panes = new[] { "Glass Outer", "Glass LamOuter", "Glass LamInner" };
            for (int i = 0; i < panes.Length; i++)
            {
                rows.Add(panes[i]);
                if (i == 0) spacerAt = rows.Count;
                if (i == 1) interlayerAt = rows.Count + 1;
            }

            AddLayer(rows, "Spacer ASP", spacerAt);
            AddLayer(rows, "Production PVB", interlayerAt);

            Assert.True(rows.IndexOf("Glass Outer") < rows.IndexOf("Spacer ASP"));
            Assert.True(rows.IndexOf("Spacer ASP") < rows.IndexOf("Glass LamOuter"));
        }

        [Fact]
        public void An_unset_index_appends_rather_than_dropping_the_row()
        {
            // A shape the parser does not recognise must still report its
            // costs. Losing the row would understate the job.
            var rows = new List<string> { "Glass Outer" };

            AddLayer(rows, "Spacer ASP", -1);

            Assert.Contains("Spacer ASP", rows);
            Assert.Equal(2, rows.Count);
        }

        // ══════════════════════════════════════════════════════════════
        //  MARGIN vs MARK-UP
        //
        //  Worked example from the business:
        //    6mm HD Grey FT + 12mm ASP + 6mm Clear FT
        //    (48 + 25) / 0.85 + 45 = 130.88 cost basis
        //    x 1.15                = 150.51 quoted at Step 4's 15%
        // ══════════════════════════════════════════════════════════════

        private const decimal CostBasis = 130.88m;

        private static decimal Markup(decimal quoted) =>
            decimal.Round((quoted - CostBasis) / CostBasis * 100m, 2);

        [Fact]
        public void Step4_rate_reproduces_the_business_figure()
        {
            decimal quoted = decimal.Round(CostBasis * 1.15m, 2);

            Assert.Equal(150.51m, quoted);
        }

        [Fact]
        public void Quoting_the_step4_rate_realises_the_intended_markup()
        {
            Assert.Equal(15.00m, Markup(150.51m));
        }

        [Fact]
        public void Discounting_to_140_more_than_halves_the_markup()
        {
            // The question the business asked: quote 140 instead of 150.51,
            // what does the margin become? 6.97%, not 15%.
            Assert.Equal(6.97m, Markup(140m));
        }

        [Fact]
        public void A_seven_percent_discount_costs_more_than_half_the_profit()
        {
            // 150.51 -> 140 is a 7% price cut but a 53% cut in profit per
            // SQM. This is the asymmetry the report has to make visible.
            decimal profitAtList = 150.51m - CostBasis;
            decimal profitAtDiscount = 140m - CostBasis;

            decimal lost = decimal.Round(
                (profitAtList - profitAtDiscount) / profitAtList * 100m, 0);

            Assert.Equal(54m, lost);
        }

        [Fact]
        public void Selling_at_cost_shows_zero_markup_not_a_small_positive()
        {
            Assert.Equal(0.00m, Markup(CostBasis));
        }

        [Fact]
        public void Selling_below_cost_reports_a_negative_markup()
        {
            // A loss must read as a loss rather than wrapping to a small
            // positive or being clamped at zero.
            Assert.True(Markup(120m) < 0m);
        }

        // ══════════════════════════════════════════════════════════════
        //  PER-SPECIFICATION RATE CHECK
        //
        //  Drives SpecCostGroup: type a rate, see the mark-up it earns on
        //  THAT specification. The invoice total cannot answer this because
        //  it mixes every specification together.
        // ══════════════════════════════════════════════════════════════

        private static ProGlassAutomation.ViewModels.ProformaInvoice
            .Step9ProfitLoss.SpecCostGroup Spec(decimal cutSqm = 40m)
        {
            // 130.88 per SQM, the worked DGU example.
            return new ProGlassAutomation.ViewModels.ProformaInvoice
                .Step9ProfitLoss.SpecCostGroup
            {
                SpecNumber = 1,
                SpecName = "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass",
                CutSqm = cutSqm,
                TotalCost = decimal.Round(CostBasis * cutSqm, 2),

                // The rate check measures against the Step 4 quote basis, not
                // the materials cost. Setting it explicitly is the whole point
                // of the fix: without it the percentages were computed against
                // sheets-purchased-at-purchase-price and bore no relation to
                // the quoted rate.
                QuoteBasisPerSqm = CostBasis
            };
        }

        [Fact]
        public void Spec_cost_per_sqm_is_derived_from_area_not_sheet_count()
        {
            Assert.Equal(130.88m, Spec().CostPerSqm);
        }

        [Fact]
        public void Rate_check_reproduces_the_fifteen_percent_rate()
        {
            var spec = Spec();
            spec.QuotedRatePerSqm = 150.51m;

            Assert.Equal(15.00m, spec.QuotedMarkupPercent);
        }

        [Fact]
        public void Rate_check_reproduces_the_ten_percent_rate()
        {
            // The business's own second figure: 143.97 is exactly 10%.
            var spec = Spec();
            spec.QuotedRatePerSqm = 143.97m;

            Assert.Equal(10.00m, spec.QuotedMarkupPercent);
        }

        [Fact]
        public void Rate_check_reports_profit_per_sqm_and_scales_to_the_job()
        {
            var spec = Spec(cutSqm: 40m);
            spec.QuotedRatePerSqm = 143.97m;

            Assert.Equal(13.09m, spec.QuotedProfitPerSqm);
            Assert.Contains("523.60", spec.RateCheckDisplay);
        }

        [Fact]
        public void Rate_below_cost_is_flagged_not_shown_as_a_small_profit()
        {
            var spec = Spec();
            spec.QuotedRatePerSqm = 120m;

            Assert.True(spec.IsQuotedRateBelowCost);
            Assert.True(spec.QuotedProfitPerSqm < 0m);
            Assert.Contains("BELOW COST", spec.RateCheckDisplay);
        }

        [Fact]
        public void Rate_equal_to_cost_is_zero_markup_and_not_flagged()
        {
            var spec = Spec();
            spec.QuotedRatePerSqm = 130.88m;

            Assert.Equal(0m, spec.QuotedMarkupPercent);
            Assert.False(spec.IsQuotedRateBelowCost);
        }

        [Fact]
        public void No_rate_entered_prompts_rather_than_reporting_zero_percent()
        {
            // Showing "0% mark-up" for an empty box would read as a real
            // finding rather than an unanswered question.
            var spec = Spec();

            Assert.False(spec.HasQuotedRate);
            Assert.Contains("Enter a rate", spec.RateCheckDisplay);
        }

        [Fact]
        public void A_spec_with_no_basis_and_no_area_cannot_be_rate_checked()
        {
            var spec = Spec(cutSqm: 0m);
            spec.QuoteBasisPerSqm = 0m;   // Step 4 could not identify the build

            Assert.Equal(0m, spec.CostPerSqm);
            Assert.Contains("Cost not known", spec.RateCheckDisplay);
        }

        // ══════════════════════════════════════════════════════════════
        //  THE BASIS THE PERCENTAGE IS MEASURED AGAINST
        //
        //  The rate box was scoring rates against the Profit & Loss
        //  materials cost — sheets purchased at purchase price — which is
        //  neither the quoted rate's basis nor close to it.
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Quote_basis_wins_over_materials_cost()
        {
            var spec = Spec();
            spec.TotalCost = 2000m;          // materials, deliberately different
            spec.QuoteBasisPerSqm = 103.82m; // Step 4 basis

            Assert.Equal(103.82m, spec.CostPerSqm);
            Assert.True(spec.HasQuoteBasis);
        }

        [Fact]
        public void Materials_cost_is_used_only_when_no_basis_is_known()
        {
            var spec = Spec(cutSqm: 40m);
            spec.TotalCost = 4000m;        // -> 100.00 per SQM
            spec.QuoteBasisPerSqm = 0m;

            Assert.Equal(100.00m, spec.CostPerSqm);
            Assert.False(spec.HasQuoteBasis);
            Assert.Contains("Step 4 basis unavailable", spec.RateCheckDisplay);
        }

        [Fact]
        public void Clear_clear_dgu_reads_ten_percent_at_the_business_rate()
        {
            // 6mm Clear FT + 12mm ASP + 6mm Clear FT
            //   (25 + 25) / 0.85 + 45 = 103.82
            //   x1.15 = 119.39   x1.10 = 114.20
            var spec = Spec(cutSqm: 40m);
            spec.QuoteBasisPerSqm = 103.82m;

            spec.QuotedRatePerSqm = 114.20m;
            Assert.Equal(10.00m, spec.QuotedMarkupPercent);

            spec.QuotedRatePerSqm = 119.39m;
            Assert.Equal(15.00m, spec.QuotedMarkupPercent);
        }

        [Fact]
        public void Negative_rate_entry_is_clamped_to_zero()
        {
            var spec = Spec();
            spec.QuotedRatePerSqm = -50m;

            Assert.Equal(0m, spec.QuotedRatePerSqm);
        }

        [Fact]
        public void Applying_new_cost_rows_does_not_wipe_the_typed_rate()
        {
            // Live recalc used to Clear()+new SpecCostGroup, which destroyed
            // the Check Rate / SQM box. In-place ApplyCalculatedState must
            // keep the operator's typed rate.
            var spec = Spec();
            spec.QuotedRatePerSqm = 150.51m;

            spec.ApplyCalculatedState(
                "6mm Low E + 12mm ASP + 6mm Obscure Clear Ramly",
                4200m,
                32m,
                103.82m,
                new[]
                {
                    new ProGlassAutomation.ViewModels.ProformaInvoice
                        .Step9ProfitLoss.ProfitLossRow
                    {
                        Kind = "Glass",
                        SpecNumber = 1,
                        Description = "Outer pane",
                        Amount = 2100m
                    }
                });

            Assert.Equal(150.51m, spec.QuotedRatePerSqm);
            Assert.Equal("6mm Low E + 12mm ASP + 6mm Obscure Clear Ramly", spec.SpecName);
            Assert.Equal(32m, spec.CutSqm);
            Assert.Equal(103.82m, spec.QuoteBasisPerSqm);
            Assert.Equal(1, spec.Rows.Count);
            Assert.Equal(15.00m, spec.QuotedMarkupPercent);
        }
    }
}
