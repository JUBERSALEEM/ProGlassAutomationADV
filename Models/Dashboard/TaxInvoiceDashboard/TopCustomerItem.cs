// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Dashboard/TaxInvoiceDashboard/TopCustomerItem.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Models.Dashboard.TaxInvoiceDashboard
//
// PURPOSE:
//   One row in the "Top 5 Customers" widget (last 12 months).
//   Ranked by total invoiced amount descending.
//
// BUSINESS RULE:
//   Only invoices with Status != Draft/Cancelled/Void count toward ranking.
//   Time window: IssueDate >= 12 months ago (rolling, not calendar year).
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Models.Dashboard.TaxInvoiceDashboard
{
    /// <summary>
    /// One customer entry in the Top Customers by invoiced amount widget.
    /// </summary>
    public sealed class TopCustomerItem
    {
        // ── Identity ──────────────────────────────────────────────────────

        /// <summary>Customer display name.</summary>
        public string CustomerName { get; set; } = string.Empty;

        /// <summary>
        /// 1-based rank position (1 = highest revenue customer).
        /// Shown as "1.", "2.", etc. in the widget.
        /// </summary>
        public int Rank { get; set; }

        // ── Aggregates ────────────────────────────────────────────────────

        /// <summary>
        /// Total invoiced amount (GrandTotal sum) for this customer
        /// over the selected period. AED.
        /// </summary>
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// Number of invoices raised for this customer in the period.
        /// Shown as sub-text: "X invoices".
        /// </summary>
        public int InvoiceCount { get; set; }

        // ── Chart ─────────────────────────────────────────────────────────

        /// <summary>
        /// Normalised bar width (0.0 to 1.0) relative to rank-1 customer.
        /// Rank 1 always gets 1.0; others are proportional.
        /// Set by TiDashboardViewModel after all 5 items are loaded.
        /// </summary>
        public double NormalizedWidth { get; set; }

        // ── Display helpers ───────────────────────────────────────────────

        /// <summary>"AED 145,678.00"</summary>
        public string AmountDisplay => $"AED {TotalAmount:N2}";

        /// <summary>"3 invoices" / "1 invoice"</summary>
        public string InvoiceCountDisplay =>
            InvoiceCount == 1 ? "1 invoice" : $"{InvoiceCount} invoices";

        /// <summary>
        /// Medal emoji for top 3 ranks. Empty string for ranks 4-5.
        /// Rank 1 = 🥇, Rank 2 = 🥈, Rank 3 = 🥉
        /// </summary>
        public string MedalIcon => Rank switch
        {
            1 => "🥇",
            2 => "🥈",
            3 => "🥉",
            _ => string.Empty
        };

        /// <summary>
        /// Bar color — gold for rank 1, silver for rank 2,
        /// bronze for rank 3, indigo for ranks 4-5.
        /// Used by ColorStringConverter in XAML.
        /// </summary>
        public string BarColor => Rank switch
        {
            1 => "#F59E0B",  // Gold
            2 => "#94A3B8",  // Silver
            3 => "#B45309",  // Bronze
            _ => "#6366F1"   // Indigo
        };
    }
}