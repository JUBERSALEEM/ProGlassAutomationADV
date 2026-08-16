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
    }
}
