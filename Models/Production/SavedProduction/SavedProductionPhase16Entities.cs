// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Production/SavedProduction/SavedProductionPhase16Entities.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 16 Process Execution Persistence
//   Shared process execution framework, pairing, assembly.
//   Extend existing production.db, additive migrations, unique constraints,
//   indexes, foreign/reference identifiers, no destructive migration.
//   ExecutionId deterministic JobRef|Spec|Serial|Stage|ExecutionSequence
//   follows existing UniqueKey conventions.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.Production.SavedProduction
{
    /// <summary>
    /// Shared process execution record — one reusable framework for all 8 stages.
    /// Cutting=1 HolesCutouts=2 Polish=3 EdgeDeletion=4 Mitring=5 Tempering=6 Pairing=7 DoublingLamination=8
    /// </summary>
    [Table("ProcessExecutions")]
    public class SavedProcessExecution
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string ExecutionId { get; set; } = string.Empty; // UNIQUE deterministic JobRef|Spec|Serial|Stage|ExecutionSequence

        [Required, MaxLength(60)]
        public string JobRef { get; set; } = string.Empty;

        [MaxLength(60)]
        public string SourceProformaId { get; set; } = string.Empty;

        [MaxLength(60)]
        public string JobOrderId { get; set; } = string.Empty;

        public int SpecGroupNumber { get; set; }

        [MaxLength(400)]
        public string SpecName { get; set; } = string.Empty;

        public int SerialNumber { get; set; }

        [MaxLength(30)]
        public string Position { get; set; } = string.Empty;

        [MaxLength(200)]
        public string PieceId { get; set; } = string.Empty;

        [MaxLength(200)]
        public string InputPieceId { get; set; } = string.Empty;

        [MaxLength(200)]
        public string OutputPieceId { get; set; } = string.Empty;

        public int ProcessType { get; set; } // ProductionProcessType 1..8

        public int Sequence { get; set; }

        public int InputWorkItemId { get; set; }
        public int OutputWorkItemId { get; set; }

        public int Status { get; set; } // WorkItemStatus 0..8 reuse

        [MaxLength(120)]
        public string StartedBy { get; set; } = string.Empty;

        public DateTime? StartedAt { get; set; }

        [MaxLength(120)]
        public string CompletedBy { get; set; } = string.Empty;

        public DateTime? CompletedAt { get; set; }

        public int QtyRequired { get; set; }
        public int QtyDone { get; set; }

        public int EntryType { get; set; } // ProductionEntryType

        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Notes { get; set; } = string.Empty;

        public int QcStatus { get; set; } // QcResult

        [MaxLength(120)]
        public string QcId { get; set; } = string.Empty;

        [MaxLength(200)]
        public string ParentExecutionId { get; set; } = string.Empty;

        public int ReworkSequence { get; set; }

        public bool IsFoc { get; set; }

        [MaxLength(40)]
        public string PreviousStage { get; set; } = string.Empty;

        [MaxLength(40)]
        public string NextStage { get; set; } = string.Empty;

        public string ProcessDetailJson { get; set; } = string.Empty;

        [MaxLength(120)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Pairing record — especially important for DGU, deterministic identity.
    /// Pair compatible components using deterministic identity JobRef Spec Serial Position Component identity Piece index.
    /// Do NOT pair pieces merely because dimensions match, do NOT pair from different jobs/specifications.
    /// </summary>
    [Table("ProcessPairings")]
    public class SavedProcessPairing
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string PairingId { get; set; } = string.Empty; // UNIQUE deterministic JobRef|Spec|Serial|Pair:seq

        [Required, MaxLength(60)]
        public string JobRef { get; set; } = string.Empty;

        [MaxLength(60)]
        public string SourceProformaId { get; set; } = string.Empty;

        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }

        [MaxLength(30)]
        public string Position { get; set; } = string.Empty;

        [MaxLength(200)]
        public string PairId { get; set; } = string.Empty; // Pair-001 human

        [MaxLength(200)]
        public string OuterPieceId { get; set; } = string.Empty;

        [MaxLength(200)]
        public string InnerPieceId { get; set; } = string.Empty;

        [MaxLength(100)]
        public string OuterGlassRef { get; set; } = string.Empty;

        [MaxLength(100)]
        public string InnerGlassRef { get; set; } = string.Empty;

        public int OuterWorkItemId { get; set; }
        public int InnerWorkItemId { get; set; }

        [MaxLength(200)]
        public string OuterExecutionId { get; set; } = string.Empty;

        [MaxLength(200)]
        public string InnerExecutionId { get; set; } = string.Empty;

        public int Status { get; set; } // WorkItemStatus

        [MaxLength(120)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(1000)]
        public string Notes { get; set; } = string.Empty;

        public bool IsFoc { get; set; }
    }

    /// <summary>
    /// Doubling/Lamination assembly — preserves component lineage, not flattened into anonymous finished product.
    /// Supports DGU, Lamination, DGU+Lamination.
    /// </summary>
    [Table("ProcessAssemblies")]
    public class SavedProcessAssembly
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string AssemblyId { get; set; } = string.Empty; // UNIQUE deterministic JobRef|Spec|Serial|Assembly:seq

        [Required, MaxLength(60)]
        public string JobRef { get; set; } = string.Empty;

        [MaxLength(60)]
        public string SourceProformaId { get; set; } = string.Empty;

        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }

        [MaxLength(30)]
        public string Position { get; set; } = string.Empty;

        [MaxLength(200)]
        public string AssemblyLabel { get; set; } = string.Empty; // DGU-001, LAM-001 human

        [MaxLength(20)]
        public string AssemblyType { get; set; } = string.Empty; // DGU, Lamination, DGU+LAM

        public string ComponentsJson { get; set; } = string.Empty; // List<AssemblyComponent> JSON preserves lineage

        [MaxLength(200)]
        public string SpacerInfo { get; set; } = string.Empty;

        [MaxLength(200)]
        public string InterlayerInfo { get; set; } = string.Empty;

        [MaxLength(200)]
        public string SealantInfo { get; set; } = string.Empty;

        [MaxLength(200)]
        public string PairingId { get; set; } = string.Empty; // related pairing if any

        public int Status { get; set; } // WorkItemStatus

        public int QtyRequired { get; set; }
        public int QtyDone { get; set; }

        [MaxLength(120)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }

        [MaxLength(1000)]
        public string Notes { get; set; } = string.Empty;

        public bool IsFoc { get; set; }

        [MaxLength(120)]
        public string QcId { get; set; } = string.Empty;
    }
}
