// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Database/StockConcurrencyTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Sheet store / inventory — stock counter atomicity
//
// WHY THIS EXISTS
// ───────────────
// Stock movements used to be a read-modify-write in C#:
//
//     var sheet = await db.GlassSheets.FindAsync(id);   // read
//     if (sheet.Balance < quantity) throw ...;          // check
//     sheet.Used += quantity;                           // write
//
// Two operators touching the same sheet both read the same starting value.
// The second save overwrites the first, so one movement is lost — silently,
// with no exception and nothing in the log. Enough overlap and the balance
// check passes against a stale number, letting Used pass Stock.
//
// These tests fail against the old code. They assert the final counters and
// the matching ledger rows, which are now committed in the same serializable
// transaction rather than as separate stock and history writes.
//
// WHAT IS ACTUALLY BEING ASSERTED
// ───────────────────────────────
// Not "it does not crash". The exact final counter. A lost update does not
// throw — it just leaves a number that is wrong by however many writes
// collided. So the only meaningful assertion is that after N concurrent
// movements the counter has moved by exactly N, and that when the balance
// cannot cover all of them, exactly the affordable number succeed.
//
// Hermetic: AppDataRoot points at a temp folder, the read-only gate is
// forced off, connections are pooled clear and the folder is deleted in
// Dispose. Same shape as InquiryRepositoryCreateResilienceTests.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.Services.SheetStore;
using Xunit;

namespace ProGlassAutomation.Tests.Database
{
    // Shares AppDataRoot.TestFolderOverride with the other hermetic tests —
    // serialized via the shared collection (defined in
    // Tests/Specifications/SpecificationLedgerTests.cs) so two classes can
    // never flip the folder static mid-run.
    [Collection("HermeticDb")]
    public sealed class StockConcurrencyTests : IDisposable
    {
        private readonly string _tempFolder;
        private readonly string? _savedFolderOverride;
        private readonly bool? _savedReadOnlyOverride;
        private readonly bool _savedOperationGuard;

        public StockConcurrencyTests()
        {
            _tempFolder = Path.Combine(
                Path.GetTempPath(),
                "pga_stock_concurrency_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);

            _savedFolderOverride = AppDataRoot.TestFolderOverride;
            AppDataRoot.TestFolderOverride = _tempFolder;

            // The repository gates every mutation on the licence read-only
            // flag. Force it open so the test exercises the write path
            // rather than the licence.
            _savedReadOnlyOverride = ReadOnlyGate.OverrideReadOnlyForTests;
            ReadOnlyGate.OverrideReadOnlyForTests = false;
            _savedOperationGuard = OperationGuard.Enabled;
            OperationGuard.Enabled = false;
        }

        public void Dispose()
        {
            AppDataRoot.TestFolderOverride = _savedFolderOverride;
            ReadOnlyGate.OverrideReadOnlyForTests = _savedReadOnlyOverride;
            OperationGuard.Enabled = _savedOperationGuard;
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(_tempFolder, recursive: true); }
            catch { /* best effort — a lingering temp folder fails nothing */ }
        }

        // ───────────────────────────────────────────────────────────────
        // GlassSheet
        // ───────────────────────────────────────────────────────────────

        [Fact]
        public async Task Concurrent_uses_all_land_and_none_is_lost()
        {
            var repo = new SheetStoreRepository();
            int id = await SeedSheetAsync(stock: 100);

            // 40 operators each issuing one sheet at the same moment.
            var outcomes = await Task.WhenAll(
                Enumerable.Range(0, 40)
                    .Select(_ => AttemptUseAsync(repo, id, 1)));

            Assert.All(outcomes, ok => Assert.True(ok));

            var (stock, used) = await ReadSheetCountersAsync(id);
            Assert.Equal(100, stock);
            Assert.Equal(40, used);          // a lost update would leave < 40
            Assert.Equal(40, await CountSheetMovementsAsync(id, "Used"));
        }

        [Fact]
        public async Task Concurrent_uses_beyond_the_balance_admit_only_what_stock_covers()
        {
            var repo = new SheetStoreRepository();
            int id = await SeedSheetAsync(stock: 10);

            // 40 operators, 10 sheets available. The old code let all 40 pass
            // the balance check against the same stale read. The guard now
            // lives in the UPDATE's WHERE clause, so exactly 10 can win.
            var outcomes = await Task.WhenAll(
                Enumerable.Range(0, 40)
                    .Select(_ => AttemptUseAsync(repo, id, 1)));

            Assert.Equal(10, outcomes.Count(ok => ok));
            Assert.Equal(30, outcomes.Count(ok => !ok));

            var (stock, used) = await ReadSheetCountersAsync(id);
            Assert.Equal(10, stock);
            Assert.Equal(10, used);          // exactly drained, never overspent
            Assert.Equal(10, await CountSheetMovementsAsync(id, "Used"));
        }

        [Fact]
        public async Task Concurrent_receipts_all_land()
        {
            var repo = new SheetStoreRepository();
            int id = await SeedSheetAsync(stock: 0);

            await Task.WhenAll(
                Enumerable.Range(0, 20)
                    .Select(_ => repo.BuyStockAsync(id, 5)));

            var (stock, used) = await ReadSheetCountersAsync(id);
            Assert.Equal(100, stock);        // 20 × 5, none overwritten
            Assert.Equal(0, used);
            Assert.Equal(20, await CountSheetMovementsAsync(id, "Bought"));
        }

        [Fact]
        public async Task Use_throws_when_short_and_leaves_the_row_untouched()
        {
            var repo = new SheetStoreRepository();
            int id = await SeedSheetAsync(stock: 4);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repo.UseStockAsync(id, 5));

            // The message is a contract: it names both numbers, and callers
            // surface it verbatim to the operator.
            Assert.Equal(
                "Not enough stock. Available: 4, Requested: 5",
                ex.Message);

            var (stock, used) = await ReadSheetCountersAsync(id);
            Assert.Equal(4, stock);
            Assert.Equal(0, used);           // a refused write wrote nothing
            Assert.Equal(0, await CountSheetMovementsAsync(id, "Used"));
        }

        [Fact]
        public async Task Use_on_a_missing_sheet_is_silent()
        {
            // Unchanged behaviour, deliberately preserved: callers treat a
            // vanished sheet as a no-op, not an error.
            var repo = new SheetStoreRepository();
            await EnsureSchemaAsync();

            await repo.UseStockAsync(999_999, 1);
            await repo.BuyStockAsync(999_999, 1);
        }

        [Fact]
        public async Task Use_records_the_purchase_price_only_when_one_is_supplied()
        {
            // A bulk write sets only the columns it names. This guards against
            // someone adding a SetProperty and forgetting another, or the
            // price branch silently overwriting with a default.
            var repo = new SheetStoreRepository();
            int id = await SeedSheetAsync(stock: 10, purchasePrice: 250m);

            await repo.BuyStockAsync(id, 5);
            Assert.Equal(250m, await ReadPurchasePriceAsync(id));

            await repo.BuyStockAsync(id, 5, newPurchasePrice: 310m);
            Assert.Equal(310m, await ReadPurchasePriceAsync(id));

            // Zero and null both mean "leave the price alone".
            await repo.BuyStockAsync(id, 5, newPurchasePrice: 0m);
            Assert.Equal(310m, await ReadPurchasePriceAsync(id));
            Assert.Equal(3, await CountSheetMovementsAsync(id, "Bought"));
        }

        [Fact]
        public async Task Stale_edit_cannot_overwrite_a_stock_movement()
        {
            var repo = new SheetStoreRepository();
            int id = await SeedSheetAsync(stock: 10);
            var stale = await repo.GetByIdAsync(id);
            Assert.NotNull(stale);

            await repo.BuyStockAsync(id, 1);
            stale!.SellPricePerSqm = 500m;

            await Assert.ThrowsAsync<ConcurrencyConflictException>(
                () => repo.UpdateAsync(stale, stale.RowVersion));

            var (stock, used) = await ReadSheetCountersAsync(id);
            Assert.Equal(11, stock);
            Assert.Equal(0, used);
            Assert.Equal(1, await CountSheetMovementsAsync(id, "Bought"));
        }

        [Fact]
        public async Task Manual_stock_and_used_adjustments_write_separate_ledger_entries()
        {
            var repo = new SheetStoreRepository();
            int id = await SeedSheetAsync(stock: 10);
            var sheet = await repo.GetByIdAsync(id);
            Assert.NotNull(sheet);

            sheet!.Stock = 12;
            sheet.Used = 3;
            await repo.UpdateAsync(sheet, sheet.RowVersion);

            using var db = new AppDbContext();
            var edited = await db.SheetHistories.AsNoTracking()
                .Where(h => h.SheetId == id && h.Action == "Edited")
                .OrderBy(h => h.Id)
                .ToListAsync();
            Assert.Equal(2, edited.Count);
            Assert.Contains(edited, h => h.QuantityChanged == 2
                && h.Details.StartsWith("Stock manually adjusted:", StringComparison.Ordinal));
            Assert.Contains(edited, h => h.QuantityChanged == 3
                && h.Details.StartsWith("Used quantity manually adjusted:", StringComparison.Ordinal));
        }

        // ───────────────────────────────────────────────────────────────
        // Helpers
        // ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Runs one movement and reports whether it was admitted. A refused
        /// movement is not a test failure — under contention that is the
        /// correct outcome, and how many are refused is what the caller
        /// asserts on.
        /// </summary>
        private static async Task<bool> AttemptUseAsync(
            SheetStoreRepository repo, int sheetId, int quantity)
        {
            try
            {
                await repo.UseStockAsync(sheetId, quantity);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static async Task EnsureSchemaAsync()
        {
            using var db = new AppDbContext();
            await db.Database.EnsureCreatedAsync();
        }

        private static async Task<int> SeedSheetAsync(
            int stock, decimal purchasePrice = 250m)
        {
            using var db = new AppDbContext();
            await db.Database.EnsureCreatedAsync();

            var sheet = new GlassSheet
            {
                Category = "Float",
                ThicknessMm = 6,
                Color = "Clear",
                WidthMm = 3210,
                HeightMm = 2250,
                Supplier = "ConcurrencyTest",
                Stock = stock,
                Used = 0,
                PurchasePricePerSqm = purchasePrice,
                SellPricePerSqm = 400m
            };

            db.GlassSheets.Add(sheet);
            await db.SaveChangesAsync();
            return sheet.Id;
        }

        private static async Task<(int Stock, int Used)> ReadSheetCountersAsync(int id)
        {
            using var db = new AppDbContext();
            var sheet = await db.GlassSheets.AsNoTracking().SingleAsync(g => g.Id == id);
            return (sheet.Stock, sheet.Used);
        }

        private static async Task<int> CountSheetMovementsAsync(int id, string action)
        {
            using var db = new AppDbContext();
            return await db.SheetHistories.AsNoTracking()
                .CountAsync(h => h.SheetId == id && h.Action == action);
        }

        private static async Task<decimal> ReadPurchasePriceAsync(int id)
        {
            using var db = new AppDbContext();
            var sheet = await db.GlassSheets.AsNoTracking().SingleAsync(g => g.Id == id);
            return sheet.PurchasePricePerSqm;
        }
    }
}
