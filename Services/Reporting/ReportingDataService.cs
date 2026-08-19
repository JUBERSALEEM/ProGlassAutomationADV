// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/ReportingDataService.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — Data Intelligence Layer (CORE)
//
// PURPOSE:
//   ONE centralized reporting/data-intelligence layer responsible for:
//     • relationships / joins / filtering / aggregation
//     • row-level data / specification matching / quantity reconciliation
//     • drill-down / profitability / cross-module traceability
//
//   All report ViewModels consume this service. No ViewModel contains
//   independent DB logic. All queries use:
//     • AsNoTracking() — read-only, no change tracking
//     • .Select() projections — JSON blobs never read unless needed
//     • Database-side filtering where possible
//     • CancellationToken + async
//     • Pagination for large result sets
//
// DOCUMENT RELATIONSHIPS (verified from entities, not assumed):
//   SavedJobOrder.SourceProformaId      → PI
//   SavedDeliveryNote.ProformaId        → PI
//   SavedDeliveryNote.JobOrderId        → JO
//   SavedDeliveryNote.IsMasterDn        → master tracker per JO
//   SavedTaxInvoice.ProformaId          → PI
//   SavedTaxInvoice.JobOrderId          → JO
//   SavedTaxInvoice.DeliveryNoteReference → DN(s) comma-separated
//   MasterData: Company / Client / Project / Salesman
//   ProfitLoss snapshot embedded in PI/JO snapshots
//
// ROW-LEVEL SOURCE:
//   PI snapshot is JSON: ProformaInvoiceSnapshot.SpecGroups[].ItemRows[]
//   DN items: DeliveryNoteItem + DnItemSnapshot (ordered/delivered/balance)
//   TI rows: TaxInvoiceSnapshot.SpecGroups[].Rows[]
//   Quantity reconciliation uses Master DN totals per JO (not GetAllAsync).
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
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

        private static bool WithinNullable(DateTime? dt, DateTime? from, DateTime? to)
        {
            if (!dt.HasValue) return false;
            return Within(dt.Value, from, to);
        }

        // ── Helpers to apply global filters on indexed columns ─────────

        private static bool MatchCommonPi(ProformaAnalyticsRowExpanded r, ReportingFilter f)
        {
            if (f.HasCompany && !ContainsCI(r.CompanyName, f.Company)) return false;
            if (f.HasCustomer && !ContainsCI(r.CustomerName, f.Customer)) return false;
            if (!string.IsNullOrWhiteSpace(f.ProformaId) && !Eq(r.ProformaId, f.ProformaId!)) return false;
            if (!string.IsNullOrWhiteSpace(f.Status) && !Eq(r.Status, f.Status!)) return false;
            if (!Within(r.IssueDate == default ? r.CreatedAt : r.IssueDate, f.DateFrom, f.DateTo)) return false;
            // Project filter: check ProjectName
            if (f.HasProject && !ContainsCI(r.ProjectName, f.Project)) return false;
            // Salesman not stored as indexed column on PI — filtered via snapshot post-process or masterdata
            if (f.HasSearch && !(
                ContainsCI(r.ProformaId, f.SearchText) ||
                ContainsCI(r.CustomerName, f.SearchText) ||
                ContainsCI(r.CompanyName, f.SearchText) ||
                ContainsCI(r.ProjectName, f.SearchText) ||
                ContainsCI(r.Status, f.SearchText))) return false;
            return true;
        }

        // Expanded row that includes ProjectName/I-Company etc for filtering
        private sealed class ProformaAnalyticsRowExpanded
        {
            public string ProformaId { get; set; } = "";
            public string CompanyName { get; set; } = "";
            public string CustomerName { get; set; } = "";
            public string ProjectName { get; set; } = "";
            public string Status { get; set; } = "";
            public DateTime IssueDate { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public decimal GrandTotal { get; set; }
        }

        // ── Snapshot deserialization helpers (never throw) ─────────────

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
            var rows = new List<GlassSpecReportRow>();
            if (snap?.SpecGroups == null) return rows;
            foreach (var grp in snap.SpecGroups)
            {
                if (grp?.ItemRows == null) continue;
                foreach (var r in grp.ItemRows)
                {
                    if (r == null) continue;
                    // Skip empty placeholder rows
                    if (string.IsNullOrWhiteSpace(r.GlassRef) && r.W1 == 0 && r.H1 == 0) continue;

                    var sqm1 = r.Sqm1; // computed via SqmCalculator
                    var totalSqm = r.TotalSqm;
                    var rate = r.Rate;
                    var amount = Math.Round(sqm1 * r.Qty * rate, 2);
                    var canonical = SpecificationMatcher.BuildCanonicalKey(
                        r.GlassRef, grp.SelectedGlassType, grp.SelectedSpecName,
                        grp.OuterGlassColor, grp.InnerGlassColor, r.W1, r.H1, r.W2, r.H2);

                    rows.Add(new GlassSpecReportRow
                    {
                        RowId = $"{proformaId}-G{grp.GroupNumber}-S{r.SerialNumber}",
                        ProformaId = proformaId,
                        GlassRef = r.GlassRef ?? "",
                        SpecName = grp.SelectedSpecName ?? grp.GroupName ?? "",
                        GlassType = grp.SelectedGlassType ?? "",
                        OuterColor = grp.OuterGlassColor ?? "",
                        InnerColor = grp.InnerGlassColor ?? "",
                        W1 = r.W1, H1 = r.H1, W2 = r.W2, H2 = r.H2,
                        Qty = r.Qty,
                        Sqm1 = sqm1,
                        TotalSqm = totalSqm,
                        Rate = rate,
                        Amount = amount,
                        BaseRate = grp.BaseRate,
                        GroupNumber = grp.GroupNumber,
                        GroupName = grp.GroupName ?? "",
                        SerialNumber = r.SerialNumber,
                        OuterSheetWidthMm = grp.OuterSheetWidthMm,
                        OuterSheetHeightMm = grp.OuterSheetHeightMm,
                        InnerSheetWidthMm = grp.InnerSheetWidthMm,
                        InnerSheetHeightMm = grp.InnerSheetHeightMm,
                        CompanyName = company,
                        CustomerName = customer,
                        Salesman = salesman,
                        ProjectName = snap.Project?.ProjectIdentifierName ?? snap.Project?.ProjectReference ?? "",
                        IssueDate = issueDate,
                        PiStatus = piStatus,
                        CanonicalKey = canonical,
                        DimensionsKey = $"{r.W1}×{r.H1}" + (r.W2 > 0 || r.H2 > 0 ? $" / {r.W2}×{r.H2}" : "")
                    });
                }
            }
            return rows;
        }

        // ════════════════════════════════════════════════════════════════
        //  PI REPORT
        // ════════════════════════════════════════════════════════════════

        public async Task<PiReportSummary> GetPiSummaryAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            var summary = new PiReportSummary();

            using var db = new ProformaInvoicesDbContext();
            var all = await db.ProformaInvoices.AsNoTracking()
                .Select(i => new ProformaAnalyticsRowExpanded
                {
                    ProformaId = i.ProformaId,
                    CompanyName = i.CompanyName,
                    CustomerName = i.CustomerName,
                    ProjectName = i.ProjectName,
                    Status = i.Status,
                    IssueDate = i.IssueDate,
                    CreatedAt = i.CreatedAt,
                    UpdatedAt = i.UpdatedAt,
                    GrandTotal = i.GrandTotalVatInclusive
                }).ToListAsync(ct);

            // Need JO links for Converted counting — load SourceProformaIds
            HashSet<string> joSources = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var joDb = new JobOrdersDbContext();
                var joIds = await joDb.JobOrders.AsNoTracking()
                    .Select(j => j.SourceProformaId).ToListAsync(ct);
                foreach (var id in joIds.Where(s => !string.IsNullOrWhiteSpace(s)))
                    joSources.Add(id!);
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

            var filtered = all.Where(r => MatchCommonPi(r, filter)).ToList();
            // Salesman filter — need to resolve via snapshot or MasterData if filter has salesman
            // For summary we approximate: if salesman filter present, we must deserialize matching PIs
            if (filter.HasSalesman)
            {
                var salesmanFiltered = new List<ProformaAnalyticsRowExpanded>();
                var piIds = filtered.Select(r => r.ProformaId).ToList();
                var fullRows = await db.ProformaInvoices.AsNoTracking()
                    .Where(i => piIds.Contains(i.ProformaId))
                    .Select(i => new { i.ProformaId, i.FullSnapshotJson })
                    .ToListAsync(ct);
                var piMap = fullRows.ToDictionary(x => x.ProformaId, x => x.FullSnapshotJson, StringComparer.OrdinalIgnoreCase);
                foreach (var r in filtered)
                {
                    if (piMap.TryGetValue(r.ProformaId, out var json))
                    {
                        var snap = TryDeserializePiSnapshot(json);
                        var sm = snap?.Client?.SalesRepresentative ?? "";
                        if (ContainsCI(sm, filter.Salesman)) salesmanFiltered.Add(r);
                    }
                }
                filtered = salesmanFiltered;
            }

            summary.TotalPi = filtered.Count;
            summary.TotalValue = filtered.Sum(r => r.GrandTotal);
            summary.Draft = filtered.Count(r => Eq(r.Status, "Draft"));
            summary.Released = filtered.Count(r => Eq(r.Status, "Released") || Eq(r.Status, "Sent"));
            summary.Confirmed = filtered.Count(r => Eq(r.Status, "Approved") || Eq(r.Status, "Confirmed") || Eq(r.Status, "Issued"));
            summary.Rejected = filtered.Count(r => Eq(r.Status, "Rejected") || Eq(r.Status, "Cancelled") || Eq(r.Status, "Void"));
            summary.Pending = filtered.Count(r => Eq(r.Status, "Sent") || Eq(r.Status, "Pending") || Eq(r.Status, "Draft"));
            summary.Converted = filtered.Count(r => joSources.Contains(r.ProformaId));
            summary.NotConverted = summary.TotalPi - summary.Converted;

            // Totals across snapshots for Qty/Sqm — sample loading
            // For large datasets, we approximate via indexed columns; for now we attempt to load snapshots for filtered set
            try
            {
                if (filtered.Count > 0 && filtered.Count <= 500)
                {
                    var ids = filtered.Select(r => r.ProformaId).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var snapshots = await db.ProformaInvoices.AsNoTracking()
                        .Where(i => ids.Contains(i.ProformaId))
                        .Select(i => new { i.ProformaId, i.CompanyName, i.CustomerName, i.FullSnapshotJson, i.IssueDate, i.Status })
                        .ToListAsync(ct);
                    int totalQty = 0;
                    decimal totalSqm = 0;
                    foreach (var s in snapshots)
                    {
                        var snap = TryDeserializePiSnapshot(s.FullSnapshotJson);
                        if (snap == null) continue;
                        var rows = ExtractGlassRowsFromPiSnapshot(snap, s.ProformaId, s.CompanyName, s.CustomerName, snap.Client?.SalesRepresentative ?? "", s.IssueDate, s.Status);
                        totalQty += rows.Sum(r => r.Qty);
                        totalSqm += rows.Sum(r => r.TotalSqm);
                    }
                    summary.TotalQty = totalQty;
                    summary.TotalSqm = Math.Round(totalSqm, 3);
                }
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

            return summary;
        }

        public async Task<PagedResult<PiReportRow>> GetPiRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new ProformaInvoicesDbContext();

            // Load all indexed rows (narrow projection) — DB-side filter on indexed columns where possible
            var query = db.ProformaInvoices.AsNoTracking().AsQueryable();

            // Apply DB-side filters that are indexed
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

            var all = await query.Select(i => new
            {
                i.Id, i.ProformaId, i.Status, i.CompanyName, i.CustomerName, i.ProjectName,
                i.IssueDate, i.ValidUntil, i.GrandTotalVatInclusive, i.FullSnapshotJson, i.CreatedAt, i.UpdatedAt
            }).OrderByDescending(i => i.CreatedAt).ToListAsync(ct);

            // Secondary filters (search, project text, salesman via snapshot)
            var filtered = new List<dynamic>();
            foreach (var r in all)
            {
                if (filter.HasProject && !ContainsCI(r.ProjectName, filter.Project)) continue;
                if (filter.HasSearch)
                {
                    var hit = ContainsCI(r.ProformaId, filter.SearchText) ||
                              ContainsCI(r.CustomerName, filter.SearchText) ||
                              ContainsCI(r.CompanyName, filter.SearchText) ||
                              ContainsCI(r.ProjectName, filter.SearchText);
                    if (!hit) continue;
                }
                filtered.Add(r);
            }

            // Salesman filter requires snapshot parse
            if (filter.HasSalesman)
            {
                var keep = new List<dynamic>();
                foreach (var r in filtered)
                {
                    var snap = TryDeserializePiSnapshot((string)r.FullSnapshotJson);
                    var sm = snap?.Client?.SalesRepresentative ?? "";
                    if (ContainsCI(sm, filter.Salesman)) keep.Add(r);
                }
                filtered = keep;
            }

            // GlassType / Specification filters require snapshot parse
            if (!string.IsNullOrWhiteSpace(filter.GlassType) || !string.IsNullOrWhiteSpace(filter.Specification) || !string.IsNullOrWhiteSpace(filter.GlassRef))
            {
                var keep = new List<dynamic>();
                foreach (var r in filtered)
                {
                    var snap = TryDeserializePiSnapshot((string)r.FullSnapshotJson);
                    if (snap?.SpecGroups == null) continue;
                    bool match = false;
                    foreach (var g in snap.SpecGroups)
                    {
                        if (!string.IsNullOrWhiteSpace(filter.GlassType) && !Eq(g.SelectedGlassType, filter.GlassType!)) continue;
                        if (!string.IsNullOrWhiteSpace(filter.Specification) && !ContainsCI(g.SelectedSpecName, filter.Specification)) continue;
                        if (!string.IsNullOrWhiteSpace(filter.GlassRef))
                        {
                            if (g.ItemRows == null || !g.ItemRows.Any(ir => ContainsCI(ir.GlassRef, filter.GlassRef))) continue;
                        }
                        match = true; break;
                    }
                    if (match) keep.Add(r);
                }
                filtered = keep;
            }

            // Get JO links for flag
            HashSet<string> joSources = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> dnPi = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> tiPi = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var joDb = new JobOrdersDbContext();
                var joIds = await joDb.JobOrders.AsNoTracking().Select(j => j.SourceProformaId).ToListAsync(ct);
                foreach (var id in joIds.Where(s => !string.IsNullOrWhiteSpace(s))) joSources.Add(id!);
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            try
            {
                using var dnDb = new DeliveryNotesDbContext();
                var dnIds = await dnDb.DeliveryNotes.AsNoTracking().Where(d => !d.IsMasterDn).Select(d => d.ProformaId).ToListAsync(ct);
                foreach (var id in dnIds.Where(s => !string.IsNullOrWhiteSpace(s))) dnPi.Add(id!);
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            try
            {
                using var tiDb = new TaxInvoiceDbContext();
                var tiIds = await tiDb.TaxInvoices.AsNoTracking().Select(t => t.ProformaId).ToListAsync(ct);
                foreach (var id in tiIds.Where(s => !string.IsNullOrWhiteSpace(s))) tiPi.Add(id!);
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

            var total = filtered.Count;
            var pageItems = filtered.Skip(filter.Skip).Take(filter.Take).ToList();

            var resultRows = new List<PiReportRow>();
            foreach (var r in pageItems)
            {
                var snap = TryDeserializePiSnapshot((string)r.FullSnapshotJson);
                var rows = ExtractGlassRowsFromPiSnapshot(snap ?? new ProformaInvoiceSnapshot(), (string)r.ProformaId, (string)r.CompanyName, (string)r.CustomerName, snap?.Client?.SalesRepresentative ?? "", (DateTime)r.IssueDate, (string)r.Status);
                var salesman = snap?.Client?.SalesRepresentative ?? "";
                resultRows.Add(new PiReportRow
                {
                    Id = (int)r.Id,
                    ProformaId = (string)r.ProformaId,
                    Status = (string)r.Status,
                    CompanyName = (string)r.CompanyName,
                    CustomerName = (string)r.CustomerName,
                    ProjectName = (string)r.ProjectName,
                    Salesman = salesman,
                    IssueDate = (DateTime)r.IssueDate,
                    ValidUntil = (DateTime)r.ValidUntil,
                    GrandTotal = (decimal)r.GrandTotalVatInclusive,
                    SpecGroupCount = snap?.SpecGroups?.Count ?? 0,
                    TotalRows = rows.Count,
                    TotalQty = rows.Sum(x => x.Qty),
                    TotalSqm = Math.Round(rows.Sum(x => x.TotalSqm), 3),
                    HasJobOrder = joSources.Contains((string)r.ProformaId),
                    HasDelivery = dnPi.Contains((string)r.ProformaId),
                    HasTaxInvoice = tiPi.Contains((string)r.ProformaId),
                    CreatedAt = (DateTime)r.CreatedAt,
                    UpdatedAt = (DateTime)r.UpdatedAt
                });
            }

            return new PagedResult<PiReportRow> { Items = resultRows, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }

        public async Task<List<PiReportRow>> GetPiLifecycleAsync(string proformaId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(proformaId)) return new List<PiReportRow>();
            var filter = new ReportingFilter { ProformaId = proformaId.Trim(), PageSize = 100 };
            var res = await GetPiRowsAsync(filter, ct);
            return res.Items;
        }

        // ════════════════════════════════════════════════════════════════
        //  JO REPORT
        // ════════════════════════════════════════════════════════════════

        public async Task<JoReportSummary> GetJoSummaryAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new JobOrdersDbContext();
            var all = await db.JobOrders.AsNoTracking()
                .Select(j => new { j.JobOrderId, j.SourceProformaId, j.Status, j.Priority, j.CustomerName, j.DeliveryDate, j.GrandTotalVatInclusive, j.CreatedAt, j.UpdatedAt, j.FullSnapshotJson })
                .ToListAsync(ct);

            // Apply filters in memory (small projection)
            var filtered = all.Where(r =>
                (string.IsNullOrWhiteSpace(filter.ProformaId) || Eq(r.SourceProformaId, filter.ProformaId!)) &&
                (string.IsNullOrWhiteSpace(filter.JobOrderId) || Eq(r.JobOrderId, filter.JobOrderId!)) &&
                (string.IsNullOrWhiteSpace(filter.Customer) || ContainsCI(r.CustomerName, filter.Customer)) &&
                (string.IsNullOrWhiteSpace(filter.Status) || Eq(r.Status, filter.Status!)) &&
                (!filter.DateFrom.HasValue || !r.DeliveryDate.HasValue || r.DeliveryDate.Value.Date >= filter.DateFrom.Value.Date) &&
                (!filter.DateTo.HasValue || !r.DeliveryDate.HasValue || r.DeliveryDate.Value.Date <= filter.DateTo.Value.Date) &&
                (string.IsNullOrWhiteSpace(filter.SearchText) || ContainsCI(r.JobOrderId, filter.SearchText) || ContainsCI(r.SourceProformaId, filter.SearchText) || ContainsCI(r.CustomerName, filter.SearchText))
            ).ToList();

            // Need PI count for awaiting JO
            int totalPi = 0;
            try
            {
                using var piDb = new ProformaInvoicesDbContext();
                var piCount = await piDb.ProformaInvoices.CountAsync(ct);
                totalPi = piCount;
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

            var summary = new JoReportSummary
            {
                TotalJo = filtered.Count,
                FromPi = filtered.Count,
                AwaitingJo = Math.Max(0, totalPi - filtered.Count)
            };

            foreach (var g in filtered.GroupBy(x => string.IsNullOrWhiteSpace(x.Status) ? "Unknown" : x.Status))
                summary.ByStatus[g.Key] = g.Count();
            foreach (var g in filtered.GroupBy(x => string.IsNullOrWhiteSpace(x.Priority) ? "Unknown" : x.Priority))
                summary.ByPriority[g.Key] = g.Count();

            // Qty/Sqm from snapshots (sample)
            int qty = 0; decimal sqm = 0; decimal val = filtered.Sum(x => x.GrandTotalVatInclusive);
            if (filtered.Count > 0 && filtered.Count <= 300)
            {
                foreach (var r in filtered)
                {
                    var snap = TryDeserializeJoSnapshot(r.FullSnapshotJson);
                    if (snap?.EmbeddedInvoice?.SpecGroups == null) continue;
                    foreach (var g in snap.EmbeddedInvoice.SpecGroups)
                    {
                        if (g?.ItemRows == null) continue;
                        foreach (var row in g.ItemRows)
                        {
                            if (string.IsNullOrWhiteSpace(row.GlassRef) && row.W1 == 0 && row.H1 == 0) continue;
                            qty += row.Qty;
                            sqm += row.TotalSqm;
                        }
                    }
                }
            }
            summary.TotalQty = qty;
            summary.TotalSqm = Math.Round(sqm, 3);
            summary.TotalValue = val;
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

            var all = await query.Select(j => new
            {
                j.Id, j.JobOrderId, j.SourceProformaId, j.CustomerName, j.Status, j.Priority, j.DeliveryDate,
                j.GrandTotalVatInclusive, j.FullSnapshotJson, j.CreatedAt, j.UpdatedAt
            }).OrderByDescending(j => j.CreatedAt).ToListAsync(ct);

            var filtered = all.Where(r =>
                (string.IsNullOrWhiteSpace(filter.SearchText) || ContainsCI(r.JobOrderId, filter.SearchText) || ContainsCI(r.SourceProformaId, filter.SearchText) || ContainsCI(r.CustomerName, filter.SearchText))
            ).ToList();

            // Optional: Project filter via snapshot, Salesman etc.
            if (filter.HasProject || filter.HasSalesman || !string.IsNullOrWhiteSpace(filter.Company))
            {
                var keep = new List<dynamic>();
                foreach (var r in filtered)
                {
                    var snap = TryDeserializeJoSnapshot(r.FullSnapshotJson);
                    var proj = snap?.EmbeddedInvoice?.Project?.ProjectIdentifierName ?? snap?.EmbeddedInvoice?.Project?.ProjectReference ?? "";
                    var sm = snap?.EmbeddedInvoice?.Client?.SalesRepresentative ?? "";
                    var comp = snap?.EmbeddedInvoice?.Company?.CompanyName ?? "";
                    if (filter.HasProject && !ContainsCI(proj, filter.Project)) continue;
                    if (filter.HasSalesman && !ContainsCI(sm, filter.Salesman)) continue;
                    if (!string.IsNullOrWhiteSpace(filter.Company) && !ContainsCI(comp, filter.Company)) continue;
                    keep.Add(r);
                }
                filtered = keep;
            }

            var total = filtered.Count;
            var page = filtered.Skip(filter.Skip).Take(filter.Take).ToList();

            // Need DN balance per JO via Master DNs
            Dictionary<string, (int Ordered, int Delivered)> masterMap = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var dnDb = new DeliveryNotesDbContext();
                var masters = await dnDb.DeliveryNotes.AsNoTracking()
                    .Where(d => d.IsMasterDn)
                    .Select(d => new { d.JobOrderId, d.TotalOrderedQty, d.TotalDeliveredQty })
                    .ToListAsync(ct);
                foreach (var m in masters)
                    if (!string.IsNullOrWhiteSpace(m.JobOrderId))
                        masterMap[m.JobOrderId!] = (m.TotalOrderedQty, m.TotalDeliveredQty);
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

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

            return new PagedResult<JoReportRow> { Items = rows, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }

        // ════════════════════════════════════════════════════════════════
        //  DELIVERY REPORT
        // ════════════════════════════════════════════════════════════════

        public async Task<DeliveryReportSummary> GetDeliverySummaryAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new DeliveryNotesDbContext();
            var all = await db.DeliveryNotes.AsNoTracking()
                .Where(d => !d.IsMasterDn)
                .Select(d => new { d.DeliveryNoteId, d.JobOrderId, d.ProformaId, d.CustomerName, d.ProjectName, d.Status, d.DeliveryDate, d.TotalOrderedQty, d.TotalDeliveredQty, d.TotalBalanceQty, d.IsMasterDn })
                .ToListAsync(ct);

            var filtered = all.Where(r =>
                (string.IsNullOrWhiteSpace(filter.ProformaId) || Eq(r.ProformaId, filter.ProformaId!)) &&
                (string.IsNullOrWhiteSpace(filter.JobOrderId) || Eq(r.JobOrderId, filter.JobOrderId!)) &&
                (string.IsNullOrWhiteSpace(filter.DeliveryNoteId) || Eq(r.DeliveryNoteId, filter.DeliveryNoteId!)) &&
                (string.IsNullOrWhiteSpace(filter.Customer) || ContainsCI(r.CustomerName, filter.Customer)) &&
                (filter.HasProject ? ContainsCI(r.ProjectName, filter.Project) : true) &&
                (string.IsNullOrWhiteSpace(filter.Status) || Eq(r.Status, filter.Status!)) &&
                (!filter.DateFrom.HasValue || r.DeliveryDate.Date >= filter.DateFrom.Value.Date) &&
                (!filter.DateTo.HasValue || r.DeliveryDate.Date <= filter.DateTo.Value.Date) &&
                (string.IsNullOrWhiteSpace(filter.SearchText) || ContainsCI(r.DeliveryNoteId, filter.SearchText) || ContainsCI(r.JobOrderId, filter.SearchText) || ContainsCI(r.CustomerName, filter.SearchText))
            ).ToList();

            var summary = new DeliveryReportSummary
            {
                TotalDn = filtered.Count,
                TotalOrderedQty = filtered.Sum(x => x.TotalOrderedQty),
                TotalDeliveredQty = filtered.Sum(x => x.TotalDeliveredQty),
                TotalRemainingQty = filtered.Sum(x => x.TotalBalanceQty)
            };

            // Status buckets: Derive pending/partial/complete from Balance
            summary.Pending = filtered.Count(x => x.TotalBalanceQty == x.TotalOrderedQty || x.TotalDeliveredQty == 0);
            summary.Completed = filtered.Count(x => x.TotalBalanceQty == 0 && x.TotalOrderedQty > 0);
            summary.Partial = filtered.Count - summary.Pending - summary.Completed;
            if (summary.Partial < 0) summary.Partial = 0;
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

            var all = await query.Select(d => new
            {
                d.Id, d.DeliveryNoteId, d.JobOrderId, d.ProformaId, d.CustomerName, d.ProjectName, d.Status, d.DeliveryType, d.IsMasterDn, d.HasTaxInvoice, d.DeliveryDate, d.TotalOrderedQty, d.TotalDeliveredQty, d.TotalBalanceQty, d.DriverName, d.CreatedAt
            }).OrderByDescending(d => d.DeliveryDate).ThenByDescending(d => d.CreatedAt).ToListAsync(ct);

            var filtered = all.Where(r =>
                (filter.HasProject ? ContainsCI(r.ProjectName, filter.Project) : true) &&
                (string.IsNullOrWhiteSpace(filter.SearchText) || ContainsCI(r.DeliveryNoteId, filter.SearchText) || ContainsCI(r.JobOrderId, filter.SearchText) || ContainsCI(r.CustomerName, filter.SearchText))
            ).ToList();

            var total = filtered.Count;
            var page = filtered.Skip(filter.Skip).Take(filter.Take).ToList();

            var rows = page.Select(r => new DeliveryReportRow
            {
                Id = r.Id,
                DeliveryNoteId = r.DeliveryNoteId,
                JobOrderId = r.JobOrderId,
                ProformaId = r.ProformaId,
                CustomerName = r.CustomerName,
                ProjectName = r.ProjectName,
                Status = r.Status,
                DeliveryType = r.DeliveryType,
                IsMasterDn = r.IsMasterDn,
                HasTaxInvoice = r.HasTaxInvoice,
                DeliveryDate = r.DeliveryDate,
                TotalOrderedQty = r.TotalOrderedQty,
                TotalDeliveredQty = r.TotalDeliveredQty,
                TotalBalanceQty = r.TotalBalanceQty,
                DriverName = r.DriverName,
                CreatedAt = r.CreatedAt
            }).ToList();

            return new PagedResult<DeliveryReportRow> { Items = rows, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }

        // ════════════════════════════════════════════════════════════════
        //  TAX INVOICE REPORT
        // ════════════════════════════════════════════════════════════════

        public async Task<TaxInvoiceReportSummary> GetTaxInvoiceSummaryAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new TaxInvoiceDbContext();
            var all = await db.TaxInvoices.AsNoTracking()
                .Select(t => new { t.InvoiceNumber, t.ProformaId, t.Status, t.InvoiceType, t.CustomerName, t.CompanyName, t.ProjectName, t.IssueDate, t.DueDate, t.GrandTotal, t.VatAmount })
                .ToListAsync(ct);

            var filtered = all.Where(r =>
                (string.IsNullOrWhiteSpace(filter.ProformaId) || Eq(r.ProformaId, filter.ProformaId!)) &&
                (string.IsNullOrWhiteSpace(filter.TaxInvoiceNumber) || Eq(r.InvoiceNumber, filter.TaxInvoiceNumber!)) &&
                (string.IsNullOrWhiteSpace(filter.Customer) || ContainsCI(r.CustomerName, filter.Customer)) &&
                (string.IsNullOrWhiteSpace(filter.Company) || ContainsCI(r.CompanyName, filter.Company)) &&
                (filter.HasProject ? ContainsCI(r.ProjectName, filter.Project) : true) &&
                (string.IsNullOrWhiteSpace(filter.Status) || Eq(r.Status, filter.Status!)) &&
                (!filter.DateFrom.HasValue || r.IssueDate.Date >= filter.DateFrom.Value.Date) &&
                (!filter.DateTo.HasValue || r.IssueDate.Date <= filter.DateTo.Value.Date) &&
                (string.IsNullOrWhiteSpace(filter.SearchText) || ContainsCI(r.InvoiceNumber, filter.SearchText) || ContainsCI(r.ProformaId, filter.SearchText) || ContainsCI(r.CustomerName, filter.SearchText))
            ).ToList();

            var summary = new TaxInvoiceReportSummary
            {
                TotalInvoices = filtered.Count,
                Issued = filtered.Count(r => Eq(r.Status, "Issued")),
                Paid = filtered.Count(r => Eq(r.Status, "Paid")),
                PartiallyPaid = filtered.Count(r => Eq(r.Status, "PartiallyPaid") || Eq(r.Status, "Partially Paid")),
                Overdue = filtered.Count(r => Eq(r.Status, "Overdue") || (Eq(r.Status, "Issued") && r.DueDate.Date < DateTime.Today)),
                Draft = filtered.Count(r => Eq(r.Status, "Draft")),
                TotalAmount = filtered.Sum(r => r.GrandTotal),
                TotalVat = filtered.Sum(r => r.VatAmount),
                Outstanding = filtered.Where(r => Eq(r.Status, "Issued") || Eq(r.Status, "Overdue") || Eq(r.Status, "PartiallyPaid")).Sum(r => r.GrandTotal)
            };

            // Pending invoice: confirmed PI without TI
            try
            {
                using var piDb = new ProformaInvoicesDbContext();
                var confirmedPis = await piDb.ProformaInvoices.AsNoTracking()
                    .Where(p => p.Status == "Approved" || p.Status == "Confirmed" || p.Status == "Issued")
                    .Select(p => p.ProformaId).ToListAsync(ct);
                var invoicedPis = new HashSet<string>(filtered.Select(r => r.ProformaId), StringComparer.OrdinalIgnoreCase);
                // Also include all TI pis (unfiltered) for accurate pending count
                var allTiPis = new HashSet<string>(all.Select(r => r.ProformaId), StringComparer.OrdinalIgnoreCase);
                summary.PendingInvoice = confirmedPis.Count(pi => !allTiPis.Contains(pi));
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

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

            var all = await query.Select(t => new
            {
                t.Id, t.InvoiceNumber, t.ProformaId, t.JobOrderId, t.DeliveryNoteReference, t.CustomerName, t.ProjectName, t.CompanyName, t.Status, t.InvoiceType, t.IssueDate, t.DueDate, t.GrandTotal, t.VatAmount, t.TotalQty, t.TotalSqm, t.CreatedAt
            }).OrderByDescending(t => t.IssueDate).ToListAsync(ct);

            var filtered = all.Where(r =>
                (filter.HasProject ? ContainsCI(r.ProjectName, filter.Project) : true) &&
                (string.IsNullOrWhiteSpace(filter.SearchText) || ContainsCI(r.InvoiceNumber, filter.SearchText) || ContainsCI(r.ProformaId, filter.SearchText) || ContainsCI(r.CustomerName, filter.SearchText))
            ).ToList();

            var total = filtered.Count;
            var page = filtered.Skip(filter.Skip).Take(filter.Take).ToList();
            var rows = page.Select(r => new TaxInvoiceReportRow
            {
                Id = r.Id,
                InvoiceNumber = r.InvoiceNumber,
                ProformaId = r.ProformaId,
                JobOrderId = r.JobOrderId,
                DeliveryNoteReference = r.DeliveryNoteReference,
                CustomerName = r.CustomerName,
                ProjectName = r.ProjectName,
                CompanyName = r.CompanyName,
                Status = r.Status,
                InvoiceType = r.InvoiceType,
                IssueDate = r.IssueDate,
                DueDate = r.DueDate,
                GrandTotal = r.GrandTotal,
                VatAmount = r.VatAmount,
                TotalQty = r.TotalQty,
                TotalSqm = r.TotalSqm,
                CreatedAt = r.CreatedAt
            }).ToList();

            return new PagedResult<TaxInvoiceReportRow> { Items = rows, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }

        public async Task<List<TaxInvoiceReportRow>> GetPendingInvoicesAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            // Confirmed PI without TI — useful for follow-up
            filter ??= new ReportingFilter();
            var tiSummary = await GetTaxInvoiceSummaryAsync(filter, ct);
            // This is a summary method; return empty list if no pending logic needed via rows
            // Instead, directly query confirmed PIs without TI
            var result = new List<TaxInvoiceReportRow>();
            try
            {
                using var piDb = new ProformaInvoicesDbContext();
                using var tiDb = new TaxInvoiceDbContext();
                var confirmedPis = await piDb.ProformaInvoices.AsNoTracking()
                    .Where(p => p.Status == "Approved" || p.Status == "Confirmed" || p.Status == "Issued" || p.Status == "Sent")
                    .Select(p => new { p.ProformaId, p.CustomerName, p.ProjectName, p.CompanyName, p.GrandTotalVatInclusive, p.IssueDate })
                    .ToListAsync(ct);
                var invoicedPis = new HashSet<string>(
                    await tiDb.TaxInvoices.AsNoTracking().Select(t => t.ProformaId).ToListAsync(ct),
                    StringComparer.OrdinalIgnoreCase);

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
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            return result;
        }

        // ════════════════════════════════════════════════════════════════
        //  COMPANY / CUSTOMER / SALESMAN / PROJECT
        // ════════════════════════════════════════════════════════════════

        public async Task<PagedResult<CompanyReportRow>> GetCompanyRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            // Aggregate from PI grouped by CompanyName, then enrich with JO/DN/TI counts per company
            using var piDb = new ProformaInvoicesDbContext();
            var piAll = await piDb.ProformaInvoices.AsNoTracking()
                .Select(p => new { p.CompanyName, p.ProformaId, p.Status, p.GrandTotalVatInclusive, p.CustomerName, p.ProjectName, p.IssueDate })
                .ToListAsync(ct);

            var groups = piAll.GroupBy(p => string.IsNullOrWhiteSpace(p.CompanyName) ? "Unknown" : p.CompanyName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new CompanyReportRow
                {
                    CompanyName = g.Key,
                    PiCount = g.Count(),
                    PiValue = g.Sum(x => x.GrandTotalVatInclusive)
                }).ToList();

            // Filter search
            if (filter.HasSearch)
                groups = groups.Where(g => ContainsCI(g.CompanyName, filter.SearchText)).ToList();
            if (filter.HasCompany)
                groups = groups.Where(g => ContainsCI(g.CompanyName, filter.Company)).ToList();

            // Enrich with JO/DN/TI counts — need company names from snapshots or master data
            // For now, count via Pi ProformaIds per company
            var piByCompany = piAll.GroupBy(p => string.IsNullOrWhiteSpace(p.CompanyName) ? "Unknown" : p.CompanyName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(x => x.ProformaId), StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            try
            {
                using var joDb = new JobOrdersDbContext();
                var joAll = await joDb.JobOrders.AsNoTracking().Select(j => j.SourceProformaId).ToListAsync(ct);
                var joSet = new HashSet<string>(joAll.Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);
                foreach (var g in groups)
                {
                    if (piByCompany.TryGetValue(g.CompanyName, out var pis))
                        g.JobOrderCount = pis.Count(pi => joSet.Contains(pi));
                }
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

            try
            {
                using var tiDb = new TaxInvoiceDbContext();
                var tiAll = await tiDb.TaxInvoices.AsNoTracking().Select(t => new { t.ProformaId, t.GrandTotal, t.Status }).ToListAsync(ct);
                var tiByPi = tiAll.GroupBy(t => t.ProformaId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
                foreach (var g in groups)
                {
                    if (!piByCompany.TryGetValue(g.CompanyName, out var pis)) continue;
                    int tiCount = 0; decimal invoiced = 0; int overdue = 0;
                    foreach (var pi in pis)
                    {
                        if (tiByPi.TryGetValue(pi, out var list))
                        {
                            tiCount += list.Count;
                            invoiced += list.Sum(x => x.GrandTotal);
                            overdue += list.Count(x => Eq(x.Status, "Overdue") || (Eq(x.Status, "Issued") && false));
                        }
                    }
                    g.TaxInvoiceCount = tiCount;
                    g.InvoicedValue = invoiced;
                    g.OverdueInvoices = overdue;
                }
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

            // Paginate
            var total = groups.Count;
            var page = groups.OrderByDescending(g => g.PiValue).Skip(filter.Skip).Take(filter.Take).ToList();
            return new PagedResult<CompanyReportRow> { Items = page, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }

        public async Task<PagedResult<CustomerReportRow>> GetCustomerRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var piDb = new ProformaInvoicesDbContext();
            var piAll = await piDb.ProformaInvoices.AsNoTracking()
                .Select(p => new { p.CustomerName, p.ProformaId, p.GrandTotalVatInclusive, p.CompanyName, p.ProjectName, p.Status, p.FullSnapshotJson })
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
                    catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
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

            // Enrich with TI invoiced + JO + DN counts per customer via PI set
            var piByCustomer = piAll.GroupBy(p => string.IsNullOrWhiteSpace(p.CustomerName) ? "Unknown" : p.CustomerName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(x => x.ProformaId), StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            try
            {
                using var joDb = new JobOrdersDbContext();
                var joPis = new HashSet<string>(await joDb.JobOrders.AsNoTracking().Select(j => j.SourceProformaId).ToListAsync(ct), StringComparer.OrdinalIgnoreCase);
                foreach (var g in groups)
                    if (piByCustomer.TryGetValue(g.CustomerName, out var pis))
                        g.JoCount = pis.Count(pi => joPis.Contains(pi));
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

            try
            {
                using var dnDb = new DeliveryNotesDbContext();
                var dnPis = new HashSet<string>(await dnDb.DeliveryNotes.AsNoTracking().Where(d => !d.IsMasterDn).Select(d => d.ProformaId).ToListAsync(ct), StringComparer.OrdinalIgnoreCase);
                foreach (var g in groups)
                    if (piByCustomer.TryGetValue(g.CustomerName, out var pis))
                        g.DnCount = pis.Count(pi => dnPis.Contains(pi));
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

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
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

            var total = groups.Count;
            var page = groups.OrderByDescending(g => g.PiValue).Skip(filter.Skip).Take(filter.Take).ToList();
            return new PagedResult<CustomerReportRow> { Items = page, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }

        public async Task<PagedResult<SalesmanReportRow>> GetSalesmanRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var piDb = new ProformaInvoicesDbContext();
            var piAll = await piDb.ProformaInvoices.AsNoTracking()
                .Select(p => new { p.ProformaId, p.Status, p.GrandTotalVatInclusive, p.CustomerName, p.ProjectName, p.FullSnapshotJson, p.CompanyName })
                .ToListAsync(ct);

            // Extract salesman per PI from snapshot
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

            // Enrich JO/DN/TI counts per salesman via PI set
            var piBySalesman = piWithSalesman.GroupBy(x => x.Salesman, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(x => x.ProformaId), StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

            try
            {
                using var joDb = new JobOrdersDbContext();
                var joPis = new HashSet<string>(await joDb.JobOrders.AsNoTracking().Select(j => j.SourceProformaId).ToListAsync(ct), StringComparer.OrdinalIgnoreCase);
                foreach (var g in groups)
                    if (piBySalesman.TryGetValue(g.SalesmanName, out var pis))
                        g.JobOrderCount = pis.Count(pi => joPis.Contains(pi));
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

            try
            {
                using var tiDb = new TaxInvoiceDbContext();
                var tiPis = new HashSet<string>(await tiDb.TaxInvoices.AsNoTracking().Select(t => t.ProformaId).ToListAsync(ct), StringComparer.OrdinalIgnoreCase);
                foreach (var g in groups)
                    if (piBySalesman.TryGetValue(g.SalesmanName, out var pis))
                        g.TaxInvoiceCount = pis.Count(pi => tiPis.Contains(pi));
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

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

            // Group by ProjectName indexed + fallback snapshot ProjectIdentifierName
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
                // Keep first non-empty contract/salesman
                if (string.IsNullOrWhiteSpace(row.ProjectContractNo) && !string.IsNullOrWhiteSpace(contractNo))
                    row.ProjectContractNo = contractNo;
                if (string.IsNullOrWhiteSpace(row.Salesman) && !string.IsNullOrWhiteSpace(salesman))
                    row.Salesman = salesman;
            }

            var list = projectRows.Values.ToList();

            if (filter.HasSearch)
                list = list.Where(r => ContainsCI(r.ProjectName, filter.SearchText) || ContainsCI(r.CustomerName, filter.SearchText)).ToList();
            if (filter.HasProject)
                list = list.Where(r => ContainsCI(r.ProjectName, filter.Project)).ToList();
            if (filter.HasCustomer)
                list = list.Where(r => ContainsCI(r.CustomerName, filter.Customer)).ToList();

            // Enrich JO/DN/TI counts
            try
            {
                using var joDb = new JobOrdersDbContext();
                var joPis = new HashSet<string>(await joDb.JobOrders.AsNoTracking().Select(j => j.SourceProformaId).ToListAsync(ct), StringComparer.OrdinalIgnoreCase);
                foreach (var r in list)
                    if (piByProject.TryGetValue(r.ProjectName, out var pis))
                        r.JoCount = pis.Count(pi => joPis.Contains(pi));
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

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
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

            var total = list.Count;
            var page = list.OrderByDescending(r => r.PiValue).Skip(filter.Skip).Take(filter.Take).ToList();
            return new PagedResult<ProjectReportRow> { Items = page, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }

        // ════════════════════════════════════════════════════════════════
        //  GLASS / SPEC REPORT — row-level
        // ════════════════════════════════════════════════════════════════

        public async Task<PagedResult<GlassSpecReportRow>> GetGlassSpecRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            var allRows = new List<GlassSpecReportRow>();

            using var piDb = new ProformaInvoicesDbContext();
            // For performance, apply indexed DB filters first
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

            var piList = await query.Select(p => new
            {
                p.ProformaId, p.CompanyName, p.CustomerName, p.ProjectName, p.Status, p.IssueDate, p.CreatedAt, p.FullSnapshotJson
            }).ToListAsync(ct);

            foreach (var pi in piList)
            {
                var snap = TryDeserializePiSnapshot(pi.FullSnapshotJson);
                if (snap == null) continue;
                var salesman = snap.Client?.SalesRepresentative ?? "";
                var rows = ExtractGlassRowsFromPiSnapshot(snap, pi.ProformaId, pi.CompanyName, pi.CustomerName, salesman, pi.IssueDate == default ? pi.CreatedAt : pi.IssueDate, pi.Status);

                // Apply row-level filters
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
                                  ContainsCI(r.CustomerName, filter.SearchText) ||
                                  ContainsCI(r.GlassType, filter.SearchText) ||
                                  ContainsCI(r.OuterColor, filter.SearchText);
                        if (!hit) continue;
                    }
                    allRows.Add(r);
                }
            }

            // Enrich with DN delivery + TI invoiced per PI+Spec matching
            // Build map: ProformaId -> (TotalOrdered from snapshot = rows sum) vs Master DN delivered
            try
            {
                using var dnDb = new DeliveryNotesDbContext();
                var masters = await dnDb.DeliveryNotes.AsNoTracking().Where(d => d.IsMasterDn)
                    .Select(d => new { d.ProformaId, d.TotalOrderedQty, d.TotalDeliveredQty, d.TotalBalanceQty })
                    .ToListAsync(ct);
                // Aggregate per PI (if multiple masters per PI, sum)
                var masterByPi = masters.GroupBy(m => m.ProformaId ?? "", StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => (Ordered: g.Sum(x => x.TotalOrderedQty), Delivered: g.Sum(x => x.TotalDeliveredQty)), StringComparer.OrdinalIgnoreCase);

                // Also need per-row delivered — approximate by distributing proportionally?
                // For now, set DeliveredQty on each row based on PI-level reconciliation:
                // We cannot know per-row delivered exactly without parsing DN items per row.
                // Instead, we attach PI-level balance and try to refine via DN items where possible.

                // Load DN items for precise per-row if small dataset
                if (allRows.Count <= 500)
                {
                    var piIds = allRows.Select(r => r.ProformaId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    var dnItems = await dnDb.DeliveryNoteItems.AsNoTracking()
                        .Where(i => i.DeliveryNote != null && piIds.Contains(i.DeliveryNote.ProformaId))
                        .Select(i => new { ProformaId = i.DeliveryNote.ProformaId, i.GlassRef, i.W1, i.H1, i.OrderedQty, i.DeliveredQty })
                        .ToListAsync(ct);
                    // Build map per PI + GlassRef + dimensions
                    var deliveredMap = new Dictionary<string, int>();
                    foreach (var it in dnItems)
                    {
                        var key = $"{it.ProformaId}|{Norm(it.GlassRef)}|{it.W1}x{it.H1}".ToLowerInvariant();
                        if (!deliveredMap.ContainsKey(key)) deliveredMap[key] = 0;
                        deliveredMap[key] += it.DeliveredQty;
                    }
                    foreach (var r in allRows)
                    {
                        var key = $"{r.ProformaId}|{Norm(r.GlassRef)}|{r.W1}x{r.H1}".ToLowerInvariant();
                        if (deliveredMap.TryGetValue(key, out var dq))
                            r.DeliveredQty = Math.Min(dq, r.Qty);
                        else if (masterByPi.TryGetValue(r.ProformaId, out var m))
                        {
                            // Fallback: if no per-row match, leave 0 delivered; status will show pending
                            r.DeliveredQty = 0;
                        }
                        r.BalanceQty = Math.Max(0, r.Qty - r.DeliveredQty);
                        r.DeliveryStatus = r.DeliveredQty == 0 ? "Pending" : r.DeliveredQty < r.Qty ? "Partial" : "Complete";
                    }
                }
                else
                {
                    foreach (var r in allRows)
                    {
                        r.DeliveredQty = 0;
                        r.BalanceQty = r.Qty;
                        r.DeliveryStatus = "Pending";
                    }
                }

                // Invoiced: from TI items per PI
                try
                {
                    using var tiDb = new TaxInvoiceDbContext();
                    if (allRows.Count <= 1000)
                    {
                        var piIds = allRows.Select(r => r.ProformaId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        var tiItems = await tiDb.TaxInvoiceItems.AsNoTracking()
                            .Where(i => piIds.Contains(i.ProformaId))
                            .Select(i => new { i.ProformaId, i.GlassRef, i.W1, i.H1, i.Qty })
                            .ToListAsync(ct);
                        var invoicedMap = new Dictionary<string, int>();
                        foreach (var it in tiItems)
                        {
                            var key = $"{it.ProformaId}|{Norm(it.GlassRef)}|{it.W1}x{it.H1}".ToLowerInvariant();
                            if (!invoicedMap.ContainsKey(key)) invoicedMap[key] = 0;
                            invoicedMap[key] += it.Qty;
                        }
                        foreach (var r in allRows)
                        {
                            var key = $"{r.ProformaId}|{Norm(r.GlassRef)}|{r.W1}x{r.H1}".ToLowerInvariant();
                            if (invoicedMap.TryGetValue(key, out var iq))
                                r.InvoicedQty = Math.Min(iq, r.Qty);
                            r.InvoiceStatus = r.InvoicedQty == 0 ? "Pending" : r.InvoicedQty < r.Qty ? "Partial" : "Complete";
                        }
                    }
                }
                catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

            // Filter by remaining quantity if status filter indicates delivery state?
            // Already handled. Apply pagination after sorting by IssueDate desc
            var sorted = allRows.OrderByDescending(r => r.IssueDate).ThenBy(r => r.ProformaId).ThenBy(r => r.GroupNumber).ThenBy(r => r.SerialNumber).ToList();
            var total = sorted.Count;
            var page = sorted.Skip(filter.Skip).Take(filter.Take).ToList();

            // Attempt JO/DN/TI link enrichment for displayed page only
            try
            {
                var piIdsPage = page.Select(r => r.ProformaId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                using var joDb = new JobOrdersDbContext();
                var joMap = (await joDb.JobOrders.AsNoTracking().Where(j => piIdsPage.Contains(j.SourceProformaId))
                    .Select(j => new { j.SourceProformaId, j.JobOrderId }).ToListAsync(ct))
                    .GroupBy(x => x.SourceProformaId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().JobOrderId, StringComparer.OrdinalIgnoreCase);
                foreach (var r in page)
                    if (joMap.TryGetValue(r.ProformaId, out var jo)) r.JobOrderId = jo;
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

            return new PagedResult<GlassSpecReportRow> { Items = page, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }

        private static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant();

        public async Task<List<SpecAggregation>> GetSpecAggregationsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            var page = await GetGlassSpecRowsAsync(new ReportingFilter
            {
                DateFrom = filter.DateFrom, DateTo = filter.DateTo,
                Company = filter.Company, Customer = filter.Customer, Salesman = filter.Salesman,
                Project = filter.Project, GlassType = filter.GlassType, Specification = filter.Specification,
                Colour = filter.Colour, Thickness = filter.Thickness, GlassRef = filter.GlassRef,
                SearchText = filter.SearchText, ProformaId = filter.ProformaId,
                Page = 1, PageSize = 100000 // get all for aggregation
            }, ct);

            var groups = page.Items.GroupBy(r => r.CanonicalKey)
                .Select(g =>
                {
                    var first = g.First();
                    return new SpecAggregation
                    {
                        CanonicalKey = g.Key,
                        SpecName = first.SpecName,
                        GlassType = first.GlassType,
                        GlassRef = first.GlassRef,
                        DimensionsKey = first.DimensionsKey,
                        TotalRows = g.Count(),
                        TotalQty = g.Sum(x => x.Qty),
                        TotalSqm = Math.Round(g.Sum(x => x.TotalSqm), 3),
                        TotalValue = Math.Round(g.Sum(x => x.Amount), 2),
                        AvgRate = g.Average(x => x.Rate),
                        DeliveredQty = g.Sum(x => x.DeliveredQty),
                        BalanceQty = g.Sum(x => x.BalanceQty),
                        DistinctCustomers = g.Select(x => x.CustomerName).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                        DistinctProjects = g.Select(x => x.ProjectName).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                        TopCustomers = g.GroupBy(x => x.CustomerName).OrderByDescending(x => x.Sum(v => v.Qty)).Take(3).Select(x => x.Key).ToList()
                    };
                })
                .OrderByDescending(g => g.TotalQty)
                .ToList();

            return groups;
        }

        public async Task<RowTrace?> GetRowTraceAsync(string rowId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rowId)) return null;
            // RowId format: PI-xxx-G1-S1 or similar. Extract ProformaId
            var parts = rowId.Split('-');
            // More robust: search for the PI by trying to find row with that RowId via glass spec query
            var all = await GetGlassSpecRowsAsync(new ReportingFilter { Page = 1, PageSize = 100000 }, ct);
            var target = all.Items.FirstOrDefault(r => Eq(r.RowId, rowId));
            if (target == null)
            {
                // Try partial match
                target = all.Items.FirstOrDefault(r => r.RowId.IndexOf(rowId, StringComparison.OrdinalIgnoreCase) >= 0);
            }
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
        //  STATEMENT REPORT
        // ════════════════════════════════════════════════════════════════

        public async Task<PagedResult<StatementReportRow>> GetStatementRowsAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            filter ??= new ReportingFilter();
            using var db = new StatementDbContext();
            var query = db.Statements.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Customer))
                query = query.Where(s => EF.Functions.Like(s.FilterCustomer, $"%{filter.Customer}%") || EF.Functions.Like(s.FilterCustomer, $"%{filter.Customer}%") || EF.Functions.Like(s.StatementNumber, $"%{filter.Customer}%"));
            // Statement uses FilterCustomer / FilterCompany columns; also check CustomerName via statement snapshot?
            if (filter.DateFrom.HasValue)
                query = query.Where(s => s.CreatedAt >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue)
                query = query.Where(s => s.CreatedAt <= filter.DateTo.Value);

            var all = await query.Select(s => new
            {
                s.Id, s.StatementNumber, s.FilterCustomer, s.FilterCompany, s.FilterProject, s.LineItems, s.TotalInvoiced, s.TotalOutstanding, s.OverdueAmount, s.CreatedAt, s.FilterJson
            }).OrderByDescending(s => s.CreatedAt).ToListAsync(ct);

            var filtered = all.Where(r =>
                (string.IsNullOrWhiteSpace(filter.SearchText) || ContainsCI(r.StatementNumber, filter.SearchText) || ContainsCI(r.FilterCustomer, filter.SearchText) || ContainsCI(r.FilterCompany, filter.SearchText)) &&
                (string.IsNullOrWhiteSpace(filter.Company) || ContainsCI(r.FilterCompany, filter.Company)) &&
                (filter.HasProject ? ContainsCI(r.FilterProject, filter.Project) : true)
            ).ToList();

            var total = filtered.Count;
            var page = filtered.Skip(filter.Skip).Take(filter.Take).ToList();
            var rows = page.Select(r => new StatementReportRow
            {
                Id = r.Id,
                StatementNumber = r.StatementNumber,
                CustomerName = r.FilterCustomer,
                CompanyName = r.FilterCompany,
                ProjectName = r.FilterProject,
                InvoiceCount = r.LineItems?.Count ?? 0,
                TotalInvoiced = r.TotalInvoiced,
                TotalOutstanding = r.TotalOutstanding,
                OverdueAmount = r.OverdueAmount,
                CreatedAt = r.CreatedAt,
                FilterJson = r.FilterJson
            }).ToList();

            return new PagedResult<StatementReportRow> { Items = rows, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }

        // ════════════════════════════════════════════════════════════════
        //  PROFITABILITY
        // ════════════════════════════════════════════════════════════════

        public async Task<ProfitabilitySummary> GetProfitabilitySummaryAsync(ReportingFilter filter, CancellationToken ct = default)
        {
            var rows = await GetProfitabilityRowsAsync(new ReportingFilter
            {
                DateFrom = filter.DateFrom, DateTo = filter.DateTo,
                Company = filter.Company, Customer = filter.Customer, Project = filter.Project,
                Salesman = filter.Salesman, Page = 1, PageSize = 10000
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
            var pis = await piDb.ProformaInvoices.AsNoTracking()
                .Select(p => new { p.ProformaId, p.CompanyName, p.CustomerName, p.ProjectName, p.GrandTotalVatInclusive, p.Status, p.IssueDate, p.FullSnapshotJson })
                .ToListAsync(ct);

            foreach (var pi in pis)
            {
                var snap = TryDeserializePiSnapshot(pi.FullSnapshotJson);
                if (snap == null) continue;
                var proj = snap.Project?.ProjectIdentifierName ?? snap.Project?.ProjectReference ?? pi.ProjectName ?? "";
                var salesman = snap.Client?.SalesRepresentative ?? "";

                if (filter.HasCompany && !ContainsCI(pi.CompanyName, filter.Company)) continue;
                if (filter.HasCustomer && !ContainsCI(pi.CustomerName, filter.Customer)) continue;
                if (filter.HasProject && !ContainsCI(proj, filter.Project)) continue;
                if (filter.HasSalesman && !ContainsCI(salesman, filter.Salesman)) continue;
                if (!string.IsNullOrWhiteSpace(filter.ProformaId) && !Eq(pi.ProformaId, filter.ProformaId!)) continue;
                if (filter.DateFrom.HasValue && pi.IssueDate.Date < filter.DateFrom.Value.Date) continue;
                if (filter.DateTo.HasValue && pi.IssueDate.Date > filter.DateTo.Value.Date) continue;
                if (filter.HasSearch && !(ContainsCI(pi.ProformaId, filter.SearchText) || ContainsCI(pi.CustomerName, filter.SearchText) || ContainsCI(proj, filter.SearchText))) continue;

                var pl = snap.ProfitLoss;
                bool hasCost = pl != null && (pl.TotalGlassCost > 0 || pl.TotalProductionCost > 0);
                decimal revenue = pl != null && pl.NetExVat > 0 ? pl.NetExVat : Math.Round(pi.GrandTotalVatInclusive / 1.05m, 2);
                decimal cost = hasCost ? pl!.TotalGlassCost + pl.TotalProductionCost : 0;
                decimal profit = hasCost ? revenue - cost : 0;
                decimal margin = revenue == 0 ? 0 : Math.Round(profit / revenue * 100, 1);

                // Also accumulate Qty/Sqm from spec groups
                int qty = 0; decimal sqm = 0;
                if (snap.SpecGroups != null)
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

            // Sort by profit descending (highest profit first), then revenue
            var sorted = rows.OrderByDescending(r => r.Profit).ThenByDescending(r => r.Revenue).ToList();
            var total = sorted.Count;
            var page = sorted.Skip(filter.Skip).Take(filter.Take).ToList();
            return new PagedResult<ProfitabilityRow> { Items = page, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
        }

        // ════════════════════════════════════════════════════════════════
        //  DRILL-DOWN HELPERS
        // ════════════════════════════════════════════════════════════════

        public async Task<List<GlassSpecReportRow>> GetPiRowsDetailAsync(string proformaId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(proformaId)) return new List<GlassSpecReportRow>();
            var res = await GetGlassSpecRowsAsync(new ReportingFilter { ProformaId = proformaId.Trim(), Page = 1, PageSize = 10000 }, ct);
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
                JobOrderQty = ordered, // JO qty == PI qty at conversion
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

            // PI
            var piRows = await GetPiRowsAsync(new ReportingFilter { ProformaId = pi, PageSize = 5 }, ct);
            dict["Proforma"] = piRows.Items.FirstOrDefault() ?? new PiReportRow { ProformaId = pi };

            // JO
            var joRows = await GetJoRowsAsync(new ReportingFilter { ProformaId = pi, PageSize = 10 }, ct);
            dict["JobOrders"] = joRows.Items;
            dict["HasJobOrder"] = joRows.Items.Count > 0;

            // DN
            var dnRows = await GetDeliveryRowsAsync(new ReportingFilter { ProformaId = pi, PageSize = 20 }, ct);
            dict["DeliveryNotes"] = dnRows.Items;

            // TI
            var tiRows = await GetTaxInvoiceRowsAsync(new ReportingFilter { ProformaId = pi, PageSize = 20 }, ct);
            dict["TaxInvoices"] = tiRows.Items;

            // Glass rows
            var glassRows = await GetPiRowsDetailAsync(pi, ct);
            dict["GlassRows"] = glassRows;
            dict["TotalQty"] = glassRows.Sum(r => r.Qty);
            dict["TotalSqm"] = Math.Round(glassRows.Sum(r => r.TotalSqm), 3);

            // Reconciliation
            dict["Reconciliation"] = await GetQuantityReconciliationAsync(pi, ct);

            // Profitability for this PI
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
                if (list.Count > 0) return list.Where(s => !string.IsNullOrWhiteSpace(s)).OrderBy(s => s).ToList()!;
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            try
            {
                using var db = new ProformaInvoicesDbContext();
                var pis = await db.ProformaInvoices.AsNoTracking().Select(p => p.FullSnapshotJson).ToListAsync(ct);
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var json in pis)
                {
                    var snap = TryDeserializePiSnapshot(json);
                    var sm = snap?.Client?.SalesRepresentative?.Trim();
                    if (!string.IsNullOrWhiteSpace(sm)) set.Add(sm!);
                }
                return set.OrderBy(s => s).ToList();
            }
            catch { return new List<string>(); }
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
