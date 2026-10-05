// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/Step4GlassTypeWiringTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Every glass type in Step 4 needs three things wired up, and DGU+LAM was
// missing all three even after its pricing worked:
//
//   1. a real spec name        — APPLY RATE's CanExecute requires one, so
//                                an empty string left the button dead and
//                                a correctly priced hybrid could never be
//                                applied to a spec group
//   2. a PropertyChanged hook  — without it, editing a field never
//                                recalculated the rate
//   3. a branch in the rate    — without it the quote is zero
//      calculator
//
// These are pure-string / pure-arithmetic checks so they run without a
// UI thread. They exist because "the pricing is correct" and "the feature
// is reachable" are different claims, and only the first had been proven.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.DguLamParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.DguParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.LamParameters;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator.SguParameters;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class Step4GlassTypeWiringTests
    {
        // ══════════════════════════════════════════════════════════════
        //  EVERY TYPE PRODUCES A SPEC NAME
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Sgu_produces_a_spec_name()
        {
            Assert.False(string.IsNullOrWhiteSpace(
                new SguParametersViewModel().GeneratedSpecName));
        }

        [Fact]
        public void Dgu_produces_a_spec_name()
        {
            Assert.False(string.IsNullOrWhiteSpace(
                new DguParametersViewModel().GeneratedSpecName));
        }


        [Fact]
        public void Dgu_overlap_dropdown_exposes_normal_and_special_modes()
        {
            var vm = new DguParametersViewModel();

            Assert.Contains("With Overlap (LM) and Silicon Bed (LM)", vm.PanelExtraOptions);
            Assert.Contains("With Overlap (SQM)", vm.PanelExtraOptions);
            Assert.Contains("With Overlap (SQM) and Silicon Bed (SQM)", vm.PanelExtraOptions);
        }

        [Fact]
        public void Dgu_special_overlap_mode_is_preserved_in_the_spec_name()
        {
            var vm = new DguParametersViewModel
            {
                PanelExtra = "With Overlap (SQM)"
            };

            Assert.EndsWith("6mm Clear FT Glass With Overlap (SQM)", vm.GeneratedSpecName);
        }

        [Fact]
        public void Lam_produces_a_spec_name()
        {
            Assert.False(string.IsNullOrWhiteSpace(
                new LamParametersViewModel().GeneratedSpecName));
        }

        [Fact]
        public void DguLam_produces_a_spec_name()
        {
            // This is the one that returned "" and disabled APPLY RATE.
            Assert.False(string.IsNullOrWhiteSpace(
                new DguLamParametersViewModel().GeneratedSpecName));
        }

        // ══════════════════════════════════════════════════════════════
        //  THE HYBRID'S SPEC NAME IS THE SHAPE STEP 9 PARSES
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void DguLam_spec_name_carries_a_spacer_and_an_interlayer()
        {
            // Step 9 identifies the hybrid by finding BOTH. Losing either
            // marker downgrades it to a two-pane unit and drops a pane of
            // glass from the cost.
            string spec = new DguLamParametersViewModel().GeneratedSpecName;

            Assert.Contains("ASP", spec);
            Assert.Contains("PVB", spec);
        }

        [Fact]
        public void DguLam_spec_name_has_four_layer_separators()
        {
            // Five layers means exactly four '+' joins.
            string spec = new DguLamParametersViewModel().GeneratedSpecName;

            Assert.Equal(4, spec.Split('+').Length - 1);
        }

        // ══════════════════════════════════════════════════════════════
        //  EVERY TYPE QUOTES A NON-ZERO RATE FROM ITS DEFAULTS
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void DguLam_quotes_a_positive_rate_from_defaults()
        {
            // A zero rate also fails APPLY RATE's CanExecute, so this is
            // the second half of the same reachability requirement.
            Assert.True(new DguLamParametersViewModel().CalculateRate() > 0m);
        }

        // ══════════════════════════════════════════════════════════════
        //  PROPERTY CHANGE NOTIFICATION
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Editing_a_hybrid_field_raises_property_changed()
        {
            // Step 4 recalculates on this event. If the view model went
            // quiet the on-screen rate would freeze at its startup value
            // while the inputs changed underneath it.
            var vm = new DguLamParametersViewModel();
            bool raised = false;
            vm.PropertyChanged += (_, _) => raised = true;

            vm.OuterBasePrice = 999m;

            Assert.True(raised);
        }

        [Fact]
        public void Editing_a_field_announces_that_same_field()
        {
            // ViewModelBase fills the property name from [CallerMemberName],
            // so a shared notify helper must forward the caller's name.
            // Announcing only the helper's own name would leave the edited
            // box unbound in practice.
            var vm = new DguLamParametersViewModel();
            string? announced = null;
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(vm.OuterBasePrice))
                    announced = e.PropertyName;
            };

            vm.OuterBasePrice = 123m;

            Assert.Equal(nameof(vm.OuterBasePrice), announced);
        }

        [Fact]
        public void Editing_a_pane_charge_announces_its_running_total()
        {
            // The processing totals are computed properties shown in the
            // UI; they update only if they are announced explicitly.
            var vm = new DguLamParametersViewModel();
            bool totalAnnounced = false;
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(vm.TotalTemperingCost))
                    totalAnnounced = true;
            };

            vm.LamInnerTemperingCost = 42m;

            Assert.True(totalAnnounced);
        }

        [Fact]
        public void Editing_a_field_announces_the_spec_name_and_rate()
        {
            var vm = new DguLamParametersViewModel();
            bool spec = false, rate = false;
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(vm.GeneratedSpecName)) spec = true;
                if (e.PropertyName == nameof(vm.EstimatedRateDisplay)) rate = true;
            };

            vm.LamInnerThickness = "8mm";

            Assert.True(spec);
            Assert.True(rate);
        }
    }
}
