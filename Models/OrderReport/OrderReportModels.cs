// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/OrderReport/OrderReportModels.cs
// PROJECT: ProGlassAutomation
// MODULE: Report Generator — Order Fulfilment Report (2026-09-06)
//
// PURPOSE:
//   POCO models for the consolidated "Overall Report" that walks one
//   order through its full lifecycle:
//     Proforma Invoice  →  Job Order (release)  →  Delivery Notes
//     →  Tax Invoices   →  reconciliation  →  attachment completeness.
//
//   All classes are pure POCOs — JSON-serializable by System.Text.Json
//   (mirrors Models/Statement/* which persist into FullSnapshotJson).
//   Computed get-only properties are ignored on deserialize, exactly
//   like StatementSnapshot / ReconciliationModels.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement.Reconciliation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Models.OrderReport
{
    /// <summary>
    /// Root snapshot for one order (Proforma family). Serialized to JSON
    /// in SavedOrderReport.FullSnapshotJson so a report can be re-rendered
    /// and re-exported without re-querying the databases.
    /// </summary>
    public class OrderReportSnapshot
    {
        public OrderReportHeader Header { get; set; } = new();

        /// <summary>PI specification rows — same data the Proforma Invoice shows.</summary>
        public List<OrderReportLineItem> SpecRows { get; set; } = new();

        /// <summary>One section per confirmed Delivery Note (excludes Master/Dummy).</summary>
        public List<OrderReportDeliveryNote> DeliveryNotes { get; set; } = new();

        /// <summary>One section per Tax Invoice in the family.</summary>
        public List<OrderReportTaxInvoice> TaxInvoices { get; set; } = new();

        /// <summary>Real attachment counts + missing-document detection.</summary>
        public OrderReportAttachmentCompleteness Attachments { get; set; } = new();

        /// <summary>Cross-PI/DN/TI line reconciliation (reuses existing engine).</summary>
        public OrderReconciliationSummary? Reconciliation { get; set; }

        /// <summary>Rolled-up quantities / weights / values.</summary>
        public OrderReportTotals Totals { get; set; } = new();

        /// <summary>Plain warnings raised while building (missing docs, over-invoicing…).</summary>
        public List<OrderReportWarning> Warnings { get; set; } = new();

        public bool HasDeliveryNotes => DeliveryNotes.Count > 0;
        public bool HasTaxInvoices => TaxInvoices.Count > 0;
    }

    // ═════════════════════════════════════════════════════════════════════
    //  BUNDLE — a generated report (single or multi-PI). Persisted as the
    //  FullSnapshotJson of SavedOrderReport so Review + PDF re-render from it.
    // ═════════════════════════════════════════════════════════════════════

    public class OrderReportBundle
    {
        public string ReportNumber { get; set; } = "";
        public string Title { get; set; } = "";
        public DateTime GeneratedAt { get; set; }

        /// <summary>One section per selected Proforma Invoice.</summary>
        public List<OrderReportSnapshot> Sections { get; set; } = new();

        public int SectionCount => Sections.Count;
        public int TotalOrderedQty => Sections.Sum(s => s.Totals.OrderedQty);
        public int TotalDeliveredQty => Sections.Sum(s => s.Totals.DeliveredQty);
        public int TotalInvoicedQty => Sections.Sum(s => s.Totals.InvoicedQty);
        public decimal TotalInvoicedValue => Sections.Sum(s => s.Totals.InvoicedValue);
        public int TotalMissingDocuments =>
            Sections.Sum(s => s.Attachments.DeliveryNotesMissing);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  HEADER — PI + job-release data
    // ═════════════════════════════════════════════════════════════════════

    public class OrderReportHeader
    {
        // ── Proforma Invoice ──────────────────────────────────────────────
        public string ProformaId { get; set; } = "";
        public string PiStatus { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string CompanyTrn { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string Salesman { get; set; } = "";
        public string PreparedBy { get; set; } = "";
        public DateTime IssueDate { get; set; }
        public DateTime ValidUntil { get; set; }
        public decimal GrandTotal { get; set; }
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }

        // ── Job release data ─────────────────────────────────────────────
        /// <summary>Comma-separated Job Order ids for this PI ("" = not converted).</summary>
        public string JobOrderIds { get; set; } = "";

        /// <summary>Most relevant JO status (first JO if several).</summary>
        public string JobOrderStatus { get; set; } = "";

        /// <summary>Production priority of the JO (High/Medium/Low).</summary>
        public string JobOrderPriority { get; set; } = "";

        /// <summary>
        /// Release / target delivery date. SavedJobOrder persists
        /// DeliveryDate (the scheduled release) — there is no separate
        /// ReleaseDate column, so this is the release figure we can show
        /// from real stored data.
        /// </summary>
        public DateTime? ReleaseDate { get; set; }

        /// <summary>When the PI was converted into this Job Order.</summary>
        public DateTime? ConvertedAt { get; set; }

        public bool HasJobOrder => !string.IsNullOrWhiteSpace(JobOrderIds);

        public string IssueDateDisplay =>
            IssueDate == default ? "—" : IssueDate.ToString("dd MMM yyyy");
        public string ValidUntilDisplay =>
            ValidUntil == default ? "—" : ValidUntil.ToString("dd MMM yyyy");
        public string ReleaseDateDisplay =>
            ReleaseDate.HasValue ? ReleaseDate.Value.ToString("dd MMM yyyy") : "—";
        public string ConvertedAtDisplay =>
            ConvertedAt.HasValue ? ConvertedAt.Value.ToString("dd MMM yyyy HH:mm") : "—";
    }

    // ═════════════════════════════════════════════════════════════════════
    //  SPEC ROWS — the PI rows exactly as the Proforma Invoice shows them
    // ═════════════════════════════════════════════════════════════════════

    public class OrderReportLineItem
    {
        public int GroupNumber { get; set; }
        public string GroupName { get; set; } = "";
        public int SerialNumber { get; set; }
        public string GlassRef { get; set; } = "";
        public string SpecName { get; set; } = "";
        public string GlassType { get; set; } = "";
        public string OuterColor { get; set; } = "";
        public string InnerColor { get; set; } = "";
        public int W1 { get; set; }
        public int H1 { get; set; }
        public int W2 { get; set; }
        public int H2 { get; set; }

        public int Qty { get; set; }
        public decimal Sqm1 { get; set; }
        public decimal TotalSqm { get; set; }
        public decimal Rate { get; set; }
        public decimal Amount { get; set; }

        // ── Lifecycle / reconciliation (per exact row) ────────────────────
        public int DeliveredQty { get; set; }
        public int BalanceQty { get; set; }
        public int InvoicedQty { get; set; }
        public string DeliveryStatus { get; set; } = "";
        public string InvoiceStatus { get; set; } = "";

        // ── Weight (from DN items; kg) ────────────────────────────────────
        public decimal WeightPerPiece { get; set; }
        public decimal TotalWeight => WeightPerPiece * DeliveredQty;
        public decimal OrderWeight => WeightPerPiece * Qty;

        public string DimensionsDisplay
        {
            get
            {
                if (W1 <= 0 && H1 <= 0) return "—";
                if ((W2 <= 0 || W2 == W1) && (H2 <= 0 || H2 == H1))
                    return $"{W1}×{H1}";
                return $"{W1}×{H1} / {W2}×{H2}";
            }
        }

        public string CompositeKey => $"{GroupNumber}|{SerialNumber}";
    }

    // ═════════════════════════════════════════════════════════════════════
    //  DELIVERY NOTES
    // ═════════════════════════════════════════════════════════════════════

    public class OrderReportDeliveryNote
    {
        public string DeliveryNoteId { get; set; } = "";
        public int SequenceNumber { get; set; } = 1;
        public string Status { get; set; } = "Draft";
        public string DeliveryType { get; set; } = "Original";
        public bool IsFoc { get; set; }
        public DateTime DeliveryDate { get; set; }
        public string DeliveryTime { get; set; } = "";
        public string DriverName { get; set; } = "";
        public string VehicleNumber { get; set; } = "";
        public string ReceiverName { get; set; } = "";
        public string Notes { get; set; } = "";

        public int TotalDeliveredQty { get; set; }
        public int TotalBalanceQty { get; set; }

        /// <summary>Sum of item TotalWeight (delivered kg) on this DN.</summary>
        public decimal TotalWeightKg { get; set; }

        /// <summary>Real attachment count for this DN (from repository).</summary>
        public int AttachmentCount { get; set; }
        public bool HasAttachments => AttachmentCount > 0;

        public List<OrderReportDeliveryNoteItem> Items { get; set; } = new();

        public string DeliveryDateDisplay =>
            DeliveryDate == default ? "—" : DeliveryDate.ToString("dd MMM yyyy");
        public string DeliveredWhenDisplay =>
            DeliveryDate == default
                ? "—"
                : $"{DeliveryDate:dd MMM yyyy}" +
                  (string.IsNullOrWhiteSpace(DeliveryTime) ? "" : $"  ·  {DeliveryTime}");
    }

    public class OrderReportDeliveryNoteItem
    {
        public int SpecGroupNumber { get; set; }
        public string SpecGroupName { get; set; } = "";
        public int SerialNumber { get; set; }
        public string GlassRef { get; set; } = "";
        public int W1 { get; set; }
        public int H1 { get; set; }
        public int W2 { get; set; }
        public int H2 { get; set; }

        public int OrderedQty { get; set; }
        public int PreviouslyDeliveredQty { get; set; }
        public int DeliveredQty { get; set; }
        public int BalanceQty { get; set; }

        public decimal WeightPerPiece { get; set; }
        public decimal TotalWeight => WeightPerPiece * DeliveredQty;
    }

    // ═════════════════════════════════════════════════════════════════════
    //  TAX INVOICES
    // ═════════════════════════════════════════════════════════════════════

    public class OrderReportTaxInvoice
    {
        public string InvoiceNumber { get; set; } = "";
        public string InvoiceType { get; set; } = "Full";   // Full | Partial
        public int SequenceNumber { get; set; } = 1;
        public string Status { get; set; } = "Draft";
        public string DeliveryNoteReference { get; set; } = "";
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }

        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public int TotalQty { get; set; }
        public decimal TotalSqm { get; set; }

        public int AttachmentCount { get; set; }
        public bool HasAttachments => AttachmentCount > 0;

        public List<OrderReportTaxInvoiceItem> Items { get; set; } = new();

        public string IssueDateDisplay =>
            IssueDate == default ? "—" : IssueDate.ToString("dd MMM yyyy");
        public string DueDateDisplay =>
            DueDate == default ? "—" : DueDate.ToString("dd MMM yyyy");
    }

    public class OrderReportTaxInvoiceItem
    {
        public int SpecGroupNumber { get; set; }
        public string SpecGroupName { get; set; } = "";
        public string GlassType { get; set; } = "";
        public int SerialNumber { get; set; }
        public string GlassRef { get; set; } = "";
        public int Qty { get; set; }
        public decimal Sqm { get; set; }
        public decimal TotalSqm { get; set; }
        public decimal Rate { get; set; }
        public decimal Value { get; set; }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  ATTACHMENT COMPLETENESS — the user's headline scenario
    //  ("3 delivery notes prepared, only 2 attached → 1 missing")
    // ═════════════════════════════════════════════════════════════════════

    public class OrderReportAttachmentCompleteness
    {
        /// <summary>Documents attached to the Proforma Invoice (Step 8 manifest).</summary>
        public int ProformaAttachmentCount { get; set; }
        public bool HasProformaAttachments => ProformaAttachmentCount > 0;

        /// <summary>Confirmed (non-master, non-dummy) delivery notes prepared.</summary>
        public int DeliveryNotesPrepared { get; set; }

        /// <summary>Of the prepared DNs, how many have ≥ 1 attachment.</summary>
        public int DeliveryNotesAttached { get; set; }

        /// <summary>DeliveryNotesPrepared − DeliveryNotesAttached.</summary>
        public int DeliveryNotesMissing =>
            Math.Max(0, DeliveryNotesPrepared - DeliveryNotesAttached);

        /// <summary>Delivery-note ids that have zero attachments.</summary>
        public List<string> MissingDeliveryNoteIds { get; set; } = new();

        /// <summary>Total attachments across all Tax Invoices in the family.</summary>
        public int TaxInvoiceAttachmentCount { get; set; }

        public bool HasMissingDocuments => DeliveryNotesMissing > 0;

        /// <summary>
        /// Example: "1 documents missing in attachments of delivery note DN-…".
        /// Pluralized like the user's wording.
        /// </summary>
        public string MissingDocumentsDisplay
        {
            get
            {
                if (DeliveryNotesMissing <= 0) return "";
                if (MissingDeliveryNoteIds.Count == 0)
                    return $"{DeliveryNotesMissing} documents missing in attachments of delivery note";
                return $"{DeliveryNotesMissing} documents missing in attachments of delivery note " +
                       string.Join(", ", MissingDeliveryNoteIds);
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  TOTALS
    // ═════════════════════════════════════════════════════════════════════

    public class OrderReportTotals
    {
        public int OrderedQty { get; set; }
        public decimal OrderedSqm { get; set; }
        public decimal OrderedValue { get; set; }
        public decimal OrderedWeightKg { get; set; }

        public int DeliveredQty { get; set; }
        public decimal DeliveredSqm { get; set; }
        public decimal DeliveredWeightKg { get; set; }

        public int BalanceQty { get; set; }
        public decimal BalanceSqm { get; set; }
        public decimal BalanceWeightKg { get; set; }

        public int InvoicedQty { get; set; }
        public decimal InvoicedValue { get; set; }
        public decimal InvoicedVat { get; set; }

        /// <summary>InvoicedQty − DeliveredQty (positive = over-invoiced).</summary>
        public int InvoicedMinusDeliveredQty => InvoicedQty - DeliveredQty;

        public decimal DeliveryPct =>
            OrderedQty <= 0 ? 0 : Math.Round(DeliveredQty / (decimal)OrderedQty * 100, 1);
        public decimal InvoicePct =>
            OrderedQty <= 0 ? 0 : Math.Round(InvoicedQty / (decimal)OrderedQty * 100, 1);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  WARNING (plain, JSON-safe)
    // ═════════════════════════════════════════════════════════════════════

    public class OrderReportWarning
    {
        /// <summary>Error | Warning | Info (matches LogSeverity names).</summary>
        public string Severity { get; set; } = "Info";

        public string Title { get; set; } = "";
        public string Detail { get; set; } = "";

        /// <summary>Action kind name (OpenProforma / OpenJobOrder / OpenInvoice / OpenPdf).</summary>
        public string ActionKind { get; set; } = "";

        /// <summary>Document reference for the action (number / id).</summary>
        public string TargetId { get; set; } = "";
    }
}
