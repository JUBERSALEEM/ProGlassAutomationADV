// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/DguLamPricingTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins the DGU+LAM hybrid rate to the chain the business supplied:
//
//     48 + 25 + 25                      = 98
//     98 / 0.85 (yield)                 = 115.29
//     + cutting     10 + 10 + 10  (30)  = 145.29
//     + tempering   12 + 10 + 10  (32)  = 177.29
//     + 110 outsourced PVB              = 287.29
//     x 1.20 (20% profit)               = 344.75 per SQM
//
// The intermediate steps are asserted as well as the total. A single
// end-total test can pass while two errors cancel out, and this formula
// was rebuilt once already after the ordering turned out to be wrong.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.DguLamParameters;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class DguLamPricingTests
    {
        /// <summary>
        /// The shipped defaults ARE the worked example, so a fresh view
        /// model must already quote the business's number without anything
        /// being typed in.
        /// </summary>
        private static DguLamParametersViewModel Vm() => new();

        // ══════════════════════════════════════════════════════════════
        //  THE HEADLINE NUMBER
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Default_parameters_quote_the_business_rate()
        {
            Assert.Equal(344.75m, Vm().CalculateRate());
        }

        [Fact]
        public void Default_rate_reports_no_warning()
        {
            Vm().CalculateRate(out string warning);

            Assert.Equal("", warning);
        }

        // ══════════════════════════════════════════════════════════════
        //  EACH LINK IN THE CHAIN
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Three_panes_sum_to_ninety_eight()
        {
            var vm = Vm();

            Assert.Equal(
                98m,
                vm.OuterBasePrice + vm.LamOuterBasePrice + vm.LamInnerBasePrice);
        }

        [Fact]
        public void Cutting_totals_thirty_across_three_panes()
        {
            Assert.Equal(30m, Vm().TotalCuttingCost);
        }

        [Fact]
        public void Tempering_totals_thirty_two_across_three_panes()
        {
            // 12 + 10 + 10. The outer pane is dearer, so this total is NOT
            // three times any single rate.
            Assert.Equal(32m, Vm().TotalTemperingCost);
        }

        [Fact]
        public void Outer_pane_tempers_dearer_than_the_laminated_panes()
        {
            var vm = Vm();

            Assert.True(vm.OuterTemperingCost > vm.LamOuterTemperingCost);
        }

        // ══════════════════════════════════════════════════════════════
        //  THINGS THAT MUST NOT BE IN THE PRICE
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Spacer_is_not_charged_by_default()
        {
            // The supplied chain runs tempering straight into the
            // interlayer. Charging 45 here would quote 398.75.
            Assert.Equal(0m, Vm().SpacerPrice);
        }

        [Fact]
        public void Pricing_a_spacer_raises_the_rate_by_that_amount_plus_margin()
        {
            var vm = Vm();
            vm.SpacerPrice = 45m;

            // 45 enters the subtotal, so it carries the 20% margin too.
            Assert.Equal(344.75m + (45m * 1.20m), vm.CalculateRate());
        }

        // ══════════════════════════════════════════════════════════════
        //  THE INTERLAYER IS MARKED UP, NOT PASSED THROUGH
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Interlayer_is_inside_the_margin()
        {
            var vm = Vm();

            decimal withPvb = vm.CalculateRate();

            vm.InterlayerSellPrice = 0m;
            decimal withoutPvb = vm.CalculateRate(out _);

            // If the interlayer were added after the margin the difference
            // would be exactly 110. It is inside, so it is 110 x 1.20.
            Assert.Equal(132m, withPvb - withoutPvb);
        }

        [Fact]
        public void Missing_interlayer_price_is_reported_not_silently_dropped()
        {
            var vm = Vm();
            vm.InterlayerSellPrice = 0m;

            vm.CalculateRate(out string warning);

            Assert.Contains("interlayer", warning);
        }

        [Fact]
        public void Both_buy_and_sell_prices_are_held()
        {
            var vm = Vm();

            // Costing needs buy, quoting needs sell. Losing either makes
            // the other side of the job wrong.
            Assert.Equal(100m, vm.InterlayerBuyPrice);
            Assert.Equal(110m, vm.InterlayerSellPrice);
        }

        [Fact]
        public void Switching_to_eva_loads_its_own_price_pair()
        {
            var vm = Vm();
            vm.InterlayerType = "EVA";

            Assert.Equal(75m, vm.InterlayerBuyPrice);
            Assert.Equal(95m, vm.InterlayerSellPrice);
        }

        [Fact]
        public void Sgp_defaults_to_zero_rather_than_an_invented_price()
        {
            // No price was ever supplied for SGP. Guessing one would put a
            // fabricated figure on a customer quote, so it must come
            // through as zero and trip the warning instead.
            var vm = Vm();
            vm.InterlayerType = "SGP";

            Assert.Equal(0m, vm.InterlayerSellPrice);

            vm.CalculateRate(out string warning);
            Assert.Contains("SGP", warning);
        }

        // ══════════════════════════════════════════════════════════════
        //  YIELD
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Yield_divides_glass_only_not_the_interlayer()
        {
            var vm = Vm();
            vm.YieldFactor = 1m;

            // Only the glass term changes: 98 - 115.294... = -17.294...,
            // then x1.20. If yield also touched the interlayer or the
            // processing the delta would be far larger.
            decimal expected = (98m + 30m + 32m + 110m) * 1.20m;

            Assert.Equal(decimal.Round(expected, 2), vm.CalculateRate());
        }

        [Fact]
        public void Zero_yield_is_survived_and_reported()
        {
            var vm = Vm();
            vm.YieldFactor = 0m;

            decimal rate = vm.CalculateRate(out string warning);

            Assert.Contains("Yield", warning);
            Assert.True(rate > 0m);   // fell back to 1.0 rather than dividing by zero
        }

        // ══════════════════════════════════════════════════════════════
        //  SPEC NAME — Step 9 parses this string, so its shape is load-bearing
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Spec_name_lists_all_five_layers_in_build_order()
        {
            Assert.Equal(
                "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass " +
                "+ 1.52mm Clear PVB + 6mm Clear FT Glass",
                Vm().GeneratedSpecName);
        }

        [Fact]
        public void Spec_name_names_three_panes_of_glass()
        {
            // Step 9 counts "Glass" segments to decide how many sheets to
            // cost. Two would silently drop a pane.
            string spec = Vm().GeneratedSpecName;

            Assert.Equal(3, spec.Split("Glass").Length - 1);
        }

        // ══════════════════════════════════════════════════════════════
        //  THREE PANES MEANS THREE OF EVERYTHING
        //
        //  The business asked for this explicitly. A hybrid priced with
        //  two cutting or two tempering charges undercharges every job.
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Cutting_total_is_the_sum_of_three_distinct_panes()
        {
            var vm = Vm();
            vm.OuterCuttingCost = 1m;
            vm.LamOuterCuttingCost = 2m;
            vm.LamInnerCuttingCost = 4m;

            // Distinct powers of two: any dropped or double-counted pane
            // produces a different total.
            Assert.Equal(7m, vm.TotalCuttingCost);
        }

        [Fact]
        public void Tempering_total_is_the_sum_of_three_distinct_panes()
        {
            var vm = Vm();
            vm.OuterTemperingCost = 1m;
            vm.LamOuterTemperingCost = 2m;
            vm.LamInnerTemperingCost = 4m;

            Assert.Equal(7m, vm.TotalTemperingCost);
        }

        [Fact]
        public void Every_pane_charge_moves_the_rate()
        {
            // Guards against a field existing in the UI but never reaching
            // the formula — the pane would look priced and be free.
            decimal baseline = Vm().CalculateRate();

            var a = Vm(); a.OuterCuttingCost += 1m;
            var b = Vm(); b.LamOuterCuttingCost += 1m;
            var c = Vm(); c.LamInnerCuttingCost += 1m;
            var d = Vm(); d.OuterTemperingCost += 1m;
            var e = Vm(); e.LamOuterTemperingCost += 1m;
            var f = Vm(); f.LamInnerTemperingCost += 1m;

            foreach (var vm in new[] { a, b, c, d, e, f })
                Assert.True(vm.CalculateRate() > baseline);
        }

        [Fact]
        public void Rate_reflects_all_six_processing_charges_at_once()
        {
            var vm = Vm();

            // Zero every pane charge: the rate must fall by the full 62
            // (30 cutting + 32 tempering) carried through the margin.
            vm.OuterCuttingCost = 0m;
            vm.LamOuterCuttingCost = 0m;
            vm.LamInnerCuttingCost = 0m;
            vm.OuterTemperingCost = 0m;
            vm.LamOuterTemperingCost = 0m;
            vm.LamInnerTemperingCost = 0m;

            Assert.Equal(344.75m - (62m * 1.20m), vm.CalculateRate());
        }

        // ══════════════════════════════════════════════════════════════
        //  FABRICATION EXTRAS
        //
        //  The hybrid is a DGU and a laminate at once, so it must offer the
        //  add-ons of both. These tests pin WHERE each one lands in the
        //  spec string, because Step 9 parses that string to cost the job:
        //  an extra dropped into the wrong segment can hide a pane.
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void No_extras_selected_leaves_the_spec_name_clean()
        {
            // "None" must contribute nothing at all — not " With None".
            string spec = Vm().GeneratedSpecName;

            Assert.DoesNotContain("None", spec);
            Assert.DoesNotContain("With", spec);
        }

        [Fact]
        public void Edge_polish_is_appended_as_a_with_suffix()
        {
            var vm = Vm();
            vm.EdgePolish = "Both Height Side Polish";

            Assert.EndsWith(" With Both Height Side Polish", vm.GeneratedSpecName);
        }

        [Fact]
        public void Spacer_extra_attaches_to_the_spacer_segment()
        {
            var vm = Vm();
            vm.SpacerExtra = "With U-Insert";

            Assert.Contains("12mm ASP With U-Insert +", vm.GeneratedSpecName);
        }

        [Fact]
        public void Panel_extra_attaches_to_the_last_pane()
        {
            var vm = Vm();
            vm.PanelExtra = "With Overlap (LM) and Silicon Bed (LM) As Per DWG";

            Assert.EndsWith(
                "6mm Clear FT Glass With Overlap (LM) and Silicon Bed (LM) As Per DWG",
                vm.GeneratedSpecName);
        }

        [Fact]
        public void An_extra_and_a_polish_can_both_apply()
        {
            // The business asked for exactly this combination, e.g.
            // "with u insert and both height side polish".
            var vm = Vm();
            vm.SpacerExtra = "With U-Insert";
            vm.EdgePolish = "Both Height Side Polish";

            string spec = vm.GeneratedSpecName;

            Assert.Contains("ASP With U-Insert", spec);
            Assert.EndsWith(" With Both Height Side Polish", spec);
        }

        [Fact]
        public void Extras_never_hide_a_pane_from_the_costing_parser()
        {
            // Whatever extras are chosen, Step 9 must still find three
            // panes. This is the failure that would silently undercost a
            // job, so it is checked against every extra at once.
            var vm = Vm();
            vm.SpacerExtra = "With U-Insert As Per DWG";
            vm.PanelExtra = "With Cutout And Holes As Per DWG";
            vm.EdgePolish = "All Side Polish As Per DWG";

            string spec = vm.GeneratedSpecName;

            Assert.Equal(3, spec.Split("Glass").Length - 1);
            Assert.Contains("ASP", spec);
            Assert.Contains("PVB", spec);
        }

        [Fact]
        public void Hybrid_offers_the_same_extras_as_the_dgu_and_lam_panels()
        {
            // Identical lists on purpose: the same physical product must
            // not get two different names depending on which screen built
            // it.
            var vm = Vm();

            Assert.Contains("With U-Insert", vm.SpacerExtraOptions);
            Assert.Contains("With Overlap (LM) and Silicon Bed (LM)", vm.PanelExtraOptions);
            Assert.Contains("With Overlap (SQM)", vm.PanelExtraOptions);
            Assert.Contains("With Overlap (SQM) and Silicon Bed (SQM)", vm.PanelExtraOptions);
            Assert.Contains("Both Height Side Polish", vm.EdgePolishOptions);
            Assert.Contains("All Side Polish", vm.EdgePolishOptions);

            // "None" must be first so the default selection is no extra.
            Assert.Equal("None", vm.SpacerExtraOptions[0]);
            Assert.Equal("None", vm.PanelExtraOptions[0]);
            Assert.Equal("None", vm.EdgePolishOptions[0]);
        }

        [Fact]
        public void Extras_do_not_change_the_price()
        {
            // They describe fabrication, not cost. If one ever should carry
            // a charge it must be added deliberately, not inherited.
            decimal before = Vm().CalculateRate();

            var vm = Vm();
            vm.SpacerExtra = "With U-Insert";
            vm.PanelExtra = "With Overlap (LM) and Silicon Bed (LM)";
            vm.EdgePolish = "All Side Polish";

            Assert.Equal(before, vm.CalculateRate());
        }

        [Fact]
        public void Spec_name_follows_the_selected_interlayer()
        {
            var vm = Vm();
            vm.InterlayerType = "EVA";

            Assert.Contains("EVA", vm.GeneratedSpecName);
            Assert.DoesNotContain("PVB", vm.GeneratedSpecName);
        }
    }
}
