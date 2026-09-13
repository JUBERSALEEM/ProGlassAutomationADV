// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Production/SavedProduction/SavedProductionEntities.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — persistence entities (Phase 2, 2026-09-11)
//
// EF Core entities for production.db. Storage pattern mirrors Job Order:
// one release row per distributed job + one row per stage work item.
// UniqueKey is the deterministic identity produced by
// ProductionDistributionEngine — a re-release upserts on it, so floor
// statuses survive re-distribution.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.Production.SavedProduction
{
    /// <summary>
    /// One distribution release — a Job Order (or a PI released directly)
    /// fanned out into stage work items.
    /// </summary>
    [Table("ProductionReleases")]
    public class SavedProductionRelease
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Job Order id, or the ProformaId when released straight from a PI. UNIQUE.</summary>
        [Required, MaxLength(60)]
        public string JobRef { get; set; } = string.Empty;

        [Required, MaxLength(60)]
        public string SourceProformaId { get; set; } = string.Empty;

        [MaxLength(200)]
        public string CustomerName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>Number of work items in the latest distribution.</summary>
        public int ItemCount { get; set; }

        /// <summary>Engine warnings from the latest distribution (JSON array).</summary>
        public string WarningsJson { get; set; } = "[]";

        public DateTime FirstReleasedAt { get; set; } = DateTime.Now;
        public DateTime LastReleasedAt { get; set; } = DateTime.Now;
    }

    /// <summary>One stage visit persisted on the factory floor.</summary>
    [Table("ProductionWorkItems")]
    public class SavedProductionWorkItem
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Deterministic identity from the distribution engine. UNIQUE.</summary>
        [Required, MaxLength(220)]
        public string UniqueKey { get; set; } = string.Empty;

        // ── Provenance ────────────────────────────────────────────────────
        [Required, MaxLength(60)]
        public string JobRef { get; set; } = string.Empty;

        [MaxLength(60)]
        public string SourceProformaId { get; set; } = string.Empty;

        [MaxLength(200)]
        public string CustomerName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>Sales representative carried from the invoice (floor contact).</summary>
        [MaxLength(200)]
        public string Salesman { get; set; } = string.Empty;

        public int SpecGroupNumber { get; set; }

        [MaxLength(400)]
        public string SpecName { get; set; } = string.Empty;

        /// <summary>GlassBuildType enum name (Sgu/Dgu/Lam/DguLam).</summary>
        [MaxLength(20)]
        public string BuildType { get; set; } = string.Empty;

        public int SerialNumber { get; set; }

        [MaxLength(100)]
        public string GlassRef { get; set; } = string.Empty;

        /// <summary>Outer / Inner / Middle / Lam Outer / Lam Inner / Glass / Unit / Batch.</summary>
        [MaxLength(30)]
        public string Position { get; set; } = string.Empty;

        // ── Geometry & quantities ────────────────────────────────────────
        public int WidthMm { get; set; }
        public int HeightMm { get; set; }
        public int Qty { get; set; }

        /// <summary>
        /// Pieces the floor has COMPLETED in this stage (0..Qty). Production
        /// flows piece by piece: only this many pieces are available to the
        /// next stage. QtyDone == Qty means the whole quantity has moved on.
        /// </summary>
        public int QtyDone { get; set; }

        [Column(TypeName = "decimal(18, 3)")]
        public decimal UnitSqm { get; set; }

        [Column(TypeName = "decimal(18, 3)")]
        public decimal TotalSqm { get; set; }

        [Column(TypeName = "decimal(18, 3)")]
        public decimal TotalLm { get; set; }

        [MaxLength(30)]
        public string Treatment { get; set; } = string.Empty;

        // ── Stage ────────────────────────────────────────────────────────
        /// <summary>ProductionStage enum value stored as int.</summary>
        public int Stage { get; set; }

        /// <summary>WorkItemStatus enum value stored as int.</summary>
        public int Status { get; set; }

        [MaxLength(40)]
        public string PredecessorStage { get; set; } = string.Empty;

        // ── Step 6 charge linkage ────────────────────────────────────────
        [MaxLength(40)]
        public string ChargeTemplateKey { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18, 3)")]
        public decimal ChargeMetricQty { get; set; }

        [MaxLength(10)]
        public string ChargeMetricUnit { get; set; } = string.Empty;

        [MaxLength(200)]
        public string PolishSubTypeLabel { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Notes { get; set; } = string.Empty;

        // ── Audit ────────────────────────────────────────────────────────
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Operator who last marked this item COMPLETED (username / display
        /// name). Empty while the item has never completed. ADDITIVE —
        /// populated by the piece-flow entry writer (2026-09-12).
        /// </summary>
        [MaxLength(120)]
        public string CompletedBy { get; set; } = string.Empty;

        // ── Phase 14: FOC / Replacement / Cancellation ───────────────────

        /// <summary>
        /// True = this work item is Free Of Charge (selling value zero,
        /// commercially traceable, profitability impact preserved).
        /// ADDITIVE Phase 14.
        /// </summary>
        public bool IsFoc { get; set; } = false;

        /// <summary>
        /// Replacement sequence for deterministic replacement tracking.
        /// 0 = original piece, 1+ = replacement generation.
        /// ADDITIVE Phase 14.
        /// </summary>
        public int ReplacementSequence { get; set; } = 0;

        /// <summary>
        /// Parent work item that this replacement replaces, if any.
        /// ADDITIVE Phase 14.
        /// </summary>
        public int? ParentWorkItemId { get; set; }

        /// <summary>
        /// True = this item has been cancelled (historical preserved,
        /// never physically deleted). ADDITIVE Phase 14.
        /// </summary>
        public bool IsCancelled { get; set; } = false;

        [MaxLength(120)]
        public string CancelledBy { get; set; } = string.Empty;

        public DateTime? CancelledAt { get; set; }

        [MaxLength(500)]
        public string CancelReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// One floor entry on a work item — every "mark done" records who did
    /// it and when, so the audit trail shows exactly which operator moved
    /// pieces through the factory and at what time. One row per save, per
    /// stage item (Outer / Inner / …).
    /// </summary>
    [Table("ProductionEntries")]
    public class SavedProductionEntry
    {
        [Key]
        public int Id { get; set; }

        /// <summary>FK to the stage work item this entry was recorded on.</summary>
        public int WorkItemId { get; set; }

        [Required, MaxLength(60)]
        public string JobRef { get; set; } = string.Empty;

        /// <summary>ProductionStage enum value stored as int.</summary>
        public int Stage { get; set; }

        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }

        [MaxLength(30)]
        public string Position { get; set; } = string.Empty;

        [MaxLength(400)]
        public string SpecName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string GlassRef { get; set; } = string.Empty;

        /// <summary>Pieces now marked done on the item after this entry.</summary>
        public int Qty { get; set; }

        /// <summary>Operator who saved the entry (AuditService.CurrentUser).</summary>
        [MaxLength(120)]
        public string DoneBy { get; set; } = string.Empty;

        public DateTime DoneAt { get; set; } = DateTime.Now;

        // ── Phase 14: EntryType / Breakage / Rework / FOC ───────────────

        /// <summary>
        /// Entry type: Normal=0 Breakage=1 Rework=2 Foc=3 QcPassed=4 QcFailed=5 Cancelled=6.
        /// ADDITIVE Phase 14 — default 0 Normal for backward compatibility.
        /// </summary>
        public int EntryType { get; set; } = 0;

        /// <summary>Reason for breakage / rework / FOC / QC / cancellation.</summary>
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        /// <summary>Parent entry that this entry replaces / originates from.</summary>
        public int? ParentEntryId { get; set; }

        /// <summary>Replacement sequence deterministic.</summary>
        public int ReplacementSequence { get; set; } = 0;

        /// <summary>True = FOC (selling value zero, cost preserved).</summary>
        public bool IsFoc { get; set; } = false;

        /// <summary>Related reservation id (logical FK).</summary>
        [MaxLength(120)]
        public string RelatedReservationId { get; set; } = string.Empty;

        /// <summary>Related QC id (logical FK).</summary>
        [MaxLength(120)]
        public string RelatedQcId { get; set; } = string.Empty;

        /// <summary>Additional notes.</summary>
        [MaxLength(1000)]
        public string Notes { get; set; } = string.Empty;
    }
}
