// Source pins for reservation lifecycle vs physical Used.
// STATIC — not executed without SDK. Does not wire Consumed → Used.

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Production
{
    public sealed class ProductionReservationLifecycleTests
    {
        [Fact]
        public void Active_holds_are_reserved_plus_allocated_only()
        {
            string res = Read("Services/Production/MaterialReservationService.cs");
            Assert.Contains(
                "&& (r.Status == (int)ReservationStatus.Reserved\n" +
                "                              || r.Status == (int)ReservationStatus.Allocated))",
                res);
            Assert.DoesNotContain(
                "|| r.Status == (int)ReservationStatus.Consumed)",
                res);

            string stock = Read("Services/Production/Cutting/StockSelectionService.cs");
            Assert.Contains("ReservationStatus.Reserved", stock);
            Assert.Contains("ReservationStatus.Allocated", stock);
        }

        [Fact]
        public void Consumed_cannot_release_cancel_or_allocate()
        {
            string res = Read("Services/Production/MaterialReservationService.cs");
            Assert.Contains("Cannot release consumed material.", res);
            Assert.Contains("Cannot cancel consumed material.", res);

            string alloc = Read("Services/Production/MaterialAllocationService.cs");
            Assert.Contains("cannot allocate.", alloc);
            Assert.Contains("Cannot consume {((AllocationStatus)alloc.Status)} allocation.", alloc);
        }

        [Fact]
        public void ConsumeAsync_does_not_overwrite_released_or_cancelled_reservation()
        {
            string alloc = Read("Services/Production/MaterialAllocationService.cs");
            Assert.Contains("reservation.Status != (int)ReservationStatus.Released", alloc);
            Assert.Contains("reservation.Status != (int)ReservationStatus.Cancelled", alloc);
        }

        [Fact]
        public void Cutting_complete_matches_consume_guards_and_uses_immediate()
        {
            string complete = Read("Services/Production/Cutting/CuttingCompletionService.cs");
            Assert.Contains("IsolationLevel.Serializable", complete);
            Assert.Contains("Cannot consume {((AllocationStatus)allocation.Status)} allocation.", complete);
            Assert.Contains("reservation.Status != (int)ReservationStatus.Released", complete);
            Assert.Contains("reservation.Status != (int)ReservationStatus.Cancelled", complete);
            Assert.Contains("reservation.Status = (int)ReservationStatus.Consumed", complete);
            Assert.DoesNotContain("UseStockAsync", complete);
        }

        [Fact]
        public void Work_item_cancel_skips_consumed_and_serializes()
        {
            string cancel = Read("Services/Production/ProductionCancellationService.cs");
            Assert.Contains("IsolationLevel.Serializable", cancel);
            Assert.Contains("if (res.Status == (int)ReservationStatus.Consumed) continue;", cancel);
            Assert.Contains("a.Status != (int)AllocationStatus.Consumed", cancel);
            Assert.DoesNotContain("UseStockAsync", cancel);
        }

        [Fact]
        public void Retry_identity_includes_opt_session_and_timestamp()
        {
            string cut = Read("Services/Production/Cutting/CuttingWorkflowService.cs");
            Assert.Contains("CUT-{jobRef}-{groupId}-{DateTime.UtcNow:yyyyMMddHHmmss}", cut);
            Assert.Contains("OPT-{cuttingSessionId}", cut);

            string res = Read("Services/Production/MaterialReservationService.cs");
            Assert.Contains(
                "return $\"{jobRef}|{specGroup}|{serial}|{glassSheetId}|{optSessionId}|{sheetNo}\";",
                res);
        }

        [Fact]
        public void Physical_used_callers_are_not_production_consume()
        {
            Assert.Contains("var committed = await _repo.UseStockAsync(",
                Read("Services/ProformaInvoice/SheetStockLinkService.cs"));
            Assert.Contains("var link = await SheetStockLinkService.CommitConsumptionAsync(",
                Read("ViewModels/SheetStore/SheetStoreViewModel.Part2.cs"));
            Assert.Contains("await SheetStockLinkService.CommitConsumptionAsync(",
                Read("ViewModels/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionViewModel.cs"));
            Assert.Contains("await repo.UseStockAsync(sheetId, qty)",
                Read("Agent/Tools/Write/UpdateSheetStockTool.cs"));

            Assert.DoesNotContain("UseStockAsync",
                Read("Services/Production/MaterialReservationService.cs"));
            Assert.DoesNotContain("UseStockAsync",
                Read("Services/Production/MaterialAllocationService.cs"));
        }

        [Fact]
        public void UseStock_cas_rejects_overdraw_and_ignores_non_positive_qty()
        {
            string use = Read("Services/SheetStore/SheetStoreRepository.cs");
            Assert.Contains("BeginTransactionAsync(IsolationLevel.Serializable)", use);
            Assert.Contains("int balance = sheet.Stock - sheet.Used;", use);
            Assert.Contains("if (balance < quantity)", use);
            Assert.Contains("if (quantity <= 0) return null;", use);
            Assert.Contains("Not enough stock. Available:", use);
            Assert.Contains("db.SheetHistories.Add(CreateHistory(", use);
        }

        [Fact]
        public void Reservation_is_a_fixed_hold_not_revalidated_against_later_stock_edits()
        {
            string res = Read("Services/Production/MaterialReservationService.cs");
            Assert.Contains("TryEnsureStockAvailableAsync", res);
            Assert.DoesNotContain("WatchStock", res);
            Assert.DoesNotContain("Revalidate", res);
            Assert.DoesNotContain("ExpiresAt", res);
            Assert.DoesNotContain("TimeToLive", res);
            Assert.DoesNotContain("Reconcile", res);
        }

        [Fact]
        public void No_production_inventory_reconciler()
        {
            string res = Read("Services/Production/MaterialReservationService.cs");
            Assert.DoesNotContain("RepairReservations", res);
            Assert.DoesNotContain("ReservationCleanup", res);
            Assert.DoesNotContain("StartupRepair", res);
        }

        [Fact]
        public void Manual_release_exists_for_stale_reserved_rows()
        {
            string vm = Read("ViewModels/Production/ProductionStageViewModelBase.Phase14.cs");
            Assert.Contains("ReleaseReservationCommand", vm);
            Assert.Contains("r.Status != (int)ReservationStatus.Consumed", vm);
            Assert.Contains(".ReleaseAsync(res.ReservationId", vm);
        }

        [Fact]
        public void Dgu_seventeen_and_allocation_one_remain_distinct_from_reservation_sheets()
        {
            string dist = Read("Services/Production/ProductionDistributionEngine.cs");
            Assert.Contains("int qty = row.Qty <= 0 ? 1 : row.Qty;", dist);

            string cut = Read("Services/Production/Cutting/CuttingWorkflowService.cs");
            Assert.Contains("Math.Ceiling(group.TotalAreaSqm", cut);
            Assert.Contains("quantity: 1", cut);
        }

        [Fact]
        public void Glass_sheet_has_no_reserved_column()
        {
            string sheet = Read("Models/SheetStore/GlassSheet.cs");
            Assert.Contains("public int Balance => Stock - Used;", sheet);
            Assert.DoesNotContain("public int Reserved", sheet);
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
