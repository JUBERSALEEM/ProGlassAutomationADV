// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Statement/Reconciliation/ReconciliationModels.cs
// PROJECT: ProGlassAutomation
// MODULE: Order Fulfillment Reconciliation (Chat 22 — Phase 1 Backend)
//
// PURPOSE:
//   Data models for cross-referencing PI ordered quantities against
//   DN delivered quantities and TI invoiced quantities.
//   Catches the scenario: "ordered 2, delivered 1, invoiced 2"
//   which means OVER-INVOICED — billed for more than delivered.
//
// CONTAINS:
//   • ReconciliationStatus     — 6-state enum with display metadata
//   • OrderReconciliationRow   — per-line-item reconciliation
//   • OrderReconciliationSummary — per-PI aggregate with rollup flags
//
// DESIGN:
//   All classes are pure POCOs — JSON-serializable by System.Text.Json.
//   Computed get-only properties are safe: STJ ignores them on deserialize.
//   When embedded in StatementInvoiceRow (Chat 23), they persist
//   automatically inside FullSnapshotJson.
//
// KEY TUPLE:
//   Line items are matched across PI/DN/TI by composite key:
//     (SpecGroupNumber, SerialNumber)
//   This key is stable across the entire lifecycle:
//     PI creates it → JO copies it → DN copies it → TI copies it
//   Format in repository maps: "specGroupNumber|serialNumber"
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Utilities;

namespace ProGlassAutomation.Models.Statement.Reconciliation
{
    // ═════════════════════════════════════════════════════════════════════
    //  RECONCILIATION STATUS ENUM
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Reconciliation outcome for a single line item.
    /// Determined by comparing Ordered vs Delivered vs Invoiced quantities.
    /// </summary>
    public enum ReconciliationStatus
    {
        /// <summary>
        /// ✅ Green — Delivered == Invoiced == Ordered.
        /// Everything matches perfectly. No action needed.
        /// </summary>
        FullyMatched,

        /// <summary>
        /// 🟡 Amber — Delivered less than Ordered, but Invoiced matches Delivered.
        /// Delivery is pending but billing is correct for what was shipped.
        /// </summary>
        PartialDelivery,

        /// <summary>
        /// 🔴 Red — Invoiced MORE than Delivered.
        /// ★ THE CRITICAL BUG SCENARIO. Customer billed for items not yet shipped.
        /// Requires immediate investigation: either DN is missing or TI is wrong.
        /// </summary>
        OverInvoiced,

        /// <summary>
        /// 🔵 Blue — Delivered but NOT yet fully invoiced.
        /// Items shipped to customer but not yet billed. Revenue leakage risk.
        /// Action: Create Tax Invoice for unbilled deliveries.
        /// </summary>
        UnderInvoiced,

        /// <summary>
        /// ⚪ Gray — Nothing delivered, nothing invoiced.
        /// Order exists in PI but production/delivery hasn't started.
        /// </summary>
        NotYetStarted,

        /// <summary>
        /// 🟠 Orange — Delivered MORE than originally ordered.
        /// Rare: typically means rework/replacement delivery.
        /// Not necessarily an error but worth flagging.
        /// </summary>
        OverDelivered
    }

    // ═════════════════════════════════════════════════════════════════════
    //  STATUS DISPLAY HELPERS (extension methods)
    // ═════════════════════════════════════════════════════════════════════

    public static class ReconciliationStatusExtensions
    {
        public static string ToLabel(this ReconciliationStatus status) => status switch
        {
            ReconciliationStatus.FullyMatched => "Fully Matched",
            ReconciliationStatus.PartialDelivery => "Partial Delivery",
            ReconciliationStatus.OverInvoiced => "Over-Invoiced",
            ReconciliationStatus.UnderInvoiced => "Under-Invoiced",
            ReconciliationStatus.NotYetStarted => "Not Started",
            ReconciliationStatus.OverDelivered => "Over-Delivered",
            _ => "Unknown"
        };

        public static string ToColorHex(this ReconciliationStatus status) => status switch
        {
            ReconciliationStatus.FullyMatched => "#047857",  // Green
            ReconciliationStatus.PartialDelivery => "#B45309",  // Amber
            ReconciliationStatus.OverInvoiced => "#B91C1C",  // Red
            ReconciliationStatus.UnderInvoiced => "#1D4ED8",  // Blue
            ReconciliationStatus.NotYetStarted => "#6B7280",  // Gray
            ReconciliationStatus.OverDelivered => "#C2410C",  // Orange
            _ => "#6B7280"
        };

        public static string ToIcon(this ReconciliationStatus status) => status switch
        {
            ReconciliationStatus.FullyMatched => "✅",
            ReconciliationStatus.PartialDelivery => "🟡",
            ReconciliationStatus.OverInvoiced => "🔴",
            ReconciliationStatus.UnderInvoiced => "🔵",
            ReconciliationStatus.NotYetStarted => "⚪",
            ReconciliationStatus.OverDelivered => "🟠",
            _ => "❓"
        };

        public static string ToActionLabel(this ReconciliationStatus status) => status switch
        {
            ReconciliationStatus.FullyMatched => "No action needed",
            ReconciliationStatus.PartialDelivery => "Await remaining delivery",
            ReconciliationStatus.OverInvoiced => "⚠ Investigate — invoiced more than delivered",
            ReconciliationStatus.UnderInvoiced => "Create invoice for unbilled delivery",
            ReconciliationStatus.NotYetStarted => "Pending production/delivery",
            ReconciliationStatus.OverDelivered => "Review — delivered more than ordered",
            _ => ""
        };
    }

    // ═════════════════════════════════════════════════════════════════════
    //  ORDER RECONCILIATION ROW (per line item)
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Reconciliation data for ONE glass line item across PI/DN/TI.
    /// Identified by the composite key (SpecGroupNumber, SerialNumber).
    ///
    /// Example row (the user's bug scenario):
    ///   GlassRef = "A1"
    ///   OrderedQty = 2  (from PI)
    ///   DeliveredQty = 1  (from DN-001)
    ///   BalanceQty = 1  (pending delivery)
    ///   InvoicedQty = 2  (from INV-001)
    ///   InvoiceVsDeliveryDiff = +1  ← OVER-INVOICED!
    ///   Status = OverInvoiced (RED)
    /// </summary>
    public class OrderReconciliationRow
    {
        // ── Line item identity (composite key) ───────────────────────────

        /// <summary>Parent PI reference (e.g. "PI-2026-001").</summary>
        public string ProformaId { get; set; } = "";

        /// <summary>Spec group index within the PI (1, 2, 3...).</summary>
        public int SpecGroupNumber { get; set; }

        /// <summary>Spec group name (e.g. "6mm Clear Tempered").</summary>
        public string SpecGroupName { get; set; } = "";

        /// <summary>Row index within the spec group (1, 2, 3...).</summary>
        public int SerialNumber { get; set; }

        /// <summary>Glass reference code (e.g. "A1", "B3").</summary>
        public string GlassRef { get; set; } = "";

        /// <summary>Panel dimensions for display (e.g. "2400 × 1200 mm").</summary>
        public string DimensionDisplay { get; set; } = "";

        // ── Raw dimensions + area (for the full spec / row view) ──────────

        /// <summary>Width of the primary panel (mm).</summary>
        public int W1 { get; set; }

        /// <summary>Height of the primary panel (mm).</summary>
        public int H1 { get; set; }

        /// <summary>Width of the secondary panel (mm) — 0 for rectangles.</summary>
        public int W2 { get; set; }

        /// <summary>Height of the secondary panel (mm) — 0 for rectangles.</summary>
        public int H2 { get; set; }

        /// <summary>Unit panel area (m²) — SqmCalculator, min 0.5 m² rule.</summary>
        public decimal UnitSqm => SqmCalculator.CalculatePanel(W1, H1);

        /// <summary>Total area for this line (unit sqm × ordered qty).</summary>
        public decimal TotalSqm => SqmCalculator.CalculateTotal(W1, H1, OrderedQty);

        /// <summary>Compact raw dimensions "W1×H1" or "W1×H1 / W2×H2".</summary>
        public string RawDimsDisplay
        {
            get
            {
                if (W1 <= 0 && H1 <= 0) return "—";
                if ((W2 <= 0 || W2 == W1) && (H2 <= 0 || H2 == H1))
                    return $"{W1}×{H1}";
                return $"{W1}×{H1} / {W2}×{H2}";
            }
        }

        // ── The 5 quantities (the core of reconciliation) ────────────────

        /// <summary>
        /// Quantity ordered on the Proforma Invoice.
        /// SOURCE: PI snapshot → SpecGroups[N].ItemRows[M].Qty
        /// This is the AUTHORITATIVE "what was ordered" number.
        /// </summary>
        public int OrderedQty { get; set; }

        /// <summary>
        /// Total quantity delivered across ALL confirmed Delivery Notes.
        /// SOURCE: SUM(DeliveredQty) from DeliveryNoteItems grouped by
        ///         (SpecGroupNumber, SerialNumber) for matching JO.
        /// Excludes Draft, Dummy, and Master DNs.
        /// </summary>
        public int DeliveredQty { get; set; }

        /// <summary>
        /// Remaining quantity pending delivery.
        /// FORMULA: MAX(0, OrderedQty - DeliveredQty)
        /// </summary>
        public int BalanceQty { get; set; }

        /// <summary>
        /// Total quantity invoiced across ALL non-cancelled Tax Invoices.
        /// SOURCE: SUM(Qty) from TaxInvoiceItems grouped by
        ///         (SpecGroupNumber, SerialNumber) for matching ProformaId.
        /// Excludes Cancelled, Void, and Draft TIs.
        /// </summary>
        public int InvoicedQty { get; set; }

        /// <summary>
        /// Difference between invoiced and delivered.
        /// FORMULA: InvoicedQty - DeliveredQty
        /// POSITIVE = over-invoiced (billed more than shipped) ← THE BUG
        /// NEGATIVE = under-invoiced (shipped more than billed)
        /// ZERO = invoiced matches delivered (correct)
        /// </summary>
        public int InvoiceVsDeliveryDiff { get; set; }

        // ── Status ───────────────────────────────────────────────────────

        /// <summary>Computed reconciliation status for this line item.</summary>
        public ReconciliationStatus Status { get; set; }

        // ── Display helpers (computed, not persisted on deserialize) ──────

        /// <summary>Human-readable status label.</summary>
        public string StatusLabel => Status.ToLabel();

        /// <summary>Hex color for UI rendering.</summary>
        public string StatusColorHex => Status.ToColorHex();

        /// <summary>Emoji icon for log/display.</summary>
        public string StatusIcon => Status.ToIcon();

        /// <summary>Recommended action text.</summary>
        public string ActionLabel => Status.ToActionLabel();

        /// <summary>
        /// Composite lookup key matching DN repository format.
        /// Used internally for matching across tables.
        /// </summary>
        public string CompositeKey => $"{SpecGroupNumber}|{SerialNumber}";

        /// <summary>True when this row has a billing discrepancy.</summary>
        public bool HasDiscrepancy =>
            Status == ReconciliationStatus.OverInvoiced ||
            Status == ReconciliationStatus.UnderInvoiced;

        /// <summary>
        /// Short summary for compact display.
        /// e.g. "Ordered: 2, Delivered: 1, Invoiced: 2 → Over-Invoiced"
        /// </summary>
        public string CompactSummary =>
            $"O:{OrderedQty}  D:{DeliveredQty}  I:{InvoicedQty}  →  {StatusLabel}";
    }

    // ═════════════════════════════════════════════════════════════════════
    //  ORDER RECONCILIATION SUMMARY (per Proforma Invoice)
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aggregate reconciliation for one Proforma Invoice family.
    /// Contains all line-item rows plus rollup statistics.
    ///
    /// Built by OrderReconciliationService.BuildForProformaAsync().
    /// Cached per-PI within a statement generation run.
    /// JSON-serializable for persistence in FullSnapshotJson.
    /// </summary>
    public class OrderReconciliationSummary
    {
        // ── Identity ─────────────────────────────────────────────────────

        /// <summary>Proforma Invoice ID this reconciliation covers.</summary>
        public string ProformaId { get; set; } = "";

        /// <summary>Job Order ID(s) linked to this PI. Comma-separated if multiple.</summary>
        public string JobOrderIds { get; set; } = "";

        // ── Data availability flags ──────────────────────────────────────

        /// <summary>
        /// True when the PI entity was found and its snapshot deserialized.
        /// False means PI was deleted or has empty/invalid JSON —
        /// reconciliation cannot determine authoritative ordered quantities.
        /// </summary>
        public bool HasProformaData { get; set; }

        /// <summary>
        /// True when at least one Job Order exists for this PI.
        /// False means PI was never converted to JO — no delivery tracking.
        /// Invoices may exist (direct PI → TI workflow) but deliveries
        /// cannot be tracked. Reconciliation is limited to PI vs TI only.
        /// </summary>
        public bool HasDeliveryTracking { get; set; }

        // ── Line-item rows ───────────────────────────────────────────────

        /// <summary>One row per glass line item in the PI.</summary>
        public List<OrderReconciliationRow> Rows { get; set; } = new();

        // ── Computed rollup quantities ───────────────────────────────────

        /// <summary>Sum of OrderedQty across all rows.</summary>
        public int TotalOrderedQty => Rows.Sum(r => r.OrderedQty);

        /// <summary>Sum of DeliveredQty across all rows.</summary>
        public int TotalDeliveredQty => Rows.Sum(r => r.DeliveredQty);

        /// <summary>Sum of BalanceQty across all rows.</summary>
        public int TotalBalanceQty => Rows.Sum(r => r.BalanceQty);

        /// <summary>Sum of InvoicedQty across all rows.</summary>
        public int TotalInvoicedQty => Rows.Sum(r => r.InvoicedQty);

        /// <summary>Net difference: total invoiced minus total delivered.</summary>
        public int TotalInvoiceVsDeliveryDiff =>
            TotalInvoicedQty - TotalDeliveredQty;

        /// <summary>Total area across all rows (m²).</summary>
        public decimal TotalSqm => Rows.Sum(r => r.TotalSqm);

        // ── Status rollup flags ──────────────────────────────────────────

        /// <summary>True when ANY row has a discrepancy (not FullyMatched).</summary>
        public bool HasDiscrepancies =>
            Rows.Any(r => r.Status != ReconciliationStatus.FullyMatched);

        /// <summary>True when ANY row is OverInvoiced. ★ THE CRITICAL FLAG.</summary>
        public bool HasOverInvoicing =>
            Rows.Any(r => r.Status == ReconciliationStatus.OverInvoiced);

        /// <summary>True when ANY row is UnderInvoiced (revenue leakage).</summary>
        public bool HasUnderInvoicing =>
            Rows.Any(r => r.Status == ReconciliationStatus.UnderInvoiced);

        /// <summary>True when ANY row has pending delivery balance.</summary>
        public bool HasPendingDelivery =>
            Rows.Any(r => r.BalanceQty > 0);

        /// <summary>True when ALL rows are FullyMatched.</summary>
        public bool AllMatched =>
            Rows.Count > 0 &&
            Rows.All(r => r.Status == ReconciliationStatus.FullyMatched);

        // ── Status counts ────────────────────────────────────────────────

        /// <summary>Count of rows per status for summary display.</summary>
        public int MatchedCount =>
            Rows.Count(r => r.Status == ReconciliationStatus.FullyMatched);
        public int OverInvoicedCount =>
            Rows.Count(r => r.Status == ReconciliationStatus.OverInvoiced);
        public int UnderInvoicedCount =>
            Rows.Count(r => r.Status == ReconciliationStatus.UnderInvoiced);
        public int PartialDeliveryCount =>
            Rows.Count(r => r.Status == ReconciliationStatus.PartialDelivery);
        public int NotStartedCount =>
            Rows.Count(r => r.Status == ReconciliationStatus.NotYetStarted);

        // ── Display helpers ──────────────────────────────────────────────

        /// <summary>
        /// Overall status for the entire PI family.
        /// Red if any over-invoiced, Amber if discrepancies, Green if all matched.
        /// </summary>
        public ReconciliationStatus OverallStatus
        {
            get
            {
                if (!HasProformaData) return ReconciliationStatus.NotYetStarted;
                if (HasOverInvoicing) return ReconciliationStatus.OverInvoiced;
                if (HasUnderInvoicing) return ReconciliationStatus.UnderInvoiced;
                if (HasPendingDelivery) return ReconciliationStatus.PartialDelivery;
                if (AllMatched) return ReconciliationStatus.FullyMatched;
                return ReconciliationStatus.NotYetStarted;
            }
        }

        /// <summary>
        /// Human-readable summary line.
        /// e.g. "3 items OK, 1 OVER-INVOICED, 2 pending delivery"
        /// </summary>
        public string SummaryDisplay
        {
            get
            {
                if (!HasProformaData) return "PI data not available";
                if (Rows.Count == 0) return "No line items";

                var parts = new List<string>();

                if (MatchedCount > 0)
                    parts.Add($"{MatchedCount} matched");
                if (OverInvoicedCount > 0)
                    parts.Add($"{OverInvoicedCount} OVER-INVOICED");
                if (UnderInvoicedCount > 0)
                    parts.Add($"{UnderInvoicedCount} under-invoiced");
                if (PartialDeliveryCount > 0)
                    parts.Add($"{PartialDeliveryCount} partial");
                if (NotStartedCount > 0)
                    parts.Add($"{NotStartedCount} not started");

                return string.Join(" · ", parts);
            }
        }

        /// <summary>
        /// Compact totals line.
        /// e.g. "Total — Ordered: 10, Delivered: 7, Invoiced: 9, Balance: 3"
        /// </summary>
        public string TotalsDisplay =>
            $"Ordered: {TotalOrderedQty}  ·  " +
            $"Delivered: {TotalDeliveredQty}  ·  " +
            $"Invoiced: {TotalInvoicedQty}  ·  " +
            $"Balance: {TotalBalanceQty}";

        /// <summary>Overall status color hex for the summary badge.</summary>
        public string OverallColorHex => OverallStatus.ToColorHex();

        /// <summary>Overall status icon for compact display.</summary>
        public string OverallIcon => OverallStatus.ToIcon();
    }
}