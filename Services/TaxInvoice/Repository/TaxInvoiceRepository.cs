using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.TaxInvoice;
using ProGlassAutomation.Services.TaxInvoice.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.TaxInvoice.Repository
{
    public class TaxInvoiceRepository : ITaxInvoiceRepository
    {
        public async Task InitializeAsync()
        {
            using var db = new TaxInvoiceDbContext();
            await db.Database.EnsureCreatedAsync();
        }

        public async Task<Models.TaxInvoice.TaxInvoice?> GetByIdAsync(int id)
        {
            using var db = new TaxInvoiceDbContext();
            return await db.TaxInvoices
                .Include(x => x.Items)
                .Include(x => x.History)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<Models.TaxInvoice.TaxInvoice?> GetByInvoiceNumberAsync(string invoiceNumber)
        {
            using var db = new TaxInvoiceDbContext();
            return await db.TaxInvoices
                .Include(x => x.Items)
                .Include(x => x.History)
                .FirstOrDefaultAsync(x => string.Equals(x.InvoiceNumber, invoiceNumber, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<Models.TaxInvoice.TaxInvoice?> GetByDeliveryNoteIdAsync(string deliveryNoteId)
        {
            using var db = new TaxInvoiceDbContext();
            return await db.TaxInvoices
                .Include(x => x.Items)
                .Include(x => x.History)
                .FirstOrDefaultAsync(x => string.Equals(x.DeliveryNoteId, deliveryNoteId, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<List<Models.TaxInvoice.TaxInvoice>> GetAllAsync()
        {
            using var db = new TaxInvoiceDbContext();
            return await db.TaxInvoices
                .Include(x => x.Items)
                .OrderByDescending(x => x.InvoiceDate)
                .ThenByDescending(x => x.InvoiceNumber)
                .ToListAsync();
        }

        public async Task<int> GetNextSequenceAsync()
        {
            using var db = new TaxInvoiceDbContext();
            bool hasInvoices = await db.TaxInvoices.AnyAsync();
            if (!hasInvoices) return 1;

            // Extract the numerical sequence from existing invoice numbers (e.g. INV-2026-001 -> 1)
            var allNumbers = await db.TaxInvoices
                .Select(x => x.InvoiceNumber)
                .ToListAsync();

            int maxSeq = 0;
            foreach (var num in allNumbers)
            {
                var parts = num.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out int seq))
                {
                    if (seq > maxSeq) maxSeq = seq;
                }
            }

            return maxSeq + 1;
        }

        public Task<string> BuildInvoiceNumberAsync(int sequence)
        {
            int year = DateTime.Today.Year;
            // e.g. INV-2026-001
            return Task.FromResult($"INV-{year}-{sequence:D3}");
        }

        public async Task<Models.TaxInvoice.TaxInvoice> InsertAsync(Models.TaxInvoice.TaxInvoice entity)
        {
            using var db = new TaxInvoiceDbContext();
            entity.CreatedAt = DateTime.Now;
            entity.UpdatedAt = DateTime.Now;

            await db.TaxInvoices.AddAsync(entity);
            await db.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(Models.TaxInvoice.TaxInvoice entity)
        {
            using var db = new TaxInvoiceDbContext();
            entity.UpdatedAt = DateTime.Now;

            // Load existing entity with child rows
            var existing = await db.TaxInvoices
                .Include(x => x.Items)
                .Include(x => x.History)
                .FirstOrDefaultAsync(x => x.Id == entity.Id);

            if (existing == null) return;

            db.Entry(existing).CurrentValues.SetValues(entity);

            // Update Items
            db.TaxInvoiceItems.RemoveRange(existing.Items);
            foreach (var item in entity.Items)
            {
                item.Id = 0; // let DB auto-increment
                existing.Items.Add(item);
            }

            // Update History
            db.TaxInvoiceHistories.RemoveRange(existing.History);
            foreach (var h in entity.History)
            {
                h.Id = 0;
                existing.History.Add(h);
            }

            await db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var db = new TaxInvoiceDbContext();
            var existing = await db.TaxInvoices.FindAsync(id);
            if (existing != null)
            {
                db.TaxInvoices.Remove(existing);
                await db.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsByInvoiceNumberAsync(string invoiceNumber)
        {
            using var db = new TaxInvoiceDbContext();
            return await db.TaxInvoices.AnyAsync(x => string.Equals(x.InvoiceNumber, invoiceNumber, StringComparison.OrdinalIgnoreCase));
        }
    }
}