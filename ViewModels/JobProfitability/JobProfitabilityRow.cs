// ═══════════════════════════════════════════════════════════════════════
// FILE: JobProfitabilityRow.cs
// PROJECT: ProGlassAutomation
//
// JobProfitabilityRow split out of JobProfitabilityViewModel.cs (Phase 11/12 God-VM split) — behaviour unchanged.
//
// TARGET-PRICE FIELDS (approved KPI):
//   Every job row carries the persisted Step 9 target-price result:
//   given price, final required target, difference and margin against
//   target — plus an explicit validity state so a job without target data
//   is reported as INCOMPLETE/STALE instead of a fake 0% margin.
// ═══════════════════════════════════════════════════════════════════════

using System;
using ProGlassAutomation.Services.ProformaInvoice.TargetPricing;

namespace ProGlassAutomation.ViewModels.JobProfitability
{
    /// <summary>
    /// Where a job's ACHIEVED margin sits relative to the company's BASE
    /// expected profit margin (Settings → Features → Proforma Invoice →
    /// Step 9 → Profit margin %, 15% default). This compares the actual P&amp;L
    /// margin against the plan — it is deliberately separate from the
    /// target-PRICE model (given price vs required target).
    /// </summary>
    public enum JobMarginBaseBand
    {
        /// <summary>Job has no P&amp;L data (not costed) — never classed.</summary>
        Unknown,
        AboveBase,
        AtBase,
        BelowBase
    }

    /// <summary>
    /// Pure banding rules shared by the row chips, the portfolio verdicts and
    /// the unit tests. A margin within <see cref="TolerancePercent"/> of the
    /// base counts as AT the base (0.05pp — far tighter than the 0.1pp the UI
    /// can render, so "15.0% vs 15%" reads as at-base, 15.1% as above).
    /// </summary>
    public static class JobMarginBands
    {
        public const decimal TolerancePercent = 0.05m;

        public static JobMarginBaseBand Classify(decimal achievedMargin, decimal baseMargin)
        {
            if (baseMargin <= 0m) return JobMarginBaseBand.Unknown;
            if (achievedMargin < baseMargin - TolerancePercent) return JobMarginBaseBand.BelowBase;
            if (achievedMargin > baseMargin + TolerancePercent) return JobMarginBaseBand.AboveBase;
            return JobMarginBaseBand.AtBase;
        }

        /// <summary>Compact chip for a row: ▲ +1.0pp / ▼ -1.0pp / ＝ 15%.</summary>
        public static string Chip(decimal achievedMargin, decimal baseMargin)
        {
            var band = Classify(achievedMargin, baseMargin);
            if (band == JobMarginBaseBand.Unknown) return "";
            if (band == JobMarginBaseBand.AtBase) return $"＝ {Math.Round(baseMargin, 1):0.#}%";
            decimal diff = Math.Round(achievedMargin - baseMargin, 1);
            return band == JobMarginBaseBand.AboveBase
                ? $"▲ +{diff:0.0}pp"
                : $"▼ {diff:0.0}pp";
        }
    }

    public class JobProfitabilityRow : ViewModelBase
    {
        public string ProformaId { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string Salesman { get; set; } = "";
        public DateTime IssueDate { get; set; }

        /// <summary>Revenue excluding VAT, as recorded by Step 9.</summary>
        public decimal NetExVat { get; set; }

        /// <summary>Glass cost + production cost.</summary>
        public decimal TotalCost { get; set; }

        public decimal Profit { get; set; }

        /// <summary>Cost-based margin saved by Step 9 (without U-insert).</summary>
        public decimal MarginPercent { get; set; }

        /// <summary>
        /// Overall margin including U-insert: (Gross − Total Cost) / Total Cost × 100.
        /// Defaults to 0 for old snapshots that didn't persist it.
        /// </summary>
        public decimal OverallMarginPercent { get; set; }

        /// <summary>Net margin: (Gross − Total Cost) / Gross × 100.</summary>
        public decimal NetMarginPercent { get; set; }

        /// <summary>True when this job lost money (actual P&L basis).</summary>
        public bool IsLoss { get; set; }

        /// <summary>True when the stored P&amp;L was produced by the pre-fix DGU/LAM costing.</summary>
        public bool NeedsRecalculation { get; set; }

        /// <summary>Why it is flagged. Empty when it is not.</summary>
        public string RecalculationWarning { get; set; } = "";

        /// <summary>
        /// False for invoices saved before Step 9 existed (no stored P&amp;L).
        /// </summary>
        public bool IsCosted { get; set; }

        // ══════════════════════════════════════════════════════════════
        //  TARGET-PRICE MODEL — persisted Step 9 target result
        // ══════════════════════════════════════════════════════════════

        /// <summary>True when the snapshot contains target-price data.</summary>
        public bool HasTargetData { get; set; }

        /// <summary>Job total Step 5 given value (Σ given × area).</summary>
        public decimal TargetGivenValue { get; set; }

        /// <summary>Job total required target value (Σ final target × area).</summary>
        public decimal TargetRequiredValue { get; set; }

        /// <summary>Job total difference (given value − required value).</summary>
        public decimal TargetDifferenceValue { get; set; }

        /// <summary>Area-weighted average Step 5 given price (AED/m²).</summary>
        public decimal TargetGivenPerSqm { get; set; }

        /// <summary>Area-weighted average final required target (AED/m²).</summary>
        public decimal TargetRequiredPerSqm { get; set; }

        /// <summary>Null when the job margin is not calculable.</summary>
        public decimal? TargetMarginAgainstTargetPercent { get; set; }

        /// <summary>Aggregate U-insert treatment summary from the job.</summary>
        public string UInsertTreatmentSummary { get; set; } = "";

        /// <summary>Raw persisted status (enum name).</summary>
        public string TargetStatus { get; set; } = "";

        /// <summary>How many specifications carried a valid target margin.</summary>
        public int TargetValidSpecCount { get; set; }

        /// <summary>How many specifications were incomplete/missing inputs.</summary>
        public int TargetIncompleteSpecCount { get; set; }

        /// <summary>Snapshot has cost data but no target block (saved pre-upgrade).</summary>
        public bool IsSnapshotStale { get; set; }

        // ── Display helpers (target) ─────────────────────────────────────

        public string GivenPerSqmDisplay => HasTargetData
            ? $"AED {TargetGivenPerSqm:N2}/m²"
            : "—";

        public string RequiredPerSqmDisplay => HasTargetData
            ? $"AED {TargetRequiredPerSqm:N2}/m²"
            : "—";

        public string DifferenceDisplay
        {
            get
            {
                if (!HasTargetData) return "—";
                return TargetDifferenceValue < 0m
                    ? $"-AED {Math.Abs(TargetDifferenceValue):N2}"
                    : $"AED {TargetDifferenceValue:N2}";
            }
        }

        public string DifferencePerSqmDisplay
        {
            get
            {
                if (!HasTargetData) return "—";
                decimal diff = TargetGivenPerSqm - TargetRequiredPerSqm;
                return diff < 0m
                    ? $"-AED {Math.Abs(diff):N2}/m²"
                    : $"AED {diff:N2}/m²";
            }
        }

        public string TargetMarginDisplay =>
            TargetMarginAgainstTargetPercent switch
            {
                null => "—",
                var m => $"{m.Value:0.##}%"
            };

        /// <summary>Status shown in the row: ABOVE/AT/BELOW TARGET, INCOMPLETE, STALE.</summary>
        public string TargetStatusLabel
        {
            get
            {
                if (!HasTargetData)
                    return IsCosted ? "STALE" : "INCOMPLETE";

                if (TargetMarginAgainstTargetPercent.HasValue
                    && !string.IsNullOrWhiteSpace(TargetStatus))
                {
                    // NOTE: the row's own string property is ALSO named
                    // `TargetStatus`, so in this instance context the enum
                    // must be reached through its full name — a bare
                    // `TargetStatus.X` would bind to the string property and
                    // fail with CS1061.
                    if (Enum.TryParse<ProGlassAutomation.Services.ProformaInvoice.TargetPricing.TargetStatus>(
                            TargetStatus, out var parsed))
                    {
                        return parsed switch
                        {
                            ProGlassAutomation.Services.ProformaInvoice.TargetPricing.TargetStatus.AboveTarget => "ABOVE TARGET",
                            ProGlassAutomation.Services.ProformaInvoice.TargetPricing.TargetStatus.AtTarget => "AT TARGET",
                            ProGlassAutomation.Services.ProformaInvoice.TargetPricing.TargetStatus.BelowTarget => "BELOW TARGET",
                            _ => parsed.ToString().ToUpperInvariant().Replace("_", " ")
                        };
                    }
                }

                return IsSnapshotStale ? "STALE" : "INCOMPLETE";
            }
        }

        public bool IsAboveTarget => TargetStatusLabel == "ABOVE TARGET";
        public bool IsBelowTarget => TargetStatusLabel == "BELOW TARGET";
        public bool IsTargetIncomplete => TargetStatusLabel == "INCOMPLETE";
        public bool IsTargetStale => TargetStatusLabel == "STALE";
        public bool HasValidTargetMargin => TargetMarginAgainstTargetPercent.HasValue;

        // ── Display helpers (legacy, kept for reporting) ────────────────

        public string IssueDateDisplay => IssueDate == default
            ? "—"
            : IssueDate.ToString("dd MMM yyyy");

        public string NetDisplay => $"AED {NetExVat:N2}";
        public string CostDisplay => $"AED {TotalCost:N2}";
        public string ProfitDisplay => $"AED {Math.Abs(Profit):N2}";

        /// <summary>
        /// Legacy margin display (actual P&amp;L) with U-insert preference —
        /// kept for the reporting tabs.
        /// </summary>
        public string MarginDisplay
        {
            get
            {
                if (!IsCosted) return "not costed";
                var pct = OverallMarginPercent != 0m ? OverallMarginPercent : MarginPercent;
                return $"{pct:0.#}%";
            }
        }

        // ── Achieved margin vs the company BASE expected margin ─────────
        // The base is the Settings default (Step 9 → Profit margin %, 15%)
        // captured by the parent VM at load time. The achieved margin is the
        // same number the MARGIN column shows (OverallMarginPercent with the
        // legacy MarginPercent fallback for pre-U-insert snapshots).

        /// <summary>The company base expected margin (%) this job is judged
        /// against. Assigned by JobProfitabilityViewModel from the live
        /// Settings default when the list loads.</summary>
        public decimal BaseExpectedMarginPercent { get; set; } = 15m;

        /// <summary>The job's effective achieved margin (%) — the number the
        /// MARGIN column renders and the bands compare.</summary>
        public decimal EffectiveMarginPercent => IsCosted
            ? (OverallMarginPercent != 0m ? OverallMarginPercent : MarginPercent)
            : 0m;

        /// <summary>True when the job has P&amp;L data to compare against the base.</summary>
        public bool HasBaseMarginComparison => IsCosted;

        public JobMarginBaseBand MarginVsBaseBand => HasBaseMarginComparison
            ? JobMarginBands.Classify(EffectiveMarginPercent, BaseExpectedMarginPercent)
            : JobMarginBaseBand.Unknown;

        public bool IsMarginAboveBase => MarginVsBaseBand == JobMarginBaseBand.AboveBase;
        public bool IsMarginAtBase => MarginVsBaseBand == JobMarginBaseBand.AtBase;
        public bool IsMarginBelowBase => MarginVsBaseBand == JobMarginBaseBand.BelowBase;

        /// <summary>Compact chip shown under the MARGIN value: ▲ +1.0pp,
        /// ▼ -1.0pp or ＝ 15%.</summary>
        public string MarginVsBaseChip => HasBaseMarginComparison
            ? JobMarginBands.Chip(EffectiveMarginPercent, BaseExpectedMarginPercent)
            : "";

        /// <summary>Customer and project on one line for the compact row.</summary>
        public string CustomerProjectDisplay =>
            string.IsNullOrWhiteSpace(ProjectName)
                ? CustomerName
                : $"{CustomerName} · {ProjectName}";

        public string SalesmanDisplay =>
            string.IsNullOrWhiteSpace(Salesman) ? "—" : Salesman;
    }
}
