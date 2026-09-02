using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.Database;
using System;
using System.Collections.Generic;
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

            query = (sortKey ?? "").ToLowerInvariant() switch
            {
                "newest" => query.OrderByDescending(g => g.CreatedAt),
                "oldest" => query.OrderBy(g => g.CreatedAt),
                "stockasc" => query.OrderBy(g => g.Stock),
                "stockdesc" => query.OrderByDescending(g => g.Stock),
                "valueasc" => query.OrderBy(g => g.TotalPurchaseValue),
                "valuedesc" => query.OrderByDescending(g => g.TotalPurchaseValue),
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
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.SheetStoreCreate, "add a glass sheet");

            using var db = new AppDbContext();
            sheet.CreatedAt = DateTime.Now;
            sheet.LastUpdated = DateTime.Now;
            if (sheet.Stock > 0)
                sheet.LastPurchaseDate = DateTime.Now;

            db.GlassSheets.Add(sheet);
            await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());
            return sheet;
        }

        public async Task UpdateAsync(GlassSheet sheet)
        {
            ReadOnlyGate.Demand("update a glass sheet");
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.SheetStoreEdit, "update a glass sheet");

            using var db = new AppDbContext();
            sheet.LastUpdated = DateTime.Now;
            db.GlassSheets.Update(sheet);
            await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());
        }

        public async Task DeleteAsync(int id)
        {
            ReadOnlyGate.Demand("delete a glass sheet");
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.SheetStoreDelete, "delete a glass sheet");

            using var db = new AppDbContext();
            var sheet = await db.GlassSheets.FindAsync(id).ConfigureAwait(false);
            if (sheet != null)
            {
                db.GlassSheets.Remove(sheet);
                await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());
            }
        }

        public async Task BuyStockAsync(int sheetId, int quantity, decimal? newPurchasePrice = null)
        {
            ReadOnlyGate.Demand("record a stock purchase");
            if (quantity <= 0) return;

            using var db = new AppDbContext();
            var sheet = await db.GlassSheets.FindAsync(sheetId).ConfigureAwait(false);
            if (sheet == null) return;

            sheet.Stock += quantity;
            sheet.LastPurchaseDate = DateTime.Now;
            sheet.LastUpdated = DateTime.Now;

            if (newPurchasePrice.HasValue && newPurchasePrice.Value > 0)
                sheet.PurchasePricePerSqm = newPurchasePrice.Value;

            await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());
        }

        public async Task UseStockAsync(int sheetId, int quantity)
        {
            ReadOnlyGate.Demand("record stock usage");
            if (quantity <= 0) return;

            using var db = new AppDbContext();
            var sheet = await db.GlassSheets.FindAsync(sheetId).ConfigureAwait(false);
            if (sheet == null) return;

            if (sheet.Balance < quantity)
                throw new InvalidOperationException(
                    $"Not enough stock. Available: {sheet.Balance}, Requested: {quantity}");

            sheet.Used += quantity;
            sheet.LastUpdated = DateTime.Now;
            await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());
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