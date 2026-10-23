// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/SheetStore/InventoryItemRepository.cs
// MODULE: Inventory — non-glass consumables
// ADDED: 2026-09-06
//
// Mirrors SheetStoreRepository's conventions deliberately: ReadOnlyGate on
// every mutation and one short-lived AppDbContext per call. Multi-row stock
// mutations use a serializable transaction so the quantity and movement row
// commit together; simpler metadata/delete saves use the shared retry helper.
//
// The one real difference: quantities are DECIMAL, because spacer bar is
// received in linear metres (45.55 LM) and desiccant in kilograms. That
// is the entire reason this table exists apart from GlassSheet.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Licensing;         // ReadOnlyGate
using System;
using System.Collections.Generic;
using System.Data;                                  // IsolationLevel
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.SheetStore
{
    public interface IInventoryItemRepository
    {
        Task<List<InventoryItem>> GetAllAsync();
        Task<InventoryItem?> GetByIdAsync(int id);
        Task<List<InventoryItem>> GetLowStockAsync();
        Task<List<InventoryItemMovement>> GetMovementsAsync(int itemId);
        Task<InventoryItem> AddAsync(InventoryItem item);
        Task UpdateAsync(InventoryItem item, int expectedRowVersion);
        Task DeleteAsync(int id, int expectedRowVersion);

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

        public async Task<List<InventoryItemMovement>> GetMovementsAsync(int itemId)
        {
            using var db = new AppDbContext();
            return await db.InventoryItemMovements.AsNoTracking()
                .Where(m => m.InventoryItemId == itemId)
                .OrderByDescending(m => m.Timestamp)
                .ThenByDescending(m => m.Id)
                .ToListAsync().ConfigureAwait(false);
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
            OperationGuard.Demand(AppPermissions.SheetStoreCreate, "add an inventory item");
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (item.Stock < 0 || item.Used < 0 || item.Stock < item.Used)
                throw new InvalidOperationException("Opening stock must be non-negative and cannot be below used stock.");

            using var db = new AppDbContext();
            await db.Database.OpenConnectionAsync().ConfigureAwait(false);
            await using var tx = await db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable).ConfigureAwait(false);

            item.CreatedAt = DateTime.Now;
            item.LastUpdated = DateTime.Now;
            item.RowVersion = 0;

            db.InventoryItems.Add(item);
            await db.SaveChangesAsync().ConfigureAwait(false);
            if (item.Stock > 0)
            {
                db.InventoryItemMovements.Add(CreateMovement(
                    item, "Added", item.Stock, item.TotalPurchaseValue,
                    $"Opening stock: {item.Stock:N2} {item.UomDisplay}."));
                await db.SaveChangesAsync().ConfigureAwait(false);
            }

            await tx.CommitAsync().ConfigureAwait(false);
            return item;
        }

        public async Task UpdateAsync(InventoryItem item, int expectedRowVersion)
        {
            ReadOnlyGate.Demand("update an inventory item");
            OperationGuard.Demand(AppPermissions.SheetStoreEdit, "update an inventory item");
            if (item == null) throw new ArgumentNullException(nameof(item));

            using var db = new AppDbContext();
            await db.Database.OpenConnectionAsync().ConfigureAwait(false);
            await using var tx = await db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable).ConfigureAwait(false);

            var existing = await db.InventoryItems
                .FirstOrDefaultAsync(i => i.Id == item.Id).ConfigureAwait(false);
            if (existing == null)
                throw new ConcurrencyConflictException(
                    "Inventory item", item.FullDescription,
                    expectedRowVersion, actualRowVersion: -1);
            if (existing.RowVersion != expectedRowVersion)
                throw new ConcurrencyConflictException(
                    "Inventory item", existing.FullDescription,
                    expectedRowVersion, existing.RowVersion);

            decimal oldStock = existing.Stock;
            existing.Name = item.Name;
            existing.Category = item.Category;
            existing.PartNumber = item.PartNumber;
            existing.Supplier = item.Supplier;
            existing.Specification = item.Specification;
            existing.Uom = item.Uom;
            existing.ReorderLevel = item.ReorderLevel;
            existing.PurchasePricePerUnit = item.PurchasePricePerUnit;
            existing.SellPricePerUnit = item.SellPricePerUnit;

            // A deliberate opening-stock correction is allowed only when it
            // does not erase recorded consumption; its adjustment row is
            // written in this same transaction.
            if (item.Stock != oldStock)
            {
                if (item.Stock < existing.Used)
                    throw new InvalidOperationException(
                        $"Stock cannot be set below {existing.Used:N2} {existing.UomDisplay} — " +
                        "that much is already recorded as used.");
                if (item.Stock < 0)
                    throw new InvalidOperationException("Stock cannot be negative.");

                existing.Stock = item.Stock;
                decimal delta = item.Stock - oldStock;
                db.InventoryItemMovements.Add(CreateMovement(
                    existing, "Adjusted", delta,
                    delta * existing.PurchasePricePerUnit,
                    $"Stock manually adjusted: {oldStock:N2} → {item.Stock:N2} {existing.UomDisplay}."));
            }

            DateTime now = DateTime.Now;
            existing.LastUpdated = now;
            existing.RowVersion = expectedRowVersion + 1;
            try
            {
                await db.SaveChangesAsync().ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);
                item.RowVersion = existing.RowVersion;
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync().ConfigureAwait(false);
                int actual = await db.InventoryItems.AsNoTracking()
                    .Where(i => i.Id == item.Id)
                    .Select(i => (int?)i.RowVersion)
                    .FirstOrDefaultAsync().ConfigureAwait(false) ?? -1;
                throw new ConcurrencyConflictException(
                    "Inventory item", item.FullDescription,
                    expectedRowVersion, actual);
            }
        }

        public async Task DeleteAsync(int id, int expectedRowVersion)
        {
            ReadOnlyGate.Demand("delete an inventory item");
            OperationGuard.Demand(AppPermissions.SheetStoreDelete, "delete an inventory item");

            using var db = new AppDbContext();
            await db.Database.OpenConnectionAsync().ConfigureAwait(false);
            await using var tx = await db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable).ConfigureAwait(false);

            var item = await db.InventoryItems
                .FirstOrDefaultAsync(i => i.Id == id).ConfigureAwait(false);
            if (item == null)
                throw new ConcurrencyConflictException(
                    "Inventory item", id.ToString(),
                    expectedRowVersion, actualRowVersion: -1);
            if (item.RowVersion != expectedRowVersion)
                throw new ConcurrencyConflictException(
                    "Inventory item", item.FullDescription,
                    expectedRowVersion, item.RowVersion);

            // Keep the final total/used/available quantities in the immutable
            // snapshot. Use the remaining balance as the removed stock quantity
            // rather than implying that all received stock was still on hand.
            db.InventoryItemMovements.Add(CreateMovement(
                item, "Deleted", item.Balance,
                item.Balance * item.PurchasePricePerUnit,
                $"Inventory item deleted. Total stock: {item.Stock:N2}; " +
                $"used: {item.Used:N2}; available: {item.Balance:N2} {item.UomDisplay}."));
            db.InventoryItems.Remove(item);

            try
            {
                await db.SaveChangesAsync().ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync().ConfigureAwait(false);
                int actual = await db.InventoryItems.AsNoTracking()
                    .Where(i => i.Id == id)
                    .Select(i => (int?)i.RowVersion)
                    .FirstOrDefaultAsync().ConfigureAwait(false) ?? -1;
                throw new ConcurrencyConflictException(
                    "Inventory item", item.FullDescription,
                    expectedRowVersion, actual);
            }
        }

        /// <summary>
        /// Receives a stock quantity and records the counter change, optional
        /// price change, row-version bump, and immutable movement entry in one
        /// serializable transaction. The addition is deliberately decimal
        /// arithmetic in C#: these columns use SQLite TEXT affinity to preserve
        /// fractional units exactly, so SQL-side numeric addition is unsafe.
        /// </summary>
        public async Task ReceiveAsync(
            int itemId, decimal quantity, decimal? newUnitPrice = null)
        {
            ReadOnlyGate.Demand("receive inventory stock");
            OperationGuard.Demand(AppPermissions.SheetStoreEdit, "receive inventory stock");

            if (quantity <= 0)
                throw new InvalidOperationException(
                    "Received quantity must be greater than zero.");

            using var db = new AppDbContext();
            await db.Database.OpenConnectionAsync().ConfigureAwait(false);
            await using var tx = await db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable)
                .ConfigureAwait(false);

            var item = await db.InventoryItems
                .FirstOrDefaultAsync(i => i.Id == itemId).ConfigureAwait(false);
            // Preserve existing command semantics: a row deleted after the
            // receipt dialog opened is a no-op, not a phantom stock movement.
            if (item == null) return;

            decimal oldStock = item.Stock;
            if (newUnitPrice.HasValue && newUnitPrice.Value > 0)
                item.PurchasePricePerUnit = newUnitPrice.Value;

            item.Stock += quantity;
            item.LastPurchaseDate = DateTime.Now;
            item.LastUpdated = DateTime.Now;
            item.RowVersion++;

            db.InventoryItemMovements.Add(CreateMovement(
                item, "Received", quantity,
                quantity * item.PurchasePricePerUnit,
                $"Received {quantity:N2} {item.UomDisplay}. " +
                $"Stock: {oldStock:N2} → {item.Stock:N2}."));

            await db.SaveChangesAsync().ConfigureAwait(false);
            await tx.CommitAsync().ConfigureAwait(false);
        }

        /// <remarks>
        /// The balance check, Used update, RowVersion bump, and Consumed ledger
        /// row share one serializable transaction. Decimal arithmetic remains
        /// in C# to preserve exact fractional quantities stored with SQLite
        /// TEXT affinity; see ReceiveAsync for the precision rationale.
        /// </remarks>
        public async Task ConsumeAsync(int itemId, decimal quantity)
        {
            ReadOnlyGate.Demand("consume inventory stock");
            OperationGuard.Demand(AppPermissions.SheetStoreEdit, "consume inventory stock");

            if (quantity <= 0)
                throw new InvalidOperationException(
                    "Consumed quantity must be greater than zero.");

            using var db = new AppDbContext();

            // See ReceiveAsync for why Serializable and not the default.
            await db.Database.OpenConnectionAsync().ConfigureAwait(false);
            using var tx = await db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable)
                .ConfigureAwait(false);

            var item = await db.InventoryItems
                .FirstOrDefaultAsync(i => i.Id == itemId).ConfigureAwait(false);
            if (item == null) return;

            if (item.Balance < quantity)
                throw new InvalidOperationException(
                    $"Not enough stock. Available: {item.Balance:N2} {item.UomDisplay}, " +
                    $"requested: {quantity:N2} {item.UomDisplay}.");

            decimal oldUsed = item.Used;
            item.Used += quantity;
            item.LastUpdated = DateTime.Now;
            item.RowVersion++;

            db.InventoryItemMovements.Add(CreateMovement(
                item, "Consumed", quantity,
                quantity * item.PurchasePricePerUnit,
                $"Consumed {quantity:N2} {item.UomDisplay}. Used: {oldUsed:N2} → {item.Used:N2}."));

            await db.SaveChangesAsync().ConfigureAwait(false);
            await tx.CommitAsync().ConfigureAwait(false);
        }

        private static InventoryItemMovement CreateMovement(
            InventoryItem item, string action, decimal quantity,
            decimal? value, string details) => new()
        {
            InventoryItemId = item.Id,
            ItemDescription = Limit(item.FullDescription, 300),
            Action = Limit(action, 20),
            Details = Limit(details, 500),
            QuantityChanged = quantity,
            ValueChanged = value,
            UserName = Limit(Environment.UserName, 100),
            Timestamp = DateTime.Now
        };

        private static string Limit(string? value, int maxLength)
        {
            value ??= string.Empty;
            return value.Length <= maxLength ? value : value[..maxLength];
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
