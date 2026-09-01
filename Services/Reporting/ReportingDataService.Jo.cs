// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/ReportingDataService.Jo.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.2 — ReportingDataService partial split. This part owns the Jo
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
        //  JO REPORT (DB-Side Filtered & Paginated)
        // ════════════════════════════════════════════════════════════════

        public async Task<JoReportSummary> GetJoSummaryAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new JobOrdersDbContext();
            var query = db.JobOrders.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.ProformaId))
                query = query.Where(j => j.SourceProformaId == filter.ProformaId);
            if (!string.IsNullOrWhiteSpace(filter.JobOrderId))
                query = query.Where(j => j.JobOrderId == filter.JobOrderId);
            if (!string.IsNullOrWhiteSpace(filter.Customer))
                query = query.Where(j => EF.Functions.Like(j.CustomerName, $"%{filter.Customer}%"));
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                var st = filter.Status.Trim();
                if (Eq(st, "In Production") || Eq(st, "InProduction"))
                    query = query.Where(j => j.Status == "InProduction" || j.Status == "In Production");
                else
                    query = query.Where(j => j.Status == st);
            }
            if (filter.DateFrom.HasValue)
                query = query.Where(j => j.DeliveryDate == null || j.DeliveryDate >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue)
                query = query.Where(j => j.DeliveryDate == null || j.DeliveryDate <= filter.DateTo.Value);

            if (filter.HasSearch)
            {
                var term = filter.SearchText.Trim();
                query = query.Where(j =>
                    EF.Functions.Like(j.JobOrderId, $"%{term}%") ||
                    EF.Functions.Like(j.SourceProformaId, $"%{term}%") ||
                    EF.Functions.Like(j.CustomerName, $"%{term}%"));
            }

            var summary = new JoReportSummary();
            summary.TotalJo = await query.CountAsync(ct);
            if (summary.TotalJo == 0) return summary;

            var totals = await query.Select(j => j.GrandTotalVatInclusive).ToListAsync(ct);
            summary.TotalValue = totals.Sum();

            var statusGroups = await query.GroupBy(j => j.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            foreach (var g in statusGroups)
            {
                var st = g.Status ?? "Unknown";
                var displayKey = Eq(st, "InProduction") ? "In Production" : st;
                summary.ByStatus[displayKey] = g.Count;
            }

            try
            {
                using var piDb = new ProformaInvoicesDbContext();
                var piCount = await piDb.ProformaInvoices.CountAsync(ct);
                summary.AwaitingJo = Math.Max(0, piCount - summary.TotalJo);
            }
            catch (Exception ex) { AppLogService.Warning($"Awaiting JO count warning: {ex.Message}"); }

            return summary;
        }

        public async Task<PagedResult<JoReportRow>> GetJoRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new JobOrdersDbContext();
            var query = db.JobOrders.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.ProformaId))
                query = query.Where(j => j.SourceProformaId == filter.ProformaId);
            if (!string.IsNullOrWhiteSpace(filter.JobOrderId))
                query = query.Where(j => j.JobOrderId == filter.JobOrderId);
            if (!string.IsNullOrWhiteSpace(filter.Customer))
                query = query.Where(j => EF.Functions.Like(j.CustomerName, $"%{filter.Customer}%"));
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                var st = filter.Status.Trim();
                if (Eq(st, "In Production") || Eq(st, "InProduction"))
                    query = query.Where(j => j.Status == "InProduction" || j.Status == "In Production");
                else
                    query = query.Where(j => j.Status == st);
            }
            if (filter.DateFrom.HasValue)
                query = query.Where(j => j.DeliveryDate == null || j.DeliveryDate >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue)
                query = query.Where(j => j.DeliveryDate == null || j.DeliveryDate <= filter.DateTo.Value);

            if (filter.HasSearch)
            {
                var term = filter.SearchText.Trim();
                query = query.Where(j =>
                    EF.Functions.Like(j.JobOrderId, $"%{term}%") ||
                    EF.Functions.Like(j.SourceProformaId, $"%{term}%") ||
                    EF.Functions.Like(j.CustomerName, $"%{term}%"));
            }

            var totalCount = await query.CountAsync(ct);
            if (totalCount == 0)
                return new PagedResult<JoReportRow> { Items = new List<JoReportRow>(), TotalCount = 0, Page = filter.Page, PageSize = filter.PageSize };

            // DB-side pagination
            var page = await query.OrderByDescending(j => j.CreatedAt)
                .Skip(filter.Skip)
                .Take(filter.Take)
                .Select(j => new
                {
                    j.Id, j.JobOrderId, j.SourceProformaId, j.CustomerName, j.Status, j.Priority, j.DeliveryDate,
                    j.GrandTotalVatInclusive, j.FullSnapshotJson, j.CreatedAt, j.UpdatedAt
                })
                .ToListAsync(ct);

            var pageJoIds = page.Select(x => x.JobOrderId).ToList();

            // Delivered quantity comes straight from the DeliveryNoteItems
            // table — the SUM of DeliveredQty across real (non-master) DNs.
            // This is the SAME canonical source the Delivery report uses.
            // Reading the Master DN's TotalDeliveredQty column was unreliable:
            // it is 0 whenever no Master DN exists or it has not been refreshed
            // after a dispatch, which made every JO show "delivered 0".
            Dictionary<string, int> deliveredByJo = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var dnDb = new DeliveryNotesDbContext();
                var realDnIds = await dnDb.DeliveryNotes.AsNoTracking()
                    .Where(d => !d.IsMasterDn && pageJoIds.Contains(d.JobOrderId))
                    .Select(d => d.DeliveryNoteId)
                    .ToListAsync(ct);
                var delivered = await dnDb.DeliveryNoteItems.AsNoTracking()
                    .Where(i => pageJoIds.Contains(i.JobOrderId) && realDnIds.Contains(i.DeliveryNoteId))
                    .Select(i => new { i.JobOrderId, i.DeliveredQty })
                    .ToListAsync(ct);
                foreach (var it in delivered)
                {
                    if (string.IsNullOrWhiteSpace(it.JobOrderId)) continue;
                    deliveredByJo[it.JobOrderId] =
                        deliveredByJo.TryGetValue(it.JobOrderId, out var n) ? n + it.DeliveredQty : it.DeliveredQty;
                }
            }
            catch (Exception ex) { AppLogService.Warning($"JO delivered-qty warning: {ex.Message}"); }

            var rows = new List<JoReportRow>();
            foreach (var r in page)
            {
                var snap = TryDeserializeJoSnapshot(r.FullSnapshotJson);
                var salesman = snap?.EmbeddedInvoice?.Client?.SalesRepresentative ?? "";
                var preparedBy = snap?.EmbeddedInvoice?.PreparedBy?.PreparedByName ?? "";
                var company = snap?.EmbeddedInvoice?.Company?.CompanyName ?? "";
                var project = snap?.EmbeddedInvoice?.Project?.ProjectIdentifierName ?? snap?.EmbeddedInvoice?.Project?.ProjectReference ?? "";
                int totalQty = 0; decimal totalSqm = 0;
                if (snap?.EmbeddedInvoice?.SpecGroups != null)
                {
                    foreach (var g in snap.EmbeddedInvoice.SpecGroups)
                    {
                        if (g?.ItemRows == null) continue;
                        foreach (var row in g.ItemRows)
                        {
                            if (string.IsNullOrWhiteSpace(row.GlassRef) && row.W1 == 0 && row.H1 == 0) continue;
                            totalQty += row.Qty;
                            totalSqm += row.TotalSqm;
                        }
                    }
                }
                int deliveredQty = deliveredByJo.TryGetValue(r.JobOrderId, out var d) ? d : 0;
                rows.Add(new JoReportRow
                {
                    Id = r.Id,
                    JobOrderId = r.JobOrderId,
                    SourceProformaId = r.SourceProformaId,
                    CompanyName = company,
                    CustomerName = r.CustomerName,
                    ProjectName = project,
                    Salesman = salesman,
                    PreparedBy = preparedBy,
                    Status = r.Status,
                    Priority = r.Priority,
                    DeliveryDate = r.DeliveryDate,
                    GrandTotal = r.GrandTotalVatInclusive,
                    TotalQty = totalQty,
                    TotalSqm = Math.Round(totalSqm, 3),
                    DeliveredQty = deliveredQty,
                    BalanceQty = Math.Max(0, totalQty - deliveredQty),
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                });
            }

            return new PagedResult<JoReportRow> { Items = rows, TotalCount = totalCount, Page = filter.Page, PageSize = filter.PageSize };
        }
    }
}
