// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Production/ProductionModels.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — distribution domain (Phase 1, 2026-09-11)
//
// Pure POCOs describing how a released Job Order (or Proforma Invoice)
// fans out into stage-level work items for the eight production modules:
// Cutting · Holes & Cutouts · Polish · Edge Deletion · Mitring ·
// Tempering · Pairing · Doubling/Lamination.
//
// See docs/PRODUCTION_DISTRIBUTION_PLAN_2026-09-11.md for the routing
// matrix these models serve. No WPF, no EF — the persistence entity in
// Services/Production mirrors this shape.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Generic;

namespace ProGlassAutomation.Models.Production
{
    /// <summary>
    /// The eight physical production stages, in canonical floor order.
    /// Names map 1:1 to the navigation module keys (Cutting, HolesCutouts,
    /// Polish, EdgeDeletion, Mitring, Tempering, Pairing,
    /// DoublingLamination).
    /// </summary>
    public enum ProductionStage
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

    /// <summary>Work-item lifecycle on the factory floor.</summary>
    public enum WorkItemStatus
    {
        Pending = 0,
        InProgress = 1,
        Completed = 2
    }

    /// <summary>Which Step 5 dimension pair a pane occupies.</summary>
    public enum PaneSlot
    {
        /// <summary>W1/H1 — every build's first pane.</summary>
        Pane1 = 1,

        /// <summary>W2/H2 — DGU/LAM inner pane; DGU+LAM laminated pair; triple panes 2-3.</summary>
        Pane2 = 2
    }

    /// <summary>
    /// One stage visit for one spec row (or one Step 6 charge batch).
    /// Identity = <see cref="UniqueKey"/> — deterministic, so a re-release
    /// of the same job upserts instead of duplicating.
    /// </summary>
    public class ProductionWorkItem
    {
        /// <summary>
        /// Deterministic identity: JobRef | SpecNo | Serial | Position |
        /// Stage (charge items append |CHG:TemplateKey). Built by the
        /// distribution engine — never hand-typed.
        /// </summary>
        public string UniqueKey { get; set; } = "";

        // ── Provenance ────────────────────────────────────────────────────
        public string JobRef { get; set; } = "";
        public string SourceProformaId { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ProjectName { get; set; } = "";

        /// <summary>Sales representative from the invoice Client snapshot.</summary>
        public string Salesman { get; set; } = "";
        public int SpecGroupNumber { get; set; }
        public string SpecName { get; set; } = "";

        /// <summary>GlassBuildType enum name (Sgu/Dgu/Lam/DguLam).</summary>
        public string BuildType { get; set; } = "";

        public int SerialNumber { get; set; }
        public string GlassRef { get; set; } = "";

        /// <summary>"Outer", "Inner", "Middle", "Lam Outer", "Lam Inner" or "" for charge batches.</summary>
        public string Position { get; set; } = "";

        // ── Geometry & quantities ────────────────────────────────────────
        public int WidthMm { get; set; }
        public int HeightMm { get; set; }
        public int Qty { get; set; }

        /// <summary>Unit area of this pane (W×H), SQM, 3 dp.</summary>
        public decimal UnitSqm { get; set; }

        /// <summary>UnitSqm × Qty.</summary>
        public decimal TotalSqm { get; set; }

        /// <summary>Perimeter LM for this pane, 3 dp (0 when not applicable).</summary>
        public decimal TotalLm { get; set; }

        /// <summary>FT / Annealed / Heat Strengthened / Heat Soaked / "".</summary>
        public string Treatment { get; set; } = "";

        // ── Stage ────────────────────────────────────────────────────────
        public ProductionStage Stage { get; set; }
        public WorkItemStatus Status { get; set; } = WorkItemStatus.Pending;

        /// <summary>
        /// Stage that must complete before this one starts ("" = none).
        /// Encodes the factory precedence: cutting → edge work/holes →
        /// tempering → pairing → doubling/lamination.
        /// </summary>
        public string PredecessorStage { get; set; } = "";

        // ── Step 6 charge linkage (charge-driven items only) ────────────
        /// <summary>Step 6 template key when this item came from a charge ("FAN_HOLES", "POLISH", …). "" for spec-driven items.</summary>
        public string ChargeTemplateKey { get; set; } = "";

        /// <summary>Charge metric quantity — hole count, polish LM, etc.</summary>
        public decimal ChargeMetricQty { get; set; }

        /// <summary>Charge metric unit label (QTY / LM / SQM).</summary>
        public string ChargeMetricUnit { get; set; } = "";

        /// <summary>Polish sub-type display label ("All Side Polish", …) when relevant.</summary>
        public string PolishSubTypeLabel { get; set; } = "";

        /// <summary>Extra production notes (argon, spacer, interlayer, warnings).</summary>
        public string Notes { get; set; } = "";
    }

    /// <summary>
    /// Complete distribution result for one release: header + every work
    /// item + honest warnings (never silently dropped data).
    /// </summary>
    public class ProductionJobPlan
    {
        public string JobRef { get; set; } = "";
        public string SourceProformaId { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ProjectName { get; set; } = "";

        /// <summary>Sales representative from the invoice Client snapshot.</summary>
        public string Salesman { get; set; } = "";

        public List<ProductionWorkItem> Items { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        public int ItemCount => Items.Count;
        public bool HasWarnings => Warnings.Count > 0;
    }
}
