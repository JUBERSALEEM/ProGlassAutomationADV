// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   ViewModels/ProformaInvoice/Step4PricingEstimator/
//     PricingEstimatorViewModel.Part2.cs   — SGU/DGU/LAM rate + cost basis
//     DguLamParameters/DguLamParametersViewModel.cs — hybrid rule set
// The WPF files are the source of truth; every step, fallback and warning
// text below is preserved. Only the shape changed: pure functions over
// parameter records instead of ViewModel properties.
//
// Golden values are pinned by ProGlass.Saas.Domain.Tests.RateCalculatorTests
// (they are the WPF's own DguLamPricingTests numbers — never re-derive).
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Money;

namespace ProGlass.Saas.Domain.Pricing;

/// <summary>The four glass types the estimator prices (WPF codes).</summary>
public static class GlassTypes
{
    public const string Sgu = "SGU";
    public const string Dgu = "DGU";
    public const string Lam = "LAM";
    public const string DguLam = "DGU+LAM";

    public static readonly string[] All = { Sgu, Dgu, Lam, DguLam };
}

/// <summary>Estimated rate + pre-margin cost basis + the same warnings the
/// WPF surfaces (BUG-40/41 yield fallback, unpriced interlayer).</summary>
public sealed record RateResult(
    decimal RatePerSqm, decimal CostBasisPerSqm, IReadOnlyList<string> Warnings);

// ── parameter sets (the WPF panel fields, one record per panel) ────────

public sealed record SguParameters(
    decimal BasePricePerSqm,
    decimal YieldFactor,
    decimal CuttingCostPerSqm,
    decimal TemperingCostPerSqm,
    decimal TargetMarginPercent);

public sealed record DguParameters(
    decimal OuterBasePrice,
    decimal InnerBasePrice,
    decimal SpacerPrice,
    decimal YieldFactor,
    decimal MarginProfitPercent);

public sealed record LamParameters(
    decimal OuterBasePrice,
    decimal InnerBasePrice,
    decimal PvbMaterialRate,
    decimal OuterCuttingCost,
    decimal InnerCuttingCost,
    decimal OuterTemperingCost,
    decimal InnerTemperingCost,
    decimal YieldFactor,
    decimal MarginProfitPercent);

/// <summary>DGU+LAM — a THREE-pane hybrid (outer + air spacer + laminated
/// outer + interlayer + laminated inner). Per-pane processing fields are
/// required: panes can temper at different rates (WPF: outer 12, laminated
/// 10 each) and a shared rate would misprice every unit whose panes
/// differ.</summary>
public sealed record DguLamParameters(
    decimal OuterBasePrice,
    decimal LamOuterBasePrice,
    decimal LamInnerBasePrice,
    decimal OuterCuttingCost,
    decimal LamOuterCuttingCost,
    decimal LamInnerCuttingCost,
    decimal OuterTemperingCost,
    decimal LamOuterTemperingCost,
    decimal LamInnerTemperingCost,
    decimal InterlayerSellPrice,
    string InterlayerType,
    decimal SpacerPrice,
    decimal YieldFactor,
    decimal MarginProfitPercent)
{
    public decimal TotalCuttingCost =>
        OuterCuttingCost + LamOuterCuttingCost + LamInnerCuttingCost;

    public decimal TotalTemperingCost =>
        OuterTemperingCost + LamOuterTemperingCost + LamInnerTemperingCost;
}

/// <summary>
/// The Step 4 rate rules, WPF-verbatim. The yield factor divides the GLASS
/// term only; processing, interlayer and spacer are added AFTER it; the
/// margin multiplies everything. A yield ≤ 0 falls back to 1.0 WITH a
/// warning instead of silently resetting (BUG-40/41). An unpriced
/// interlayer is called out rather than quietly producing a low rate.
/// </summary>
public static class RateCalculator
{
    /// <summary>Yield never divides by zero; mirrors the WPF rate methods.</summary>
    private static decimal SafeYield(decimal yieldFactor) =>
        yieldFactor <= 0m ? 1m : yieldFactor;

    // ── SGU ────────────────────────────────────────────────────────
    // Step 1: raw = Base Price per SQM
    // Step 2: cost = raw / Yield Factor
    // Step 3: + Cutting Cost
    // Step 4: + Tempering Cost
    // Step 5: * (1 + Margin/100)

    public static RateResult Calculate(SguParameters p)
    {
        var warnings = new List<string>();
        var yieldFactor = p.YieldFactor;
        if (yieldFactor <= 0m)
        {
            warnings.Add(
                $"⚠  SGU yield factor must be > 0 (currently {yieldFactor:0.###}). " +
                "Using 1.0 as fallback.");
            yieldFactor = 1m;
        }

        decimal step1 = p.BasePricePerSqm;
        decimal step2 = step1 / yieldFactor;
        decimal step3 = step2 + p.CuttingCostPerSqm;
        decimal step4 = step3 + p.TemperingCostPerSqm;
        decimal step5 = step4 * (1m + (p.TargetMarginPercent / 100m));

        var basis = MoneyRounding.Round(
            p.BasePricePerSqm / SafeYield(p.YieldFactor)
            + p.CuttingCostPerSqm
            + p.TemperingCostPerSqm);

        return new RateResult(MoneyRounding.Round(step5), basis, warnings);
    }

    // ── DGU ────────────────────────────────────────────────────────
    // step1 = outer + inner; step2 = / yield; step3 = + spacer;
    // step4 = * (1 + margin/100)

    public static RateResult Calculate(DguParameters p)
    {
        var warnings = new List<string>();
        var yieldFactor = p.YieldFactor;
        if (yieldFactor <= 0m)
        {
            warnings.Add(
                $"⚠  DGU yield factor must be > 0 (currently {yieldFactor:0.###}). " +
                "Using 1.0 as fallback.");
            yieldFactor = 1m;
        }

        decimal step1 = p.OuterBasePrice + p.InnerBasePrice;
        decimal step2 = step1 / yieldFactor;
        decimal step3 = step2 + p.SpacerPrice;
        decimal step4 = step3 * (1m + (p.MarginProfitPercent / 100m));

        var basis = MoneyRounding.Round(
            (p.OuterBasePrice + p.InnerBasePrice) / SafeYield(p.YieldFactor)
            + p.SpacerPrice);

        return new RateResult(MoneyRounding.Round(step4), basis, warnings);
    }

    // ── LAM ────────────────────────────────────────────────────────
    // step1 = outer + inner; step2 = / yield; step3 = + PVB;
    // step4 = + both cutting; step5 = + both tempering; step6 = * margin

    public static RateResult Calculate(LamParameters p)
    {
        var warnings = new List<string>();
        var yieldFactor = p.YieldFactor;
        if (yieldFactor <= 0m)
        {
            warnings.Add(
                $"⚠  LAM yield factor must be > 0 (currently {yieldFactor:0.###}). " +
                "Using 1.0 as fallback.");
            yieldFactor = 1m;
        }

        decimal step1 = p.OuterBasePrice + p.InnerBasePrice;
        decimal step2 = step1 / yieldFactor;
        decimal step3 = step2 + p.PvbMaterialRate;
        decimal step4 = step3 + p.OuterCuttingCost + p.InnerCuttingCost;
        decimal step5 = step4 + p.OuterTemperingCost + p.InnerTemperingCost;
        decimal step6 = step5 * (1m + (p.MarginProfitPercent / 100m));

        var basis = MoneyRounding.Round(
            (p.OuterBasePrice + p.InnerBasePrice) / SafeYield(p.YieldFactor)
            + p.PvbMaterialRate
            + p.OuterCuttingCost + p.InnerCuttingCost
            + p.OuterTemperingCost + p.InnerTemperingCost);

        return new RateResult(MoneyRounding.Round(step6), basis, warnings);
    }

    // ── DGU+LAM — the business's own worked chain (verbatim header):
    //
    //     48 + 25 + 25                        = 98      three panes of glass
    //     98 / 0.85 (yield)                   = 115.29
    //     + cutting     10 + 10 + 10   (30)   = 145.29
    //     + tempering   12 + 10 + 10   (32)   = 177.29
    //     + 110 outsourced PVB                = 287.29
    //     x 1.20 (20% profit)                 = 344.75  per SQM
    //
    // 1. Cutting and tempering are charged on ALL THREE panes.
    // 2. The air spacer is NOT a line in this price by default
    //    (SpacerPrice = 0; including it would quote 398.75 instead of
    //    344.75). It stays editable — opt-in, never silently added.
    // 3. The interlayer is added BEFORE the margin and IS marked up
    //    (110 × 1.20 = 132, not 110).
    // 4. The yield factor divides the glass term ONLY.

    public static RateResult Calculate(DguLamParameters p)
    {
        var warnings = new List<string>();

        var yieldFactor = p.YieldFactor;
        if (yieldFactor <= 0m)
        {
            warnings.Add(
                "⚠  Yield factor must be greater than 0 " +
                $"(currently {yieldFactor:0.###}). Using 1.0 instead.");
            yieldFactor = 1m;
        }

        decimal glass = p.OuterBasePrice + p.LamOuterBasePrice + p.LamInnerBasePrice;
        decimal running = glass / yieldFactor;
        running += p.TotalCuttingCost;
        running += p.TotalTemperingCost;
        running += p.InterlayerSellPrice;
        // Zero unless this job has been given a spacer charge.
        running += p.SpacerPrice;
        decimal rate = running * (1m + (p.MarginProfitPercent / 100m));

        // An unpriced interlayer is the one mistake that silently
        // undercharges a whole job, so it is called out rather than
        // quietly producing a low rate.
        if (p.InterlayerSellPrice <= 0m)
        {
            warnings.Add(
                $"⚠  No selling price set for the {p.InterlayerType} " +
                "interlayer, so it is NOT included in this rate.");
        }

        var basis = MoneyRounding.Round(
            (p.OuterBasePrice + p.LamOuterBasePrice + p.LamInnerBasePrice)
            / SafeYield(p.YieldFactor)
            + p.TotalCuttingCost
            + p.TotalTemperingCost
            + p.InterlayerSellPrice
            + p.SpacerPrice);

        return new RateResult(MoneyRounding.Round(rate), basis, warnings);
    }
}
