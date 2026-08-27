// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/OtherChargeAutomationRulesTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pure rule tests for the Step 6 advanced auto-charge engine.
// These lock the business mappings supplied by the user so Step 4 / Step 5
// extras turn into the correct Step 6 charges without relying on WPF.
// ═══════════════════════════════════════════════════════════════════════

using System.Linq;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.Services.ProformaInvoice;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class OtherChargeAutomationRulesTests
    {
        [Fact]
        public void Sgu_holes_cutout_and_polish_enable_all_matching_rows()
        {
            var rules = OtherChargeAutomationRules.GetChargeSuggestions(
                "SGU",
                "6mm Clear FT Glass With Holes And Cutouts As Per DWG With All Side Polish",
                matchedSameGlass: false);

            Assert.Contains(rules, r => r.TemplateKey == "HOLES");
            Assert.Contains(rules, r => r.TemplateKey == "CUTOUT");
            Assert.Contains(rules, r => r.TemplateKey == "POLISH"
                && r.PolishSubType == PolishSubType.AllSidePolishAsPerDwg);
        }

        [Fact]
        public void Dgu_normal_overlap_implies_overlap_and_silicon_bed_in_lm()
        {
            var rules = OtherChargeAutomationRules.GetChargeSuggestions(
                "DGU",
                "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass With Overlap (LM) and Silicon Bed (LM)",
                matchedSameGlass: false);

            Assert.Contains(rules, r => r.TemplateKey == "OVERLAP" && r.MetricKey == "LM1");
            Assert.Contains(rules, r => r.TemplateKey == "SILICON_BED" && r.MetricKey == "LM1");
        }

        [Fact]
        public void Dgu_u_insert_overlap_combines_all_required_rows()
        {
            var rules = OtherChargeAutomationRules.GetChargeSuggestions(
                "DGU",
                "6mm HD Grey FT Glass + 12mm ASP With U-Insert + 6mm Clear FT Glass With Overlap (LM) and Silicon Bed (LM)",
                matchedSameGlass: false);

            Assert.Contains(rules, r => r.TemplateKey == "U_INSERT" && r.MetricKey == "DGU_CAVITY");
            Assert.Contains(rules, r => r.TemplateKey == "OVERLAP");
            Assert.Contains(rules, r => r.TemplateKey == "SILICON_BED");
        }

        [Fact]
        public void Dgu_special_overlap_uses_sqm_and_leaves_silicon_bed_off_by_default()
        {
            var rules = OtherChargeAutomationRules.GetChargeSuggestions(
                "DGU",
                "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass With Overlap (SQM)",
                matchedSameGlass: false);

            Assert.Contains(rules, r => r.TemplateKey == "OVERLAP_SQM" && r.MetricKey == "SQM1");
            Assert.DoesNotContain(rules, r => r.TemplateKey == "SILICON_BED");
            Assert.DoesNotContain(rules, r => r.TemplateKey == "SILICON_BED_SQM");
        }

        [Fact]
        public void Dgu_special_overlap_can_optionally_add_silicon_bed_in_sqm()
        {
            var rules = OtherChargeAutomationRules.GetChargeSuggestions(
                "DGU",
                "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass With Overlap (SQM) and Silicon Bed (SQM)",
                matchedSameGlass: false);

            Assert.Contains(rules, r => r.TemplateKey == "OVERLAP_SQM" && r.MetricKey == "SQM1");
            Assert.Contains(rules, r => r.TemplateKey == "SILICON_BED_SQM" && r.MetricKey == "SQM1");
        }

        [Fact]
        public void Legacy_dgu_overlap_text_still_maps_to_the_normal_lm_pair()
        {
            var rules = OtherChargeAutomationRules.GetChargeSuggestions(
                "DGU",
                "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass With Overlap",
                matchedSameGlass: false);

            Assert.Contains(rules, r => r.TemplateKey == "OVERLAP" && r.MetricKey == "LM1");
            Assert.Contains(rules, r => r.TemplateKey == "SILICON_BED" && r.MetricKey == "LM1");
        }

        [Fact]
        public void Lam_same_glass_polish_uses_the_double_polish_row()
        {
            var rules = OtherChargeAutomationRules.GetChargeSuggestions(
                "LAM",
                "6mm Clear FT Glass + 1.52mm Clear PVB + 6mm Clear FT Glass With All Side Polish",
                matchedSameGlass: true);

            Assert.Contains(rules, r => r.TemplateKey == "POLISH_MATCHED_LAM"
                && r.PolishSubType == PolishSubType.AllSidePolish);
        }

        [Fact]
        public void Wastage_rules_return_outer_and_inner_for_double_glass()
        {
            var rows = OtherChargeAutomationRules.GetWastageSuggestions(
                3,
                "DGU",
                "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass",
                "HD Grey",
                "Clear");

            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, r => r.SourceSideLabel == "Outer (W1/H1)");
            Assert.Contains(rows, r => r.SourceSideLabel == "Inner (W2/H2)");
        }

        [Fact]
        public void Hybrid_three_pane_counts_are_supported()
        {
            var metric = new SpecGroupMetric { Qty = 4, PaneFactor = 3 };
            var option = new SpecCheckOption { Qty = 4, PaneFactor = 3 };

            Assert.Equal(12, metric.PhysicalPanes);
            Assert.Equal(12, option.PhysicalPanes);
        }
    }
}
