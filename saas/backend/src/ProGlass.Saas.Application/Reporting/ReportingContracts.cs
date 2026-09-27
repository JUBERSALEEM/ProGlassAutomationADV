// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27), Phase 20:
//   Services/Reporting/ReportingFilter.cs        — filter + PagedResult
//   Services/Reporting/ReportingModels.cs        — summary DTOs (§1.9)
//   Services/Reporting/ExecutiveSummary.cs       — funnel + headline DTOs
//   Services/Reporting/ReportingAnalyticsModels.cs — mix/ageing DTOs
// The WPF files are the source of truth; this port changes no behaviour
// except where a documented difference is cited (normalized schema names
// are joined instead of denormalized columns; SaaS statuses are mapped).
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Application.Reporting;

/// <summary>
/// Single filter contract consumed by ALL report read models — the WPF
/// ReportingFilter port. Every field optional; dates are inclusive;
/// text filters are case-insensitive contains; Status is exact match.
/// </summary>
public sealed class ReportingFilter
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

    // Glass/Spec filters
    public string? GlassType { get; set; }
    public string? Specification { get; set; }
    public string? GlassRef { get; set; }

    // Global text search
    public string? SearchText { get; set; }

    // Sort (report-defined key; unknown keys fall back to the default)
    public string? SortField { get; set; }
    public bool SortDescending { get; set; } = true;

    // Pagination
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;

    public int Skip => (Math.Max(1, Page) - 1) * Math.Max(1, PageSize);
    public int Take => Math.Max(1, PageSize);

    public bool HasCompany => !string.IsNullOrWhiteSpace(Company);
    public bool HasCustomer => !string.IsNullOrWhiteSpace(Customer);
    public bool HasSalesman => !string.IsNullOrWhiteSpace(Salesman);
    public bool HasProject => !string.IsNullOrWhiteSpace(Project);
    public bool HasSearch => !string.IsNullOrWhiteSpace(SearchText);
    public bool HasDateRange => DateFrom.HasValue || DateTo.HasValue;

    public ReportingFilter Clone() => (ReportingFilter)MemberwiseClone();
}

/// <summary>WPF PagedResult&lt;T&gt; port (TotalPages/HasNext/HasPrevious).</summary>
public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}

// ── PI summary (WPF PiReportSummary) ─────────────────────────────────

public sealed class PiSummaryDto
{
    public int TotalPi { get; set; }
    public int Draft { get; set; }
    public int Released { get; set; }
    public int Confirmed { get; set; }
    public int Rejected { get; set; }
    public int Pending { get; set; }
    public int Converted { get; set; }
    public int NotConverted { get; set; }
    public decimal TotalValue { get; set; }
    public int TotalQty { get; set; }
    public decimal TotalSqm { get; set; }
}

// ── Tax invoice summary (WPF TaxInvoiceReportSummary) ────────────────

public sealed class TaxInvoiceSummaryDto
{
    public int TotalInvoices { get; set; }
    public int Issued { get; set; }
    public int Paid { get; set; }
    public int PartiallyPaid { get; set; }
    public int Overdue { get; set; }
    public int Draft { get; set; }
    public int PendingInvoice { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalVat { get; set; }
    public decimal Outstanding { get; set; }
    public decimal InvoicableAmount { get; set; }
    public decimal BalanceInvoicableAmount { get; set; }
}

// ── Delivery summary (WPF DeliveryReportSummary) ─────────────────────

public sealed class DeliverySummaryDto
{
    public int TotalRows { get; set; }
    public int NotDelivered { get; set; }
    public int Partial { get; set; }
    public int FullyDelivered { get; set; }
    public int TotalOrderedQty { get; set; }
    public int TotalDeliveredQty { get; set; }
    public int TotalRemainingQty { get; set; }
    public decimal CompletionPct =>
        TotalOrderedQty <= 0 ? 0 : Math.Round(TotalDeliveredQty / (decimal)TotalOrderedQty * 100, 1);
}

// ── Analytics (WPF MixRow / AgeingBucket / Jo+Delivery summaries) ────

public sealed class MixRowDto
{
    public int Rank { get; set; }
    public string Label { get; set; } = "";
    public int Count { get; set; }
    public int Qty { get; set; }
    public decimal Sqm { get; set; }
    public decimal Value { get; set; }
    public decimal SharePct { get; set; }
    public bool IsOtherBucket { get; set; }
}

public sealed class AgeingBucketDto
{
    public string Label { get; set; } = "";
    public int MinDays { get; set; }
    public int MaxDays { get; set; }
    public int Count { get; set; }
    public int Qty { get; set; }
    public decimal Sqm { get; set; }
    public decimal SharePct { get; set; }
    public bool IsCritical { get; set; }
}

public sealed class ThroughputPointDto
{
    public DateTime WeekStart { get; set; }
    public int Count { get; set; }
    public decimal Sqm { get; set; }
}

public sealed class JoAnalyticsDto
{
    public int TotalJobOrders { get; set; }
    public int TotalQty { get; set; }
    public decimal TotalSqm { get; set; }
    public decimal TotalValue { get; set; }
    public List<MixRowDto> TopCustomers { get; set; } = new();
    public List<MixRowDto> TopSpecifications { get; set; } = new();
    public List<MixRowDto> TopColours { get; set; } = new();
    public List<MixRowDto> GlassTypeMix { get; set; } = new();
    public List<ThroughputPointDto> Throughput { get; set; } = new();
    public string DominantSpecification { get; set; } = "—";
    public decimal TopSpecSharePct { get; set; }
    public string DominantColour { get; set; } = "—";
}

public sealed class DeliveryAnalyticsDto
{
    public int TotalRows { get; set; }
    public int OrderedQty { get; set; }
    public int DeliveredQty { get; set; }
    public int PendingQty { get; set; }
    public int NotStartedRows { get; set; }
    public int CompletedRows { get; set; }
    public int PartialRows { get; set; }
    public List<AgeingBucketDto> PendingAgeing { get; set; } = new();
    public int CriticalPendingQty { get; set; }
    public List<MixRowDto> PendingByCustomer { get; set; } = new();
    public List<MixRowDto> PendingBySpecification { get; set; } = new();
    public List<MixRowDto> DeliveredBySpecification { get; set; } = new();
}

// ── Executive summary (WPF ExecutiveSummary + FunnelStage) ───────────

public sealed class FunnelStageDto
{
    public string Label { get; set; } = "";
    public int Count { get; set; }
    public decimal Value { get; set; }
    /// <summary>Share of the FIRST stage, 0-100 (single drop-off curve).</summary>
    public decimal SharePct { get; set; }
    /// <summary>Conversion from the immediately preceding stage, 0-100.</summary>
    public decimal StepConversionPct { get; set; }
    public int FirstStageCount { get; set; }
    public bool IsBottleneck { get; set; }
}

public sealed class ExecutiveSummaryDto
{
    public DateTime GeneratedAt { get; set; }
    public string PeriodDescription { get; set; } = "All time";

    public List<FunnelStageDto> Funnel { get; set; } = new();

    public decimal QuotedValue { get; set; }
    public decimal InvoicedValue { get; set; }
    public decimal OutstandingValue { get; set; }

    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit => Revenue - Cost;
    public decimal MarginPct => Revenue == 0m ? 0m : Math.Round(Profit / Revenue * 100m, 1);

    public int UnconvertedQuotes { get; set; }
    public int UninvoicedConfirmed { get; set; }
    public int OverdueInvoices { get; set; }
    public decimal DeliveryCompletionPct { get; set; }
    public int CriticalPendingQty { get; set; }

    public List<MixRowDto> TopSpecifications { get; set; } = new();
    public List<MixRowDto> TopCustomers { get; set; } = new();
    public string DominantSpecification { get; set; } = "—";
    public string DominantColour { get; set; } = "—";

    public int InvoicesWithoutCost { get; set; }
    public bool HasCostGap => InvoicesWithoutCost > 0;
    public bool HasData => Funnel.Count > 0 && Funnel[0].Count > 0;
}

// ── Profitability (WPF ProfitabilityRow / ProfitabilitySummary) ───────

public sealed class ProfitabilityRowDto
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal GlassCost { get; set; }
    public decimal ProductionCost { get; set; }
    public decimal Profit { get; set; }
    public decimal Margin { get; set; }
    public int Qty { get; set; }
    public decimal Sqm { get; set; }
    public int DocumentCount { get; set; }
    public bool HasCostData { get; set; }
    public bool IsStaleCost { get; set; }
    public string CostStalenessNote { get; set; } = "";
    public string CostSource { get; set; } = "";
}

public sealed class ProfitabilitySummaryDto
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal AvgMargin { get; set; }
    public int InvoicesWithCost { get; set; }
    public int InvoicesWithoutCost { get; set; }
    public List<string> MissingCostSources { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════════════════
//  Store port — the read-model queries the WPF ran with EF over its 16
//  SQLite DBs, re-expressed over the normalized SaaS schema (joins to
//  master data replace denormalized name columns; documented in the
//  mapping doc §1.9).
// ═══════════════════════════════════════════════════════════════════════

/// <summary>One aggregated delivery report row (WPF DeliveryRowAgg).</summary>
public sealed record DeliveryReportRow(
    Guid? JobOrderId,
    Guid ProformaId,
    int SpecGroupNumber,
    int SerialNumber,
    string Specification,
    string CustomerName,
    string ProjectName,
    string GlassRef,
    string OuterColour,
    string GlassType,
    int W1, int H1, int W2, int H2,
    int TotalQty,
    int DeliveredQty,
    DateTime OrderedAt)
{
    public int BalanceQty => Math.Max(0, TotalQty - DeliveredQty);
}

/// <summary>One job-order analytics row (WPF JoReportRow, reduced).</summary>
public sealed record JoReportRow(
    Guid Id,
    string JobNo,
    Guid ProformaId,
    string CustomerName,
    DateTime CreatedAt,
    int TotalQty,
    decimal TotalSqm,
    decimal GrandTotal);

/// <summary>Per-PI aggregate for profitability (WPF snapshot P&amp;L read).</summary>
public sealed record ProfitabilityPiRow(
    Guid Id,
    string ProformaNo,
    string CompanyName,
    string CustomerName,
    string ProjectName,
    string Status,
    DateOnly? IssueDate,
    DateTime CreatedAt,
    decimal GrandTotalVatInclusive,
    bool HasPnl,
    bool HasPnlRows,
    decimal PnlNetExVat,
    decimal PnlTotalGlassCost,
    decimal PnlTotalProductionCost,
    decimal PnlProfit,
    decimal PnlWastagePercent,
    bool PnlCostAtSellingPrice,
    int Qty,
    decimal TotalSqm);

/// <summary>Tax-invoice pair projection for the funnel (WPF pairs list).</summary>
public sealed record InvoicePair(Guid ProformaId, string Status, decimal GrandTotal);

public interface IReportingStore
{
    // ── PI ────────────────────────────────────────────────────────────
    Task<PiSummaryDto> GetPiSummaryAsync(Guid tenantId, ReportingFilter filter, CancellationToken ct);
    Task<List<Guid>> FilteredProformaIdsAsync(Guid tenantId, ReportingFilter filter, CancellationToken ct);
    Task<PagedResult<ProfitabilityPiRow>> GetProfitabilityRowsAsync(Guid tenantId, ReportingFilter filter, CancellationToken ct);

    // ── Tax invoices ──────────────────────────────────────────────────
    Task<TaxInvoiceSummaryDto> GetTaxInvoiceSummaryAsync(Guid tenantId, ReportingFilter filter, CancellationToken ct);
    Task<List<InvoicePair>> GetFilteredInvoicePairsAsync(Guid tenantId, ReportingFilter filter, CancellationToken ct);
    Task<int> CountConfirmedWithoutInvoiceAsync(Guid tenantId, ReportingFilter filter, CancellationToken ct);
    Task<decimal> SumConfirmedPiValueAsync(Guid tenantId, ReportingFilter filter, CancellationToken ct);

    // ── Delivery ──────────────────────────────────────────────────────
    Task<DeliverySummaryDto> GetDeliverySummaryAsync(Guid tenantId, ReportingFilter filter, CancellationToken ct);
    Task<List<DeliveryReportRow>> GetDeliveryRowsAsync(Guid tenantId, ReportingFilter filter, int maxRows, CancellationToken ct);
    Task<List<DeliveryReportRow>> GetCohortDeliveryRowsAsync(Guid tenantId, IReadOnlyList<Guid> piIds, ReportingFilter filter, CancellationToken ct);

    // ── Job orders ────────────────────────────────────────────────────
    Task<List<JoReportRow>> GetJoRowsAsync(Guid tenantId, ReportingFilter filter, int maxRows, CancellationToken ct);
    Task<int> CountCohortInProductionAsync(Guid tenantId, IReadOnlyList<Guid> piIds, CancellationToken ct);
}
