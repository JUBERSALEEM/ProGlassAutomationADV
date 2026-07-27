// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Dashboard/TaxInvoiceMetrics/ITaxInvoiceMetricsService.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Services.Dashboard.TaxInvoiceMetrics
//
// PURPOSE:
//   Contract for the Tax Invoice dashboard metrics service.
//   Separates the query logic from the ViewModel so it can be
//   easily tested or swapped (e.g. mock data for design-time).
//
// IMPLEMENTATION: TaxInvoiceMetricsService.cs (FILE 07)
// CONSUMER: TiDashboardViewModel.cs (FILE 08)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Dashboard.TaxInvoiceDashboard;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Dashboard.TaxInvoiceMetrics
{
    /// <summary>
    /// Defines all data aggregation queries needed by the Tax Invoice
    /// dashboard tab. All methods are async to keep the UI responsive
    /// while SQLite queries execute.
    /// </summary>
    public interface ITaxInvoiceMetricsService
    {
        /// <summary>
        /// Loads all 5 KPI hero card values in one pass.
        /// Single method = single DB round-trip (loads all TI records once,
        /// then slices in-memory for MTD / overdue / pending / paid buckets).
        /// </summary>
        /// <returns>
        /// Populated <see cref="TaxInvoiceKpiSummary"/> with all card data.
        /// Returns a zeroed summary (never null) if no invoices exist yet.
        /// </returns>
        Task<TaxInvoiceKpiSummary> GetKpiSummaryAsync();

        /// <summary>
        /// Returns the most recently created/issued Tax Invoices.
        /// Used by the "Recent Tax Invoices" widget (default: last 5).
        /// Ordered by IssueDate DESC, then CreatedAt DESC as tiebreaker.
        /// </summary>
        /// <param name="limit">Maximum rows to return. Default 5.</param>
        Task<List<RecentInvoiceItem>> GetRecentInvoicesAsync(int limit = 5);

        /// <summary>
        /// Returns the 4 standard aging brackets for overdue invoices.
        /// Only invoices with Status NOT IN (Paid, Cancelled, Void) and
        /// DueDate &lt; today are included.
        /// Brackets: 0-30 · 31-60 · 61-90 · 90+ days past due date.
        /// NormalizedWidth is NOT set here — set by ViewModel after return.
        /// </summary>
        Task<List<AgingBracket>> GetAgingReportAsync();

        /// <summary>
        /// Returns the top N customers ranked by total invoiced amount
        /// over the past N months (rolling window, not calendar year).
        /// Excludes Draft, Cancelled, Void invoices from the sum.
        /// NormalizedWidth is NOT set here — set by ViewModel after return.
        /// </summary>
        /// <param name="monthsBack">Rolling window in months. Default 12.</param>
        /// <param name="limit">Max customers to return. Default 5.</param>
        Task<List<TopCustomerItem>> GetTopCustomersAsync(int monthsBack = 12, int limit = 5);

        /// <summary>
        /// Returns monthly revenue data points for the trend bar chart.
        /// Covers the last N calendar months (oldest → newest order).
        /// NormalizedHeight and BarColor are NOT set here — set by ViewModel.
        /// </summary>
        /// <param name="monthsBack">Number of months to include. Default 6.</param>
        Task<List<MonthlyRevenuePoint>> GetMonthlyRevenueTrendAsync(int monthsBack = 6);
    }
}