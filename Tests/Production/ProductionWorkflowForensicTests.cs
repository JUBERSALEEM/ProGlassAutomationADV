// Production workflow forensic pins. STATIC source. Does not invent stages.

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Production
{
    public sealed class ProductionWorkflowForensicTests
    {
        [Fact]
        public void Eight_canonical_stages_exist_and_dfu_does_not()
        {
            string models = Read("Models/Production/ProductionModels.cs");
            Assert.Contains("Cutting = 1", models);
            Assert.Contains("HolesCutouts = 2", models);
            Assert.Contains("Polish = 3", models);
            Assert.Contains("EdgeDeletion = 4", models);
            Assert.Contains("Mitring = 5", models);
            Assert.Contains("Tempering = 6", models);
            Assert.Contains("Pairing = 7", models);
            Assert.Contains("DoublingLamination = 8", models);
            Assert.DoesNotContain("Dfu", models);
            Assert.DoesNotContain("DFU", models);
        }

        [Fact]
        public void Unique_key_is_job_spec_serial_position_stage()
        {
            string eng = Read("Services/Production/ProductionDistributionEngine.cs");
            Assert.Contains(
                "string key = $\"{jobRef}|{specNo}|{serial}|{position}|{(int)stage}\";",
                eng);
        }

        [Fact]
        public void Release_uses_embedded_jo_snapshot_not_live_pi()
        {
            string rel = Read("Services/Production/ProductionReleaseService.cs");
            Assert.Contains("LoadJobOrderJsonAsync", rel);
            Assert.Contains("ProductionSnapshotIntegrity.ReadJobOrderJson", rel);
            Assert.DoesNotContain("LoadSnapshotAsync", rel);
            Assert.Contains("Released or In Production", rel);
        }

        [Fact]
        public void Floor_done_qty_is_clamped_to_inflow_and_non_negative_remaining()
        {
            string repo = Read("Services/Production/ProductionRepository.cs");
            Assert.Contains("int applied = Math.Clamp(requestedQty, 0, Math.Max(0, inflow));", repo);
            Assert.Contains("public int Remaining => Math.Max(0, Qty - QtyDone);",
                Read("Models/Production/Control/ProductionControlModels.cs"));
        }

        [Fact]
        public void Cutting_complete_is_idempotent_and_does_not_use_stock()
        {
            string cut = Read("Services/Production/Cutting/CuttingCompletionService.cs");
            Assert.Contains("Already completed (idempotent)", cut);
            Assert.Contains("int newDone = Math.Min(workItem.Qty, workItem.QtyDone + 1);", cut);
            Assert.DoesNotContain("UseStockAsync", cut);
        }

        [Fact]
        public void Control_start_refuses_completed_cancelled_hold_and_not_ready()
        {
            string ctrl = Read("Services/Production/Control/ProductionControlService.cs");
            Assert.Contains("if (wi.IsCancelled) return (false", ctrl);
            Assert.Contains("WorkItem already completed", ctrl);
            Assert.Contains("Not ready:", ctrl);
            Assert.Contains("SetDoneQtyAsync", ctrl);
        }

        [Fact]
        public void Queue_treats_allocated_reservation_as_an_active_hold()
        {
            string q = Read("Services/Production/Control/ProductionQueueService.cs");
            Assert.Contains("r.Status == (int)ReservationStatus.Reserved", q);
            Assert.Contains("r.Status == (int)ReservationStatus.Allocated", q);
            Assert.DoesNotContain("r.Status == 1)", q);
        }

        [Fact]
        public void Control_panel_sums_qty_not_row_count_and_not_fake_area()
        {
            string vm = Read("ViewModels/Production/ProductionControlCenterViewModel.cs");
            Assert.Contains("filtered.Sum(i => (long)Math.Max(0, i.Qty))", vm);
            Assert.DoesNotContain("1.575", vm);
            Assert.Contains("WidthMm * i.HeightMm * i.Qty / 1_000_000.0", vm);
        }

        [Fact]
        public void Foc_persists_foc_entry_type()
        {
            string br = Read("Services/Production/ProductionBreakageService.cs");
            Assert.Contains("row.EntryType = (int)ProductionEntryType.Foc;", br);
            Assert.DoesNotContain("ContinueWith", br);
        }

        [Fact]
        public void Delivery_caps_are_finished_pcs_not_sheet_used()
        {
            string gw = Read("Services/Production/ProductionDeliveryGateway.cs");
            Assert.Contains("GetDispatchReadyByRowAsync", gw);
            Assert.DoesNotContain("UseStockAsync", gw);
        }

        [Fact]
        public void Competing_status_systems_are_documented_in_source()
        {
            Assert.Contains("enum WorkItemStatus", Read("Models/Production/ProductionModels.cs"));
            Assert.Contains("enum ProductionReadiness", Read("Models/Production/Control/ProductionControlModels.cs"));
            Assert.Contains("enum ReservationStatus", Read("Models/Production/SavedProduction/SavedProductionPhase14Entities.cs"));
            Assert.Contains("enum JobOrderStatus", Read("Models/JobOrder/Common/JobOrderStatus/JobOrderStatus.cs"));
        }

        [Fact]
        public void Routing_is_spec_and_charge_driven_not_every_stage()
        {
            string eng = Read("Services/Production/ProductionDistributionEngine.cs");
            Assert.Contains("Tempering only when the pane", eng);
            Assert.Contains("HoleChargeKeys.Contains(key) ? ProductionStage.HolesCutouts", eng);
            Assert.Contains("Sandblasting is charged on this job but has no dedicated production", eng);
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
