// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27), Phase 20:
//   Views/Reports — the executive summary + profitability report family
//   (the WPF renders these in-app; the SaaS exposes the same read models
//   over the API — PDF/print rendering stays a documented deferral).
// Permission: the WPF Reports module requires Reports.View.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.Reporting;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

public sealed class ReportingController(
    ReportingService reporting) : TenantControllerBase
{
    /// <summary>
    /// Cross-module executive summary: the order-to-cash cohort funnel
    /// (Quoted → In production → Delivered → Invoiced → Paid with share +
    /// step-conversion + bottleneck flags), headline money, operational
    /// health and the spec/customer mix — all under ONE shared filter.
    /// </summary>
    [HttpGet("executive-summary")]
    [HasPermission(AppPermissions.ReportsView)]
    public async Task<ActionResult<ExecutiveSummaryDto>> GetExecutiveSummary(
        [FromQuery] ReportingQuery query, CancellationToken ct)
    {
        var tenant = RequiredTenantId;
        return Ok(await reporting.GetExecutiveSummaryAsync(
            tenant, query.ToFilter(), ct).ConfigureAwait(false));
    }

    /// <summary>Profitability rows — one per proforma; revenue/cost/profit
    /// from the persisted Step 9 P&amp;L (uncosted PIs surface as Missing).
    /// Paged + DB-side sorted.</summary>
    [HttpGet("profitability")]
    [HasPermission(AppPermissions.ReportsView)]
    public async Task<ActionResult<PagedResult<ProfitabilityRowDto>>> GetProfitability(
        [FromQuery] ReportingQuery query, CancellationToken ct)
    {
        var tenant = RequiredTenantId;
        return Ok(await reporting.GetProfitabilityRowsAsync(
            tenant, query.ToFilter(), ct).ConfigureAwait(false));
    }

    /// <summary>Profitability summary — totals, average margin and the
    /// costing-coverage gap (invoices without a Step 9 calculation).</summary>
    [HttpGet("profitability/summary")]
    [HasPermission(AppPermissions.ReportsView)]
    public async Task<ActionResult<ProfitabilitySummaryDto>> GetProfitabilitySummary(
        [FromQuery] ReportingQuery query, CancellationToken ct)
    {
        var tenant = RequiredTenantId;
        return Ok(await reporting.GetProfitabilitySummaryAsync(
            tenant, query.ToFilter(), ct).ConfigureAwait(false));
    }

    /// <summary>Job-order mix analytics: top customers/specifications/
    /// colours/glass types (Pareto, top 10 + Other) and weekly throughput.</summary>
    [HttpGet("analytics/job-orders")]
    [HasPermission(AppPermissions.ReportsView)]
    public async Task<ActionResult<JoAnalyticsDto>> GetJoAnalytics(
        [FromQuery] ReportingQuery query, CancellationToken ct)
    {
        var tenant = RequiredTenantId;
        return Ok(await reporting.GetJoAnalyticsAsync(
            tenant, query.ToFilter(), ct).ConfigureAwait(false));
    }

    /// <summary>Delivery analytics: pending ageing buckets (0–7 / 8–14 /
    /// 15–30 / over-30 critical), pending by customer/specification.</summary>
    [HttpGet("analytics/delivery")]
    [HasPermission(AppPermissions.ReportsView)]
    public async Task<ActionResult<DeliveryAnalyticsDto>> GetDeliveryAnalytics(
        [FromQuery] ReportingQuery query, CancellationToken ct)
    {
        var tenant = RequiredTenantId;
        return Ok(await reporting.GetDeliveryAnalyticsAsync(
            tenant, query.ToFilter(), ct).ConfigureAwait(false));
    }
}

/// <summary>Query-string binding for the shared reporting filter.</summary>
public sealed class ReportingQuery
{
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public string? Company { get; set; }
    public string? Customer { get; set; }
    public string? Salesman { get; set; }
    public string? Project { get; set; }
    public string? ProformaNo { get; set; }
    public string? JobOrderNo { get; set; }
    public string? DeliveryNoteNo { get; set; }
    public string? TaxInvoiceNo { get; set; }
    public string? Status { get; set; }
    public string? GlassType { get; set; }
    public string? Specification { get; set; }
    public string? GlassRef { get; set; }
    public string? SearchText { get; set; }
    public string? SortField { get; set; }
    public bool SortDescending { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;

    public ReportingFilter ToFilter() => new()
    {
        DateFrom = DateFrom,
        DateTo = DateTo,
        Company = Company,
        Customer = Customer,
        Salesman = Salesman,
        Project = Project,
        ProformaNo = ProformaNo,
        JobOrderNo = JobOrderNo,
        DeliveryNoteNo = DeliveryNoteNo,
        TaxInvoiceNo = TaxInvoiceNo,
        Status = Status,
        GlassType = GlassType,
        Specification = Specification,
        GlassRef = GlassRef,
        SearchText = SearchText,
        SortField = SortField,
        SortDescending = SortDescending,
        Page = Page,
        PageSize = PageSize
    };
}
