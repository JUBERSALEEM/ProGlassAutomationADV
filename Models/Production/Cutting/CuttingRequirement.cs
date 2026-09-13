// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Production/Cutting/CuttingRequirement.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 15 Cutting Requirement Generation
//   Real transformation JO / Production Work Items → Cutting Requirements
//   Preserves complete traceability, no anonymous rectangles.
//   Every requirement knows JobRef PI JO Spec SpecNo Serial Position
//   W/H Qty Glass type Color Thickness Processing flags Rotation Margin
//   Priority EntryType Parent/replacement.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Optimization;

namespace ProGlassAutomation.Models.Production.Cutting
{
    /// <summary>
    /// One cutting requirement — one physical glass piece that must be cut.
    /// Deterministic piece identity, preserves spec identity, not anonymous.
    /// </summary>
    public sealed class CuttingRequirement
    {
        // ── Traceability ──────────────────────────────────────────────
        public string JobRef { get; set; } = string.Empty;           // JO number
        public string SourceProformaId { get; set; } = string.Empty; // PI number
        public int SpecGroupNumber { get; set; }                      // SpecNo
        public string SpecName { get; set; } = string.Empty;          // Specification name
        public string BuildType { get; set; } = string.Empty;         // SGU/DGU/LAM/DguLam
        public int SerialNumber { get; set; }                         // Step5 serial
        public string GlassRef { get; set; } = string.Empty;          // Row ref G-101 etc
        public string Position { get; set; } = string.Empty;          // Outer/Inner/Lam Outer etc
        public int WidthMm { get; set; }
        public int HeightMm { get; set; }
        public int Quantity { get; set; } = 1;                        // Usually 1 per expanded piece, but requirement may aggregate

        // ── Material identity ─────────────────────────────────────────
        public string GlassFamily { get; set; } = string.Empty;       // Category: Clear, Low-E, HD Grey etc
        public string Color { get; set; } = string.Empty;             // Color: Clear, Grey, etc
        public int ThicknessMm { get; set; }
        public string Coating { get; set; } = string.Empty;           // Low-E, etc
        public string Treatment { get; set; } = string.Empty;         // FT, Annealed, Heat Strengthened
        public bool RequiresTempering { get; set; }

        // ── Processing flags ──────────────────────────────────────────
        public string ProcessingFlags { get; set; } = string.Empty;   // e.g., Polish, Holes, Mitring notes

        // ── Optimization hints ────────────────────────────────────────
        public RotationPolicy RotationPolicy { get; set; } = RotationPolicy.Free90;
        public int MarginProfileThickness { get; set; }               // For MarginProfile.DefaultFor
        public int Priority { get; set; } = 5;                        // 1..10, 5=Medium

        // ── Piece identity ────────────────────────────────────────────
        public string PieceId { get; set; } = string.Empty;           // Deterministic: JobRef|Spec|Serial|Position|W*H|Index
        public string PieceLabel { get; set; } = string.Empty;        // Human: Piece-001, G-101·3/24 etc
        public int PieceIndex { get; set; }                           // 1..Qty per serial
        public int PieceSequence { get; set; }                        // Global sequence for deterministic ordering

        // ── Entry / Parent / Replacement ──────────────────────────────
        public ProductionEntryType EntryType { get; set; } = ProductionEntryType.Normal;
        public int? ParentWorkItemId { get; set; }
        public int ReplacementSequence { get; set; }
        public bool IsFoc { get; set; }
        public bool IsCancelled { get; set; }
        public string Notes { get; set; } = string.Empty;

        // ── Source WorkItem ───────────────────────────────────────────
        public int SourceWorkItemId { get; set; }                     // SavedProductionWorkItem.Id
        public string UniqueKey { get; set; } = string.Empty;         // Deterministic UniqueKey

        // ── Helpers ───────────────────────────────────────────────────
        public string MaterialKey => $"{GlassFamily}|{Color}|{ThicknessMm}|{Coating}|{Treatment}".Trim('|');
        public string SizeKey => $"{WidthMm}x{HeightMm}";
        public decimal AreaSqm => (decimal)WidthMm * HeightMm / 1_000_000m;

        public CutPiece ToCutPiece()
        {
            return new CutPiece
            {
                JobRef = JobRef,
                GlassRef = GlassRef,
                SpecLabel = SpecName,
                Position = Position,
                Color = Color,
                ThicknessMm = ThicknessMm,
                WidthMm = WidthMm,
                HeightMm = HeightMm,
                AllowRotate = RotationPolicy != RotationPolicy.Locked,
                PieceLabel = string.IsNullOrWhiteSpace(PieceLabel) ? PieceId : PieceLabel
            };
        }
    }

    /// <summary>
    /// Group of cutting requirements that share physically compatible material.
    /// </summary>
    public sealed class MaterialGroup
    {
        public string GroupId { get; set; } = string.Empty;           // Deterministic: Family|Color|Thickness|Coating|Treatment
        public string GlassFamily { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int ThicknessMm { get; set; }
        public string Coating { get; set; } = string.Empty;
        public string Treatment { get; set; } = string.Empty;

        public List<CuttingRequirement> Requirements { get; set; } = new();
        public int TotalPieces => Requirements.Sum(r => r.Quantity);
        public decimal TotalAreaSqm => Requirements.Sum(r => r.AreaSqm * r.Quantity);

        public string DisplayLabel => $"{GlassFamily} {Color} {ThicknessMm}mm {Coating} {Treatment}".Trim();
    }
}
