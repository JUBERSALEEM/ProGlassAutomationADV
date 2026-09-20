// Pins the Production reservation ↔ Sheet Store split discovered in the
// forensic pass. Source-level — no SDK. Does not touch frozen PI / JO /
// DN / TI / Statement / P&L / Dashboard / Sheet Store inventory files.

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Production
{
    public sealed class ProductionReservationForensicTests
    {
        [Fact]
        public void Reservation_write_checks_stock_minus_used_minus_other_holds()
        {
            string src = Read("Services/Production/MaterialReservationService.cs");

            Assert.Contains("g.Stock - g.Used", src);
            Assert.Contains("Not enough stock. Available:", src);
            Assert.Contains("Could not check availability.", src);
            Assert.Contains("GlassSheetId required to reserve against Sheet Store stock.", src);
            Assert.DoesNotContain("UseStockAsync", src);
        }

        [Fact]
        public void Reservation_does_not_write_glass_sheet()
        {
            string src = Read("Services/Production/MaterialReservationService.cs");

            Assert.Contains("production.db", src);
            Assert.DoesNotContain("sheet.Used", src);
            Assert.DoesNotContain("ExecuteUpdateAsync", src);
        }

        [Fact]
        public void Allocation_consume_does_not_increment_sheet_store_used()
        {
            string src = Read("Services/Production/MaterialAllocationService.cs");

            Assert.Contains("alloc.Status = (int)AllocationStatus.Consumed", src);
            Assert.DoesNotContain("UseStockAsync", src);
            Assert.DoesNotContain("CommitConsumptionAsync", src);
        }

        [Fact]
        public void Cutting_completion_consume_does_not_increment_sheet_store_used()
        {
            string src = Read("Services/Production/Cutting/CuttingCompletionService.cs");

            Assert.Contains("allocation.Status = (int)AllocationStatus.Consumed", src);
            Assert.DoesNotContain("UseStockAsync", src);
        }

        [Fact]
        public void Stock_selection_does_not_query_unmapped_balance_or_invent_a_rack()
        {
            string src = Read("Services/Production/Cutting/StockSelectionService.cs");

            Assert.Contains("g.Stock - g.Used > 0", src);
            Assert.DoesNotContain(".Where(g => g.Balance > 0)", src);
            Assert.DoesNotContain("AvailableBalance = 100", src);
            Assert.DoesNotContain("Supplier = \"Fallback Benchmark\"", src);
            Assert.Contains("return new List<StockSelectionResult>();", src);
        }

        [Fact]
        public void Effective_available_is_balance_minus_reservation_sheets_not_placed_pieces()
        {
            string src = Read("Services/Production/Cutting/StockSelectionService.cs");

            Assert.Contains(
                "public int EffectiveAvailable => Math.Max(0, AvailableBalance - ReservedQty);",
                src);
            Assert.DoesNotContain(
                "AvailableBalance - ReservedQty - AllocatedQty",
                src);
        }

        [Fact]
        public void Glass_sheet_still_has_no_reserved_column()
        {
            string src = Read("Models/SheetStore/GlassSheet.cs");

            Assert.Contains("public int Balance => Stock - Used;", src);
            Assert.DoesNotContain("Reserved", src);
            Assert.DoesNotContain("public int Available", src);
        }

        [Fact]
        public void Reservation_id_is_deterministic()
        {
            string src = Read("Services/Production/MaterialReservationService.cs");

            Assert.Contains(
                "return $\"{jobRef}|{specGroup}|{serial}|{glassSheetId}|{optSessionId}|{sheetNo}\";",
                src);
            Assert.Contains("IX_MaterialReservation_ReservationId_Unique",
                Read("Services/Production/ProductionDbContext.cs"));
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
