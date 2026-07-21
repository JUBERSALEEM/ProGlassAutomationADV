using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository
{
    public class ProformaInvoiceRepository : IProformaInvoiceRepository
    {
        public async Task InitializeAsync()
        {
            using var db = new ProformaInvoicesDbContext();
            await db.Database.EnsureCreatedAsync();
        }

        public async Task<bool> ExistsByProformaIdAsync(string proformaId)
        {
            if (string.IsNullOrWhiteSpace(proformaId)) return false;

            using var db = new ProformaInvoicesDbContext();
            return await db.ProformaInvoices.AnyAsync(x => x.ProformaId == proformaId);
        }

        /// <summary>
        /// Suggests the next available ProformaId by auto-incrementing the trailing number.
        /// Examples:
        ///   "PI-2026-001" → "PI-2026-002" (or 003, 004... if those also exist)
        ///   "INVOICE-42"  → "INVOICE-43"
        ///   "ABC"         → "ABC-001"  (no digits found → append -001)
        /// </summary>
        public async Task<string> SuggestNextIdAsync(string currentId)
        {
            if (string.IsNullOrWhiteSpace(currentId))
                currentId = "PI-000";

            // Find trailing digits (with optional dash separator)
            var match = Regex.Match(currentId, @"^(.*?)(\d+)$");

            string prefix;
            int currentNumber;
            int padWidth;

            if (match.Success)
            {
                prefix = match.Groups[1].Value;
                currentNumber = int.Parse(match.Groups[2].Value);
                padWidth = match.Groups[2].Value.Length;
            }
            else
            {
                prefix = currentId + "-";
                currentNumber = 0;
                padWidth = 3;
            }

            using var db = new ProformaInvoicesDbContext();

            // Try incrementing until we find an unused ID (up to 10000 attempts safety cap)
            for (int i = 1; i <= 10000; i++)
            {
                var candidate = $"{prefix}{(currentNumber + i).ToString().PadLeft(padWidth, '0')}";
                bool exists = await db.ProformaInvoices.AnyAsync(x => x.ProformaId == candidate);
                if (!exists) return candidate;
            }

            // Fallback — extremely unlikely
            return $"{prefix}{DateTime.Now:yyyyMMddHHmmss}";
        }

        public async Task<int> InsertAsync(SavedProformaInvoice invoice)
        {
            if (invoice == null) throw new ArgumentNullException(nameof(invoice));

            using var db = new ProformaInvoicesDbContext();

            // Guard against duplicate ProformaId
            bool exists = await db.ProformaInvoices.AnyAsync(x => x.ProformaId == invoice.ProformaId);
            if (exists)
                throw new InvalidOperationException(
                    $"An invoice with ProformaId '{invoice.ProformaId}' already exists. Use UpdateAsync or generate a new ID.");

            invoice.CreatedAt = DateTime.Now;
            invoice.UpdatedAt = DateTime.Now;

            db.ProformaInvoices.Add(invoice);
            await db.SaveChangesAsync();

            return invoice.Id;
        }

        public async Task UpdateAsync(SavedProformaInvoice invoice)
        {
            if (invoice == null) throw new ArgumentNullException(nameof(invoice));

            using var db = new ProformaInvoicesDbContext();

            var existing = await db.ProformaInvoices
                .FirstOrDefaultAsync(x => x.ProformaId == invoice.ProformaId);

            if (existing == null)
            {
                // Doesn't exist — treat as insert
                await InsertAsync(invoice);
                return;
            }

            // Copy fields onto tracked entity
            existing.CompanyName = invoice.CompanyName;
            existing.CustomerName = invoice.CustomerName;
            existing.ProjectName = invoice.ProjectName;
            existing.Status = invoice.Status;
            existing.IssueDate = invoice.IssueDate;
            existing.ValidUntil = invoice.ValidUntil;
            existing.GlassMaterialsSubtotal = invoice.GlassMaterialsSubtotal;
            existing.OtherChargesSubtotal = invoice.OtherChargesSubtotal;
            existing.DiscountAmount = invoice.DiscountAmount;
            existing.VatAmount = invoice.VatAmount;
            existing.GrandTotalVatInclusive = invoice.GrandTotalVatInclusive;
            existing.FullSnapshotJson = invoice.FullSnapshotJson;
            existing.UpdatedAt = DateTime.Now;

            await db.SaveChangesAsync();
        }

        public async Task<SavedProformaInvoice> GetByIdAsync(int id)
        {
            using var db = new ProformaInvoicesDbContext();
            return await db.ProformaInvoices.FindAsync(id);
        }

        public async Task<SavedProformaInvoice> GetByProformaIdAsync(string proformaId)
        {
            if (string.IsNullOrWhiteSpace(proformaId)) return null;

            using var db = new ProformaInvoicesDbContext();
            return await db.ProformaInvoices
                .FirstOrDefaultAsync(x => x.ProformaId == proformaId);
        }

        public async Task<List<SavedProformaInvoice>> GetAllAsync()
        {
            using var db = new ProformaInvoicesDbContext();
            return await db.ProformaInvoices
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var db = new ProformaInvoicesDbContext();
            var entity = await db.ProformaInvoices.FindAsync(id);
            if (entity == null) return;

            db.ProformaInvoices.Remove(entity);
            await db.SaveChangesAsync();
        }
    }
}