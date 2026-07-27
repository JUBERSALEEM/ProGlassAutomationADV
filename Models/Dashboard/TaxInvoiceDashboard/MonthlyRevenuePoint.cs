// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Dashboard/TaxInvoiceDashboard/MonthlyRevenuePoint.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Models.Dashboard.TaxInvoiceDashboard
//
// FIX (Chat 15 hotfix):
//   Added explicit 'public' modifier to sealed class.
//   'sealed class' without 'public' defaults to 'internal' in C#.
//   Internal class cannot be used as a public property type or
//   public method return type — causes CS0050 + CS0053 + CS1061.
//
//   ALL other properties were correct — only the class access modifier
//   was missing.
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.Models.Dashboard.TaxInvoiceDashboard
{
    /// <summary>
    /// One month's revenue data point for the Tax Invoice trend bar chart.
    /// Designed to bind directly to the same bar chart XAML template
    /// used in OverviewDashboardView (NormalizedHeight + ScaleConverter).
    /// </summary>
    public sealed class MonthlyRevenuePoint   // ← 'public' was missing before
    {
        // ── Time identity ─────────────────────────────────────────────────

        /// <summary>
        /// The calendar month this point represents.
        /// Day component is always 1 (first of the month).
        /// </summary>
        public DateTime Month { get; set; }

        // ── Financials ────────────────────────────────────────────────────

        /// <summary>
        /// Total invoiced amount (GrandTotal sum) for this month. AED.
        /// Excludes Draft, Cancelled, Void invoices.
        /// </summary>
        public decimal TotalInvoiced { get; set; }

        /// <summary>
        /// UAE 5% VAT component for this month.
        /// = Sum of VatAmount for all revenue invoices in the month.
        /// Displayed as tooltip sub-label.
        /// </summary>
        public decimal VatCollected { get; set; }

        // ── Chart rendering ───────────────────────────────────────────────

        /// <summary>
        /// Normalised height (0.0 to 1.0) relative to the highest month.
        /// Highest month = 1.0. Months with zero data = 0.04 (stub bar).
        /// Set by TiDashboardViewModel after computing max(TotalInvoiced).
        /// XAML: Height="{Binding NormalizedHeight,
        ///        Converter={StaticResource ScaleConverter},
        ///        ConverterParameter=140}"
        /// </summary>
        public double NormalizedHeight { get; set; }

        /// <summary>
        /// True when this is the highest-revenue month in the 6-month window.
        /// Drives highlight bar color: emerald (#10B981) vs indigo (#6366F1).
        /// </summary>
        public bool IsHighlight { get; set; }

        /// <summary>
        /// True when this month is the current calendar month.
        /// Available for additional visual emphasis if needed.
        /// </summary>
        public bool IsCurrentMonth =>
            Month.Year == DateTime.Today.Year
            && Month.Month == DateTime.Today.Month;

        /// <summary>
        /// Bar fill color hex string.
        /// Emerald (#10B981) for highlight month, Indigo (#6366F1) for others.
        /// Resolved by ColorStringConverter in XAML.
        /// Set by TiDashboardViewModel.NormalizeRevenueBars().
        /// </summary>
        public string BarColor { get; set; } = "#6366F1";

        // ── Display helpers ───────────────────────────────────────────────

        /// <summary>X-axis label: "Jan", "Feb", … "Dec"</summary>
        public string Label => Month.ToString("MMM");

        /// <summary>
        /// Bar top value label: "AED 45,678" or "—" when zero.
        /// </summary>
        public string ValueDisplay =>
            TotalInvoiced > 0 ? $"AED {TotalInvoiced:N0}" : "—";

        /// <summary>
        /// Tooltip text shown on bar hover.
        /// "Jan 2026 · AED 45,678.00 invoiced · AED 2,170.38 VAT"
        /// </summary>
        public string TooltipText =>
            TotalInvoiced > 0
                ? $"{Month:MMM yyyy} · AED {TotalInvoiced:N2} invoiced · AED {VatCollected:N2} VAT"
                : $"{Month:MMM yyyy} · No invoices";
    }
}