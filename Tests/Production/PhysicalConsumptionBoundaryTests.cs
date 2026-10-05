// Physical consumption boundary — FACTS vs OPTIONS.
// Scanner is read-only. Contract is unwired. STATIC source pins.

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Production
{
    public sealed class PhysicalConsumptionBoundaryTests
    {
        [Fact]
        public void Scanner_is_read_only_and_never_wires_used()
        {
            string src = Read("Services/Production/ProductionInventoryConsistencyScanner.cs");
            Assert.Contains("AsNoTracking()", src);
            Assert.DoesNotContain("SaveChanges", src);
            Assert.DoesNotContain("UseStockAsync", src);
            Assert.DoesNotContain("CommitConsumptionAsync", src);
            Assert.DoesNotContain("ExecuteUpdateAsync", src);
            Assert.DoesNotContain("Status = (int)ReservationStatus", src);
            Assert.Contains("ConsistencyKind.Observation", src);
            Assert.Contains("PlanningConsumedNoMatchingHistory", src);
            Assert.Contains("Cannot prove Consumed-without-Used is an error", src);
        }

        [Fact]
        public void Physical_contract_is_not_called_from_consume_paths()
        {
            Assert.DoesNotContain("PhysicalInventoryConsumptionContract",
                Read("Services/Production/MaterialAllocationService.cs"));
            Assert.DoesNotContain("PhysicalInventoryConsumptionContract",
                Read("Services/Production/Cutting/CuttingCompletionService.cs"));
            Assert.DoesNotContain("PhysicalInventoryConsumptionContract",
                Read("Services/SheetStore/SheetStoreRepository.cs"));
            Assert.DoesNotContain("PhysicalInventoryConsumptionContract",
                Read("Services/ProformaInvoice/SheetStockLinkService.cs"));

            string contract = Read("Services/Production/PhysicalInventoryConsumptionContract.cs");
            Assert.Contains("MISSING — WOULD REQUIRE A DECISION", contract);
            Assert.Contains("MissingPhysicalEventId", contract);
        }

        [Fact]
        public void Consumed_releases_planning_hold_and_does_not_change_used()
        {
            string res = Read("Services/Production/MaterialReservationService.cs");
            Assert.Contains(
                "&& (r.Status == (int)ReservationStatus.Reserved\n" +
                "                              || r.Status == (int)ReservationStatus.Allocated))",
                res);

            string consume = Read("Services/Production/MaterialAllocationService.cs");
            Assert.DoesNotContain("UseStockAsync", consume);
            Assert.Contains("Cannot consume {((AllocationStatus)alloc.Status)} allocation.", consume);

            string complete = Read("Services/Production/Cutting/CuttingCompletionService.cs");
            Assert.Contains("does NOT call UseStockAsync", complete);
            Assert.Contains("IsolationLevel.Serializable", complete);
        }

        [Fact]
        public void Physical_used_callers_remain_manual_wastage_agent()
        {
            Assert.Contains("origin: \"Sheet Store (manual)\"",
                Read("ViewModels/SheetStore/SheetStoreViewModel.Part2.cs"));
            Assert.Contains("CommitConsumptionAsync",
                Read("ViewModels/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionViewModel.cs"));
            Assert.Contains("await repo.UseStockAsync(sheetId, qty)",
                Read("Agent/Tools/Write/UpdateSheetStockTool.cs"));
            Assert.DoesNotContain("UseStockAsync",
                Read("Services/Production/ProductionDeliveryGateway.cs"));
        }

        [Fact]
        public void UseStock_cas_and_qty_guard_unchanged()
        {
            string use = Read("Services/SheetStore/SheetStoreRepository.cs");
            Assert.Contains("g.Id == sheetId && g.Stock - g.Used >= quantity", use);
            Assert.Contains("if (quantity <= 0) return;", use);
        }

        [Fact]
        public void History_has_no_reservation_or_allocation_id()
        {
            string hist = Read("Models/SheetStore/SheetStoreHistory/SheetHistory.cs");
            Assert.DoesNotContain("ReservationId", hist);
            Assert.DoesNotContain("AllocationId", hist);
            Assert.Contains("DocumentReference", hist);
            Assert.Contains("public int Id { get; set; }", hist);
        }

        [Fact]
        public void Delivery_is_not_a_sheet_store_used_event()
        {
            string gw = Read("Services/Production/ProductionDeliveryGateway.cs");
            Assert.DoesNotContain("UseStockAsync", gw);
            Assert.DoesNotContain("GlassSheets", gw);
            Assert.Contains("GetDispatchReadyByRowAsync", gw);
        }

        [Fact]
        public void Immediate_and_consumed_guards_still_present()
        {
            string res = Read("Services/Production/MaterialReservationService.cs");
            Assert.Contains("Cannot release consumed material.", res);
            Assert.Contains("Cannot cancel consumed material.", res);
            Assert.Contains("IsolationLevel.Serializable", res);

            string complete = Read("Services/Production/Cutting/CuttingCompletionService.cs");
            Assert.Contains("reservation.Status != (int)ReservationStatus.Released", complete);
            Assert.Contains("reservation.Status != (int)ReservationStatus.Cancelled", complete);
        }

        [Fact]
        public void Scanner_classifies_retry_as_not_detectable()
        {
            string src = Read("Services/Production/ProductionInventoryConsistencyScanner.cs");
            Assert.Contains("RetryVsNewConsumption", src);
            Assert.Contains("ConsistencyKind.NotDetectable", src);
            Assert.Contains("ActiveHoldExceedsPhysical", src);
            Assert.Contains("AllocConsumedReservationReleased", src);
        }

        [Fact]
        public void Csv_update_all_can_overwrite_used_without_event_id()
        {
            string csv = Read("ViewModels/SheetStore/SheetStoreViewModel.Part1.cs");
            Assert.Contains("existing.Used = row.Used;", csv);
            Assert.Contains("DuplicateAction.UpdateAll", csv);
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
                    "Services", "Production", "MaterialReservationService.cs");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
