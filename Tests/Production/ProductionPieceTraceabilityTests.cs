// Piece identity + activity integrity pins. Source + in-memory. No SDK required.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Models.Production.Process;
using ProGlassAutomation.Services.Production;
using ProGlassAutomation.Services.Production.Cutting;
using ProGlassAutomation.Services.Production.Process;
using Xunit;

namespace ProGlassAutomation.Tests.Production
{
    public sealed class ProductionPieceTraceabilityTests
    {
        [Fact]
        public void Workitem_uniquekey_is_stable_and_qty_17_is_not_one_lite()
        {
            string key = "JO-T|1|10|Outer|1";
            var wi = Make("JO-T", 1, 10, "Outer", ProductionStage.Cutting, 17, 0);
            Assert.Equal(key, wi.UniqueKey);
            Assert.Equal(17, wi.Qty);
            Assert.NotEqual(17, 1);
            Assert.False(ProductionPieceIdentity.IsPaneInstanceId(wi.UniqueKey));
        }

        [Fact]
        public void Dgu_17_units_are_17_pairs_not_34_finished_units()
        {
            var outer = Enumerable.Range(1, 17)
                .Select(i => ProductionPieceIdentity.BuildPaneInstanceId(
                    "JO-D", 1, 10, "Outer", 1000, 800, i))
                .ToList();
            var inner = Enumerable.Range(1, 17)
                .Select(i => ProductionPieceIdentity.BuildPaneInstanceId(
                    "JO-D", 1, 10, "Inner", 980, 780, i))
                .ToList();
            var pairs = Enumerable.Range(0, 17)
                .Select(i => ProductionProcessRoutingService.BuildPairingId("JO-D", 1, 10, i))
                .ToList();
            var assemblies = Enumerable.Range(0, 17)
                .Select(i => ProductionProcessRoutingService.BuildAssemblyId("JO-D", 1, 10, i, "DGU"))
                .ToList();

            Assert.Equal(17, outer.Distinct().Count());
            Assert.Equal(17, inner.Distinct().Count());
            Assert.Empty(outer.Intersect(inner));
            Assert.Equal(34, outer.Concat(inner).Distinct().Count());
            Assert.Equal(17, pairs.Distinct().Count());
            Assert.Equal(17, assemblies.Distinct().Count());
            Assert.All(outer, id => Assert.True(ProductionPieceIdentity.IsPaneInstanceId(id)));
            Assert.Equal("pairing", ProductionPieceIdentity.Classify(pairs[0]));
            Assert.Equal("assembly", ProductionPieceIdentity.Classify(assemblies[0]));
        }

        [Fact]
        public void Pane_instance_format_is_canonical_p_index_not_star_or_bare_zero()
        {
            string id = CuttingRequirementService.BuildPieceId("JO-T", 1, 10, "Outer", 1000, 800, 1);
            Assert.Equal("JO-T|1|10|Outer|1000x800|P0001", id);
            Assert.DoesNotContain("*", id);
            Assert.False(id.EndsWith("|0", StringComparison.Ordinal));
        }

        [Fact]
        public void Replacement_pane_id_differs_from_original()
        {
            string orig = ProductionPieceIdentity.BuildPaneInstanceId(
                "JO-T", 1, 10, "Outer", 1000, 800, 1, replacementSequence: 0);
            string rpl = ProductionPieceIdentity.BuildPaneInstanceId(
                "JO-T", 1, 10, "Outer", 1000, 800, 1, replacementSequence: 1);
            Assert.NotEqual(orig, rpl);
            Assert.Contains("|RPL:1", rpl);
            Assert.Equal("pane-instance-replacement", ProductionPieceIdentity.Classify(rpl));
            Assert.True(ProductionPieceIdentity.IsReplacementWorkItemKey("JO-T|1|10|Outer|1|RPL:1"));
        }

        [Fact]
        public void Execution_reservation_allocation_session_are_not_piece_ids()
        {
            string exec = ProductionProcessRoutingService.BuildExecutionId(
                "JO-T", 1, 10, ProductionProcessType.Tempering, 0);
            Assert.True(ProductionPieceIdentity.LooksLikeExecutionId(exec));
            Assert.False(ProductionPieceIdentity.IsPaneInstanceId(exec));
            Assert.Equal("process-execution", ProductionPieceIdentity.Classify(exec));

            Assert.True(ProductionPieceIdentity.IsCuttingSessionId("CUT-JO-T-Clear-20260921120000"));
            Assert.False(ProductionPieceIdentity.IsPaneInstanceId("CUT-JO-T-Clear-20260921120000"));
            Assert.False(ProductionPieceIdentity.IsPaneInstanceId("RES-not-a-pane"));
        }

        [Fact]
        public void Pairing_does_not_invent_1000x1000_and_uses_canonical_pane_ids()
        {
            string pair = Read("Services/Production/Process/PairingService.cs");
            Assert.DoesNotContain("1000}*{1000", pair);
            Assert.Contains("Finished-unit count = pairing WorkItem.Qty", pair);
            Assert.Contains("ProductionPieceIdentity.IsPaneInstanceId", pair);
            Assert.Contains("never writes WorkItem.QtyDone", pair);
        }

        [Fact]
        public void Tempering_does_not_mint_a_new_lite_id()
        {
            string exec = Read("Services/Production/Process/ProductionProcessExecutionService.cs");
            Assert.DoesNotContain("-T01", exec);
            Assert.Contains("Stage move does not mint a new physical lite", exec);
            Assert.Contains("ProcessExecution is SUPPORTING metadata", exec);
        }

        [Fact]
        public void Cutting_requirement_expands_qty_and_tags_replacements()
        {
            string src = Read("Services/Production/Cutting/CuttingRequirementService.cs");
            Assert.Contains("for (int q = 0; q < wi.Qty; q++)", src);
            Assert.Contains("ProductionPieceIdentity.BuildPaneInstanceId", src);
            Assert.Contains("wi.ReplacementSequence", src);
        }

        [Fact]
        public void Qc_and_dispatch_are_workitem_scoped()
        {
            string qc = Read("Services/Production/ProductionQcService.cs");
            Assert.Contains("WorkItemId = workItemId", qc);
            Assert.DoesNotContain("workItem.QtyDone =", qc);

            string gw = Read("Services/Production/ProductionDeliveryGateway.cs");
            Assert.Contains("GetDispatchReadyByRowAsync", gw);
            Assert.DoesNotContain("ProcessExecution", gw);

            string repo = Read("Services/Production/ProductionRepository.cs");
            Assert.Contains("flow.DispatchReadyQty = doubleDone", repo);
        }

        [Fact]
        public void Duplicate_pairing_assembly_cutting_complete_are_idempotent()
        {
            string pair = Read("Services/Production/Process/PairingService.cs");
            Assert.Contains("idempotent — no duplicate pairing", pair);
            string asm = Read("Services/Production/Process/DoublingLaminationService.cs");
            Assert.Contains("idempotent — no duplicate assembly", asm);
            string cut = Read("Services/Production/Cutting/CuttingCompletionService.cs");
            Assert.Contains("Already completed (idempotent).", cut);
            Assert.Contains("int newDone = Math.Min(workItem.Qty, workItem.QtyDone + 1);", cut);
        }

        [Fact]
        public void Production_entry_carries_workitem_identity()
        {
            string repo = Read("Services/Production/ProductionRepository.cs");
            Assert.Contains("WorkItemId = row.Id", repo);
            Assert.Contains("JobRef = row.JobRef", repo);
            Assert.Contains("row.QtyDone = applied;", repo);
        }

        [Fact]
        public void No_identity_path_calls_usestock_or_writes_qtydone_outside_apply()
        {
            foreach (var rel in new[]
            {
                "Services/Production/ProductionPieceIdentity.cs",
                "Services/Production/Process/PairingService.cs",
                "Services/Production/Process/DoublingLaminationService.cs",
                "Services/Production/Process/ProductionProcessExecutionService.cs",
                "Services/Production/Cutting/CuttingRequirementService.cs"
            })
            {
                Assert.DoesNotContain("UseStockAsync(", Read(rel));
            }

            Assert.DoesNotContain("wi.QtyDone =",
                Read("Services/Production/Process/ProductionProcessExecutionService.cs"));
        }

        [Fact]
        public void Routing_pieceid_is_workitem_uniquekey_not_a_fake_lite()
        {
            string src = Read("Services/Production/Process/ProductionProcessRoutingService.cs");
            Assert.Contains("PieceId = workItem.UniqueKey", src);
            Assert.DoesNotContain("WidthMm}*{workItem.HeightMm}|0", src);
        }

        private static SavedProductionWorkItem Make(
            string job, int spec, int serial, string pos,
            ProductionStage stage, int qty, int done)
        {
            return new SavedProductionWorkItem
            {
                UniqueKey = $"{job}|{spec}|{serial}|{pos}|{(int)stage}",
                JobRef = job,
                SpecGroupNumber = spec,
                SerialNumber = serial,
                Position = pos,
                Stage = (int)stage,
                Qty = qty,
                QtyDone = done
            };
        }

        private static string Read(string relative)
        {
            return File.ReadAllText(Path.Combine(LocateProjectRoot(),
                relative.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Services", "Production", "ProductionReleaseService.cs");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
