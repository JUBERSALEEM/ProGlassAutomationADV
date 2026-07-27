// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Dashboard/TaxInvoiceDashboard/AgingBracket.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Models.Dashboard.TaxInvoiceDashboard
//
// PURPOSE:
//   One bracket in the Invoice Aging widget.
//   Aging = how many days an invoice is PAST its due date.
//
// BRACKETS (standard UAE/GCC accounts receivable convention):
//   0–30   days overdue  → Current (yellow — watch closely)
//   31–60  days overdue  → Moderate (amber — follow up)
//   61–90  days overdue  → Serious  (orange — escalate)
//   90+    days overdue  → Critical (red — legal/write-off risk)
//
// BAR CHART:
//   NormalizedWidth (0.0–1.0) drives the bar fill width in XAML.
//   Calculated by TiDashboardViewModel after all 4 brackets are known.
//   PercentToWidthConverter (already in App.xaml) scales it to pixels.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Models.Dashboard.TaxInvoiceDashboard
{
    /// <summary>
    /// One aging bracket in the Invoice Aging report widget.
    /// Four instances are created per load: 0-30, 31-60, 61-90, 90+.
    /// </summary>
    public sealed class AgingBracket
    {
        // ── Identity ──────────────────────────────────────────────────────

        /// <summary>
        /// Display label for the bracket.
        /// Values: "0-30 days", "31-60 days", "61-90 days", "90+ days"
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Lower bound (inclusive) in days overdue.
        /// 0 | 31 | 61 | 91
        /// </summary>
        public int DaysFrom { get; set; }

        /// <summary>
        /// Upper bound (inclusive) in days overdue.
        /// 30 | 60 | 90 | int.MaxValue (for 90+ bracket)
        /// </summary>
        public int DaysTo { get; set; }

        // ── Aggregates ────────────────────────────────────────────────────

        /// <summary>
        /// Sum of GrandTotal for all overdue invoices in this bracket. AED.
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Count of overdue invoices in this bracket.
        /// </summary>
        public int InvoiceCount { get; set; }

        // ── Chart rendering ───────────────────────────────────────────────

        /// <summary>
        /// Normalised bar width (0.0 to 1.0).
        /// Set by TiDashboardViewModel after computing max(Amount) across brackets.
        /// XAML: Width="{Binding NormalizedWidth, Converter={StaticResource
        ///        PercentToWidthConverter}, ConverterParameter=280}"
        /// </summary>
        public double NormalizedWidth { get; set; }

        // ── Display helpers ───────────────────────────────────────────────

        /// <summary>"AED 15,200.00" or "—" when zero.</summary>
        public string AmountDisplay =>
            Amount > 0 ? $"AED {Amount:N2}" : "—";

        /// <summary>"3 invoices" or "1 invoice" (grammatically correct).</summary>
        public string CountDisplay =>
            InvoiceCount == 1 ? "1 invoice" : $"{InvoiceCount} invoices";

        /// <summary>
        /// Severity color brush key — escalates with age.
        /// Used by BrushKeyConverter for the bar fill and label color.
        /// </summary>
        public string SeverityColor => Label switch
        {
            "0-30 days" => "WarningBrush",
            "31-60 days" => "WarningBrush",
            "61-90 days" => "DangerBrush",
            "90+ days" => "DangerBrush",
            _ => "NeutralBrush"
        };

        /// <summary>
        /// Tint brush key for the row background.
        /// </summary>
        public string TintColor => Label switch
        {
            "0-30 days" => "TintAmberBrush",
            "31-60 days" => "TintAmberBrush",
            "61-90 days" => "TintRedBrush",
            "90+ days" => "TintRedBrush",
            _ => "TintGrayBrush"
        };

        /// <summary>True when this bracket has at least one overdue invoice.</summary>
        public bool HasData => InvoiceCount > 0;
    }
}