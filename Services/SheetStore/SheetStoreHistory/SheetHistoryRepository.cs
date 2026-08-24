using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.SheetStore.SheetStoreHistory;
using ProGlassAutomation.Services.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.SheetStore.SheetStoreHistory
{
    public class SheetHistoryRepository : ISheetHistoryRepository
    {
        public async Task<List<SheetHistory>> GetAllAsync()
        {
            using var db = new AppDbContext();
            return await db.SheetHistories
                .OrderByDescending(h => h.Timestamp)
                .ToListAsync();
        }

        public async Task<List<SheetHistory>> GetBySheetIdAsync(int sheetId)
        {
            using var db = new AppDbContext();
            return await db.SheetHistories
                .Where(h => h.SheetId == sheetId)
                .OrderByDescending(h => h.Timestamp)
                .ToListAsync();
        }

        public async Task LogAsync(int sheetId, string sheetDescription, string action, string details,
                                    int quantityChanged = 0, decimal? valueChanged = null)
        {
            using var db = new AppDbContext();
            var entry = new SheetHistory
            {
                SheetId = sheetId,
                SheetDescription = sheetDescription,
                Action = action,
                Details = details,
                QuantityChanged = quantityChanged,
                ValueChanged = valueChanged,
                Timestamp = DateTime.Now,
                UserName = Environment.UserName
            };
            db.SheetHistories.Add(entry);
            await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());
        }

        public async Task<DateTime?> GetLastUpdatedAsync()
        {
            using var db = new AppDbContext();
            if (!await db.SheetHistories.AnyAsync())
                return null;
            return await db.SheetHistories.MaxAsync(h => h.Timestamp);
        }

        public async Task<int> GetTotalCountAsync()
        {
            using var db = new AppDbContext();
            return await db.SheetHistories.CountAsync();
        }
    }
}