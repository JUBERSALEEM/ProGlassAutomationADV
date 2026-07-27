// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Dashboard/TaxInvoiceMetrics/TaxInvoiceMetricsService.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Services.Dashboard.TaxInvoiceMetrics
//
// PURPOSE:
//   Concrete implementation of ITaxInvoiceMetricsService.
//   All queries run against TaxInvoiceDbContext (taxinvoices.db).
//
// PATTERN:
//   Loads ALL relevant invoices once into a typed list,
//   then slices in-memory for each metric bucket.
//   Single DB round-trip per LoadAsync() call.
//
// STATUS BUCKETS:
//   Excluded from revenue:  Draft, Cancelled, Void
//   Overdue filter:         DueDate < today AND Status NOT IN
//                           (Paid, PartiallyPaid, Cancelled, Void)
//   MTD filter:             IssueDate.Year == thisYear AND
//                           IssueDate.Month == thisMonth
//
// VAT: VatAmount stored directly on entity — summed directly,
//      no recalculation needed.
//
// THREAD SAFETY: Each method = own DbContext instance (not shared).
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Dashboard.TaxInvoiceDashboard;
using ProGlassAutomation.Services.TaxInvoice.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Dashboard.TaxInvoiceMetrics
{
    /// <summary>
    /// Loads and aggregates Tax Invoice data for the dashboard metrics section.
    /// Uses TaxInvoiceDbContext directly — read-only analytics queries
    /// do not need the full repository layer.
    /// </summary>
    public sealed class TaxInvoiceMetricsService : ITaxInvoiceMetricsService
    {
        // ── Status constant arrays (StringComparer.OrdinalIgnoreCase below) ─

        private static readonly HashSet<string> ExcludedFromRevenue =
            new(StringComparer.OrdinalIgnoreCase) { "Draft", "Cancelled", "Void" };

        private static readonly HashSet<string> ExcludedFromOverdue =
            new(StringComparer.OrdinalIgnoreCase)
            { "Paid", "PartiallyPaid", "Cancelled", "Void" };

        // ═════════════════════════════════════════════════════════════════
        //  TYPED PROJECTION — avoids FullSnapshotJson blob in memory
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Lightweight projection of SavedTaxInvoice for analytics queries.
        /// Only the columns needed by all 5 KPI cards + widgets.
        /// </summary>
        private sealed class InvoiceMetricRow
        {
            public int Id { get; init; }
            public string InvoiceNumber { get; init; } = "";
            public string CustomerName { get; init; } = "";
            public string Status { get; init; } = "";
            public DateTime IssueDate { get; init; }
            public DateTime DueDate { get; init; }
            public decimal GrandTotal { get; init; }
            public decimal VatAmount { get; init; }
            public DateTime CreatedAt { get; init; }
        }

        // ═════════════════════════════════════════════════════════════════
        //  KPI SUMMARY — all 5 cards in one DB round-trip
        // ═════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<TaxInvoiceKpiSummary> GetKpiSummaryAsync()
        {
            var summary = new TaxInvoiceKpiSummary();

            using var db = new TaxInvoiceDbContext();
            await db.Database.EnsureCreatedAsync();

            // ── Single projected query — no JSON blob loaded ──────────────
            var all = await db.TaxInvoices
                .AsNoTracking()
                .Select(t => new InvoiceMetricRow
                {
                    Id = t.Id,
                    InvoiceNumber = t.InvoiceNumber,
                    CustomerName = t.CustomerName,
                    Status = t.Status,
                    IssueDate = t.IssueDate,
                    DueDate = t.DueDate,
                    GrandTotal = t.GrandTotal,
                    VatAmount = t.VatAmount,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            if (all.Count == 0)
                return summary; // All zeros — no invoices yet

            var today = DateTime.Today;
            var thisYear = today.Year;
            var thisMonth = today.Month;
            var firstOfThis = new DateTime(thisYear, thisMonth, 1);
            var firstOfLast = firstOfThis.AddMonths(-1);
            var lastOfLast = firstOfThis.AddDays(-1).Date;

            // ── Revenue slice (excludes Draft, Cancelled, Void) ───────────
            var revenue = all
                .Where(r => !ExcludedFromRevenue.Contains(r.Status))
                .ToList();

            // ── CARD 1: Total Invoiced MTD + Last Month ───────────────────
            summary.TotalInvoicedThisMonth = revenue
                .Where(r => r.IssueDate.Year == thisYear
                          && r.IssueDate.Month == thisMonth)
                .Sum(r => r.GrandTotal);

            summary.TotalInvoicedLastMonth = revenue
                .Where(r => r.IssueDate.Date >= firstOfLast
                          && r.IssueDate.Date <= lastOfLast)
                .Sum(r => r.GrandTotal);

            // ── CARD 2: VAT Collected MTD ─────────────────────────────────
            // VatAmount stored directly — sum it (no 5% recalc needed)
            summary.VatCollectedThisMonth = revenue
                .Where(r => r.IssueDate.Year == thisYear
                          && r.IssueDate.Month == thisMonth)
                .Sum(r => r.VatAmount);

            // ── CARD 3: Overdue Amount + Count ───────────────────────────
            var overdueList = all
                .Where(r => r.DueDate.Date < today
                          && !ExcludedFromOverdue.Contains(r.Status))
                .ToList();

            summary.OverdueAmount = overdueList.Sum(r => r.GrandTotal);
            summary.OverdueCount = overdueList.Count;

            // ── CARD 4: Pending (Draft + Issued) ─────────────────────────
            summary.DraftCount = all.Count(r =>
                r.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase));
            summary.IssuedCount = all.Count(r =>
                r.Status.Equals("Issued", StringComparison.OrdinalIgnoreCase));
            summary.PendingCount = summary.DraftCount + summary.IssuedCount;

            // ── CARD 5: Paid MTD ─────────────────────────────────────────
            // Includes both Paid and PartiallyPaid
            var paidThisMonth = all
                .Where(r =>
                    (r.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase) ||
                     r.Status.Equals("PartiallyPaid", StringComparison.OrdinalIgnoreCase))
                    && r.IssueDate.Year == thisYear
                    && r.IssueDate.Month == thisMonth)
                .ToList();

            summary.PaidThisMonthCount = paidThisMonth.Count;
            summary.PaidThisMonthAmount = paidThisMonth.Sum(r => r.GrandTotal);

            return summary;
        }

        // ═════════════════════════════════════════════════════════════════
        //  RECENT INVOICES — last N by IssueDate DESC
        // ═════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<List<RecentInvoiceItem>> GetRecentInvoicesAsync(int limit = 5)
        {
            using var db = new TaxInvoiceDbContext();
            await db.Database.EnsureCreatedAsync();

            var rows = await db.TaxInvoices
                .AsNoTracking()
                .OrderByDescending(t => t.IssueDate)
                .ThenByDescending(t => t.CreatedAt)
                .Take(limit)
                .Select(t => new InvoiceMetricRow
                {
                    Id = t.Id,
                    InvoiceNumber = t.InvoiceNumber,
                    CustomerName = t.CustomerName,
                    Status = t.Status,
                    IssueDate = t.IssueDate,
                    DueDate = t.DueDate,
                    GrandTotal = t.GrandTotal,
                    VatAmount = t.VatAmount,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            return rows
                .Select(r => new RecentInvoiceItem
                {
                    TaxInvoiceId = r.Id,
                    InvoiceNumber = r.InvoiceNumber,
                    CustomerName = r.CustomerName,
                    IssueDate = r.IssueDate,
                    DueDate = r.DueDate,
                    GrandTotal = r.GrandTotal,
                    // Resolve display status: Issued + past due → Overdue
                    Status = ResolveDisplayStatus(r.Status, r.DueDate)
                })
                .ToList();
        }

        // ═════════════════════════════════════════════════════════════════
        //  AGING REPORT — 4 overdue brackets
        // ═════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<List<AgingBracket>> GetAgingReportAsync()
        {
            using var db = new TaxInvoiceDbContext();
            await db.Database.EnsureCreatedAsync();

            var today = DateTime.Today;

            // Load all invoices whose DueDate is past
            // (Status filter done in-memory for OrdinalIgnoreCase safety)
            var all = await db.TaxInvoices
                .AsNoTracking()
                .Where(t => t.DueDate < today)
                .Select(t => new
                {
                    t.Status,
                    t.DueDate,
                    t.GrandTotal
                })
                .ToListAsync();

            // Apply status filter in-memory
            var overdue = all
                .Where(r => !ExcludedFromOverdue.Contains(r.Status))
                .Select(r => new
                {
                    DaysOverdue = (today - r.DueDate.Date).Days,
                    r.GrandTotal
                })
                .ToList();

            // ── Build 4 standard aging brackets ──────────────────────────
            AgingBracket MakeBracket(string label, int from, int to)
            {
                var items = overdue
                    .Where(r => r.DaysOverdue >= from
                              && (to == int.MaxValue || r.DaysOverdue <= to))
                    .ToList();

                return new AgingBracket
                {
                    Label = label,
                    DaysFrom = from,
                    DaysTo = to == int.MaxValue ? 9999 : to,
                    Amount = items.Sum(r => r.GrandTotal),
                    InvoiceCount = items.Count
                    // NormalizedWidth → set by TiDashboardViewModel after return
                };
            }

            return new List<AgingBracket>
            {
                MakeBracket("0-30 days",   0,  30),
                MakeBracket("31-60 days", 31,  60),
                MakeBracket("61-90 days", 61,  90),
                MakeBracket("90+ days",   91,  int.MaxValue)
            };
        }

        // ═════════════════════════════════════════════════════════════════
        //  TOP CUSTOMERS — ranked by total invoiced amount
        // ═════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<List<TopCustomerItem>> GetTopCustomersAsync(
            int monthsBack = 12, int limit = 5)
        {
            using var db = new TaxInvoiceDbContext();
            await db.Database.EnsureCreatedAsync();

            // Rolling window cutoff — e.g. 12 months ago from today
            var cutoff = DateTime.Today.AddMonths(-monthsBack);

            var rows = await db.TaxInvoices
                .AsNoTracking()
                .Where(t => t.IssueDate >= cutoff)
                .Select(t => new
                {
                    t.CustomerName,
                    t.Status,
                    t.GrandTotal
                })
                .ToListAsync();

            // Group in-memory (status filter + case-insensitive customer name)
            var grouped = rows
                .Where(r => !ExcludedFromRevenue.Contains(r.Status))
                .GroupBy(r => (r.CustomerName ?? "").Trim(),
                         StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    CustomerName = g.Key,
                    TotalAmount = g.Sum(r => r.GrandTotal),
                    InvoiceCount = g.Count()
                })
                .OrderByDescending(g => g.TotalAmount)
                .Take(limit)
                .ToList();

            // Build result with 1-based rank
            // NormalizedWidth set by ViewModel after return
            return grouped
                .Select((g, idx) => new TopCustomerItem
                {
                    Rank = idx + 1,
                    CustomerName = string.IsNullOrWhiteSpace(g.CustomerName)
                                   ? "(No Name)" : g.CustomerName,
                    TotalAmount = g.TotalAmount,
                    InvoiceCount = g.InvoiceCount
                })
                .ToList();
        }

        // ═════════════════════════════════════════════════════════════════
        //  MONTHLY REVENUE TREND — last N months bar chart
        // ═════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public async Task<List<MonthlyRevenuePoint>> GetMonthlyRevenueTrendAsync(
            int monthsBack = 6)
        {
            using var db = new TaxInvoiceDbContext();
            await db.Database.EnsureCreatedAsync();

            var now = DateTime.Now;

            // Build ordered month list: oldest → newest
            // e.g. monthsBack=6: [Feb, Mar, Apr, May, Jun, Jul] if now = Jul
            var months = Enumerable.Range(0, monthsBack)
                .Select(i => now.AddMonths(-(monthsBack - 1) + i))
                .Select(m => new DateTime(m.Year, m.Month, 1))
                .ToList();

            var cutoff = months.First(); // First day of oldest month

            // Load revenue invoices in the window — projected, no JSON blob
            var rows = await db.TaxInvoices
                .AsNoTracking()
                .Where(t => t.IssueDate >= cutoff)
                .Select(t => new
                {
                    t.Status,
                    t.IssueDate,
                    t.GrandTotal,
                    t.VatAmount
                })
                .ToListAsync();

            // Status filter in-memory
            var revenue = rows
                .Where(r => !ExcludedFromRevenue.Contains(r.Status))
                .ToList();

            // Build one MonthlyRevenuePoint per month
            // NormalizedHeight + BarColor + IsHighlight → set by ViewModel
            var points = months
                .Select(m =>
                {
                    var monthData = revenue
                        .Where(r => r.IssueDate.Year == m.Year
                                 && r.IssueDate.Month == m.Month)
                        .ToList();

                    return new MonthlyRevenuePoint
                    {
                        Month = m,
                        TotalInvoiced = monthData.Sum(r => r.GrandTotal),
                        VatCollected = monthData.Sum(r => r.VatAmount)
                    };
                })
                .ToList();

            return points;
        }

        // ═════════════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Resolves the status shown in the Recent Invoices widget.
        /// An Issued invoice whose DueDate has passed is displayed as Overdue
        /// (matching the visual indicator logic in the list view).
        /// The stored DB status is NOT changed — display only.
        /// </summary>
        private static string ResolveDisplayStatus(string storedStatus, DateTime dueDate)
        {
            if (storedStatus.Equals("Issued", StringComparison.OrdinalIgnoreCase)
                && dueDate.Date < DateTime.Today)
            {
                return "Overdue";
            }

            return storedStatus;
        }
    }
}