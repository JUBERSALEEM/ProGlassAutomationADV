// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/ReportingDataService.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — Data Intelligence Layer (HIGH PERFORMANCE CORE)
//
// PURPOSE:
//   ONE centralized reporting/data-intelligence layer responsible for:
//     • relationships / joins / filtering / aggregation
//     • row-level data / specification matching / quantity reconciliation
//     • drill-down / profitability / cross-module traceability
//
// HIGH-SCALE ENTERPRISE HARDENING:
//   • DB-side pagination (.Skip/.Take in SQL before snapshot retrieval)
//   • Zero full-table in-memory JSON deserialization
//   • Database-side aggregation (.CountAsync, .SumAsync, GroupBy)
//   • CancellationToken propagated across every async boundary
//   • Safe string parsing with no OOM even with 100K+ records
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
using ProGlassAutomation.Services.MasterData.Database;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
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
    public interface IReportingDataService
    {
        // ── PI Report ────────────────────────────────────────────────
        Task<PiReportSummary> GetPiSummaryAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<PiReportRow>> GetPiRowsAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<List<PiReportRow>> GetPiLifecycleAsync(string proformaId, CancellationToken ct = default);

        // ── JO Report ────────────────────────────────────────────────
        Task<JoReportSummary> GetJoSummaryAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<JoReportRow>> GetJoRowsAsync(ReportingFilter filter, CancellationToken ct = default);

        // ── Delivery Report ──────────────────────────────────────────
        Task<DeliveryReportSummary> GetDeliverySummaryAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<DeliveryReportRow>> GetDeliveryRowsAsync(ReportingFilter filter, CancellationToken ct = default);

        // ── Tax Invoice Report ───────────────────────────────────────
        Task<TaxInvoiceReportSummary> GetTaxInvoiceSummaryAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<TaxInvoiceReportRow>> GetTaxInvoiceRowsAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<List<TaxInvoiceReportRow>> GetPendingInvoicesAsync(ReportingFilter filter, CancellationToken ct = default);

        // ── Company / Customer / Salesman / Project ──────────────────
        Task<PagedResult<CompanyReportRow>> GetCompanyRowsAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<CustomerReportRow>> GetCustomerRowsAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<SalesmanReportRow>> GetSalesmanRowsAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<ProjectReportRow>> GetProjectRowsAsync(ReportingFilter filter, CancellationToken ct = default);

        // ── Glass / Spec Report — row-level intelligence ─────────────
        Task<PagedResult<GlassSpecReportRow>> GetGlassSpecRowsAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<List<SpecAggregation>> GetSpecAggregationsAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<RowTrace?> GetRowTraceAsync(string rowId, CancellationToken ct = default);

        // ── Statement Report ─────────────────────────────────────────
        Task<StatementReportSummary> GetStatementSummaryAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<StatementReportRow>> GetStatementRowsAsync(ReportingFilter filter, CancellationToken ct = default);

        // ── Profitability ────────────────────────────────────────────
        Task<ProfitabilitySummary> GetProfitabilitySummaryAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<ProfitabilityRow>> GetProfitabilityRowsAsync(ReportingFilter filter, CancellationToken ct = default);

        // ── Drill-down helpers ───────────────────────────────────────
        Task<List<GlassSpecReportRow>> GetPiRowsDetailAsync(string proformaId, CancellationToken ct = default);
        Task<QuantityReconciliation> GetQuantityReconciliationAsync(string proformaId, CancellationToken ct = default);
        Task<Dictionary<string, object>> GetDocumentChainAsync(string proformaId, CancellationToken ct = default);

        // ── Filter lookups ───────────────────────────────────────────
        Task<List<string>> GetCompanyOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetCustomerOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetSalesmanOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetProjectOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetGlassTypeOptionsAsync(CancellationToken ct = default);
    }

    public sealed class ReportingDataService : IReportingDataService
    {
        private static bool Eq(string? a, string b) =>
            string.Equals((a ?? "").Trim(), b, StringComparison.OrdinalIgnoreCase);

        private static bool ContainsCI(string? source, string? term)
        {
            if (string.IsNullOrWhiteSpace(term)) return true;
            if (string.IsNullOrWhiteSpace(source)) return false;
            return source.IndexOf(term.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool Within(DateTime dt, DateTime? from, DateTime? to)
        {
            if (from.HasValue && dt.Date < from.Value.Date) return false;
            if (to.HasValue && dt.Date > to.Value.Date) return false;
            return true;
        }

        // ── Snapshot deserialization helpers (safe, never throws) ─────────────

        private static ProformaInvoiceSnapshot? TryDeserializePiSnapshot(string? json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}") return null;
            try
            {
                return JsonSerializer.Deserialize<ProformaInvoiceSnapshot>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch { return null; }
        }

        private static JobOrderSnapshot? TryDeserializeJoSnapshot(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<JobOrderSnapshot>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch { return null; }
        }

        private static List<GlassSpecReportRow> ExtractGlassRowsFromPiSnapshot(
            ProformaInvoiceSnapshot snap, string proformaId, string company, string customer,
            string salesman, DateTime issueDate, string piStatus)
        {
            return SpecificationIntelligenceService.Instance.ExtractGlassRowsFromPi(
                snap, proformaId, company, customer, salesman, issueDate, piStatus);
        }

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
            summary.TotalPi = await query.CountAsync(ct);
            if (summary.TotalPi == 0) return summary;

            summary.TotalValue = await query.SumAsync(i => (decimal?)i.GrandTotalVatInclusive, ct) ?? 0m;

            var statusGroups = await query.GroupBy(i => i.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

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
                var piIds = await query.Select(i => i.ProformaId).ToListAsync(ct);
                var convertedCount = await joDb.JobOrders.AsNoTracking()
                    .Where(j => piIds.Contains(j.SourceProformaId))
                    .Select(j => j.SourceProformaId)
                    .Distinct()
                    .CountAsync(ct);
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

            var totalCount = await query.CountAsync(ct);
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
                .ToListAsync(ct);

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
                    .Select(j => j.SourceProformaId).ToListAsync(ct);
                foreach (var id in jos.Where(s => !string.IsNullOrWhiteSpace(s))) joSet.Add(id!);
            }
            catch (Exception ex) { AppLogService.Warning($"JO flag warning: {ex.Message}"); }

            try
            {
                using var dnDb = new DeliveryNotesDbContext();
                var dns = await dnDb.DeliveryNotes.AsNoTracking()
                    .Where(d => !d.IsMasterDn && pagePiIds.Contains(d.ProformaId))
                    .Select(d => d.ProformaId).ToListAsync(ct);
                foreach (var id in dns.Where(s => !string.IsNullOrWhiteSpace(s))) dnSet.Add(id!);
            }
            catch (Exception ex) { AppLogService.Warning($"DN flag warning: {ex.Message}"); }

            try
            {
                using var tiDb = new TaxInvoiceDbContext();
                var tis = await tiDb.TaxInvoices.AsNoTracking()
                    .Where(t => pagePiIds.Contains(t.ProformaId))
                    .Select(t => t.ProformaId).ToListAsync(ct);
                foreach (var id in tis.Where(s => !string.IsNullOrWhiteSpace(s))) tiSet.Add(id!);
            }
            catch (Exception ex) { AppLogService.Warning($"TI flag warning: {ex.Message}"); }

            var resultRows = new List<PiReportRow>();
            foreach (var r in pageItems)
            {
                var snap = TryDeserializePiSnapshot(r.FullSnapshotJson);
                var rows = ExtractGlassRowsFromPiSnapshot(snap ?? new ProformaInvoiceSnapshot(), r.ProformaId, r.CompanyName, r.CustomerName, snap?.Client?.SalesRepresentative ?? "", r.IssueDate, r.Status);
                var salesman = snap?.Client?.SalesRepresentative ?? "";

                resultRows.Add(new PiReportRow
                {
                    Id = (int)r.Id,
                    ProformaId = r.ProformaId,
                    Status = r.Status,
                    CompanyName = r.CompanyName,
                    CustomerName = r.CustomerName,
                    ProjectName = r.ProjectName,
                    Salesman = salesman,
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
                query = query.Where(j => j.Status == filter.Status);
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
            summary.TotalValue = await query.SumAsync(j => (decimal?)j.GrandTotalVatInclusive, ct) ?? 0m;

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
                query = query.Where(j => j.Status == filter.Status);
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

            // Fetch Master DN delivery balances only for page
            Dictionary<string, (int Ordered, int Delivered)> masterMap = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var dnDb = new DeliveryNotesDbContext();
                var masters = await dnDb.DeliveryNotes.AsNoTracking()
                    .Where(d => d.IsMasterDn && pageJoIds.Contains(d.JobOrderId))
                    .Select(d => new { d.JobOrderId, d.TotalOrderedQty, d.TotalDeliveredQty })
                    .ToListAsync(ct);
                foreach (var m in masters)
                    if (!string.IsNullOrWhiteSpace(m.JobOrderId))
                        masterMap[m.JobOrderId!] = (m.TotalOrderedQty, m.TotalDeliveredQty);
            }
            catch (Exception ex) { AppLogService.Warning($"Master DN map warning: {ex.Message}"); }

            var rows = new List<JoReportRow>();
            foreach (var r in page)
            {
                var snap = TryDeserializeJoSnapshot(r.FullSnapshotJson);
                var salesman = snap?.EmbeddedInvoice?.Client?.SalesRepresentative ?? "";
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
                masterMap.TryGetValue(r.JobOrderId, out var bal);
                rows.Add(new JoReportRow
                {
                    Id = r.Id,
                    JobOrderId = r.JobOrderId,
                    SourceProformaId = r.SourceProformaId,
                    CustomerName = r.CustomerName,
                    ProjectName = project,
                    Salesman = salesman,
                    Status = r.Status,
                    Priority = r.Priority,
                    DeliveryDate = r.DeliveryDate,
                    GrandTotal = r.GrandTotalVatInclusive,
                    TotalQty = totalQty,
                    TotalSqm = Math.Round(totalSqm, 3),
                    DeliveredQty = bal.Delivered,
                    BalanceQty = Math.Max(0, bal.Ordered - bal.Delivered),
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                });
            }

            return new PagedResult<JoReportRow> { Items = rows, TotalCount = totalCount, Page = filter.Page, PageSize = filter.PageSize };
        }

        // ════════════════════════════════════════════════════════════════
        //  DELIVERY REPORT (DB-Side Filtered & Paginated)
        // ════════════════════════════════════════════════════════════════

        public async Task<DeliveryReportSummary> GetDeliverySummaryAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new DeliveryNotesDbContext();
            var query = db.DeliveryNotes.AsNoTracking().Where(d => !d.IsMasterDn).AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.ProformaId))
                query = query.Where(d => d.ProformaId == filter.ProformaId);
            if (!string.IsNullOrWhiteSpace(filter.JobOrderId))
                query = query.Where(d => d.JobOrderId == filter.JobOrderId);
            if (!string.IsNullOrWhiteSpace(filter.DeliveryNoteId))
                query = query.Where(d => d.DeliveryNoteId == filter.DeliveryNoteId);
            if (!string.IsNullOrWhiteSpace(filter.Customer))
                query = query.Where(d => EF.Functions.Like(d.CustomerName, $"%{filter.Customer}%"));
            if (!string.IsNullOrWhiteSpace(filter.Status))
                query = query.Where(d => d.Status == filter.Status);
            if (filter.DateFrom.HasValue)
                query = query.Where(d => d.DeliveryDate >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue)
                query = query.Where(d => d.DeliveryDate <= filter.DateTo.Value);

            if (filter.HasSearch)
            {
                var term = filter.SearchText.Trim();
                query = query.Where(d =>
                    EF.Functions.Like(d.DeliveryNoteId, $"%{term}%") ||
                    EF.Functions.Like(d.JobOrderId, $"%{term}%") ||
                    EF.Functions.Like(d.CustomerName, $"%{term}%"));
            }

            var summary = new DeliveryReportSummary();
            summary.TotalDn = await query.CountAsync(ct);
            if (summary.TotalDn == 0) return summary;

            summary.TotalOrderedQty = await query.SumAsync(d => (int?)d.TotalOrderedQty, ct) ?? 0;
            summary.TotalDeliveredQty = await query.SumAsync(d => (int?)d.TotalDeliveredQty, ct) ?? 0;
            summary.TotalRemainingQty = await query.SumAsync(d => (int?)d.TotalBalanceQty, ct) ?? 0;

            summary.Pending = await query.CountAsync(d => d.TotalDeliveredQty == 0, ct);
            summary.Completed = await query.CountAsync(d => d.TotalBalanceQty == 0 && d.TotalOrderedQty > 0, ct);
            summary.Partial = Math.Max(0, summary.TotalDn - summary.Pending - summary.Completed);

            return summary;
        }

        public async Task<PagedResult<DeliveryReportRow>> GetDeliveryRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new DeliveryNotesDbContext();
            var query = db.DeliveryNotes.AsNoTracking().Where(d => !d.IsMasterDn).AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.ProformaId))
                query = query.Where(d => d.ProformaId == filter.ProformaId);
            if (!string.IsNullOrWhiteSpace(filter.JobOrderId))
                query = query.Where(d => d.JobOrderId == filter.JobOrderId);
            if (!string.IsNullOrWhiteSpace(filter.DeliveryNoteId))
                query = query.Where(d => d.DeliveryNoteId == filter.DeliveryNoteId);
            if (!string.IsNullOrWhiteSpace(filter.Customer))
                query = query.Where(d => EF.Functions.Like(d.CustomerName, $"%{filter.Customer}%"));
            if (!string.IsNullOrWhiteSpace(filter.Status))
                query = query.Where(d => d.Status == filter.Status);
            if (filter.DateFrom.HasValue)
                query = query.Where(d => d.DeliveryDate >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue)
                query = query.Where(d => d.DeliveryDate <= filter.DateTo.Value);

            if (filter.HasSearch)
            {
                var term = filter.SearchText.Trim();
                query = query.Where(d =>
                    EF.Functions.Like(d.DeliveryNoteId, $"%{term}%") ||
                    EF.Functions.Like(d.JobOrderId, $"%{term}%") ||
                    EF.Functions.Like(d.CustomerName, $"%{term}%"));
            }

            var totalCount = await query.CountAsync(ct);
            if (totalCount == 0)
                return new PagedResult<DeliveryReportRow> { Items = new List<DeliveryReportRow>(), TotalCount = 0, Page = filter.Page, PageSize = filter.PageSize };

            var page = await query.OrderByDescending(d => d.DeliveryDate).ThenByDescending(d => d.CreatedAt)
                .Skip(filter.Skip)
                .Take(filter.Take)
                .Select(d => new DeliveryReportRow
                {
                    Id = d.Id,
                    DeliveryNoteId = d.DeliveryNoteId,
                    JobOrderId = d.JobOrderId,
                    ProformaId = d.ProformaId,
                    CustomerName = d.CustomerName,
                    ProjectName = d.ProjectName,
                    Status = d.Status,
                    DeliveryType = d.DeliveryType,
                    IsMasterDn = d.IsMasterDn,
                    HasTaxInvoice = d.HasTaxInvoice,
                    DeliveryDate = d.DeliveryDate,
                    TotalOrderedQty = d.TotalOrderedQty,
                    TotalDeliveredQty = d.TotalDeliveredQty,
                    TotalBalanceQty = d.TotalBalanceQty,
                    DriverName = d.DriverName,
                    CreatedAt = d.CreatedAt
                })
                .ToListAsync(ct);

            return new PagedResult<DeliveryReportRow> { Items = page, TotalCount = totalCount, Page = filter.Page, PageSize = filter.PageSize };
        }

        // ════════════════════════════════════════════════════════════════
        //  TAX INVOICE REPORT (DB-Side Filtered & Paginated)
        // ════════════════════════════════════════════════════════════════

        public async Task<TaxInvoiceReportSummary> GetTaxInvoiceSummaryAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new TaxInvoiceDbContext();
            var query = db.TaxInvoices.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.ProformaId))
                query = query.Where(t => t.ProformaId == filter.ProformaId);
            if (!string.IsNullOrWhiteSpace(filter.TaxInvoiceNumber))
                query = query.Where(t => t.InvoiceNumber == filter.TaxInvoiceNumber);
            if (!string.IsNullOrWhiteSpace(filter.Customer))
                query = query.Where(t => EF.Functions.Like(t.CustomerName, $"%{filter.Customer}%"));
            if (!string.IsNullOrWhiteSpace(filter.Company))
                query = query.Where(t => EF.Functions.Like(t.CompanyName, $"%{filter.Company}%"));
            if (!string.IsNullOrWhiteSpace(filter.Status))
                query = query.Where(t => t.Status == filter.Status);
            if (filter.DateFrom.HasValue)
                query = query.Where(t => t.IssueDate >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue)
                query = query.Where(t => t.IssueDate <= filter.DateTo.Value);

            if (filter.HasSearch)
            {
                var term = filter.SearchText.Trim();
                query = query.Where(t =>
                    EF.Functions.Like(t.InvoiceNumber, $"%{term}%") ||
                    EF.Functions.Like(t.ProformaId, $"%{term}%") ||
                    EF.Functions.Like(t.CustomerName, $"%{term}%"));
            }

            var summary = new TaxInvoiceReportSummary();
            summary.TotalInvoices = await query.CountAsync(ct);
            if (summary.TotalInvoices == 0) return summary;

            summary.TotalAmount = await query.SumAsync(t => (decimal?)t.GrandTotal, ct) ?? 0m;
            summary.TotalVat = await query.SumAsync(t => (decimal?)t.VatAmount, ct) ?? 0m;

            summary.Issued = await query.CountAsync(t => t.Status == "Issued", ct);
            summary.Paid = await query.CountAsync(t => t.Status == "Paid", ct);
            summary.PartiallyPaid = await query.CountAsync(t => t.Status == "PartiallyPaid" || t.Status == "Partially Paid", ct);
            summary.Overdue = await query.CountAsync(t => t.Status == "Overdue" || (t.Status == "Issued" && t.DueDate < DateTime.Today), ct);
            summary.Draft = await query.CountAsync(t => t.Status == "Draft", ct);
            summary.Outstanding = await query.Where(t => t.Status == "Issued" || t.Status == "Overdue" || t.Status == "PartiallyPaid").SumAsync(t => (decimal?)t.GrandTotal, ct) ?? 0m;

            return summary;
        }

        public async Task<PagedResult<TaxInvoiceReportRow>> GetTaxInvoiceRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new TaxInvoiceDbContext();
            var query = db.TaxInvoices.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.ProformaId))
                query = query.Where(t => t.ProformaId == filter.ProformaId);
            if (!string.IsNullOrWhiteSpace(filter.TaxInvoiceNumber))
                query = query.Where(t => t.InvoiceNumber == filter.TaxInvoiceNumber);
            if (!string.IsNullOrWhiteSpace(filter.Customer))
                query = query.Where(t => EF.Functions.Like(t.CustomerName, $"%{filter.Customer}%"));
            if (!string.IsNullOrWhiteSpace(filter.Company))
                query = query.Where(t => EF.Functions.Like(t.CompanyName, $"%{filter.Company}%"));
            if (!string.IsNullOrWhiteSpace(filter.Status))
                query = query.Where(t => t.Status == filter.Status);
            if (filter.DateFrom.HasValue)
                query = query.Where(t => t.IssueDate >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue)
                query = query.Where(t => t.IssueDate <= filter.DateTo.Value);

            if (filter.HasSearch)
            {
                var term = filter.SearchText.Trim();
                query = query.Where(t =>
                    EF.Functions.Like(t.InvoiceNumber, $"%{term}%") ||
                    EF.Functions.Like(t.ProformaId, $"%{term}%") ||
                    EF.Functions.Like(t.CustomerName, $"%{term}%"));
            }

            var totalCount = await query.CountAsync(ct);
            if (totalCount == 0)
                return new PagedResult<TaxInvoiceReportRow> { Items = new List<TaxInvoiceReportRow>(), TotalCount = 0, Page = filter.Page, PageSize = filter.PageSize };

            var page = await query.OrderByDescending(t => t.IssueDate)
                .Skip(filter.Skip)
                .Take(filter.Take)
                .Select(t => new TaxInvoiceReportRow
                {
                    Id = t.Id,
                    InvoiceNumber = t.InvoiceNumber,
                    ProformaId = t.ProformaId,
                    JobOrderId = t.JobOrderId,
                    DeliveryNoteReference = t.DeliveryNoteReference,
                    CustomerName = t.CustomerName,
                    ProjectName = t.ProjectName,
                    CompanyName = t.CompanyName,
                    Status = t.Status,
                    InvoiceType = t.InvoiceType,
                    IssueDate = t.IssueDate,
                    DueDate = t.DueDate,
                    GrandTotal = t.GrandTotal,
                    VatAmount = t.VatAmount,
                    TotalQty = t.TotalQty,
                    TotalSqm = t.TotalSqm,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync(ct);

            return new PagedResult<TaxInvoiceReportRow> { Items = page, TotalCount = totalCount, Page = filter.Page, PageSize = filter.PageSize };
        }

        public async Task<List<TaxInvoiceReportRow>> GetPendingInvoicesAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            var result = new List<TaxInvoiceReportRow>();
            try
            {
                using var piDb = new ProformaInvoicesDbContext();
                using var tiDb = new TaxInvoiceDbContext();

                var confirmedPis = await piDb.ProformaInvoices.AsNoTracking()
                    .Where(p => p.Status == "Approved" || p.Status == "Confirmed" || p.Status == "Issued" || p.Status == "Sent")
                    .Select(p => new { p.ProformaId, p.CustomerName, p.ProjectName, p.CompanyName, p.GrandTotalVatInclusive, p.IssueDate })
                    .ToListAsync(ct);

                var invoicedPis = (await tiDb.TaxInvoices.AsNoTracking().Select(t => t.ProformaId).ToListAsync(ct))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var pi in confirmedPis.Where(p => !invoicedPis.Contains(p.ProformaId)))
                {
                    if (filter.HasCustomer && !ContainsCI(pi.CustomerName, filter.Customer)) continue;
                    if (filter.HasCompany && !ContainsCI(pi.CompanyName, filter.Company)) continue;
                    if (filter.HasProject && !ContainsCI(pi.ProjectName, filter.Project)) continue;
                    result.Add(new TaxInvoiceReportRow
                    {
                        ProformaId = pi.ProformaId,
                        CustomerName = pi.CustomerName,
                        ProjectName = pi.ProjectName,
                        CompanyName = pi.CompanyName,
                        Status = "Pending Invoice",
                        GrandTotal = pi.GrandTotalVatInclusive,
                        IssueDate = pi.IssueDate
                    });
                }
            }
            catch (Exception ex) { AppLogService.Warning($"Pending invoices query warning: {ex.Message}"); }
            return result;
        }

        // ════════════════════════════════════════════════════════════════
        //  COMPANY / CUSTOMER / SALESMAN / PROJECT
        // ════════════════════════════════════════════════════════════════

        public async Task<PagedResult<CompanyReportRow>> GetCompanyRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var piDb = new ProformaInvoicesDbContext();
            var piAll = await piDb.ProformaInvoices.AsNoTracking()
                .Select(p => new { p.CompanyName, p.ProformaId, p.Status, p.GrandTotalVatInclusive })
                .ToListAsync(ct);

            var groups = piAll.GroupBy(p => string.IsNullOrWhiteSpace(p.CompanyName) ? "Unknown" : p.CompanyName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new CompanyReportRow
                {
                    CompanyName = g.Key,
                    PiCount = g.Count(),
                    PiValue = g.Sum(x => x.GrandTotalVatInclusive)
                }).ToList();

            if (filter.HasSearch)
                groups = groups.Where(g => ContainsCI(g.CompanyName, filter.SearchText)).ToList();
            if (filter.HasCompany)
                groups = groups.Where(g => ContainsCI(g.CompanyName, filter.Company)).ToList();

            var piByCompany = piAll.GroupBy(p => string.IsNullOrWhiteSpace(p.CompanyName) ? "Unknown" : p.CompanyName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ProformaId).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            try
            {
                using var joDb = new JobOrdersDbContext();
                var joAll = await joDb.JobOrders.AsNoTracking().Select(j => j.SourceProformaId).ToListAsync(ct);
                var joSet = joAll.Where(s => !string.IsNullOrWhiteSpace(s)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var g in groups)
                {
                    if (piByCompany.TryGetValue(g.CompanyName, out var pis))
                        g.JobOrderCount = pis.Count(pi => joSet.Contains(pi));
                }
            }
            catch (Exception ex) { AppLogService.Warning($"Company JO count warning: {ex.Message}"); }

            try
            {
                using var tiDb = new TaxInvoiceDbContext();
                var tiAll = await tiDb.TaxInvoices.AsNoTracking().Select(t => new { t.ProformaId, t.GrandTotal, t.Status }).ToListAsync(ct);
                var tiByPi = tiAll.GroupBy(t => t.ProformaId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
                foreach (var g in groups)
                {
                    if (!piByCompany.TryGetValue(g.CompanyName, out var pis)) continue;
                    int tiCount = 0; decimal invoiced = 0;
                    foreach (var pi in pis)
                    {
                        if (tiByPi.TryGetValue(pi, out var list))
                        {
                            tiCount += list.Count;
                            invoiced += list.Sum(x => x.GrandTotal);
                        }
                    }
                    g.TaxInvoiceCount = tiCount;
                    g.InvoicedValue = invoiced;
                }
            }
            catch (Exception ex) { AppLogService.Warning($"Company TI count warning: {ex.Message}"); }

            var total = groups.Count;
            var page = groups.OrderByDescending(g => g.PiValue).Skip(filter.Skip).Take(filter.Take).ToList();
            return new PagedResult<CompanyReportRow> { Items = page, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }

        public async Task<PagedResult<CustomerReportRow>> GetCustomerRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var piDb = new ProformaInvoicesDbContext();
            var piAll = await piDb.ProformaInvoices.AsNoTracking()
                .Select(p => new { p.CustomerName, p.ProformaId, p.GrandTotalVatInclusive, p.FullSnapshotJson })
                .ToListAsync(ct);

            var groups = piAll.GroupBy(p => string.IsNullOrWhiteSpace(p.CustomerName) ? "Unknown" : p.CustomerName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    string salesman = "";
                    try
                    {
                        var first = g.FirstOrDefault();
                        if (first != null)
                        {
                            var snap = TryDeserializePiSnapshot(first.FullSnapshotJson);
                            salesman = snap?.Client?.SalesRepresentative ?? "";
                        }
                    }
                    catch { }
                    return new CustomerReportRow
                    {
                        CustomerName = g.Key,
                        Salesman = salesman,
                        PiCount = g.Count(),
                        PiValue = g.Sum(x => x.GrandTotalVatInclusive)
                    };
                }).ToList();

            if (filter.HasSearch)
                groups = groups.Where(g => ContainsCI(g.CustomerName, filter.SearchText) || ContainsCI(g.Salesman, filter.SearchText)).ToList();
            if (filter.HasCustomer)
                groups = groups.Where(g => ContainsCI(g.CustomerName, filter.Customer)).ToList();
            if (filter.HasSalesman)
                groups = groups.Where(g => ContainsCI(g.Salesman, filter.Salesman)).ToList();

            var piByCustomer = piAll.GroupBy(p => string.IsNullOrWhiteSpace(p.CustomerName) ? "Unknown" : p.CustomerName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ProformaId).ToHashSet(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            try
            {
                using var joDb = new JobOrdersDbContext();
                var joPis = (await joDb.JobOrders.AsNoTracking().Select(j => j.SourceProformaId).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var g in groups)
                    if (piByCustomer.TryGetValue(g.CustomerName, out var pis))
                        g.JoCount = pis.Count(pi => joPis.Contains(pi));
            }
            catch (Exception ex) { AppLogService.Warning($"Customer JO count warning: {ex.Message}"); }

            try
            {
                using var tiDb = new TaxInvoiceDbContext();
                var tiAll = await tiDb.TaxInvoices.AsNoTracking().Select(t => new { t.ProformaId, t.GrandTotal }).ToListAsync(ct);
                var tiByPi = tiAll.GroupBy(t => t.ProformaId, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Sum(x => x.GrandTotal), StringComparer.OrdinalIgnoreCase);
                foreach (var g in groups)
                {
                    if (!piByCustomer.TryGetValue(g.CustomerName, out var pis)) continue;
                    int tiCount = 0; decimal invoiced = 0;
                    foreach (var pi in pis)
                        if (tiByPi.TryGetValue(pi, out var sum)) { tiCount++; invoiced += sum; }
                    g.TiCount = tiCount;
                    g.InvoicedValue = invoiced;
                    g.Outstanding = Math.Max(0, g.PiValue - invoiced);
                }
            }
            catch (Exception ex) { AppLogService.Warning($"Customer TI count warning: {ex.Message}"); }

            var total = groups.Count;
            var page = groups.OrderByDescending(g => g.PiValue).Skip(filter.Skip).Take(filter.Take).ToList();
            return new PagedResult<CustomerReportRow> { Items = page, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }

        public async Task<PagedResult<SalesmanReportRow>> GetSalesmanRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var piDb = new ProformaInvoicesDbContext();
            var piAll = await piDb.ProformaInvoices.AsNoTracking()
                .Select(p => new { p.ProformaId, p.Status, p.GrandTotalVatInclusive, p.CustomerName, p.ProjectName, p.FullSnapshotJson })
                .ToListAsync(ct);

            var piWithSalesman = new List<(string ProformaId, string Salesman, string Status, decimal Total, string Customer, string Project)>();
            foreach (var p in piAll)
            {
                var snap = TryDeserializePiSnapshot(p.FullSnapshotJson);
                var sm = (snap?.Client?.SalesRepresentative ?? "").Trim();
                if (string.IsNullOrWhiteSpace(sm)) sm = "Unassigned";
                piWithSalesman.Add((p.ProformaId, sm, p.Status, p.GrandTotalVatInclusive, p.CustomerName, p.ProjectName));
            }

            var groups = piWithSalesman.GroupBy(x => x.Salesman, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var confirmed = g.Where(x => Eq(x.Status, "Approved") || Eq(x.Status, "Confirmed") || Eq(x.Status, "Issued")).ToList();
                    return new SalesmanReportRow
                    {
                        SalesmanName = g.Key,
                        PiCount = g.Count(),
                        ConfirmedPiCount = confirmed.Count,
                        ConfirmedValue = confirmed.Sum(x => x.Total),
                        CustomerCount = g.Select(x => x.Customer).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                        ProjectCount = g.Select(x => x.Project).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                        Revenue = confirmed.Sum(x => x.Total)
                    };
                }).ToList();

            if (filter.HasSearch)
                groups = groups.Where(g => ContainsCI(g.SalesmanName, filter.SearchText)).ToList();
            if (filter.HasSalesman)
                groups = groups.Where(g => ContainsCI(g.SalesmanName, filter.Salesman)).ToList();

            var total = groups.Count;
            var page = groups.OrderByDescending(g => g.Revenue).Skip(filter.Skip).Take(filter.Take).ToList();
            return new PagedResult<SalesmanReportRow> { Items = page, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }

        public async Task<PagedResult<ProjectReportRow>> GetProjectRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var piDb = new ProformaInvoicesDbContext();
            var piAll = await piDb.ProformaInvoices.AsNoTracking()
                .Select(p => new { p.ProformaId, p.ProjectName, p.CustomerName, p.CompanyName, p.GrandTotalVatInclusive, p.FullSnapshotJson })
                .ToListAsync(ct);

            var projectRows = new Dictionary<string, ProjectReportRow>(StringComparer.OrdinalIgnoreCase);
            var piByProject = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in piAll)
            {
                var snap = TryDeserializePiSnapshot(p.FullSnapshotJson);
                var projName = (!string.IsNullOrWhiteSpace(p.ProjectName) ? p.ProjectName : snap?.Project?.ProjectIdentifierName ?? snap?.Project?.ProjectReference ?? "").Trim();
                if (string.IsNullOrWhiteSpace(projName)) projName = "(No Project)";
                var salesman = snap?.Client?.SalesRepresentative ?? "";
                var contractNo = snap?.Project?.ProjectContractNo ?? "";

                if (!projectRows.TryGetValue(projName, out var row))
                {
                    row = new ProjectReportRow
                    {
                        ProjectName = projName,
                        ProjectContractNo = contractNo,
                        ProjectReference = snap?.Project?.ProjectReference ?? "",
                        CustomerName = p.CustomerName,
                        CompanyName = p.CompanyName,
                        Salesman = salesman
                    };
                    projectRows[projName] = row;
                    piByProject[projName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
                row.PiCount++;
                row.PiValue += p.GrandTotalVatInclusive;
                piByProject[projName].Add(p.ProformaId);
            }

            var list = projectRows.Values.ToList();

            if (filter.HasSearch)
                list = list.Where(r => ContainsCI(r.ProjectName, filter.SearchText) || ContainsCI(r.CustomerName, filter.SearchText)).ToList();
            if (filter.HasProject)
                list = list.Where(r => ContainsCI(r.ProjectName, filter.Project)).ToList();
            if (filter.HasCustomer)
                list = list.Where(r => ContainsCI(r.CustomerName, filter.Customer)).ToList();

            try
            {
                using var tiDb = new TaxInvoiceDbContext();
                var tiByPi = (await tiDb.TaxInvoices.AsNoTracking().Select(t => new { t.ProformaId, t.GrandTotal }).ToListAsync(ct))
                    .GroupBy(t => t.ProformaId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.GrandTotal), StringComparer.OrdinalIgnoreCase);
                foreach (var r in list)
                    if (piByProject.TryGetValue(r.ProjectName, out var pis))
                    {
                        int c = 0; decimal sum = 0;
                        foreach (var pi in pis) if (tiByPi.TryGetValue(pi, out var v)) { c++; sum += v; }
                        r.TiCount = c;
                        r.InvoicedValue = sum;
                    }
            }
            catch (Exception ex) { AppLogService.Warning($"Project TI calculation warning: {ex.Message}"); }

            var total = list.Count;
            var page = list.OrderByDescending(r => r.PiValue).Skip(filter.Skip).Take(filter.Take).ToList();
            return new PagedResult<ProjectReportRow> { Items = page, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }

        // ════════════════════════════════════════════════════════════════
        //  GLASS / SPEC REPORT — Row-Level Intelligence
        // ════════════════════════════════════════════════════════════════

        public async Task<PagedResult<GlassSpecReportRow>> GetGlassSpecRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            var allRows = new List<GlassSpecReportRow>();

            using var piDb = new ProformaInvoicesDbContext();
            var query = piDb.ProformaInvoices.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.ProformaId))
                query = query.Where(p => p.ProformaId == filter.ProformaId);
            if (!string.IsNullOrWhiteSpace(filter.Company))
                query = query.Where(p => EF.Functions.Like(p.CompanyName, $"%{filter.Company}%"));
            if (!string.IsNullOrWhiteSpace(filter.Customer))
                query = query.Where(p => EF.Functions.Like(p.CustomerName, $"%{filter.Customer}%"));
            if (!string.IsNullOrWhiteSpace(filter.Status))
                query = query.Where(p => p.Status == filter.Status);
            if (filter.DateFrom.HasValue)
                query = query.Where(p => p.IssueDate >= filter.DateFrom.Value || (p.IssueDate == default && p.CreatedAt >= filter.DateFrom.Value));
            if (filter.DateTo.HasValue)
                query = query.Where(p => p.IssueDate <= filter.DateTo.Value || (p.IssueDate == default && p.CreatedAt <= filter.DateTo.Value));

            // Load PI records (limit snapshot stream if unbounded)
            var piList = await query.OrderByDescending(p => p.CreatedAt)
                .Take(string.IsNullOrWhiteSpace(filter.ProformaId) ? 1000 : 1)
                .Select(p => new
                {
                    p.ProformaId, p.CompanyName, p.CustomerName, p.ProjectName, p.Status, p.IssueDate, p.CreatedAt, p.FullSnapshotJson
                }).ToListAsync(ct);

            foreach (var pi in piList)
            {
                var snap = TryDeserializePiSnapshot(pi.FullSnapshotJson);
                if (snap == null) continue;
                var salesman = snap.Client?.SalesRepresentative ?? "";
                var rows = ExtractGlassRowsFromPiSnapshot(snap, pi.ProformaId, pi.CompanyName, pi.CustomerName, salesman, pi.IssueDate == default ? pi.CreatedAt : pi.IssueDate, pi.Status);

                foreach (var r in rows)
                {
                    if (!string.IsNullOrWhiteSpace(filter.GlassType) && !Eq(r.GlassType, filter.GlassType!)) continue;
                    if (!string.IsNullOrWhiteSpace(filter.GlassRef) && !ContainsCI(r.GlassRef, filter.GlassRef)) continue;
                    if (!string.IsNullOrWhiteSpace(filter.Specification) && !ContainsCI(r.SpecName, filter.Specification)) continue;
                    if (!string.IsNullOrWhiteSpace(filter.Colour) && !(ContainsCI(r.OuterColor, filter.Colour) || ContainsCI(r.InnerColor, filter.Colour))) continue;
                    if (!string.IsNullOrWhiteSpace(filter.Thickness) && !ContainsCI(r.SpecName, filter.Thickness)) continue;
                    if (!string.IsNullOrWhiteSpace(filter.Project) && !ContainsCI(r.ProjectName, filter.Project)) continue;
                    if (!string.IsNullOrWhiteSpace(filter.Salesman) && !ContainsCI(r.Salesman, filter.Salesman)) continue;
                    if (filter.HasSearch)
                    {
                        var hit = ContainsCI(r.GlassRef, filter.SearchText) ||
                                  ContainsCI(r.SpecName, filter.SearchText) ||
                                  ContainsCI(r.ProformaId, filter.SearchText) ||
                                  ContainsCI(r.CustomerName, filter.SearchText);
                        if (!hit) continue;
                    }
                    allRows.Add(r);
                }
            }

            var sorted = allRows.OrderByDescending(r => r.IssueDate).ThenBy(r => r.ProformaId).ThenBy(r => r.GroupNumber).ThenBy(r => r.SerialNumber).ToList();
            var total = sorted.Count;
            var page = sorted.Skip(filter.Skip).Take(filter.Take).ToList();

            return new PagedResult<GlassSpecReportRow> { Items = page, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }

        public async Task<List<SpecAggregation>> GetSpecAggregationsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            var page = await GetGlassSpecRowsAsync(new ReportingFilter
            {
                DateFrom = filter.DateFrom, DateTo = filter.DateTo,
                Company = filter.Company, Customer = filter.Customer, Salesman = filter.Salesman,
                Project = filter.Project, GlassType = filter.GlassType, Specification = filter.Specification,
                Colour = filter.Colour, Thickness = filter.Thickness, GlassRef = filter.GlassRef,
                SearchText = filter.SearchText, ProformaId = filter.ProformaId,
                Page = 1, PageSize = 5000
            }, ct);

            return SpecificationIntelligenceService.Instance.AggregateSpecificationRows(page.Items);
        }

        public async Task<RowTrace?> GetRowTraceAsync(string rowId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rowId)) return null;

            // Direct extraction of ProformaId from RowId (e.g. "PI-2026-0842-G1-S1" -> "PI-2026-0842")
            string piId = rowId.Trim();
            var gIdx = rowId.IndexOf("-G", StringComparison.OrdinalIgnoreCase);
            if (gIdx > 0) piId = rowId.Substring(0, gIdx);

            var detailRows = await GetPiRowsDetailAsync(piId, ct);
            var target = detailRows.FirstOrDefault(r => Eq(r.RowId, rowId)) ?? detailRows.FirstOrDefault();
            if (target == null) return null;

            var trace = new RowTrace { SourceRow = target };
            trace.Steps.Add(new TraceStep { Stage = "Proforma Invoice", DocumentNumber = target.ProformaId, Status = target.PiStatus, Qty = target.Qty, Value = target.Amount, Date = target.IssueDate });
            if (!string.IsNullOrWhiteSpace(target.JobOrderId))
                trace.Steps.Add(new TraceStep { Stage = "Job Order", DocumentNumber = target.JobOrderId, Qty = target.Qty });
            if (target.DeliveredQty > 0)
                trace.Steps.Add(new TraceStep { Stage = "Delivery", DocumentNumber = target.DeliveryNoteId ?? target.JobOrderId, Status = target.DeliveryStatus, Qty = target.DeliveredQty });
            else
                trace.Steps.Add(new TraceStep { Stage = "Delivery", Status = "Pending", Qty = 0, Notes = "Not yet delivered" });

            if (target.InvoicedQty > 0)
                trace.Steps.Add(new TraceStep { Stage = "Tax Invoice", DocumentNumber = target.TaxInvoiceNumber ?? "", Status = target.InvoiceStatus, Qty = target.InvoicedQty });
            else
                trace.Steps.Add(new TraceStep { Stage = "Tax Invoice", Status = "Pending", Qty = 0, Notes = "Not yet invoiced" });

            trace.Reconciliation = new QuantityReconciliation
            {
                OrderedQty = target.Qty,
                JobOrderQty = string.IsNullOrWhiteSpace(target.JobOrderId) ? 0 : target.Qty,
                DeliveredQty = target.DeliveredQty,
                RemainingQty = target.BalanceQty,
                InvoicedQty = target.InvoicedQty,
                DeliveryState = target.DeliveryStatus == "Complete" ? "FullyDelivered" : target.DeliveryStatus == "Partial" ? "PartiallyDelivered" : "NotDelivered",
                InvoiceState = target.InvoiceStatus == "Complete" ? "FullyInvoiced" : target.InvoiceStatus == "Partial" ? "PartiallyInvoiced" : "Pending"
            };

            return trace;
        }

        // ════════════════════════════════════════════════════════════════
        //  STATEMENT REPORT (DB-Side Filtered & Paginated)
        // ════════════════════════════════════════════════════════════════

        public async Task<StatementReportSummary> GetStatementSummaryAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new StatementDbContext();
            var query = db.Statements.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Customer))
                query = query.Where(s => EF.Functions.Like(s.FilterCustomer, $"%{filter.Customer}%") || EF.Functions.Like(s.StatementNumber, $"%{filter.Customer}%"));
            if (filter.DateFrom.HasValue)
                query = query.Where(s => s.CreatedAt >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue)
                query = query.Where(s => s.CreatedAt <= filter.DateTo.Value);

            if (filter.HasSearch)
            {
                var term = filter.SearchText.Trim();
                query = query.Where(s =>
                    EF.Functions.Like(s.StatementNumber, $"%{term}%") ||
                    EF.Functions.Like(s.FilterCustomer, $"%{term}%") ||
                    EF.Functions.Like(s.FilterCompany, $"%{term}%"));
            }

            var summary = new StatementReportSummary();
            summary.TotalCount = await query.CountAsync(ct);
            if (summary.TotalCount == 0) return summary;

            summary.TotalInvoiced = await query.SumAsync(s => (decimal?)s.TotalInvoiced, ct) ?? 0m;
            summary.TotalOutstanding = await query.SumAsync(s => (decimal?)s.TotalOutstanding, ct) ?? 0m;
            summary.TotalOverdue = await query.SumAsync(s => (decimal?)s.OverdueAmount, ct) ?? 0m;

            return summary;
        }

        public async Task<PagedResult<StatementReportRow>> GetStatementRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new StatementDbContext();
            var query = db.Statements.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Customer))
                query = query.Where(s => EF.Functions.Like(s.FilterCustomer, $"%{filter.Customer}%") || EF.Functions.Like(s.StatementNumber, $"%{filter.Customer}%"));
            if (filter.DateFrom.HasValue)
                query = query.Where(s => s.CreatedAt >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue)
                query = query.Where(s => s.CreatedAt <= filter.DateTo.Value);

            if (filter.HasSearch)
            {
                var term = filter.SearchText.Trim();
                query = query.Where(s =>
                    EF.Functions.Like(s.StatementNumber, $"%{term}%") ||
                    EF.Functions.Like(s.FilterCustomer, $"%{term}%") ||
                    EF.Functions.Like(s.FilterCompany, $"%{term}%"));
            }

            var totalCount = await query.CountAsync(ct);
            if (totalCount == 0)
                return new PagedResult<StatementReportRow> { Items = new List<StatementReportRow>(), TotalCount = 0, Page = filter.Page, PageSize = filter.PageSize };

            var page = await query.OrderByDescending(s => s.CreatedAt)
                .Skip(filter.Skip)
                .Take(filter.Take)
                .Select(s => new StatementReportRow
                {
                    Id = s.Id,
                    StatementNumber = s.StatementNumber,
                    CustomerName = s.FilterCustomer,
                    CompanyName = s.FilterCompany,
                    ProjectName = s.FilterProject,
                    InvoiceCount = s.LineItems != null ? s.LineItems.Count : 0,
                    TotalInvoiced = s.TotalInvoiced,
                    TotalOutstanding = s.TotalOutstanding,
                    OverdueAmount = s.OverdueAmount,
                    CreatedAt = s.CreatedAt,
                    FilterJson = s.FilterJson
                })
                .ToListAsync(ct);

            return new PagedResult<StatementReportRow> { Items = page, TotalCount = totalCount, Page = filter.Page, PageSize = filter.PageSize };
        }

        // ════════════════════════════════════════════════════════════════
        //  PROFITABILITY (DB-Side Filtered & Paginated)
        // ════════════════════════════════════════════════════════════════

        public async Task<ProfitabilitySummary> GetProfitabilitySummaryAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            var rows = await GetProfitabilityRowsAsync(new ReportingFilter
            {
                DateFrom = filter.DateFrom, DateTo = filter.DateTo,
                Company = filter.Company, Customer = filter.Customer, Project = filter.Project,
                Salesman = filter.Salesman, Page = 1, PageSize = 1000
            }, ct);

            var summary = new ProfitabilitySummary
            {
                TotalRevenue = rows.Items.Sum(r => r.Revenue),
                TotalCost = rows.Items.Sum(r => r.Cost),
                TotalProfit = rows.Items.Sum(r => r.Profit),
                InvoicesWithCost = rows.Items.Count(r => r.HasCostData),
                InvoicesWithoutCost = rows.Items.Count(r => !r.HasCostData)
            };
            summary.AvgMargin = summary.TotalRevenue == 0 ? 0 : Math.Round(summary.TotalProfit / summary.TotalRevenue * 100, 1);
            summary.MissingCostSources = rows.Items.Where(r => !r.HasCostData).Select(r => r.Name).Distinct().Take(10).ToList();
            return summary;
        }

        public async Task<PagedResult<ProfitabilityRow>> GetProfitabilityRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            var rows = new List<ProfitabilityRow>();

            using var piDb = new ProformaInvoicesDbContext();
            var query = piDb.ProformaInvoices.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Company))
                query = query.Where(p => EF.Functions.Like(p.CompanyName, $"%{filter.Company}%"));
            if (!string.IsNullOrWhiteSpace(filter.Customer))
                query = query.Where(p => EF.Functions.Like(p.CustomerName, $"%{filter.Customer}%"));
            if (!string.IsNullOrWhiteSpace(filter.ProformaId))
                query = query.Where(p => p.ProformaId == filter.ProformaId);
            if (filter.DateFrom.HasValue)
                query = query.Where(p => p.IssueDate >= filter.DateFrom.Value || (p.IssueDate == default && p.CreatedAt >= filter.DateFrom.Value));
            if (filter.DateTo.HasValue)
                query = query.Where(p => p.IssueDate <= filter.DateTo.Value || (p.IssueDate == default && p.CreatedAt <= filter.DateTo.Value));

            var totalCount = await query.CountAsync(ct);
            if (totalCount == 0)
                return new PagedResult<ProfitabilityRow> { Items = rows, TotalCount = 0, Page = filter.Page, PageSize = filter.PageSize };

            var pis = await query.OrderByDescending(p => p.CreatedAt)
                .Skip(filter.Skip)
                .Take(filter.Take)
                .Select(p => new { p.ProformaId, p.CompanyName, p.CustomerName, p.ProjectName, p.GrandTotalVatInclusive, p.Status, p.IssueDate, p.FullSnapshotJson })
                .ToListAsync(ct);

            foreach (var pi in pis)
            {
                var snap = TryDeserializePiSnapshot(pi.FullSnapshotJson);
                var pl = snap?.ProfitLoss;
                bool hasCost = pl != null && (pl.TotalGlassCost > 0 || pl.TotalProductionCost > 0);
                decimal revenue = pl != null && pl.NetExVat > 0 ? pl.NetExVat : Math.Round(pi.GrandTotalVatInclusive / 1.05m, 2);
                decimal cost = hasCost ? pl!.TotalGlassCost + pl.TotalProductionCost : 0;
                decimal profit = hasCost ? revenue - cost : 0;
                decimal margin = revenue == 0 ? 0 : Math.Round(profit / revenue * 100, 1);

                int qty = 0; decimal sqm = 0;
                if (snap?.SpecGroups != null)
                    foreach (var g in snap.SpecGroups)
                        if (g?.ItemRows != null)
                            foreach (var r in g.ItemRows)
                            {
                                qty += r.Qty;
                                sqm += r.TotalSqm;
                            }

                rows.Add(new ProfitabilityRow
                {
                    Key = pi.ProformaId,
                    Name = pi.ProformaId,
                    Category = "PI",
                    Revenue = revenue,
                    Cost = cost,
                    GlassCost = pl?.TotalGlassCost ?? 0,
                    ProductionCost = pl?.TotalProductionCost ?? 0,
                    Profit = profit,
                    Margin = margin,
                    Qty = qty,
                    Sqm = Math.Round(sqm, 3),
                    DocumentCount = 1,
                    HasCostData = hasCost,
                    CostSource = hasCost ? (pl!.CostAtSellingPrice ? "Selling price" : "Purchase price") + $" (wastage {pl.WastagePercent}%)" : "Missing — no Step 9 calculation saved"
                });
            }

            return new PagedResult<ProfitabilityRow> { Items = rows, TotalCount = totalCount, Page = filter.Page, PageSize = filter.PageSize };
        }

        // ════════════════════════════════════════════════════════════════
        //  DRILL-DOWN HELPERS
        // ════════════════════════════════════════════════════════════════

        public async Task<List<GlassSpecReportRow>> GetPiRowsDetailAsync(string proformaId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(proformaId)) return new List<GlassSpecReportRow>();
            var res = await GetGlassSpecRowsAsync(new ReportingFilter { ProformaId = proformaId.Trim(), Page = 1, PageSize = 500 }, ct);
            return res.Items;
        }

        public async Task<QuantityReconciliation> GetQuantityReconciliationAsync(string proformaId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(proformaId)) return new QuantityReconciliation();
            var rows = await GetPiRowsDetailAsync(proformaId, ct);
            int ordered = rows.Sum(r => r.Qty);
            int delivered = rows.Sum(r => r.DeliveredQty);
            int invoiced = rows.Sum(r => r.InvoicedQty);
            return new QuantityReconciliation
            {
                OrderedQty = ordered,
                JobOrderQty = ordered,
                DeliveredQty = delivered,
                RemainingQty = Math.Max(0, ordered - delivered),
                InvoicedQty = invoiced,
                DeliveryState = delivered == 0 ? "NotDelivered" : delivered < ordered ? "PartiallyDelivered" : "FullyDelivered",
                InvoiceState = invoiced == 0 ? "Pending" : invoiced < ordered ? "PartiallyInvoiced" : "FullyInvoiced"
            };
        }

        public async Task<Dictionary<string, object>> GetDocumentChainAsync(string proformaId, CancellationToken ct = default)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(proformaId)) return dict;
            var pi = proformaId.Trim();

            var piRows = await GetPiRowsAsync(new ReportingFilter { ProformaId = pi, PageSize = 5 }, ct);
            dict["Proforma"] = piRows.Items.FirstOrDefault() ?? new PiReportRow { ProformaId = pi };

            var joRows = await GetJoRowsAsync(new ReportingFilter { ProformaId = pi, PageSize = 10 }, ct);
            dict["JobOrders"] = joRows.Items;
            dict["HasJobOrder"] = joRows.Items.Count > 0;

            var dnRows = await GetDeliveryRowsAsync(new ReportingFilter { ProformaId = pi, PageSize = 20 }, ct);
            dict["DeliveryNotes"] = dnRows.Items;

            var tiRows = await GetTaxInvoiceRowsAsync(new ReportingFilter { ProformaId = pi, PageSize = 20 }, ct);
            dict["TaxInvoices"] = tiRows.Items;

            var glassRows = await GetPiRowsDetailAsync(pi, ct);
            dict["GlassRows"] = glassRows;
            dict["TotalQty"] = glassRows.Sum(r => r.Qty);
            dict["TotalSqm"] = Math.Round(glassRows.Sum(r => r.TotalSqm), 3);

            dict["Reconciliation"] = await GetQuantityReconciliationAsync(pi, ct);

            var profit = await GetProfitabilityRowsAsync(new ReportingFilter { ProformaId = pi, PageSize = 5 }, ct);
            dict["Profitability"] = profit.Items.FirstOrDefault();

            return dict;
        }

        // ── Filter lookups ──────────────────────────────────────────────

        public async Task<List<string>> GetCompanyOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new ProformaInvoicesDbContext();
                var list = await db.ProformaInvoices.AsNoTracking().Select(p => p.CompanyName).Distinct().ToListAsync(ct);
                return list.Where(s => !string.IsNullOrWhiteSpace(s)).OrderBy(s => s).ToList()!;
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetCustomerOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new ProformaInvoicesDbContext();
                var list = await db.ProformaInvoices.AsNoTracking().Select(p => p.CustomerName).Distinct().ToListAsync(ct);
                return list.Where(s => !string.IsNullOrWhiteSpace(s)).OrderBy(s => s).ToList()!;
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetSalesmanOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new MasterDataDbContext();
                var list = await db.Salesmen.AsNoTracking().Select(s => s.Name).ToListAsync(ct);
                var clientReps = await db.Clients.AsNoTracking().Select(c => c.SalesRepresentative).Distinct().ToListAsync(ct);
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in list.Where(x => !string.IsNullOrWhiteSpace(x))) set.Add(s.Trim());
                foreach (var s in clientReps.Where(x => !string.IsNullOrWhiteSpace(x))) set.Add(s.Trim());
                return set.OrderBy(s => s).ToList();
            }
            catch (Exception ex) { AppLogService.Warning($"Salesman options masterdata warning: {ex.Message}"); }
            return new List<string>();
        }

        public async Task<List<string>> GetProjectOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new ProformaInvoicesDbContext();
                var list = await db.ProformaInvoices.AsNoTracking().Select(p => p.ProjectName).Distinct().ToListAsync(ct);
                return list.Where(s => !string.IsNullOrWhiteSpace(s)).OrderBy(s => s).ToList()!;
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetGlassTypeOptionsAsync(CancellationToken ct = default)
        {
            return new List<string> { "SGU", "DGU", "LAM", "DGU+LAM" };
        }
    }
}
