// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ProductionDistributionTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// PINS THE PRODUCTION DISTRIBUTION RULES (2026-09-11, owner-confirmed):
//   • SGU rows distribute W1/H1 only; W2/H2 belong to multi-pane builds.
//   • FT / Heat Strengthened / Heat Soaked panes get a Tempering item
//     after Cutting; Annealed panes go Cutting → ready for dispatch.
//   • Step 6 FAN_HOLES/HOLES feed the Holes & Cutouts queue with the
//     charged qty; POLISH feeds the Polish queue with the charged LM and
//     its sub-type label.
//   • DGU splits into Outer (W1/H1) + Inner (W2/H2) with Pairing +
//     Doubling items; LAM labels the pair Lam Outer/Lam Inner; DGU+LAM
//     cuts THREE panes (lam pair shares W2/H2); triple DGU cuts three
//     panes with the middle at W2/H2.
//   • Work-item identity is deterministic — a re-release produces the
//     SAME UniqueKeys so floor statuses survive re-distribution.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Production;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class ProductionDistributionTests
    {
        // ── builders ────────────────────────────────────────────────────

        private static GlassItemRowSnapshot Row(
            int serial, string glassRef,
            int w1, int h1, int qty,
            int w2 = 0, int h2 = 0) => new()
        {
            SerialNumber = serial,
            GlassRef = glassRef,
            W1 = w1, H1 = h1, W2 = w2, H2 = h2,
            Qty = qty,
            PerimeterFactor = 2
        };

        private static SpecGroupSnapshot Group(
            int number, string specName, params GlassItemRowSnapshot[] rows) => new()
        {
            GroupNumber = number,
            GroupName = $"Spec {number}",
            SelectedSpecName = specName,
            ItemRows = rows.ToList()
        };

        private static ProformaInvoiceSnapshot Snapshot(
            params SpecGroupSnapshot[] groups)
        {
            var snap = new ProformaInvoiceSnapshot
            {
                SpecGroups = groups.ToList()
            };
            snap.Company.ProformaId = "PI-TEST-001";
            snap.Client.CustomerName = "Test Customer";
            snap.Project.ProjectIdentifierName = "Test Project";
            return snap;
        }

        private static PredefinedChargeSnapshot Charge(
            string key, decimal qty, params int[] specs) => new()
        {
            TemplateKey = key,
            IsActive = true,
            MetricQty = qty,
            CheckedSpecGroupNumbers = specs.ToList()
        };

        private static List<ProductionWorkItem> Distribute(ProformaInvoiceSnapshot snap) =>
            ProductionDistributionEngine.Distribute("JOB-2026-001", snap).Items;

        private static IEnumerable<ProductionWorkItem> Stage(
            List<ProductionWorkItem> items, ProductionStage stage) =>
            items.Where(i => i.Stage == stage);

        // ══════════════════════════════════════════════════════════════
        //  PHASE 0 — the grammar captures the treatment token
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Pane_treatment_is_captured_from_the_spec_name()
        {
            var panes = PnLSpecParser.ResolveAllPanes(
                "6mm Clear FT Glass + 12mm ASP + 6mm Clear Annealed Glass");

            Assert.Equal(2, panes.Count);
            Assert.Equal("FT", panes[0].Treatment);
            Assert.Equal("Annealed", panes[1].Treatment);
            Assert.True(panes[0].RequiresTempering);
            Assert.False(panes[1].RequiresTempering);
        }

        [Theory]
        [InlineData("Heat Strengthened", true)]
        [InlineData("Heat Soaked", true)]
        [InlineData("FT", true)]
        [InlineData("Annealed", false)]
        public void Furnace_routing_follows_the_treatment(string treatment, bool needsFurnace)
        {
            var panes = PnLSpecParser.ResolveAllPanes($"6mm Clear {treatment} Glass");
            Assert.Single(panes);
            Assert.Equal(needsFurnace, panes[0].RequiresTempering);
        }

        // ══════════════════════════════════════════════════════════════
        //  SGU ROUTING
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Sgu_annealed_gets_cutting_only_then_dispatch()
        {
            var items = Distribute(Snapshot(
                Group(1, "6mm Clear Annealed Glass",
                    Row(1, "G-01", 1000, 800, 10))));

            Assert.Single(Stage(items, ProductionStage.Cutting));
            Assert.Empty(Stage(items, ProductionStage.Tempering));
            Assert.Empty(Stage(items, ProductionStage.Pairing));
            Assert.Empty(Stage(items, ProductionStage.DoublingLamination));

            var cutting = Stage(items, ProductionStage.Cutting).Single();
            Assert.Equal(1000, cutting.WidthMm);
            Assert.Equal(800, cutting.HeightMm);
            Assert.Equal(10, cutting.Qty);
            Assert.Equal("Annealed", cutting.Treatment);
            Assert.Equal("Glass", cutting.Position);
        }

        [Fact]
        public void Sgu_ft_gets_cutting_then_tempering()
        {
            var items = Distribute(Snapshot(
                Group(1, "6mm Clear FT Glass",
                    Row(1, "G-01", 1000, 800, 10))));

            Assert.Single(Stage(items, ProductionStage.Cutting));
            var tempering = Stage(items, ProductionStage.Tempering).Single();
            Assert.Equal("FT", tempering.Treatment);
            Assert.Equal("Cutting", tempering.PredecessorStage);
        }

        [Fact]
        public void Sgu_ignores_w2_h2_and_warns()
        {
            var plan = ProductionDistributionEngine.Distribute("JOB-X", Snapshot(
                Group(1, "6mm Clear Annealed Glass",
                    Row(1, "G-01", 1000, 800, 4, w2: 500, h2: 400))));

            // Only one cutting item — the stray W2/H2 pair is NOT cut.
            Assert.Single(plan.Items.Where(i => i.Stage == ProductionStage.Cutting));
            Assert.Contains(plan.Warnings, w => w.Contains("W2/H2"));
        }

        // ══════════════════════════════════════════════════════════════
        //  STEP 6 CHARGE ROUTING
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Fan_holes_qty_is_read_from_step6_into_holes_queue()
        {
            var snap = Snapshot(
                Group(1, "6mm Clear FT Glass", Row(1, "G-01", 1000, 800, 12)));
            snap.OtherCharges.PredefinedCharges.Add(Charge("FAN_HOLES", 24m, 1));

            var holes = Stage(Distribute(snap), ProductionStage.HolesCutouts).Single();
            Assert.Equal(24m, holes.ChargeMetricQty);
            Assert.Equal("QTY", holes.ChargeMetricUnit);
            Assert.Equal("FAN_HOLES", holes.ChargeTemplateKey);
            Assert.Equal(1, holes.SpecGroupNumber);
        }

        [Fact]
        public void Polish_lm_and_subtype_are_read_from_step6()
        {
            var snap = Snapshot(
                Group(1, "6mm Clear FT Glass", Row(1, "G-01", 1000, 800, 10)));
            snap.OtherCharges.PredefinedCharges.Add(new PredefinedChargeSnapshot
            {
                TemplateKey = "POLISH",
                IsActive = true,
                MetricQty = 72.5m,
                PolishSubType = "AllSidePolish",
                CheckedSpecGroupNumbers = new List<int> { 1 }
            });

            var polish = Stage(Distribute(snap), ProductionStage.Polish).Single();
            Assert.Equal(72.5m, polish.ChargeMetricQty);
            Assert.Equal(72.5m, polish.TotalLm);
            Assert.Contains("AllSidePolish", polish.PolishSubTypeLabel);
        }

        [Fact]
        public void Inactive_charges_create_no_work()
        {
            var snap = Snapshot(
                Group(1, "6mm Clear FT Glass", Row(1, "G-01", 1000, 800, 10)));
            snap.OtherCharges.PredefinedCharges.Add(new PredefinedChargeSnapshot
            {
                TemplateKey = "FAN_HOLES",
                IsActive = false,
                MetricQty = 24m
            });

            Assert.Empty(Stage(Distribute(snap), ProductionStage.HolesCutouts));
        }

        // ══════════════════════════════════════════════════════════════
        //  MULTI-PANE BUILDS
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Dgu_splits_into_outer_and_inner_panes_with_assembly()
        {
            var snap = Snapshot(
                Group(1, "6mm Clear FT Glass + 12mm ASP + 6mm Clear Annealed Glass",
                    Row(1, "D-01", 1200, 900, 5, w2: 1190, h2: 890)));
            snap.OtherCharges.PredefinedCharges.Add(Charge("ARGON_GAS", 6.3m, 1));

            var items = Distribute(snap);

            var cutting = Stage(items, ProductionStage.Cutting).ToList();
            Assert.Equal(2, cutting.Count);
            Assert.Contains(cutting, c => c.Position == "Outer" && c.WidthMm == 1200);
            Assert.Contains(cutting, c => c.Position == "Inner" && c.WidthMm == 1190);

            // Only the FT outer pane tempers; the annealed inner does not.
            var tempering = Stage(items, ProductionStage.Tempering).ToList();
            Assert.Single(tempering);
            Assert.Equal("Outer", tempering[0].Position);

            Assert.Single(Stage(items, ProductionStage.Pairing));
            var assembly = Stage(items, ProductionStage.DoublingLamination).Single();
            Assert.Contains("DGU ASSEMBLY", assembly.Notes);
            Assert.Contains("SPACER 12MM", assembly.Notes);
            Assert.Contains("ARGON GAS FILL", assembly.Notes);
        }

        [Fact]
        public void Lam_labels_the_pair_and_laminates()
        {
            var items = Distribute(Snapshot(
                Group(1, "6mm Clear FT Glass + 1.52mm Clear PVB + 6mm Clear FT Glass",
                    Row(1, "L-01", 1000, 1000, 3, w2: 1000, h2: 1000))));

            var cutting = Stage(items, ProductionStage.Cutting).ToList();
            Assert.Contains(cutting, c => c.Position == "Lam Outer");
            Assert.Contains(cutting, c => c.Position == "Lam Inner");

            // Both panes FT → both temper.
            Assert.Equal(2, Stage(items, ProductionStage.Tempering).Count());

            var assembly = Stage(items, ProductionStage.DoublingLamination).Single();
            Assert.Contains("LAMINATION", assembly.Notes);
            Assert.Contains("PVB", assembly.Notes);
        }

        [Fact]
        public void DguLam_cuts_three_panes_with_the_lam_pair_at_w2h2()
        {
            var items = Distribute(Snapshot(
                Group(1,
                    "8mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass + 1.52mm Clear PVB + 6mm Clear FT Glass",
                    Row(1, "H-01", 1500, 1200, 2, w2: 1490, h2: 1190))));

            var cutting = Stage(items, ProductionStage.Cutting).ToList();
            Assert.Equal(3, cutting.Count);
            Assert.Equal(1, cutting.Count(c => c.Position == "Outer" && c.WidthMm == 1500));
            Assert.Equal(2, cutting.Count(c => c.WidthMm == 1490)); // lam pair shares W2/H2
            Assert.Equal("DguLam", cutting[0].BuildType);

            var pairing = Stage(items, ProductionStage.Pairing).Single();
            Assert.Contains("3-PANE UNIT", pairing.Notes);
        }

        [Fact]
        public void Triple_glazed_dgu_cuts_three_panes_middle_at_w2h2()
        {
            var items = Distribute(Snapshot(
                Group(1,
                    "6mm Clear FT Glass + 12mm ASP + 6mm Clear Annealed Glass + 12mm ASP + 6mm Clear FT Glass",
                    Row(1, "T-01", 900, 900, 4, w2: 890, h2: 890))));

            var cutting = Stage(items, ProductionStage.Cutting).ToList();
            Assert.Equal(3, cutting.Count);
            Assert.Contains(cutting, c => c.Position == "Outer");
            Assert.Contains(cutting, c => c.Position == "Middle" && c.WidthMm == 890);
            Assert.Contains(cutting, c => c.Position == "Inner" && c.WidthMm == 890);

            // Outer + Inner FT, middle annealed → two furnace items.
            Assert.Equal(2, Stage(items, ProductionStage.Tempering).Count());
        }

        // ══════════════════════════════════════════════════════════════
        //  DETERMINISM / IDEMPOTENCY
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Releasing_twice_produces_identical_unique_keys()
        {
            ProformaInvoiceSnapshot Build() => Snapshot(
                Group(1, "6mm Clear FT Glass",
                    Row(1, "G-01", 1000, 800, 10),
                    Row(2, "G-02", 1200, 600, 2)),
                Group(2, "6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass",
                    Row(1, "D-01", 1200, 900, 5, w2: 1190, h2: 890)));

            var first = ProductionDistributionEngine.Distribute("JOB-1", Build());
            var second = ProductionDistributionEngine.Distribute("JOB-1", Build());

            Assert.Equal(
                first.Items.Select(i => i.UniqueKey).OrderBy(k => k),
                second.Items.Select(i => i.UniqueKey).OrderBy(k => k));

            // Keys are unique within a release.
            Assert.Equal(first.Items.Count, first.Items.Select(i => i.UniqueKey).Distinct().Count());
        }

        [Fact]
        public void Empty_invoice_reports_an_honest_warning()
        {
            var plan = ProductionDistributionEngine.Distribute("JOB-X", new ProformaInvoiceSnapshot());
            Assert.Empty(plan.Items);
            Assert.True(plan.HasWarnings);
        }
    }
}
