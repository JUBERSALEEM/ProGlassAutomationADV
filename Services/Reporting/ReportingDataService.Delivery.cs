// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/ReportingDataService.Delivery.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.2 — ReportingDataService partial split. This part owns the Delivery
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

            summary.TotalRows = await query.CountAsync(ct).ConfigureAwait(false);
            if (summary.TotalRows == 0) return summary;

            // Individual DB-side aggregates (no row materialization).
            summary.TotalOrderedQty = await query.SumAsync(x => x.TotalQty, ct).ConfigureAwait(false);
            summary.TotalDeliveredQty = await query.SumAsync(x => x.DeliveredQty, ct).ConfigureAwait(false);
            summary.TotalRemainingQty = Math.Max(0, summary.TotalOrderedQty - summary.TotalDeliveredQty);
            summary.NotDelivered = await query.CountAsync(x => x.DeliveredQty == 0, ct).ConfigureAwait(false);
            summary.FullyDelivered = await query.CountAsync(x => x.DeliveredQty > 0 && x.DeliveredQty >= x.TotalQty, ct).ConfigureAwait(false);
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

            var totalCount = await query.CountAsync(ct).ConfigureAwait(false);
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
                .ToListAsync(ct).ConfigureAwait(false);

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
                DeliveryStatusBrushKey = RowStatusBrushKey(x.DeliveredQty, x.TotalQty),
                OrderedAt = x.OrderedAt
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
                .ToListAsync(ct).ConfigureAwait(false);
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

            /// <summary>
            /// Earliest date this row was raised, used for delivery AGEING.
            /// Taken from the MASTER DN (the JO-side catalogue row), which
            /// is when the work was ordered — not from a real DN, which is
            /// when part of it shipped. Ageing must measure how long the
            /// customer has been waiting, so the order date is correct and
            /// the dispatch date would understate every late row.
            /// </summary>
            public DateTime OrderedAt { get; set; }
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
                    DeliveredQty = g.Sum(i => i.DeliveryNote!.IsMasterDn ? 0 : i.DeliveredQty),

                    // Min() = the first time this row appeared anywhere,
                    // i.e. when it was ordered. Translatable to SQL MIN.
                    OrderedAt = g.Min(i => i.DeliveryNote!.CreatedAt)
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
                    .ToListAsync(ct).ConfigureAwait(false);

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
                    .ToListAsync(ct).ConfigureAwait(false);

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
                    .ToListAsync(ct).ConfigureAwait(false);

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
    }
}
