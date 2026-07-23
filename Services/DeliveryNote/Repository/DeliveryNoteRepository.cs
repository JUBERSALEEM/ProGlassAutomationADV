using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.DeliveryNote.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.DeliveryNote.Repository
{
    /// <summary>
    /// EF Core implementation of IDeliveryNoteRepository.
    /// Each method creates a fresh DbContext (same pattern as JO + PI repos).
    ///
    /// Phase 10.5c: Added GetReturnedAsync() for the Returned tab.
    /// Phase 10.6 : Attachments are now managed independently of parent DN
    ///              save. UpdateAsync no longer wipes attachments — that
    ///              would delete Phase 10.6 uploaded files on every Save.
    /// </summary>
    public class DeliveryNoteRepository : IDeliveryNoteRepository
    {
        // ── Initialize (create DB + tables if not exist) ─────────────────

        public async Task InitializeAsync()
        {
            using var ctx = new DeliveryNotesDbContext();
            await ctx.Database.EnsureCreatedAsync();
        }

        // ── Single DN retrieval ──────────────────────────────────────────

        public async Task<SavedDeliveryNote?> GetByIdAsync(int id)
        {
            using var ctx = new DeliveryNotesDbContext();
            return await ctx.DeliveryNotes
                .Include(x => x.Items)
                .Include(x => x.Attachments)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<SavedDeliveryNote?> GetByDeliveryNoteIdAsync(
            string deliveryNoteId)
        {
            using var ctx = new DeliveryNotesDbContext();
            return await ctx.DeliveryNotes
                .Include(x => x.Items)
                .Include(x => x.Attachments)
                .FirstOrDefaultAsync(x => x.DeliveryNoteId == deliveryNoteId);
        }

        // ── List retrieval ───────────────────────────────────────────────

        public async Task<List<DeliveryNoteListItem>> GetAllAsync()
        {
            using var ctx = new DeliveryNotesDbContext();
            var entities = await ctx.DeliveryNotes
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
            return entities.Select(ToListItem).ToList();
        }

        public async Task<List<DeliveryNoteListItem>> GetByJobOrderIdAsync(
            string jobOrderId)
        {
            using var ctx = new DeliveryNotesDbContext();
            var entities = await ctx.DeliveryNotes
                .Where(x => x.JobOrderId == jobOrderId)
                .OrderBy(x => x.SequenceNumber)
                .ToListAsync();
            return entities.Select(ToListItem).ToList();
        }

        public async Task<List<DeliveryNoteListItem>> GetPreparedAsync()
        {
            using var ctx = new DeliveryNotesDbContext();
            var entities = await ctx.DeliveryNotes
                .Where(x => x.Status == "Draft")
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
            return entities.Select(ToListItem).ToList();
        }

        public async Task<List<DeliveryNoteListItem>> GetDeliveredAsync()
        {
            using var ctx = new DeliveryNotesDbContext();
            var entities = await ctx.DeliveryNotes
                .Where(x => x.Status == "Delivered"
                         || x.Status == "Signed"
                         || x.Status == "Received")
                .OrderByDescending(x => x.DeliveryDate)
                .ToListAsync();
            return entities.Select(ToListItem).ToList();
        }

        public async Task<List<PendingQtySummary>> GetPendingQtyJobsAsync()
        {
            using var ctx = new DeliveryNotesDbContext();

            var groups = await ctx.DeliveryNoteItems
                .GroupBy(i => i.JobOrderId)
                .Select(g => new
                {
                    JobOrderId = g.Key,
                    TotalOrdered = g.Max(i => i.OrderedQty),
                    TotalDelivered = g.Sum(i => i.DeliveredQty),
                    TotalBalance = g.Sum(i => i.BalanceQty)
                })
                .Where(g => g.TotalBalance > 0)
                .ToListAsync();

            var result = new List<PendingQtySummary>();

            foreach (var g in groups)
            {
                var latestDn = await ctx.DeliveryNotes
                    .Where(x => x.JobOrderId == g.JobOrderId)
                    .OrderByDescending(x => x.SequenceNumber)
                    .FirstOrDefaultAsync();

                result.Add(new PendingQtySummary
                {
                    JobOrderId = g.JobOrderId,
                    CustomerName = latestDn?.CustomerName ?? "",
                    ProjectName = latestDn?.ProjectName ?? "",
                    TotalOrdered = g.TotalOrdered,
                    TotalDelivered = g.TotalDelivered,
                    TotalBalance = g.TotalBalance,
                    LastDnId = latestDn?.DeliveryNoteId ?? ""
                });
            }

            return result.OrderByDescending(x => x.TotalBalance).ToList();
        }

        public async Task<List<DeliveryNoteListItem>> GetReworkAsync()
        {
            using var ctx = new DeliveryNotesDbContext();
            var entities = await ctx.DeliveryNotes
                .Where(x => x.DeliveryType == "Rework")
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
            return entities.Select(ToListItem).ToList();
        }

        public async Task<List<DeliveryNoteListItem>> GetReturnedAsync()
        {
            using var ctx = new DeliveryNotesDbContext();
            var entities = await ctx.DeliveryNotes
                .Where(x => x.DeliveryType == "Return")
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
            return entities.Select(ToListItem).ToList();
        }

        // ── Delivery history ─────────────────────────────────────────────

        public async Task<DeliveryHistorySummary> GetDeliveryHistoryAsync(
            string jobOrderId)
        {
            using var ctx = new DeliveryNotesDbContext();

            var items = await ctx.DeliveryNoteItems
                .Where(i => i.JobOrderId == jobOrderId)
                .ToListAsync();

            var dns = await ctx.DeliveryNotes
                .Where(x => x.JobOrderId == jobOrderId)
                .OrderBy(x => x.SequenceNumber)
                .ToListAsync();

            var summary = new DeliveryHistorySummary
            {
                JobOrderId = jobOrderId,
                TotalOrdered = items.GroupBy(i => new { i.SpecGroupNumber, i.SerialNumber })
                                      .Sum(g => g.Max(i => i.OrderedQty)),
                TotalDelivered = items.Sum(i => i.DeliveredQty),
                TotalBalance = items.Sum(i => i.BalanceQty),
                TotalRework = items.Sum(i => i.ReworkQty),
                TotalReturned = items.Sum(i => i.ReturnedQty)
            };

            foreach (var dn in dns)
            {
                var dnItems = items.Where(i => i.DeliveryNoteId == dn.DeliveryNoteId);
                summary.Entries.Add(new DeliveryHistoryEntry
                {
                    DeliveryNoteId = dn.DeliveryNoteId,
                    DeliveryType = dn.DeliveryType,
                    Status = dn.Status,
                    DeliveredQty = dnItems.Sum(i => i.DeliveredQty),
                    BalanceQty = dn.TotalBalanceQty,
                    DeliveryDate = dn.DeliveryDate,
                    DriverName = dn.DriverName
                });
            }

            return summary;
        }

        public async Task<int> GetPreviouslyDeliveredQtyAsync(
            string jobOrderId,
            int specGroupNumber,
            int serialNumber)
        {
            using var ctx = new DeliveryNotesDbContext();

            var confirmedDnIds = await ctx.DeliveryNotes
                .Where(x => x.JobOrderId == jobOrderId
                         && x.Status != "Draft")
                .Select(x => x.DeliveryNoteId)
                .ToListAsync();

            return await ctx.DeliveryNoteItems
                .Where(i => confirmedDnIds.Contains(i.DeliveryNoteId)
                         && i.JobOrderId == jobOrderId
                         && i.SpecGroupNumber == specGroupNumber
                         && i.SerialNumber == serialNumber)
                .SumAsync(i => i.DeliveredQty);
        }

        // ── Numbering ────────────────────────────────────────────────────

        public async Task<int> GetNextSequenceAsync(string jobOrderId)
        {
            using var ctx = new DeliveryNotesDbContext();
            var max = await ctx.DeliveryNotes
                .Where(x => x.JobOrderId == jobOrderId)
                .MaxAsync(x => (int?)x.SequenceNumber) ?? 0;
            return max + 1;
        }

        public async Task<string> BuildDeliveryNoteIdAsync(
            string jobOrderId, int sequence)
        {
            string core = jobOrderId.StartsWith("JOB-", StringComparison.OrdinalIgnoreCase)
                ? jobOrderId.Substring(4)
                : jobOrderId;

            return $"DN-{core}-{sequence:D2}";
        }

        // ── CRUD ─────────────────────────────────────────────────────────

        public async Task<SavedDeliveryNote> InsertAsync(SavedDeliveryNote entity)
        {
            using var ctx = new DeliveryNotesDbContext();
            entity.CreatedAt = DateTime.Now;
            entity.UpdatedAt = DateTime.Now;
            ctx.DeliveryNotes.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(SavedDeliveryNote entity)
        {
            using var ctx = new DeliveryNotesDbContext();
            entity.UpdatedAt = DateTime.Now;

            // Items: wipe + re-add strategy (matches JO repo pattern —
            // avoids EF tracking conflicts on child collection).
            var existingItems = await ctx.DeliveryNoteItems
                .Where(i => i.DeliveryNoteId == entity.DeliveryNoteId)
                .ToListAsync();
            ctx.DeliveryNoteItems.RemoveRange(existingItems);

            // Phase 10.6 — Attachments are managed independently via
            // AddAttachmentAsync / DeleteAttachmentAsync. Do NOT wipe them
            // on regular Save — that would delete uploaded files on every
            // Save click. Detach the entity's Attachments navigation from
            // tracking so EF doesn't try to persist stale rows.
            entity.Attachments.Clear();

            ctx.DeliveryNotes.Update(entity);
            await ctx.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var ctx = new DeliveryNotesDbContext();
            var entity = await ctx.DeliveryNotes
                .Include(x => x.Items)
                .Include(x => x.Attachments)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity == null) return;

            // EF cascade handles Items + Attachments deletion
            ctx.DeliveryNotes.Remove(entity);
            await ctx.SaveChangesAsync();
        }

        // ── Existence checks ─────────────────────────────────────────────

        public async Task<bool> ExistsByDeliveryNoteIdAsync(string deliveryNoteId)
        {
            using var ctx = new DeliveryNotesDbContext();
            return await ctx.DeliveryNotes
                .AnyAsync(x => x.DeliveryNoteId == deliveryNoteId);
        }

        public async Task<bool> HasAnyDeliveryAsync(string jobOrderId)
        {
            using var ctx = new DeliveryNotesDbContext();
            return await ctx.DeliveryNotes
                .AnyAsync(x => x.JobOrderId == jobOrderId);
        }

        // ── Attachments (Phase 10.6) ─────────────────────────────────────

        public async Task<List<DeliveryNoteAttachment>> GetAttachmentsAsync(
            string deliveryNoteId)
        {
            using var ctx = new DeliveryNotesDbContext();
            return await ctx.DeliveryNoteAttachments
                .Where(a => a.DeliveryNoteId == deliveryNoteId)
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync();
        }

        public async Task<DeliveryNoteAttachment> AddAttachmentAsync(
            DeliveryNoteAttachment entity)
        {
            using var ctx = new DeliveryNotesDbContext();
            entity.UploadedAt = DateTime.Now;
            ctx.DeliveryNoteAttachments.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task DeleteAttachmentAsync(int attachmentId)
        {
            using var ctx = new DeliveryNotesDbContext();
            var entity = await ctx.DeliveryNoteAttachments
                .FirstOrDefaultAsync(a => a.Id == attachmentId);
            if (entity == null) return;

            ctx.DeliveryNoteAttachments.Remove(entity);
            await ctx.SaveChangesAsync();
        }

        // ── Private helpers ──────────────────────────────────────────────

        private static DeliveryNoteListItem ToListItem(SavedDeliveryNote e) =>
            new()
            {
                Id = e.Id,
                DeliveryNoteId = e.DeliveryNoteId,
                JobOrderId = e.JobOrderId,
                ProformaId = e.ProformaId,
                CustomerName = e.CustomerName,
                ProjectName = e.ProjectName,
                Status = e.Status,
                DeliveryType = e.DeliveryType,
                IsFoc = e.IsFoc,
                DriverName = e.DriverName,
                VehicleNumber = e.VehicleNumber,
                TotalOrderedQty = e.TotalOrderedQty,
                TotalDeliveredQty = e.TotalDeliveredQty,
                TotalBalanceQty = e.TotalBalanceQty,
                SequenceNumber = e.SequenceNumber,
                DeliveryDate = e.DeliveryDate,
                DeliveryTime = e.DeliveryTime,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt
            };
    }
}