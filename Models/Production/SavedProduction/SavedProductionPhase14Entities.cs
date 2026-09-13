// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Production/SavedProduction/SavedProductionPhase14Entities.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 14 foundations (2026-09-13)
//   Breakage/Rework/FOC, Material Allocation/Reservation, QC, Actual Cost
//   All additive, all in production.db unless absolutely necessary cross-DB.
//   Deterministic IDs for idempotency, logical FKs to avoid cross-DB FK issues.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.Production.SavedProduction
{
    // ── Enums (persisted as INT, append-only) ──────────────────────────

    /// <summary>
    /// Production entry type — Normal, Breakage, Rework, FOC, QC Passed/Failed, Cancelled.
    /// Stored as INT in ProductionEntries.EntryType, additive Phase 14.
    /// </summary>
    public enum ProductionEntryType
    {
        Normal = 0,
        Breakage = 1,
        Rework = 2,
        Foc = 3,
        QcPassed = 4,
        QcFailed = 5,
        Cancelled = 6
    }

    public enum ReservationStatus
    {
        Available = 0,
        Reserved = 1,
        Allocated = 2,
        Consumed = 3,
        Released = 4,
        Cancelled = 5
    }

    public enum AllocationStatus
    {
        Available = 0,
        Reserved = 1,
        Allocated = 2,
        Consumed = 3,
        Released = 4,
        Cancelled = 5
    }

    public enum QcResult
    {
        Pending = 0,
        Passed = 1,
        Failed = 2,
        Rework = 3
    }

    public enum ActualCostType
    {
        Estimated = 0,
        ActualMaterial = 1,
        Production = 2,
        Wastage = 3,
        Processing = 4,
        TotalActual = 5
    }

    // ── Material Reservation ───────────────────────────────────────────

    /// <summary>
    /// Auditable reservation of stock sheets for a production job.
    /// Lives in production.db, links logically to GlassSheet (proglass.db)
    /// via GlassSheetId, and to Optimization Session via OptimizationSessionId.
    /// Deterministic ReservationId for idempotency:
    /// JobRef|Spec|Serial|GlassSheetId|OptSession|SheetNo
    /// </summary>
    [Table("MaterialReservations")]
    public class SavedMaterialReservation
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Deterministic idempotency key. UNIQUE.</summary>
        [Required, MaxLength(200)]
        public string ReservationId { get; set; } = string.Empty;

        [Required, MaxLength(60)]
        public string JobRef { get; set; } = string.Empty;

        [MaxLength(60)]
        public string SourceProformaId { get; set; } = string.Empty;

        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }

        [MaxLength(100)]
        public string GlassRef { get; set; } = string.Empty;

        [MaxLength(400)]
        public string SpecName { get; set; } = string.Empty;

        [MaxLength(120)]
        public string OptimizationSessionId { get; set; } = string.Empty;

        /// <summary>Logical FK to GlassSheet.Id in proglass.db (not enforced cross-DB).</summary>
        public int GlassSheetId { get; set; }

        [MaxLength(120)]
        public string StockSizeLabel { get; set; } = string.Empty;

        public int WidthMm { get; set; }
        public int HeightMm { get; set; }
        public int ThicknessMm { get; set; }

        [MaxLength(50)]
        public string Color { get; set; } = string.Empty;

        /// <summary>Number of stock sheets reserved.</summary>
        public int Quantity { get; set; }

        /// <summary>ReservationStatus enum stored as INT.</summary>
        public int Status { get; set; } = (int)ReservationStatus.Reserved;

        [MaxLength(120)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? ReleasedAt { get; set; }

        [MaxLength(500)]
        public string ReleaseReason { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Notes { get; set; } = string.Empty;
    }

    // ── Material Allocation ────────────────────────────────────────────

    /// <summary>
    /// Allocation of a placed piece to a reservation / sheet layout.
    /// Traceability: Stock Sheet → Material Allocation → Optimization Layout
    /// → Cut Piece → Production → Breakage/Rework → Consumption → Actual Cost
    /// </summary>
    [Table("MaterialAllocations")]
    public class SavedMaterialAllocation
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Deterministic idempotency key. UNIQUE.</summary>
        [Required, MaxLength(250)]
        public string AllocationId { get; set; } = string.Empty;

        /// <summary>Logical FK to SavedMaterialReservation.ReservationId</summary>
        [Required, MaxLength(200)]
        public string ReservationId { get; set; } = string.Empty;

        [Required, MaxLength(60)]
        public string JobRef { get; set; } = string.Empty;

        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }

        [MaxLength(120)]
        public string OptimizationSessionId { get; set; } = string.Empty;

        public int SheetNo { get; set; }

        [MaxLength(120)]
        public string PlacedPieceLabel { get; set; } = string.Empty;

        [MaxLength(100)]
        public string GlassRef { get; set; } = string.Empty;

        public int WidthMm { get; set; }
        public int HeightMm { get; set; }

        public int Quantity { get; set; } = 1;

        [Column(TypeName = \"decimal(18, 2)\")]
        public decimal UtilizationPct { get; set; }

        [Column(TypeName = \"decimal(18, 2)\")]
        public decimal WastagePct { get; set; }

        public int Status { get; set; } = (int)AllocationStatus.Allocated;

        [MaxLength(120)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? ConsumedAt { get; set; }

        [MaxLength(1000)]
        public string Notes { get; set; } = string.Empty;
    }

    // ── QC Entry ───────────────────────────────────────────────────────

    /// <summary>
    /// Minimal but production-grade QC state flow:
    /// Production → QC Pending → QC Passed → QC Failed → Rework → Ready
    /// Preserves Work Item, Piece, Stage, Result, Reason, Inspector, Timestamp, Notes.
    /// QC failure does NOT destroy completed production history.
    /// </summary>
    [Table(\"ProductionQcEntries\")]
    public class SavedProductionQcEntry
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Deterministic idempotency key. UNIQUE.</summary>
        [Required, MaxLength(200)]
        public string QcId { get; set; } = string.Empty;

        /// <summary>FK to SavedProductionWorkItem.Id (same DB, enforced).</summary>
        public int WorkItemId { get; set; }

        [Required, MaxLength(60)]
        public string JobRef { get; set; } = string.Empty;

        [MaxLength(60)]
        public string SourceProformaId { get; set; } = string.Empty;

        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }

        [MaxLength(100)]
        public string GlassRef { get; set; } = string.Empty;

        [MaxLength(400)]
        public string SpecName { get; set; } = string.Empty;

        [MaxLength(30)]
        public string Position { get; set; } = string.Empty;

        /// <summary>ProductionStage enum stored as INT.</summary>
        public int Stage { get; set; }

        public int QcSequence { get; set; } = 1;

        /// <summary>QcResult enum stored as INT.</summary>
        public int Result { get; set; } = (int)QcResult.Pending;

        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(120)]
        public string Inspector { get; set; } = string.Empty;

        public DateTime InspectedAt { get; set; } = DateTime.Now;

        [MaxLength(1000)]
        public string Notes { get; set; } = string.Empty;

        [MaxLength(200)]
        public string ParentQcId { get; set; } = string.Empty;

        public bool IsRework { get; set; } = false;
    }

    // ── Actual Cost ────────────────────────────────────────────────────

    /// <summary>
    /// Actual production material costing foundation.
    /// Separates Estimated vs Actual, derived from actual production/material usage.
    /// Traceable: Stock Sheet → Material Allocation → Optimization Layout
    /// → Cut Piece → Production → Breakage/Rework → Consumption → Actual Cost
    /// </summary>
    [Table(\"ProductionActualCosts\")]
    public class SavedActualCost
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Deterministic idempotency key. UNIQUE.</summary>
        [Required, MaxLength(250)]
        public string CostId { get; set; } = string.Empty;

        [Required, MaxLength(60)]
        public string JobRef { get; set; } = string.Empty;

        [MaxLength(60)]
        public string SourceProformaId { get; set; } = string.Empty;

        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }

        [MaxLength(100)]
        public string GlassRef { get; set; } = string.Empty;

        [MaxLength(400)]
        public string SpecName { get; set; } = string.Empty;

        /// <summary>ActualCostType enum stored as INT.</summary>
        public int CostType { get; set; } = (int)ActualCostType.ActualMaterial;

        /// <summary>Logical FK to GlassSheet.Id (proglass.db).</summary>
        public int? StockSheetId { get; set; }

        [MaxLength(200)]
        public string MaterialAllocationId { get; set; } = string.Empty;

        [Column(TypeName = \"decimal(18, 4)\")]
        public decimal Quantity { get; set; }

        [Column(TypeName = \"decimal(18, 4)\")]
        public decimal UnitCost { get; set; }

        [Column(TypeName = \"decimal(18, 4)\")]
        public decimal TotalCost { get; set; }

        [Column(TypeName = \"decimal(18, 4)\")]
        public decimal SellingValue { get; set; }

        public bool IsFoc { get; set; } = false;

        [MaxLength(120)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(1000)]
        public string Notes { get; set; } = string.Empty;
    }

    // ── Production Replacement (Breakage/Rework/FOC traceability) ──────

    /// <summary>
    /// Traceable replacement/rework requirement preserving history.
    /// Original piece NOT silently modified; breakage recorded as entry,
    /// replacement created as new work item with deterministic ReplacementId.
    /// </summary>
    [Table(\"ProductionReplacements\")]
    public class SavedProductionReplacement
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Deterministic idempotency key. UNIQUE.</summary>
        [Required, MaxLength(250)]
        public string ReplacementId { get; set; } = string.Empty;

        [Required, MaxLength(60)]
        public string JobRef { get; set; } = string.Empty;

        [MaxLength(60)]
        public string SourceProformaId { get; set; } = string.Empty;

        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }

        [MaxLength(100)]
        public string GlassRef { get; set; } = string.Empty;

        [MaxLength(400)]
        public string SpecName { get; set; } = string.Empty;

        [MaxLength(30)]
        public string Position { get; set; } = string.Empty;

        public int Stage { get; set; }

        /// <summary>Original work item that broke / needs rework.</summary>
        public int OriginalWorkItemId { get; set; }

        /// <summary>Breakage entry that triggered replacement.</summary>
        public int? OriginalEntryId { get; set; }

        /// <summary>Replacement work item (newly created).</summary>
        public int? ReplacementWorkItemId { get; set; }

        /// <summary>ProductionEntryType Rework=2 Foc=3 etc stored as INT.</summary>
        public int EntryType { get; set; } = (int)ProductionEntryType.Rework;

        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public int Quantity { get; set; } = 1;

        public bool IsFoc { get; set; } = false;

        public int ReplacementSequence { get; set; } = 1;

        [MaxLength(120)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int Status { get; set; } = 0; // 0 Pending 1 InProgress 2 Completed 3 Cancelled
    }
}
