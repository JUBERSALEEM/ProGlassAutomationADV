using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.JobOrder.SavedJobOrder
{
    /// <summary>
    /// EF Core entity for saved Job Orders in joborders.db.
    ///
    /// Storage pattern: indexed searchable columns + JSON blob for full state.
    /// FullSnapshotJson contains a serialized JobOrderSnapshot which embeds
    /// the full ProformaInvoiceSnapshot at conversion time (immutable) plus
    /// ProductionDetails (mutable).
    ///
    /// Search + list queries use indexed columns for performance. Loading
    /// a job order for detail view deserializes FullSnapshotJson.
    /// </summary>
    [Table("JobOrders")]
    public class SavedJobOrder
    {
        // ── primary key ───────────────────────────────────────────────────
        [Key]
        public int Id { get; set; }

        // ── indexed searchable columns ────────────────────────────────────

        /// <summary>User-facing ID, e.g. "JOB-2026-001". UNIQUE index.</summary>
        [Required, MaxLength(50)]
        public string JobOrderId { get; set; } = string.Empty;

        /// <summary>Source Proforma Invoice ID. Indexed for cross-lookup.</summary>
        [Required, MaxLength(50)]
        public string SourceProformaId { get; set; } = string.Empty;

        /// <summary>Customer name (copied from PI, indexed for search).</summary>
        [MaxLength(200)]
        public string CustomerName { get; set; } = string.Empty;

        /// <summary>Project name (copied from PI, indexed for search).</summary>
        [MaxLength(200)]
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>Grand total VAT inclusive (copied from PI, indexed for sorting).</summary>
        [Column(TypeName = "decimal(18, 2)")]
        public decimal GrandTotalVatInclusive { get; set; }

        /// <summary>
        /// Current status (Draft / Released / InProduction / Completed / Delivered).
        /// Stored as string for readability in raw DB queries.
        /// Indexed for filtering.
        /// </summary>
        [Required, MaxLength(30)]
        public string Status { get; set; } = "Draft";

        /// <summary>
        /// Production priority (High / Medium / Low).
        /// Stored as string. Indexed for sorting.
        /// </summary>
        [Required, MaxLength(20)]
        public string Priority { get; set; } = "Medium";

        /// <summary>Target delivery date. Indexed for date-range filtering.</summary>
        public DateTime? DeliveryDate { get; set; }

        // ── timestamps ────────────────────────────────────────────────────

        /// <summary>When this job order was converted from PI.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>Last status change or production detail edit.</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // ── full snapshot blob ────────────────────────────────────────────

        /// <summary>
        /// Full JobOrderSnapshot serialized as JSON. Contains embedded
        /// ProformaInvoiceSnapshot (immutable copy at conversion time) plus
        /// ProductionDetails (mutable). Deserialized when opening detail view.
        /// </summary>
        [Required]
        public string FullSnapshotJson { get; set; } = string.Empty;
    }
}