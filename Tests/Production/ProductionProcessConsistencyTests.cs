// Process consistency pins. Source + in-memory readiness. No SDK required.

using System;
using System.Collections.Generic;
using System.IO;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.Control;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Production.Control;
using Xunit;

namespace ProGlassAutomation.Tests.Production
{
    public sealed class ProductionProcessConsistencyTests
    {
        [Fact]
        public void Workitem_apply_is_the_only_qtydone_assignment_on_workitems()
        {
            string repo = Read("Services/Production/ProductionRepository.cs");
            Assert.Contains("row.QtyDone = applied;", repo);
            Assert.Contains("ApplyDoneQtyOnTrackedItem", repo);

            Assert.DoesNotContain("wi.QtyDone =",
                Read("Services/Production/Process/ProductionProcessExecutionService.cs"));
            Assert.DoesNotContain("QtyDone",
                Read("Services/Production/Process/PairingService.cs").Replace(
                    "never writes WorkItem.QtyDone", ""));
            string pair = Read("Services/Production/Process/PairingService.cs");
            Assert.Contains("never writes WorkItem.QtyDone", pair);
            Assert.DoesNotContain("workItem.QtyDone", pair);

            string asm = Read("Services/Production/Process/DoublingLaminationService.cs");
            Assert.Contains("never writes WorkItem.QtyDone", asm);
            Assert.DoesNotContain("pairingWorkItem.QtyDone =", asm);
        }

        [Fact]
        public void Process_execution_complete_refuses_cancelled_rejected_qcfailed()
        {
            string src = Read("Services/Production/Process/ProductionProcessExecutionService.cs");
            Assert.Contains("Linked work item cancelled", src);
            Assert.Contains("Linked work item rejected", src);
            Assert.Contains("Linked work item QC failed", src);
            Assert.Contains("ProcessExecution is SUPPORTING metadata", src);
        }

        [Fact]
        public void Pairing_and_assembly_are_supporting_and_idempotent()
        {
            string pair = Read("Services/Production/Process/PairingService.cs");
            Assert.Contains("idempotent — no duplicate pairing", pair);
            Assert.Contains("IsolationLevel.Serializable", pair);
            Assert.Contains("QC failed — cannot pair", pair);

            string asm = Read("Services/Production/Process/DoublingLaminationService.cs");
            Assert.Contains("idempotent — no duplicate assembly", asm);
            Assert.Contains("pairing has not passed", asm);
            Assert.Contains("Finished-unit count = pairing/doubling WorkItem.Qty", asm);
            Assert.DoesNotContain("qty = pairings.Count", asm);
        }

        [Fact]
        public void Doubling_readiness_uses_pairing_workitem_not_pairing_table()
        {
            string eng = Read("Services/Production/Control/ProductionReadinessEngine.cs");
            Assert.Contains("ProcessPairing.Status", eng);
            Assert.Contains("pairItem.QtyDone < pairItem.Qty", eng);
            Assert.DoesNotContain("pairingComplete.TryGetValue", eng);

            string q = Read("Services/Production/Control/ProductionQueueService.cs");
            Assert.Contains("pairWi.QtyDone >= pairWi.Qty", q);
        }

        [Fact]
        public void Pairing_table_complete_does_not_unblock_doubling_when_pcs_incomplete()
        {
            var pairing = Make("JO-D", 1, 1, "Unit", ProductionStage.Pairing, WorkItemStatus.Pending, 17, 0);
            var doubling = Make("JO-D", 1, 1, "Unit", ProductionStage.DoublingLamination, WorkItemStatus.Pending, 17, 0);
            var all = new List<SavedProductionWorkItem> { pairing, doubling };
            var fakePairTable = new Dictionary<(int, int), bool> { [(1, 1)] = true };

            var (readiness, reason) = ProductionReadinessEngine.Calculate(
                doubling, all, all,
                new Dictionary<(int, int, string, int), int>(),
                fakePairTable, null, true, true, false, "");

            Assert.Equal(ProductionReadiness.Blocked, readiness);
            Assert.Contains("Pairing", reason);
        }

        [Fact]
        public void Dgu_dispatch_uses_workitem_flows_not_processexecution()
        {
            string gw = Read("Services/Production/ProductionDeliveryGateway.cs");
            Assert.Contains("GetDispatchReadyByRowAsync", gw);
            Assert.DoesNotContain("ProcessExecution", gw);
            Assert.DoesNotContain("UseStockAsync", gw);

            string repo = Read("Services/Production/ProductionRepository.cs");
            Assert.Contains("flow.DispatchReadyQty = doubleDone", repo);
        }

        [Fact]
        public void Qc_and_breakage_do_not_increment_qtydone()
        {
            string qc = Read("Services/Production/ProductionQcService.cs");
            Assert.Contains("workItem.Status = (int)WorkItemStatus.QcFailed", qc);
            Assert.DoesNotContain("workItem.QtyDone =", qc);
            Assert.Contains("Cancelled work cannot be QC'd", qc);

            string br = Read("Services/Production/ProductionBreakageService.cs");
            Assert.Contains("does NOT modify original Qty/QtyDone", br);
            Assert.Contains("Cancelled work cannot record breakage", br);
            Assert.Contains("|RPL:{nextSeq}", br);
        }

        [Fact]
        public void Setdoneqty_and_cutting_concurrency_and_idempotency_remain()
        {
            string repo = Read("Services/Production/ProductionRepository.cs");
            Assert.Contains("IsolationLevel.Serializable", repo);
            Assert.Contains("Unchanged (idempotent).", repo);
            Assert.Contains("QC failed — cannot increase completion.", repo);

            string cut = Read("Services/Production/Cutting/CuttingCompletionService.cs");
            Assert.Contains("ApplyDoneQtyOnTrackedItem", cut);
            Assert.Contains("int newDone = Math.Min(workItem.Qty, workItem.QtyDone + 1);", cut);
            Assert.DoesNotContain("UseStockAsync(", cut);
        }

        [Fact]
        public void Cancel_commits_immediate_transaction()
        {
            string c = Read("Services/Production/ProductionCancellationService.cs");
            Assert.Contains("IsolationLevel.Serializable", c);
            Assert.Contains("await tx.CommitAsync(ct)", c);
        }

        [Fact]
        public void Process_center_does_not_invent_area()
        {
            string vm = Read("ViewModels/Production/ProcessWorkCenterViewModel.cs");
            Assert.DoesNotContain("* 1.575", vm);
        }

        [Fact]
        public void No_production_path_calls_usestock()
        {
            foreach (var rel in new[]
            {
                "Services/Production/ProductionRepository.cs",
                "Services/Production/Process/PairingService.cs",
                "Services/Production/Process/DoublingLaminationService.cs",
                "Services/Production/Process/ProductionProcessExecutionService.cs",
                "Services/Production/ProductionQcService.cs",
                "Services/Production/ProductionBreakageService.cs",
                "Services/Production/ProductionDeliveryGateway.cs"
            })
            {
                Assert.DoesNotContain("UseStockAsync(", Read(rel));
            }
        }

        private static SavedProductionWorkItem Make(
            string job, int spec, int serial, string pos,
            ProductionStage stage, WorkItemStatus status, int qty, int done)
        {
            return new SavedProductionWorkItem
            {
                UniqueKey = $"{job}|{spec}|{serial}|{pos}|{(int)stage}",
                JobRef = job,
                SpecGroupNumber = spec,
                SerialNumber = serial,
                Position = pos,
                Stage = (int)stage,
                Status = (int)status,
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
