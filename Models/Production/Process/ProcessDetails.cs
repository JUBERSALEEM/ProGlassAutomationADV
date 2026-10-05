// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Production/Process/ProcessDetails.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 16 Process-Specific Details
//   Holes/Cutouts, Polish, EdgeDeletion, Mitring, Tempering, Pairing,
//   Doubling/Lamination — traceable production data, not full CAD.
//   If existing specification models already contain hole/cutout data, reuse it,
//   do not duplicate specification geometry model.
//   Use extensible metadata only where necessary, no invented machine-specific
//   fields if application does not have them.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Generic;

namespace ProGlassAutomation.Models.Production.Process
{
    // ── Holes / Cutouts ──────────────────────────────────────────────────

    public sealed class HolesCutoutsDetail
    {
        public int HoleCount { get; set; }
        public int CutoutCount { get; set; }
        public List<HoleInfo> Holes { get; set; } = new();
        public List<CutoutInfo> Cutouts { get; set; } = new();
        public string ToolInfo { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class HoleInfo
    {
        public double DiameterMm { get; set; }
        public double Xmm { get; set; }
        public double Ymm { get; set; }
        public string Shape { get; set; } = string.Empty; // Round, CounterSunk, etc
        public string EdgeRef { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class CutoutInfo
    {
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public double Xmm { get; set; }
        public double Ymm { get; set; }
        public string Shape { get; set; } = string.Empty; // Rectangular, Notch, etc
        public string EdgeRef { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    // ── Polish ───────────────────────────────────────────────────────────

    public sealed class PolishDetail
    {
        public string PolishType { get; set; } = string.Empty; // e.g., All Side Polish, Edge Grinding
        public List<string> Edges { get; set; } = new(); // Top, Bottom, Left, Right, All
        public int EdgeCount { get; set; }
        public decimal QuantityLm { get; set; }
        public string OperatorName { get; set; } = string.Empty;
        public string Machine { get; set; } = string.Empty;
        public string Workstation { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    // ── Edge Deletion ────────────────────────────────────────────────────

    public sealed class EdgeDeletionDetail
    {
        public List<string> RequiredEdges { get; set; } = new();
        public int EdgeCount { get; set; }
        public string OperatorName { get; set; } = string.Empty;
        public string Machine { get; set; } = string.Empty;
        public string Workstation { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    // ── Mitring ──────────────────────────────────────────────────────────

    public sealed class MitringDetail
    {
        public List<MitringEdge> Edges { get; set; } = new();
        public string OperatorName { get; set; } = string.Empty;
        public string Machine { get; set; } = string.Empty;
        public string Workstation { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class MitringEdge
    {
        public string Edge { get; set; } = string.Empty; // Top, Bottom, Left, Right
        public double? AngleDeg { get; set; } // null = not specified, do NOT assume 45°
        public string Notes { get; set; } = string.Empty;
    }

    // ── Tempering ────────────────────────────────────────────────────────

    public sealed class TemperingDetail
    {
        public string TemperingRequirement { get; set; } = string.Empty; // FT, Heat Strengthened, Heat Soaked
        public string HeatRequirement { get; set; } = string.Empty;
        public string BatchId { get; set; } = string.Empty;
        public string OperatorName { get; set; } = string.Empty;
        public string Machine { get; set; } = string.Empty;
        public string Furnace { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    // ── Pairing ──────────────────────────────────────────────────────────

    public sealed class PairingDetail
    {
        public string PairId { get; set; } = string.Empty; // deterministic Pair-001
        public string OuterPieceId { get; set; } = string.Empty;
        public string InnerPieceId { get; set; } = string.Empty;
        public string OuterGlassRef { get; set; } = string.Empty;
        public string InnerGlassRef { get; set; } = string.Empty;
        public int OuterWorkItemId { get; set; }
        public int InnerWorkItemId { get; set; }
        public string JobRef { get; set; } = string.Empty;
        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }
        public string Position { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    // ── Doubling / Lamination ────────────────────────────────────────────

    public sealed class DoublingLaminationDetail
    {
        public string AssemblyId { get; set; } = string.Empty; // deterministic DGU-001 or LAM-001
        public string AssemblyType { get; set; } = string.Empty; // DGU, Lamination, DGU+LAM
        public List<AssemblyComponent> Components { get; set; } = new();
        public string SpacerInfo { get; set; } = string.Empty; // e.g., 12mm Air, Argon
        public string InterlayerInfo { get; set; } = string.Empty; // e.g., PVB 0.76mm Clear
        public string SealantInfo { get; set; } = string.Empty;
        public string OperatorName { get; set; } = string.Empty;
        public string Machine { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class AssemblyComponent
    {
        public string PieceId { get; set; } = string.Empty; // original piece identity preserved
        public string GlassRef { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty; // Outer/Inner/Lam Outer/Lam Inner
        public int WorkItemId { get; set; }
        public string ExecutionId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // Outer, Inner, Interlayer, Spacer
    }
}
