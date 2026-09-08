// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/SheetStore/InventoryItemRepository.cs
// MODULE: Inventory — non-glass consumables
// ADDED: 2026-09-06
//
// Mirrors SheetStoreRepository's conventions deliberately: ReadOnlyGate on
// every mutation, SqliteResilience.SaveWithRetryAsync on every save, one
// short-lived AppDbContext per call. Consistency matters more than
// cleverness here — the next person to read these two files should not
// have to learn two idioms.
//
// The one real difference: quantities are DECIMAL, because spacer bar is
// received in linear metres (45.55 LM) and desiccant in kilograms. That
// is the entire reason this table exists apart from GlassSheet.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Licensing;         // ReadOnlyGate
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.SheetStore
{
    public interface IInventoryItemRepository
    {
        Task<List<InventoryItem>> GetAllAsync();
        Task<InventoryItem?> GetByIdAsync(int id);
        Task<List<InventoryItem>> GetLowStockAsync();
        Task<InventoryItem> AddAsync(InventoryItem item);
        Task UpdateAsync(InventoryItem item);
        Task DeleteAsync(int id);

        /// <summary>Receives stock. Quantity must be &gt; 0.</summary>
        Task ReceiveAsync(int itemId, decimal quantity, decimal? newUnitPrice = null);

        /// <summary>
        /// Consumes stock. Quantity must be &gt; 0 and within the balance;
        /// over-issue throws rather than driving the balance negative.
        /// </summary>
        Task ConsumeAsync(int itemId, decimal quantity);

        Task<List<string>> GetSuppliersAsync();
    }

    public class InventoryItemRepository : IInventoryItemRepository
    {
        public async Task<List<InventoryItem>> GetAllAsync()
        {
            using var db = new AppDbContext();
            return await db.InventoryItems.AsNoTracking()
                .OrderBy(i => i.Category)
                .ThenBy(i => i.Name)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<InventoryItem?> GetByIdAsync(int id)
        {
            using var db = new AppDbContext();
            return await db.InventoryItems.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id).ConfigureAwait(false);
        }

        /// <summary>
        /// Items at or below their reorder level.
        ///
        /// Filtered in MEMORY on purpose: Balance and IsLowStock are
        /// [NotMapped] computed properties, so EF cannot translate them to
        /// SQL. This table holds consumable TYPES (tens of rows), not
        /// movements, so loading it whole is cheap — and a wrong-but-fast
        /// SQL translation here would silently miss low-stock items, which
        /// is the one thing this query exists to prevent.
        /// </summary>
        public async Task<List<InventoryItem>> GetLowStockAsync()
        {
            using var db = new AppDbContext();
            var all = await db.InventoryItems.AsNoTracking()
                .ToListAsync().ConfigureAwait(false);

            return all.Where(i => i.IsLowStock)
                      .OrderBy(i => i.Balance)
                      .ToList();
        }

        public async Task<InventoryItem> AddAsync(InventoryItem item)
        {
            ReadOnlyGate.Demand("add an inventory item");

            using var db = new AppDbContext();
            item.CreatedAt = DateTime.Now;
            item.LastUpdated = DateTime.Now;

            db.InventoryItems.Add(item);
            await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());
            return item;
        }

        public async Task UpdateAsync(InventoryItem item)
        {
            ReadOnlyGate.Demand("update an inventory item");

            using var db = new AppDbContext();
            var existing = await db.InventoryItems
                .FirstOrDefaultAsync(i => i.Id == item.Id).ConfigureAwait(false);
            if (existing == null) return;

            existing.Name = item.Name;
            existing.Category = item.Category;
            existing.PartNumber = item.PartNumber;
            existing.Supplier = item.Supplier;
            existing.Specification = item.Specification;
            existing.Uom = item.Uom;
            existing.ReorderLevel = item.ReorderLevel;
            existing.PurchasePricePerUnit = item.PurchasePricePerUnit;
            existing.SellPricePerUnit = item.SellPricePerUnit;

            // Stock is NOT copied wholesale. It moves only through
            // ReceiveAsync/ConsumeAsync so every change has a reason —
            // except a deliberate correction, which must still respect
            // what has already been consumed.
            if (item.Stock != existing.Stock)
            {
                if (item.Stock < existing.Used)
                    throw new InvalidOperationException(
                        $"Stock cannot be set below {existing.Used:N2} {existing.UomDisplay} — " +
                        "that much is already recorded as used.");
                existing.Stock = item.Stock;
            }

            existing.LastUpdated = DateTime.Now;
            await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());
        }

        public async Task DeleteAsync(int id)
        {
            ReadOnlyGate.Demand("delete an inventory item");

            using var db = new AppDbContext();
            var item = await db.InventoryItems
                .FirstOrDefaultAsync(i => i.Id == id).ConfigureAwait(false);
            if (item == null) return;

            db.InventoryItems.Remove(item);
            await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());
        }

        public async Task ReceiveAsync(int itemId, decimal quantity, decimal? newUnitPrice = null)
        {
            ReadOnlyGate.Demand("receive inventory stock");

            // Refuse rather than no-op. A zero receipt is a mistake, and
            // silently doing nothing is how the operator ends up believing
            // stock was booked in when it was not.
            if (quantity <= 0)
                throw new InvalidOperationException(
                    "Received quantity must be greater than zero.");

            using var db = new AppDbContext();
            var item = await db.InventoryItems
                .FirstOrDefaultAsync(i => i.Id == itemId).ConfigureAwait(false);
            if (item == null) return;

            item.Stock += quantity;
            if (newUnitPrice.HasValue && newUnitPrice.Value > 0)
                item.PurchasePricePerUnit = newUnitPrice.Value;

            item.LastPurchaseDate = DateTime.Now;
            item.LastUpdated = DateTime.Now;

            await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());
        }

        public async Task ConsumeAsync(int itemId, decimal quantity)
        {
            ReadOnlyGate.Demand("consume inventory stock");

            if (quantity <= 0)
                throw new InvalidOperationException(
                    "Consumed quantity must be greater than zero.");

            using var db = new AppDbContext();
            var item = await db.InventoryItems
                .FirstOrDefaultAsync(i => i.Id == itemId).ConfigureAwait(false);
            if (item == null) return;

            if (item.Balance < quantity)
                throw new InvalidOperationException(
                    $"Not enough stock. Available: {item.Balance:N2} {item.UomDisplay}, " +
                    $"requested: {quantity:N2} {item.UomDisplay}.");

            item.Used += quantity;
            item.LastUpdated = DateTime.Now;

            await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());
        }

        public async Task<List<string>> GetSuppliersAsync()
        {
            using var db = new AppDbContext();
            return await db.InventoryItems.AsNoTracking()
                .Select(i => i.Supplier)
                .Where(s => s != "")
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync().ConfigureAwait(false);
        }
    }
}
