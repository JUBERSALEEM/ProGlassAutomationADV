using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.ProformaInvoice.SavedInvoice
{
    /// <summary>
    /// EF Core entity representing one saved proforma invoice row in the
    /// separate proformainvoices.db SQLite database.
    ///
    /// Phase 7.15 uses a JSON-blob strategy: the FullSnapshotJson column
    /// contains the entire serialized wizard state (all 7 steps + Terms).
    /// A few top-level fields are surfaced as real columns so we can index
    /// and query them for the Load List UI (Phase 7.17).
    /// </summary>
    [Table("ProformaInvoices")]
    public class SavedProformaInvoice
    {
        [Key]
        public int Id { get; set; }

        /// <summary>User-facing proforma reference, e.g. "PI-2026-001". UNIQUE.</summary>
        [Required, MaxLength(50)]
        public string ProformaId { get; set; } = "";

        // ── Indexed searchable fields (surface from snapshot for list queries) ──

        [MaxLength(200)]
        public string CompanyName { get; set; } = "";

        [MaxLength(200)]
        public string CustomerName { get; set; } = "";

        [MaxLength(200)]
        public string ProjectName { get; set; } = "";

        [MaxLength(50)]
        public string Status { get; set; } = "Draft";

        public DateTime IssueDate { get; set; } = DateTime.Now;

        public DateTime ValidUntil { get; set; } = DateTime.Now.AddDays(7);

        // ── Financial snapshot (for quick reports without deserializing JSON) ──

        [Column(TypeName = "DECIMAL(18,2)")]
        public decimal GlassMaterialsSubtotal { get; set; }

        [Column(TypeName = "DECIMAL(18,2)")]
        public decimal OtherChargesSubtotal { get; set; }

        [Column(TypeName = "DECIMAL(18,2)")]
        public decimal DiscountAmount { get; set; }

        [Column(TypeName = "DECIMAL(18,2)")]
        public decimal VatAmount { get; set; }

        [Column(TypeName = "DECIMAL(18,2)")]
        public decimal GrandTotalVatInclusive { get; set; }

        // ── Full snapshot (all wizard data) ─────────────────────────────

        /// <summary>
        /// Serialized JSON of the entire wizard state (Steps 1-7 + Terms).
        /// Load/edit operations deserialize this and rehydrate VMs.
        /// </summary>
        [Required]
        public string FullSnapshotJson { get; set; } = "{}";

        // ── Timestamps ──────────────────────────────────────────────────

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}