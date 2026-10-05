// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Database/InventoryStockConcurrencyTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Inventory — consumable stock counter atomicity
//
// WHY THIS EXISTS
// ───────────────
// GlassSheet's counters were converted to a single guarded conditional UPDATE
// and got a regression net (StockConcurrencyTests). The consumables table —
// spacer bar in linear metres, desiccant in kilograms — has DECIMAL counters
// and never got one. Its movements were still a read-modify-write in C#:
//
//     var item = await db.InventoryItems.FirstOrDefaultAsync(...);   // read
//     if (item.Balance < quantity) throw ...;                        // check
//     item.Used += quantity;                                         // write
//
// Two operators issuing from the same item both read the same balance, both
// pass the check, and the second save overwrites the first. One issue is
// lost — no exception, no log line, and a row that looks perfectly consistent
// afterwards. The only way to see it is that the count is wrong later.
//
// These tests fail against that code. They are the net for the fix, which
// wraps the read-check-write in a serializable transaction rather than moving
// the arithmetic into SQL: Stock and Used are declared with TEXT affinity so
// that 45.55 LM stays an exact .NET decimal, and SQL arithmetic would route
// both the increment and the guard through SQLite's REAL type. See the
// remarks on InventoryItemRepository.ReceiveAsync for the measured evidence.
//
// WHAT IS ACTUALLY BEING ASSERTED
// ───────────────────────────────
// Not "it does not crash". The exact final counter. A lost update does not
// throw — it leaves a number wrong by however many writes collided. So the
// assertions are: after N concurrent movements the counter moved by exactly
// N; when the balance cannot cover all of them, exactly the affordable number
// are admitted; and the decimal precision the TEXT affinity exists to protect
// survives the round trip.
//
// Hermetic: AppDataRoot points at a temp folder, the read-only gate is forced
// off, connection pools are cleared and the folder is deleted in Dispose.
// Same shape as StockConcurrencyTests.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.Database;
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
    public sealed class InventoryStockConcurrencyTests : IDisposable
    {
        private readonly string _tempFolder;
        private readonly string? _savedFolderOverride;
        private readonly bool? _savedReadOnlyOverride;

        public InventoryStockConcurrencyTests()
        {
            _tempFolder = Path.Combine(
                Path.GetTempPath(),
                "pga_inventory_concurrency_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);

            _savedFolderOverride = AppDataRoot.TestFolderOverride;
            AppDataRoot.TestFolderOverride = _tempFolder;

            // The repository gates every mutation on the licence read-only
            // flag. Force it open so the test exercises the write path
            // rather than the licence.
            _savedReadOnlyOverride = ReadOnlyGate.OverrideReadOnlyForTests;
            ReadOnlyGate.OverrideReadOnlyForTests = false;
        }

        public void Dispose()
        {
            AppDataRoot.TestFolderOverride = _savedFolderOverride;
            ReadOnlyGate.OverrideReadOnlyForTests = _savedReadOnlyOverride;
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(_tempFolder, recursive: true); }
            catch { /* best effort — a lingering temp folder fails nothing */ }
        }

        // ───────────────────────────────────────────────────────────────
        // ConsumeAsync
        // ───────────────────────────────────────────────────────────────

        [Fact]
        public async Task Concurrent_issues_all_land_and_none_is_lost()
        {
            var repo = new InventoryItemRepository();
            int id = await SeedItemAsync(stock: 100m);

            // 40 operators each issuing one piece at the same moment.
            var outcomes = await Task.WhenAll(
                Enumerable.Range(0, 40)
                    .Select(_ => AttemptConsumeAsync(repo, id, 1m)));

            Assert.All(outcomes, ok => Assert.True(ok));

            var (stock, used) = await ReadCountersAsync(id);
            Assert.Equal(100m, stock);
            Assert.Equal(40m, used);          // a lost update would leave < 40
        }

        [Fact]
        public async Task Concurrent_issues_beyond_the_balance_admit_only_what_stock_covers()
        {
            var repo = new InventoryItemRepository();
            int id = await SeedItemAsync(stock: 10m);

            // 40 operators, 10 pieces available. Against the old code all 40
            // passed the balance check on the same stale read and the balance
            // ended up overspent. Inside the transaction the check and the
            // write are one unit, so exactly 10 can win.
            var outcomes = await Task.WhenAll(
                Enumerable.Range(0, 40)
                    .Select(_ => AttemptConsumeAsync(repo, id, 1m)));

            Assert.Equal(10, outcomes.Count(ok => ok));
            Assert.Equal(30, outcomes.Count(ok => !ok));

            var (stock, used) = await ReadCountersAsync(id);
            Assert.Equal(10m, stock);
            Assert.Equal(10m, used);          // exactly drained, never overspent
        }

        [Fact]
        public async Task Consume_throws_when_short_and_leaves_the_row_untouched()
        {
            var repo = new InventoryItemRepository();
            int id = await SeedItemAsync(stock: 4m);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repo.ConsumeAsync(id, 5m));

            // The message is a contract: it names both numbers and the unit,
            // and callers surface it verbatim to the operator. Built with the
            // same format string rather than hardcoded, so the assertion tests
            // the contract and not the runner's culture.
            Assert.Equal(
                $"Not enough stock. Available: {4m:N2} pcs, requested: {5m:N2} pcs.",
                ex.Message);

            var (stock, used) = await ReadCountersAsync(id);
            Assert.Equal(4m, stock);
            Assert.Equal(0m, used);           // a refused write wrote nothing
        }

        [Fact]
        public async Task Consume_on_a_missing_item_is_silent()
        {
            // Unchanged behaviour, deliberately preserved: callers treat a
            // vanished item as a no-op, not an error.
            var repo = new InventoryItemRepository();
            await EnsureSchemaAsync();

            await repo.ConsumeAsync(999_999, 1m);
            await repo.ReceiveAsync(999_999, 1m);
        }

        // ───────────────────────────────────────────────────────────────
        // ReceiveAsync
        // ───────────────────────────────────────────────────────────────

        [Fact]
        public async Task Concurrent_receipts_all_land()
        {
            var repo = new InventoryItemRepository();
            int id = await SeedItemAsync(stock: 0m);

            await Task.WhenAll(
                Enumerable.Range(0, 20)
                    .Select(_ => repo.ReceiveAsync(id, 5m)));

            var (stock, used) = await ReadCountersAsync(id);
            Assert.Equal(100m, stock);        // 20 × 5, none overwritten
            Assert.Equal(0m, used);
        }

        [Fact]
        public async Task Concurrent_fractional_receipts_keep_decimal_precision()
        {
            // The whole reason this table is not GlassSheet: spacer bar is
            // received in linear metres and 45.55 LM has to survive exactly.
            // The fix deliberately keeps the addition in C# on decimal — in
            // SQL these TEXT-affinity columns would be summed as REAL.
            var repo = new InventoryItemRepository();
            int id = await SeedItemAsync(stock: 0m, uom: StockUom.LinearMetre);

            await Task.WhenAll(
                Enumerable.Range(0, 200)
                    .Select(_ => repo.ReceiveAsync(id, 0.05m)));

            var (stock, _) = await ReadCountersAsync(id);
            Assert.Equal(10m, stock);         // 200 × 0.05, to the last digit
        }

        [Fact]
        public async Task Receive_records_the_unit_price_only_when_one_is_supplied()
        {
            var repo = new InventoryItemRepository();
            int id = await SeedItemAsync(stock: 10m, purchasePrice: 25m);

            await repo.ReceiveAsync(id, 5m);
            Assert.Equal(25m, await ReadPurchasePriceAsync(id));

            await repo.ReceiveAsync(id, 5m, newUnitPrice: 31m);
            Assert.Equal(31m, await ReadPurchasePriceAsync(id));

            // Zero and null both mean "leave the price alone".
            await repo.ReceiveAsync(id, 5m, newUnitPrice: 0m);
            Assert.Equal(31m, await ReadPurchasePriceAsync(id));
        }

        [Fact]
        public async Task Interleaved_receipts_and_issues_end_at_the_arithmetic_answer()
        {
            // The mixed case is the one that shows up in production: a
            // delivery lands while the shop floor is issuing. Whatever the
            // interleaving, the final Used is the sum of the issues and the
            // final Stock is the seed plus the receipts.
            var repo = new InventoryItemRepository();
            int id = await SeedItemAsync(stock: 50m);

            await Task.WhenAll(
                Enumerable.Range(0, 30).Select(_ => repo.ReceiveAsync(id, 2m))
                    .Concat(Enumerable.Range(0, 30).Select(_ => repo.ConsumeAsync(id, 1m))));

            var (stock, used) = await ReadCountersAsync(id);
            Assert.Equal(110m, stock);        // 50 + (30 × 2)
            Assert.Equal(30m, used);          // 30 × 1
        }

        // ───────────────────────────────────────────────────────────────
        // Helpers
        // ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Runs one issue and reports whether it was admitted. A refused
        /// movement is not a test failure — under contention that is the
        /// correct outcome, and how many are refused is what the caller
        /// asserts on.
        /// </summary>
        private static async Task<bool> AttemptConsumeAsync(
            InventoryItemRepository repo, int itemId, decimal quantity)
        {
            try
            {
                await repo.ConsumeAsync(itemId, quantity);
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

        private static async Task<int> SeedItemAsync(
            decimal stock,
            decimal purchasePrice = 25m,
            StockUom uom = StockUom.Piece)
        {
            using var db = new AppDbContext();
            await db.Database.EnsureCreatedAsync();

            var item = new InventoryItem
            {
                Name = "Concurrency test consumable",
                Supplier = "ConcurrencyTest",
                Uom = uom,
                Stock = stock,
                Used = 0m,
                ReorderLevel = 0m,
                PurchasePricePerUnit = purchasePrice,
                SellPricePerUnit = purchasePrice * 2m
            };

            db.InventoryItems.Add(item);
            await db.SaveChangesAsync();
            return item.Id;
        }

        private static async Task<(decimal Stock, decimal Used)> ReadCountersAsync(int id)
        {
            using var db = new AppDbContext();
            var item = await db.InventoryItems.AsNoTracking().SingleAsync(i => i.Id == id);
            return (item.Stock, item.Used);
        }

        private static async Task<decimal> ReadPurchasePriceAsync(int id)
        {
            using var db = new AppDbContext();
            var item = await db.InventoryItems.AsNoTracking().SingleAsync(i => i.Id == id);
            return item.PurchasePricePerUnit;
        }
    }
}
