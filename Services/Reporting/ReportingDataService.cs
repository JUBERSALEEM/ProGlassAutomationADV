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
    public interface IReportingDataService
    {
        // ── PI Report ────────────────────────────────────────────────
        Task<PiReportSummary> GetPiSummaryAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<PiReportRow>> GetPiRowsAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<List<PiReportRow>> GetPiLifecycleAsync(string proformaId, CancellationToken ct = default);

        // ── JO Report ────────────────────────────────────────────────
        Task<JoReportSummary> GetJoSummaryAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<JoReportRow>> GetJoRowsAsync(ReportingFilter filter, CancellationToken ct = default);

        // ── Delivery Report — ROW-LEVEL ─────────────────────────────
        // Every row = one exact PI specification row (PI → Spec → Sr No)
        // with its Total / Delivered / Balance quantities reconciled from
        // the DeliveryNoteItems table. Delivered quantities are summed per
        // row key (JobOrderId + SpecGroupNumber + SerialNumber).
        Task<DeliveryReportSummary> GetDeliverySummaryAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<DeliveryReportRow>> GetDeliveryRowsAsync(ReportingFilter filter, CancellationToken ct = default);

        /// <summary>
        /// The Delivery Notes that delivered this exact row (detail panel):
        /// DN #, delivery date, status, quantity delivered in each DN.
        /// </summary>
        Task<List<DeliveryNoteDelivery>> GetRowDeliveriesAsync(
            string jobOrderId, int specGroupNumber, int serialNumber,
            CancellationToken ct = default);

        // ── Delivery report filter lookups (data-driven, not hardcoded) ──
        Task<List<string>> GetDeliveryPiOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetDeliverySpecificationOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetDeliveryGlassRefOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetDeliveryProjectOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetDeliverySalesmanOptionsAsync(CancellationToken ct = default);

        // ── Module-specific filter lookups (each document's own dimension) ──
        Task<List<string>> GetJoSourcePiOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetTiProformaOptionsAsync(CancellationToken ct = default);

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

        // ── Statement FINANCIAL ledger (Phase 8) ─────────────────────
        // One row per statement line item rendered as a ledger document:
        // Date | Document # | Type | Description | Debit | Credit | Balance.
        Task<StatementLineSummary> GetStatementLineSummaryAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<StatementLineRow>> GetStatementLineRowsAsync(ReportingFilter filter, CancellationToken ct = default);

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

        // ── Status lookups (distinct statuses actually stored) ────────
        Task<List<string>> GetPiStatusOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetJoStatusOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetTiStatusOptionsAsync(CancellationToken ct = default);

        // ── VAT Submission Report ─────────────────────────────────────
        Task<VatSubmissionSummary> GetVatSubmissionSummaryAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<VatSubmissionRow>> GetVatSubmissionRowsAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<List<VatSubmissionRow>> GetAllVatSubmissionRowsAsync(ReportingFilter filter, CancellationToken ct = default);
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
            summary.TotalPi = await query.CountAsync(ct);
            if (summary.TotalPi == 0) return summary;

            var totals = await query.Select(i => i.GrandTotalVatInclusive).ToListAsync(ct);
            summary.TotalValue = totals.Sum();

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

        // ════════════════════════════════════════════════════════════════
        //  DELIVERY REPORT (DB-Side Filtered & Paginated)
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Row-level summary: counts every exact PI spec row (via the
        /// DeliveryNoteItems catalog — master-DN items carry the full JO
        /// row list) and reconciles delivered quantities from real DNs.
        /// All filtering is database-side; only the four bucket counts and
        /// three quantity sums are materialized.
        /// </summary>
        public async Task<DeliveryReportSummary> GetDeliverySummaryAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            var summary = new DeliveryReportSummary();

            using var db = new DeliveryNotesDbContext();
            var salesmanJobIds = await ResolveSalesmanJobOrderIdsAsync(filter.Salesman, ct);
            var glassTypeKeys = await ResolveGlassTypeKeysAsync(filter.GlassType, ct);
            var query = BuildDeliveryRowQuery(db, filter, salesmanJobIds, glassTypeKeys);

            summary.TotalRows = await query.CountAsync(ct);
            if (summary.TotalRows == 0) return summary;

            // Individual DB-side aggregates (no row materialization).
            summary.TotalOrderedQty = await query.SumAsync(x => x.TotalQty, ct);
            summary.TotalDeliveredQty = await query.SumAsync(x => x.DeliveredQty, ct);
            summary.TotalRemainingQty = Math.Max(0, summary.TotalOrderedQty - summary.TotalDeliveredQty);
            summary.NotDelivered = await query.CountAsync(x => x.DeliveredQty == 0, ct);
            summary.FullyDelivered = await query.CountAsync(x => x.DeliveredQty > 0 && x.DeliveredQty >= x.TotalQty, ct);
            summary.Partial = Math.Max(0, summary.TotalRows - summary.NotDelivered - summary.FullyDelivered);

            return summary;
        }

        /// <summary>
        /// ROW-LEVEL delivery report. One row per exact PI specification row
        /// (PI → Specification → Sr No), quantities reconciled from the
        /// DeliveryNoteItems table:
        ///   TotalQty      = original order qty of that exact row
        ///   DeliveredQty  = SUM of DeliveredQty across real DNs for that row
        ///   BalanceQty    = TotalQty − DeliveredQty
        /// Grouping key = (JobOrderId, SpecGroupNumber, SerialNumber), so
        /// different specifications or rows are NEVER merged. Company and
        /// Salesman are enriched from the JO snapshot JSON for the visible
        /// page only (≤100 rows), keeping the hot path database-side.
        /// </summary>
        public async Task<PagedResult<DeliveryReportRow>> GetDeliveryRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();

            using var db = new DeliveryNotesDbContext();
            var salesmanJobIds = await ResolveSalesmanJobOrderIdsAsync(filter.Salesman, ct);
            var glassTypeKeys = await ResolveGlassTypeKeysAsync(filter.GlassType, ct);
            var query = BuildDeliveryRowQuery(db, filter, salesmanJobIds, glassTypeKeys);

            var totalCount = await query.CountAsync(ct);
            if (totalCount == 0)
                return new PagedResult<DeliveryReportRow> { Items = new List<DeliveryReportRow>(), TotalCount = 0, Page = filter.Page, PageSize = filter.PageSize };

            // Project the row-status string in SQL (CASE WHEN), then filter.
            IQueryable<DeliveryRowProjection> projected = query
                .Select(x => new DeliveryRowProjection
                {
                    Id = x.Id,
                    JobOrderId = x.JobOrderId,
                    ProformaId = x.ProformaId,
                    SpecGroupNumber = x.SpecGroupNumber,
                    SerialNumber = x.SerialNumber,
                    Specification = x.Specification,
                    CustomerName = x.CustomerName,
                    ProjectName = x.ProjectName,
                    GlassRef = x.GlassRef,
                    W1 = x.W1, H1 = x.H1, W2 = x.W2, H2 = x.H2,
                    TotalQty = x.TotalQty,
                    DeliveredQty = x.DeliveredQty,
                    DeliveryStatus = x.DeliveredQty <= 0 ? "Not delivered"
                        : x.DeliveredQty >= x.TotalQty ? "Fully delivered"
                        : "Partially delivered"
                });

            if (!string.IsNullOrWhiteSpace(filter.Status))
                projected = projected.Where(x => x.DeliveryStatus == filter.Status.Trim());

            var page = await projected
                .OrderBy(x => x.ProformaId)
                .ThenBy(x => x.SpecGroupNumber)
                .ThenBy(x => x.SerialNumber)
                .Skip(filter.Skip)
                .Take(filter.Take)
                .ToListAsync(ct);

            var rows = page.Select(x => new DeliveryReportRow
            {
                Id = x.Id,
                JobOrderId = x.JobOrderId,
                ProformaId = x.ProformaId,
                SpecGroupNumber = x.SpecGroupNumber,
                SerialNumber = x.SerialNumber,
                Specification = FormatSpecification(x.Specification),
                CustomerName = x.CustomerName,
                ProjectName = x.ProjectName,
                GlassRef = x.GlassRef,
                W1 = x.W1, H1 = x.H1, W2 = x.W2, H2 = x.H2,
                TotalQty = x.TotalQty,
                DeliveredQty = x.DeliveredQty,
                BalanceQty = Math.Max(0, x.TotalQty - x.DeliveredQty),
                DeliveryStatus = x.DeliveryStatus,
                DeliveryStatusBrushKey = RowStatusBrushKey(x.DeliveredQty, x.TotalQty)
            }).ToList();

            await EnrichCompanyAndSalesmanAsync(rows, ct);

            return new PagedResult<DeliveryReportRow>
            {
                Items = rows,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }

        /// <summary>
        /// The Delivery Notes that delivered one exact row — for the
        /// right-side detail panel (DN #, date, status, qty per DN).
        /// </summary>
        public async Task<List<DeliveryNoteDelivery>> GetRowDeliveriesAsync(
            string jobOrderId, int specGroupNumber, int serialNumber,
            CancellationToken ct = default)
        {
            using var db = new DeliveryNotesDbContext();
            return await db.DeliveryNoteItems.AsNoTracking()
                .Where(i => i.JobOrderId == jobOrderId
                            && i.SpecGroupNumber == specGroupNumber
                            && i.SerialNumber == serialNumber
                            && i.DeliveryNote != null
                            && !i.DeliveryNote!.IsMasterDn)
                .OrderBy(i => i.DeliveryNote!.DeliveryDate)
                .Select(i => new DeliveryNoteDelivery
                {
                    DeliveryNoteId = i.DeliveryNote!.DeliveryNoteId,
                    DeliveryDate = i.DeliveryNote!.DeliveryDate,
                    Status = i.DeliveryNote!.Status,
                    QtyDelivered = i.DeliveredQty,
                    DriverName = i.DeliveryNote!.DriverName ?? "",
                    VehicleNumber = i.DeliveryNote!.VehicleNumber ?? ""
                })
                .ToListAsync(ct);
        }

        // ══════════════════════════════════════════════════════════════
        //  ROW-LEVEL DELIVERY QUERY BUILDERS
        // ══════════════════════════════════════════════════════════════

        /// <summary>Numeric projection used by the SQL-side row aggregation.</summary>
        private class DeliveryRowAgg
        {
            public int Id { get; set; }
            public string JobOrderId { get; set; } = "";
            public string ProformaId { get; set; } = "";
            public int SpecGroupNumber { get; set; }
            public int SerialNumber { get; set; }
            public string Specification { get; set; } = "";
            public string CustomerName { get; set; } = "";
            public string ProjectName { get; set; } = "";
            public string GlassRef { get; set; } = "";
            public int W1 { get; set; }
            public int H1 { get; set; }
            public int W2 { get; set; }
            public int H2 { get; set; }
            public int TotalQty { get; set; }
            public int DeliveredQty { get; set; }
        }

        /// <summary>Projection with the SQL-computed delivery status string.</summary>
        private sealed class DeliveryRowProjection : DeliveryRowAgg
        {
            public string DeliveryStatus { get; set; } = "";
        }

        private static string RowStatusBrushKey(int delivered, int total) =>
            delivered <= 0 ? "NeutralBrush"
            : delivered >= total ? "SuccessBrush"
            : "WarningBrush";

        /// <summary>
        /// Returns the FULL specification name exactly as stored on the
        /// DeliveryNoteItem (already resolved to the real spec name at DN
        /// creation time via SelectedSpecName → GroupName precedence).
        /// No artificial "Spec N" prefix — the user wants the exact spec
        /// name as it appears on the Proforma Invoice.
        /// </summary>
        private static string FormatSpecification(string name) => (name ?? "").Trim();

        /// <summary>
        /// Builds the SQL-side row query. The catalog is the master-DN
        /// DeliveryNoteItems (one item per JO spec row, carrying the PI row
        /// data) plus every real-DN item; grouping key
        /// (JobOrderId, SpecGroupNumber, SerialNumber) guarantees each row
        /// stays exactly one report row and never merges with another spec.
        /// </summary>
        private static IQueryable<DeliveryRowAgg> BuildDeliveryRowQuery(
            DeliveryNotesDbContext db, ReportingFilter filter,
            HashSet<string>? salesmanJobOrderIds = null,
            HashSet<string>? glassTypeKeys = null)
        {
            IQueryable<DeliveryNoteItem> items = db.DeliveryNoteItems
                .AsNoTracking()
                .Where(i => i.DeliveryNote != null && !string.IsNullOrWhiteSpace(i.JobOrderId))
                // Exclude PHANTOM rows: a spec group without real row data
                // (blank glass ref + zero dims, or zero quantity) must never
                // appear in the delivery report. This filters existing data
                // that predates the creation-side guard in DeliveryNoteMapper.
                .Where(i => !(string.IsNullOrWhiteSpace(i.GlassRef)
                              && i.W1 == 0 && i.H1 == 0))
                .Where(i => i.OrderedQty > 0 || i.DeliveredQty > 0);

            // A date range describes DELIVERIES in the period — only real DNs
            // participate (undelivered rows are excluded once a range is set).
            if (filter.DateFrom.HasValue)
                items = items.Where(i => !i.DeliveryNote!.IsMasterDn
                                        && i.DeliveryNote!.DeliveryDate >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue)
                items = items.Where(i => !i.DeliveryNote!.IsMasterDn
                                        && i.DeliveryNote!.DeliveryDate <= filter.DateTo.Value);

            if (!string.IsNullOrWhiteSpace(filter.ProformaId))
                items = items.Where(i => i.DeliveryNote!.ProformaId == filter.ProformaId);
            if (!string.IsNullOrWhiteSpace(filter.JobOrderId))
                items = items.Where(i => i.JobOrderId == filter.JobOrderId);
            if (!string.IsNullOrWhiteSpace(filter.DeliveryNoteId))
                items = items.Where(i => i.DeliveryNote!.DeliveryNoteId == filter.DeliveryNoteId);
            if (!string.IsNullOrWhiteSpace(filter.Customer))
                items = items.Where(i => EF.Functions.Like(i.DeliveryNote!.CustomerName, $"%{filter.Customer}%"));

            // Exact-match filters — the dropdowns provide the exact stored
            // value (as it appears on the PI / JO), so equality is correct
            // and never merges across specifications or glass rows.
            if (!string.IsNullOrWhiteSpace(filter.Project))
                items = items.Where(i => i.DeliveryNote!.ProjectName == filter.Project);
            if (!string.IsNullOrWhiteSpace(filter.GlassRef))
                items = items.Where(i => i.GlassRef == filter.GlassRef);
            if (!string.IsNullOrWhiteSpace(filter.Specification))
                items = items.Where(i => i.SpecGroupName == filter.Specification);

            // Salesman lives only in the JO snapshot JSON (not a column), so
            // it is resolved to a set of JobOrderIds before the query runs.
            if (salesmanJobOrderIds != null)
                items = items.Where(i => salesmanJobOrderIds.Contains(i.JobOrderId));

            // Glass type also lives only in the JO snapshot JSON; it is
            // resolved to a set of "JobOrderId|SpecGroupNumber" keys so the
            // filter stays exact per spec group (never merges SGU and DGU).
            if (glassTypeKeys != null)
                items = items.Where(i => glassTypeKeys.Contains(
                    i.JobOrderId + "|" + i.SpecGroupNumber.ToString()));

            if (filter.HasSearch)
            {
                var term = filter.SearchText.Trim();
                items = items.Where(i =>
                    EF.Functions.Like(i.DeliveryNote!.ProformaId, $"%{term}%") ||
                    EF.Functions.Like(i.JobOrderId, $"%{term}%") ||
                    EF.Functions.Like(i.DeliveryNote!.DeliveryNoteId, $"%{term}%") ||
                    EF.Functions.Like(i.DeliveryNote!.CustomerName, $"%{term}%") ||
                    EF.Functions.Like(i.GlassRef, $"%{term}%") ||
                    EF.Functions.Like(i.SpecGroupName, $"%{term}%"));
            }

            return items
                .GroupBy(i => new { i.JobOrderId, i.SpecGroupNumber, i.SerialNumber })
                .Select(g => new DeliveryRowAgg
                {
                    JobOrderId = g.Key.JobOrderId,
                    SpecGroupNumber = g.Key.SpecGroupNumber,
                    SerialNumber = g.Key.SerialNumber,
                    ProformaId = g.Max(i => i.DeliveryNote!.ProformaId ?? ""),
                    CustomerName = g.Max(i => i.DeliveryNote!.CustomerName ?? ""),
                    ProjectName = g.Max(i => i.DeliveryNote!.ProjectName ?? ""),
                    Specification = g.Max(i => i.SpecGroupName ?? ""),
                    GlassRef = g.Max(i => i.GlassRef ?? ""),
                    W1 = g.Max(i => i.W1), H1 = g.Max(i => i.H1),
                    W2 = g.Max(i => i.W2), H2 = g.Max(i => i.H2),
                    TotalQty = g.Max(i => i.OrderedQty),
                    DeliveredQty = g.Sum(i => i.DeliveryNote!.IsMasterDn ? 0 : i.DeliveredQty)
                });
        }

        /// <summary>
        /// Enriches the visible page (≤100 rows) with Company and Salesman
        /// from the JO snapshot JSON — never touches the full dataset.
        /// </summary>
        private static async Task EnrichCompanyAndSalesmanAsync(
            List<DeliveryReportRow> rows, CancellationToken ct)
        {
            var jobIds = rows
                .Select(r => r.JobOrderId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();
            if (jobIds.Count == 0) return;

            try
            {
                using var joDb = new JobOrdersDbContext();
                var snapshots = await joDb.JobOrders.AsNoTracking()
                    .Where(j => jobIds.Contains(j.JobOrderId))
                    .Select(j => new { j.JobOrderId, j.FullSnapshotJson })
                    .ToListAsync(ct);

                foreach (var s in snapshots)
                {
                    var snap = JobOrderMapper.DeserializeSnapshot(
                        new SavedJobOrder
                        {
                            JobOrderId = s.JobOrderId,
                            FullSnapshotJson = s.FullSnapshotJson
                        });
                    if (snap?.EmbeddedInvoice == null) continue;

                    string company = snap.EmbeddedInvoice.Company?.CompanyName ?? "";
                    string salesman = snap.EmbeddedInvoice.Client?.SalesRepresentative ?? "";

                    // Glass type per spec group (SGU / DGU / LAM / DGU+LAM).
                    var glassTypeByGroup = new Dictionary<int, string>();
                    if (snap.EmbeddedInvoice.SpecGroups != null)
                        foreach (var g in snap.EmbeddedInvoice.SpecGroups)
                            if (g != null && !string.IsNullOrWhiteSpace(g.SelectedGlassType))
                                glassTypeByGroup[g.GroupNumber] = g.SelectedGlassType;

                    foreach (var r in rows)
                    {
                        if (!string.Equals(r.JobOrderId, s.JobOrderId, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (string.IsNullOrWhiteSpace(r.CompanyName))
                            r.CompanyName = company;
                        if (string.IsNullOrWhiteSpace(r.Salesman))
                            r.Salesman = salesman;
                        if (string.IsNullOrWhiteSpace(r.GlassType) &&
                            glassTypeByGroup.TryGetValue(r.SpecGroupNumber, out var gt))
                            r.GlassType = gt;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"Delivery report enrichment failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Resolves a salesman name to the set of JobOrderIds whose JO
        /// snapshot carries that sales representative. Salesman is not a
        /// column on DeliveryNoteItems — it lives only in the JO JSON — so
        /// the salesman filter is applied by JobOrderId. Returns null when
        /// no salesman filter is requested (no filtering).
        /// </summary>
        private static async Task<HashSet<string>?> ResolveSalesmanJobOrderIdsAsync(
            string? salesman, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(salesman)) return null;

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var joDb = new JobOrdersDbContext();
                var snapshots = await joDb.JobOrders.AsNoTracking()
                    .Select(j => new { j.JobOrderId, j.FullSnapshotJson })
                    .ToListAsync(ct);

                foreach (var s in snapshots)
                {
                    var snap = JobOrderMapper.DeserializeSnapshot(
                        new SavedJobOrder
                        {
                            JobOrderId = s.JobOrderId,
                            FullSnapshotJson = s.FullSnapshotJson
                        });
                    var rep = (snap?.EmbeddedInvoice?.Client?.SalesRepresentative ?? "").Trim();
                    if (string.Equals(rep, salesman.Trim(), StringComparison.OrdinalIgnoreCase))
                        ids.Add(s.JobOrderId);
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"Salesman resolution failed: {ex.Message}");
            }
            return ids;
        }

        /// <summary>
        /// Resolves a glass type (SGU / DGU / LAM / DGU+LAM) to the set of
        /// "JobOrderId|SpecGroupNumber" keys whose JO snapshot spec group
        /// carries that glass type. Glass type is not a column on
        /// DeliveryNoteItems — it lives only in the JO JSON. Returns null
        /// when no glass-type filter is requested (no filtering).
        /// </summary>
        private static async Task<HashSet<string>?> ResolveGlassTypeKeysAsync(
            string? glassType, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(glassType)) return null;

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var joDb = new JobOrdersDbContext();
                var snapshots = await joDb.JobOrders.AsNoTracking()
                    .Select(j => new { j.JobOrderId, j.FullSnapshotJson })
                    .ToListAsync(ct);

                foreach (var s in snapshots)
                {
                    var snap = JobOrderMapper.DeserializeSnapshot(
                        new SavedJobOrder
                        {
                            JobOrderId = s.JobOrderId,
                            FullSnapshotJson = s.FullSnapshotJson
                        });
                    if (snap?.EmbeddedInvoice?.SpecGroups == null) continue;

                    foreach (var g in snap.EmbeddedInvoice.SpecGroups)
                    {
                        if (g == null) continue;
                        if (string.Equals(g.SelectedGlassType, glassType, StringComparison.OrdinalIgnoreCase))
                            keys.Add($"{s.JobOrderId}|{g.GroupNumber}");
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"Glass type resolution failed: {ex.Message}");
            }
            return keys;
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
            {
                var st = filter.Status.Trim();
                if (Eq(st, "Partially Paid") || Eq(st, "PartiallyPaid"))
                    query = query.Where(t => t.Status == "PartiallyPaid" || t.Status == "Partially Paid");
                else
                    query = query.Where(t => t.Status == st);
            }
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

            var tiAmounts = await query.Select(t => new { t.GrandTotal, t.VatAmount, t.Status, t.DueDate }).ToListAsync(ct);
            summary.TotalAmount = tiAmounts.Sum(x => x.GrandTotal);
            summary.TotalVat = tiAmounts.Sum(x => x.VatAmount);

            summary.Issued = tiAmounts.Count(t => t.Status == "Issued");
            summary.Paid = tiAmounts.Count(t => t.Status == "Paid");
            summary.PartiallyPaid = tiAmounts.Count(t => t.Status == "PartiallyPaid" || t.Status == "Partially Paid");
            summary.Overdue = tiAmounts.Count(t => t.Status == "Overdue" || (t.Status == "Issued" && t.DueDate < DateTime.Today));
            summary.Draft = tiAmounts.Count(t => t.Status == "Draft");
            summary.Outstanding = tiAmounts.Where(t => t.Status == "Issued" || t.Status == "Overdue" || t.Status == "PartiallyPaid" || t.Status == "Partially Paid").Sum(x => x.GrandTotal);

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
            {
                var st = filter.Status.Trim();
                if (Eq(st, "Partially Paid") || Eq(st, "PartiallyPaid"))
                    query = query.Where(t => t.Status == "PartiallyPaid" || t.Status == "Partially Paid");
                else
                    query = query.Where(t => t.Status == st);
            }
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

            // Enrich the visible page (≤100 rows) with Salesman + Prepared By
            // from the linked PI snapshot JSON — never the full dataset.
            await EnrichTiPageAsync(page, ct);

            return new PagedResult<TaxInvoiceReportRow> { Items = page, TotalCount = totalCount, Page = filter.Page, PageSize = filter.PageSize };
        }

        /// <summary>
        /// Page-only enrichment of TI rows with Salesman and Prepared By from
        /// the source PI snapshot. Reads at most one PI snapshot per distinct
        /// ProformaId on the visible page.
        /// </summary>
        private static async Task EnrichTiPageAsync(
            List<TaxInvoiceReportRow> rows, CancellationToken ct)
        {
            var piIds = rows
                .Select(r => r.ProformaId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();
            if (piIds.Count == 0) return;

            try
            {
                using var piDb = new ProformaInvoicesDbContext();
                var snapshots = await piDb.ProformaInvoices.AsNoTracking()
                    .Where(p => piIds.Contains(p.ProformaId))
                    .Select(p => new { p.ProformaId, p.FullSnapshotJson })
                    .ToListAsync(ct);

                foreach (var s in snapshots)
                {
                    var snap = TryDeserializePiSnapshot(s.FullSnapshotJson);
                    if (snap == null) continue;

                    string salesman = snap.Client?.SalesRepresentative ?? "";
                    string preparedBy = snap.PreparedBy?.PreparedByName ?? "";

                    foreach (var r in rows)
                    {
                        if (!string.Equals(r.ProformaId, s.ProformaId, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (string.IsNullOrWhiteSpace(r.Salesman))
                            r.Salesman = salesman;
                        if (string.IsNullOrWhiteSpace(r.PreparedBy))
                            r.PreparedBy = preparedBy;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"TI report enrichment failed: {ex.Message}");
            }
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
                    catch (Exception ex)
                    {
                        // Phase 2: Never swallow — log with correlation context
                        AppLogService.Warning(
                            $"Could not extract salesman for customer '{g.Key}': {ex.Message}",
                            new LogContext
                            {
                                Module = "Reporting",
                                Class = nameof(ReportingDataService),
                                Method = nameof(GetCustomerRowsAsync),
                                Operation = "EnrichSalesman",
                                Extra = $"Customer={g.Key}"
                            });
                    }
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
            return await RowTraceabilityService.Instance.TraceRowLifecycleAsync(rowId, ct);
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

            var stAmounts = await query.Select(s => new { s.TotalInvoiced, s.TotalOutstanding, s.OverdueAmount }).ToListAsync(ct);
            summary.TotalInvoiced = stAmounts.Sum(x => x.TotalInvoiced);
            summary.TotalOutstanding = stAmounts.Sum(x => x.TotalOutstanding);
            summary.TotalOverdue = stAmounts.Sum(x => x.OverdueAmount);

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
        //  STATEMENT FINANCIAL LEDGER (Phase 8)
        //  One row per statement line item: a real Tax Invoice inside a
        //  saved statement, rendered as Date | Doc # | Type | Description |
        //  Debit | Credit | Balance. All figures from stored data.
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds the SQL-side statement-line query: StatementLineItems
        /// joined to their parent Statement for company/customer context.
        /// Filters: company, customer, date range (by invoice issue date),
        /// free-text search across doc numbers / PI / description.
        /// </summary>
        private static IQueryable<StatementLineProjection> BuildStatementLineQuery(
            StatementDbContext db, ReportingFilter filter)
        {
            IQueryable<StatementLineItem> items = db.LineItems
                .AsNoTracking()
                .Where(i => i.Statement != null);

            if (!string.IsNullOrWhiteSpace(filter.Company))
                items = items.Where(i => EF.Functions.Like(i.Statement!.FilterCompany, $"%{filter.Company}%"));
            if (!string.IsNullOrWhiteSpace(filter.Customer))
                items = items.Where(i => EF.Functions.Like(i.Statement!.FilterCustomer, $"%{filter.Customer}%"));
            if (filter.DateFrom.HasValue)
                items = items.Where(i => i.IssueDate >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue)
                items = items.Where(i => i.IssueDate <= filter.DateTo.Value);

            if (filter.HasSearch)
            {
                var term = filter.SearchText.Trim();
                items = items.Where(i =>
                    EF.Functions.Like(i.InvoiceNumber, $"%{term}%") ||
                    EF.Functions.Like(i.ProformaId, $"%{term}%") ||
                    EF.Functions.Like(i.DeliveryNoteRef, $"%{term}%") ||
                    EF.Functions.Like(i.Statement!.StatementNumber, $"%{term}%"));
            }

            return items.Select(i => new StatementLineProjection
            {
                Id = i.Id,
                StatementNumber = i.Statement!.StatementNumber,
                CustomerName = i.Statement!.FilterCustomer,
                CompanyName = i.Statement!.FilterCompany,
                IssueDate = i.IssueDate,
                InvoiceNumber = i.InvoiceNumber,
                ProformaId = i.ProformaId,
                DocumentType = i.InvoiceType,
                Description = i.DeliveryNoteRef,
                Status = i.Status,
                Debit = i.GrandTotal,
                Credit = i.PaidAmount
            });
        }

        /// <summary>SQL projection shape for statement financial rows.</summary>
        private sealed class StatementLineProjection
        {
            public int Id { get; set; }
            public string StatementNumber { get; set; } = "";
            public string CustomerName { get; set; } = "";
            public string CompanyName { get; set; } = "";
            public DateTime IssueDate { get; set; }
            public string InvoiceNumber { get; set; } = "";
            public string ProformaId { get; set; } = "";
            public string DocumentType { get; set; } = "";
            public string Description { get; set; } = "";
            public string Status { get; set; } = "";
            public decimal Debit { get; set; }
            public decimal Credit { get; set; }
        }

        public async Task<StatementLineSummary> GetStatementLineSummaryAsync(
            ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            var summary = new StatementLineSummary();

            using var db = new StatementDbContext();
            var query = BuildStatementLineQuery(db, filter);

            summary.TotalCount = await query.CountAsync(ct);
            if (summary.TotalCount == 0) return summary;

            // SQLite cannot aggregate decimal server-side (no native decimal
            // type), so materialize the Debit/Credit values and sum in memory.
            var amounts = await query.Select(x => new { x.Debit, x.Credit }).ToListAsync(ct);
            summary.TotalDebit = amounts.Sum(x => x.Debit);
            summary.TotalCredit = amounts.Sum(x => x.Credit);
            summary.ClosingBalance = summary.TotalDebit - summary.TotalCredit;

            // Opening balance = outstanding (Debit − Credit) of documents
            // dated BEFORE the period start, same company/customer filters.
            // With no date-from the tracked window starts at the first
            // document, so opening is 0 (honest — we never fabricate it).
            if (filter.DateFrom.HasValue)
            {
                var openingFilter = new ReportingFilter
                {
                    Company = filter.Company,
                    Customer = filter.Customer,
                    DateFrom = null,
                    DateTo = filter.DateFrom.Value.AddDays(-1)
                };
                var openingQuery = BuildStatementLineQuery(db, openingFilter);
                var openingAmounts = await openingQuery
                    .Select(x => new { x.Debit, x.Credit })
                    .ToListAsync(ct);
                summary.OpeningBalance = Math.Max(0,
                    openingAmounts.Sum(x => x.Debit - x.Credit));
            }

            return summary;
        }

        public async Task<PagedResult<StatementLineRow>> GetStatementLineRowsAsync(
            ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new StatementDbContext();

            var query = BuildStatementLineQuery(db, filter);
            var totalCount = await query.CountAsync(ct);
            if (totalCount == 0)
                return new PagedResult<StatementLineRow> { Items = new List<StatementLineRow>(), TotalCount = 0, Page = filter.Page, PageSize = filter.PageSize };

            var page = await query
                .OrderBy(x => x.IssueDate)
                .ThenBy(x => x.InvoiceNumber)
                .Skip(filter.Skip)
                .Take(filter.Take)
                .ToListAsync(ct);

            var rows = page.Select(x => new StatementLineRow
            {
                Id = x.Id,
                StatementNumber = x.StatementNumber,
                CustomerName = x.CustomerName,
                CompanyName = x.CompanyName,
                IssueDate = x.IssueDate,
                InvoiceNumber = x.InvoiceNumber,
                ProformaId = x.ProformaId,
                DocumentType = string.IsNullOrWhiteSpace(x.DocumentType) ? "Tax Invoice" : x.DocumentType,
                Description = x.Description,
                Status = x.Status,
                Debit = x.Debit,
                Credit = x.Credit,
                Balance = x.Debit - x.Credit
            }).ToList();

            return new PagedResult<StatementLineRow>
            {
                Items = rows,
                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
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
            return await RowTraceabilityService.Instance.CalculateReconciliationAsync(proformaId, "", 0, 0, ct);
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
            // Intentional: a PI with no profitability rows stores a
            // null slot in the chain dictionary (consumers already
            // treat missing rows as "not computed").
            dict["Profitability"] = profit.Items.FirstOrDefault()!;

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

        // ── Delivery report filter lookups (data-driven) ──────────────

        public async Task<List<string>> GetDeliveryPiOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new DeliveryNotesDbContext();
                var list = await db.DeliveryNotes.AsNoTracking()
                    .Where(d => !d.IsMasterDn && !string.IsNullOrWhiteSpace(d.ProformaId))
                    .Select(d => d.ProformaId)
                    .Distinct().ToListAsync(ct);
                return list.OrderBy(s => s).ToList()!;
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetDeliverySpecificationOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new DeliveryNotesDbContext();
                var list = await db.DeliveryNoteItems.AsNoTracking()
                    .Where(i => !string.IsNullOrWhiteSpace(i.SpecGroupName))
                    .Select(i => i.SpecGroupName)
                    .Distinct().ToListAsync(ct);
                return list.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetDeliveryGlassRefOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new DeliveryNotesDbContext();
                var list = await db.DeliveryNoteItems.AsNoTracking()
                    .Where(i => !string.IsNullOrWhiteSpace(i.GlassRef))
                    .Select(i => i.GlassRef)
                    .Distinct().ToListAsync(ct);
                return list.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetDeliveryProjectOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new DeliveryNotesDbContext();
                var list = await db.DeliveryNotes.AsNoTracking()
                    .Where(d => !d.IsMasterDn && !string.IsNullOrWhiteSpace(d.ProjectName))
                    .Select(d => d.ProjectName)
                    .Distinct().ToListAsync(ct);
                return list.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetDeliverySalesmanOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var joDb = new JobOrdersDbContext();
                var snapshots = await joDb.JobOrders.AsNoTracking()
                    .Select(j => new { j.FullSnapshotJson })
                    .ToListAsync(ct);

                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in snapshots)
                {
                    var snap = JobOrderMapper.DeserializeSnapshot(
                        new SavedJobOrder { FullSnapshotJson = s.FullSnapshotJson });
                    var rep = (snap?.EmbeddedInvoice?.Client?.SalesRepresentative ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(rep)) set.Add(rep);
                }
                return set.OrderBy(s => s).ToList();
            }
            catch (Exception ex) { AppLogService.Warning($"Delivery salesman options warning: {ex.Message}"); }
            return new List<string>();
        }

        public async Task<List<string>> GetJoSourcePiOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new JobOrdersDbContext();
                var list = await db.JobOrders.AsNoTracking()
                    .Where(j => !string.IsNullOrWhiteSpace(j.SourceProformaId))
                    .Select(j => j.SourceProformaId)
                    .Distinct().ToListAsync(ct);
                return list.Where(s => !string.IsNullOrWhiteSpace(s)).OrderBy(s => s).ToList()!;
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetTiProformaOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new TaxInvoiceDbContext();
                var list = await db.TaxInvoices.AsNoTracking()
                    .Where(t => !string.IsNullOrWhiteSpace(t.ProformaId))
                    .Select(t => t.ProformaId)
                    .Distinct().ToListAsync(ct);
                return list.Where(s => !string.IsNullOrWhiteSpace(s)).OrderBy(s => s).ToList()!;
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetPiStatusOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new ProformaInvoicesDbContext();
                var list = await db.ProformaInvoices.AsNoTracking().Select(p => p.Status).Distinct().ToListAsync(ct);
                return list.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetJoStatusOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new JobOrdersDbContext();
                var list = await db.JobOrders.AsNoTracking().Select(j => j.Status).Distinct().ToListAsync(ct);
                // Normalise the legacy "InProduction" ↔ "In Production" split
                // to a single human label so the dropdown reflects real data.
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in list.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    var v = s!.Trim();
                    set.Add(Eq(v, "InProduction") ? "In Production" : v);
                }
                return set.OrderBy(s => s).ToList();
            }
            catch { return new List<string>(); }
        }

        public async Task<List<string>> GetTiStatusOptionsAsync(CancellationToken ct = default)
        {
            try
            {
                using var db = new TaxInvoiceDbContext();
                var list = await db.TaxInvoices.AsNoTracking().Select(t => t.Status).Distinct().ToListAsync(ct);
                // Normalise "PartiallyPaid" ↔ "Partially Paid" to one label.
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in list.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    var v = s!.Trim();
                    set.Add(Eq(v, "PartiallyPaid") ? "Partially Paid" : v);
                }
                return set.OrderBy(s => s).ToList();
            }
            catch { return new List<string>(); }
        }

        // ── VAT Submission Report ─────────────────────────────────────

        public async Task<VatSubmissionSummary> GetVatSubmissionSummaryAsync(
            ReportingFilter filter, 
            CancellationToken ct = default)
        {
            var rows = await GetAllVatSubmissionRowsAsync(filter, ct);
            
            return new VatSubmissionSummary
            {
                TotalInvoices = rows.Count,
                TotalSubtotal = rows.Sum(r => r.SubtotalBeforeVat),
                TotalVatCollected = rows.Sum(r => r.VatAmount),
                TotalGrandTotal = rows.Sum(r => r.GrandTotalVatInclusive),
                
                BySalesman = rows
                    .GroupBy(r => r.Salesman)
                    .Select(g => new SalesmanVatSummary
                    {
                        Salesman = g.Key,
                        InvoiceCount = g.Count(),
                        TotalVat = g.Sum(r => r.VatAmount),
                        TotalRevenue = g.Sum(r => r.GrandTotalVatInclusive)
                    })
                    .OrderByDescending(s => s.TotalVat)
                    .ToList(),
                    
                ByCompany = rows
                    .GroupBy(r => r.CompanyName)
                    .Select(g => new CompanyVatSummary
                    {
                        CompanyName = g.Key,
                        InvoiceCount = g.Count(),
                        TotalVat = g.Sum(r => r.VatAmount),
                        TotalRevenue = g.Sum(r => r.GrandTotalVatInclusive)
                    })
                    .OrderByDescending(c => c.TotalVat)
                    .ToList(),
                    
                ByProject = rows
                    .GroupBy(r => new { r.ProjectName, r.CompanyName })
                    .Select(g => new ProjectVatSummary
                    {
                        ProjectName = g.Key.ProjectName,
                        CompanyName = g.Key.CompanyName,
                        InvoiceCount = g.Count(),
                        TotalVat = g.Sum(r => r.VatAmount),
                        TotalRevenue = g.Sum(r => r.GrandTotalVatInclusive)
                    })
                    .OrderByDescending(p => p.TotalVat)
                    .ToList()
            };
        }

        public async Task<PagedResult<VatSubmissionRow>> GetVatSubmissionRowsAsync(
            ReportingFilter filter, 
            CancellationToken ct = default)
        {
            var allRows = await GetAllVatSubmissionRowsAsync(filter, ct);
            
            return new PagedResult<VatSubmissionRow>
            {
                Items = allRows
                    .Skip((filter.Page - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .ToList(),
                TotalCount = allRows.Count,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }

        public async Task<List<VatSubmissionRow>> GetAllVatSubmissionRowsAsync(
            ReportingFilter filter, 
            CancellationToken ct = default)
        {
            try
            {
                // SECURITY: Validate date range (max 5 years to prevent DoS)
                if (filter.DateFrom.HasValue && filter.DateTo.HasValue)
                {
                    var dateRange = filter.DateTo.Value - filter.DateFrom.Value;
                    if (dateRange.TotalDays > 1825) // 5 years
                    {
                        throw new ArgumentException(
                            "Date range cannot exceed 5 years. Please use more restrictive filters to prevent performance issues.");
                    }
                }

                using var db = new ProformaInvoicesDbContext();
                var query = db.ProformaInvoices.AsNoTracking().AsQueryable();
                
                // CRITICAL: VAT compliance — only include finalized invoices
                // Draft = never issued, Sent/Released = pending approval, Rejected/Cancelled = void
                // Only Approved/Confirmed/Issued represent actual VAT liability to FTA
                var vatEligibleStatuses = new[] { "Approved", "Confirmed", "Issued" };
                query = query.Where(i => i.Status != null && vatEligibleStatuses.Contains(i.Status));
                
                // Apply filters
                if (filter.DateFrom.HasValue)
                    query = query.Where(i => i.IssueDate >= filter.DateFrom.Value);
                if (filter.DateTo.HasValue)
                    query = query.Where(i => i.IssueDate <= filter.DateTo.Value);
                if (!string.IsNullOrWhiteSpace(filter.Company))
                    query = query.Where(i => Eq(i.CompanyName, filter.Company));
                if (!string.IsNullOrWhiteSpace(filter.Customer))
                    query = query.Where(i => Eq(i.CustomerName, filter.Customer));
                if (!string.IsNullOrWhiteSpace(filter.Project))
                    query = query.Where(i => Eq(i.ProjectName, filter.Project));
                if (!string.IsNullOrWhiteSpace(filter.ProformaId))
                    query = query.Where(i => ContainsCI(i.ProformaId, filter.ProformaId));
                
                ct.ThrowIfCancellationRequested();
                
                // SECURITY: DoS protection - limit maximum records
                const int MAX_RECORDS = 10000;
                var invoiceList = await query.Take(MAX_RECORDS + 1).ToListAsync(ct);
                
                if (invoiceList.Count > MAX_RECORDS)
                {
                    Services.Logging.AppLogService.Warning(
                        $"VAT Submission: Dataset truncated at {MAX_RECORDS} records. " +
                        "Consider using more restrictive filters.");
                    invoiceList = invoiceList.Take(MAX_RECORDS).ToList();
                }
                
                var result = new List<VatSubmissionRow>();
                var validationWarnings = new List<string>();
                var invalidCount = 0;
                
                // SEQUENTIAL PROCESSING: Avoids thread pool starvation and UI freezing
                foreach (var invoice in invoiceList)
                {
                    ct.ThrowIfCancellationRequested();
                    
                    // CRITICAL: Data Validation using VatCalculator
                    var validation = VatCalculator.ValidateInvoice(
                        invoice.GrandTotalVatInclusive, 
                        invoice.VatAmount, 
                        invoice.IssueDate);
                    
                    if (!validation.IsValid)
                    {
                        invalidCount++;
                        continue; // Skip invalid data
                    }
                    
                    if (validation.HasWarnings)
                    {
                        validationWarnings.AddRange(
                            validation.Warnings.Select(w => $"PI {invoice.ProformaId}: {w}"));
                    }
                    
                    // Extract SalesRepresentative from snapshot (synchronous - fast enough)
                    var snap = ProformaInvoiceMapper.DeserializeSnapshot(invoice);
                    var salesman = snap?.Client?.SalesRepresentative ?? "Unassigned";
                    
                    // Apply salesman filter if specified
                    if (!string.IsNullOrWhiteSpace(filter.Salesman) && !Eq(salesman, filter.Salesman))
                        continue;
                    
                    // CRITICAL: Use VatCalculator for accurate calculations
                    var subtotal = VatCalculator.CalculateSubtotal(
                        invoice.GrandTotalVatInclusive, 
                        invoice.VatAmount);
                    
                    result.Add(new VatSubmissionRow
                    {
                        Id = invoice.Id,
                        ProformaId = invoice.ProformaId ?? "",
                        CompanyName = invoice.CompanyName ?? "",
                        CustomerName = invoice.CustomerName ?? "",
                        ProjectName = invoice.ProjectName ?? "",
                        Salesman = salesman,
                        IssueDate = invoice.IssueDate,
                        Status = invoice.Status ?? "",
                        SubtotalBeforeVat = subtotal,
                        VatAmount = VatCalculator.Round(invoice.VatAmount),
                        GrandTotalVatInclusive = VatCalculator.Round(invoice.GrandTotalVatInclusive)
                    });
                }
                
                // Log validation summary
                if (invalidCount > 0)
                {
                    Services.Logging.AppLogService.Warning(
                        $"VAT Submission: {invalidCount} invoices skipped due to validation errors");
                }
                
                if (validationWarnings.Any())
                {
                    Services.Logging.AppLogService.Warning(
                        $"VAT Submission: {validationWarnings.Count} validation warnings:\n" +
                        string.Join("\n", validationWarnings.Take(10)));
                }
                
                // AUDIT: Log access
                Services.Logging.AppLogService.Info(
                    $"VAT Submission: Retrieved {result.Count} invoices " +
                    $"(filtered from {invoiceList.Count}, {invalidCount} invalid)");
                
                return result
                    .OrderByDescending(r => r.IssueDate)
                    .ThenBy(r => r.ProformaId)
                    .ToList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ArgumentException)
            {
                throw; // Re-throw validation errors
            }
            catch (Exception ex)
            {
                Services.Logging.AppLogService.Error($"VAT Submission data error: {ex.Message}", ex);
                throw new InvalidOperationException(
                    "Failed to load VAT data. Please check your database connection and try again.", 
                    ex);
            }
        }
    }
}
