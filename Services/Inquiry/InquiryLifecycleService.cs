// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Inquiry/InquiryLifecycleService.cs
// PROJECT: ProGlassAutomation
// MODULE: Inquiry — lifecycle hydration implementation
//
// Implementation strategy (per senior-dev review):
//   1. Walk the join chain (Inquiry → PI → JO → DN → TI → Statement)
//      as a series of EF Core queries, one per stage.
//   2. Each stage is a small read-only SELECT — no tracking, no joins
//      to other tables. Five round-trips per page load, each on a
//      different database file. They run in parallel.
//   3. Stitch the per-stage rows into a single
//      InquiryLifecycleSnapshot per inquiry ID.
//   4. Cache the stitched result for 60 seconds keyed by the
//      ID-batch. Cache hit = instant return, no DB calls.
//
// The cache is intentionally simple: a single dictionary guarded by
// a lock. We don't need distributed cache, we don't need eviction
// logic beyond TTL — the register is a single-process desktop app
// and the volume of inquiry IDs is bounded by page size (50).
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.DeliveryNote.Database;
using ProGlassAutomation.Services.JobOrder.Database;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.Statement.Database;
using ProGlassAutomation.Services.TaxInvoice.Database;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Inquiry
{
    public sealed class InquiryLifecycleService : IInquiryLifecycleService
    {
        /// <summary>
        /// 60s TTL: long enough to absorb re-sort / re-filter / page
        /// navigation, short enough that a downstream edit
        /// (PI created, DN dispatched, TI paid) becomes visible
        /// within a minute even without an explicit Invalidate().
        /// </summary>
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        private sealed class CacheEntry
        {
            public DateTime StoredAt { get; init; } = DateTime.UtcNow;
            public IReadOnlyDictionary<int, InquiryLifecycleSnapshot> Snapshots { get; init; } = new Dictionary<int, InquiryLifecycleSnapshot>();
            public bool IsFresh => DateTime.UtcNow - StoredAt < CacheTtl;
        }

        // Guarded by lock(_cache). Bounded by page-size * active users;
        // we keep at most 64 batches (~3,200 inquiry IDs) before pruning.
        private readonly Dictionary<string, CacheEntry> _cache = new();
        private readonly object _lock = new();

        public async Task<IReadOnlyDictionary<int, InquiryLifecycleSnapshot>> GetLifecycleAsync(
            IReadOnlyCollection<int> inquiryIds, CancellationToken ct = default)
        {
            if (inquiryIds == null || inquiryIds.Count == 0)
                return new Dictionary<int, InquiryLifecycleSnapshot>();

            string key = string.Join(",", inquiryIds.OrderBy(x => x));
            CacheEntry? hit = null;
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var entry) && entry.IsFresh)
                    hit = entry;
            }
            if (hit != null) return hit.Snapshots;

            // ── Walk the chain, one stage at a time, all in parallel ────
            // Each task opens its own DbContext, runs one AsNoTracking
            // SELECT, and returns a (id, stage-data) projection. We
            // then stitch them together into per-inquiry snapshots.

            var piTask = LoadProformaAsync(inquiryIds, ct);
            var joTask = LoadJobOrderAsync(inquiryIds, ct);
            var dnTask = LoadDeliveryNoteAsync(inquiryIds, ct);
            var tiTask = LoadTaxInvoiceAsync(inquiryIds, ct);
            // Statement has no FK to inquiry; it's filtered by customer/date
            // and we surface a "Statements exist for this customer" badge
            // rather than a per-invoice link. Skipped here.
            await Task.WhenAll(piTask, joTask, dnTask, tiTask).ConfigureAwait(false);

            var piByInquiry = await piTask.ConfigureAwait(false);
            var joByPiNumber = await joTask.ConfigureAwait(false);
            var dnByJoNumber = await dnTask.ConfigureAwait(false);
            var tiByPiNumber = await tiTask.ConfigureAwait(false);

            var result = new Dictionary<int, InquiryLifecycleSnapshot>(inquiryIds.Count);
            foreach (int id in inquiryIds)
            {
                piByInquiry.TryGetValue(id, out var pi);
                var piNumber = pi?.ProformaId;
                if (!string.IsNullOrWhiteSpace(piNumber) &&
                    joByPiNumber.TryGetValue(piNumber, out var jo))
                {
                    var joNumber = jo.JobOrderId;
                    if (!string.IsNullOrWhiteSpace(joNumber) &&
                        dnByJoNumber.TryGetValue(joNumber, out var dn))
                    {
                        if (!string.IsNullOrWhiteSpace(piNumber) &&
                            tiByPiNumber.TryGetValue(piNumber, out var ti))
                        {
                            result[id] = BuildSnapshot(id, pi!, jo, dn, ti);
                            continue;
                        }
                        result[id] = BuildSnapshot(id, pi!, jo, dn, ti: null);
                        continue;
                    }
                    if (!string.IsNullOrWhiteSpace(piNumber) &&
                        tiByPiNumber.TryGetValue(piNumber, out var ti2))
                    {
                        result[id] = BuildSnapshot(id, pi!, jo, dn: null, ti: ti2);
                        continue;
                    }
                    result[id] = BuildSnapshot(id, pi!, jo, dn: null, ti: null);
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(piNumber) &&
                    tiByPiNumber.TryGetValue(piNumber, out var ti3))
                {
                    result[id] = BuildSnapshot(id, pi!, jo: null, dn: null, ti: ti3);
                    continue;
                }
                result[id] = InquiryLifecycleSnapshot.Empty(id);
            }

            // ── Cache + prune ───────────────────────────────────────────
            lock (_lock)
            {
                _cache[key] = new CacheEntry { Snapshots = result };
                if (_cache.Count > 64)
                {
                    // Drop expired entries first; if still over budget,
                    // drop the oldest.
                    var expired = _cache.Where(kv => !kv.Value.IsFresh)
                        .Select(kv => kv.Key).ToList();
                    foreach (var k in expired) _cache.Remove(k);
                    if (_cache.Count > 64)
                    {
                        var oldest = _cache.OrderBy(kv => kv.Value.StoredAt)
                            .Take(_cache.Count - 64).Select(kv => kv.Key).ToList();
                        foreach (var k in oldest) _cache.Remove(k);
                    }
                }
            }
            return result;
        }

        public void InvalidateAll()
        {
            lock (_lock) _cache.Clear();
        }

        public void Invalidate(int inquiryId)
        {
            lock (_lock)
            {
                var stale = _cache.Where(kv => kv.Value.Snapshots.ContainsKey(inquiryId))
                    .Select(kv => kv.Key).ToList();
                foreach (var k in stale) _cache.Remove(k);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  Stage loaders — one small SELECT per stage, AsNoTracking,
        //  no joins across the chain. They run in parallel.
        // ════════════════════════════════════════════════════════════════

        private static async Task<Dictionary<int, ProformaLite>> LoadProformaAsync(
            IReadOnlyCollection<int> inquiryIds, CancellationToken ct)
        {
            await using var db = new ProformaInvoicesDbContext();
            var rows = await db.ProformaInvoices.AsNoTracking()
                .Where(p => p.InquiryId.HasValue && inquiryIds.Contains(p.InquiryId.Value))
                .Select(p => new
                {
                    p.Id,
                    p.InquiryId,
                    p.ProformaId,
                    p.Status,
                    p.UpdatedAt,
                    p.GrandTotalVatInclusive
                })
                .ToListAsync(ct).ConfigureAwait(false);
            // If multiple PIs exist for one inquiry (theoretically allowed
            // for revisions), prefer the most recently updated.
            var byInquiry = new Dictionary<int, ProformaLite>();
            foreach (var r in rows)
            {
                if (!r.InquiryId.HasValue) continue;
                var lite = new ProformaLite(r.Id, r.ProformaId, r.Status, r.UpdatedAt, r.GrandTotalVatInclusive);
                if (!byInquiry.TryGetValue(r.InquiryId.Value, out var existing) ||
                    lite.UpdatedAt > existing.UpdatedAt)
                    byInquiry[r.InquiryId.Value] = lite;
            }
            return byInquiry;
        }

        private static async Task<Dictionary<string, JobOrderLite>> LoadJobOrderAsync(
            IReadOnlyCollection<int> inquiryIds, CancellationToken ct)
        {
            // We can't go straight inquiry → JO (no FK). So we go via the
            // PI numbers we already know exist for this inquiry batch.
            // First, get those PI numbers:
            var piNumbers = await LoadProformaAsync(inquiryIds, ct).ConfigureAwait(false);
            var numbers = piNumbers.Values.Select(p => p.ProformaId)
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (numbers.Count == 0) return new Dictionary<string, JobOrderLite>();

            await using var db = new JobOrdersDbContext();
            var rows = await db.JobOrders.AsNoTracking()
                .Where(j => numbers.Contains(j.SourceProformaId))
                .Select(j => new
                {
                    j.Id,
                    j.SourceProformaId,
                    j.JobOrderId,
                    j.Status,
                    j.UpdatedAt
                })
                .ToListAsync(ct).ConfigureAwait(false);
            // Keep the most recently updated JO per PI.
            var byPi = new Dictionary<string, JobOrderLite>();
            foreach (var r in rows)
            {
                var lite = new JobOrderLite(r.Id, r.JobOrderId, r.Status, r.UpdatedAt);
                if (!byPi.TryGetValue(r.SourceProformaId, out var existing) ||
                    lite.UpdatedAt > existing.UpdatedAt)
                    byPi[r.SourceProformaId] = lite;
            }
            return byPi;
        }

        private static async Task<Dictionary<string, DeliveryNoteSummary>> LoadDeliveryNoteAsync(
            IReadOnlyCollection<int> inquiryIds, CancellationToken ct)
        {
            var piNumbers = await LoadProformaAsync(inquiryIds, ct).ConfigureAwait(false);
            // We need JO numbers too. Walk the chain one more hop.
            var piToJo = new Dictionary<string, string>();
            var piNumberList = piNumbers.Values.Select(p => p.ProformaId)
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (piNumberList.Count > 0)
            {
                await using var jdb = new JobOrdersDbContext();
                var jrs = await jdb.JobOrders.AsNoTracking()
                    .Where(j => piNumberList.Contains(j.SourceProformaId))
                    .Select(j => new { j.SourceProformaId, j.JobOrderId })
                    .ToListAsync(ct).ConfigureAwait(false);
                foreach (var r in jrs) piToJo[r.SourceProformaId] = r.JobOrderId;
            }
            var joNumbers = piToJo.Values.Distinct().ToList();
            if (joNumbers.Count == 0) return new Dictionary<string, DeliveryNoteSummary>();

            await using var db = new DeliveryNotesDbContext();
            // Master DNs are bookkeeping rows (CHAT 19) — ignore them in
            // the register summary; only real DNs count.
            var rows = await db.DeliveryNotes.AsNoTracking()
                .Where(d => !d.IsMasterDn && joNumbers.Contains(d.JobOrderId))
                .Select(d => new
                {
                    d.Id,
                    d.JobOrderId,
                    d.DeliveryNoteId,
                    d.Status,
                    d.DeliveryDate,
                    d.CreatedAt
                })
                .ToListAsync(ct).ConfigureAwait(false);
            // For each JO, take the most recent real DN.
            var byJo = new Dictionary<string, DeliveryNoteSummary>();
            foreach (var r in rows)
            {
                DateTime when = r.DeliveryDate == default ? r.CreatedAt : r.CreatedAt;
                if (!byJo.TryGetValue(r.JobOrderId, out var existing) || when > existing.When)
                    byJo[r.JobOrderId] = new DeliveryNoteSummary(
                        r.Id, r.DeliveryNoteId, r.Status, when, 1);
            }
            // Now also count how many DNs each JO has.
            foreach (var grp in rows.GroupBy(x => x.JobOrderId))
            {
                if (byJo.TryGetValue(grp.Key, out var summary))
                    byJo[grp.Key] = summary with { Count = grp.Count() };
            }
            return byJo;
        }

        private static async Task<Dictionary<string, TaxInvoiceSummary>> LoadTaxInvoiceAsync(
            IReadOnlyCollection<int> inquiryIds, CancellationToken ct)
        {
            var piNumbers = await LoadProformaAsync(inquiryIds, ct).ConfigureAwait(false);
            var piNumberList = piNumbers.Values.Select(p => p.ProformaId)
                .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            if (piNumberList.Count == 0) return new Dictionary<string, TaxInvoiceSummary>();

            await using var db = new TaxInvoiceDbContext();
            var rows = await db.TaxInvoices.AsNoTracking()
                .Where(t => piNumberList.Contains(t.ProformaId))
                .Select(t => new
                {
                    t.Id,
                    t.ProformaId,
                    t.InvoiceNumber,
                    t.Status,
                    t.GrandTotal,
                    t.IssueDate,
                    t.UpdatedAt
                })
                .ToListAsync(ct).ConfigureAwait(false);

            // Aggregate per PI family: count, latest, total billed, totals outstanding.
            var byPi = new Dictionary<string, TaxInvoiceSummary>();
            foreach (var grp in rows.GroupBy(x => x.ProformaId))
            {
                var list = grp.OrderByDescending(t => t.UpdatedAt).ToList();
                var latest = list.First();
                decimal totalBilled = list.Sum(t => t.GrandTotal);
                // Outstanding: for tax invoices not yet fully paid/cancelled/void,
                // the full GrandTotal counts as owed (per the senior-dev review:
                // we don't have a "paid amount" column on SavedTaxInvoice, so
                // "outstanding" is the GrandTotal of every non-paid invoice).
                // Paid / Void / Cancelled contribute 0.
                decimal totalOutstanding = list
                    .Where(t => !t.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase)
                             && !t.Status.Equals("Void", StringComparison.OrdinalIgnoreCase)
                             && !t.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                    .Sum(t => t.GrandTotal);
                byPi[grp.Key] = new TaxInvoiceSummary(
                    Id: latest.Id,
                    InvoiceNumber: latest.InvoiceNumber,
                    Status: latest.Status,
                    When: latest.UpdatedAt == default ? latest.IssueDate : latest.UpdatedAt,
                    GrandTotal: latest.GrandTotal,
                    Count: list.Count,
                    TotalBilled: totalBilled,
                    TotalOutstanding: totalOutstanding);
            }
            return byPi;
        }

        // ════════════════════════════════════════════════════════════════
        //  Snapshot assembly
        // ════════════════════════════════════════════════════════════════

        private static InquiryLifecycleSnapshot BuildSnapshot(
            int inquiryId,
            ProformaLite pi,
            JobOrderLite? jo,
            DeliveryNoteSummary? dn,
            TaxInvoiceSummary? ti)
        {
            int furthest = 1; // we always have a PI
            if (jo != null) furthest = 2;
            if (dn != null) furthest = 3;
            if (ti != null && ti.Count > 0) furthest = 4;
            // Statement is not auto-linked; if a statement was generated
            // for this customer, the user can find it via the statements
            // module. We don't promote furthest to 5 here because we
            // have no FK to join on.
            return new InquiryLifecycleSnapshot
            {
                InquiryId = inquiryId,
                ProformaRowId = pi.Id,
                ProformaNumber = pi.ProformaId,
                ProformaStatus = pi.Status,
                ProformaUpdatedAt = pi.UpdatedAt,
                JobOrderRowId = jo?.Id ?? 0,
                JobOrderNumber = jo?.JobOrderId,
                JobOrderStatus = jo?.Status,
                JobOrderUpdatedAt = jo?.UpdatedAt,
                DeliveryNoteRowId = dn?.Id ?? 0,
                DeliveryNoteCount = dn?.Count ?? 0,
                LatestDeliveryNoteNumber = dn?.DeliveryNoteId,
                LatestDeliveryNoteStatus = dn?.Status,
                LatestDeliveryNoteAt = dn?.When,
                TaxInvoiceRowId = ti?.Id ?? 0,
                TaxInvoiceCount = ti?.Count ?? 0,
                LatestTaxInvoiceNumber = ti?.InvoiceNumber,
                LatestTaxInvoiceStatus = ti?.Status,
                LatestTaxInvoiceAt = ti?.When,
                LatestTaxInvoiceGrandTotal = ti?.GrandTotal ?? 0,
                TotalBilled = ti?.TotalBilled ?? 0,
                TotalOutstanding = ti?.TotalOutstanding ?? 0,
                CurrentBillOutstanding = ti?.GrandTotal ?? 0,
                FurthestStageReached = furthest
            };
        }

        // ── Internal projection records (lightweight, AsNoTracking-safe) ──
        // Each record now carries the database row ID alongside the
        // human-readable number. The number is still the primary key
        // for the join chain (PI number → JO number → DN/TI), but the
        // row ID is what the right-click "Open …" handlers need to
        // actually load a specific document.

        private sealed record ProformaLite(int Id, string ProformaId, string Status, DateTime UpdatedAt, decimal GrandTotal);
        private sealed record JobOrderLite(int Id, string JobOrderId, string Status, DateTime UpdatedAt);
        private sealed record DeliveryNoteSummary(int Id, string DeliveryNoteId, string Status, DateTime When, int Count);
        private sealed record TaxInvoiceSummary(
            int Id, string InvoiceNumber, string Status, DateTime When,
            decimal GrandTotal, int Count,
            decimal TotalBilled, decimal TotalOutstanding);
    }
}
