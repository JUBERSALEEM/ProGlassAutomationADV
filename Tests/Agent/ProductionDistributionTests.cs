// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ProductionDistributionTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// PINS THE PRODUCTION DISTRIBUTION RULES (2026-09-11, owner-confirmed):
//   • SGU rows distribute W1/H1 only; W2/H2 belong to multi-pane builds.
//   • FT / Heat Strengthened / Heat Soaked panes get a Tempering item
//     after Cutting; Annealed panes go Cutting → ready for dispatch.
//   • Step 6 FAN_HOLES/HOLES feed the Holes & Cutouts stage per pane
//     (charged qty rides along); POLISH feeds the Polish stage per pane
//     with the charged LM + sub-type label; MITRING feeds Mitring.
//   • Piece flow: ComputeUnitFlows tracks done pieces per stage — cut
//     pieces walk into edge work, finished edge pieces walk into
//     tempering, ready panes walk into pairing/doubling.
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
            // Piece flow: polish is a per-pane stage item — its LM is the
            // pane perimeter work ((1000+800)/1000 × 2 × 10 pcs), while the
            // charged commercial LM rides along as ChargeMetricQty.
            Assert.Equal(36.0m, polish.TotalLm);
            Assert.Equal(10, polish.Qty);
            Assert.Equal("Glass", polish.Position);
            Assert.Contains("AllSidePolish", polish.PolishSubTypeLabel);
        }

        [Fact]
        public void Charges_fan_out_per_pane_on_multi_pane_builds()
        {
            var snap = Snapshot(
                Group(1, "6mm Clear FT Glass + 12mm ASP + 6mm Clear Annealed Glass",
                    Row(1, "D-01", 1200, 900, 5, w2: 1190, h2: 890)));
            snap.OtherCharges.PredefinedCharges.Add(Charge("FAN_HOLES", 20m, 1));

            // Both panes walk through the holes stage — pieces flow.
            var holes = Stage(Distribute(snap), ProductionStage.HolesCutouts).ToList();
            Assert.Equal(2, holes.Count);
            Assert.Contains(holes, h => h.Position == "Outer" && h.WidthMm == 1200);
            Assert.Contains(holes, h => h.Position == "Inner" && h.WidthMm == 1190);
            Assert.All(holes, h => Assert.Equal(5, h.Qty));
            Assert.All(holes, h => Assert.Equal("Cutting", h.PredecessorStage));
        }

        // ══════════════════════════════════════════════════════════════
        //  PIECE FLOW — quantities walk the chain (owner model 2026-09-11)
        // ══════════════════════════════════════════════════════════════

        private static ProGlassAutomation.Models.Production.SavedProduction.SavedProductionWorkItem FlowItem(
            string key, ProductionStage stage, string position, int qty, int done,
            int w = 1000, int h = 800) => new()
        {
            Id = key.GetHashCode(),
            UniqueKey = key,
            JobRef = "JOB-FLOW",
            SpecGroupNumber = 1,
            SerialNumber = 1,
            GlassRef = "G-01",
            Position = position,
            Stage = (int)stage,
            Qty = qty,
            QtyDone = done,
            WidthMm = w,
            HeightMm = h
        };

        [Fact]
        public void Cut_pieces_flow_to_holes_and_only_done_pieces_move_on()
        {
            // SGU FT: cut 10 of 24 → holes receives 10; holes 5 done →
            // only 5 are eligible for tempering.
            var items = new List<ProGlassAutomation.Models.Production.SavedProduction.SavedProductionWorkItem>
            {
                FlowItem("cut", ProductionStage.Cutting, "Glass", 24, 10),
                FlowItem("holes", ProductionStage.HolesCutouts, "Glass", 24, 5),
                FlowItem("temper", ProductionStage.Tempering, "Glass", 24, 0)
            };

            var flow = ProductionRepository.ComputeUnitFlows(items).Single();

            Assert.Equal((24, 10), flow.Stages[(int)ProductionStage.Cutting]);
            Assert.Equal((10, 5), flow.Stages[(int)ProductionStage.HolesCutouts]);
            Assert.Equal((5, 0), flow.Stages[(int)ProductionStage.Tempering]);
            Assert.False(flow.IsDispatchReady);
        }

        [Fact]
        public void All_pieces_holed_release_the_whole_cut_quantity_to_tempering()
        {
            var items = new List<ProGlassAutomation.Models.Production.SavedProduction.SavedProductionWorkItem>
            {
                FlowItem("cut", ProductionStage.Cutting, "Glass", 24, 10),
                FlowItem("holes", ProductionStage.HolesCutouts, "Glass", 24, 10),
                FlowItem("temper", ProductionStage.Tempering, "Glass", 24, 5)
            };

            var flow = ProductionRepository.ComputeUnitFlows(items).Single();
            Assert.Equal((10, 5), flow.Stages[(int)ProductionStage.Tempering]);
        }

        [Fact]
        public void Multiple_edge_operations_on_one_pane_count_pieces_once()
        {
            // A pane holding FAN HOLES + CUTOUT at the same stage: the
            // stage IN counts the PIECES once (the cut quantity); DONE
            // advances only as each piece completes ALL its operations.
            var items = new List<ProGlassAutomation.Models.Production.SavedProduction.SavedProductionWorkItem>
            {
                FlowItem("cut", ProductionStage.Cutting, "Glass", 30, 30),
                FlowItem("fan", ProductionStage.HolesCutouts, "Glass", 30, 30),
                FlowItem("cutout", ProductionStage.HolesCutouts, "Glass", 30, 20),
                FlowItem("temper", ProductionStage.Tempering, "Glass", 30, 0)
            };

            var flow = ProductionRepository.ComputeUnitFlows(items).Single();
            Assert.Equal((30, 20), flow.Stages[(int)ProductionStage.HolesCutouts]);
            // Only the 20 pieces done for BOTH ops may temper.
            Assert.Equal((20, 0), flow.Stages[(int)ProductionStage.Tempering]);
        }

        [Fact]
        public void Pairing_waits_for_the_slowest_pane()
        {
            // DGU: outer FT tempered 5; inner annealed cut 10 → only 5
            // units may be paired.
            var items = new List<ProGlassAutomation.Models.Production.SavedProduction.SavedProductionWorkItem>
            {
                FlowItem("cut-o", ProductionStage.Cutting, "Outer", 10, 10),
                FlowItem("cut-i", ProductionStage.Cutting, "Inner", 10, 10, w: 990, h: 790),
                FlowItem("temp-o", ProductionStage.Tempering, "Outer", 10, 5),
                FlowItem("pair", ProductionStage.Pairing, "Unit", 10, 2)
            };

            var flow = ProductionRepository.ComputeUnitFlows(items).Single();
            Assert.Equal((5, 2), flow.Stages[(int)ProductionStage.Pairing]);
        }

        [Fact]
        public void Sgu_annealed_chain_is_dispatch_ready_when_fully_cut()
        {
            var items = new List<ProGlassAutomation.Models.Production.SavedProduction.SavedProductionWorkItem>
            {
                FlowItem("cut", ProductionStage.Cutting, "Glass", 12, 12)
            };

            var flow = ProductionRepository.ComputeUnitFlows(items).Single();
            Assert.Equal(12, flow.PaneReadyQty);
            Assert.Equal(12, flow.DispatchReadyQty);
            Assert.True(flow.IsDispatchReady);
        }

        [Fact]
        public void Sgu_dispatches_pieces_as_they_temper_no_pairing_no_doubling()
        {
            // G-101 owner trace: cut 10 of 24 → 10 holed + polished →
            // tempering completes 5 → exactly 5 pieces are READY FOR
            // DISPATCH now (SGU needs no pairing / doubling).
            var items = new List<ProGlassAutomation.Models.Production.SavedProduction.SavedProductionWorkItem>
            {
                FlowItem("cut", ProductionStage.Cutting, "Glass", 24, 10),
                FlowItem("holes", ProductionStage.HolesCutouts, "Glass", 24, 10),
                FlowItem("polish", ProductionStage.Polish, "Glass", 24, 10),
                FlowItem("temper", ProductionStage.Tempering, "Glass", 24, 5)
            };

            var flow = ProductionRepository.ComputeUnitFlows(items).Single();
            Assert.Equal(5, flow.PaneReadyQty);
            Assert.Equal(5, flow.DispatchReadyQty);
            Assert.False(flow.IsDispatchReady); // 19 pieces still in flight
        }

        [Fact]
        public void Dgu_dispatches_only_what_finished_doubling()
        {
            var items = new List<ProGlassAutomation.Models.Production.SavedProduction.SavedProductionWorkItem>
            {
                FlowItem("cut-o", ProductionStage.Cutting, "Outer", 10, 10),
                FlowItem("cut-i", ProductionStage.Cutting, "Inner", 10, 10, w: 990, h: 790),
                FlowItem("temp-o", ProductionStage.Tempering, "Outer", 10, 10),
                FlowItem("pair", ProductionStage.Pairing, "Unit", 10, 8),
                FlowItem("double", ProductionStage.DoublingLamination, "Unit", 10, 3)
            };

            var flow = ProductionRepository.ComputeUnitFlows(items).Single();
            // All panes ready (10) → pairing received 10, paired 8,
            // doubled 3 → exactly 3 pieces dispatch-ready.
            Assert.Equal((10, 8), flow.Stages[(int)ProductionStage.Pairing]);
            Assert.Equal((8, 3), flow.Stages[(int)ProductionStage.DoublingLamination]);
            Assert.Equal(3, flow.DispatchReadyQty);
            Assert.False(flow.IsDispatchReady);
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

        // ══════════════════════════════════════════════════════════════
        //  STEP 4/5 SIZE SEMANTICS (owner rules 2026-09-11)
        //  • W2/H2 blank on a multi-pane build → EQUAL-SIZE unit: the
        //    second pane inherits W1×H1 (never skipped, never warned).
        //  • W2/H2 present → OVERLAP glass: cut exactly W2×H2.
        //  • Pane quantities always read from the spec build: every pane
        //    occurrence cuts the row qty (qty 20 → outer 20 + inner 20).
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Dgu_blank_w2h2_inner_inherits_w1h1_equal_size_unit()
        {
            var plan = ProductionDistributionEngine.Distribute("JOB-EQ", Snapshot(
                Group(1, "6mm Clear FT Glass + 12mm ASP + 6mm Clear Annealed Glass",
                    Row(1, "D-01", 1200, 900, 20)))); // W2/H2 left blank

            var cutting = Stage(plan.Items, ProductionStage.Cutting).ToList();
            Assert.Equal(2, cutting.Count); // both panes are cut
            Assert.Contains(cutting, c => c.Position == "Outer" && c.WidthMm == 1200 && c.HeightMm == 900 && c.Qty == 20);
            Assert.Contains(cutting, c => c.Position == "Inner" && c.WidthMm == 1200 && c.HeightMm == 900 && c.Qty == 20);
            Assert.DoesNotContain(plan.Warnings, w => w.Contains("was skipped"));
        }

        [Fact]
        public void Dgu_with_w2h2_is_overlap_glass_cut_to_exact_size()
        {
            var plan = ProductionDistributionEngine.Distribute("JOB-OL", Snapshot(
                Group(1, "6mm Clear FT Glass + 12mm ASP + 6mm Clear Annealed Glass",
                    Row(1, "D-01", 1500, 1200, 10, w2: 1250, h2: 950))));

            var cutting = Stage(plan.Items, ProductionStage.Cutting).ToList();
            Assert.Contains(cutting, c => c.Position == "Outer" && c.WidthMm == 1500 && c.HeightMm == 1200);
            Assert.Contains(cutting, c => c.Position == "Inner" && c.WidthMm == 1250 && c.HeightMm == 950 && c.Qty == 10);
        }

        [Fact]
        public void Lam_blank_w2h2_both_lites_inherit_w1h1()
        {
            var plan = ProductionDistributionEngine.Distribute("JOB-LAM", Snapshot(
                Group(1, "6mm Clear FT Glass + 1.52mm Clear PVB + 6mm Clear FT Glass",
                    Row(1, "L-01", 1000, 1000, 4))));

            var cutting = Stage(plan.Items, ProductionStage.Cutting).ToList();
            Assert.Equal(2, cutting.Count);
            Assert.All(cutting, c => Assert.Equal(1000, c.WidthMm));
            Assert.All(cutting, c => Assert.Equal(1000, c.HeightMm));
            Assert.All(cutting, c => Assert.Equal(4, c.Qty));
        }

        [Fact]
        public void Incomplete_w2_h2_does_not_invent_charge_sizes()
        {
            var snap = Snapshot(
                Group(1, "6mm Clear FT Glass + 12mm ASP + 6mm Clear Annealed Glass",
                    Row(1, "D-01", 3210, 2250, 17, w2: 0, h2: 1800)));
            snap.OtherCharges.PredefinedCharges.Add(Charge("FAN_HOLES", 17m, 1));

            var plan = ProductionDistributionEngine.Distribute("JOB-INC-CHG", snap);
            var cutting = Stage(plan.Items, ProductionStage.Cutting).ToList();
            var holes = Stage(plan.Items, ProductionStage.HolesCutouts).ToList();

            Assert.Single(cutting);
            Assert.Equal("Outer", cutting[0].Position);
            Assert.Equal(3210, cutting[0].WidthMm);
            Assert.Equal(2250, cutting[0].HeightMm);
            Assert.Equal(17, cutting[0].Qty);

            Assert.Single(holes);
            Assert.Equal("Outer", holes[0].Position);
            Assert.Equal(3210, holes[0].WidthMm);
            Assert.Equal(2250, holes[0].HeightMm);
            Assert.DoesNotContain(holes, h => h.Position == "Inner");
        }

        [Fact]
        public void Asymmetric_dgu_preserves_w1_h1_w2_h2_and_qty()
        {
            var items = Distribute(Snapshot(
                Group(1, "6mm Clear FT Glass + 12mm ASP + 6mm Clear Annealed Glass",
                    Row(1, "D-17", 3210, 2250, 17, w2: 3000, h2: 1800))));

            var cutting = Stage(items, ProductionStage.Cutting).ToList();
            Assert.Contains(cutting, c => c.Position == "Outer"
                && c.WidthMm == 3210 && c.HeightMm == 2250 && c.Qty == 17);
            Assert.Contains(cutting, c => c.Position == "Inner"
                && c.WidthMm == 3000 && c.HeightMm == 1800 && c.Qty == 17);
            Assert.DoesNotContain(cutting, c => c.WidthMm == 2250 && c.HeightMm == 3210);
            Assert.DoesNotContain(cutting, c => c.WidthMm == 1800 && c.HeightMm == 3000);
        }

        [Fact]
        public void Charges_fan_out_to_inherited_pane_sizes_too()
        {
            var snap = Snapshot(
                Group(1, "6mm Clear FT Glass + 12mm ASP + 6mm Clear Annealed Glass",
                    Row(1, "D-01", 1400, 1000, 6))); // blank W2/H2
            snap.OtherCharges.PredefinedCharges.Add(Charge("FAN_HOLES", 12m, 1));

            var holes = Stage(ProductionDistributionEngine.Distribute("JOB-CH", snap).Items,
                ProductionStage.HolesCutouts).ToList();
            Assert.Equal(2, holes.Count);
            Assert.All(holes, h => Assert.Equal(1400, h.WidthMm));
            Assert.All(holes, h => Assert.Equal(1000, h.HeightMm));
        }

        // ══════════════════════════════════════════════════════════════
        //  SCOPE LOCK (2026-09-11) — only the four Step 4 build families
        //  exist. Triple glazing is NOT in the product today: a name with
        //  three glass tokens and no interlayer degrades to the supported
        //  two-pane DGU split (first + last pane), never to invention.
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Out_of_scope_three_token_name_stays_on_the_supported_dgu_split()
        {
            var items = Distribute(Snapshot(
                Group(1,
                    "6mm Clear FT Glass + 12mm ASP + 6mm Clear Annealed Glass + 12mm ASP + 6mm Clear FT Glass",
                    Row(1, "T-01", 900, 900, 4, w2: 890, h2: 890))));

            var cutting = Stage(items, ProductionStage.Cutting).ToList();
            Assert.Equal(2, cutting.Count);
            Assert.Contains(cutting, c => c.Position == "Outer" && c.WidthMm == 900);
            Assert.Contains(cutting, c => c.Position == "Inner" && c.WidthMm == 890);
            Assert.DoesNotContain(cutting, c => c.Position == "Middle");
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
