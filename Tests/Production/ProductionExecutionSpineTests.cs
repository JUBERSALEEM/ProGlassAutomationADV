// Production execution spine pins. STATIC source. No SDK / no runtime.

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Production
{
    public sealed class ProductionExecutionSpineTests
    {
        [Fact]
        public void SetDoneQty_uses_immediate_and_shared_apply()
        {
            string repo = Read("Services/Production/ProductionRepository.cs");
            Assert.Contains("IsolationLevel.Serializable", repo);
            Assert.Contains("ApplyDoneQtyOnTrackedItem", repo);
            Assert.Contains("int applied = Math.Clamp(requestedQty, 0, Math.Max(0, inflow));", repo);
            Assert.Contains("QC failed — cannot increase completion.", repo);
            Assert.Contains("Rejected work cannot be completed.", repo);
            Assert.Contains("Unchanged (idempotent).", repo);
            Assert.Contains("Already completed (idempotent).", repo);
        }

        [Fact]
        public void Cutting_complete_keeps_min_increment_then_apply()
        {
            string cut = Read("Services/Production/Cutting/CuttingCompletionService.cs");
            Assert.Contains("int newDone = Math.Min(workItem.Qty, workItem.QtyDone + 1);", cut);
            Assert.Contains("ProductionRepository.ApplyDoneQtyOnTrackedItem", cut);
            Assert.DoesNotContain("UseStockAsync(", cut);
            Assert.DoesNotContain("workItem.QtyDone = newDone;", cut);
        }

        [Fact]
        public void Process_execution_complete_does_not_write_workitem_qtydone()
        {
            string src = Read("Services/Production/Process/ProductionProcessExecutionService.cs");
            Assert.Contains("ProcessExecution is SUPPORTING metadata", src);
            Assert.Contains("exec.QtyDone = newDone;", src);
            Assert.DoesNotContain("wi.QtyDone =", src);
            Assert.DoesNotContain("wiNewDone", src);
        }

        [Fact]
        public void Control_complete_is_setdoneqty_only()
        {
            string ctrl = Read("Services/Production/Control/ProductionControlService.cs");
            Assert.Contains("ProductionRepository.SetDoneQtyAsync(workItemId, qty)", ctrl);
            Assert.Contains("wi.Stage == (int)ProductionStage.Cutting", ctrl);
            Assert.Contains("reservations.Any()", ctrl);
        }

        [Fact]
        public void Queue_material_uses_sheets_and_allocation_counts()
        {
            string q = Read("Services/Production/Control/ProductionQueueService.cs");
            Assert.Contains("MaterialReserved = reservations.Where(r => r.JobRef == wi.JobRef).Sum(r => r.Quantity)", q);
            Assert.Contains("AllocationStatus.Allocated", q);
            Assert.Contains("AllocationStatus.Consumed", q);
            Assert.Contains("wi.Stage == (int)ProductionStage.Cutting", q);
            Assert.Contains("reservations.Any(r => r.JobRef == wi.JobRef)", q);
            Assert.DoesNotContain("MaterialConsumed = wi.QtyDone", q);
            Assert.DoesNotContain("MaterialReserved = hasReservation ? wi.Qty", q);
        }

        [Fact]
        public void Control_dgu_tab_includes_pairing_and_doubling()
        {
            string vm = Read("ViewModels/Production/ProductionControlCenterViewModel.cs");
            Assert.Contains("i.Stage == ProductionStage.Pairing", vm);
            Assert.Contains("i.Stage == ProductionStage.DoublingLamination", vm);
            Assert.Contains("filtered.Sum(i => (long)Math.Max(0, i.Qty))", vm);
            Assert.DoesNotContain("2 => \"Pairing\"", vm);
        }

        [Fact]
        public void Compute_unit_flows_still_gates_inflow()
        {
            string repo = Read("Services/Production/ProductionRepository.cs");
            Assert.Contains("public static List<ProductionUnitFlow> ComputeUnitFlows", repo);
            Assert.Contains("if (flowItem != null) inflow = flowItem.Inflow;", repo);
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
