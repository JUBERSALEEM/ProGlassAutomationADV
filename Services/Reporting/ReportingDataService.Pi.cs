// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/ReportingDataService.Pi.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.2 — ReportingDataService partial split. This part owns the Pi
// report family members; the class declaration + shared helpers live in
// ReportingDataService.cs.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.DeliveryNote.Database;
using ProGlassAutomation.Services.JobOrder.Database;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.MasterData.Database;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Services.Statement.Database;
using ProGlassAutomation.Services.TaxInvoice.Database;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Reporting
{
    public sealed partial class ReportingDataService : IReportingDataService
    {
        // ════════════════════════════════════════════════════════════════
        //  PI REPORT (DB-Side Filtered & Paginated)
        // ════════════════════════════════════════════════════════════════

        public async Task<PiReportSummary> GetPiSummaryAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            var summary = new PiReportSummary();

            using var db = new ProformaInvoicesDbContext();
            var query = Live(db.ProformaInvoices.AsNoTracking()).AsQueryable();

            query = ApplyPiFilters(query, filter);

            // DB-side counts & sums
            summary.TotalPi = await query.CountAsync(ct).ConfigureAwait(false);
            if (summary.TotalPi == 0) return summary;

            var totals = await query.Select(i => i.GrandTotalVatInclusive).ToListAsync(ct).ConfigureAwait(false);
            summary.TotalValue = totals.Sum();

            var statusGroups = await query.GroupBy(i => i.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct).ConfigureAwait(false);

            foreach (var g in statusGroups)
            {
                var st = g.Status ?? "";
                if (Eq(st, "Draft")) summary.Draft += g.Count;
                else if (Eq(st, "Released") || Eq(st, "Sent")) summary.Released += g.Count;
                else if (Eq(st, "Approved") || Eq(st, "Confirmed") || Eq(st, "Issued")) summary.Confirmed += g.Count;
                else if (Eq(st, "Rejected") || Eq(st, "Cancelled") || Eq(st, "Void")) summary.Rejected += g.Count;
            }
            summary.Pending = summary.Draft + summary.Released;

            // Check JO conversion count
            try
            {
                using var joDb = new JobOrdersDbContext();
                var piIds = await query.Select(i => i.ProformaId).ToListAsync(ct).ConfigureAwait(false);
                var convertedCount = await Live(joDb.JobOrders.AsNoTracking())
                    .Where(j => piIds.Contains(j.SourceProformaId))
                    .Select(j => j.SourceProformaId)
                    .Distinct()
                    .CountAsync(ct).ConfigureAwait(false);
                summary.Converted = convertedCount;
                summary.NotConverted = Math.Max(0, summary.TotalPi - convertedCount);
            }
            catch (Exception ex) { AppLogService.Warning($"Conversion count warning: {ex.Message}"); }

            // ── Quantity / area for the QUANTITY KPI card ───────────────
            // Pieces and m² live inside each PI's snapshot JSON (not
            // columns), so this is a bounded snapshot scan over the
            // filtered set — newest first, capped at 1000 like the
            // GlassSpec engine. Best effort: a failure leaves the card at
            // 0 instead of blanking the report. (2026-09-10 — the card
            // previously read a permanent 0: the fields were never
            // assigned anywhere.)
            try
            {
                var snapPage = await query
                    .OrderByDescending(i => i.CreatedAt)
                    .Take(1000)
                    .Select(i => new { i.ProformaId, i.CompanyName, i.CustomerName, i.IssueDate, i.Status, i.FullSnapshotJson })
                    .ToListAsync(ct).ConfigureAwait(false);

                long qtySum = 0;
                decimal sqmSum = 0m;
                foreach (var r in snapPage)
                {
                    var snap = TryDeserializePiSnapshot(r.FullSnapshotJson);
                    var glassRows = ExtractGlassRowsFromPiSnapshot(
                        snap ?? new ProformaInvoiceSnapshot(), r.ProformaId,
                        r.CompanyName, r.CustomerName,
                        snap?.Client?.SalesRepresentative ?? "", r.IssueDate, r.Status);
                    qtySum += glassRows.Sum(x => x.Qty);
                    sqmSum += glassRows.Sum(x => x.TotalSqm);
                }
                summary.TotalQty = (int)qtySum;
                summary.TotalSqm = Math.Round(sqmSum, 3);
            }
            catch (Exception ex) { AppLogService.Warning($"PI quantity summary warning: {ex.Message}"); }

            return summary;
        }

        /// <summary>
        /// THE proforma filter — single source of truth for how a
        /// ReportingFilter narrows the PI table. Used by the PI summary, the
        /// PI rows, the executive cohort funnel and the pending-invoice
        /// count, so none of them can drift apart again. (Extracted
        /// 2026-09-10 from two identical inline copies. DateTo compares
        /// against the true end of day — entity dates carry a time.)
        /// </summary>
        private static IQueryable<SavedProformaInvoice> ApplyPiFilters(
            IQueryable<SavedProformaInvoice> query, ReportingFilter filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.Company))
                query = query.Where(i => EF.Functions.Like(i.CompanyName, $"%{filter.Company}%"));
            if (!string.IsNullOrWhiteSpace(filter.Customer))
                query = query.Where(i => EF.Functions.Like(i.CustomerName, $"%{filter.Customer}%"));
            if (!string.IsNullOrWhiteSpace(filter.ProformaId))
                query = query.Where(i => i.ProformaId == filter.ProformaId);
            if (!string.IsNullOrWhiteSpace(filter.Status))
                query = query.Where(i => i.Status == filter.Status);
            if (!string.IsNullOrWhiteSpace(filter.Project))
                query = query.Where(i => i.ProjectName == filter.Project);
            if (filter.DateFrom.HasValue)
                query = query.Where(i => i.IssueDate >= filter.DateFrom.Value
                                         || (i.IssueDate == default && i.CreatedAt >= filter.DateFrom.Value));
            if (filter.DateTo.HasValue)
                query = query.Where(i => i.IssueDate <= DayEnd(filter.DateTo)
                                         || (i.IssueDate == default && i.CreatedAt <= DayEnd(filter.DateTo)));
            if (filter.HasSearch)
            {
                var term = filter.SearchText.Trim();
                query = query.Where(i =>
                    EF.Functions.Like(i.ProformaId, $"%{term}%") ||
                    EF.Functions.Like(i.CustomerName, $"%{term}%") ||
                    EF.Functions.Like(i.CompanyName, $"%{term}%") ||
                    EF.Functions.Like(i.ProjectName, $"%{term}%"));
            }
            return query;
        }

        public async Task<PagedResult<PiReportRow>> GetPiRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new ProformaInvoicesDbContext();

            var query = Live(db.ProformaInvoices.AsNoTracking()).AsQueryable();

            query = ApplyPiFilters(query, filter);

            var totalCount = await query.CountAsync(ct).ConfigureAwait(false);
            if (totalCount == 0)
                return new PagedResult<PiReportRow> { Items = new List<PiReportRow>(), TotalCount = 0, Page = filter.Page, PageSize = filter.PageSize };

            // User-selectable sort (applied server-side, before paging, so it
            // is correct across pages). Unknown keys fall back to Date.
            bool desc = filter.SortDescending;
            IOrderedQueryable<SavedProformaInvoice> ordered = (filter.SortField) switch
            {
                "Amount"   => desc ? query.OrderByDescending(i => i.GrandTotalVatInclusive) : query.OrderBy(i => i.GrandTotalVatInclusive),
                "Customer" => desc ? query.OrderByDescending(i => i.CustomerName) : query.OrderBy(i => i.CustomerName),
                "Company"  => desc ? query.OrderByDescending(i => i.CompanyName) : query.OrderBy(i => i.CompanyName),
                "Status"   => desc ? query.OrderByDescending(i => i.Status) : query.OrderBy(i => i.Status),
                "Created"  => desc ? query.OrderByDescending(i => i.CreatedAt) : query.OrderBy(i => i.CreatedAt),
                _          => desc ? query.OrderByDescending(i => i.IssueDate) : query.OrderBy(i => i.IssueDate),
            };

            // DB-side Skip & Take — only loads JSON for the 20-50 rows on current page!
            var pageItems = await ordered
                .Skip(filter.Skip)
                .Take(filter.Take)
                .Select(i => new
                {
                    i.Id, i.ProformaId, i.Status, i.CompanyName, i.CustomerName, i.ProjectName,
                    i.IssueDate, i.ValidUntil, i.GrandTotalVatInclusive, i.FullSnapshotJson, i.CreatedAt, i.UpdatedAt
                })
                .ToListAsync(ct).ConfigureAwait(false);

            var pagePiIds = pageItems.Select(x => x.ProformaId).ToList();

            // Fetch JO / DN / TI flags ONLY for page items
            HashSet<string> joSet = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> dnSet = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> tiSet = new(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var joDb = new JobOrdersDbContext();
                var jos = await Live(joDb.JobOrders.AsNoTracking())
                    .Where(j => pagePiIds.Contains(j.SourceProformaId))
                    .Select(j => j.SourceProformaId).ToListAsync(ct).ConfigureAwait(false);
                foreach (var id in jos.Where(s => !string.IsNullOrWhiteSpace(s))) joSet.Add(id!);
            }
            catch (Exception ex) { AppLogService.Warning($"JO flag warning: {ex.Message}"); }

            try
            {
                using var dnDb = new DeliveryNotesDbContext();
                var dns = await Live(dnDb.DeliveryNotes.AsNoTracking())
                    .Where(d => !d.IsMasterDn && pagePiIds.Contains(d.ProformaId))
                    .Select(d => d.ProformaId).ToListAsync(ct).ConfigureAwait(false);
                foreach (var id in dns.Where(s => !string.IsNullOrWhiteSpace(s))) dnSet.Add(id!);
            }
            catch (Exception ex) { AppLogService.Warning($"DN flag warning: {ex.Message}"); }

            try
            {
                using var tiDb = new TaxInvoiceDbContext();
                var tis = await Live(tiDb.TaxInvoices.AsNoTracking())
                    .Where(t => pagePiIds.Contains(t.ProformaId))
                    .Select(t => t.ProformaId).ToListAsync(ct).ConfigureAwait(false);
                foreach (var id in tis.Where(s => !string.IsNullOrWhiteSpace(s))) tiSet.Add(id!);
            }
            catch (Exception ex) { AppLogService.Warning($"TI flag warning: {ex.Message}"); }

            var resultRows = new List<PiReportRow>();
            foreach (var r in pageItems)
            {
                var snap = TryDeserializePiSnapshot(r.FullSnapshotJson);
                var rows = ExtractGlassRowsFromPiSnapshot(snap ?? new ProformaInvoiceSnapshot(), r.ProformaId, r.CompanyName, r.CustomerName, snap?.Client?.SalesRepresentative ?? "", r.IssueDate, r.Status);
                var salesman = snap?.Client?.SalesRepresentative ?? "";
                var preparedBy = snap?.PreparedBy?.PreparedByName ?? "";

                // The SPEC RATE(S) straight from this PI's priced rows —
                // what the user entered per m² in the quotation (a display
                // bug previously showed the AREA wearing a "/m²" mask).
                var rates = rows.Select(x => x.Rate).Where(v => v > 0m).Distinct().ToList();
                var rateDisplay = rates.Count == 1
                    ? $"AED {rates[0]:0.##} / m²"
                    : rates.Count > 1
                        ? $"AED {rates.Min():0.##}–{rates.Max():0.##} / m²"
                        : "";

                resultRows.Add(new PiReportRow
                {
                    Id = (int)r.Id,
                    RateDisplay = rateDisplay,
                    ProformaId = r.ProformaId,
                    Status = r.Status,
                    CompanyName = r.CompanyName,
                    CustomerName = r.CustomerName,
                    ProjectName = r.ProjectName,
                    Salesman = salesman,
                    PreparedBy = preparedBy,
                    IssueDate = r.IssueDate,
                    ValidUntil = r.ValidUntil,
                    GrandTotal = r.GrandTotalVatInclusive,
                    SpecGroupCount = snap?.SpecGroups?.Count ?? 0,
                    TotalRows = rows.Count,
                    TotalQty = rows.Sum(x => x.Qty),
                    TotalSqm = Math.Round(rows.Sum(x => x.TotalSqm), 3),
                    HasJobOrder = joSet.Contains(r.ProformaId),
                    HasDelivery = dnSet.Contains(r.ProformaId),
                    HasTaxInvoice = tiSet.Contains(r.ProformaId),
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                });
            }

            // Page-scoped Sr for the report's serial column (2026-09-10).
            for (int i = 0; i < resultRows.Count; i++)
                resultRows[i].SerialNumber = i + 1;

            return new PagedResult<PiReportRow> { Items = resultRows, TotalCount = totalCount, Page = filter.Page, PageSize = filter.PageSize };
        }

        public async Task<List<PiReportRow>> GetPiLifecycleAsync(string proformaId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(proformaId)) return new List<PiReportRow>();
            var filter = new ReportingFilter { ProformaId = proformaId.Trim(), PageSize = 100 };
            var res = await GetPiRowsAsync(filter, ct);
            return res.Items;
        }
    }
}
