using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.TaxInvoice.Attachment;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Models.TaxInvoice.TaxInvoiceFamily;
using ProGlassAutomation.Services.TaxInvoice.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProGlassAutomation.Services.Audit;

namespace ProGlassAutomation.Services.TaxInvoice.Repository
{
    /// <summary>
    /// EF Core implementation of ITaxInvoiceRepository.
    /// Each method creates a fresh DbContext (same pattern as DN + JO + PI repos).
    ///
    /// NUMBERING LOGIC:
    ///   PI-2026-001  → strip "PI-" prefix → "2026-001"
    ///   Full invoice : INV-2026-001
    ///   Partial A    : INV-2026-001-A
    ///   Partial B    : INV-2026-001-B
    ///
    /// FAMILY MATH:
    ///   Ordered   = ProformaTotal (set from PI snapshot — 0 if PI not found)
    ///   Invoiced  = SUM GrandTotal of Issued + Paid + PartiallyPaid members
    ///   Balance   = MAX(0, Ordered − Invoiced)
    ///
    /// UPDATE PATTERN:
    ///   Items are delete-then-reinsert (same as DN repo).
    ///   Attachments are NOT cleared on update — managed independently.
    /// </summary>
    public class TaxInvoiceRepository : ITaxInvoiceRepository
    {
        public async Task InitializeAsync()
        {
            using var ctx = new TaxInvoiceDbContext();
            await ctx.Database.EnsureCreatedAsync();
        }

        // ── Single invoice retrieval ──────────────────────────────────────

        public async Task<SavedTaxInvoice?> GetByIdAsync(int id)
        {
            using var ctx = new TaxInvoiceDbContext();
            return await ctx.TaxInvoices
                .Include(x => x.Items)
                .Include(x => x.Attachments)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<SavedTaxInvoice?> GetByInvoiceNumberAsync(
            string invoiceNumber)
        {
            using var ctx = new TaxInvoiceDbContext();
            return await ctx.TaxInvoices
                .Include(x => x.Items)
                .Include(x => x.Attachments)
                .FirstOrDefaultAsync(x => x.InvoiceNumber == invoiceNumber);
        }

        // ── List retrieval ────────────────────────────────────────────────

        public async Task<List<TaxInvoiceListItem>> GetAllAsync()
        {
            using var ctx = new TaxInvoiceDbContext();
            var entities = await ctx.TaxInvoices
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
            return entities.Select(ToListItem).ToList();
        }

        public async Task<List<TaxInvoiceListItem>> GetByProformaIdAsync(
            string proformaId)
        {
            using var ctx = new TaxInvoiceDbContext();
            var entities = await ctx.TaxInvoices
                .Where(x => x.ProformaId == proformaId)
                .OrderBy(x => x.SequenceNumber)
                .ToListAsync();
            return entities.Select(ToListItem).ToList();
        }

        public async Task<List<TaxInvoiceListItem>> GetByStatusAsync(
            string status)
        {
            using var ctx = new TaxInvoiceDbContext();
            var entities = await ctx.TaxInvoices
                .Where(x => x.Status == status)
                .OrderByDescending(x => x.IssueDate)
                .ToListAsync();
            return entities.Select(ToListItem).ToList();
        }

        public async Task<List<TaxInvoiceListItem>> GetDraftAsync()
            => await GetByStatusAsync("Draft");

        public async Task<List<TaxInvoiceListItem>> GetIssuedAsync()
            => await GetByStatusAsync("Issued");

        public async Task<List<TaxInvoiceListItem>> GetPaidAsync()
            => await GetByStatusAsync("Paid");

        public async Task<List<TaxInvoiceListItem>> GetOverdueAsync()
        {
            using var ctx = new TaxInvoiceDbContext();
            var entities = await ctx.TaxInvoices
                .Where(x => x.Status == "Overdue"
                         || (x.Status == "Issued"
                             && x.DueDate < DateTime.Today))
                .OrderBy(x => x.DueDate)
                .ToListAsync();
            return entities.Select(ToListItem).ToList();
        }

        // ── Family retrieval ──────────────────────────────────────────────

        public async Task<List<TaxInvoiceFamily>> GetAllFamiliesAsync()
        {
            using var ctx = new TaxInvoiceDbContext();
            var entities = await ctx.TaxInvoices
                .OrderByDescending(x => x.IssueDate)
                .ToListAsync();

            var items = entities.Select(ToListItem).ToList();
            var families = TaxInvoiceFamily.BuildFrom(items).ToList();
            return families;
        }

        public async Task<TaxInvoiceFamily?> GetFamilyAsync(string proformaId)
        {
            using var ctx = new TaxInvoiceDbContext();
            var entities = await ctx.TaxInvoices
                .Where(x => x.ProformaId == proformaId)
                .OrderBy(x => x.SequenceNumber)
                .ToListAsync();

            if (entities.Count == 0) return null;

            var items = entities.Select(ToListItem).ToList();
            var families = TaxInvoiceFamily.BuildFrom(items);
            return families.FirstOrDefault();
        }

        // ── Financial summary ─────────────────────────────────────────────

        public async Task<InvoiceFamilySummary> GetFamilySummaryAsync(
            string proformaId)
        {
            using var ctx = new TaxInvoiceDbContext();

            var members = await ctx.TaxInvoices
                .Where(x => x.ProformaId == proformaId)
                .ToListAsync();

            if (members.Count == 0)
                return new InvoiceFamilySummary { ProformaId = proformaId };

            // Only issued/paid count toward InvoicedTotal
            var issuedStatuses = new[] { "Issued", "Paid", "PartiallyPaid" };
            var issuedMembers = members
                .Where(m => issuedStatuses.Contains(m.Status,
                    StringComparer.OrdinalIgnoreCase))
                .ToList();

            return new InvoiceFamilySummary
            {
                ProformaId = proformaId,
                InvoiceCount = members.Count,
                TotalInvoiced = issuedMembers.Sum(m => m.GrandTotal),
                TotalVat = issuedMembers.Sum(m => m.VatAmount),
                TotalGrandTotal = members.Sum(m => m.GrandTotal),
                ProformaTotal = 0m // caller must populate from PI snapshot
            };
        }

        // ── Numbering ─────────────────────────────────────────────────────

        public async Task<int> GetNextSequenceNumberAsync(string proformaId)
        {
            using var ctx = new TaxInvoiceDbContext();
            var max = await ctx.TaxInvoices
                .Where(x => x.ProformaId == proformaId)
                .MaxAsync(x => (int?)x.SequenceNumber) ?? 0;
            return max + 1;
        }

        public async Task<string> GetNextSequenceLetterAsync(string proformaId)
        {
            using var ctx = new TaxInvoiceDbContext();

            var letters = await ctx.TaxInvoices
                .Where(x => x.ProformaId == proformaId
                         && x.SequenceLetter != null
                         && x.SequenceLetter != "")
                .Select(x => x.SequenceLetter)
                .ToListAsync();

            if (letters.Count == 0) return "A";

            var validLetters = letters
                .Where(l => l.Length == 1
                         && l[0] >= 'A'
                         && l[0] <= 'Z')
                .OrderBy(l => l)
                .ToList();

            if (validLetters.Count == 0) return "A";

            char last = validLetters.Last()[0];
            return last < 'Z'
                ? ((char)(last + 1)).ToString()
                : "A"; // safety fallback
        }

        public async Task<string> BuildInvoiceNumberAsync(
            string proformaId, string sequenceLetter = "")
        {
            // Strip prefix: "PI-2026-001" → "2026-001"
            string core = proformaId.StartsWith("PI-",
                StringComparison.OrdinalIgnoreCase)
                ? proformaId.Substring(3)
                : proformaId;

            string baseNumber = $"INV-{core}";

            return string.IsNullOrWhiteSpace(sequenceLetter)
                ? baseNumber
                : $"{baseNumber}-{sequenceLetter.ToUpperInvariant().Trim()}";
        }

        // ── CRUD ──────────────────────────────────────────────────────────

        public async Task<SavedTaxInvoice> InsertAsync(SavedTaxInvoice entity)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.TaxInvoiceEdit, "create a tax invoice");

            using var ctx = new TaxInvoiceDbContext();
            entity.CreatedAt = DateTime.Now;
            entity.UpdatedAt = DateTime.Now;
            ctx.TaxInvoices.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(SavedTaxInvoice entity)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.TaxInvoiceEdit, "update a tax invoice");

            using var ctx = new TaxInvoiceDbContext();
            entity.UpdatedAt = DateTime.Now;

            // Delete-then-reinsert items (same pattern as DN repo)
            var existingItems = await ctx.TaxInvoiceItems
                .Where(i => i.InvoiceNumber == entity.InvoiceNumber)
                .ToListAsync();
            ctx.TaxInvoiceItems.RemoveRange(existingItems);

            // Attachments managed independently — do NOT clear on update
            entity.Attachments.Clear();

            ctx.TaxInvoices.Update(entity);
            await ctx.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.TaxInvoiceEdit, "delete a tax invoice");

            using var ctx = new TaxInvoiceDbContext();
            var entity = await ctx.TaxInvoices
                .Include(x => x.Items)
                .Include(x => x.Attachments)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity == null) return;

            ctx.TaxInvoices.Remove(entity);
            await ctx.SaveChangesAsync();
        }

        // ── Existence checks ──────────────────────────────────────────────

        public async Task<bool> ExistsByInvoiceNumberAsync(string invoiceNumber)
        {
            using var ctx = new TaxInvoiceDbContext();
            return await ctx.TaxInvoices
                .AnyAsync(x => x.InvoiceNumber == invoiceNumber);
        }

        public async Task<bool> HasAnyInvoiceAsync(string proformaId)
        {
            using var ctx = new TaxInvoiceDbContext();
            return await ctx.TaxInvoices
                .AnyAsync(x => x.ProformaId == proformaId);
        }

        // ── Status management ─────────────────────────────────────────────

        public async Task UpdateStatusAsync(
            string invoiceNumber, string newStatus)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.TaxInvoiceEdit, "change a tax invoice status");

            using var ctx = new TaxInvoiceDbContext();
            var entity = await ctx.TaxInvoices
                .FirstOrDefaultAsync(x => x.InvoiceNumber == invoiceNumber);

            if (entity == null) return;

            entity.Status = newStatus;
            entity.UpdatedAt = DateTime.Now;
            await ctx.SaveChangesAsync();
        }

        // ── Attachments ───────────────────────────────────────────────────

        public async Task<List<TaxInvoiceAttachment>> GetAttachmentsAsync(
            string invoiceNumber)
        {
            using var ctx = new TaxInvoiceDbContext();
            return await ctx.TaxInvoiceAttachments
                .Where(a => a.InvoiceNumber == invoiceNumber)
                // Statement PDF order: SignedPI → SignedDN → Other
                .OrderBy(a => a.AttachmentType == "SignedPI" ? 0
                            : a.AttachmentType == "SignedDN" ? 1 : 2)
                .ThenByDescending(a => a.UploadedAt)
                .ToListAsync();
        }

        public async Task<TaxInvoiceAttachment> AddAttachmentAsync(
            TaxInvoiceAttachment entity)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.TaxInvoiceEdit, "add a tax invoice attachment");

            using var ctx = new TaxInvoiceDbContext();
            entity.UploadedAt = DateTime.Now;
            ctx.TaxInvoiceAttachments.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task DeleteAttachmentAsync(int attachmentId)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.TaxInvoiceEdit, "delete a tax invoice attachment");

            using var ctx = new TaxInvoiceDbContext();
            var entity = await ctx.TaxInvoiceAttachments
                .FirstOrDefaultAsync(a => a.Id == attachmentId);
            if (entity == null) return;

            ctx.TaxInvoiceAttachments.Remove(entity);
            await ctx.SaveChangesAsync();
        }

        public async Task<int> GetAttachmentCountAsync(string invoiceNumber)
        {
            using var ctx = new TaxInvoiceDbContext();
            return await ctx.TaxInvoiceAttachments
                .CountAsync(a => a.InvoiceNumber == invoiceNumber);
        }

        public async Task<Dictionary<string, int>> GetAttachmentCountsAsync(
            IEnumerable<string> invoiceNumbers)
        {
            var idList = invoiceNumbers.ToList();
            if (idList.Count == 0)
                return new Dictionary<string, int>();

            using var ctx = new TaxInvoiceDbContext();

            var raw = await ctx.TaxInvoiceAttachments
                .Where(a => idList.Contains(a.InvoiceNumber))
                .GroupBy(a => a.InvoiceNumber)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync();

            var dict = raw.ToDictionary(x => x.Id, x => x.Count);

            // Ensure all requested IDs are present (even with 0 count)
            foreach (var id in idList)
                if (!dict.ContainsKey(id))
                    dict[id] = 0;

            return dict;
        }

        // ── ANALYTICS PROJECTION ─────────────────────────────────────────

        public async Task<List<TaxInvoiceAnalyticsRow>> GetAnalyticsRowsAsync()
        {
            using var ctx = new TaxInvoiceDbContext();
            return await ctx.TaxInvoices
                .AsNoTracking()
                .Select(t => new TaxInvoiceAnalyticsRow
                {
                    InvoiceNumber = t.InvoiceNumber,
                    Status = t.Status,
                    DueDate = t.DueDate,
                    GrandTotal = t.GrandTotal
                })
                .ToListAsync();
        }

        // ── Private helpers ───────────────────────────────────────────────

        private static TaxInvoiceListItem ToListItem(SavedTaxInvoice e) =>
            new()
            {
                Id = e.Id,
                InvoiceNumber = e.InvoiceNumber,
                ProformaId = e.ProformaId,
                DeliveryNoteReference = e.DeliveryNoteReference,
                JobOrderId = e.JobOrderId,
                PurchaseOrderNumber = e.PurchaseOrderNumber,
                InvoiceType = e.InvoiceType,
                SequenceLetter = e.SequenceLetter,
                SequenceNumber = e.SequenceNumber,
                Status = e.Status,
                CustomerName = e.CustomerName,
                ProjectName = e.ProjectName,
                CompanyName = e.CompanyName,
                PaymentTerms = e.PaymentTerms,
                IssueDate = e.IssueDate,
                DueDate = e.DueDate,
                Subtotal = e.Subtotal,
                DiscountAmount = e.DiscountAmount,
                AfterDiscount = e.AfterDiscount,
                VatAmount = e.VatAmount,
                GrandTotal = e.GrandTotal,
                TotalQty = e.TotalQty,
                TotalSqm = e.TotalSqm,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt
            };
    }
}