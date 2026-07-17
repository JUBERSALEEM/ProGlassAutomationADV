using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
                .OrderBy(g => g.Category)
                .ThenBy(g => g.Color)
                .ThenBy(g => g.ThicknessMm)
                .ToListAsync();
        }

        public async Task<GlassSheet?> GetByIdAsync(int id)
        {
            using var db = new AppDbContext();
            return await db.GlassSheets.FindAsync(id);
        }

        public async Task<GlassSheet> AddAsync(GlassSheet sheet)
        {
            using var db = new AppDbContext();
            sheet.CreatedAt = DateTime.Now;
            sheet.LastUpdated = DateTime.Now;
            if (sheet.Stock > 0)
                sheet.LastPurchaseDate = DateTime.Now;

            db.GlassSheets.Add(sheet);
            await db.SaveChangesAsync();
            return sheet;
        }

        public async Task UpdateAsync(GlassSheet sheet)
        {
            using var db = new AppDbContext();
            sheet.LastUpdated = DateTime.Now;
            db.GlassSheets.Update(sheet);
            await db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var db = new AppDbContext();
            var sheet = await db.GlassSheets.FindAsync(id);
            if (sheet != null)
            {
                db.GlassSheets.Remove(sheet);
                await db.SaveChangesAsync();
            }
        }

        public async Task BuyStockAsync(int sheetId, int quantity, decimal? newPurchasePrice = null)
        {
            if (quantity <= 0) return;

            using var db = new AppDbContext();
            var sheet = await db.GlassSheets.FindAsync(sheetId);
            if (sheet == null) return;

            sheet.Stock += quantity;
            sheet.LastPurchaseDate = DateTime.Now;
            sheet.LastUpdated = DateTime.Now;

            if (newPurchasePrice.HasValue && newPurchasePrice.Value > 0)
                sheet.PurchasePricePerSqm = newPurchasePrice.Value;

            await db.SaveChangesAsync();
        }

        public async Task UseStockAsync(int sheetId, int quantity)
        {
            if (quantity <= 0) return;

            using var db = new AppDbContext();
            var sheet = await db.GlassSheets.FindAsync(sheetId);
            if (sheet == null) return;

            if (sheet.Balance < quantity)
                throw new InvalidOperationException(
                    $"Not enough stock. Available: {sheet.Balance}, Requested: {quantity}");

            sheet.Used += quantity;
            sheet.LastUpdated = DateTime.Now;
            await db.SaveChangesAsync();
        }

        public async Task<List<string>> GetCategoriesAsync()
        {
            using var db = new AppDbContext();
            return await db.GlassSheets
                .Select(g => g.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }

        public async Task<List<string>> GetColorsAsync()
        {
            using var db = new AppDbContext();
            return await db.GlassSheets
                .Select(g => g.Color)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }

        public async Task<List<string>> GetSuppliersAsync()
        {
            using var db = new AppDbContext();
            return await db.GlassSheets
                .Select(g => g.Supplier)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();
        }

        public async Task<List<int>> GetThicknessesAsync()
        {
            using var db = new AppDbContext();
            return await db.GlassSheets
                .Select(g => g.ThicknessMm)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();
        }
    }
}