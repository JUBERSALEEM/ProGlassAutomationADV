// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/ProformaInvoice/TargetPricing/Step6ChargeResolver.cs
// PROJECT: ProGlassAutomation
// MODULE: U-insert treatment + rate resolution (single dedicated resolver)
//
// The Step 6 charge-row state is the established application convention for
// whether a U-insert extra applies to a specification. This resolver reads
// that state and decides the ACCOUNTING TREATMENT:
//
//   • U-insert row ACTIVE and targeted at the spec  → ADDITIONAL
//     Final required target = specification target + resolved U-insert rate.
//
//   • Spec carries no U-insert, or the row is NOT active for the spec
//     → INCLUDED in the Step 5 price (target unchanged). This is never
//       inferred from the generic automation on/off toggle.
//
// The rate is read dynamically — Step 4 per-spec seed wins, then the Step 6
// row rate, then the Step 6 template default. AED 7 / AED 10 are never
// hardcoded here.
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.Services.ProformaInvoice.TargetPricing
{
    /// <summary>Inputs the resolver needs. Built by the Step 9 adapter.</summary>
    public sealed class Step6UInsertQuery
    {
        /// <summary>
        /// True when this specification actually carries a U-insert extra
        /// (spec name / automation suggestion evidence).
        /// </summary>
        public bool SpecRequiresUInsert { get; set; }

        /// <summary>True when a Step 6 U-insert charge row exists.</summary>
        public bool RowExists { get; set; }

        /// <summary>True when the Step 6 U-insert row is active FOR THIS SPEC.</summary>
        public bool RowActiveForSpec { get; set; }

        /// <summary>Step 4 per-spec U-insert seed (SpecGroup.UInsertPricePerSqm).</summary>
        public decimal Step4SeedRate { get; set; }

        /// <summary>Rate typed on the Step 6 U-insert row.</summary>
        public decimal RowRate { get; set; }

        /// <summary>Step 6 U-insert template default rate.</summary>
        public decimal TemplateDefaultRate { get; set; }
    }

    /// <summary>
    /// Resolves U-insert accounting treatment and the dynamic rate for one
    /// specification. Pure and deterministic.
    /// </summary>
    public static class Step6ChargeResolver
    {
        public static UInsertResolution ResolveUInsert(Step6UInsertQuery query)
        {
            query ??= new Step6UInsertQuery();
            var result = new UInsertResolution();

            if (!query.SpecRequiresUInsert)
            {
                result.Treatment = UInsertTreatment.NotApplicable;
                result.RatePerSqm = 0m;
                return result;
            }

            // Dynamic rate order — never a hardcoded AED 7 / AED 10.
            decimal rate = query.Step4SeedRate > 0m
                ? query.Step4SeedRate
                : query.RowRate > 0m
                    ? query.RowRate
                    : query.TemplateDefaultRate > 0m
                        ? query.TemplateDefaultRate
                        : 0m;

            if (query.RowExists && query.RowActiveForSpec)
            {
                result.Treatment = UInsertTreatment.Additional;
                result.RatePerSqm = Math.Max(0m, rate);

                if (result.RatePerSqm <= 0m)
                {
                    result.Treatment = UInsertTreatment.Unresolved;
                    result.Warning =
                        "U-insert is required and its Step 6 row is active, but no rate is available " +
                        "(Step 4 seed, Step 6 row rate and template default are all empty).";
                }

                return result;
            }

            // Spec requires a U-insert but the Step 6 row is not active for
            // it — the operator is not billing the extra, so the Step 5
            // price is treated as already covering it.
            result.Treatment = UInsertTreatment.IncludedInStep5Price;
            result.RatePerSqm = 0m;

            if (!query.RowExists && rate <= 0m)
            {
                result.Treatment = UInsertTreatment.Unresolved;
                result.Warning =
                    "U-insert is required but no Step 6 U-insert row exists and no rate is configured.";
            }

            return result;
        }
    }
}
