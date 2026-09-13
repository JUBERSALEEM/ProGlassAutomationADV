// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Production/SavedProduction/SavedProductionPhase15Entities.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 15 Cutting + 2D Optimization entities
//   New tables for cutting workflow: CuttingSheet, CuttingPlacedPiece,
//   CuttingSession (links optimization session to cutting).
//   All deterministic IDs, UNIQUE indexes, additive, idempotent.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.Production.SavedProduction
{
    public enum CuttingSessionStatus
    {
        Draft = 0,
        Prepared = 1,
        Optimized = 2,
        Verified = 3,
        Allocated = 4,
        Cutting = 5,
        Completed = 6,
        Cancelled = 7,
        Failed = 8
    }

    public enum CuttingPieceStatus
    {
        Pending = 0,
        Reserved = 1,
        Allocated = 2,
        Cutting = 3,
        Completed = 4,
        QcPending = 5,
        QcPassed = 6,
        QcFailed = 7,
        Rework = 8,
        Breakage = 9,
        Foc = 10,
        Cancelled = 11
    }

    /// <summary>
    /// Cutting session — links JO → Cutting Requirements → Optimization → Allocation → Cutting Work Items.
    /// Deterministic session id: CUT-JobRef-yyyyMMddHHmmss
    /// </summary>
    public sealed class SavedCuttingSession
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string CuttingSessionId { get; set; } = string.Empty; // UNIQUE deterministic

        [Required, MaxLength(100)]
        public string JobRef { get; set; } = string.Empty;

        [MaxLength(100)]
        public string SourceProformaId { get; set; } = string.Empty;

        [MaxLength(200)]
        public string OptimizationSessionId { get; set; } = string.Empty; // Link to optimization.db session

        [MaxLength(100)]
        public string MaterialGroupId { get; set; } = string.Empty;

        public int TotalPieces { get; set; }
        public int SheetsUsed { get; set; }
        public decimal UtilizationPct { get; set; }
        public decimal WastagePct { get; set; }

        public int Status { get; set; } = (int)CuttingSessionStatus.Draft;

        public int RotationPolicy { get; set; } = 1; // Free90
        public int SplitMode { get; set; } = 0; // HorizontalFirst
        public int EffortLevel { get; set; } = 3;
        public string MarginProfileJson { get; set; } = string.Empty;

        public bool IsVerified { get; set; }
        public string VerificationResult { get; set; } = string.Empty;

        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }

        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>
    /// Cutting sheet — one physical sheet with its layout.
    /// Deterministic id: CuttingSessionId|SheetNo
    /// </summary>
    public sealed class SavedCuttingSheet
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(250)]
        public string CuttingSheetId { get; set; } = string.Empty; // UNIQUE

        [Required, MaxLength(200)]
        public string CuttingSessionId { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string JobRef { get; set; } = string.Empty;

        public int SheetNo { get; set; }

        public int StockSheetId { get; set; } // GlassSheet.Id logical FK
        [MaxLength(200)]
        public string StockSizeLabel { get; set; } = string.Empty;

        public int StockWidthMm { get; set; }
        public int StockHeightMm { get; set; }
        public int ThicknessMm { get; set; }
        [MaxLength(50)]
        public string Color { get; set; } = string.Empty;

        public int UsableX { get; set; }
        public int UsableY { get; set; }
        public int UsableWidth { get; set; }
        public int UsableHeight { get; set; }

        public decimal UsedSqm { get; set; }
        public decimal StockSqm { get; set; }
        public decimal UtilizationPct { get; set; }
        public decimal WastagePct { get; set; }

        public bool IsVerified { get; set; }
        [MaxLength(500)]
        public string VerificationResult { get; set; } = string.Empty;

        public int PlacedPieceCount { get; set; }

        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Cutting placed piece — one piece placed on a sheet.
    /// Deterministic id: CuttingSheetId|PieceId
    /// </summary>
    public sealed class SavedCuttingPlacedPiece
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(300)]
        public string PlacedPieceId { get; set; } = string.Empty; // UNIQUE

        [Required, MaxLength(250)]
        public string CuttingSheetId { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string CuttingSessionId { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string JobRef { get; set; } = string.Empty;

        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }

        [MaxLength(100)]
        public string GlassRef { get; set; } = string.Empty;

        [MaxLength(200)]
        public string SpecName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Position { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string PieceId { get; set; } = string.Empty; // CuttingRequirement.PieceId

        [MaxLength(200)]
        public string PieceLabel { get; set; } = string.Empty;

        public int WidthMm { get; set; }
        public int HeightMm { get; set; }

        public int X { get; set; }
        public int Y { get; set; }

        public bool IsRotated { get; set; } // 0° vs 90°
        public int RotationDegrees => IsRotated ? 90 : 0;

        public int CutSequence { get; set; } // Deterministic placement order

        public int Status { get; set; } = (int)CuttingPieceStatus.Pending;

        public int SourceWorkItemId { get; set; } // SavedProductionWorkItem.Id

        [MaxLength(200)]
        public string ReservationId { get; set; } = string.Empty;

        [MaxLength(200)]
        public string AllocationId { get; set; } = string.Empty;

        public bool IsFoc { get; set; }

        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;
    }
}
