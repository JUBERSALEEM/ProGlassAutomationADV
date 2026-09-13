// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Production/SavedProduction/SavedProductionPhase17Entities.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 17 Production Control & Factory Readiness
//   Additive, idempotent, backward compatible, deterministic identities.
//   Uses existing production.db, no new database.
//   Tables: Workstations, ProductionControlHolds, ProductionControlAssignments, ProductionControlOverrides
//   Preserve existing WorkItem architecture, no ProductionOrder aggregate.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.Production.SavedProduction
{
    /// <summary>
    /// Factory workstation / resource master — ERP-level only, no PLC/CNC.
    /// Smallest reusable master-data structure necessary.
    /// Examples: CUT-01, TEMPER-01, POLISH-01, CNC-01, LAM-01
    /// </summary>
    [Table("Workstations")]
    public sealed class SavedWorkstation
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(60)]
        public string WorkstationId { get; set; } = string.Empty; // deterministic UNIQUE e.g., CUT-01, TEMPER-01

        [Required, MaxLength(30)]
        public string Code { get; set; } = string.Empty; // CUT-01

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty; // Cutting Station 01

        /// <summary>ProductionStage int 1..8 — which process this workstation serves.</summary>
        public int ProcessType { get; set; } = 1;

        [MaxLength(30)]
        public string ProcessName { get; set; } = string.Empty; // Cutting, Tempering, etc.

        public bool IsActive { get; set; } = true;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(120)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Production hold — records hold reason, heldBy, heldAt, releasedBy, releasedAt, notes.
    /// History preserved when released, never deleted.
    /// </summary>
    [Table("ProductionControlHolds")]
    public sealed class SavedProductionControlHold
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(120)]
        public string HoldId { get; set; } = string.Empty; // deterministic UNIQUE JobRef|Spec|Serial|Stage|HoldSeq

        public int WorkItemId { get; set; } // FK to ProductionWorkItems Id

        [Required, MaxLength(60)]
        public string JobRef { get; set; } = string.Empty;

        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }

        [MaxLength(30)]
        public string Position { get; set; } = string.Empty;

        public int Stage { get; set; } // ProductionStage int

        [Required, MaxLength(100)]
        public string HoldReason { get; set; } = string.Empty; // Customer clarification, Material issue, etc.

        [MaxLength(120)]
        public string HeldBy { get; set; } = string.Empty;

        public DateTime HeldAt { get; set; } = DateTime.Now;

        [MaxLength(120)]
        public string ReleasedBy { get; set; } = string.Empty;

        public DateTime? ReleasedAt { get; set; }

        public bool IsReleased { get; set; } = false;

        [MaxLength(1000)]
        public string Notes { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Operator / workstation assignment — preserves AssignedBy, AssignedAt, AssignedUser, current operator/assignee.
    /// Uses existing identity/audit, no second user system.
    /// </summary>
    [Table("ProductionControlAssignments")]
    public sealed class SavedProductionControlAssignment
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(120)]
        public string AssignmentId { get; set; } = string.Empty; // deterministic UNIQUE JobRef|Spec|Serial|Stage|Seq

        public int WorkItemId { get; set; }

        [Required, MaxLength(60)]
        public string JobRef { get; set; } = string.Empty;

        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }

        [MaxLength(30)]
        public string Position { get; set; } = string.Empty;

        public int Stage { get; set; }

        [MaxLength(60)]
        public string WorkstationId { get; set; } = string.Empty; // FK to Workstations WorkstationId

        [MaxLength(120)]
        public string Operator { get; set; } = string.Empty; // assigned operator display name / username

        [MaxLength(120)]
        public string AssignedBy { get; set; } = string.Empty;

        public DateTime AssignedAt { get; set; } = DateTime.Now;

        [MaxLength(120)]
        public string UnassignedBy { get; set; } = string.Empty;

        public DateTime? UnassignedAt { get; set; }

        public bool IsActive { get; set; } = true;

        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Priority / DueDate override — preserves existing priority info, deterministic, no duplicate fields.
    /// Priority: 0 Normal, 1 High, 2 Urgent — influences queue ordering, must NOT bypass dependencies.
    /// DueDate: visibility only, not autonomous scheduling, do not silently change customer dates.
    /// </summary>
    [Table("ProductionControlOverrides")]
    public sealed class SavedProductionControlOverride
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(120)]
        public string ControlId { get; set; } = string.Empty; // deterministic UNIQUE JobRef|Spec|Serial|Stage

        public int WorkItemId { get; set; }

        [Required, MaxLength(60)]
        public string JobRef { get; set; } = string.Empty;

        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }

        [MaxLength(30)]
        public string Position { get; set; } = string.Empty;

        public int Stage { get; set; }

        /// <summary>0 Normal, 1 High, 2 Urgent</summary>
        public int Priority { get; set; } = 0;

        public DateTime? DueDate { get; set; } // factory due-date visibility, derived from JO DeliveryDate if null

        [MaxLength(120)]
        public string PrioritySetBy { get; set; } = string.Empty;

        public DateTime PrioritySetAt { get; set; } = DateTime.Now;

        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
