// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27), Phase 20:
//   Services/Reporting/ReportingDataService.Executive.cs      (slice 5)
//   Services/Reporting/ReportingDataService.Profitability.cs
//   Services/Reporting/ReportingDataService.Analytics.cs
// The WPF composes the executive summary from the per-module summaries
// under ONE shared filter so the executive page and the module reports
// can never disagree — the cardinal rule applied across modules. This
// port keeps exactly that structure (the store owns each summary).
//
// Documented differences (mapping §1.9):
//  • Cost-basis staleness: the WPF classifies each P&L against the Sheet
//    Store price stamp (GlassPriceStamper). The SaaS Step 9 has no
//    persisted price stamp, so IsStaleCost is always false and the note
//    is empty — surfaced here rather than invented.
//  • Tax invoice statuses use the WPF-verbatim vocabulary ported in
//    Phase 12 (Draft/Issued/Paid/PartiallyPaid/Overdue/Cancelled/Void).
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlass.Saas.Application.Reporting;

public sealed class ReportingService(
    IReportingStore store,
    ReportingCacheService? cache = null)
{
    /// <summary>
    /// A step converting below this rate is flagged as a bottleneck.
    /// Deliberately generous: a warning that fires on every row stops
    /// being a warning.
    /// </summary>
    private const decimal BottleneckThresholdPct = 60m;

    /// <summary>
    /// Upper bound on rows pulled for analytics (the WPF DoS guard,
    /// shared with the VAT report so the two behave consistently).
    /// </summary>
    private const int AnalyticsMaxRows = 10000;

    // ═════════════════════════════════════════════════════════════════
    //  EXECUTIVE SUMMARY
    // ═════════════════════════════════════════════════════════════════

    public async Task<ExecutiveSummaryDto> GetExecutiveSummaryAsync(
        Guid tenantId, ReportingFilter? filter, CancellationToken ct = default)
    {
        filter ??= new ReportingFilter();

        // The 30s memoization (WPF Phase 9): a repeated filter within the
        // window hits the cache instead of re-querying every module.
        if (cache != null)
        {
            var key = cache.BuildKey(tenantId, "executive", filter);
            return await cache.GetOrAddAsync(key,
                () => BuildExecutiveSummaryAsync(tenantId, filter, ct)).ConfigureAwait(false);
        }

        return await BuildExecutiveSummaryAsync(tenantId, filter, ct).ConfigureAwait(false);
    }

    private async Task<ExecutiveSummaryDto> BuildExecutiveSummaryAsync(
        Guid tenantId, ReportingFilter filter, CancellationToken ct)
    {
        var result = new ExecutiveSummaryDto
        {
            GeneratedAt = DateTime.UtcNow,
            PeriodDescription = DescribePeriod(filter)
        };

        // Every stage under the SAME filter — that is what keeps the
        // executive page reconciled with the module reports.
        var pi = await store.GetPiSummaryAsync(tenantId, filter, ct).ConfigureAwait(false);
        var dn = await store.GetDeliverySummaryAsync(tenantId, filter, ct).ConfigureAwait(false);
        var ti = await store.GetTaxInvoiceSummaryAsync(tenantId, filter, ct).ConfigureAwait(false);

        // Cohort milestone funnel (WPF 2026-09-10 rewrite).
        await BuildCohortFunnelAsync(tenantId, result, filter, pi, ti, ct).ConfigureAwait(false);

        result.QuotedValue = pi?.TotalValue ?? 0m;
        result.InvoicedValue = ti?.TotalAmount ?? 0m;
        // Balance due across tax invoices in the period — the TI summary
        // carries the figure the TaxInvoice report shows, so the two
        // reconcile (WPF: Σ grand total of non-paid invoices).
        result.OutstandingValue = ti?.Outstanding ?? 0m;
        result.UnconvertedQuotes = pi?.NotConverted ?? 0;
        result.UninvoicedConfirmed = ti?.PendingInvoice ?? 0;
        result.OverdueInvoices = ti?.Overdue ?? 0;
        result.DeliveryCompletionPct = dn?.CompletionPct ?? 0m;

        // Profitability and mix are best-effort enrichments: a failure in
        // either must leave the funnel intact, since the funnel is the
        // reason the page exists.
        await AddProfitabilityAsync(tenantId, result, filter, ct).ConfigureAwait(false);
        await AddMixAsync(tenantId, result, filter, ct).ConfigureAwait(false);

        return result;
    }

    // ── THE COHORT FUNNEL ─────────────────────────────────────────────
    //
    // A true COHORT MILESTONE funnel: stage 1 is the set of quotes in the
    // period; every later stage counts how many of THOSE SAME quotes
    // reached the milestone (became a job order → fully delivered →
    // invoiced → paid). Every count is a distinct-ProformaId count so
    // units can never mix and every rate is a real "share of quotes".
    // Dates on downstream stages are intentionally ignored
    // (ever-happened semantics). All filters flow through the same store
    // filter path so the funnel, the module reports and the KPI cards
    // cannot disagree.

    private async Task BuildCohortFunnelAsync(
        Guid tenantId,
        ExecutiveSummaryDto result,
        ReportingFilter filter,
        PiSummaryDto? pi,
        TaxInvoiceSummaryDto? ti,
        CancellationToken ct)
    {
        int quoted = pi?.TotalPi ?? 0;

        // The cohort: quotes in the period (the PI summary's own filter).
        var piIds = quoted > 0
            ? await store.FilteredProformaIdsAsync(tenantId, filter, ct).ConfigureAwait(false)
            : new List<Guid>();

        // Stage 2: cohort quotes that became job orders.
        int inProduction = piIds.Count > 0
            ? await store.CountCohortInProductionAsync(tenantId, piIds, ct).ConfigureAwait(false)
            : 0;

        // Stage 3: cohort quotes FULLY delivered — a quote counts when
        // EVERY one of its spec rows is complete (row predicates mirror
        // the delivery report: non-void parents, no phantom rows, real
        // quantities; master DNs carry no delivery credit).
        int delivered = 0;
        if (piIds.Count > 0)
        {
            var rows = await store.GetCohortDeliveryRowsAsync(tenantId, piIds, filter, ct)
                .ConfigureAwait(false);
            delivered = rows
                .GroupBy(r => r.ProformaId)
                .Count(g => g.All(r => r.TotalQty > 0 && r.DeliveredQty >= r.TotalQty));
        }

        // Stages 4 + 5: cohort quotes invoiced / paid.
        int invoiced = 0;
        int paid = 0;
        decimal invoicedValue = 0m;
        if (piIds.Count > 0)
        {
            var pairs = (await store.GetFilteredInvoicePairsAsync(tenantId, filter, ct)
                    .ConfigureAwait(false))
                .Where(p => piIds.Contains(p.ProformaId))
                .ToList();

            invoiced = pairs.Select(p => p.ProformaId).Distinct().Count();
            paid = pairs
                .Where(p => IsPaid(p.Status))
                .Select(p => p.ProformaId)
                .Distinct()
                .Count();
            invoicedValue = pairs.Sum(p => p.GrandTotal);
        }

        var stages = new List<FunnelStageDto>
        {
            new()
            {
                Label = "Quoted (Proforma)",
                Count = quoted,
                Value = pi?.TotalValue ?? 0m
            },
            new()
            {
                Label = "In production (Job Order)",
                Count = inProduction,
                Value = 0m
            },
            new()
            {
                Label = "Delivered (full order)",
                Count = delivered,
                Value = 0m
            },
            new()
            {
                Label = "Invoiced (Tax Invoice)",
                Count = invoiced,
                Value = invoicedValue
            },
            new()
            {
                Label = "Paid",
                Count = paid,
                Value = 0m
            }
        };

        decimal first = stages[0].Count;

        for (int i = 0; i < stages.Count; i++)
        {
            var s = stages[i];
            s.FirstStageCount = (int)first;

            s.SharePct = first <= 0m
                ? 0m
                : Math.Round(s.Count / first * 100m, 1);

            if (i == 0)
            {
                s.StepConversionPct = 100m;
                continue;
            }

            decimal prev = stages[i - 1].Count;
            s.StepConversionPct = prev <= 0m
                ? 0m
                : Math.Round(s.Count / prev * 100m, 1);

            // Only flag when the previous stage actually had volume;
            // 0 -> 0 is not a bottleneck, it is an empty period.
            s.IsBottleneck = prev > 0m && s.StepConversionPct < BottleneckThresholdPct;
        }

        result.Funnel = stages;
    }

    private async Task AddProfitabilityAsync(
        Guid tenantId, ExecutiveSummaryDto result, ReportingFilter filter, CancellationToken ct)
    {
        try
        {
            var p = await GetProfitabilitySummaryAsync(tenantId, filter, ct).ConfigureAwait(false);
            if (p == null) return;

            result.Revenue = p.TotalRevenue;
            result.Cost = p.TotalCost;

            // Surfaced, not buried: a margin computed from half the
            // invoices looks authoritative and misleads.
            result.InvoicesWithoutCost = p.InvoicesWithoutCost;
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            // Best-effort enrichment — the funnel stays intact.
        }
    }

    private async Task AddMixAsync(
        Guid tenantId, ExecutiveSummaryDto result, ReportingFilter filter, CancellationToken ct)
    {
        try
        {
            var jo = await GetJoAnalyticsAsync(tenantId, filter, ct).ConfigureAwait(false);
            if (jo != null)
            {
                result.TopSpecifications = jo.TopSpecifications;
                result.TopCustomers = jo.TopCustomers;
                result.DominantSpecification = jo.DominantSpecification;
                result.DominantColour = jo.DominantColour;
            }

            var dn = await GetDeliveryAnalyticsAsync(tenantId, filter, ct).ConfigureAwait(false);
            if (dn != null)
            {
                result.CriticalPendingQty = dn.CriticalPendingQty;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            // Best-effort enrichment.
        }
    }

    /// <summary>
    /// Human-readable filter description — a summary without its criteria
    /// stated is unauditable.
    /// </summary>
    internal static string DescribePeriod(ReportingFilter f)
    {
        var parts = new List<string>();

        if (f.DateFrom.HasValue && f.DateTo.HasValue)
            parts.Add($"{f.DateFrom.Value:dd-MMM-yyyy} to {f.DateTo:dd-MMM-yyyy}");
        else if (f.DateFrom.HasValue)
            parts.Add($"from {f.DateFrom:dd-MMM-yyyy}");
        else if (f.DateTo.HasValue)
            parts.Add($"up to {f.DateTo:dd-MMM-yyyy}");
        else
            parts.Add("All time");

        if (f.HasCompany) parts.Add($"company: {f.Company}");
        if (f.HasCustomer) parts.Add($"customer: {f.Customer}");
        if (f.HasSalesman) parts.Add($"salesman: {f.Salesman}");
        if (f.HasProject) parts.Add($"project: {f.Project}");
        if (!string.IsNullOrWhiteSpace(f.GlassType)) parts.Add($"glass: {f.GlassType}");

        return string.Join("  ·  ", parts);
    }

    // ═════════════════════════════════════════════════════════════════
    //  ANALYTICS (slice 1)
    // ═════════════════════════════════════════════════════════════════

    public async Task<JoAnalyticsDto> GetJoAnalyticsAsync(
        Guid tenantId, ReportingFilter? filter, CancellationToken ct = default)
    {
        filter ??= new ReportingFilter();

        if (cache != null)
        {
            var key = cache.BuildKey(tenantId, "jo-analytics", filter);
            var cached = await cache.GetOrAddAsync(key,
                () => BuildJoAnalyticsAsync(tenantId, filter, ct)).ConfigureAwait(false);
            return cached;
        }

        return await BuildJoAnalyticsAsync(tenantId, filter, ct).ConfigureAwait(false);
    }

    private async Task<JoAnalyticsDto> BuildJoAnalyticsAsync(
        Guid tenantId, ReportingFilter filter, CancellationToken ct)
    {
        try
        {
            // Clone so raising the page size cannot leak back into the
            // caller's filter and silently change their grid paging.
            var wide = CloneForAnalytics(filter);

            var jobOrders = await store.GetJoRowsAsync(tenantId, wide, AnalyticsMaxRows, ct)
                .ConfigureAwait(false);

            // Spec/colour mix needs per-item glass detail, which a
            // JoReportRow does not carry. The delivery row set is the
            // item-level view of the same filtered work.
            var items = await store.GetDeliveryRowsAsync(tenantId, wide, AnalyticsMaxRows, ct)
                .ConfigureAwait(false);

            return ReportingAnalytics.BuildJoAnalytics(jobOrders, items);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            // Analytics is an enhancement, never the gatekeeper.
            return new JoAnalyticsDto();
        }
    }

    public async Task<DeliveryAnalyticsDto> GetDeliveryAnalyticsAsync(
        Guid tenantId, ReportingFilter? filter, CancellationToken ct = default)
    {
        filter ??= new ReportingFilter();

        try
        {
            var wide = CloneForAnalytics(filter);
            var rows = await store.GetDeliveryRowsAsync(tenantId, wide, AnalyticsMaxRows, ct)
                .ConfigureAwait(false);

            return ReportingAnalytics.BuildDeliveryAnalytics(rows, DateTime.Now);
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return new DeliveryAnalyticsDto();
        }
    }

    private static ReportingFilter CloneForAnalytics(ReportingFilter source)
    {
        return new ReportingFilter
        {
            DateFrom = source.DateFrom,
            DateTo = source.DateTo,
            Company = source.Company,
            Customer = source.Customer,
            Salesman = source.Salesman,
            Project = source.Project,
            ProformaNo = source.ProformaNo,
            JobOrderNo = source.JobOrderNo,
            DeliveryNoteNo = source.DeliveryNoteNo,
            TaxInvoiceNo = source.TaxInvoiceNo,
            Status = source.Status,
            GlassType = source.GlassType,
            Specification = source.Specification,
            GlassRef = source.GlassRef,
            SearchText = source.SearchText,
            Page = 1,
            PageSize = AnalyticsMaxRows
        };
    }

    // ═════════════════════════════════════════════════════════════════
    //  PROFITABILITY
    // ═════════════════════════════════════════════════════════════════

    public async Task<ProfitabilitySummaryDto> GetProfitabilitySummaryAsync(
        Guid tenantId, ReportingFilter? filter, CancellationToken ct = default)
    {
        filter ??= new ReportingFilter();
        var rows = await GetProfitabilityRowsAsync(tenantId, new ReportingFilter
        {
            DateFrom = filter.DateFrom, DateTo = filter.DateTo,
            Company = filter.Company, Customer = filter.Customer, Project = filter.Project,
            Salesman = filter.Salesman, Page = 1, PageSize = 1000
        }, ct).ConfigureAwait(false);

        var summary = new ProfitabilitySummaryDto
        {
            TotalRevenue = rows.Items.Sum(r => r.Revenue),
            TotalCost = rows.Items.Sum(r => r.Cost),
            TotalProfit = rows.Items.Sum(r => r.Profit),
            InvoicesWithCost = rows.Items.Count(r => r.HasCostData),
            InvoicesWithoutCost = rows.Items.Count(r => !r.HasCostData)
        };
        summary.AvgMargin = summary.TotalRevenue == 0
            ? 0
            : Math.Round(summary.TotalProfit / summary.TotalRevenue * 100, 1);
        summary.MissingCostSources = rows.Items
            .Where(r => !r.HasCostData).Select(r => r.Name).Distinct().Take(10).ToList();
        return summary;
    }

    public async Task<PagedResult<ProfitabilityRowDto>> GetProfitabilityRowsAsync(
        Guid tenantId, ReportingFilter? filter, CancellationToken ct = default)
    {
        filter ??= new ReportingFilter();
        var page = await store.GetProfitabilityRowsAsync(tenantId, filter, ct).ConfigureAwait(false);

        var rows = new List<ProfitabilityRowDto>();
        foreach (var pi in page.Items)
        {
            // Same "costed" gate as the WPF: a Step 9 result with rows,
            // cost, or net. Do NOT VAT-divide the header when Step 9 was
            // never opened — that invented revenue with profit 0 and
            // diluted AvgMargin.
            decimal glass = pi.PnlTotalGlassCost;
            decimal production = pi.PnlTotalProductionCost;
            bool costed = pi.HasPnl
                && (pi.HasPnlRows || glass > 0m || production > 0m || pi.PnlNetExVat > 0m);
            bool hasCost = costed;

            // Documented difference: no persisted sheet-store price stamp
            // in the SaaS Step 9 — staleness classification is not
            // invented (always fresh; the note stays empty).
            bool isStaleCost = false;
            string stalenessNote = "";

            // Persist Step 9 numbers. Do not recompute Profit = revenue −
            // cost (charges/discount/rounding live in the saved Profit).
            decimal revenue = costed ? pi.PnlNetExVat : 0m;
            decimal cost = costed ? glass + production : 0m;
            decimal profit = costed ? pi.PnlProfit : 0m;
            decimal margin = revenue == 0 ? 0 : Math.Round(profit / revenue * 100, 1);

            rows.Add(new ProfitabilityRowDto
            {
                Key = pi.ProformaNo,
                Name = pi.ProformaNo,
                Category = "PI",
                Revenue = revenue,
                Cost = cost,
                GlassCost = glass,
                ProductionCost = production,
                Profit = profit,
                Margin = margin,
                Qty = pi.Qty,
                Sqm = Math.Round(pi.TotalSqm, 3),
                DocumentCount = 1,
                HasCostData = hasCost,
                IsStaleCost = isStaleCost,
                CostStalenessNote = stalenessNote,
                CostSource = hasCost
                    ? (pi.PnlCostAtSellingPrice ? "Selling price" : "Purchase price")
                      + $" (wastage {pi.PnlWastagePercent}%)"
                      + (isStaleCost ? " — ⚠ " + stalenessNote : "")
                    : "Missing — no Step 9 calculation saved"
            });
        }

        return new PagedResult<ProfitabilityRowDto>
        {
            Items = rows, TotalCount = page.TotalCount, Page = filter.Page, PageSize = filter.PageSize
        };
    }

    /// <summary>Delivery rows for CSV export (the analytics window —
    /// the WHOLE filtered set, never one page).</summary>
    public Task<List<DeliveryReportRow>> GetDeliveryRowsForExportAsync(
        Guid tenantId, ReportingFilter filter, CancellationToken ct = default) =>
        store.GetDeliveryRowsAsync(tenantId, CloneForAnalytics(filter), AnalyticsMaxRows, ct);

    /// <summary>Job-order rows for CSV export (the whole filtered set).</summary>
    public Task<List<JoReportRow>> GetJoRowsForExportAsync(
        Guid tenantId, ReportingFilter filter, CancellationToken ct = default) =>
        store.GetJoRowsAsync(tenantId, CloneForAnalytics(filter), AnalyticsMaxRows, ct);

    // ── helpers ────────────────────────────────────────────────────────

    /// <summary>WPF TaxInvoiceStatusValues.IsPaid.</summary>
    internal static bool IsPaid(string status) => status == "Paid";
}
