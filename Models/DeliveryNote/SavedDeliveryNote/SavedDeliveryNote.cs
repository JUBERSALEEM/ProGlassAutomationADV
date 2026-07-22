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
    /// Status and DeliveryType stored as strings (same pattern as JO — gotcha #24).
    /// Compare with string constants, not enum values.
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

        /// <summary>Proforma Invoice reference for Tax Invoice link, e.g. "PI-2026-001".</summary>
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

        /// <summary>Auto-generated date at creation.</summary>
        public DateTime DeliveryDate { get; set; } = DateTime.Now;

        /// <summary>Auto-generated time at creation, stored as "HH:mm".</summary>
        [MaxLength(10)]
        public string DeliveryTime { get; set; } = DateTime.Now.ToString("HH:mm");

        [MaxLength(500)]
        public string Notes { get; set; } = "";

        // ── Quantity summary (for quick list display) ────────────────────

        public int TotalOrderedQty { get; set; }
        public int TotalDeliveredQty { get; set; }
        public int TotalBalanceQty { get; set; }

        // ── Full snapshot ────────────────────────────────────────────────

        [Required]
        public string FullSnapshotJson { get; set; } = "{}";

        // ── Timestamps ───────────────────────────────────────────────────

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // ── Navigation (EF relationships — items + attachments) ──────────

        public List<DeliveryNoteItem> Items { get; set; } = new();
        public List<DeliveryNoteAttachment> Attachments { get; set; } = new();
    }
}