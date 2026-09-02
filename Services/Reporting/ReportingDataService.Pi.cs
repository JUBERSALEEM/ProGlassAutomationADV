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
            var query = db.ProformaInvoices.AsNoTracking().AsQueryable();

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
                query = query.Where(i => i.IssueDate >= filter.DateFrom.Value || (i.IssueDate == default && i.CreatedAt >= filter.DateFrom.Value));
            if (filter.DateTo.HasValue)
                query = query.Where(i => i.IssueDate <= filter.DateTo.Value || (i.IssueDate == default && i.CreatedAt <= filter.DateTo.Value));

            if (filter.HasSearch)
            {
                var term = filter.SearchText.Trim();
                query = query.Where(i =>
                    EF.Functions.Like(i.ProformaId, $"%{term}%") ||
                    EF.Functions.Like(i.CustomerName, $"%{term}%") ||
                    EF.Functions.Like(i.CompanyName, $"%{term}%") ||
                    EF.Functions.Like(i.ProjectName, $"%{term}%"));
            }

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
                var convertedCount = await joDb.JobOrders.AsNoTracking()
                    .Where(j => piIds.Contains(j.SourceProformaId))
                    .Select(j => j.SourceProformaId)
                    .Distinct()
                    .CountAsync(ct).ConfigureAwait(false);
                summary.Converted = convertedCount;
                summary.NotConverted = Math.Max(0, summary.TotalPi - convertedCount);
            }
            catch (Exception ex) { AppLogService.Warning($"Conversion count warning: {ex.Message}"); }

            return summary;
        }

        public async Task<PagedResult<PiReportRow>> GetPiRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new ProformaInvoicesDbContext();

            var query = db.ProformaInvoices.AsNoTracking().AsQueryable();

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
                query = query.Where(i => i.IssueDate >= filter.DateFrom.Value || (i.IssueDate == default && i.CreatedAt >= filter.DateFrom.Value));
            if (filter.DateTo.HasValue)
                query = query.Where(i => i.IssueDate <= filter.DateTo.Value || (i.IssueDate == default && i.CreatedAt <= filter.DateTo.Value));

            if (filter.HasSearch)
            {
                var term = filter.SearchText.Trim();
                query = query.Where(i =>
                    EF.Functions.Like(i.ProformaId, $"%{term}%") ||
                    EF.Functions.Like(i.CustomerName, $"%{term}%") ||
                    EF.Functions.Like(i.CompanyName, $"%{term}%") ||
                    EF.Functions.Like(i.ProjectName, $"%{term}%"));
            }

            var totalCount = await query.CountAsync(ct).ConfigureAwait(false);
            if (totalCount == 0)
                return new PagedResult<PiReportRow> { Items = new List<PiReportRow>(), TotalCount = 0, Page = filter.Page, PageSize = filter.PageSize };

            // DB-side Skip & Take — only loads JSON for the 20-50 rows on current page!
            var pageItems = await query.OrderByDescending(i => i.CreatedAt)
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
                var jos = await joDb.JobOrders.AsNoTracking()
                    .Where(j => pagePiIds.Contains(j.SourceProformaId))
                    .Select(j => j.SourceProformaId).ToListAsync(ct).ConfigureAwait(false);
                foreach (var id in jos.Where(s => !string.IsNullOrWhiteSpace(s))) joSet.Add(id!);
            }
            catch (Exception ex) { AppLogService.Warning($"JO flag warning: {ex.Message}"); }

            try
            {
                using var dnDb = new DeliveryNotesDbContext();
                var dns = await dnDb.DeliveryNotes.AsNoTracking()
                    .Where(d => !d.IsMasterDn && pagePiIds.Contains(d.ProformaId))
                    .Select(d => d.ProformaId).ToListAsync(ct).ConfigureAwait(false);
                foreach (var id in dns.Where(s => !string.IsNullOrWhiteSpace(s))) dnSet.Add(id!);
            }
            catch (Exception ex) { AppLogService.Warning($"DN flag warning: {ex.Message}"); }

            try
            {
                using var tiDb = new TaxInvoiceDbContext();
                var tis = await tiDb.TaxInvoices.AsNoTracking()
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

                resultRows.Add(new PiReportRow
                {
                    Id = (int)r.Id,
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
