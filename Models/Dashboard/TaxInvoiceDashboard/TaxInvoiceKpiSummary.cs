// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Dashboard/TaxInvoiceDashboard/TaxInvoiceKpiSummary.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Models.Dashboard.TaxInvoiceDashboard
//
// PURPOSE:
//   Flat data transfer object returned by TaxInvoiceMetricsService.
//   All 5 KPI hero cards on the Tax Invoice dashboard tab read from
//   this single object — one DB round-trip populates everything.
//
// BUSINESS LOGIC:
//   MTD  = Month To Date (from 1st of current calendar month to today)
//   Overdue = DueDate < today AND Status NOT IN (Paid, Cancelled, Void)
//   Pending = Status IN (Draft, Issued)
//   Paid MTD = Status = Paid AND PaidDate in current month
//
// CURRENCY: AED only (UAE). All decimal fields are AED amounts.
// VAT: UAE standard rate 5%. VatCollectedThisMonth = TotalInvoiced * 0.05
//      (already embedded in GrandTotal — extracted for display only)
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Models.Dashboard.TaxInvoiceDashboard
{
    /// <summary>
    /// Aggregated KPI summary for the Tax Invoice dashboard section.
    /// Populated by <see cref="Services.Dashboard.TaxInvoiceMetrics.TaxInvoiceMetricsService"/>.
    /// All monetary values are in AED.
    /// </summary>
    public sealed class TaxInvoiceKpiSummary
    {
        // ── CARD 1: Total Invoiced (MTD) ──────────────────────────────────

        /// <summary>
        /// Sum of GrandTotal for all non-Draft, non-Cancelled, non-Void
        /// invoices whose IssueDate falls in the current calendar month.
        /// </summary>
        public decimal TotalInvoicedThisMonth { get; set; }

        /// <summary>
        /// Same calculation for the previous calendar month.
        /// Used to compute month-over-month % change for the sub-text arrow.
        /// </summary>
        public decimal TotalInvoicedLastMonth { get; set; }

        /// <summary>
        /// Percentage change vs last month.
        /// Positive = growth (green arrow ↑), Negative = decline (red arrow ↓).
        /// Null when last month was zero (avoids divide-by-zero).
        /// </summary>
        public decimal? MonthOverMonthPct
        {
            get
            {
                if (TotalInvoicedLastMonth == 0m) return null;
                return ((TotalInvoicedThisMonth - TotalInvoicedLastMonth)
                        / TotalInvoicedLastMonth) * 100m;
            }
        }

        // ── CARD 2: VAT Collected (MTD) ───────────────────────────────────

        /// <summary>
        /// UAE 5% VAT collected this month.
        /// Derived from TotalInvoicedThisMonth (VAT portion = Total * 5/105).
        /// Displayed separately for FTA reporting awareness.
        /// </summary>
        public decimal VatCollectedThisMonth { get; set; }

        // ── CARD 3: Overdue Amount ────────────────────────────────────────

        /// <summary>
        /// Sum of GrandTotal for invoices where DueDate &lt; today
        /// AND Status is NOT Paid, Cancelled, or Void.
        /// </summary>
        public decimal OverdueAmount { get; set; }

        /// <summary>
        /// Count of overdue invoices (same filter as OverdueAmount).
        /// Shown in sub-text: "X invoices past due date".
        /// </summary>
        public int OverdueCount { get; set; }

        // ── CARD 4: Pending Invoices ──────────────────────────────────────

        /// <summary>
        /// Total count of invoices in Draft or Issued status.
        /// The hero number on Card 4.
        /// </summary>
        public int PendingCount { get; set; }

        /// <summary>Draft invoice count (breakdown for sub-text).</summary>
        public int DraftCount { get; set; }

        /// <summary>Issued invoice count (breakdown for sub-text).</summary>
        public int IssuedCount { get; set; }

        // ── CARD 5: Paid (MTD) ────────────────────────────────────────────

        /// <summary>
        /// Count of invoices marked Paid whose IssueDate (or PaidDate
        /// if available) falls in the current calendar month.
        /// </summary>
        public int PaidThisMonthCount { get; set; }

        /// <summary>
        /// Sum of GrandTotal for Paid invoices this month.
        /// Shown in sub-text: "AED X,XXX collected".
        /// </summary>
        public decimal PaidThisMonthAmount { get; set; }

        // ── Overdue alert banner ──────────────────────────────────────────

        /// <summary>
        /// True when OverdueCount > 0.
        /// Drives the conditional red banner visibility in the View.
        /// </summary>
        public bool HasOverdueInvoices => OverdueCount > 0;
    }
}