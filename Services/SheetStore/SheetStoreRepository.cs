using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Models.SheetStore.SheetStoreHistory;
using ProGlassAutomation.Services.Database;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using ProGlassAutomation.Services.Audit;

using ProGlassAutomation.Services.Licensing;         // ReadOnlyGate
namespace ProGlassAutomation.Services.SheetStore
{
    /// <summary>
    /// SQLite / EF Core implementation of ISheetStoreRepository.
    /// One DbContext per operation for thread safety.
    /// </summary>
    public class SheetStoreRepository : ISheetStoreRepository
    {
        public async Task<List<GlassSheet>> GetAllAsync()
        {
            using var db = new AppDbContext();
            return await db.GlassSheets
                .AsNoTracking()
                .OrderBy(g => g.Category)
                .ThenBy(g => g.Color)
                .ThenBy(g => g.ThicknessMm)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<GlassSheet?> GetByIdAsync(int id)
        {
            using var db = new AppDbContext();
            return await db.GlassSheets.FindAsync(id).ConfigureAwait(false);
        }

        public async Task<List<GlassSheet>> SearchAsync(
            string? category = null,
            string? color = null,
            string? supplier = null,
            int? thicknessMm = null,
            string? search = null,
            string? sortKey = null,
            int skip = 0,
            int take = 200)
        {
            if (skip < 0) skip = 0;
            if (take <= 0) take = 200;

            using var db = new AppDbContext();

            IQueryable<GlassSheet> query = db.GlassSheets.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(g => g.Category == category);
            if (!string.IsNullOrWhiteSpace(color))
                query = query.Where(g => g.Color == color);
            if (!string.IsNullOrWhiteSpace(supplier))
                query = query.Where(g => g.Supplier == supplier);
            if (thicknessMm.HasValue && thicknessMm.Value > 0)
                query = query.Where(g => g.ThicknessMm == thicknessMm.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim().ToLower();
                // NOTE: no ThicknessDisplay here - it is a computed property
                // (not a DB column) so EF cannot translate it to SQL. Thickness
                // is searchable via the exact thicknessMm filter above.
                query = query.Where(g =>
                    g.Category.ToLower().Contains(s) ||
                    g.Color.ToLower().Contains(s) ||
                    g.Supplier.ToLower().Contains(s));
            }

            // TotalPurchaseValue / Balance are [NotMapped] computed
            // properties — EF cannot translate them. Value sorts stay
            // in the caller (SheetStoreViewModel.ApplySort) after the
            // page is materialised. Asking SQL to OrderBy them throws.
            query = (sortKey ?? "").ToLowerInvariant() switch
            {
                "newest" => query.OrderByDescending(g => g.CreatedAt),
                "oldest" => query.OrderBy(g => g.CreatedAt),
                "stockasc" => query.OrderBy(g => g.Stock),
                "stockdesc" => query.OrderByDescending(g => g.Stock),
                _ => query.OrderBy(g => g.Category)
                          .ThenBy(g => g.Color)
                          .ThenBy(g => g.ThicknessMm)
            };

            return await query
                .Skip(skip)
                .Take(take)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<GlassSheet> AddAsync(GlassSheet sheet)
        {
            ReadOnlyGate.Demand("add a glass sheet");
            OperationGuard.Demand(
                AppPermissions.SheetStoreCreate, "add a glass sheet");
            if (sheet == null) throw new ArgumentNullException(nameof(sheet));
            if (sheet.Stock < 0 || sheet.Used < 0 || sheet.Stock < sheet.Used)
                throw new InvalidOperationException("Opening stock must be non-negative and cannot be below used stock.");

            using var db = new AppDbContext();
            await db.Database.OpenConnectionAsync().ConfigureAwait(false);
            await using var tx = await db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable).ConfigureAwait(false);

            DateTime now = DateTime.Now;
            sheet.CreatedAt = now;
            sheet.LastUpdated = now;
            sheet.RowVersion = 0;
            if (sheet.Stock > 0)
                sheet.LastPurchaseDate = now;

            db.GlassSheets.Add(sheet);
            await db.SaveChangesAsync().ConfigureAwait(false);

            // The sheet needs its generated Id before the immutable movement
            // row can reference it. Both saves remain inside this transaction:
            // either the sheet and opening-stock history commit together or
            // neither does.
            if (sheet.Stock > 0)
            {
                db.SheetHistories.Add(CreateHistory(
                    sheet, "Added", sheet.Stock, sheet.TotalPurchaseValue,
                    $"Initial stock: {sheet.Stock} sheets @ {sheet.PurchasePricePerSqm:N2} AED/SQM. " +
                    $"Total value: {sheet.TotalPurchaseValue:N2} AED."));
                await db.SaveChangesAsync().ConfigureAwait(false);
            }

            await tx.CommitAsync().ConfigureAwait(false);
            return sheet;
        }

        public async Task UpdateAsync(GlassSheet sheet, int expectedRowVersion)
        {
            ReadOnlyGate.Demand("update a glass sheet");
            OperationGuard.Demand(
                AppPermissions.SheetStoreEdit, "update a glass sheet");
            if (sheet == null) throw new ArgumentNullException(nameof(sheet));

            using var db = new AppDbContext();
            await db.Database.OpenConnectionAsync().ConfigureAwait(false);
            await using var tx = await db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable).ConfigureAwait(false);

            var stored = await db.GlassSheets
                .FirstOrDefaultAsync(g => g.Id == sheet.Id).ConfigureAwait(false);
            if (stored == null)
                throw new ConcurrencyConflictException(
                    "Glass sheet", sheet.Id.ToString(), expectedRowVersion,
                    actualRowVersion: -1);
            if (stored.RowVersion != expectedRowVersion)
                throw new ConcurrencyConflictException(
                    "Glass sheet", DescribeSheet(stored), expectedRowVersion,
                    stored.RowVersion);
            if (sheet.Stock < 0 || sheet.Used < 0 || sheet.Stock < sheet.Used)
                throw new InvalidOperationException("Stock and used quantities must be non-negative, and stock cannot be below used stock.");

            int oldStock = stored.Stock;
            int oldUsed = stored.Used;
            decimal oldPurchasePrice = stored.PurchasePricePerSqm;
            decimal oldSellPrice = stored.SellPricePerSqm;
            string oldCategory = stored.Category;
            string oldColor = stored.Color;
            int oldThickness = stored.ThicknessMm;
            int oldWidth = stored.WidthMm;
            int oldHeight = stored.HeightMm;
            string oldSupplier = stored.Supplier;
            DateTime createdAt = stored.CreatedAt;

            db.Entry(stored).CurrentValues.SetValues(sheet);
            stored.CreatedAt = createdAt;
            stored.LastUpdated = DateTime.Now;
            stored.RowVersion = expectedRowVersion + 1;

            var metadataChanges = new List<string>();
            if (oldPurchasePrice != stored.PurchasePricePerSqm)
                metadataChanges.Add($"Buy: {oldPurchasePrice:N2} → {stored.PurchasePricePerSqm:N2} AED/SQM");
            if (oldSellPrice != stored.SellPricePerSqm)
                metadataChanges.Add($"Sell: {oldSellPrice:N2} → {stored.SellPricePerSqm:N2} AED/SQM");
            if (oldCategory != stored.Category) metadataChanges.Add($"Category: {oldCategory} → {stored.Category}");
            if (oldColor != stored.Color) metadataChanges.Add($"Color: {oldColor} → {stored.Color}");
            if (oldThickness != stored.ThicknessMm) metadataChanges.Add($"Thickness: {oldThickness} → {stored.ThicknessMm}mm");
            if (oldWidth != stored.WidthMm || oldHeight != stored.HeightMm)
                metadataChanges.Add($"Size: {oldWidth} × {oldHeight} → {stored.WidthMm} × {stored.HeightMm}");
            if (oldSupplier != stored.Supplier) metadataChanges.Add($"Supplier: {oldSupplier} → {stored.Supplier}");

            int stockDelta = stored.Stock - oldStock;
            int usedDelta = stored.Used - oldUsed;

            // Stock and Used are separate counters, so a single generic edit
            // row cannot truthfully encode both deltas in QuantityChanged.
            // Write one immutable ledger entry per changed counter in this
            // same transaction as the sheet update. This also means CSV
            // DuplicateAction.UpdateAll cannot silently bypass movement
            // history when it replaces imported balances.
            if (stockDelta != 0)
            {
                db.SheetHistories.Add(CreateHistory(
                    stored, "Edited", stockDelta,
                    stockDelta * stored.SqmPerSheet * stored.PurchasePricePerSqm,
                    $"Stock manually adjusted: {oldStock} → {stored.Stock}."));
            }
            if (usedDelta != 0)
            {
                db.SheetHistories.Add(CreateHistory(
                    stored, "Edited", usedDelta,
                    usedDelta * stored.SqmPerSheet * stored.PurchasePricePerSqm,
                    $"Used quantity manually adjusted: {oldUsed} → {stored.Used}."));
            }

            // Metadata-only changes still need an audit row. When a quantity
            // counter also changed, keep its movement row unambiguous and
            // record the scalar edits separately with quantity zero.
            if (metadataChanges.Count > 0 || (stockDelta == 0 && usedDelta == 0))
            {
                string details = metadataChanges.Count == 0
                    ? "Sheet details saved without scalar changes."
                    : string.Join(" | ", metadataChanges);
                db.SheetHistories.Add(CreateHistory(
                    stored, "Edited", 0, null, details));
            }

            try
            {
                await db.SaveChangesAsync().ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);
                sheet.RowVersion = stored.RowVersion;
                sheet.LastUpdated = stored.LastUpdated;
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync().ConfigureAwait(false);
                int actual = await db.GlassSheets.AsNoTracking()
                    .Where(g => g.Id == sheet.Id)
                    .Select(g => (int?)g.RowVersion)
                    .FirstOrDefaultAsync().ConfigureAwait(false) ?? -1;
                throw new ConcurrencyConflictException(
                    "Glass sheet", DescribeSheet(sheet), expectedRowVersion, actual);
            }
        }

        public async Task DeleteAsync(int id, int expectedRowVersion)
        {
            ReadOnlyGate.Demand("delete a glass sheet");
            OperationGuard.Demand(
                AppPermissions.SheetStoreDelete, "delete a glass sheet");

            using var db = new AppDbContext();
            await db.Database.OpenConnectionAsync().ConfigureAwait(false);
            await using var tx = await db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable).ConfigureAwait(false);

            var sheet = await db.GlassSheets
                .FirstOrDefaultAsync(g => g.Id == id).ConfigureAwait(false);
            if (sheet == null)
                throw new ConcurrencyConflictException(
                    "Glass sheet", id.ToString(), expectedRowVersion,
                    actualRowVersion: -1);
            if (sheet.RowVersion != expectedRowVersion)
                throw new ConcurrencyConflictException(
                    "Glass sheet", DescribeSheet(sheet), expectedRowVersion,
                    sheet.RowVersion);

            db.SheetHistories.Add(CreateHistory(
                sheet, "Deleted", sheet.Stock,
                sheet.TotalPurchaseValue,
                $"Removed from inventory. Lost {sheet.Stock} sheets worth {sheet.TotalPurchaseValue:N2} AED."));
            db.GlassSheets.Remove(sheet);

            try
            {
                await db.SaveChangesAsync().ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync().ConfigureAwait(false);
                int actual = await db.GlassSheets.AsNoTracking()
                    .Where(g => g.Id == id)
                    .Select(g => (int?)g.RowVersion)
                    .FirstOrDefaultAsync().ConfigureAwait(false) ?? -1;
                throw new ConcurrencyConflictException(
                    "Glass sheet", id.ToString(), expectedRowVersion, actual);
            }
        }

        /// <summary>
        /// Atomically increments stock and records the matching purchase
        /// movement in the same SQLite transaction. The current counters are
        /// read after taking the serializable write transaction, so concurrent
        /// receipts cannot overwrite each other or leave an unlogged change.
        /// A missing sheet still returns without error.
        /// </summary>
        public async Task BuyStockAsync(
            int sheetId, int quantity, decimal? newPurchasePrice = null,
            string? source = null)
        {
            ReadOnlyGate.Demand("record a stock purchase");
            OperationGuard.Demand(
                AppPermissions.SheetStoreEdit, "record a stock purchase");
            if (quantity <= 0) return;

            using var db = new AppDbContext();
            await db.Database.OpenConnectionAsync().ConfigureAwait(false);
            await using var tx = await db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable).ConfigureAwait(false);

            var sheet = await db.GlassSheets
                .FirstOrDefaultAsync(g => g.Id == sheetId).ConfigureAwait(false);
            if (sheet == null) return;

            int oldStock = sheet.Stock;
            if (newPurchasePrice.HasValue && newPurchasePrice.Value > 0)
                sheet.PurchasePricePerSqm = newPurchasePrice.Value;
            sheet.Stock += quantity;
            DateTime now = DateTime.Now;
            sheet.LastPurchaseDate = now;
            sheet.LastUpdated = now;
            sheet.RowVersion++;

            decimal value = quantity * sheet.SqmPerSheet * sheet.PurchasePricePerSqm;
            string reason = string.IsNullOrWhiteSpace(source)
                ? "Purchase received"
                : source.Trim();
            db.SheetHistories.Add(CreateHistory(
                sheet, "Bought", quantity, value,
                $"{reason}: purchased {quantity} sheet(s). Stock: {oldStock} → {sheet.Stock}. " +
                $"Value: {value:N2} AED."));

            await db.SaveChangesAsync().ConfigureAwait(false);
            await tx.CommitAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Consumes stock and records a matching Used movement atomically.
        /// The balance guard, row-version bump, and history insert all share
        /// one serializable transaction; a failed history write rolls back the
        /// stock change as well. A missing sheet is returned as null;
        /// insufficient stock throws an InvalidOperationException.
        /// </summary>
        public async Task<GlassSheet?> UseStockAsync(
            int sheetId, int quantity, string origin = "Sheet Store",
            string? documentReference = null, string? customerName = null,
            string? projectName = null, string? salesman = null,
            string? panePosition = null)
        {
            ReadOnlyGate.Demand("record stock usage");
            OperationGuard.Demand(
                AppPermissions.SheetStoreEdit, "record stock usage");
            if (quantity <= 0) return null;

            using var db = new AppDbContext();
            await db.Database.OpenConnectionAsync().ConfigureAwait(false);
            await using var tx = await db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable).ConfigureAwait(false);

            var sheet = await db.GlassSheets
                .FirstOrDefaultAsync(g => g.Id == sheetId).ConfigureAwait(false);
            if (sheet == null) return null;

            int balance = sheet.Stock - sheet.Used;
            if (balance < quantity)
                throw new InvalidOperationException(
                    $"Not enough stock. Available: {balance}, Requested: {quantity}");

            int oldUsed = sheet.Used;
            sheet.Used += quantity;
            sheet.LastUpdated = DateTime.Now;
            sheet.RowVersion++;

            decimal value = quantity * sheet.SqmPerSheet * sheet.SellPricePerSqm;
            string safeOrigin = string.IsNullOrWhiteSpace(origin)
                ? "Sheet Store"
                : origin.Trim();
            string jobTag = string.IsNullOrWhiteSpace(documentReference)
                ? string.Empty
                : $" [Job: {documentReference.Trim()}]";
            db.SheetHistories.Add(CreateHistory(
                sheet, "Used", quantity, value,
                $"Consumed {quantity} sheet(s) via {safeOrigin}.{jobTag} " +
                $"Used: {oldUsed} → {sheet.Used}. Value: {value:N2} AED.",
                documentReference, customerName, projectName, salesman,
                panePosition));

            await db.SaveChangesAsync().ConfigureAwait(false);
            await tx.CommitAsync().ConfigureAwait(false);
            return sheet;
        }

        private static SheetHistory CreateHistory(
            GlassSheet sheet, string action, int quantityChanged,
            decimal? valueChanged, string details,
            string? documentReference = null, string? customerName = null,
            string? projectName = null, string? salesman = null,
            string? panePosition = null) => new()
        {
            SheetId = sheet.Id,
            SheetDescription = Limit(DescribeSheet(sheet), 200),
            Action = Limit(action, 20),
            Details = Limit(details, 500),
            QuantityChanged = quantityChanged,
            ValueChanged = valueChanged,
            UserName = Limit(Environment.UserName, 100),
            Timestamp = DateTime.Now,
            DocumentReference = CleanTrace(documentReference),
            CustomerName = CleanTrace(customerName),
            ProjectName = CleanTrace(projectName),
            Salesman = CleanTrace(salesman),
            PanePosition = CleanTrace(panePosition)
        };

        private static string DescribeSheet(GlassSheet sheet) =>
            $"{sheet.Color} {sheet.ThicknessMm}mm {sheet.SizeDisplay}";

        private static string? CleanTrace(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string Limit(string? value, int maxLength)
        {
            value ??= string.Empty;
            return value.Length <= maxLength ? value : value[..maxLength];
        }

        public async Task<List<string>> GetCategoriesAsync()
        {
            using var db = new AppDbContext();
            return await db.GlassSheets
                .AsNoTracking()
                .Select(g => g.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<List<string>> GetColorsAsync()
        {
            using var db = new AppDbContext();
            return await db.GlassSheets
                .AsNoTracking()
                .Select(g => g.Color)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<List<string>> GetSuppliersAsync()
        {
            using var db = new AppDbContext();
            return await db.GlassSheets
                .AsNoTracking()
                .Select(g => g.Supplier)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<List<int>> GetThicknessesAsync()
        {
            using var db = new AppDbContext();
            return await db.GlassSheets
                .AsNoTracking()
                .Select(g => g.ThicknessMm)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync().ConfigureAwait(false);
        }
    }
}