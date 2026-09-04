// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/ProformaInvoice/TargetPricing/TargetPriceModels.cs
// PROJECT: ProGlassAutomation
// MODULE: Shared target-price calculation model (Step 9 & Job Profitability)
//
// AUTHORITATIVE BUSINESS MODEL (approved by the customer):
//   • The GIVEN selling price is the Step 5 specification Base Price.
//   • The SPECIFICATION TARGET is the Step 4 specification formula result
//     lifted by the approved target uplift (15%):
//         DGU: ((Outer Glass Rate + Inner Glass Rate) ÷ Yield + ASP Rate) × 1.15
//   • U-INSERT ACCOUNTING (revised 2026-09-04 — the Step 6 row toggle
//     decides the treatment; the required target NEVER moves):
//         Toggle ON  (Treatment = Additional): the U-insert is billed as its
//             own Step 6 line on top of the given price — the FINAL REQUIRED
//             TARGET stays the NORMAL specification target and the rate is
//             display-only.
//         Toggle OFF (Treatment = IncludedInStep5Price): the U-insert is
//             covered INSIDE the Step 5 given price (the customer is quoted
//             one all-in price) — the FINAL REQUIRED TARGET STILL stays the
//             NORMAL specification target. Adding the rate would double-count
//             it: the given price already includes the U-insert, so the KPI
//             would punish a profitable quotation (e.g. 125 given ≈ 20% over
//             a 103.82 basis judged against a folded 129.39 instead of the
//             base-price 119.39). The rate is visible on the KPI as
//             informational only.
//   • The only approved KPI margin:
//         Margin Against Target =
//             (Given Price − Final Required Target) ÷ Final Required Target × 100
//
// These types are deliberately immutable POCOs (no WPF dependency) so the
// same numbers drive Step 9, the saved invoice snapshot, Job Profitability
// and the unit tests — one calculation model, no duplicated formulas.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.ProformaInvoice.TargetPricing
{
    /// <summary>
    /// Physical build of a specification. The calculator never guesses from
    /// the live Step 4 selection alone: the caller (Step 5 group adapter)
    /// resolves it from the spec name first, exactly as Step 9 costing does.
    /// </summary>
    public enum GlassBuildType
    {
        Unknown = 0,
        Sgu,
        Dgu,
        Lam,
        DguLam
    }

    /// <summary>
    /// Accounting treatment of the U-insert extra. Deliberately independent
    /// from the Step 6 automation toggle: automation decides WHICH charge
    /// rows are suggested; this decides how the KPI should READ the U-insert.
    ///
    /// Revised rule (2026-09-04): neither treatment moves the required
    /// target — the KPI always judges the base-price specification target.
    ///   • Step 6 U-insert row ACTIVE for the spec (billed as its own
    ///     Step 6 line)  → <see cref="Additional"/>. The rate is carried for
    ///     display only; the target stays the normal specification target.
    ///   • Step 6 U-insert row NOT active (U-insert covered by the Step 5
    ///     given price)   → <see cref="IncludedInStep5Price"/>. The given
    ///     price already contains the U-insert, so folding the rate into the
    ///     target would double-count it; the rate is shown on the KPI as
    ///     informational only and the target stays the base-price target.
    /// </summary>
    public enum UInsertTreatment
    {
        /// <summary>Specification carries no U-insert extra.</summary>
        NotApplicable = 0,

        /// <summary>
        /// Step 6 U-insert row is NOT active — the U-insert is expected to be
        /// covered inside the Step 5 given price. The final required target
        /// remains the base-price specification target (the rate is shown for
        /// reference only and is never added).
        /// </summary>
        IncludedInStep5Price,

        /// <summary>
        /// Step 6 U-insert row IS active for this spec — the extra is billed
        /// separately on top of the given price. The final required target is
        /// the normal specification target; the rate is display-only.
        /// </summary>
        Additional,

        /// <summary>
        /// The specification requires a U-insert but the treatment cannot be
        /// resolved (no rate anywhere). Never silently treated as zero.
        /// </summary>
        Unresolved
    }

    /// <summary>
    /// Validation states exposed by the target-price model. "Missing…" states
    /// must never render a fake 0% margin — the UI shows "—" plus a warning.
    /// </summary>
    public enum TargetStatus
    {
        /// <summary>Calculation complete and currently valid.</summary>
        UpToDate = 0,

        BelowTarget,
        AtTarget,
        AboveTarget,
        MissingBasePrice,
        MissingGlassRate,
        MissingAspRate,
        MissingYield,
        MissingUInsertTreatment,
        CalculationIncomplete,
        SnapshotStale
    }

    /// <summary>
    /// One specification's resolved rate card — the inputs the target formula
    /// consumes. Kept granular so the UI can print the formula with the real
    /// numbers and so unit tests can drive the calculator directly.
    /// </summary>
    public sealed class SpecificationRateCard
    {
        public GlassBuildType BuildType { get; set; } = GlassBuildType.Unknown;

        /// <summary>SGU glass base rate / DGU outer / LAM outer / DGU+LAM outer pane.</summary>
        public decimal OuterGlassRatePerSqm { get; set; }

        /// <summary>Second pane rate (DGU inner, LAM inner). 0 for SGU.</summary>
        public decimal InnerGlassRatePerSqm { get; set; }

        /// <summary>DGU+LAM third (laminated) pane rate. 0 otherwise.</summary>
        public decimal ThirdGlassRatePerSqm { get; set; }

        /// <summary>ASP/spacer per-SQM rate (DGU, DGU+LAM). 0 otherwise.</summary>
        public decimal AspRatePerSqm { get; set; }

        /// <summary>
        /// Remaining per-SQM formula add-ons for the build type, already
        /// summed by the caller:
        ///   SGU    = cutting + tempering
        ///   LAM    = PVB material + cutting (outer+inner) + tempering (outer+inner)
        ///   DGU    = 0 (the ASP row already includes cutting/tempering/production)
        ///   DGU+LAM = interlayer sell + cutting (all panes) + tempering (all panes)
        /// </summary>
        public decimal ProcessingRatePerSqm { get; set; }

        /// <summary>Yield factor (0.85 in the approved example).</summary>
        public decimal YieldFactor { get; set; }

        /// <summary>
        /// Target uplift applied to the specification formula result.
        /// The approved model fixes this at 15%.
        /// </summary>
        public decimal TargetUpliftPercent { get; set; } = SpecificationTargetCalculator.DefaultTargetUpliftPercent;
    }

    /// <summary>
    /// U-insert resolution: treatment + resolved dynamic rate + warnings.
    /// </summary>
    public sealed class UInsertResolution
    {
        public UInsertTreatment Treatment { get; set; } = UInsertTreatment.NotApplicable;

        /// <summary>
        /// Resolved per-SQM rate read dynamically from Step 6 (seed order:
        /// Step 4 per-spec seed → Step 6 U-insert row rate → Step 6 template
        /// default). Carried for KPI display in both treatments; 0 when the
        /// spec carries no U-insert or no rate is configured anywhere.
        /// </summary>
        public decimal RatePerSqm { get; set; }

        /// <summary>Warning text when the configuration is broken/missing.</summary>
        public string Warning { get; set; } = "";

        public bool HasWarning => !string.IsNullOrWhiteSpace(Warning);
    }

    /// <summary>
    /// Target-price result for ONE specification group.
    /// </summary>
    public sealed class SpecTargetPriceResult
    {
        public int SpecNumber { get; set; }
        public string SpecName { get; set; } = "";
        public string GlassTypeCode { get; set; } = "";
        public GlassBuildType BuildType { get; set; } = GlassBuildType.Unknown;

        // ── Step 5 inputs ──────────────────────────────────────────────
        /// <summary>Step 5 specification Base Price (AED per SQM).</summary>
        public decimal GivenPricePerSqm { get; set; }

        /// <summary>Measured cut area (SQM) for this specification.</summary>
        public decimal AreaSqm { get; set; }

        /// <summary>Given value = GivenPricePerSqm × AreaSqm.</summary>
        public decimal GivenValue { get; set; }

        // ── Formula inputs ─────────────────────────────────────────────
        public decimal OuterGlassRatePerSqm { get; set; }
        public decimal InnerGlassRatePerSqm { get; set; }
        public decimal ThirdGlassRatePerSqm { get; set; }
        public decimal AspRatePerSqm { get; set; }
        public decimal ProcessingRatePerSqm { get; set; }
        public decimal YieldFactor { get; set; }
        public decimal TargetUpliftPercent { get; set; }

        /// <summary>
        /// Readable formula with the live numbers, e.g.
        /// "((47 + 47) ÷ 0.85 + 45) × 1.15".
        /// </summary>
        public string FormulaText { get; set; } = "";

        /// <summary>
        /// Specification target AFTER the uplift and BEFORE any additional
        /// U-insert rate: e.g. AED 178.93/m².
        /// </summary>
        public decimal SpecificationTargetPerSqm { get; set; }

        // ── U-insert ───────────────────────────────────────────────────
        public UInsertTreatment UInsertTreatment { get; set; } = UInsertTreatment.NotApplicable;
        public decimal UInsertRatePerSqm { get; set; }
        public string UInsertSourceLabel { get; set; } = "";

        /// <summary>
        /// U-insert rate folded into the required target. Always 0 since the
        /// revised rule (2026-09-04): the KPI target never includes the
        /// U-insert for either treatment. Kept on the model so old snapshots
        /// and callers that read the field keep working.
        /// </summary>
        public decimal AppliedUInsertPerSqm { get; set; }

        /// <summary>
        /// Specification target + applied U-insert (AED per SQM). Under the
        /// revised rule the applied U-insert is always 0, so this equals the
        /// base-price <see cref="SpecificationTargetPerSqm"/>.
        /// </summary>
        public decimal FinalRequiredTargetPerSqm { get; set; }

        // ── Result ─────────────────────────────────────────────────────
        public decimal DifferencePerSqm { get; set; }
        public decimal RequiredTargetValue { get; set; }
        public decimal DifferenceValue { get; set; }

        /// <summary>Null when the margin is NOT calculable (never fake 0%).</summary>
        public decimal? MarginAgainstTargetPercent { get; set; }

        public TargetStatus Status { get; set; } = TargetStatus.CalculationIncomplete;
        public List<string> Warnings { get; set; } = new();

        public bool HasValidMargin => MarginAgainstTargetPercent.HasValue;
    }

    /// <summary>
    /// Job-level aggregation across every specification group of one invoice.
    /// Business rule (documented): the job KPI is value-weighted —
    ///   Total Given Value  = Σ (Given Per-SQM × Area)
    ///   Total Required     = Σ (Final Required Target Per-SQM × Area)
    ///   Job Margin Against Target = (Total Given − Total Required) ÷ Total Required × 100
    /// The per-SQM figures shown on the KPI cards are the area-weighted
    /// averages, so a multi-specification invoice still reconciles with the
    /// value totals.
    /// </summary>
    public sealed class TargetPriceResult
    {
        public List<SpecTargetPriceResult> Specs { get; } = new();

        public decimal TotalAreaSqm { get; set; }
        public decimal TotalGivenValue { get; set; }

        /// <summary>
        /// Σ (specification target before U-insert) × area. Kept so the KPI
        /// card can show the "specification target" line separately from the
        /// "final required target" line.
        /// </summary>
        public decimal TotalSpecificationTargetValue { get; set; }

        public decimal TotalRequiredTargetValue { get; set; }
        public decimal TotalDifferenceValue { get; set; }

        /// <summary>Area-weighted average Step 5 given price (AED/m²).</summary>
        public decimal AverageGivenPricePerSqm { get; set; }

        /// <summary>Area-weighted average specification target before U-insert (AED/m²).</summary>
        public decimal AverageSpecificationTargetPerSqm { get; set; }

        /// <summary>Area-weighted average final required target (AED/m²).</summary>
        public decimal AverageFinalRequiredTargetPerSqm { get; set; }

        /// <summary>
        /// Area-weighted average U-insert rate applied to the required
        /// target (AED/m²). Always 0 under the revised rule — the U-insert
        /// rate is never added to the required target for either treatment.
        /// Kept for backward compatibility with saved snapshots.
        /// </summary>
        public decimal AverageAppliedUInsertPerSqm { get; set; }

        public decimal? MarginAgainstTargetPercent { get; set; }

        public TargetStatus Status { get; set; } = TargetStatus.CalculationIncomplete;

        public int ValidSpecCount { get; set; }
        public int IncompleteSpecCount { get; set; }

        public List<string> Warnings { get; set; } = new();

        public bool HasValidMargin => MarginAgainstTargetPercent.HasValue;

        public IReadOnlyList<SpecTargetPriceResult> Specifications => Specs;

        public void AddSpec(SpecTargetPriceResult spec)
        {
            if (spec == null) return;
            Specs.Add(spec);
        }
    }

    /// <summary>
    /// Aggregation + averaging helpers shared by Step 9 and Job Profitability.
    /// The two headline portfolio measures are intentionally DIFFERENT:
    ///   Portfolio margin against target = Σ Difference ÷ Σ Required Target
    ///   Average job margin against target = plain mean of VALID job margins
    /// </summary>
    public static class TargetProfitabilityAggregator
    {
        /// <summary>
        /// Aggregates per-specification results into a single job/portfolio
        /// result using the documented value-weighted rule.
        /// </summary>
        public static TargetPriceResult AggregateJob(
            IEnumerable<SpecTargetPriceResult> specs)
        {
            var result = new TargetPriceResult();
            var list = (specs ?? Enumerable.Empty<SpecTargetPriceResult>())
                .Where(s => s != null)
                .ToList();

            foreach (var spec in list)
            {
                result.AddSpec(spec);
                result.TotalAreaSqm += Math.Max(0m, spec.AreaSqm);
                result.TotalGivenValue += Math.Max(0m, spec.GivenValue);
                result.TotalSpecificationTargetValue += Math.Max(0m,
                    Math.Round(Math.Max(0m, spec.SpecificationTargetPerSqm) * Math.Max(0m, spec.AreaSqm), 2));
                result.TotalRequiredTargetValue += Math.Max(0m, spec.RequiredTargetValue);
                result.TotalDifferenceValue += Math.Max(0m, spec.DifferenceValue);
            }

            result.TotalGivenValue = Math.Round(result.TotalGivenValue, 2);
            result.TotalSpecificationTargetValue = Math.Round(result.TotalSpecificationTargetValue, 2);
            result.TotalRequiredTargetValue = Math.Round(result.TotalRequiredTargetValue, 2);
            result.TotalDifferenceValue = Math.Round(result.TotalDifferenceValue, 2);

            result.AverageGivenPricePerSqm = result.TotalAreaSqm > 0m
                ? Math.Round(result.TotalGivenValue / result.TotalAreaSqm, 2)
                : 0m;

            result.AverageSpecificationTargetPerSqm = result.TotalAreaSqm > 0m
                ? Math.Round(result.TotalSpecificationTargetValue / result.TotalAreaSqm, 2)
                : 0m;

            result.AverageFinalRequiredTargetPerSqm = result.TotalAreaSqm > 0m
                ? Math.Round(result.TotalRequiredTargetValue / result.TotalAreaSqm, 2)
                : 0m;

            result.AverageAppliedUInsertPerSqm = result.TotalAreaSqm > 0m
                ? Math.Round(Math.Max(0m, result.TotalRequiredTargetValue - result.TotalSpecificationTargetValue)
                             / result.TotalAreaSqm, 2)
                : 0m;

            if (result.TotalRequiredTargetValue > 0m
                && list.All(s => s.HasValidMargin))
            {
                result.MarginAgainstTargetPercent = Math.Round(
                    (result.TotalGivenValue - result.TotalRequiredTargetValue)
                    / result.TotalRequiredTargetValue * 100m, 2);
            }

            result.ValidSpecCount = list.Count(s => s.HasValidMargin);
            result.IncompleteSpecCount = list.Count - result.ValidSpecCount;

            result.Status = DeriveStatus(result, list);
            result.Warnings = list
                .SelectMany(s => s.Warnings)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return result;
        }

        /// <summary>
        /// Plain average of valid job margins (each job margin is itself the
        /// value-weighted margin of that job's specifications). Incomplete
        /// jobs are excluded — never diluted with a fake 0%.
        /// </summary>
        public static decimal? AverageJobMargin(
            IEnumerable<decimal?> jobMargins)
        {
            var valid = (jobMargins ?? Enumerable.Empty<decimal?>())
                .Where(m => m.HasValue)
                .Select(m => m!.Value)
                .ToList();

            return valid.Count == 0
                ? (decimal?)null
                : Math.Round(valid.Average(), 2);
        }

        private static TargetStatus DeriveStatus(
            TargetPriceResult aggregate,
            IReadOnlyList<SpecTargetPriceResult> specs)
        {
            if (specs.Count == 0) return TargetStatus.CalculationIncomplete;
            if (specs.All(s => s.HasValidMargin))
            {
                if (!aggregate.MarginAgainstTargetPercent.HasValue)
                    return TargetStatus.CalculationIncomplete;

                decimal m = aggregate.MarginAgainstTargetPercent.Value;
                if (m <= -0.005m) return TargetStatus.BelowTarget;
                if (m >= 0.005m) return TargetStatus.AboveTarget;
                return TargetStatus.AtTarget;
            }

            // At least one specification is incomplete — surface the most
            // important missing input instead of a generic state.
            if (specs.Any(s => s.Status == TargetStatus.MissingBasePrice))
                return TargetStatus.MissingBasePrice;
            if (specs.Any(s => s.Status == TargetStatus.MissingGlassRate))
                return TargetStatus.MissingGlassRate;
            if (specs.Any(s => s.Status == TargetStatus.MissingAspRate))
                return TargetStatus.MissingAspRate;
            if (specs.Any(s => s.Status == TargetStatus.MissingYield))
                return TargetStatus.MissingYield;
            if (specs.Any(s => s.Status == TargetStatus.MissingUInsertTreatment))
                return TargetStatus.MissingUInsertTreatment;

            return TargetStatus.CalculationIncomplete;
        }
    }
}
