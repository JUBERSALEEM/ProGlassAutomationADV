// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/ProformaInvoice/Step6OtherCharges/SpecGroupMetric.cs
// ═══════════════════════════════════════════════════════════════════════
// CHAT 17 — NEW FILE
//
//   Simple POCO snapshot of one spec group's aggregated metrics.
//   Populated by MetricDiscoveryHubViewModel.Refresh() per spec group.
//   Consumed by OtherChargesViewModel.BuildSourcePool() to generate
//   per-spec GridSourceOption entries in the "Charge Basis" dropdown.
//
//   NOT a ViewModel — no INotifyPropertyChanged. Rebuilt fresh
//   on every Refresh() call. Immutable after construction.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges
{
    /// <summary>
    /// Aggregated metrics for a single spec group.
    /// One instance per spec group, rebuilt on every MetricHub Refresh.
    /// </summary>
    public class SpecGroupMetric
    {
        /// <summary>Spec group number (1-based, matches GroupNumber on SpecGroup VM).</summary>
        public int SpecGroupNumber { get; set; }

        /// <summary>
        /// Display name for dropdown grouping header.
        /// Format: "SPEC 1 — 6mm Clear Tempered"
        /// Built from SpecGroup.GroupNumber + SpecGroup.SelectedSpecName.
        /// </summary>
        public string SpecDisplayName { get; set; } = "";

        /// <summary>Selected glass type code: SGU / DGU / LAM / DGU+LAM.</summary>
        public string GlassType { get; set; } = "";

        /// <summary>True for DGU, LAM, DGU+LAM — has two glass panes per unit.</summary>
        public bool IsDoubleGlass { get; set; }

        // ── Perimeter (Linear Meters) ──────────────────────────────────

        /// <summary>LM1 = sum of 2×(W1+H1)/1000 × Qty across all rows in this spec group.</summary>
        public decimal Lm1 { get; set; }

        /// <summary>LM2 = sum of 2×(W2+H2)/1000 × Qty (zero for SGU).</summary>
        public decimal Lm2 { get; set; }

        /// <summary>LM1 + LM2.</summary>
        public decimal LmCombined => Lm1 + Lm2;

        // ── Area (Square Meters) ───────────────────────────────────────

        /// <summary>SQM1 = sum of (W1×H1)/1000000 × Qty.</summary>
        public decimal Sqm1 { get; set; }

        /// <summary>SQM2 = sum of (W2×H2)/1000000 × Qty (zero for SGU).</summary>
        public decimal Sqm2 { get; set; }

        /// <summary>SQM1 + SQM2.</summary>
        public decimal SqmCombined => Sqm1 + Sqm2;

        // ── Quantity ───────────────────────────────────────────────────

        /// <summary>Total pieces (sum of Qty across all rows).</summary>
        public int Qty { get; set; }

        /// <summary>
        /// Physical pane count:
        ///   SGU → Qty × 1
        ///   DGU/LAM/DGU+LAM → Qty × 2
        /// </summary>
        public int PhysicalPanes => IsDoubleGlass ? Qty * 2 : Qty;
    }
}