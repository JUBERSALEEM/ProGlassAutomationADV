// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Statement/Workflow/WorkflowResult.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report — Workflow Engine
//
// CHAT 22 v3 ADDITIONS:
//   • ReconciliationSummaries — per-PI line-item reconciliation results
//   • HasOverInvoicing — convenience flag for critical bug detection
//   • HasReconciliationDiscrepancies — flag for any billing discrepancy
//   • TotalOverInvoicedLineCount — badge count for UI display
//   • TotalUnderInvoicedLineCount — badge count for UI display
//
// CHAT 18 CHANGES (PRESERVED):
//   BUG 1 FIX: WorkflowTaxInvoicePdf.Attachments — new field holds
//              TI attachment bytes+names loaded from DB in Step 7.
//   BUG 3 FIX: WorkflowTaxInvoicePdf.AttachmentFileCount — computed
//              from Attachments list, used by Step 8 section counter.
//   BUG 3 FIX: TotalAttachmentCount — now includes TI attachment counts.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement.Reconciliation;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Models.Statement.Workflow
{
    /// <summary>Overall outcome of the workflow run.</summary>
    public enum WorkflowOutcome
    {
        /// <summary>All steps completed cleanly.</summary>
        Success = 0,

        /// <summary>Completed with warnings user bypassed.</summary>
        Warning = 1,

        /// <summary>Workflow blocked by critical validation failure.</summary>
        Blocked = 2,

        /// <summary>User cancelled or clicked Abort.</summary>
        Cancelled = 3
    }

    /// <summary>
    /// Complete result of a workflow run.
    /// Fully self-contained — contains all data needed for PDF export.
    /// </summary>
    public class WorkflowResult
    {
        /// <summary>Overall outcome — governs whether PDF gets generated.</summary>
        public WorkflowOutcome Outcome { get; set; } = WorkflowOutcome.Success;

        /// <summary>
        /// Human-readable summary shown to user at end of workflow.
        /// Example: "Statement ready. 2 warnings bypassed."
        /// </summary>
        public string Summary { get; set; } = "";

        /// <summary>
        /// All step states — used for post-run audit + final UI update.
        /// </summary>
        public List<WorkflowStepState> Steps { get; set; } = new();

        /// <summary>
        /// All warnings collected across all steps.
        /// Used for final warning summary dialog.
        /// </summary>
        public List<string> AllWarnings { get; set; } = new();

        // ── Collected data for PDF export ─────────────────────────────────

        /// <summary>
        /// The completed statement snapshot to render into pages 1-2.
        /// Already contains all filtered invoice rows + financials + aging.
        /// </summary>
        public StatementSnapshot? Snapshot { get; set; }

        /// <summary>
        /// Signed Proforma Invoice attachment bytes (if found).
        /// Currently sourced from TaxInvoiceAttachment where Type=SignedPI.
        /// Null when no signed PI exists.
        /// </summary>
        public byte[]? SignedProformaInvoiceBytes { get; set; }

        /// <summary>
        /// Signed Proforma Invoice file name (for display in PDF header).
        /// Empty when no signed PI exists.
        /// </summary>
        public string SignedProformaInvoiceFileName { get; set; } = "";

        /// <summary>
        /// Signed Delivery Note attachments in delivery order.
        /// Each item is a per-DN attachment set.
        /// Multiple attachments per DN are allowed.
        /// </summary>
        public List<WorkflowDeliveryNoteAttachment> SignedDeliveryNotes
        { get; set; } = new();

        /// <summary>
        /// Regenerated Tax Invoice PDF bytes, one per invoice.
        /// Order matches Snapshot.InvoiceRows order.
        /// Each entry also carries the invoice's own DB attachments
        /// (SignedPI, SignedDN, Other) loaded in Step 7.
        /// A null Bytes entry means invoice regeneration failed — a
        /// placeholder page will be inserted in the final PDF.
        /// </summary>
        public List<WorkflowTaxInvoicePdf> TaxInvoicePdfs { get; set; } = new();

        // ── Quantity coverage metrics ─────────────────────────────────────

        /// <summary>Total ordered qty from Job Order.</summary>
        public int OrderedQty { get; set; }

        /// <summary>Total delivered qty (sum across all DNs).</summary>
        public int DeliveredQty { get; set; }

        /// <summary>Total invoiced qty (sum across all Tax Invoices).</summary>
        public int InvoicedQty { get; set; }

        /// <summary>Delivered but not yet invoiced (BalanceToInvoice).</summary>
        public int UninvoicedDeliveredQty
            => System.Math.Max(0, DeliveredQty - InvoicedQty);

        /// <summary>Ordered but not yet delivered (in-progress production).</summary>
        public int PendingProductionQty
            => System.Math.Max(0, OrderedQty - DeliveredQty);

        // ── Missing invoice detection ─────────────────────────────────────

        /// <summary>
        /// Delivery Notes that have delivered qty but no corresponding
        /// Tax Invoice generated yet. Populated in Step 6.
        /// </summary>
        public List<UninvoicedDeliveryNote> UninvoicedDeliveryNotes
        { get; set; } = new();

        // ═════════════════════════════════════════════════════════════════
        //  CHAT 22 v3 — LINE-ITEM RECONCILIATION DATA
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Per-PI reconciliation summaries produced during Step 6.
        /// One entry per unique Proforma ID in the statement.
        /// Contains line-item breakdown of Ordered vs Delivered vs Invoiced
        /// with per-row status (FullyMatched, OverInvoiced, etc.).
        ///
        /// Populated by StatementWorkflowEngine.Step6_CrossMatchCoverage
        /// via OrderReconciliationService.BuildForProformaAsync().
        ///
        /// Empty list when no PIs matched or reconciliation service failed.
        /// UI (Review step, Preview step, PDF exporter) can iterate this
        /// to display detailed line-item analysis with color coding.
        /// </summary>
        public List<OrderReconciliationSummary> ReconciliationSummaries
        { get; set; } = new();

        /// <summary>
        /// True when ANY line item across ANY PI is OVER-INVOICED
        /// (billed for more units than actually delivered).
        ///
        /// ★ CRITICAL FLAG — this is the primary bug scenario the
        /// reconciliation feature was built to detect. UI should
        /// use this to trigger RED alerts / warning banners.
        /// </summary>
        public bool HasOverInvoicing =>
            ReconciliationSummaries.Any(s => s.HasOverInvoicing);

        /// <summary>
        /// True when ANY line item has any kind of billing discrepancy
        /// (over-invoiced, under-invoiced, over-delivered, etc.).
        /// Broader than HasOverInvoicing — catches all non-matched states.
        /// </summary>
        public bool HasReconciliationDiscrepancies =>
            ReconciliationSummaries.Any(s => s.HasDiscrepancies);

        /// <summary>
        /// Total count of over-invoiced line items across all PI families.
        /// Used for badge display: "3 lines OVER-INVOICED".
        /// </summary>
        public int TotalOverInvoicedLineCount =>
            ReconciliationSummaries.Sum(s => s.OverInvoicedCount);

        /// <summary>
        /// Total count of under-invoiced line items across all PI families.
        /// Under-invoiced = delivered but not yet billed (revenue leakage).
        /// Used for badge display: "2 lines under-invoiced".
        /// </summary>
        public int TotalUnderInvoicedLineCount =>
            ReconciliationSummaries.Sum(s => s.UnderInvoicedCount);

        // ── Helpers ───────────────────────────────────────────────────────

        /// <summary>True when PDF should be generated (Success or Warning).</summary>
        public bool ShouldGeneratePdf =>
            Outcome == WorkflowOutcome.Success ||
            Outcome == WorkflowOutcome.Warning;

        /// <summary>
        /// Total attachment count for PDF page estimation.
        /// BUG 3 FIX: now includes TI attachment counts per invoice.
        /// </summary>
        public int TotalAttachmentCount
        {
            get
            {
                int count = 0;

                // Signed PI (1 file)
                if (SignedProformaInvoiceBytes != null &&
                    SignedProformaInvoiceBytes.Length > 0)
                    count++;

                // DN attachments (1 per file, across all DNs)
                foreach (var dn in SignedDeliveryNotes)
                    count += dn.AttachmentBytes.Count;

                // Invoice PDFs themselves
                count += TaxInvoicePdfs.Count;

                // BUG 3 FIX: TI attachments (SignedPI, SignedDN, Other per invoice)
                foreach (var inv in TaxInvoicePdfs)
                    count += inv.AttachmentFileCount;

                return count;
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  SUPPORTING DTOs
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// One DN's signed attachment set.
    /// Multiple attachments per DN are allowed (photos, PDFs, etc.).
    /// </summary>
    public class WorkflowDeliveryNoteAttachment
    {
        public string DeliveryNoteId { get; set; } = "";
        public string DeliveryNoteType { get; set; } = "";
        public int DeliveredQty { get; set; }
        public System.DateTime DeliveryDate { get; set; }

        /// <summary>
        /// All attachment byte arrays for this DN (already converted
        /// to PDF-ready format — images wrapped in PDF pages).
        /// Empty list = no attachments (may have been bypassed by user).
        /// </summary>
        public List<byte[]> AttachmentBytes { get; set; } = new();

        /// <summary>File names for display in progress dialog.</summary>
        public List<string> AttachmentFileNames { get; set; } = new();

        /// <summary>True when DN was included without attachments.</summary>
        public bool WasBypassed { get; set; }
    }

    /// <summary>
    /// One Tax Invoice's PDF bytes + its own DB attachments.
    ///
    /// BUG 1 FIX:
    ///   New field: Attachments — holds TaxInvoiceAttachment records
    ///   (SignedPI, SignedDN, Other) loaded from DB in Step 7.
    ///   StatementPdfExporter.ExportFromWorkflowAsync Step 4 iterates
    ///   these and calls ConvertToRawPdfBytes on each.
    ///
    /// BUG 3 FIX:
    ///   AttachmentFileCount — computed property used by Step 8
    ///   section counter so total section count is accurate.
    ///
    /// Null Bytes = regeneration failed → placeholder page inserted.
    /// </summary>
    public class WorkflowTaxInvoicePdf
    {
        public string InvoiceNumber { get; set; } = "";
        public string InvoiceType { get; set; } = ""; // Full | Partial

        /// <summary>Regenerated PDF bytes, or null if generation failed.</summary>
        public byte[]? Bytes { get; set; }

        /// <summary>Error message if regeneration failed.</summary>
        public string? Error { get; set; }

        /// <summary>True when regeneration succeeded.</summary>
        public bool IsValid => Bytes != null && Bytes.Length > 0;

        // ── BUG 1 FIX: TI attachment storage ─────────────────────────────

        /// <summary>
        /// Tax Invoice DB attachments for this invoice.
        /// Loaded in Step 7 after PDF regeneration.
        /// Order: SignedPI → SignedDN → Other (ordered by AttachmentType).
        /// Each tuple: (FileBytes, FileName) ready for ConvertToRawPdfBytes.
        /// </summary>
        public List<(byte[] FileBytes, string FileName)> Attachments
        { get; set; } = new();

        /// <summary>
        /// BUG 3 FIX: Count of TI attachments loaded from DB.
        /// Used by Step 8 to compute accurate total section count.
        /// </summary>
        public int AttachmentFileCount => Attachments.Count;
    }

    /// <summary>
    /// A delivery note that has delivered items but no invoice covering them.
    /// Shown in the "Create Missing Invoice" warning dialog.
    /// </summary>
    public class UninvoicedDeliveryNote
    {
        public string DeliveryNoteId { get; set; } = "";
        public string DeliveryNoteType { get; set; } = "";
        public int DeliveredQty { get; set; }
        public System.DateTime DeliveryDate { get; set; }
        public string ProformaId { get; set; } = "";
        public string JobOrderId { get; set; } = "";
    }
}