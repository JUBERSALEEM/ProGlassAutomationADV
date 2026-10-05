// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Dashboard/TaxInvoiceDashboard/RecentInvoiceItem.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Models.Dashboard.TaxInvoiceDashboard
//
// PURPOSE:
//   One row in the "Recent Tax Invoices" widget (last 5 invoices).
//   Mirrors the ActivityFeedItem pattern used in OverviewDashboardViewModel
//   so the XAML DataTemplate can reuse the same structural approach.
//
// DISPLAY:
//   INV-2026-001-A · ssss · 27 Jul 2026 · AED 3,912.82  ✅ PAID
//   INV-2026-001   · tee  · 25 Jul 2026 · AED 12,450    📤 ISSUED
//
// NAVIGATION:
//   TaxInvoiceId is passed to TaxInvoiceViewModel to open detail view.
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.Models.Dashboard.TaxInvoiceDashboard
{
    /// <summary>
    /// Display model for one row in the Recent Tax Invoices widget.
    /// </summary>
    public sealed class RecentInvoiceItem
    {
        // ── Identity ──────────────────────────────────────────────────────

        /// <summary>Primary key — used for click-to-navigate.</summary>
        public int TaxInvoiceId { get; set; }

        /// <summary>
        /// Human-readable invoice number.
        /// Format: INV-YYYY-NNN (master) or INV-YYYY-NNN-A (partial).
        /// </summary>
        public string InvoiceNumber { get; set; } = string.Empty;

        // ── Customer ──────────────────────────────────────────────────────

        /// <summary>Customer display name (may be truncated in UI).</summary>
        public string CustomerName { get; set; } = string.Empty;

        // ── Dates ─────────────────────────────────────────────────────────

        /// <summary>Invoice issue date — shown as "27 Jul 2026".</summary>
        public DateTime IssueDate { get; set; }

        /// <summary>Payment due date — used to detect overdue state.</summary>
        public DateTime? DueDate { get; set; }

        // ── Financial ─────────────────────────────────────────────────────

        /// <summary>Grand total including 5% UAE VAT. Display in AED.</summary>
        public decimal GrandTotal { get; set; }

        // ── Status ────────────────────────────────────────────────────────

        /// <summary>
        /// Raw status string: Draft | Issued | Paid | Overdue | Cancelled | Void.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        // ── Computed display helpers ───────────────────────────────────────

        /// <summary>
        /// Status emoji icon for the badge column.
        /// ✅ Paid · 📤 Issued · ⚠️ Overdue · 📝 Draft · ❌ Cancelled
        /// </summary>
        public string StatusIcon => Status?.ToLowerInvariant() switch
        {
            "paid" => "✅",
            "issued" => "📤",
            "overdue" => "⚠️",
            "draft" => "📝",
            "cancelled" => "❌",
            "void" => "🚫",
            _ => "📄"
        };

        /// <summary>
        /// Theme-aware brush key for the status badge background.
        /// Resolved by BrushKeyConverter in XAML.
        /// </summary>
        public string StatusColor => Status?.ToLowerInvariant() switch
        {
            "paid" => "SuccessBrush",
            "issued" => "InfoBrush",
            "overdue" => "DangerBrush",
            "draft" => "NeutralBrush",
            "cancelled" => "NeutralBrush",
            "void" => "NeutralBrush",
            _ => "NeutralBrush"
        };

        /// <summary>
        /// Theme-aware tint brush key for the row background.
        /// </summary>
        public string TintColor => Status?.ToLowerInvariant() switch
        {
            "paid" => "TintGreenBrush",
            "issued" => "TintBlueBrush",
            "overdue" => "TintRedBrush",
            "draft" => "TintGrayBrush",
            "cancelled" => "TintGrayBrush",
            "void" => "TintGrayBrush",
            _ => "TintGrayBrush"
        };

        /// <summary>
        /// AED formatted amount for display: "AED 3,912.82"
        /// </summary>
        public string AmountDisplay => $"AED {GrandTotal:N2}";

        /// <summary>
        /// Issue date formatted for the row: "27 Jul 2026"
        /// </summary>
        public string IssueDateDisplay => IssueDate.ToString("dd MMM yyyy");

        /// <summary>
        /// True when this invoice is past its due date and not paid/cancelled.
        /// </summary>
        public bool IsOverdue =>
            DueDate.HasValue
            && DueDate.Value.Date < DateTime.Today
            && !string.Equals(Status, "Paid", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Status, "Void", StringComparison.OrdinalIgnoreCase);
    }
}