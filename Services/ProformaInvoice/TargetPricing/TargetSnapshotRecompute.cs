// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/ProformaInvoice/TargetPricing/TargetSnapshotRecompute.cs
// PROJECT: ProGlassAutomation
// MODULE: Corrected read-side rebuild of a saved target-price snapshot
//
// Why this exists (2026-09-04):
//   The required target a quotation is judged against is the BASE-PRICE
//   specification target (DGU formula result × uplift). Earlier builds
//   could persist a FINAL REQUIRED TARGET that had the U-insert rate folded
//   in (specification target + rate, e.g. 119.39 + 10 = 129.39). Because a
//   given price that already includes the U-insert must not be judged
//   against a target that also adds it, Job Profitability and its detail
//   sidebar rebuild each saved invoice from the per-specification rows
//   using ONLY the specification target. Old invoices therefore correct
//   themselves on view — no data migration.
//
//   Every rebuild result still honours the "never fake a margin" rule:
//   a job is only complete when EVERY specification row is complete; any
//   incomplete spec makes the whole job incomplete (null margin).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;

namespace ProGlassAutomation.Services.ProformaInvoice.TargetPricing
{
    /// <summary>
    /// Corrected aggregate rebuilt from one invoice's per-specification
    /// snapshot rows. Null-safe output; callers fall back to the stored
    /// aggregate totals when no per-spec rows exist.
    /// </summary>
    public sealed class RebuiltTargetTotals
    {
        public decimal TotalAreaSqm { get; set; }
        public decimal TotalGivenValue { get; set; }
        public decimal TotalRequiredValue { get; set; }
        public decimal TotalDifferenceValue { get; set; }
        public decimal AverageGivenPricePerSqm { get; set; }
        public decimal AverageRequiredPerSqm { get; set; }

        /// <summary>Null when any specification row is incomplete.</summary>
        public decimal? MarginAgainstTargetPercent { get; set; }

        /// <summary>TargetStatus enum name ("AboveTarget" / "BelowTarget" / …).</summary>
        public string Status { get; set; } = "CalculationIncomplete";

        public int ValidSpecCount { get; set; }
        public int IncompleteSpecCount { get; set; }
        public bool AllSpecsComplete { get; set; }
    }

    public static class TargetSnapshotRecompute
    {
        private const decimal AtTargetTolerancePercent = 0.05m;

        /// <summary>TargetStatus names that mean "fully calculable".</summary>
        private static readonly HashSet<string> CompleteStatusNames =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "UpToDate", "AboveTarget", "AtTarget", "BelowTarget"
            };

        /// <summary>
        /// Rebuilds the job-level given/required/difference + margin from the
        /// per-specification snapshot rows. The per-spec required basis is
        /// ALWAYS the base-price <see cref="SpecTargetSnapshot.SpecificationTargetPerSqm"/>
        /// — never a stored final target that may have had the U-insert rate
        /// folded in by an older build.
        /// </summary>
        /// <returns>Null when there are no usable per-spec rows at all.</returns>
        public static RebuiltTargetTotals? Recompute(
            IReadOnlyList<SpecTargetSnapshot>? specs)
        {
            var rows = (specs ?? Array.Empty<SpecTargetSnapshot>())
                .Where(s => s != null)
                .ToList();

            if (rows.Count == 0) return null;

            bool usable(SpecTargetSnapshot s) =>
                s.AreaSqm > 0m
                && s.GivenPricePerSqm > 0m
                && s.SpecificationTargetPerSqm > 0m;

            var usableRows = rows.Where(usable).ToList();
            bool allComplete = usableRows.Count == rows.Count
                               && rows.All(s =>
                                   CompleteStatusNames.Contains(s.Status ?? "")
                                   || s.MarginAgainstTargetPercent.HasValue);

            var result = new RebuiltTargetTotals
            {
                AllSpecsComplete = allComplete,
                ValidSpecCount = rows.Count(s =>
                    CompleteStatusNames.Contains(s.Status ?? "")
                    || s.MarginAgainstTargetPercent.HasValue),
                IncompleteSpecCount = rows.Count(s =>
                    !CompleteStatusNames.Contains(s.Status ?? "")
                    && !s.MarginAgainstTargetPercent.HasValue)
            };

            decimal totalArea = 0m, given = 0m, required = 0m;
            foreach (var s in usableRows)
            {
                decimal area = Math.Max(0m, s.AreaSqm);
                totalArea += area;
                given += Math.Max(0m, s.GivenPricePerSqm) * area;
                required += Math.Max(0m, s.SpecificationTargetPerSqm) * area;
            }

            result.TotalAreaSqm = Math.Round(totalArea, 2);
            result.TotalGivenValue = Math.Round(given, 2);
            result.TotalRequiredValue = Math.Round(required, 2);
            result.TotalDifferenceValue = Math.Round(given - required, 2);
            result.AverageGivenPricePerSqm = totalArea > 0m
                ? Math.Round(given / totalArea, 2)
                : 0m;
            result.AverageRequiredPerSqm = totalArea > 0m
                ? Math.Round(required / totalArea, 2)
                : 0m;

            if (allComplete
                && usableRows.Count > 0
                && result.TotalRequiredValue > 0m
                && result.TotalGivenValue > 0m)
            {
                decimal margin = (result.TotalGivenValue - result.TotalRequiredValue)
                                 / result.TotalRequiredValue * 100m;
                result.MarginAgainstTargetPercent = Math.Round(margin, 2);

                decimal m = result.MarginAgainstTargetPercent.Value;
                result.Status = m > AtTargetTolerancePercent ? "AboveTarget"
                    : m < -AtTargetTolerancePercent ? "BelowTarget"
                    : "AtTarget";
            }
            else if (allComplete)
            {
                // Complete rows but no money yet — never a fake 0% margin.
                result.Status = "MissingBasePrice";
            }
            else
            {
                // Surface the most important missing input, mirroring
                // TargetProfitabilityAggregator's ordering.
                var first = rows.FirstOrDefault(s =>
                    !CompleteStatusNames.Contains(s.Status ?? "")
                    && !s.MarginAgainstTargetPercent.HasValue);
                result.Status = !string.IsNullOrWhiteSpace(first?.Status)
                    ? first!.Status!
                    : "CalculationIncomplete";
            }

            return result;
        }
    }
}
