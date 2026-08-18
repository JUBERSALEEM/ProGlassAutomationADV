using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.Database.SchemaMigrations;
using ProGlassAutomation.Services.DeliveryNote.Database;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProGlassAutomation.Services.Audit;

namespace ProGlassAutomation.Services.DeliveryNote.Repository
{
    /// <summary>
    /// EF Core implementation of IDeliveryNoteRepository.
    ///
    /// CHAT 19: Master DN system added.
    /// CHAT 19 INVOICED:
    ///   MarkAsInvoicedAsync         — marks single DN invoiced after TI saved
    ///   MarkMultipleAsInvoicedAsync — marks multiple DNs invoiced (Master Invoice)
    ///   GetDeliveryHistoryAsync     — now maps HasTaxInvoice + TaxInvoiceId
    ///                                 onto each DeliveryHistoryEntry so the
    ///                                 history sidebar can show invoiced badges.
    /// </summary>
    public class DeliveryNoteRepository : IDeliveryNoteRepository
    {
        public async Task InitializeAsync()
        {
            using var ctx = new DeliveryNotesDbContext();
            await ctx.Database.EnsureCreatedAsync();

            // Additive schema upgrades for an existing deliverynotes.db.
            SchemaMigrationRunner.Upgrade(
                DeliveryNotesDbContext.DatabaseFilePath,
                AppSchemaMigrations.DeliveryNotes,
                "Delivery Note");
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

        // ── CHAT 19 — Master DN methods ──────────────────────────────────

        public async Task<SavedDeliveryNote?> GetMasterDnForJobOrderAsync(
            string jobOrderId)
        {
            using var ctx = new DeliveryNotesDbContext();
            return await ctx.DeliveryNotes
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x =>
                    x.JobOrderId == jobOrderId &&
                    x.IsMasterDn);
        }

        public async Task<List<DeliveryNoteListItem>> GetMasterDnsAsync()
        {
            using var ctx = new DeliveryNotesDbContext();
            var entities = await ctx.DeliveryNotes
                .Where(x => x.IsMasterDn)
                .OrderByDescending(x => x.UpdatedAt)
                .ToListAsync();
            return entities.Select(ToListItem).ToList();
        }

        public async Task<SavedDeliveryNote?> CreateMasterDnAsync(
            string jobOrderId,
            string proformaId,
            string customerName,
            string projectName,
            string masterDnId,
            List<DeliveryNoteItem> items)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.DeliveryNoteEdit, "create a master delivery note");

            using var ctx = new DeliveryNotesDbContext();

            bool exists = await ctx.DeliveryNotes
                .AnyAsync(x => x.JobOrderId == jobOrderId && x.IsMasterDn);
            if (exists) return null;

            var now = DateTime.Now;
            int totalQty = items.Sum(i => i.OrderedQty);

            var masterDn = new SavedDeliveryNote
            {
                DeliveryNoteId = masterDnId,
                JobOrderId = jobOrderId,
                ProformaId = proformaId,
                SequenceNumber = 0,
                CustomerName = customerName,
                ProjectName = projectName,
                Status = "Draft",
                DeliveryType = "Dummy",
                IsMasterDn = true,
                LastUpdatedFromDnId = "",
                IsFoc = false,
                DriverName = "",
                VehicleNumber = "",
                ReceiverName = "",
                DeliveryDate = now,
                DeliveryTime = now.ToString("HH:mm"),
                Notes = "Master DN — auto-generated reference tracker",
                TotalOrderedQty = totalQty,
                TotalDeliveredQty = 0,
                TotalBalanceQty = totalQty,
                TotalPreviouslyDeliveredQty = 0,
                TaxInvoiceId = "",
                HasTaxInvoice = false,
                FullSnapshotJson = "{}",
                CreatedAt = now,
                UpdatedAt = now,
                Items = items
            };

            ctx.DeliveryNotes.Add(masterDn);
            await ctx.SaveChangesAsync();
            return masterDn;
        }

        public async Task UpdateMasterDnAsync(
            string jobOrderId,
            string triggeredByDnId)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.DeliveryNoteEdit, "update a master delivery note");

            using var ctx = new DeliveryNotesDbContext();

            var masterDn = await ctx.DeliveryNotes
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x =>
                    x.JobOrderId == jobOrderId &&
                    x.IsMasterDn);

            if (masterDn == null) return;

            // Confirmed real DNs only. Drafts must not inflate Master
            // Prev/Balance — same rule as GetPreviouslyDeliveredMapAsync.
            var realDnIds = await ctx.DeliveryNotes
                .Where(x => x.JobOrderId == jobOrderId &&
                            !x.IsMasterDn &&
                            x.DeliveryType != "Dummy" &&
                            x.Status != "Draft")
                .Select(x => x.DeliveryNoteId)
                .ToListAsync();

            var allRealItems = await ctx.DeliveryNoteItems
                .Where(i => i.JobOrderId == jobOrderId &&
                            realDnIds.Contains(i.DeliveryNoteId))
                .ToListAsync();

            var deliveredMap = allRealItems
                .GroupBy(i => $"{i.SpecGroupNumber}|{i.SerialNumber}")
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(i => i.DeliveredQty));

            foreach (var masterItem in masterDn.Items)
            {
                var key = $"{masterItem.SpecGroupNumber}|{masterItem.SerialNumber}";
                int totalDelivered =
                    deliveredMap.TryGetValue(key, out var d) ? d : 0;

                masterItem.PreviouslyDeliveredQty = totalDelivered;
                masterItem.DeliveredQty = 0;
                masterItem.BalanceQty = Math.Max(0,
                    masterItem.OrderedQty - totalDelivered);
            }

            masterDn.TotalDeliveredQty =
                masterDn.Items.Sum(i => i.PreviouslyDeliveredQty);
            masterDn.TotalBalanceQty =
                masterDn.Items.Sum(i => i.BalanceQty);
            masterDn.TotalPreviouslyDeliveredQty =
                masterDn.TotalDeliveredQty;
            masterDn.LastUpdatedFromDnId = triggeredByDnId;
            masterDn.UpdatedAt = DateTime.Now;

            var existingItems = await ctx.DeliveryNoteItems
                .Where(i => i.DeliveryNoteId == masterDn.DeliveryNoteId)
                .ToListAsync();
            ctx.DeliveryNoteItems.RemoveRange(existingItems);

            masterDn.Attachments.Clear();
            ctx.DeliveryNotes.Update(masterDn);
            await ctx.SaveChangesAsync();
        }

        public async Task<string> BuildMasterDnIdAsync(string jobOrderId)
        {
            string core = jobOrderId.StartsWith(
                "JOB-", StringComparison.OrdinalIgnoreCase)
                ? jobOrderId.Substring(4)
                : jobOrderId;

            return await Task.FromResult($"MDN-{core}");
        }

        // ── CHAT 19 INVOICED — mark DNs as invoiced ──────────────────────

        /// <summary>
        /// Marks a single DN as invoiced after Tax Invoice is saved.
        /// Sets HasTaxInvoice = true and TaxInvoiceId on the DN.
        /// Only updates indexed columns — FullSnapshotJson NOT rebuilt
        /// (snapshot is used for PDF/export only, not for invoiced state).
        /// Idempotent — safe to call multiple times on same DN.
        /// </summary>
        public async Task MarkAsInvoicedAsync(
            string deliveryNoteId,
            string taxInvoiceId)
        {
            if (string.IsNullOrWhiteSpace(deliveryNoteId)) return;
            if (string.IsNullOrWhiteSpace(taxInvoiceId)) return;

            using var ctx = new DeliveryNotesDbContext();

            var entity = await ctx.DeliveryNotes
                .FirstOrDefaultAsync(x =>
                    x.DeliveryNoteId == deliveryNoteId);

            if (entity == null) return;

            // Already marked with same TI — no-op
            if (entity.HasTaxInvoice &&
                string.Equals(entity.TaxInvoiceId, taxInvoiceId,
                    StringComparison.OrdinalIgnoreCase))
                return;

            entity.HasTaxInvoice = true;
            entity.TaxInvoiceId = taxInvoiceId;
            entity.UpdatedAt = DateTime.Now;

            await ctx.SaveChangesAsync();
        }

        /// <summary>
        /// Marks multiple DNs as invoiced in one operation.
        /// Used when a Master Invoice covers multiple sibling DNs.
        /// deliveryNoteIds parsed from DeliveryNoteReference (comma-separated).
        /// All DNs get the same taxInvoiceId.
        /// Idempotent. Safe with empty list — no-ops gracefully.
        /// Single SaveChangesAsync call for performance.
        /// </summary>
        public async Task MarkMultipleAsInvoicedAsync(
            IEnumerable<string> deliveryNoteIds,
            string taxInvoiceId)
        {
            if (string.IsNullOrWhiteSpace(taxInvoiceId)) return;

            var idList = deliveryNoteIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (idList.Count == 0) return;

            using var ctx = new DeliveryNotesDbContext();

            var entities = await ctx.DeliveryNotes
                .Where(x => idList.Contains(x.DeliveryNoteId))
                .ToListAsync();

            if (entities.Count == 0) return;

            var now = DateTime.Now;
            bool anyChanged = false;

            foreach (var entity in entities)
            {
                if (entity.HasTaxInvoice &&
                    string.Equals(entity.TaxInvoiceId, taxInvoiceId,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                entity.HasTaxInvoice = true;
                entity.TaxInvoiceId = taxInvoiceId;
                entity.UpdatedAt = now;
                anyChanged = true;
            }

            if (anyChanged)
                await ctx.SaveChangesAsync();
        }

        // ── List retrieval ───────────────────────────────────────────────

        public async Task<List<DeliveryNoteListItem>> GetAllAsync()
        {
            using var ctx = new DeliveryNotesDbContext();
            // PERF: project straight to the list item at the SQL level so the
            // FullSnapshotJson blob column is never read. Behaviourally
            // identical to materialising the entity then calling ToListItem,
            // but the blobs never cross the wire.
            return await ctx.DeliveryNotes
                .Where(x => !x.IsMasterDn)
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new DeliveryNoteListItem
                {
                    Id = x.Id,
                    DeliveryNoteId = x.DeliveryNoteId,
                    JobOrderId = x.JobOrderId,
                    ProformaId = x.ProformaId,
                    CustomerName = x.CustomerName,
                    ProjectName = x.ProjectName,
                    Status = x.Status,
                    DeliveryType = x.DeliveryType,
                    IsFoc = x.IsFoc,
                    IsMasterDn = x.IsMasterDn,
                    DriverName = x.DriverName,
                    VehicleNumber = x.VehicleNumber,
                    TotalOrderedQty = x.TotalOrderedQty,
                    TotalDeliveredQty = x.TotalDeliveredQty,
                    TotalBalanceQty = x.TotalBalanceQty,
                    TotalPreviouslyDeliveredQty = x.TotalPreviouslyDeliveredQty,
                    SequenceNumber = x.SequenceNumber,
                    DeliveryDate = x.DeliveryDate,
                    DeliveryTime = x.DeliveryTime,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    TaxInvoiceId = x.TaxInvoiceId,
                    HasTaxInvoice = x.HasTaxInvoice
                })
                .ToListAsync();
        }

        public async Task<List<DeliveryNoteListItem>> GetByJobOrderIdAsync(
            string jobOrderId)
        {
            using var ctx = new DeliveryNotesDbContext();
            var entities = await ctx.DeliveryNotes
                .Where(x => x.JobOrderId == jobOrderId &&
                            !x.IsMasterDn)
                .OrderBy(x => x.SequenceNumber)
                .ToListAsync();
            return entities.Select(ToListItem).ToList();
        }

        public async Task<List<DeliveryNoteListItem>> GetPreparedAsync()
        {
            using var ctx = new DeliveryNotesDbContext();
            var entities = await ctx.DeliveryNotes
                .Where(x => x.Status == "Draft" &&
                            !x.IsMasterDn)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
            return entities.Select(ToListItem).ToList();
        }

        public async Task<List<DeliveryNoteListItem>> GetDeliveredAsync()
        {
            using var ctx = new DeliveryNotesDbContext();
            var entities = await ctx.DeliveryNotes
                .Where(x => (x.Status == "Delivered" ||
                             x.Status == "Signed" ||
                             x.Status == "Received") &&
                            !x.IsMasterDn)
                .OrderByDescending(x => x.DeliveryDate)
                .ToListAsync();
            return entities.Select(ToListItem).ToList();
        }

        public async Task<List<PendingQtySummary>> GetPendingQtyJobsAsync()
        {
            using var ctx = new DeliveryNotesDbContext();

            var realDnIds = await ctx.DeliveryNotes
                .Where(x => !x.IsMasterDn &&
                            x.DeliveryType != "Dummy")
                .Select(x => x.DeliveryNoteId)
                .ToListAsync();

            var allItems = await ctx.DeliveryNoteItems
                .Where(i => i.JobOrderId != null &&
                            realDnIds.Contains(i.DeliveryNoteId))
                .ToListAsync();

            var grouped = allItems
                .GroupBy(i => i.JobOrderId)
                .ToList();

            var result = new List<PendingQtySummary>();

            foreach (var joGroup in grouped)
            {
                string joId = joGroup.Key!;

                var itemGroups = joGroup
                    .GroupBy(i => new { i.SpecGroupNumber, i.SerialNumber });

                int totalOrdered = itemGroups.Sum(g => g.Max(i => i.OrderedQty));
                int totalDelivered = itemGroups.Sum(g => g.Sum(i => i.DeliveredQty));
                int totalBalance = Math.Max(0, totalOrdered - totalDelivered);

                if (totalBalance == 0) continue;

                var latestDn = await ctx.DeliveryNotes
                    .Where(x => x.JobOrderId == joId && !x.IsMasterDn)
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

        /// <summary>
        /// CHAT 19 INVOICED FIX: Each DeliveryHistoryEntry now carries
        /// HasTaxInvoice and TaxInvoiceId from the DN record.
        /// This allows the history sidebar to show invoiced badge
        /// per DN row without additional DB calls.
        /// </summary>
        public async Task<DeliveryHistorySummary> GetDeliveryHistoryAsync(
            string jobOrderId)
        {
            using var ctx = new DeliveryNotesDbContext();

            var allDns = await ctx.DeliveryNotes
                .Where(x => x.JobOrderId == jobOrderId &&
                            !x.IsMasterDn &&
                            x.DeliveryType != "Dummy")
                .OrderBy(x => x.SequenceNumber)
                .ToListAsync();

            var realDnIds = allDns.Select(x => x.DeliveryNoteId).ToList();

            var items = await ctx.DeliveryNoteItems
                .Where(i => i.JobOrderId == jobOrderId &&
                            realDnIds.Contains(i.DeliveryNoteId))
                .ToListAsync();

            var itemGroups = items
                .GroupBy(i => new { i.SpecGroupNumber, i.SerialNumber })
                .ToList();

            int totalOrdered = itemGroups.Count > 0
                ? itemGroups.Sum(g => g.Max(i => i.OrderedQty))
                : 0;
            int totalDelivered = itemGroups.Count > 0
                ? itemGroups.Sum(g => g.Sum(i => i.DeliveredQty))
                : 0;
            int totalBalance = Math.Max(0, totalOrdered - totalDelivered);

            if (totalOrdered == 0)
            {
                var masterDn = await ctx.DeliveryNotes
                    .FirstOrDefaultAsync(x =>
                        x.JobOrderId == jobOrderId && x.IsMasterDn);
                if (masterDn != null)
                {
                    totalOrdered = masterDn.TotalOrderedQty;
                    totalBalance = totalOrdered;
                }
            }

            var summary = new DeliveryHistorySummary
            {
                JobOrderId = jobOrderId,
                TotalOrdered = totalOrdered,
                TotalDelivered = totalDelivered,
                TotalBalance = totalBalance,
                TotalRework = items.Sum(i => i.ReworkQty),
                TotalReturned = items.Sum(i => i.ReturnedQty)
            };

            int runningPrevTotal = 0;

            foreach (var dn in allDns)
            {
                var dnItems = items
                    .Where(i => i.DeliveryNoteId == dn.DeliveryNoteId);
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
                    CumulativeBalanceQty = cumulativeBalanceAfter,

                    // CHAT 19 INVOICED FIX: map invoice state per DN
                    // These drive the invoiced badge in the history sidebar
                    HasTaxInvoice = dn.HasTaxInvoice,
                    TaxInvoiceId = dn.TaxInvoiceId ?? ""
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

            var siblingIds = await ctx.DeliveryNotes
                .Where(x => x.JobOrderId == jobOrderId
                         && !x.IsMasterDn
                         && x.DeliveryType != "Dummy")
                .Select(x => x.DeliveryNoteId)
                .ToListAsync();

            return await ctx.DeliveryNoteItems
                .Where(i => siblingIds.Contains(i.DeliveryNoteId)
                         && i.JobOrderId == jobOrderId
                         && i.SpecGroupNumber == specGroupNumber
                         && i.SerialNumber == serialNumber)
                .SumAsync(i => i.DeliveredQty);
        }

        public async Task<Dictionary<string, int>> GetPreviouslyDeliveredMapAsync(
            string jobOrderId, string excludeDnId)
        {
            using var ctx = new DeliveryNotesDbContext();

            var siblingIds = await ctx.DeliveryNotes
                .Where(x => x.JobOrderId == jobOrderId
                         && x.DeliveryNoteId != excludeDnId
                         && !x.IsMasterDn
                         && x.DeliveryType != "Dummy")
                .Select(x => x.DeliveryNoteId)
                .ToListAsync();

            if (siblingIds.Count == 0)
                return new Dictionary<string, int>();

            var items = await ctx.DeliveryNoteItems
                .Where(i => siblingIds.Contains(i.DeliveryNoteId)
                         && i.JobOrderId == jobOrderId)
                .ToListAsync();

            return BuildDeliveredLookup(items);
        }

        private static Dictionary<string, int> BuildDeliveredLookup(
            IEnumerable<DeliveryNoteItem> items)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                if (item.DeliveredQty <= 0) continue;
                Acc(map, SpecKey(item), item.DeliveredQty);
                Acc(map, GlassKey(item), item.DeliveredQty);
            }
            return map;
        }

        private static int LookupDelivered(
            Dictionary<string, int> map, DeliveryNoteItem item)
        {
            if (map.TryGetValue(SpecKey(item), out var bySpec) && bySpec > 0)
                return bySpec;
            if (map.TryGetValue(GlassKey(item), out var byGlass) && byGlass > 0)
                return byGlass;
            return 0;
        }

        private static string SpecKey(DeliveryNoteItem i) =>
            $"{i.SpecGroupNumber}|{i.SerialNumber}";

        private static string GlassKey(DeliveryNoteItem i) =>
            $"G|{i.GlassRef}|{i.W1}|{i.H1}|{i.W2}|{i.H2}";

        private static void Acc(Dictionary<string, int> map, string key, int qty)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            map[key] = map.TryGetValue(key, out var n) ? n + qty : qty;
        }

        // ── SQM aggregation ──────────────────────────────────────────────

        public async Task<Dictionary<string, JobOrderSqmSummary>>
            GetSqmSummariesByJobOrderAsync(IEnumerable<string> jobOrderIds)
        {
            var idList = jobOrderIds.ToList();
            if (idList.Count == 0)
                return new Dictionary<string, JobOrderSqmSummary>();

            using var ctx = new DeliveryNotesDbContext();

            var realDnIds = await ctx.DeliveryNotes
                .Where(x => idList.Contains(x.JobOrderId) &&
                            !x.IsMasterDn &&
                            x.DeliveryType != "Dummy")
                .Select(x => x.DeliveryNoteId)
                .ToListAsync();

            var items = await ctx.DeliveryNoteItems
                .Where(i => i.JobOrderId != null
                         && idList.Contains(i.JobOrderId)
                         && realDnIds.Contains(i.DeliveryNoteId))
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
                    var first = itemGroup.First();
                    decimal panelSqm =
                        SqmCalculator.CalculatePanel(first.W1, first.H1);
                    int maxOrdered = itemGroup.Max(i => i.OrderedQty);
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
                .Where(x => x.JobOrderId == jobOrderId && !x.IsMasterDn)
                .MaxAsync(x => (int?)x.SequenceNumber) ?? 0;
            return max + 1;
        }

        public async Task<string> BuildDeliveryNoteIdAsync(
            string jobOrderId, int sequence)
        {
            string core = jobOrderId.StartsWith(
                "JOB-", StringComparison.OrdinalIgnoreCase)
                ? jobOrderId.Substring(4)
                : jobOrderId;

            return $"DN-{core}-{sequence:D2}";
        }

        // ── CRUD ─────────────────────────────────────────────────────────

        public async Task<SavedDeliveryNote> InsertAsync(SavedDeliveryNote entity)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.DeliveryNoteEdit, "create a delivery note");

            using var ctx = new DeliveryNotesDbContext();
            entity.CreatedAt = DateTime.Now;
            entity.UpdatedAt = DateTime.Now;
            ctx.DeliveryNotes.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(SavedDeliveryNote entity)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.DeliveryNoteEdit, "update a delivery note");

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
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.DeliveryNoteEdit, "delete a delivery note");

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
                .AnyAsync(x => x.JobOrderId == jobOrderId && !x.IsMasterDn);
        }

        // ── Attachments ──────────────────────────────────────────────────

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
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.DeliveryNoteEdit, "add a delivery note attachment");

            using var ctx = new DeliveryNotesDbContext();
            entity.UploadedAt = DateTime.Now;
            ctx.DeliveryNoteAttachments.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task DeleteAttachmentAsync(int attachmentId)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.DeliveryNoteEdit, "delete a delivery note attachment");

            using var ctx = new DeliveryNotesDbContext();
            var entity = await ctx.DeliveryNoteAttachments
                .FirstOrDefaultAsync(a => a.Id == attachmentId);
            if (entity == null) return;

            ctx.DeliveryNoteAttachments.Remove(entity);
            await ctx.SaveChangesAsync();
        }

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

        // ── ANALYTICS PROJECTION ────────────────────────────────────────

        public async Task<List<DeliveryNoteAnalyticsRow>> GetAnalyticsRowsAsync()
        {
            using var ctx = new DeliveryNotesDbContext();
            return await ctx.DeliveryNotes
                .Where(d => !d.IsMasterDn)
                .AsNoTracking()
                .Select(d => new DeliveryNoteAnalyticsRow
                {
                    DeliveryNoteId = d.DeliveryNoteId,
                    Status = d.Status,
                    DeliveryDate = d.DeliveryDate,
                    HasTaxInvoice = d.HasTaxInvoice
                })
                .ToListAsync();
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
                IsMasterDn = e.IsMasterDn,
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