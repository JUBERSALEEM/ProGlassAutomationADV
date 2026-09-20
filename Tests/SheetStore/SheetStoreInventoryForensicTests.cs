// Pins the Sheet Store inventory model discovered in the forensic pass.
// Source-level — no SDK. Does not touch frozen PI / JO / DN / TI /
// Statement / P&L / Dashboard modules.

using System;
using System.IO;
using Xunit;
using ProGlassAutomation.Models.SheetStore;

namespace ProGlassAutomation.Tests.SheetStore
{
    public sealed class SheetStoreInventoryForensicTests
    {
        [Fact]
        public void Balance_is_stock_minus_used_and_is_not_stored()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Models", "SheetStore", "GlassSheet.cs"));

            Assert.Contains("[NotMapped]", src);
            Assert.Contains("public int Balance => Stock - Used;", src);
            Assert.DoesNotContain("public int Available", src);
            Assert.DoesNotContain("Reserved", src);
        }

        [Fact]
        public void Identity_is_category_thickness_color_size_supplier()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "Database", "AppDbContext.cs"));

            Assert.Contains("HasIndex(g => new { g.Category, g.ThicknessMm, g.Color, g.WidthMm, g.HeightMm, g.Supplier })", src);
            Assert.Contains("HasDatabaseName(\"IX_GlassSheet_Unique\")", src);
        }

        [Fact]
        public void Use_stock_guards_balance_in_the_update_where()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "SheetStore", "SheetStoreRepository.cs"));

            Assert.Contains(
                "g.Id == sheetId && g.Stock - g.Used >= quantity", src);
            Assert.Contains("g.Used + quantity", src);
            Assert.DoesNotContain("g.Used = quantity", src);
        }

        [Fact]
        public void Buy_stock_increments_in_sql()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "SheetStore", "SheetStoreRepository.cs"));

            Assert.Contains("g.Stock + quantity", src);
        }

        [Fact]
        public void Csv_import_skips_invalid_quantities_instead_of_clamping()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "SheetStore", "SheetStoreCsv",
                "SheetStoreCsvService.cs"));

            Assert.Contains(
                "record.Stock < 0 || record.Used < 0 || record.Used > record.Stock",
                src);
            Assert.DoesNotContain("record.Stock = 0", src);
            Assert.DoesNotContain("record.Used = 0", src);
            Assert.DoesNotContain("record.Used = record.Stock", src);
        }

        [Fact]
        public void Search_does_not_order_by_unmapped_value()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "SheetStore", "SheetStoreRepository.cs"));

            Assert.DoesNotContain("OrderBy(g => g.TotalPurchaseValue)", src);
            Assert.DoesNotContain("OrderByDescending(g => g.TotalPurchaseValue)", src);
        }

        [Fact]
        public void Filter_fallback_reapplies_category_filters()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "ViewModels", "SheetStore", "SheetStoreViewModel.Part1.cs"));

            Assert.Contains(
                "filtered = filtered.Where(g => g.Category == SelectedCategory)",
                src);
            Assert.Contains(
                "Could not load sheet store.", src);
        }

        [Fact]
        public void Low_stock_is_balance_at_or_below_five()
        {
            Assert.Equal(5, GlassSheetLowStock.Threshold);
            Assert.True(GlassSheetLowStock.IsLow(0));
            Assert.True(GlassSheetLowStock.IsLow(5));
            Assert.False(GlassSheetLowStock.IsLow(6));
        }

        [Fact]
        public void Dashboard_metrics_still_use_stock_minus_used_sell_value()
        {
            // Contract with frozen Dashboard d0de426 — document, do not edit.
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "Dashboard", "Core",
                "DashboardMetricsService.cs"));

            Assert.Contains("public int Balance => Stock - Used;", src);
            Assert.Contains("BalanceValue => SqmPerSheet * Balance * SellPricePerSqm", src);
            Assert.Contains("GlassSheetLowStock.Threshold", src);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Models", "SheetStore", "GlassSheet.cs");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
