using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote
{
    /// <summary>
    /// EF Core entity for one delivery note row in deliverynotes.db.
    ///
    /// Uses JSON-blob strategy (same as PI + JO):
    /// FullSnapshotJson contains entire DN state for load/export.
    /// Indexed columns surfaced for list queries without JSON parsing.
    ///
    /// Status and DeliveryType stored as strings (same pattern as JO).
    /// Compare with string constants, not enum values.
    ///
    /// BUG-10: DeliveryDate / DeliveryTime defaults removed from property
    ///         level — set explicitly by mapper on creation only.
    /// BUG-11: CreatedAt / UpdatedAt defaults removed from property
    ///         level — set explicitly by repository on insert/update only.
    ///
    /// Phase 10.7: Added TotalPreviouslyDeliveredQty (Improvement 7).
    ///             Added TaxInvoiceId + HasTaxInvoice (Improvement 6).
    ///             ⚠️ DB schema change — delete deliverynotes.db before first run.
    /// </summary>
    [Table("DeliveryNotes")]
    public class SavedDeliveryNote
    {
        [Key]
        public int Id { get; set; }

        /// <summary>User-facing DN reference, e.g. "DN-2026-001-01". UNIQUE.</summary>
        [Required, MaxLength(50)]
        public string DeliveryNoteId { get; set; } = "";

        /// <summary>Parent Job Order reference, e.g. "JOB-2026-001".</summary>
        [Required, MaxLength(50)]
        public string JobOrderId { get; set; } = "";

        /// <summary>Proforma Invoice reference, e.g. "PI-2026-001".</summary>
        [MaxLength(50)]
        public string ProformaId { get; set; } = "";

        /// <summary>Delivery sequence within this JO (1, 2, 3...).</summary>
        public int SequenceNumber { get; set; } = 1;

        // ── Indexed searchable fields ────────────────────────────────────

        [MaxLength(200)]
        public string CustomerName { get; set; } = "";

        [MaxLength(200)]
        public string ProjectName { get; set; } = "";

        /// <summary>Draft | Dispatched | Delivered | Signed | Received</summary>
        [MaxLength(50)]
        public string Status { get; set; } = "Draft";

        /// <summary>Original | Dummy | Rework | Return</summary>
        [MaxLength(50)]
        public string DeliveryType { get; set; } = "Original";

        /// <summary>Free of charge flag — entire DN is FOC when true.</summary>
        public bool IsFoc { get; set; }

        // ── Delivery logistics ───────────────────────────────────────────

        [MaxLength(200)]
        public string DriverName { get; set; } = "";

        [MaxLength(50)]
        public string VehicleNumber { get; set; } = "";

        [MaxLength(200)]
        public string ReceiverName { get; set; } = "";

        /// <summary>
        /// Set explicitly by mapper at creation time.
        /// BUG-10: Removed DateTime.Now default at property level to avoid
        /// EF materialization interference on existing DB rows.
        /// </summary>
        public DateTime DeliveryDate { get; set; }

        /// <summary>
        /// Stored as "HH:mm". Set explicitly by mapper at creation time.
        /// BUG-10: Removed DateTime.Now.ToString("HH:mm") default at
        /// property level.
        /// </summary>
        [MaxLength(10)]
        public string DeliveryTime { get; set; } = "";

        [MaxLength(500)]
        public string Notes { get; set; } = "";

        // ── Quantity summary (for quick list display) ────────────────────

        public int TotalOrderedQty { get; set; }
        public int TotalDeliveredQty { get; set; }
        public int TotalBalanceQty { get; set; }

        /// <summary>
        /// Phase 10.7 (Improvement 7) — sum of PreviouslyDeliveredQty across
        /// all items on this DN at the time it was saved.
        /// Self-healed on load/save by DeliveryNoteDetailViewModel.
        /// </summary>
        public int TotalPreviouslyDeliveredQty { get; set; }

        // ── Tax Invoice link (Phase 19 prep — Improvement 6) ─────────────

        /// <summary>
        /// Phase 10.7 placeholder — wired up in Phase 19 (Tax Invoice module).
        /// Empty string = no invoice yet.
        /// </summary>
        [MaxLength(50)]
        public string TaxInvoiceId { get; set; } = "";

        /// <summary>
        /// True when a Tax Invoice has been generated from this DN.
        /// Phase 19 will set this when converting DN → Tax Invoice.
        /// </summary>
        public bool HasTaxInvoice { get; set; }

        // ── Full snapshot ────────────────────────────────────────────────

        [Required]
        public string FullSnapshotJson { get; set; } = "{}";

        // ── Timestamps ───────────────────────────────────────────────────

        /// <summary>
        /// BUG-11: Removed DateTime.Now default at property level.
        /// Set explicitly by repository in InsertAsync only.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// BUG-11: Removed DateTime.Now default at property level.
        /// Set explicitly by repository in InsertAsync + UpdateAsync.
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        // ── Navigation (EF relationships) ────────────────────────────────

        public List<DeliveryNoteItem> Items { get; set; } = new();
        public List<DeliveryNoteAttachment> Attachments { get; set; } = new();
    }
}