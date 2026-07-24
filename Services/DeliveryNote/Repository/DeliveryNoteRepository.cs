using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.DeliveryNote.Database;
using ProGlassAutomation.Utilities;
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
    /// Phase 10.6 : Attachments managed independently of parent DN save.
    /// Phase 10.6b: Added GetAttachmentCountAsync + batch variant.
    /// Phase 10.7 : Fixed Balance double-count bug (Gotcha #40).
    ///              Balance = Math.Max(0, TotalOrdered − TotalDelivered).
    ///              Never sum per-DN BalanceQty snapshots across siblings.
    ///              Added GetPreviouslyDeliveredMapAsync for self-healing PREV.
    ///              ToListItem now maps TotalPreviouslyDeliveredQty,
    ///              TaxInvoiceId, HasTaxInvoice (Improvements 6 + 7).
    /// Phase 10.8b: GetDeliveryHistoryAsync now builds cumulative timeline
    ///              (OrderedQty, CumulativePrevQty, CumulativeBalanceQty)
    ///              per entry so DN detail history feed shows master-DN
    ///              pattern instead of just per-DN qty.
    /// Ship 8c-2  : GetSqmSummariesByJobOrderAsync — batch SQM aggregation
    ///              per Job Order for the list view. Uses SqmCalculator
    ///              (0.5 m² minimum per panel). Correct MAX/SUM math
    ///              matching GetPendingQtyJobsAsync pattern.
    /// </summary>
    public class DeliveryNoteRepository : IDeliveryNoteRepository
    {
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

            // Phase 10.7 fix (Gotcha #40): compute balance per unique item,
            // not by summing BalanceQty snapshots across sibling DNs.
            // TotalOrdered   = max OrderedQty per unique (SpecGroup, Serial).
            // TotalDelivered = sum of DeliveredQty across all DNs for that item.
            // TotalBalance   = Math.Max(0, TotalOrdered − TotalDelivered).

            var allItems = await ctx.DeliveryNoteItems
                .Where(i => i.JobOrderId != null)
                .ToListAsync();

            var grouped = allItems
                .GroupBy(i => i.JobOrderId)
                .ToList();

            var result = new List<PendingQtySummary>();

            foreach (var joGroup in grouped)
            {
                string joId = joGroup.Key;

                var itemGroups = joGroup
                    .GroupBy(i => new { i.SpecGroupNumber, i.SerialNumber });

                int totalOrdered = itemGroups.Sum(g => g.Max(i => i.OrderedQty));
                int totalDelivered = itemGroups.Sum(g => g.Sum(i => i.DeliveredQty));
                int totalBalance = Math.Max(0, totalOrdered - totalDelivered);

                if (totalBalance == 0) continue;

                var latestDn = await ctx.DeliveryNotes
                    .Where(x => x.JobOrderId == joId)
                    .OrderByDescending(x => x.SequenceNumber)
                    .FirstOrDefaultAsync();

                result.Add(new PendingQtySummary
                {
                    JobOrderId = joId,
                    CustomerName = latestDn?.CustomerName ?? "",
                    ProjectName = latestDn?.ProjectName ?? "",
                    TotalOrdered = totalOrdered,
                    TotalDelivered = totalDelivered,
                    TotalBalance = totalBalance,
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

            // Phase 10.7 fix (Gotcha #40):
            // TotalOrdered   = sum of max OrderedQty per unique (SpecGroup, Serial).
            // TotalDelivered = sum of all DeliveredQty across every sibling DN.
            // TotalBalance   = Math.Max(0, TotalOrdered − TotalDelivered).

            var itemGroups = items
                .GroupBy(i => new { i.SpecGroupNumber, i.SerialNumber })
                .ToList();

            int totalOrdered = itemGroups.Sum(g => g.Max(i => i.OrderedQty));
            int totalDelivered = itemGroups.Sum(g => g.Sum(i => i.DeliveredQty));
            int totalBalance = Math.Max(0, totalOrdered - totalDelivered);

            var summary = new DeliveryHistorySummary
            {
                JobOrderId = jobOrderId,
                TotalOrdered = totalOrdered,
                TotalDelivered = totalDelivered,
                TotalBalance = totalBalance,
                TotalRework = items.Sum(i => i.ReworkQty),
                TotalReturned = items.Sum(i => i.ReturnedQty)
            };

            // Phase 10.8b — cumulative timeline
            int runningPrevTotal = 0;

            foreach (var dn in dns)
            {
                var dnItems = items.Where(i => i.DeliveryNoteId == dn.DeliveryNoteId);
                int thisDnDelivered = dnItems.Sum(i => i.DeliveredQty);

                int cumulativePrev = runningPrevTotal;
                int cumulativeBalanceAfter = Math.Max(0,
                    totalOrdered - cumulativePrev - thisDnDelivered);

                summary.Entries.Add(new DeliveryHistoryEntry
                {
                    DeliveryNoteId = dn.DeliveryNoteId,
                    DeliveryType = dn.DeliveryType,
                    Status = dn.Status,
                    DeliveredQty = thisDnDelivered,
                    BalanceQty = dn.TotalBalanceQty,
                    DeliveryDate = dn.DeliveryDate,
                    DriverName = dn.DriverName,
                    SequenceNumber = dn.SequenceNumber,
                    OrderedQty = totalOrdered,
                    CumulativePrevQty = cumulativePrev,
                    CumulativeBalanceQty = cumulativeBalanceAfter
                });

                runningPrevTotal += thisDnDelivered;
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

        // ── Phase 10.7 — Self-healing PREV map ──────────────────────────

        public async Task<Dictionary<string, int>> GetPreviouslyDeliveredMapAsync(
            string jobOrderId, string excludeDnId)
        {
            using var ctx = new DeliveryNotesDbContext();

            var confirmedDnIds = await ctx.DeliveryNotes
                .Where(x => x.JobOrderId == jobOrderId
                         && x.Status != "Draft"
                         && x.DeliveryNoteId != excludeDnId)
                .Select(x => x.DeliveryNoteId)
                .ToListAsync();

            if (confirmedDnIds.Count == 0)
                return new Dictionary<string, int>();

            var items = await ctx.DeliveryNoteItems
                .Where(i => confirmedDnIds.Contains(i.DeliveryNoteId)
                         && i.JobOrderId == jobOrderId)
                .ToListAsync();

            var map = items
                .GroupBy(i => $"{i.SpecGroupNumber}|{i.SerialNumber}")
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(i => i.DeliveredQty));

            return map;
        }

        // ══════════════════════════════════════════════════════════════════
        //  SHIP 8c-2 — SQM aggregation per Job Order (batch)
        //
        //  Called by DeliveryNoteListViewModel once per list load.
        //  Returns aggregated SQM for every JO in the input list.
        //
        //  Math (matches GetPendingQtyJobsAsync pattern):
        //    OrderedSqm   = Σ (Max(OrderedQty)  × panelSqm) per unique item
        //    DeliveredSqm = Σ (Sum(DeliveredQty) × panelSqm) per unique item
        //    BalanceSqm   = Max(0, OrderedSqm − DeliveredSqm)
        //
        //  panelSqm uses SqmCalculator (0.5 m² floor per panel).
        //
        //  "Unique item" = (SpecGroupNumber, SerialNumber) — same panel
        //  can appear in multiple split DNs; we group them per JO.
        // ══════════════════════════════════════════════════════════════════
        public async Task<Dictionary<string, JobOrderSqmSummary>>
            GetSqmSummariesByJobOrderAsync(IEnumerable<string> jobOrderIds)
        {
            var idList = jobOrderIds.ToList();
            if (idList.Count == 0)
                return new Dictionary<string, JobOrderSqmSummary>();

            using var ctx = new DeliveryNotesDbContext();

            var items = await ctx.DeliveryNoteItems
                .Where(i => i.JobOrderId != null
                         && idList.Contains(i.JobOrderId))
                .ToListAsync();

            var result = new Dictionary<string, JobOrderSqmSummary>();

            var joGroups = items.GroupBy(i => i.JobOrderId!);

            foreach (var joGroup in joGroups)
            {
                var uniqueItems = joGroup
                    .GroupBy(i => new { i.SpecGroupNumber, i.SerialNumber });

                decimal orderedSqm = 0m;
                decimal deliveredSqm = 0m;

                foreach (var itemGroup in uniqueItems)
                {
                    // All rows in this group are the same panel (W1/H1 identical).
                    var first = itemGroup.First();
                    decimal panelSqm = SqmCalculator.CalculatePanel(first.W1, first.H1);

                    // OrderedQty replicated across splits → MAX
                    int maxOrdered = itemGroup.Max(i => i.OrderedQty);
                    // DeliveredQty is per-split partial → SUM
                    int totalDelivered = itemGroup.Sum(i => i.DeliveredQty);

                    orderedSqm += panelSqm * maxOrdered;
                    deliveredSqm += panelSqm * totalDelivered;
                }

                result[joGroup.Key!] = new JobOrderSqmSummary
                {
                    OrderedSqm = Math.Round(orderedSqm, 3),
                    DeliveredSqm = Math.Round(deliveredSqm, 3),
                    BalanceSqm = Math.Round(
                        Math.Max(0m, orderedSqm - deliveredSqm), 3)
                };
            }

            return result;
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

            var existingItems = await ctx.DeliveryNoteItems
                .Where(i => i.DeliveryNoteId == entity.DeliveryNoteId)
                .ToListAsync();
            ctx.DeliveryNoteItems.RemoveRange(existingItems);

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

        // ── Attachment counts (Phase 10.6b) ──────────────────────────────

        public async Task<int> GetAttachmentCountAsync(string deliveryNoteId)
        {
            using var ctx = new DeliveryNotesDbContext();
            return await ctx.DeliveryNoteAttachments
                .CountAsync(a => a.DeliveryNoteId == deliveryNoteId);
        }

        public async Task<Dictionary<string, int>> GetAttachmentCountsAsync(
            IEnumerable<string> deliveryNoteIds)
        {
            var idList = deliveryNoteIds.ToList();
            if (idList.Count == 0) return new Dictionary<string, int>();

            using var ctx = new DeliveryNotesDbContext();

            var raw = await ctx.DeliveryNoteAttachments
                .Where(a => idList.Contains(a.DeliveryNoteId))
                .GroupBy(a => a.DeliveryNoteId)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync();

            var dict = raw.ToDictionary(x => x.Id, x => x.Count);

            foreach (var id in idList)
                if (!dict.ContainsKey(id))
                    dict[id] = 0;

            return dict;
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
                TotalPreviouslyDeliveredQty = e.TotalPreviouslyDeliveredQty,
                SequenceNumber = e.SequenceNumber,
                DeliveryDate = e.DeliveryDate,
                DeliveryTime = e.DeliveryTime,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
                TaxInvoiceId = e.TaxInvoiceId,
                HasTaxInvoice = e.HasTaxInvoice
            };
    }
}