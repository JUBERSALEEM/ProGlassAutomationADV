// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/ProformaInvoice/TargetPricing/SpecificationTargetCalculator.cs
// PROJECT: ProGlassAutomation
// MODULE: Shared target-price calculation layer
//
// THE ONLY APPROVED KPI MARGIN FORMULA:
//     Margin Against Target =
//         (Step 5 Given Price − Final Required Target)
//         ÷ Final Required Target × 100
//
//   Specification target (DGU example):
//     ((Outer Glass Rate + Inner Glass Rate) ÷ Yield Factor + ASP Rate) × 1.15
//
// The pre-uplift formula result is NEVER used as the KPI denominator, and
// wastage never enters this model (it lives exclusively in Sheet Detail).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ProGlassAutomation.Services.ProformaInvoice.TargetPricing
{
    /// <summary>
    /// Pure, deterministic per-specification target-price calculator.
    /// No WPF, no Sheet Store, no dispatcher — the same code drives Step 9,
    /// the saved snapshot and the unit tests.
    /// </summary>
    public static class SpecificationTargetCalculator
    {
        /// <summary>
        /// The approved target uplift applied to the specification formula
        /// result. Never read from a competing margin setting.
        /// </summary>
        public const decimal DefaultTargetUpliftPercent = 15m;

        /// <summary>Tolerance used to decide AT TARGET vs ABOVE/BELOW TARGET.</summary>
        private const decimal AtTargetTolerancePercent = 0.005m;

        public static SpecTargetPriceResult Calculate(
            int specNumber,
            string specName,
            string glassTypeCode,
            GlassBuildType buildType,
            SpecificationRateCard card,
            decimal givenPricePerSqm,
            decimal areaSqm)
        {
            var result = new SpecTargetPriceResult
            {
                SpecNumber = specNumber,
                SpecName = specName ?? "",
                GlassTypeCode = glassTypeCode ?? "",
                BuildType = buildType,
                GivenPricePerSqm = givenPricePerSqm,
                AreaSqm = Math.Max(0m, areaSqm),
                OuterGlassRatePerSqm = card?.OuterGlassRatePerSqm ?? 0m,
                InnerGlassRatePerSqm = card?.InnerGlassRatePerSqm ?? 0m,
                ThirdGlassRatePerSqm = card?.ThirdGlassRatePerSqm ?? 0m,
                AspRatePerSqm = card?.AspRatePerSqm ?? 0m,
                ProcessingRatePerSqm = card?.ProcessingRatePerSqm ?? 0m,
                YieldFactor = card?.YieldFactor ?? 0m,
                TargetUpliftPercent = (card?.TargetUpliftPercent ?? 0m) > 0m
                    ? card!.TargetUpliftPercent
                    : DefaultTargetUpliftPercent
            };

            if (card == null)
            {
                result.Status = TargetStatus.CalculationIncomplete;
                result.Warnings.Add("No rate card could be built for this specification.");
                FinalizeValues(result);
                return result;
            }

            // ── Input validation (never silently treat missing as zero) ──
            if (buildType == GlassBuildType.Unknown)
            {
                result.Status = TargetStatus.CalculationIncomplete;
                result.Warnings.Add(
                    "Could not identify the build (DGU / SGU / LAM / DGU+LAM) for this specification.");
                FinalizeValues(result);
                return result;
            }

            if (result.YieldFactor <= 0m)
            {
                result.Status = TargetStatus.MissingYield;
                result.Warnings.Add("Missing yield factor — the specification target cannot be calculated.");
                FinalizeValues(result);
                return result;
            }

            if (!HasRequiredGlassRates(result, buildType))
            {
                result.Status = TargetStatus.MissingGlassRate;
                result.Warnings.Add("Missing glass rate — the specification target cannot be calculated.");
                FinalizeValues(result);
                return result;
            }

            if (RequiresAsp(buildType) && result.AspRatePerSqm <= 0m)
            {
                result.Status = TargetStatus.MissingAspRate;
                result.Warnings.Add("Missing ASP/spacer rate — the specification target cannot be calculated.");
                FinalizeValues(result);
                return result;
            }

            // All required formula inputs are present.
            result.Status = TargetStatus.UpToDate;

            // ── Specification formula ────────────────────────────────────
            decimal glassSum = GlassRateSum(result, buildType);
            decimal addons = result.AspRatePerSqm + result.ProcessingRatePerSqm;
            decimal upliftFactor = 1m + result.TargetUpliftPercent / 100m;

            decimal preUplift = (glassSum / result.YieldFactor) + addons;
            result.SpecificationTargetPerSqm = Math.Round(preUplift * upliftFactor, 2);
            result.FormulaText = BuildFormulaText(result, buildType, upliftFactor);

            FinalizeValues(result);
            return result;
        }

        /// <summary>
        /// Applies the resolved U-insert treatment to a calculated spec
        /// result and derives the final target, difference and margin.
        /// Call after <see cref="Calculate"/> on every result.
        /// </summary>
        public static void ApplyUInsert(
            SpecTargetPriceResult result,
            UInsertResolution resolution)
        {
            if (result == null) return;
            resolution ??= new UInsertResolution();

            result.UInsertTreatment = resolution.Treatment;
            result.UInsertRatePerSqm = resolution.RatePerSqm;
            result.UInsertSourceLabel = "Step 6 Other Charges — U-insert";

            result.AppliedUInsertPerSqm =
                resolution.Treatment == UInsertTreatment.Additional
                    ? Math.Max(0m, resolution.RatePerSqm)
                    : 0m;

            result.FinalRequiredTargetPerSqm = Math.Round(
                result.SpecificationTargetPerSqm + result.AppliedUInsertPerSqm, 2);

            if (resolution.HasWarning
                && !result.Warnings.Contains(resolution.Warning, StringComparer.Ordinal))
            {
                result.Warnings.Add(resolution.Warning);
            }

            // Only promote to the U-insert state when the rest of the
            // formula is complete — a missing yield must stay MissingYield.
            if (resolution.Treatment == UInsertTreatment.Unresolved
                && result.Status is TargetStatus.UpToDate
                                or TargetStatus.BelowTarget
                                or TargetStatus.AtTarget
                                or TargetStatus.AboveTarget)
            {
                result.Status = TargetStatus.MissingUInsertTreatment;
            }

            FinalizeValues(result);
        }

        /// <summary>
        /// Marks a specification whose saved snapshot predates the target
        /// model (or whose inputs are unavailable) as stale/incomplete so the
        /// UI never paints a fake 0% margin.
        /// </summary>
        public static SpecTargetPriceResult StaleResult(
            int specNumber, string specName, string staleReason)
        {
            return new SpecTargetPriceResult
            {
                SpecNumber = specNumber,
                SpecName = specName ?? "",
                Status = TargetStatus.SnapshotStale,
                Warnings =
                {
                    string.IsNullOrWhiteSpace(staleReason)
                        ? "Saved profitability data predates the target-price model — reopen Step 9 and save to refresh."
                        : staleReason
                }
            };
        }

        // ────────────────────────────────────────────────────────────────

        private static bool HasRequiredGlassRates(
            SpecTargetPriceResult r, GlassBuildType buildType)
        {
            return buildType switch
            {
                GlassBuildType.Sgu => r.OuterGlassRatePerSqm > 0m,
                GlassBuildType.Dgu => r.OuterGlassRatePerSqm > 0m && r.InnerGlassRatePerSqm > 0m,
                GlassBuildType.Lam => r.OuterGlassRatePerSqm > 0m && r.InnerGlassRatePerSqm > 0m,
                GlassBuildType.DguLam => r.OuterGlassRatePerSqm > 0m
                                         && r.InnerGlassRatePerSqm > 0m
                                         && r.ThirdGlassRatePerSqm > 0m,
                _ => false
            };
        }

        private static bool RequiresAsp(GlassBuildType buildType) =>
            buildType is GlassBuildType.Dgu or GlassBuildType.DguLam;

        private static decimal GlassRateSum(
            SpecTargetPriceResult r, GlassBuildType buildType)
        {
            return buildType switch
            {
                GlassBuildType.Sgu => r.OuterGlassRatePerSqm,
                GlassBuildType.Dgu => r.OuterGlassRatePerSqm + r.InnerGlassRatePerSqm,
                GlassBuildType.Lam => r.OuterGlassRatePerSqm + r.InnerGlassRatePerSqm,
                GlassBuildType.DguLam => r.OuterGlassRatePerSqm
                                         + r.InnerGlassRatePerSqm
                                         + r.ThirdGlassRatePerSqm,
                _ => 0m
            };
        }

        private static string BuildFormulaText(
            SpecTargetPriceResult r, GlassBuildType buildType, decimal upliftFactor)
        {
            string glass;
            string yieldDisp = Fmt(r.YieldFactor);

            switch (buildType)
            {
                case GlassBuildType.Sgu:
                    glass = Fmt(r.OuterGlassRatePerSqm);
                    break;
                case GlassBuildType.Dgu:
                case GlassBuildType.Lam:
                    glass = $"({Fmt(r.OuterGlassRatePerSqm)} + {Fmt(r.InnerGlassRatePerSqm)})";
                    break;
                default:
                    glass = $"({Fmt(r.OuterGlassRatePerSqm)} + {Fmt(r.InnerGlassRatePerSqm)}"
                            + $" + {Fmt(r.ThirdGlassRatePerSqm)})";
                    break;
            }

            var inner = new List<string> { $"{glass} ÷ {yieldDisp}" };

            if (r.AspRatePerSqm > 0m)
                inner.Add(Fmt(r.AspRatePerSqm));
            if (r.ProcessingRatePerSqm > 0m)
                inner.Add(Fmt(r.ProcessingRatePerSqm));

            return $"({string.Join(" + ", inner)}) × {Fmt(upliftFactor)}";
        }

        private static void FinalizeValues(SpecTargetPriceResult r)
        {
            // The FINAL required target (after any additional U-insert) is the
            // authoritative per-SQM basis once it exists; before the U-insert
            // merge the specification target stands in for it.
            decimal targetPerSqm = r.FinalRequiredTargetPerSqm > 0m
                ? r.FinalRequiredTargetPerSqm
                : r.SpecificationTargetPerSqm;

            if (targetPerSqm > 0m)
            {
                r.RequiredTargetValue = Math.Round(targetPerSqm * r.AreaSqm, 2);
            }

            if (r.GivenPricePerSqm > 0m)
            {
                r.GivenValue = Math.Round(r.GivenPricePerSqm * r.AreaSqm, 2);
                r.DifferencePerSqm = Math.Round(r.GivenPricePerSqm - targetPerSqm, 2);
                r.DifferenceValue = Math.Round(r.GivenValue - r.RequiredTargetValue, 2);

                if (targetPerSqm > 0m
                    && r.Status is not (TargetStatus.MissingBasePrice
                            or TargetStatus.MissingGlassRate
                            or TargetStatus.MissingAspRate
                            or TargetStatus.MissingYield
                            or TargetStatus.MissingUInsertTreatment
                            or TargetStatus.CalculationIncomplete
                            or TargetStatus.SnapshotStale))
                {
                    decimal margin = (r.GivenPricePerSqm - targetPerSqm)
                                     / targetPerSqm * 100m;
                    r.MarginAgainstTargetPercent = Math.Round(margin, 2);

                    if (r.Status == TargetStatus.UpToDate)
                    {
                        decimal m = r.MarginAgainstTargetPercent.Value;
                        r.Status = m <= -AtTargetTolerancePercent
                            ? TargetStatus.BelowTarget
                            : m >= AtTargetTolerancePercent
                                ? TargetStatus.AboveTarget
                                : TargetStatus.AtTarget;
                    }
                }
            }
            else
            {
                r.DifferencePerSqm = 0m;
                r.DifferenceValue = 0m;
                r.MarginAgainstTargetPercent = null;

                if (r.Status is TargetStatus.UpToDate
                    or TargetStatus.BelowTarget
                    or TargetStatus.AtTarget
                    or TargetStatus.AboveTarget)
                {
                    r.Status = TargetStatus.MissingBasePrice;
                }

                if (!r.Warnings.Contains("Missing Step 5 Base Price — the given price is not set.",
                        StringComparer.Ordinal))
                {
                    r.Warnings.Add("Missing Step 5 Base Price — the given price is not set.");
                }
            }
        }

        /// <summary>Formats a decimal without trailing zeros ("47", "0.85", "1.15").</summary>
        public static string Fmt(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
