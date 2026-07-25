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
    ///
    /// BUG-04: IssueDate / ValidUntil / CreatedAt / UpdatedAt no longer
    ///         default to DateTime.Now at property level. Repository sets
    ///         them explicitly on insert/update.
    /// BUG-05: Unique index on ProformaId is configured in DbContext via
    ///         fluent API (HasIndex(...).IsUnique()). Kept there because
    ///         MaxLength attribute is already on the property.
    /// BUG-06: FullSnapshotJson keeps "{}" default — required by EF Core
    ///         non-null constraint. Callers must handle empty JSON gracefully.
    /// </summary>
    [Table("ProformaInvoices")]
    public class SavedProformaInvoice
    {
        [Key]
        public int Id { get; set; }

        /// <summary>User-facing proforma reference, e.g. "PI-2026-001". UNIQUE.</summary>
        [Required, MaxLength(50)]
        public string ProformaId { get; set; } = "";

        // ── Indexed searchable fields ────────────────────────────────────

        [MaxLength(200)]
        public string CompanyName { get; set; } = "";

        [MaxLength(200)]
        public string CustomerName { get; set; } = "";

        [MaxLength(200)]
        public string ProjectName { get; set; } = "";

        [MaxLength(50)]
        public string Status { get; set; } = "Draft";

        /// <summary>
        /// BUG-04: Removed DateTime.Now default. Set by mapper on save.
        /// </summary>
        public DateTime IssueDate { get; set; }

        /// <summary>
        /// BUG-04: Removed DateTime.Now.AddDays(7) default. Set by mapper.
        /// </summary>
        public DateTime ValidUntil { get; set; }

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
        /// BUG-06: Default "{}" preserved for EF Required constraint.
        /// </summary>
        [Required]
        public string FullSnapshotJson { get; set; } = "{}";

        // ── Timestamps ──────────────────────────────────────────────────

        /// <summary>
        /// BUG-04: Removed DateTime.Now default. Set by repository InsertAsync.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// BUG-04: Removed DateTime.Now default. Set by repository on every save.
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}