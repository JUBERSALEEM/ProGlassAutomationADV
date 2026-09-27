// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   Services/ProformaInvoice/TargetPricing/TargetPriceModels.cs
//     (GlassBuildType, UInsertTreatment, TargetStatus, SpecificationRateCard,
//      UInsertResolution, SpecTargetPriceResult, TargetPriceResult,
//      TargetProfitabilityAggregator)
//   Services/ProformaInvoice/TargetPricing/SpecificationTargetCalculator.cs
//     (Calculate / ApplyUInsert / StaleResult — verbatim, incl. the
//      2026-09-04 revised U-insert rule: the required target NEVER moves)
//   Services/ProformaInvoice/TargetPricing/TargetMarginTolerance.cs
//   Services/ProformaInvoice/TargetPricing/Step5PriceSource.cs
//   Services/ProformaInvoice/TargetPricing/Step6ChargeResolver.cs
//   ViewModels/ProformaInvoice/Step9ProfitLoss/
//     ProfitLossStepViewModel.TargetPricing.cs — the RebuildTargetResults
//     loop + BuildRateCard adapter (handoff-first, see the SaaS notes on
//     SpecificationTargetBuilder below for the documented differences).
// The WPF files are the source of truth; behaviour preserved verbatim.
// ═══════════════════════════════════════════════════════════════════════

using System.Globalization;

namespace ProGlass.Saas.Domain.Proforma;

/// <summary>Physical build of a specification.</summary>
public enum GlassBuildType
{
    Unknown = 0,
    Sgu,
    Dgu,
    Lam,
    DguLam
}

/// <summary>
/// Accounting treatment of the U-insert extra (revised 2026-09-04):
/// neither treatment moves the required target — the KPI always judges
/// the base-price specification target.
/// </summary>
public enum UInsertTreatment
{
    /// <summary>Specification carries no U-insert extra.</summary>
    NotApplicable = 0,

    /// <summary>Step 6 row NOT active — the U-insert is covered inside
    /// the Step 5 given price; the given price judged for the margin is
    /// net of the rate.</summary>
    IncludedInStep5Price,

    /// <summary>Step 6 U-insert row IS active — billed separately on top
    /// of the given price; the rate is display-only.</summary>
    Additional,

    /// <summary>U-insert required but no rate resolvable anywhere — never
    /// silently treated as zero.</summary>
    Unresolved
}

/// <summary>
/// Validation states. "Missing…" states must never render a fake 0%
/// margin — the client shows "—" plus the warning.
/// </summary>
public enum TargetStatus
{
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
/// consumes, kept granular so the client can print the formula with the
/// real numbers.
/// </summary>
public sealed class SpecificationRateCard
{
    public GlassBuildType BuildType { get; set; } = GlassBuildType.Unknown;
    public decimal OuterGlassRatePerSqm { get; set; }
    public decimal InnerGlassRatePerSqm { get; set; }
    public decimal ThirdGlassRatePerSqm { get; set; }
    public decimal AspRatePerSqm { get; set; }

    /// <summary>
    /// Remaining per-SQM formula add-ons for the build type, already
    /// summed: SGU = cutting + tempering; LAM = interlayer + cutting +
    /// tempering; DGU = 0; DGU+LAM = interlayer + cutting + tempering.
    /// </summary>
    public decimal ProcessingRatePerSqm { get; set; }
    public decimal YieldFactor { get; set; }

    /// <summary>The approved target uplift (fixed at 15%).</summary>
    public decimal TargetUpliftPercent { get; set; } =
        SpecificationTargetCalculator.DefaultTargetUpliftPercent;
}

/// <summary>U-insert resolution: treatment + rate + warning.</summary>
public sealed class UInsertResolution
{
    public UInsertTreatment Treatment { get; set; } = UInsertTreatment.NotApplicable;

    /// <summary>Resolved per-SQM rate (seed order: Step 4 per-spec seed →
    /// Step 6 row rate → Step 6 template default). Display-only.</summary>
    public decimal RatePerSqm { get; set; }

    public string Warning { get; set; } = "";
    public bool HasWarning => !string.IsNullOrWhiteSpace(Warning);
}

/// <summary>Target-price result for ONE specification group.</summary>
public sealed class SpecTargetPriceResult
{
    public int SpecNumber { get; set; }
    public string SpecName { get; set; } = "";
    public string GlassTypeCode { get; set; } = "";
    public GlassBuildType BuildType { get; set; } = GlassBuildType.Unknown;

    // Step 5 inputs
    public decimal GivenPricePerSqm { get; set; }
    public decimal AreaSqm { get; set; }
    public decimal GivenValue { get; set; }

    // Formula inputs
    public decimal OuterGlassRatePerSqm { get; set; }
    public decimal InnerGlassRatePerSqm { get; set; }
    public decimal ThirdGlassRatePerSqm { get; set; }
    public decimal AspRatePerSqm { get; set; }
    public decimal ProcessingRatePerSqm { get; set; }
    public decimal YieldFactor { get; set; }
    public decimal TargetUpliftPercent { get; set; }

    /// <summary>Readable formula with the live numbers, e.g.
    /// "((47 + 47) ÷ 0.85 + 45) × 1.15".</summary>
    public string FormulaText { get; set; } = "";

    /// <summary>Specification target AFTER the uplift and BEFORE any
    /// U-insert rate.</summary>
    public decimal SpecificationTargetPerSqm { get; set; }

    // U-insert
    public UInsertTreatment UInsertTreatment { get; set; } = UInsertTreatment.NotApplicable;
    public decimal UInsertRatePerSqm { get; set; }
    public string UInsertSourceLabel { get; set; } = "";

    /// <summary>Always 0 under the revised rule (kept for snapshot
    /// compatibility).</summary>
    public decimal AppliedUInsertPerSqm { get; set; }

    /// <summary>Specification target + applied U-insert — equals the
    /// base-price specification target under the revised rule.</summary>
    public decimal FinalRequiredTargetPerSqm { get; set; }

    // Result
    public decimal DifferencePerSqm { get; set; }
    public decimal RequiredTargetValue { get; set; }
    public decimal DifferenceValue { get; set; }

    // Base-price judgement (the KPI trigger)
    /// <summary>Pre-uplift formula result, rounded to 2dp — the BASE
    /// price every margin trigger hangs off: (25 + 25) ÷ 0.85 + 45 =
    /// 103.82 → target 103.82 × 1.15.</summary>
    public decimal BasePricePerSqm { get; set; }

    /// <summary>The given price as it counts for the margin judgement
    /// (net of an INSIDE-the-price U-insert).</summary>
    public decimal GivenPriceJudgedPerSqm { get; set; }

    /// <summary>GivenPriceJudgedPerSqm − BasePricePerSqm. Null when not
    /// calculable (never fake 0).</summary>
    public decimal? ExtraProfitPerSqm { get; set; }

    /// <summary>(GivenPriceJudgedPerSqm − BasePricePerSqm) ÷ BasePrice ×
    /// 100. Null when not calculable.</summary>
    public decimal? ExtraProfitMarginPercent { get; set; }

    public bool HasValidExtraProfit => ExtraProfitPerSqm.HasValue
                                       && ExtraProfitMarginPercent.HasValue;

    /// <summary>Null when the margin is NOT calculable.</summary>
    public decimal? MarginAgainstTargetPercent { get; set; }

    public TargetStatus Status { get; set; } = TargetStatus.CalculationIncomplete;
    public List<string> Warnings { get; set; } = new();

    public bool HasValidMargin => MarginAgainstTargetPercent.HasValue;
}

/// <summary>
/// Job-level aggregation across every specification group of one invoice
/// (value-weighted, documented business rule).
/// </summary>
public sealed class TargetPriceResult
{
    public List<SpecTargetPriceResult> Specs { get; } = new();

    public decimal TotalAreaSqm { get; set; }
    public decimal TotalGivenValue { get; set; }
    public decimal TotalSpecificationTargetValue { get; set; }
    public decimal TotalRequiredTargetValue { get; set; }
    public decimal TotalDifferenceValue { get; set; }

    public decimal AverageGivenPricePerSqm { get; set; }
    public decimal AverageSpecificationTargetPerSqm { get; set; }
    public decimal AverageFinalRequiredTargetPerSqm { get; set; }
    public decimal AverageAppliedUInsertPerSqm { get; set; }

    public decimal AverageBasePricePerSqm { get; set; }
    public decimal AverageGivenJudgedPerSqm { get; set; }
    public decimal? AverageExtraProfitPerSqm { get; set; }
    public decimal? ExtraProfitMarginPercent { get; set; }
    public decimal? CostBasedProfitMarginPercent { get; set; }
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
/// Aggregation + averaging helpers shared by Step 9 and Job
/// Profitability. The two headline portfolio measures are intentionally
/// DIFFERENT: portfolio margin = Σ difference ÷ Σ required; average job
/// margin = plain mean of VALID job margins.
/// </summary>
public static class TargetProfitabilityAggregator
{
    public static TargetPriceResult AggregateJob(
        IEnumerable<SpecTargetPriceResult> specs)
    {
        var result = new TargetPriceResult();
        var list = (specs ?? Enumerable.Empty<SpecTargetPriceResult>())
            .Where(s => s != null)
            .ToList();

        decimal baseW = 0m, judgedW = 0m;
        foreach (var spec in list)
        {
            result.AddSpec(spec);
            decimal area = Math.Max(0m, spec.AreaSqm);
            result.TotalAreaSqm += area;
            result.TotalGivenValue += Math.Max(0m, spec.GivenValue);
            result.TotalSpecificationTargetValue += Math.Max(0m,
                MoneyRounding.Round(Math.Max(0m, spec.SpecificationTargetPerSqm) * area));
            result.TotalRequiredTargetValue += Math.Max(0m, spec.RequiredTargetValue);
            // Difference is a balance, not income — preserve the sign.
            result.TotalDifferenceValue += spec.DifferenceValue;

            baseW += Math.Max(0m, spec.BasePricePerSqm) * area;
            judgedW += Math.Max(0m, spec.GivenPriceJudgedPerSqm) * area;
        }

        result.TotalGivenValue = MoneyRounding.Round(result.TotalGivenValue);
        result.TotalSpecificationTargetValue = MoneyRounding.Round(result.TotalSpecificationTargetValue);
        result.TotalRequiredTargetValue = MoneyRounding.Round(result.TotalRequiredTargetValue);
        result.TotalDifferenceValue = MoneyRounding.Round(result.TotalDifferenceValue);

        result.AverageGivenPricePerSqm = result.TotalAreaSqm > 0m
            ? MoneyRounding.Round(result.TotalGivenValue / result.TotalAreaSqm)
            : 0m;

        result.AverageSpecificationTargetPerSqm = result.TotalAreaSqm > 0m
            ? MoneyRounding.Round(result.TotalSpecificationTargetValue / result.TotalAreaSqm)
            : 0m;

        result.AverageFinalRequiredTargetPerSqm = result.TotalAreaSqm > 0m
            ? MoneyRounding.Round(result.TotalRequiredTargetValue / result.TotalAreaSqm)
            : 0m;

        result.AverageAppliedUInsertPerSqm = result.TotalAreaSqm > 0m
            ? MoneyRounding.Round(Math.Max(0m, result.TotalRequiredTargetValue - result.TotalSpecificationTargetValue)
                         / result.TotalAreaSqm)
            : 0m;

        if (result.TotalRequiredTargetValue > 0m
            && judgedW > 0m
            && list.All(s => s.HasValidMargin))
        {
            // Status margin — judged given vs required target,
            // value-weighted.
            result.MarginAgainstTargetPercent = Math.Round(
                (judgedW - result.TotalRequiredTargetValue)
                / result.TotalRequiredTargetValue * 100m, 2);
        }

        // Base-price judgement aggregates — only when EVERY specification
        // carries a valid judgement (never diluted with a fake 0).
        bool allExtraValid = list.Count > 0
                             && list.All(s => s.HasValidExtraProfit);
        result.AverageBasePricePerSqm = result.TotalAreaSqm > 0m
            ? MoneyRounding.Round(baseW / result.TotalAreaSqm)
            : 0m;
        result.AverageGivenJudgedPerSqm = result.TotalAreaSqm > 0m
            ? MoneyRounding.Round(judgedW / result.TotalAreaSqm)
            : 0m;

        if (allExtraValid && result.TotalAreaSqm > 0m)
        {
            result.AverageExtraProfitPerSqm = MoneyRounding.Round(
                (judgedW - baseW) / result.TotalAreaSqm);
            if (baseW > 0m)
            {
                result.ExtraProfitMarginPercent = Math.Round(
                    (judgedW - baseW) / baseW * 100m, 2);
            }
        }
        else
        {
            result.AverageExtraProfitPerSqm = null;
            result.ExtraProfitMarginPercent = null;
        }

        result.CostBasedProfitMarginPercent = CalculateCostBasedProfitMarginPercent(
            list, result.TotalGivenValue);

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
    /// Cost margin from the same resolved per-spec inputs: a Step 6
    /// U-insert contributes cost only while its row is active
    /// (Additional). Missing inputs return null — never a fabricated
    /// percentage.
    /// </summary>
    private static decimal? CalculateCostBasedProfitMarginPercent(
        IReadOnlyList<SpecTargetPriceResult> specs,
        decimal totalSellingValue)
    {
        if (specs.Count == 0 || totalSellingValue <= 0m)
            return null;

        decimal totalBaseCost = 0m;
        decimal totalEnabledUInsertCost = 0m;
        foreach (var spec in specs)
        {
            if (spec.AreaSqm <= 0m
                || spec.GivenPricePerSqm <= 0m
                || spec.GivenValue <= 0m
                || spec.BasePricePerSqm <= 0m
                || spec.UInsertTreatment == UInsertTreatment.Unresolved)
            {
                return null;
            }

            totalBaseCost += spec.BasePricePerSqm * spec.AreaSqm;

            if (spec.UInsertTreatment == UInsertTreatment.Additional)
            {
                if (spec.UInsertRatePerSqm <= 0m)
                    return null;

                totalEnabledUInsertCost += spec.UInsertRatePerSqm * spec.AreaSqm;
            }
        }

        return Math.Round(
            (totalSellingValue - totalBaseCost - totalEnabledUInsertCost)
            / totalSellingValue * 100m, 2);
    }

    /// <summary>
    /// Plain average of valid job margins — incomplete jobs are excluded,
    /// never diluted with a fake 0%.
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

            if (TargetMarginTolerance.IsBelow(m)) return TargetStatus.BelowTarget;
            if (TargetMarginTolerance.IsAbove(m)) return TargetStatus.AboveTarget;
            return TargetStatus.AtTarget;
        }

        // Surface the most important missing input instead of a generic
        // state.
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

/// <summary>
/// The band around zero within which a margin against target counts as AT
/// TARGET (one declaration — the WPF once had a 0.005 vs 0.05 split).
/// </summary>
public static class TargetMarginTolerance
{
    public const decimal AtTargetPercent = 0.005m;

    /// <summary>Inclusive at the boundary: exactly +0.005 is ABOVE.</summary>
    public static bool IsAbove(decimal marginAgainstTargetPercent) =>
        marginAgainstTargetPercent >= AtTargetPercent;

    public static bool IsBelow(decimal marginAgainstTargetPercent) =>
        marginAgainstTargetPercent <= -AtTargetPercent;

    public static bool IsAt(decimal marginAgainstTargetPercent) =>
        !IsAbove(marginAgainstTargetPercent) && !IsBelow(marginAgainstTargetPercent);
}

/// <summary>The given selling price = the Step 5 specification Base
/// Price (SpecGroup.BaseRate → the SaaS rate_per_sqm).</summary>
public static class Step5PriceSource
{
    public const string SourceLabel = "Step 5 Specification Base Price";

    public const string MissingBasePriceWarning =
        "Missing Step 5 Base Price — enter the given price on the Step 5 specification row.";

    public static decimal GivenPricePerSqm(decimal baseRatePerSqm) =>
        Math.Max(0m, baseRatePerSqm);

    public static bool HasGivenPrice(decimal baseRatePerSqm) => baseRatePerSqm > 0m;
}

/// <summary>Inputs the U-insert resolver needs (built by the caller).</summary>
public sealed class Step6UInsertQuery
{
    /// <summary>True when this spec actually carries a U-insert extra
    /// (rate seed, spec-name evidence or automation suggestion).</summary>
    public bool SpecRequiresUInsert { get; set; }

    /// <summary>True when a Step 6 U-insert charge row exists.</summary>
    public bool RowExists { get; set; }

    /// <summary>True when the Step 6 U-insert row is active FOR THIS SPEC.</summary>
    public bool RowActiveForSpec { get; set; }

    /// <summary>Step 4 per-spec U-insert seed.</summary>
    public decimal Step4SeedRate { get; set; }

    /// <summary>Rate typed on the Step 6 U-insert row.</summary>
    public decimal RowRate { get; set; }

    /// <summary>Step 6 U-insert template default rate.</summary>
    public decimal TemplateDefaultRate { get; set; }
}

/// <summary>
/// Resolves the U-insert accounting treatment + dynamic rate. Pure and
/// deterministic; the rate is ALWAYS read dynamically (seed → row →
/// template default — never hardcoded).
/// </summary>
public static class Step6ChargeResolver
{
    public static UInsertResolution ResolveUInsert(Step6UInsertQuery? query)
    {
        query ??= new Step6UInsertQuery();
        var result = new UInsertResolution();

        if (!query.SpecRequiresUInsert)
        {
            result.Treatment = UInsertTreatment.NotApplicable;
            result.RatePerSqm = 0m;
            return result;
        }

        decimal rate = query.Step4SeedRate > 0m
            ? query.Step4SeedRate
            : query.RowRate > 0m
                ? query.RowRate
                : query.TemplateDefaultRate > 0m
                    ? query.TemplateDefaultRate
                    : 0m;

        if (query.RowExists && query.RowActiveForSpec)
        {
            // "Trigger ON": billed separately in Step 6 — the target stays
            // the normal specification target; the rate is display-only.
            result.Treatment = UInsertTreatment.Additional;
            result.RatePerSqm = Math.Max(0m, rate);

            if (result.RatePerSqm <= 0m)
            {
                result.Treatment = UInsertTreatment.Unresolved;
                result.Warning =
                    "U-insert is billed as a Step 6 extra but no rate is available " +
                    "(Step 4 seed, Step 6 row rate and template default are all empty).";
            }

            return result;
        }

        // "Toggle OFF": covered inside the Step 5 price — the rate is
        // carried for the KPI display, the required target never moves.
        result.Treatment = UInsertTreatment.IncludedInStep5Price;
        result.RatePerSqm = Math.Max(0m, rate);

        if (result.RatePerSqm <= 0m)
        {
            result.Treatment = UInsertTreatment.Unresolved;
            result.Warning =
                "The U-insert is covered by the given price but no rate is configured " +
                "(Step 4 seed, Step 6 row rate and template default are all empty), so " +
                "the U-insert cannot be shown on the KPI.";
        }

        return result;
    }
}

/// <summary>
/// Pure, deterministic per-specification target-price calculator (WPF
/// verbatim).
/// </summary>
public static class SpecificationTargetCalculator
{
    /// <summary>The approved target uplift — never read from a competing
    /// margin setting.</summary>
    public const decimal DefaultTargetUpliftPercent = 15m;

    private const decimal AtTargetTolerancePercent = TargetMarginTolerance.AtTargetPercent;

    public static SpecTargetPriceResult Calculate(
        int specNumber,
        string specName,
        string glassTypeCode,
        GlassBuildType buildType,
        SpecificationRateCard? card,
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

        // Input validation — never silently treat missing as zero.
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

        result.Status = TargetStatus.UpToDate;

        // Specification formula. The target lifts the ROUNDED base price
        // (103.82 × 1.15 = 119.39) so the written-down numbers always
        // reconcile with the shop's own arithmetic.
        decimal glassSum = GlassRateSum(result, buildType);
        decimal addons = result.AspRatePerSqm + result.ProcessingRatePerSqm;
        decimal upliftFactor = 1m + result.TargetUpliftPercent / 100m;

        decimal preUplift = (glassSum / result.YieldFactor) + addons;

        decimal basePerSqm = MoneyRounding.Round(preUplift);
        result.BasePricePerSqm = basePerSqm;
        result.SpecificationTargetPerSqm = MoneyRounding.Round(basePerSqm * upliftFactor);

        // The U-insert rate is never folded in (revised 2026-09-04).
        result.FinalRequiredTargetPerSqm = result.SpecificationTargetPerSqm;
        result.FormulaText = BuildFormulaText(result, buildType, upliftFactor);

        FinalizeValues(result);
        return result;
    }

    /// <summary>
    /// Applies the resolved U-insert treatment and derives the final
    /// target, difference and margin. The required target a quotation is
    /// judged against is always the base-price specification target —
    /// for BOTH treatments (adding the rate for an INSIDE-the-price
    /// U-insert would double-count it and punish a profitable
    /// quotation).
    /// </summary>
    public static void ApplyUInsert(
        SpecTargetPriceResult? result,
        UInsertResolution? resolution)
    {
        if (result == null) return;
        resolution ??= new UInsertResolution();

        result.UInsertTreatment = resolution.Treatment;
        result.UInsertRatePerSqm = Math.Max(0m, resolution.RatePerSqm);
        result.UInsertSourceLabel = "Step 6 Other Charges — U-insert";

        // Informational only — the required target never moves.
        result.AppliedUInsertPerSqm = 0m;
        result.FinalRequiredTargetPerSqm = result.SpecificationTargetPerSqm;

        if (resolution.HasWarning
            && !result.Warnings.Contains(resolution.Warning, StringComparer.Ordinal))
        {
            result.Warnings.Add(resolution.Warning);
        }

        // Only promote to the U-insert state when the rest of the formula
        // is complete — a missing yield must stay MissingYield.
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
    /// model as stale so the UI never paints a fake 0% margin.
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
        // The FINAL required target is the authoritative per-SQM basis
        // once it exists; before the U-insert merge the specification
        // target stands in for it.
        decimal targetPerSqm = r.FinalRequiredTargetPerSqm > 0m
            ? r.FinalRequiredTargetPerSqm
            : r.SpecificationTargetPerSqm;

        if (targetPerSqm > 0m)
        {
            r.RequiredTargetValue = MoneyRounding.Round(targetPerSqm * r.AreaSqm);
        }

        if (r.GivenPricePerSqm > 0m)
        {
            r.GivenValue = MoneyRounding.Round(r.GivenPricePerSqm * r.AreaSqm);
            r.DifferencePerSqm = MoneyRounding.Round(r.GivenPricePerSqm - targetPerSqm);
            r.DifferenceValue = MoneyRounding.Round(r.GivenValue - r.RequiredTargetValue);

            // Given JUDGED for the margin: an INSIDE-the-price U-insert is
            // netted out of the given price; a separate Step 6 charge
            // (Additional) leaves the given price untouched.
            decimal judged = r.GivenPricePerSqm;
            if (r.UInsertTreatment == UInsertTreatment.IncludedInStep5Price)
                judged = Math.Max(0m, judged - Math.Max(0m, r.UInsertRatePerSqm));
            r.GivenPriceJudgedPerSqm = MoneyRounding.Round(judged);

            bool marginEligible = r.Status is not (TargetStatus.MissingBasePrice
                    or TargetStatus.MissingGlassRate
                    or TargetStatus.MissingAspRate
                    or TargetStatus.MissingYield
                    or TargetStatus.MissingUInsertTreatment
                    or TargetStatus.CalculationIncomplete
                    or TargetStatus.SnapshotStale);

            if (targetPerSqm > 0m && marginEligible)
            {
                // Status margin: judged given vs the required target.
                decimal margin = (judged - targetPerSqm)
                                 / targetPerSqm * 100m;
                r.MarginAgainstTargetPercent = Math.Round(margin, 2);

                // Re-derive the classification whenever the result is
                // already in a CLASSIFIED state — not only from UpToDate.
                // ApplyUInsert calls FinalizeValues again after netting an
                // included U-insert; with the guard limited to UpToDate the
                // second pass recomputed the margin but left the status
                // frozen (a toggle-off quotation could show -2.20% beside a
                // stale ABOVE TARGET). Safe because marginEligible already
                // excludes every incomplete state.
                if (r.Status is TargetStatus.UpToDate
                                or TargetStatus.BelowTarget
                                or TargetStatus.AtTarget
                                or TargetStatus.AboveTarget)
                {
                    decimal m = r.MarginAgainstTargetPercent.Value;
                    r.Status = TargetMarginTolerance.IsBelow(m)
                        ? TargetStatus.BelowTarget
                        : TargetMarginTolerance.IsAbove(m)
                            ? TargetStatus.AboveTarget
                            : TargetStatus.AtTarget;
                }
            }

            // Base-price judgement (the KPI trigger): measured from the
            // BASE price, never from the final required target.
            if (r.BasePricePerSqm > 0m && marginEligible)
            {
                decimal extra = judged - r.BasePricePerSqm;
                r.ExtraProfitPerSqm = MoneyRounding.Round(extra);
                r.ExtraProfitMarginPercent = Math.Round(
                    extra / r.BasePricePerSqm * 100m, 2);
            }
            else
            {
                r.ExtraProfitPerSqm = null;
                r.ExtraProfitMarginPercent = null;
            }
        }
        else
        {
            r.DifferencePerSqm = 0m;
            r.DifferenceValue = 0m;
            r.MarginAgainstTargetPercent = null;
            r.GivenPriceJudgedPerSqm = 0m;
            r.ExtraProfitPerSqm = null;
            r.ExtraProfitMarginPercent = null;

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

/// <summary>
/// SaaS adapter for the WPF's ProfitLossStepViewModel.TargetPricing.cs
/// RebuildTargetResults loop + BuildRateCard. Maps the normalized
/// SaaS spec-group inputs onto the rate card, then runs the verbatim
/// calculator + U-insert resolver + aggregator.
///
/// DOCUMENTED DIFFERENCES from the WPF adapter (WPF_TO_SAAS_MAPPING
/// §1.3d):
///   • No live Step 4 panel fallbacks (_dguVm?.OuterBasePrice etc.) — a
///     server has no live panel. The per-group Step 4 handoff columns are
///     the only source; when they are absent the result is honestly
///     Missing* (never a guess).
///   • No Step 6 wastage-source price rescue for legacy rows — same
///     reason (the SaaS grid always carries its own columns).
///   • DGU+LAM third pane: the WPF passed groupRate 0 (its SpecGroup has
///     no third-pane column) and relied on the live panel; the SaaS
///     persisted spec groups carry third_sheet_price_per_sqm (0010), so
///     the handoff-first rule now covers the third pane too.
///   • TargetUpliftPercent: the WPF reads the Step 9 "expected profit
///     margin %" operator box (default 15); the SaaS fixes the approved
///     15% (the box is not a persisted input there — documented).
///   • Yield: same derivation, yield = 1 − wastage%/100 with the ≤0 →
///     0.05 floor (WPF EffectiveYieldFactor).
///   • Area: PnLCalculator.MeasureGroup (the same cut-area measure the
///     WPF's PnLMeasurement.MeasureGroup produces); groups with no cut
///     area are skipped exactly like the WPF loop.
/// </summary>
public static class SpecificationTargetBuilder
{
    public static GlassBuildType BuildTypeOf(string? effectiveGlassType) =>
        (effectiveGlassType ?? "") switch
        {
            "SGU" => GlassBuildType.Sgu,
            "DGU" => GlassBuildType.Dgu,
            "LAM" => GlassBuildType.Lam,
            "DGU+LAM" => GlassBuildType.DguLam,
            _ => GlassBuildType.Unknown
        };

    /// <summary>WPF EffectiveYieldFactor: yield = 1 − wastage%/100, with
    /// the ≤0 → 0.05 floor (a valid box never trips it).</summary>
    public static decimal EffectiveYieldFactor(decimal wastagePercent)
    {
        decimal yield = 1m - (wastagePercent / 100m);
        return yield <= 0m ? 0.05m : yield;
    }

    /// <summary>
    /// The commercial target-rate card from the group's persisted Step 4
    /// handoff columns (outer/inner/third pane prices, spacer,
    /// interlayer, cutting, tempering). Handoff-only by design — see the
    /// class-level SaaS notes.
    /// </summary>
    public static SpecificationRateCard? BuildRateCard(
        SpecGroupInput group, decimal yieldFactor, decimal upliftPercent)
    {
        switch (BuildTypeOf(group.EffectiveGlassType))
        {
            case GlassBuildType.Dgu:
                return new SpecificationRateCard
                {
                    BuildType = GlassBuildType.Dgu,
                    OuterGlassRatePerSqm = group.OuterSheetPricePerSqm,
                    InnerGlassRatePerSqm = group.InnerSheetPricePerSqm,
                    AspRatePerSqm = group.Step4SpacerPricePerSqm ?? 0m,
                    ProcessingRatePerSqm = 0m,
                    YieldFactor = yieldFactor,
                    TargetUpliftPercent = upliftPercent
                };

            case GlassBuildType.Sgu:
                return new SpecificationRateCard
                {
                    BuildType = GlassBuildType.Sgu,
                    OuterGlassRatePerSqm = group.OuterSheetPricePerSqm,
                    InnerGlassRatePerSqm = 0m,
                    AspRatePerSqm = 0m,
                    ProcessingRatePerSqm = (group.Step4CuttingCostPerSqm ?? 0m)
                                           + (group.Step4TemperingCostPerSqm ?? 0m),
                    YieldFactor = yieldFactor,
                    TargetUpliftPercent = upliftPercent
                };

            case GlassBuildType.Lam:
                return new SpecificationRateCard
                {
                    BuildType = GlassBuildType.Lam,
                    OuterGlassRatePerSqm = group.OuterSheetPricePerSqm,
                    InnerGlassRatePerSqm = group.InnerSheetPricePerSqm,
                    AspRatePerSqm = 0m,
                    ProcessingRatePerSqm = (group.Step4InterlayerCostPerSqm ?? 0m)
                                           + (group.Step4CuttingCostPerSqm ?? 0m)
                                           + (group.Step4TemperingCostPerSqm ?? 0m),
                    YieldFactor = yieldFactor,
                    TargetUpliftPercent = upliftPercent
                };

            case GlassBuildType.DguLam:
                return new SpecificationRateCard
                {
                    BuildType = GlassBuildType.DguLam,
                    OuterGlassRatePerSqm = group.OuterSheetPricePerSqm,
                    InnerGlassRatePerSqm = group.InnerSheetPricePerSqm,
                    ThirdGlassRatePerSqm = group.ThirdSheetPricePerSqm,
                    AspRatePerSqm = group.Step4SpacerPricePerSqm ?? 0m,
                    ProcessingRatePerSqm = (group.Step4CuttingCostPerSqm ?? 0m)
                                           + (group.Step4TemperingCostPerSqm ?? 0m)
                                           + (group.Step4InterlayerCostPerSqm ?? 0m),
                    YieldFactor = yieldFactor,
                    TargetUpliftPercent = upliftPercent
                };

            default:
                return null;
        }
    }

    /// <summary>
    /// One specification's target result from the SaaS grid inputs: rate
    /// card + given price + cut area + U-insert resolution, exactly the
    /// WPF RebuildTargetResults loop body.
    /// </summary>
    public static SpecTargetPriceResult CalculateGroup(
        SpecGroupInput group,
        Step6UInsertQuery uInsertQuery,
        decimal wastagePercent,
        decimal upliftPercent = SpecificationTargetCalculator.DefaultTargetUpliftPercent)
    {
        var buildType = BuildTypeOf(group.EffectiveGlassType);
        var card = BuildRateCard(group, EffectiveYieldFactor(wastagePercent), upliftPercent);

        var result = SpecificationTargetCalculator.Calculate(
            group.GroupNumber,
            group.SpecName,
            group.GlassType,
            buildType,
            card,
            Step5PriceSource.GivenPricePerSqm(group.RatePerSqm),
            PnLCalculator.MeasureGroup(group));

        SpecificationTargetCalculator.ApplyUInsert(
            result, Step6ChargeResolver.ResolveUInsert(uInsertQuery));
        return result;
    }

    /// <summary>
    /// The U-insert query for one group, from the persisted Step 6
    /// charges (WPF FindUInsertRow / UInsertRowActiveForSpec): the FIRST
    /// U_INSERT row decides everything — its rate is read even when the
    /// row is inactive, and "active for this spec" requires the row to
    /// be active AND this group to be checked in its two-part targeting
    /// (an empty targeting list targets no spec — same as the WPF's
    /// TargetSelection with nothing checked).
    /// </summary>
    public static Step6UInsertQuery BuildUInsertQuery(
        SpecGroupInput group,
        IReadOnlyList<(bool IsActive, IReadOnlyList<int>? Targeting, decimal Rate)> uInsertRows,
        decimal uInsertTemplateDefaultRate)
    {
        var row = uInsertRows.Count > 0 ? uInsertRows[0] : ((bool)false, null, 0m);

        bool requires = group.UInsertPricePerSqm > 0m
                        || $"{group.SpecName} {group.GlassType}"
                            .IndexOf("U-Insert", StringComparison.OrdinalIgnoreCase) >= 0
                        || ChargeAutomation
                            .GetChargeSuggestions(
                                group.EffectiveGlassType, group.SpecName,
                                ChargeMetrics.ForGroup(group).IsMatchedSameGlass)
                            .Any(s => s.TemplateKey.Equals("U_INSERT",
                                StringComparison.OrdinalIgnoreCase));

        return new Step6UInsertQuery
        {
            SpecRequiresUInsert = requires,
            Step4SeedRate = group.UInsertPricePerSqm ?? 0m,
            RowExists = uInsertRows.Count > 0,
            RowActiveForSpec = uInsertRows.Count > 0
                               && row.IsActive
                               && (row.Targeting ?? Array.Empty<int>())
                                   .Contains(group.GroupNumber),
            RowRate = uInsertRows.Count > 0 ? row.Rate : 0m,
            TemplateDefaultRate = uInsertTemplateDefaultRate
        };
    }

    /// <summary>
    /// The whole job: per-group results (groups without cut area are
    /// skipped, WPF loop) aggregated by TargetProfitabilityAggregator.
    /// </summary>
    public static TargetPriceResult BuildJob(
        IReadOnlyList<SpecGroupInput> groups,
        Func<SpecGroupInput, Step6UInsertQuery> uInsertQueryFor,
        decimal wastagePercent,
        decimal upliftPercent = SpecificationTargetCalculator.DefaultTargetUpliftPercent)
    {
        var specs = new List<SpecTargetPriceResult>();
        foreach (var group in groups)
        {
            if (PnLCalculator.MeasureGroup(group) <= 0m) continue;
            specs.Add(CalculateGroup(group, uInsertQueryFor(group), wastagePercent, upliftPercent));
        }

        return TargetProfitabilityAggregator.AggregateJob(specs);
    }
}
