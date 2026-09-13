// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Production/Process/ProductionProcessExecution.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 16 Shared Process Execution Framework
//   Conceptually: ProductionProcessExecution preserves ExecutionId JobRef PI JO
//   SpecNo Serial PieceId ProcessType Sequence InputWorkItemId OutputWorkItemId
//   Status StartedBy StartedAt CompletedBy CompletedAt QtyRequired QtyDone EntryType
//   Reason Notes QcStatus ParentExecutionId ReworkSequence
//   Use existing production identity conventions, deterministic ids.
//   Reuse existing WorkItemStatus — do not create second status system.
//   Input/Output traceability: Previous stage → current → next, preserve original
//   piece identity, deterministic child/output identity when transformation creates
//   new entity, never lose original.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Production;
using System;

namespace ProGlassAutomation.Models.Production.Process
{
    /// <summary>
    /// Canonical process types — must remain compatible with ProductionStage numeric identities:
    /// Cutting=1 HolesCutouts=2 Polish=3 EdgeDeletion=4 Mitring=5 Tempering=6 Pairing=7 DoublingLamination=8
    /// </summary>
    public enum ProductionProcessType
    {
        Cutting = 1,
        HolesCutouts = 2,
        Polish = 3,
        EdgeDeletion = 4,
        Mitring = 5,
        Tempering = 6,
        Pairing = 7,
        DoublingLamination = 8
    }

    /// <summary>
    /// Shared execution record for any production process — cutting through doubling/lamination.
    /// One reusable framework, configured per process, not eight independent systems.
    /// </summary>
    public sealed class ProductionProcessExecution
    {
        // ── Identity ─────────────────────────────────────────────────────
        public string ExecutionId { get; set; } = string.Empty; // deterministic JobRef|Spec|Serial|Stage|ExecutionSequence

        public string JobRef { get; set; } = string.Empty;
        public string SourceProformaId { get; set; } = string.Empty; // PI
        public string JobOrderId { get; set; } = string.Empty; // JO (same as JobRef in this app)

        public int SpecGroupNumber { get; set; }
        public string SpecName { get; set; } = string.Empty;
        public int SerialNumber { get; set; }
        public string Position { get; set; } = string.Empty; // Outer/Inner/Lam Outer/Lam Inner/Glass/Unit

        public string PieceId { get; set; } = string.Empty; // original piece identity — never lost
        public string InputPieceId { get; set; } = string.Empty; // previous stage output
        public string OutputPieceId { get; set; } = string.Empty; // current output — same as PieceId if safe, else deterministic child P001-T01

        public ProductionProcessType ProcessType { get; set; }
        public int Sequence { get; set; } // execution sequence per piece per stage, deterministic

        public int InputWorkItemId { get; set; } // predecessor WorkItem Id
        public int OutputWorkItemId { get; set; } // current WorkItem Id (or next stage WorkItem Id for pairing/doubling)

        // ── Status — reuse existing WorkItemStatus ───────────────────────
        public WorkItemStatus Status { get; set; } = WorkItemStatus.Pending;

        // ── Operator / timing ────────────────────────────────────────────
        public string StartedBy { get; set; } = string.Empty;
        public DateTime? StartedAt { get; set; }
        public string CompletedBy { get; set; } = string.Empty;
        public DateTime? CompletedAt { get; set; }

        // ── Quantity / piece-flow ────────────────────────────────────────
        public int QtyRequired { get; set; }
        public int QtyDone { get; set; }

        // ── Entry / reason / notes ───────────────────────────────────────
        public int EntryType { get; set; } // ProductionEntryType Normal=0 Breakage=1 Rework=2 Foc=3 QcPassed=4 QcFailed=5 Cancelled=6
        public string Reason { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        // ── QC ───────────────────────────────────────────────────────────
        public int QcStatus { get; set; } // QcResult Pending=0 Passed=1 Failed=2 Rework=3
        public string QcId { get; set; } = string.Empty; // related QC record

        // ── Parent / rework ──────────────────────────────────────────────
        public string ParentExecutionId { get; set; } = string.Empty;
        public int ReworkSequence { get; set; } // 0 original, 1+ rework generation
        public bool IsFoc { get; set; }

        // ── Traceability ─────────────────────────────────────────────────
        public string PreviousStage { get; set; } = string.Empty; // e.g., Cutting
        public string NextStage { get; set; } = string.Empty; // e.g., Tempering

        // ── Extensible metadata — process-specific details as JSON ───────
        public string ProcessDetailJson { get; set; } = string.Empty; // Holes/Polish/EdgeDeletion/Mitring/Tempering/Pairing/Doubling details

        // ── Audit ────────────────────────────────────────────────────────
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Deterministic route for one spec row — which stages actually required, in canonical order.
    /// Route derived from specification and production requirements, not UI selections.
    /// </summary>
    public sealed class ProductionProcessRoute
    {
        public string JobRef { get; set; } = string.Empty;
        public string SourceProformaId { get; set; } = string.Empty;
        public int SpecGroupNumber { get; set; }
        public string SpecName { get; set; } = string.Empty;
        public int SerialNumber { get; set; }
        public string Position { get; set; } = string.Empty;
        public string BuildType { get; set; } = string.Empty; // Sgu/Dgu/Lam/DguLam
        public string GlassRef { get; set; } = string.Empty;

        public string PieceId { get; set; } = string.Empty; // deterministic identity for this row's piece

        public System.Collections.Generic.List<ProductionProcessType> RequiredStages { get; set; } = new();

        public string RouteLabel => string.Join(" → ", RequiredStages);

        public bool RequiresStage(ProductionProcessType stage) => RequiredStages.Contains(stage);

        public ProductionProcessType? NextRequiredAfter(ProductionProcessType current)
        {
            int idx = RequiredStages.IndexOf(current);
            if (idx < 0 || idx + 1 >= RequiredStages.Count) return null;
            return RequiredStages[idx + 1];
        }

        public ProductionProcessType? PreviousRequiredBefore(ProductionProcessType current)
        {
            int idx = RequiredStages.IndexOf(current);
            if (idx <= 0) return null;
            return RequiredStages[idx - 1];
        }
    }
}
