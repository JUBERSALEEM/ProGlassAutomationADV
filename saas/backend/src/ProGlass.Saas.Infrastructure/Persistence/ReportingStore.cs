using Microsoft.EntityFrameworkCore;
using ProGlass.Saas.Application.Reporting;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Infrastructure.Persistence;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 20 storage — the WPF ReportingDataService partials (Pi, Jo,
//  Delivery, TaxInvoice, Profitability, Executive) re-expressed over the
//  normalized SaaS schema:
//   • denormalized WPF name columns → joins to the master-data tables
//     (same semantics: contains for company/customer, exact for project);
//   • WPF snapshot-JSON reads (qty/sqm, P&L, salesman) → the normalized
//     spec_group_items / pnl KPI columns / first-class FKs;
//   • WPF status strings mapped to the SaaS vocabularies (documented in
//     the mapping doc §1.9).
//  The filter semantics (Live = !IsVoid, inclusive dates, phantom-row
//  exclusion, master-DN delivery credit) are the WPF's, verbatim.
// ═══════════════════════════════════════════════════════════════════════

public sealed class ReportingStore(SaasDbContextFactory factory) : IReportingStore
{
    // ── PI ────────────────────────────────────────────────────────────

    /// <summary>
    /// The WPF ApplyPiFilters port. <paramref name="projectContains"/>
    /// mirrors the two WPF call sites: the PI summary filters project by
    /// EXACT match, the profitability report by contains.
    /// </summary>
    private IQueryable<ProformaInvoice> FilteredProformas(
        SaasDbContext db, ReportingFilter filter, bool projectExact)
    {
        var query = db.ProformaInvoices.AsNoTracking().AsQueryable(); // Live: IsVoid excluded below

        if (filter.HasCompany)
            query = query.Where(p => db.Companies.Any(c => c.Id == p.CompanyId
                && c.Name.Contains(filter.Company!)));
        if (filter.HasCustomer)
            query = query.Where(p => db.Clients.Any(c => c.Id == p.ClientId
                && c.CustomerName.Contains(filter.Customer!)));
        if (filter.HasProject)
        {
            query = projectExact
                ? query.Where(p => db.Projects.Any(pr => pr.Id == p.ProjectId
                    && pr.IdentifierName == filter.Project))
                : query.Where(p => db.Projects.Any(pr => pr.Id == p.ProjectId
                    && pr.IdentifierName.Contains(filter.Project!)));
        }
        if (!string.IsNullOrWhiteSpace(filter.ProformaNo))
            query = query.Where(p => p.ProformaNo == filter.ProformaNo);
        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(p => p.Status == filter.Status);
        if (filter.DateFrom.HasValue)
        {
            var from = filter.DateFrom.Value;
            var fromUtc = from.ToDateTime(TimeOnly.MinValue);
            query = query.Where(p => p.IssueDate >= from
                                     || (p.IssueDate == null && p.CreatedAt >= fromUtc));
        }
        if (filter.DateTo.HasValue)
        {
            var to = filter.DateTo.Value;
            var toUtc = to.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(p => p.IssueDate <= to
                                     || (p.IssueDate == null && p.CreatedAt <= toUtc));
        }
        if (filter.HasSearch)
        {
            var term = filter.SearchText!.Trim();
            query = query.Where(p =>
                p.ProformaNo.Contains(term)
                || db.Clients.Any(c => c.Id == p.ClientId && c.CustomerName.Contains(term))
                || db.Companies.Any(c => c.Id == p.CompanyId && c.Name.Contains(term))
                || db.Projects.Any(pr => pr.Id == p.ProjectId && pr.IdentifierName.Contains(term)));
        }

        return query.Where(p => !p.IsVoid);
    }

    public async Task<PiSummaryDto> GetPiSummaryAsync(
        Guid tenantId, ReportingFilter filter, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var query = FilteredProformas(db, filter, projectExact: true);

        var summary = new PiSummaryDto
        {
            TotalPi = await query.CountAsync(ct).ConfigureAwait(false)
        };
        if (summary.TotalPi == 0) return summary;

        summary.TotalValue = await query.SumAsync(p => p.GrandTotalVatInclusive, ct)
            .ConfigureAwait(false);

        // WPF status buckets: Draft → Draft; Released/Sent → Released;
        // Approved/Confirmed/Issued → Confirmed; Rejected/Cancelled/Void →
        // Rejected. SaaS vocabulary: status Sent → Released, status
        // Approved or lifecycle Confirmed → Confirmed (documented).
        var statusGroups = await query
            .GroupBy(p => new { p.Status, p.LifecycleState })
            .Select(g => new { g.Key.Status, g.Key.LifecycleState, Count = g.Count() })
            .ToListAsync(ct).ConfigureAwait(false);

        foreach (var g in statusGroups)
        {
            if (g.Status == "Draft") summary.Draft += g.Count;
            else if (g.Status == "Sent") summary.Released += g.Count;
            else if (g.Status == "Approved" || g.LifecycleState == "Confirmed")
                summary.Confirmed += g.Count;
            else summary.Rejected += g.Count;
        }
        summary.Pending = summary.Draft + summary.Released;

        var piIds = await query.Select(p => p.Id).ToListAsync(ct).ConfigureAwait(false);
        summary.Converted = await db.JobOrders.AsNoTracking()
            .Where(j => !j.IsVoid && piIds.Contains(j.ProformaId))
            .Select(j => j.ProformaId)
            .Distinct()
            .CountAsync(ct).ConfigureAwait(false);
        summary.NotConverted = Math.Max(0, summary.TotalPi - summary.Converted);

        // Quantity/area KPI card — the WPF runs a bounded snapshot scan
        // (newest 1000); the SaaS reads the normalized spec items for the
        // same bounded set.
        var newest = await query.OrderByDescending(p => p.CreatedAt)
            .Take(1000).Select(p => p.Id).ToListAsync(ct).ConfigureAwait(false);
        var groupIds = await db.SpecGroups.AsNoTracking()
            .Where(g => newest.Contains(g.ProformaId)).Select(g => g.Id)
            .ToListAsync(ct).ConfigureAwait(false);
        var agg = await db.SpecGroupItems.AsNoTracking()
            .Where(i => groupIds.Contains(i.SpecGroupId))
            .GroupBy(_ => 1)
            .Select(g => new { Qty = g.Sum(x => x.Qty), Sqm = g.Sum(x => x.TotalSqm) })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (agg != null)
        {
            summary.TotalQty = agg.Qty;
            summary.TotalSqm = Math.Round(agg.Sqm, 3);
        }

        return summary;
    }

    public async Task<List<Guid>> FilteredProformaIdsAsync(
        Guid tenantId, ReportingFilter filter, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await FilteredProformas(db, filter, projectExact: true)
            .Select(p => p.Id).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<PagedResult<ProfitabilityPiRow>> GetProfitabilityRowsAsync(
        Guid tenantId, ReportingFilter filter, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var query = FilteredProformas(db, filter, projectExact: false);

        var totalCount = await query.CountAsync(ct).ConfigureAwait(false);
        var page = new PagedResult<ProfitabilityPiRow>
        {
            TotalCount = totalCount, Page = filter.Page, PageSize = filter.PageSize
        };
        if (totalCount == 0) return page;

        // Sort DB-side (before paging) — the WPF's mapped sort keys.
        bool desc = filter.SortDescending;
        IOrderedQueryable<ProformaInvoice> ordered = filter.SortField switch
        {
            "Amount" => desc ? query.OrderByDescending(p => p.GrandTotalVatInclusive)
                             : query.OrderBy(p => p.GrandTotalVatInclusive),
            "Customer" => desc ? query.OrderByDescending(p => db.Clients
                    .Where(c => c.Id == p.ClientId).Select(c => c.CustomerName).FirstOrDefault())
                               : query.OrderBy(p => db.Clients
                    .Where(c => c.Id == p.ClientId).Select(c => c.CustomerName).FirstOrDefault()),
            "Company" => desc ? query.OrderByDescending(p => db.Companies
                    .Where(c => c.Id == p.CompanyId).Select(c => c.Name).FirstOrDefault())
                              : query.OrderBy(p => db.Companies
                    .Where(c => c.Id == p.CompanyId).Select(c => c.Name).FirstOrDefault()),
            "Project" => desc ? query.OrderByDescending(p => db.Projects
                    .Where(pr => pr.Id == p.ProjectId).Select(pr => pr.IdentifierName).FirstOrDefault())
                              : query.OrderBy(p => db.Projects
                    .Where(pr => pr.Id == p.ProjectId).Select(pr => pr.IdentifierName).FirstOrDefault()),
            "Status" => desc ? query.OrderByDescending(p => p.Status) : query.OrderBy(p => p.Status),
            "Date" => desc ? query.OrderByDescending(p => p.IssueDate) : query.OrderBy(p => p.IssueDate),
            _ => desc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
        };

        var pis = await ordered
            .Skip(filter.Skip).Take(filter.Take)
            .Select(p => new
            {
                p.Id, p.ProformaNo, p.CompanyId, p.ClientId, p.ProjectId,
                p.GrandTotalVatInclusive, p.Status, p.IssueDate, p.CreatedAt,
                p.PnlCalculatedAt, p.PnlNetExVat, p.PnlTotalGlassCost,
                p.PnlTotalProductionCost, p.PnlProfit, p.PnlWastagePercent,
                p.PnlCostAtSellingPrice
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var piIds = pis.Select(p => p.Id).ToList();
        var companyNames = await db.Companies.AsNoTracking()
            .Where(c => pis.Select(p => p.CompanyId).Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct).ConfigureAwait(false);
        var clientNames = await db.Clients.AsNoTracking()
            .Where(c => pis.Select(p => p.ClientId).Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.CustomerName, ct).ConfigureAwait(false);
        var projectNames = await db.Projects.AsNoTracking()
            .Where(pr => pis.Select(p => p.ProjectId).Contains(pr.Id))
            .ToDictionaryAsync(pr => pr.Id, pr => pr.IdentifierName, ct).ConfigureAwait(false);
        var hasPnlRows = (await db.PnlRows.AsNoTracking()
                .Where(r => piIds.Contains(r.ProformaId))
                .Select(r => r.ProformaId).Distinct().ToListAsync(ct).ConfigureAwait(false))
            .ToHashSet();
        var groupIds = await db.SpecGroups.AsNoTracking()
            .Where(g => piIds.Contains(g.ProformaId))
            .Select(g => new { g.Id, g.ProformaId }).ToListAsync(ct).ConfigureAwait(false);
        var groupToPi = groupIds.ToDictionary(g => g.Id, g => g.ProformaId);
        var gidList = groupIds.Select(g => g.Id).ToList();
        var itemAgg = await db.SpecGroupItems.AsNoTracking()
            .Where(i => gidList.Contains(i.SpecGroupId))
            .GroupBy(i => i.SpecGroupId)
            .Select(g => new { SpecGroupId = g.Key, Qty = g.Sum(x => x.Qty), Sqm = g.Sum(x => x.TotalSqm) })
            .ToListAsync(ct).ConfigureAwait(false);
        var qtyByPi = new Dictionary<Guid, int>();
        var sqmByPi = new Dictionary<Guid, decimal>();
        foreach (var a in itemAgg)
        {
            var piId = groupToPi[a.SpecGroupId];
            qtyByPi[piId] = qtyByPi.TryGetValue(piId, out var q) ? q + a.Qty : a.Qty;
            sqmByPi[piId] = sqmByPi.TryGetValue(piId, out var s) ? s + a.Sqm : a.Sqm;
        }

        page.Items = pis.Select(p => new ProfitabilityPiRow(
            p.Id, p.ProformaNo,
            p.CompanyId is { } cid && companyNames.TryGetValue(cid, out var cn) ? cn : "",
            p.ClientId is { } lid && clientNames.TryGetValue(lid, out var ln) ? ln : "",
            p.ProjectId is { } pid2 && projectNames.TryGetValue(pid2, out var pn) ? pn : "",
            p.Status, p.IssueDate, p.CreatedAt, p.GrandTotalVatInclusive,
            p.PnlCalculatedAt != null, hasPnlRows.Contains(p.Id),
            p.PnlNetExVat, p.PnlTotalGlassCost, p.PnlTotalProductionCost,
            p.PnlProfit, p.PnlWastagePercent, p.PnlCostAtSellingPrice,
            qtyByPi.TryGetValue(p.Id, out var qty) ? qty : 0,
            sqmByPi.TryGetValue(p.Id, out var sqm) ? sqm : 0m)).ToList();
        return page;
    }

    // ── Tax invoices ──────────────────────────────────────────────────

    /// <summary>The WPF ApplyTiFilters port.</summary>
    private IQueryable<TaxInvoice> FilteredInvoices(
        SaasDbContext db, ReportingFilter filter, Guid? proformaId)
    {
        var query = db.TaxInvoices.AsNoTracking();

        if (proformaId.HasValue)
            query = query.Where(t => t.ProformaId == proformaId.Value);
        if (!string.IsNullOrWhiteSpace(filter.TaxInvoiceNo))
            query = query.Where(t => t.InvoiceNo == filter.TaxInvoiceNo);
        if (filter.HasCustomer)
            query = query.Where(t => t.CustomerName.Contains(filter.Customer!));
        // WPF filters TI.CompanyName; the SaaS TI carries customer/project
        // and reaches the company through the linked PI (documented).
        if (filter.HasCompany)
            query = query.Where(t => db.ProformaInvoices.Any(p => p.Id == t.ProformaId
                && db.Companies.Any(c => c.Id == p.CompanyId && c.Name.Contains(filter.Company!))));
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var st = filter.Status.Trim();
            if (st.Equals("Partially Paid", StringComparison.OrdinalIgnoreCase)
                || st.Equals("PartiallyPaid", StringComparison.OrdinalIgnoreCase))
                query = query.Where(t => t.Status == TaxInvoiceStatuses.PartiallyPaid);
            else
                query = query.Where(t => t.Status == st);
        }
        if (filter.DateFrom.HasValue)
            query = query.Where(t => t.IssueDate >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue)
            query = query.Where(t => t.IssueDate <= filter.DateTo.Value);
        if (filter.HasSearch)
        {
            var term = filter.SearchText!.Trim();
            query = query.Where(t =>
                t.InvoiceNo.Contains(term)
                || t.CustomerName.Contains(term)
                || db.ProformaInvoices.Any(p => p.Id == t.ProformaId && p.ProformaNo.Contains(term)));
        }

        return query.Where(t => !t.IsVoid);
    }

    private async Task<Guid?> ResolveProformaIdAsync(
        SaasDbContext db, string? proformaNo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(proformaNo)) return null;
        return await db.ProformaInvoices.AsNoTracking()
            .Where(p => p.ProformaNo == proformaNo)
            .Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task<TaxInvoiceSummaryDto> GetTaxInvoiceSummaryAsync(
        Guid tenantId, ReportingFilter filter, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var proformaId = await ResolveProformaIdAsync(db, filter.ProformaNo, ct).ConfigureAwait(false);
        var query = FilteredInvoices(db, filter, proformaId);

        var summary = new TaxInvoiceSummaryDto
        {
            TotalInvoices = await query.CountAsync(ct).ConfigureAwait(false)
        };

        // Confirmed-PI pipeline (computed under the same PI filters).
        summary.PendingInvoice = await CountConfirmedWithoutInvoiceAsync(tenantId, filter, ct)
            .ConfigureAwait(false);
        summary.InvoicableAmount = await SumConfirmedPiValueAsync(tenantId, filter, ct)
            .ConfigureAwait(false);

        if (summary.TotalInvoices == 0)
        {
            summary.BalanceInvoicableAmount = Math.Max(0m, summary.InvoicableAmount - summary.TotalAmount);
            return summary;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tiAmounts = await query
            .Select(t => new { t.GrandTotal, t.VatAmount, t.Status, t.DueDate, t.PaidAmount, t.BalanceAmount })
            .ToListAsync(ct).ConfigureAwait(false);
        summary.TotalAmount = tiAmounts.Sum(x => x.GrandTotal);
        summary.TotalVat = tiAmounts.Sum(x => x.VatAmount);

        summary.Issued = tiAmounts.Count(t => t.Status == TaxInvoiceStatuses.Issued);
        summary.Paid = tiAmounts.Count(t => t.Status == TaxInvoiceStatuses.Paid);
        summary.PartiallyPaid = tiAmounts.Count(
            t => t.Status == TaxInvoiceStatuses.PartiallyPaid);
        summary.Overdue = tiAmounts.Count(t =>
            t.Status == TaxInvoiceStatuses.Overdue
            || (t.Status == TaxInvoiceStatuses.Issued && t.DueDate < today));
        summary.Draft = tiAmounts.Count(t => t.Status == TaxInvoiceStatuses.Draft);
        // WPF verbatim: Σ grand total of Issued + Overdue + PartiallyPaid
        // — the full grand total of unpaid invoices.
        summary.Outstanding = tiAmounts
            .Where(t => t.Status == TaxInvoiceStatuses.Issued
                        || t.Status == TaxInvoiceStatuses.Overdue
                        || t.Status == TaxInvoiceStatuses.PartiallyPaid)
            .Sum(x => x.GrandTotal);
        summary.BalanceInvoicableAmount =
            Math.Max(0m, summary.InvoicableAmount - summary.TotalAmount);

        return summary;
    }

    public async Task<List<InvoicePair>> GetFilteredInvoicePairsAsync(
        Guid tenantId, ReportingFilter filter, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var proformaId = await ResolveProformaIdAsync(db, filter.ProformaNo, ct).ConfigureAwait(false);
        return await FilteredInvoices(db, filter, proformaId)
            .Where(t => t.ProformaId != null)
            .Select(t => new InvoicePair(t.ProformaId!.Value, t.Status, t.GrandTotal))
            .ToListAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Confirmed quotes with no tax invoice raised yet (the WPF
    /// CountPendingInvoicesAsync). WPF "confirmed" = PI status
    /// Approved/Confirmed/Issued; SaaS = status Approved or lifecycle
    /// Confirmed (documented mapping).
    /// </summary>
    public async Task<int> CountConfirmedWithoutInvoiceAsync(
        Guid tenantId, ReportingFilter filter, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var confirmed = FilteredProformas(db, filter, projectExact: true)
            .Where(p => p.Status == "Approved" || p.LifecycleState == "Confirmed")
            .Select(p => p.Id);
        var count = await confirmed.CountAsync(ct).ConfigureAwait(false);
        if (count == 0) return 0;

        var invoiced = await db.TaxInvoices.AsNoTracking()
            .Where(t => !t.IsVoid && confirmed.Contains(t.ProformaId!.Value))
            .Select(t => t.ProformaId!.Value)
            .Distinct()
            .CountAsync(ct).ConfigureAwait(false);
        return Math.Max(0, count - invoiced);
    }

    public async Task<decimal> SumConfirmedPiValueAsync(
        Guid tenantId, ReportingFilter filter, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var confirmed = FilteredProformas(db, filter, projectExact: true)
            .Where(p => p.Status == "Approved" || p.LifecycleState == "Confirmed");
        return await confirmed.SumAsync(p => p.GrandTotalVatInclusive, ct).ConfigureAwait(false);
    }

    // ── Delivery ──────────────────────────────────────────────────────

    /// <summary>
    /// The WPF BuildDeliveryRowQuery port: one aggregated row per
    /// (job order, spec group, serial), delivery credit only on real DNs
    /// (master DNs contribute 0), phantom rows excluded, non-void parents.
    /// </summary>
    private IQueryable<DeliveryRowProjection> DeliveryRowQuery(
        SaasDbContext db, ReportingFilter filter, Guid? proformaId, Guid? jobOrderId)
    {
        var rows = from i in db.DeliveryNoteItems.AsNoTracking()
                   join d in db.DeliveryNotes.AsNoTracking() on i.DeliveryNoteId equals d.Id
                   join j in db.JobOrders.AsNoTracking() on i.JobOrderId equals j.Id
                   from sg in db.SpecGroups.AsNoTracking()
                       .Where(s => s.ProformaId == j.ProformaId && s.GroupNumber == i.SpecGroupNumber)
                       .DefaultIfEmpty()
                   where !d.IsVoid
                         // Phantom rows: blank glass ref + zero dims, or
                         // zero quantity, must never appear.
                         && !(i.GlassRef == "" && i.WidthMm == 0 && i.HeightMm == 0)
                         && (i.OrderedQty > 0 || i.DeliveredQty > 0)
                   select new { i, d, j, sg };

        // A date range describes DELIVERIES in the period — only real DNs
        // participate (undelivered rows are excluded once a range is set).
        if (filter.DateFrom.HasValue)
            rows = rows.Where(x => !x.d.IsMasterDn && x.d.DeliveryDate >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue)
            rows = rows.Where(x => !x.d.IsMasterDn && x.d.DeliveryDate <= filter.DateTo.Value);

        if (proformaId.HasValue)
            rows = rows.Where(x => x.j.ProformaId == proformaId.Value);
        if (jobOrderId.HasValue)
            rows = rows.Where(x => x.i.JobOrderId == jobOrderId.Value);
        if (!string.IsNullOrWhiteSpace(filter.DeliveryNoteNo))
            rows = rows.Where(x => x.d.DeliveryNo == filter.DeliveryNoteNo);
        if (filter.HasCustomer)
            rows = rows.Where(x => x.d.CustomerName.Contains(filter.Customer!));
        // Exact-match filters — the dropdowns provide the exact stored
        // value, so equality is correct and never merges across rows.
        if (filter.HasProject)
            rows = rows.Where(x => x.d.ProjectName == filter.Project);
        if (!string.IsNullOrWhiteSpace(filter.GlassRef))
            rows = rows.Where(x => x.i.GlassRef == filter.GlassRef);
        if (!string.IsNullOrWhiteSpace(filter.Specification))
            rows = rows.Where(x => x.i.SpecGroupName == filter.Specification);
        // Salesman: the WPF resolves it to job-order ids from the JO
        // snapshot; the SaaS JO carries a first-class salesman FK.
        if (filter.HasSalesman)
            rows = rows.Where(x => db.Salesmen.Any(s =>
                s.Id == x.j.SalesmanId && s.Name.Contains(filter.Salesman!)));
        // Glass type: the WPF resolves "JobOrderId|SpecGroupNumber" keys
        // from the JO snapshot; the SaaS joins the normalized spec group.
        if (!string.IsNullOrWhiteSpace(filter.GlassType))
            rows = rows.Where(x => x.sg != null && x.sg.GlassType == filter.GlassType);

        if (filter.HasSearch)
        {
            var term = filter.SearchText!.Trim();
            rows = rows.Where(x =>
                x.i.JobOrderId != null && db.JobOrders.Any(j2 => j2.Id == x.i.JobOrderId
                    && db.ProformaInvoices.Any(p => p.Id == j2.ProformaId && p.ProformaNo.Contains(term)))
                || x.i.JobOrderId != null && db.JobOrders.Any(j2 => j2.Id == x.i.JobOrderId && j2.JobNo.Contains(term))
                || x.d.DeliveryNo.Contains(term)
                || x.d.CustomerName.Contains(term)
                || x.i.GlassRef.Contains(term)
                || x.i.SpecGroupName.Contains(term));
        }

        return rows
            .GroupBy(x => new { x.i.JobOrderId, x.i.SpecGroupNumber, x.i.SerialNumber })
            .Select(g => new DeliveryRowProjection
            {
                JobOrderId = g.Key.JobOrderId,
                ProformaId = g.Max(x => x.j.ProformaId),
                SpecGroupNumber = g.Key.SpecGroupNumber,
                SerialNumber = g.Key.SerialNumber,
                Specification = g.Max(x => x.i.SpecGroupName),
                CustomerName = g.Max(x => x.d.CustomerName),
                ProjectName = g.Max(x => x.d.ProjectName),
                GlassRef = g.Max(x => x.i.GlassRef),
                OuterColour = g.Max(x => x.sg != null ? x.sg.OuterGlassColor : ""),
                GlassType = g.Max(x => x.sg != null ? x.sg.GlassType : ""),
                W1 = g.Max(x => x.i.WidthMm),
                H1 = g.Max(x => x.i.HeightMm),
                W2 = g.Max(x => x.i.W2Mm),
                H2 = g.Max(x => x.i.H2Mm),
                TotalQty = g.Max(x => x.i.OrderedQty),
                // Master DNs carry no delivery credit.
                DeliveredQty = g.Sum(x => x.d.IsMasterDn ? 0 : x.i.DeliveredQty),
                // Min() = the first time this row appeared anywhere.
                OrderedAt = g.Min(x => x.d.CreatedAt)
            });
    }

    private sealed class DeliveryRowProjection
    {
        public Guid? JobOrderId { get; set; }
        public Guid ProformaId { get; set; }
        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }
        public string Specification { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string GlassRef { get; set; } = "";
        public string OuterColour { get; set; } = "";
        public string GlassType { get; set; } = "";
        public int W1 { get; set; }
        public int H1 { get; set; }
        public int W2 { get; set; }
        public int H2 { get; set; }
        public int TotalQty { get; set; }
        public int DeliveredQty { get; set; }
        public DateTime OrderedAt { get; set; }
    }

    private static DeliveryReportRow Map(DeliveryRowProjection p) => new(
        p.JobOrderId, p.ProformaId, p.SpecGroupNumber, p.SerialNumber,
        p.Specification, p.CustomerName, p.ProjectName, p.GlassRef,
        p.OuterColour, p.GlassType,
        p.W1, p.H1, p.W2, p.H2, p.TotalQty, p.DeliveredQty, p.OrderedAt);

    public async Task<DeliverySummaryDto> GetDeliverySummaryAsync(
        Guid tenantId, ReportingFilter filter, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var proformaId = await ResolveProformaIdAsync(db, filter.ProformaNo, ct).ConfigureAwait(false);
        var jobOrderId = await ResolveJobOrderIdAsync(db, filter.JobOrderNo, ct).ConfigureAwait(false);
        var query = DeliveryRowQuery(db, filter, proformaId, jobOrderId);

        // Status filter must narrow the SUMMARY too — the same bucket
        // predicates as the rows page (the WPF's three view options).
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var st = filter.Status.Trim();
            if (st.Equals("Not delivered", StringComparison.OrdinalIgnoreCase))
                query = ApplyNotDelivered(query);
            else if (st.Equals("Fully delivered", StringComparison.OrdinalIgnoreCase))
                query = ApplyFullyDelivered(query);
            else if (st.Equals("Partially delivered", StringComparison.OrdinalIgnoreCase))
                query = ApplyPartiallyDelivered(query);
        }

        var summary = new DeliverySummaryDto
        {
            TotalRows = await query.CountAsync(ct).ConfigureAwait(false)
        };
        if (summary.TotalRows == 0) return summary;

        summary.TotalOrderedQty = await query.SumAsync(x => x.TotalQty, ct).ConfigureAwait(false);
        summary.TotalDeliveredQty = await query.SumAsync(x => x.DeliveredQty, ct).ConfigureAwait(false);
        summary.TotalRemainingQty = Math.Max(0, summary.TotalOrderedQty - summary.TotalDeliveredQty);
        summary.NotDelivered = await query.CountAsync(x => x.DeliveredQty == 0, ct).ConfigureAwait(false);
        summary.FullyDelivered = await query.CountAsync(
            x => x.DeliveredQty > 0 && x.DeliveredQty >= x.TotalQty, ct).ConfigureAwait(false);
        summary.Partial = Math.Max(0, summary.TotalRows - summary.NotDelivered - summary.FullyDelivered);

        return summary;
    }

    private static IQueryable<DeliveryRowProjection> ApplyNotDelivered(
        IQueryable<DeliveryRowProjection> q) => q.Where(x => x.DeliveredQty <= 0);

    private static IQueryable<DeliveryRowProjection> ApplyFullyDelivered(
        IQueryable<DeliveryRowProjection> q) =>
        q.Where(x => x.DeliveredQty > 0 && x.DeliveredQty >= x.TotalQty);

    private static IQueryable<DeliveryRowProjection> ApplyPartiallyDelivered(
        IQueryable<DeliveryRowProjection> q) =>
        q.Where(x => x.DeliveredQty > 0 && x.DeliveredQty < x.TotalQty);

    public async Task<List<DeliveryReportRow>> GetDeliveryRowsAsync(
        Guid tenantId, ReportingFilter filter, int maxRows, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var proformaId = await ResolveProformaIdAsync(db, filter.ProformaNo, ct).ConfigureAwait(false);
        var jobOrderId = await ResolveJobOrderIdAsync(db, filter.JobOrderNo, ct).ConfigureAwait(false);
        var query = DeliveryRowQuery(db, filter, proformaId, jobOrderId);

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var st = filter.Status.Trim();
            if (st.Equals("Not delivered", StringComparison.OrdinalIgnoreCase))
                query = ApplyNotDelivered(query);
            else if (st.Equals("Fully delivered", StringComparison.OrdinalIgnoreCase))
                query = ApplyFullyDelivered(query);
            else if (st.Equals("Partially delivered", StringComparison.OrdinalIgnoreCase))
                query = ApplyPartiallyDelivered(query);
        }

        var page = await query
            .Skip((Math.Max(1, filter.Page) - 1) * Math.Max(1, filter.PageSize))
            .Take(Math.Max(1, filter.PageSize))
            .ToListAsync(ct).ConfigureAwait(false);
        return page.Select(Map).ToList();
    }

    /// <summary>
    /// Funnel stage 3 — the cohort's delivery rows (ever-happened: dates
    /// ignored, no DN-status/date filters; only the PI-membership matters).
    /// </summary>
    public async Task<List<DeliveryReportRow>> GetCohortDeliveryRowsAsync(
        Guid tenantId, IReadOnlyList<Guid> piIds, ReportingFilter filter, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var rows = from i in db.DeliveryNoteItems.AsNoTracking()
                   join d in db.DeliveryNotes.AsNoTracking() on i.DeliveryNoteId equals d.Id
                   join j in db.JobOrders.AsNoTracking() on i.JobOrderId equals j.Id
                   where !d.IsVoid
                         && i.JobOrderId != null
                         && piIds.Contains(j.ProformaId)
                         && !(i.GlassRef == "" && i.WidthMm == 0 && i.HeightMm == 0)
                         && (i.OrderedQty > 0 || i.DeliveredQty > 0)
                   group new { i, d, j } by new { i.JobOrderId, i.SpecGroupNumber, i.SerialNumber }
                   into g
                   select new DeliveryRowProjection
                   {
                       JobOrderId = g.Key.JobOrderId,
                       ProformaId = g.Max(x => x.j.ProformaId),
                       SpecGroupNumber = g.Key.SpecGroupNumber,
                       SerialNumber = g.Key.SerialNumber,
                       TotalQty = g.Max(x => x.i.OrderedQty),
                       DeliveredQty = g.Sum(x => x.d.IsMasterDn ? 0 : x.i.DeliveredQty),
                       OrderedAt = g.Min(x => x.d.CreatedAt)
                   };

        var page = await rows.ToListAsync(ct).ConfigureAwait(false);
        return page.Select(Map).ToList();
    }

    // ── Job orders ────────────────────────────────────────────────────

    private async Task<Guid?> ResolveJobOrderIdAsync(
        SaasDbContext db, string? jobOrderNo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(jobOrderNo)) return null;
        return await db.JobOrders.AsNoTracking()
            .Where(j => j.JobNo == jobOrderNo)
            .Select(j => (Guid?)j.Id).FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    /// <summary>The WPF ApplyJoFilters port (Live = !IsVoid).</summary>
    private IQueryable<JobOrder> FilteredJobOrders(
        SaasDbContext db, ReportingFilter filter, Guid? proformaId, Guid? jobOrderId)
    {
        var query = db.JobOrders.AsNoTracking().Where(j => !j.IsVoid);

        if (proformaId.HasValue)
            query = query.Where(j => j.ProformaId == proformaId.Value);
        if (jobOrderId.HasValue)
            query = query.Where(j => j.Id == jobOrderId.Value);
        if (filter.HasCustomer)
            query = query.Where(j => j.CustomerName.Contains(filter.Customer!));
        if (filter.HasProject)
            query = query.Where(j => j.ProjectName == filter.Project);
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var st = filter.Status.Trim();
            if (st.Equals("In Production", StringComparison.OrdinalIgnoreCase)
                || st.Equals("InProduction", StringComparison.OrdinalIgnoreCase))
            {
                // Single canonical spelling in the SaaS schema (the WPF's
                // two-spelling equivalence collapses to one — documented).
                var inProd = JobOrderStatuses.InProduction;
                query = query.Where(j => j.Status == inProd);
            }
            else
            {
                query = query.Where(j => j.Status == st);
            }
        }
        if (filter.HasSalesman)
            query = query.Where(j => db.Salesmen.Any(s =>
                s.Id == j.SalesmanId && s.Name.Contains(filter.Salesman!)));
        // WPF: a JO with no delivery date passes an open date range.
        if (filter.DateFrom.HasValue)
            query = query.Where(j => j.DeliveryDate == null || j.DeliveryDate >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue)
            query = query.Where(j => j.DeliveryDate == null || j.DeliveryDate <= filter.DateTo.Value);
        if (filter.HasSearch)
        {
            var term = filter.SearchText!.Trim();
            query = query.Where(j =>
                j.JobNo.Contains(term)
                || j.CustomerName.Contains(term)
                || db.ProformaInvoices.Any(p => p.Id == j.ProformaId && p.ProformaNo.Contains(term)));
        }

        return query;
    }

    public async Task<List<JoReportRow>> GetJoRowsAsync(
        Guid tenantId, ReportingFilter filter, int maxRows, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var proformaId = await ResolveProformaIdAsync(db, filter.ProformaNo, ct).ConfigureAwait(false);
        var jobOrderId = await ResolveJobOrderIdAsync(db, filter.JobOrderNo, ct).ConfigureAwait(false);
        var query = FilteredJobOrders(db, filter, proformaId, jobOrderId);

        var page = await query
            .OrderByDescending(j => j.CreatedAt)
            .Take(maxRows)
            .Select(j => new { j.Id, j.JobNo, j.ProformaId, j.CustomerName, j.CreatedAt, j.GrandTotalVatInclusive })
            .ToListAsync(ct).ConfigureAwait(false);

        // WPF: qty/sqm from the JO's embedded PI snapshot; SaaS: the
        // normalized spec items of the source PI (same source of truth).
        var piIds = page.Select(j => j.ProformaId).Distinct().ToList();
        var groupIds = await db.SpecGroups.AsNoTracking()
            .Where(g => piIds.Contains(g.ProformaId))
            .Select(g => new { g.Id, g.ProformaId }).ToListAsync(ct).ConfigureAwait(false);
        var groupToPi = groupIds.ToDictionary(g => g.Id, g => g.ProformaId);
        var gidList = groupIds.Select(g => g.Id).ToList();
        var itemAgg = await db.SpecGroupItems.AsNoTracking()
            .Where(i => gidList.Contains(i.SpecGroupId))
            .GroupBy(i => i.SpecGroupId)
            .Select(g => new { SpecGroupId = g.Key, Qty = g.Sum(x => x.Qty), Sqm = g.Sum(x => x.TotalSqm) })
            .ToListAsync(ct).ConfigureAwait(false);
        var qtyByPi = new Dictionary<Guid, int>();
        var sqmByPi = new Dictionary<Guid, decimal>();
        foreach (var a in itemAgg)
        {
            var piId = groupToPi[a.SpecGroupId];
            qtyByPi[piId] = qtyByPi.TryGetValue(piId, out var q) ? q + a.Qty : a.Qty;
            sqmByPi[piId] = sqmByPi.TryGetValue(piId, out var s) ? s + a.Sqm : a.Sqm;
        }

        return page.Select(j => new JoReportRow(
            j.Id, j.JobNo, j.ProformaId, j.CustomerName, j.CreatedAt,
            qtyByPi.TryGetValue(j.ProformaId, out var qty) ? qty : 0,
            sqmByPi.TryGetValue(j.ProformaId, out var sqm) ? sqm : 0m,
            j.GrandTotalVatInclusive)).ToList();
    }

    public async Task<int> CountCohortInProductionAsync(
        Guid tenantId, IReadOnlyList<Guid> piIds, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.JobOrders.AsNoTracking()
            .Where(j => !j.IsVoid && piIds.Contains(j.ProformaId))
            .Select(j => j.ProformaId)
            .Distinct()
            .CountAsync(ct).ConfigureAwait(false);
    }

    // ── Row traceability (Phase 21 — WPF RowTraceabilityService) ──────

    public async Task<TraceProforma?> FindTraceProformaAsync(
        Guid tenantId, string proformaNo, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProformaInvoices.AsNoTracking()
            .Where(p => p.ProformaNo == proformaNo)
            .Select(p => new TraceProforma(
                p.Id, p.ProformaNo, p.Status, p.IssueDate,
                db.Clients.Where(c => c.Id == p.ClientId).Select(c => c.CustomerName).FirstOrDefault() ?? "",
                db.Projects.Where(pr => pr.Id == p.ProjectId).Select(pr => pr.IdentifierName).FirstOrDefault() ?? "",
                p.GrandTotalVatInclusive,
                p.PnlCalculatedAt, p.PnlNetExVat,
                p.PnlTotalGlassCost, p.PnlTotalProductionCost))
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task<List<TraceSourceRowDto>> ListTraceRowsAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var rows = await (from g in db.SpecGroups.AsNoTracking()
                          join i in db.SpecGroupItems.AsNoTracking() on g.Id equals i.SpecGroupId
                          where g.ProformaId == proformaId
                          orderby g.GroupNumber, i.RowSerial
                          select new TraceSourceRowDto(
                              "", // RowId filled by the service (needs the PI number)
                              "", g.GroupNumber, i.RowSerial,
                              g.SpecName, g.GlassType,
                              "", // GlassRef is snapshot-only (documented)
                              i.W1, i.H1, i.W2, i.H2,
                              i.Qty, i.UnitPrice, i.Total))
            .ToListAsync(ct).ConfigureAwait(false);
        return rows;
    }

    public async Task<List<TraceJobOrder>> ListJobOrdersForProformaAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.JobOrders.AsNoTracking()
            .Where(j => j.ProformaId == proformaId)
            .OrderBy(j => j.CreatedAt)
            .Select(j => new TraceJobOrder(
                j.JobNo, j.Status, j.Priority, j.DeliveryDate, j.CreatedAt,
                j.GrandTotalVatInclusive))
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> CountJobOrdersForProformaAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.JobOrders.AsNoTracking()
            .Where(j => j.ProformaId == proformaId)
            .CountAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The PI's delivery lines on NON-MASTER notes (WPF verbatim: voided
    /// notes are NOT excluded here — documented; the reconciliation
    /// endpoint does exclude them).
    /// </summary>
    public async Task<List<TraceDeliveryItem>> ListTraceDeliveriesAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await (from i in db.DeliveryNoteItems.AsNoTracking()
                      join d in db.DeliveryNotes.AsNoTracking() on i.DeliveryNoteId equals d.Id
                      join j in db.JobOrders.AsNoTracking() on d.JobOrderId equals j.Id
                      where j.ProformaId == proformaId && !d.IsMasterDn
                      orderby d.DeliveryDate
                      select new TraceDeliveryItem(
                          d.DeliveryNo, d.Status, d.DeliveryDate,
                          d.Driver, d.Vehicle, d.DeliveryType,
                          i.SpecGroupNumber, i.SerialNumber,
                          i.DeliveredQty, i.ReworkQty, i.ReturnedQty))
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<List<TraceInvoiceItem>> ListTraceInvoiceItemsAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await (from item in db.TaxInvoiceItems.AsNoTracking()
                      join t in db.TaxInvoices.AsNoTracking() on item.TaxInvoiceId equals t.Id
                      where t.ProformaId == proformaId
                      orderby t.IssueDate
                      select new TraceInvoiceItem(
                          t.InvoiceNo, t.Status, t.IssueDate,
                          item.SpecGroupNumber, item.SerialNumber,
                          item.Qty, item.Value))
            .ToListAsync(ct).ConfigureAwait(false);
    }

    // ── Cutting reports (Phase 22 — WPF CuttingReportService) ─────────

    public async Task<TraceJob?> FindTraceJobAsync(
        Guid tenantId, string jobOrderNo, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.JobOrders.AsNoTracking()
            .Where(j => j.JobNo == jobOrderNo)
            .Select(j => new TraceJob(j.Id, j.JobNo, j.ProformaId))
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task<string?> GetProformaNoAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProformaInvoices.AsNoTracking()
            .Where(p => p.Id == proformaId)
            .Select(p => p.ProformaNo)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task<string?> GetUserDisplayNameAsync(
        Guid tenantId, Guid? userId, CancellationToken ct)
    {
        if (userId is null) return null;
        using var db = factory.CreateForTenant(tenantId);
        return await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task<List<ReservationRow>> ListReservationsForJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.MaterialReservations.AsNoTracking()
            .Where(r => r.JobOrderId == jobOrderId)
            .Select(r => new ReservationRow(r.ReservedQty, r.Status))
            .ToListAsync(ct).ConfigureAwait(false);
    }
}
