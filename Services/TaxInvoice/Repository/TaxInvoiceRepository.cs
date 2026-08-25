using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.TaxInvoice.Attachment;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Models.TaxInvoice.TaxInvoiceFamily;
using ProGlassAutomation.Services.Database.SchemaMigrations;
using ProGlassAutomation.Services.TaxInvoice.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.ProformaInvoice;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services.Logging;

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

            // Additive schema upgrades for an existing taxinvoices.db.
            SchemaMigrationRunner.Upgrade(
                TaxInvoiceDbContext.DatabaseFilePath,
                AppSchemaMigrations.TaxInvoices,
                "Tax Invoice");
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
            // PERF: project straight to the list item at the SQL level so the
            // FullSnapshotJson blob column (which carries base64 attachments)
            // is never read. Behaviourally identical to materialising the
            // entity then calling ToListItem.
            return await ctx.TaxInvoices
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new TaxInvoiceListItem
                {
                    Id = x.Id,
                    InvoiceNumber = x.InvoiceNumber,
                    ProformaId = x.ProformaId,
                    DeliveryNoteReference = x.DeliveryNoteReference,
                    JobOrderId = x.JobOrderId,
                    PurchaseOrderNumber = x.PurchaseOrderNumber,
                    InvoiceType = x.InvoiceType,
                    SequenceLetter = x.SequenceLetter,
                    SequenceNumber = x.SequenceNumber,
                    Status = x.Status,
                    CustomerName = x.CustomerName,
                    ProjectName = x.ProjectName,
                    CompanyName = x.CompanyName,
                    PaymentTerms = x.PaymentTerms,
                    IssueDate = x.IssueDate,
                    DueDate = x.DueDate,
                    Subtotal = x.Subtotal,
                    DiscountAmount = x.DiscountAmount,
                    AfterDiscount = x.AfterDiscount,
                    VatAmount = x.VatAmount,
                    GrandTotal = x.GrandTotal,
                    TotalQty = x.TotalQty,
                    TotalSqm = x.TotalSqm,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync();
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
            await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());
            DocumentLifecycle.Raise();
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
            await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());
            DocumentLifecycle.Raise();
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

            string invoiceNumber = entity.InvoiceNumber;
            string? proformaId = entity.ProformaId;

            ctx.TaxInvoices.Remove(entity);
            await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());

            await ClearLinkedDeliveryNotesAsync(invoiceNumber);

            // Run the sync BEFORE raising the change signal so PI list
            // refreshes see the final (cancelled) status the first time.
            await ProformaLifecycleSync.AfterDownstreamChangeAsync(proformaId);
            DocumentLifecycle.Raise();
        }

        /// <summary>
        /// DNs keep a copy of the TI number for list badges. When the
        /// invoice is gone those flags must be cleared or the DN still
        /// looks invoiced and the PI list stays on Fully invoiced.
        /// </summary>
        private static async Task ClearLinkedDeliveryNotesAsync(string? invoiceNumber)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber) || App.DeliveryNotes == null)
                return;

            try
            {
                await App.DeliveryNotes.ClearInvoicedByTaxInvoiceAsync(invoiceNumber);
            }
            catch (System.Exception ex)
            {
                // TI is already gone. A leftover DN badge is repaired
                // the next time this invoice number is written or the
                // user re-saves the delivery note.
                ProGlassAutomation.Services.Logging.AppLogService.Debug("[TaxInvoiceRepository] Linked-DN badge cleanup failed: " + ex.Message);
            }
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
            await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());
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
            await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());
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
            await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());
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

        // ═══════════════════════════════════════════════════════════════
        //  PHASE 2 — Result Pattern with CorrelationId
        // ═══════════════════════════════════════════════════════════════

        public async Task<Result<SavedTaxInvoice>> InsertWithResultAsync(SavedTaxInvoice entity, string? correlationId = null)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.TaxInvoiceEdit, "create a tax invoice (Result variant)");

            var corrId = correlationId ?? CorrelationContext.NewIdFor("TaxInvoice", entity?.InvoiceNumber);
            using var scope = CorrelationContext.BeginDocumentScope("TaxInvoice", entity?.InvoiceNumber ?? "", corrId);

            try
            {
                var result = await InsertAsync(entity!);
                AppLogService.Info(
                    $"TI inserted: {entity!.InvoiceNumber} for DN {entity.DeliveryNoteReference}",
                    new LogContext
                    {
                        Module = "TaxInvoice",
                        Operation = "Insert",
                        DocumentId = entity.InvoiceNumber,
                        DocumentType = "TaxInvoice",
                        CorrelationId = corrId
                    });
                return Result<SavedTaxInvoice>.Ok(result, corrId);
            }
            catch (Exception ex)
            {
                var errorId = AppLogService.Error(
                    $"Failed to insert TI {entity?.InvoiceNumber}: {ex.Message}",
                    ex,
                    new LogContext
                    {
                        Module = "TaxInvoice",
                        Operation = "Insert",
                        DocumentId = entity?.InvoiceNumber ?? "",
                        DocumentType = "TaxInvoice",
                        CorrelationId = corrId
                    });
                return Result<SavedTaxInvoice>.Fail($"Could not save tax invoice {entity?.InvoiceNumber}: {ex.Message}", ex, corrId, errorId);
            }
        }

        public async Task<Result> DeleteWithResultAsync(int id, string? invoiceNumber = null, string? correlationId = null)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.TaxInvoiceEdit, "delete a tax invoice (Result variant)");

            var corrId = correlationId ?? CorrelationContext.NewIdFor("TaxInvoice", invoiceNumber ?? id.ToString());
            using var scope = CorrelationContext.BeginDocumentScope("TaxInvoice", invoiceNumber ?? id.ToString(), corrId);

            try
            {
                await DeleteAsync(id);
                AppLogService.Info(
                    $"TI deleted: Id={id}, InvoiceNumber={invoiceNumber}",
                    new LogContext
                    {
                        Module = "TaxInvoice",
                        Operation = "Delete",
                        DocumentId = invoiceNumber ?? id.ToString(),
                        DocumentType = "TaxInvoice",
                        CorrelationId = corrId
                    });
                return Result.Ok(corrId);
            }
            catch (Exception ex)
            {
                var errorId = AppLogService.Error(
                    $"Failed to delete TI Id={id}: {ex.Message}",
                    ex,
                    new LogContext
                    {
                        Module = "TaxInvoice",
                        Operation = "Delete",
                        DocumentId = invoiceNumber ?? id.ToString(),
                        DocumentType = "TaxInvoice",
                        CorrelationId = corrId
                    });
                return Result.Fail($"Could not delete tax invoice: {ex.Message}", ex, corrId, errorId);
            }
        }
    }
}