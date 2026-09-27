// ═══════════════════════════════════════════════════════════════════════
// GOLDEN VALUES — ported from the WPF's own test suites and in-code
// worked chains (read 2026-09-27). These numbers come from the business;
// they are never re-derived here:
//   Tests/Agent/DguLamPricingTests.cs        — the DGU+LAM chain
//   PricingEstimatorViewModel.Part2.cs       — DGU/LAM/basis examples
//   DguLamParametersViewModel.CalculateRate  — the hybrid chain
// A single end-total test can pass while two errors cancel out, so every
// intermediate step is pinned too (the WPF file says exactly this).
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Money;
using ProGlass.Saas.Domain.Pricing;
using Xunit;

namespace ProGlass.Saas.Domain.Tests;

public class RateCalculatorTests
{
    // ════════════════════════════════════════════════════════════════
    //  SGU — (base/yield + cutting + tempering) × (1 + margin/100)
    //  WPF default panel: 46 / 0.85, cutting 15, tempering 25, margin 15
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void Sgu_default_panel_rate()
    {
        // 46 / 0.85 = 54.117647…  + 15 = 69.117647…  + 25 = 94.117647…
        // × 1.15 = 108.235294…  → 108.24
        var r = RateCalculator.Calculate(new SguParameters(
            46m, 0.85m, 15m, 25m, 15m));

        Assert.Equal(108.24m, r.RatePerSqm);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Sgu_cost_basis_is_pre_margin()
    {
        var r = RateCalculator.Calculate(new SguParameters(
            46m, 0.85m, 15m, 25m, 15m));

        Assert.Equal(94.12m, r.CostBasisPerSqm);   // 94.117647… rounded
    }

    // ════════════════════════════════════════════════════════════════
    //  DGU — ((outer + inner)/yield + spacer) × (1 + margin/100)
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void Dgu_rate_with_wpf_default_panel()
    {
        // (46 + 46) / 0.85 = 108.235294…  + 45 (12mm NMA spacer) =
        // 153.235294…  × 1.15 = 176.215588…  → 176.22
        var r = RateCalculator.Calculate(new DguParameters(
            46m, 46m, 45m, 0.85m, 15m));

        Assert.Equal(176.22m, r.RatePerSqm);
        Assert.Empty(r.Warnings);
    }

    /// <summary>The WPF's own in-code worked example (Part2.cs): basis
    /// (25 + 25)/0.85 + 45 = 103.82 per SQM for "6mm Clear FT + 12mm ASP
    /// + 6mm Clear FT".</summary>
    [Fact]
    public void Dgu_cost_basis_wpf_worked_example()
    {
        var r = RateCalculator.Calculate(new DguParameters(
            25m, 25m, 45m, 0.85m, 15m));

        Assert.Equal(103.82m, r.CostBasisPerSqm);
        // …and the rate it produces: 103.823529… × 1.15 = 119.397058… → 119.40
        Assert.Equal(119.40m, r.RatePerSqm);
    }

    [Fact]
    public void Dgu_zero_yield_falls_back_to_one_with_wpf_warning_text()
    {
        var r = RateCalculator.Calculate(new DguParameters(
            46m, 46m, 45m, 0m, 15m));

        // Yield fell back to 1.0, so the glass term is 92 not 108.235…
        Assert.Equal(MoneyRounding.Round((92m + 45m) * 1.15m), r.RatePerSqm);

        var warning = Assert.Single(r.Warnings);
        Assert.Equal(
            "⚠  DGU yield factor must be > 0 (currently 0). Using 1.0 as fallback.",
            warning);
    }

    // ════════════════════════════════════════════════════════════════
    //  LAM — ((outer + inner)/yield + PVB + both cutting + both
    //  tempering) × (1 + margin/100)
    //  WPF in-code example (Part2.cs): 50/0.85 → 58.82; + 110 (PVB) →
    //  168.82; + 10 → 178.82; + 20 → 198.82; × 1.15 = 228.65
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void Lam_wpf_worked_example_rates_to_228_65()
    {
        var r = RateCalculator.Calculate(new LamParameters(
            25m, 25m, 110m, 10m, 0m, 20m, 0m, 0.85m, 15m));

        Assert.Equal(228.65m, r.RatePerSqm);
        Assert.Empty(r.Warnings);
    }

    [Fact]
    public void Lam_default_panel_rate()
    {
        // (46 + 46)/0.85 = 108.235294… + 80 = 188.235294…
        // + 15 + 15 = 218.235294… + 25 + 25 = 268.235294… × 1.15 = 308.47
        var r = RateCalculator.Calculate(new LamParameters(
            46m, 46m, 80m, 15m, 15m, 25m, 25m, 0.85m, 15m));

        Assert.Equal(308.47m, r.RatePerSqm);
        Assert.Equal(268.24m, r.CostBasisPerSqm);
    }

    [Fact]
    public void Lam_negative_yield_uses_the_fallback_and_warns()
    {
        var r = RateCalculator.Calculate(new LamParameters(
            46m, 46m, 80m, 15m, 15m, 25m, 25m, -0.2m, 15m));

        Assert.True(r.RatePerSqm > 0m);
        var warning = Assert.Single(r.Warnings);
        Assert.StartsWith("⚠  LAM yield factor must be > 0 (currently -0.2)", warning);
    }

    // ════════════════════════════════════════════════════════════════
    //  DGU+LAM — the business's own chain, verbatim:
    //      48 + 25 + 25                      = 98
    //      98 / 0.85 (yield)                 = 115.29
    //      + cutting     10 + 10 + 10  (30)  = 145.29
    //      + tempering   12 + 10 + 10  (32)  = 177.29
    //      + 110 outsourced PVB              = 287.29
    //      x 1.20 (20% profit)               = 344.75 per SQM
    //  (Tests/Agent/DguLamPricingTests.cs — the shipped defaults ARE the
    //  worked example.)
    // ════════════════════════════════════════════════════════════════

    private static DguLamParameters BusinessChain() => new(
        OuterBasePrice: 48m,
        LamOuterBasePrice: 25m,
        LamInnerBasePrice: 25m,
        OuterCuttingCost: 10m,
        LamOuterCuttingCost: 10m,
        LamInnerCuttingCost: 10m,
        OuterTemperingCost: 12m,
        LamOuterTemperingCost: 10m,
        LamInnerTemperingCost: 10m,
        InterlayerSellPrice: 110m,
        InterlayerType: "PVB",
        SpacerPrice: 0m,
        YieldFactor: 0.85m,
        MarginProfitPercent: 20m);

    [Fact]
    public void DguLam_default_parameters_quote_the_business_rate()
    {
        Assert.Equal(344.75m, RateCalculator.Calculate(BusinessChain()).RatePerSqm);
    }

    [Fact]
    public void DguLam_default_rate_reports_no_warning()
    {
        Assert.Empty(RateCalculator.Calculate(BusinessChain()).Warnings);
    }

    [Fact]
    public void DguLam_three_panes_sum_to_ninety_eight()
    {
        var p = BusinessChain();
        Assert.Equal(98m, p.OuterBasePrice + p.LamOuterBasePrice + p.LamInnerBasePrice);
    }

    [Fact]
    public void DguLam_cutting_totals_thirty_across_three_panes()
    {
        Assert.Equal(30m, BusinessChain().TotalCuttingCost);
    }

    [Fact]
    public void DguLam_tempering_totals_thirty_two_across_three_panes()
    {
        // 12 + 10 + 10. The outer pane is dearer, so this total is NOT
        // three times any single rate.
        Assert.Equal(32m, BusinessChain().TotalTemperingCost);
    }

    [Fact]
    public void DguLam_cost_basis_is_287_29()
    {
        Assert.Equal(287.29m, RateCalculator.Calculate(BusinessChain()).CostBasisPerSqm);
    }

    [Fact]
    public void DguLam_spacer_is_not_charged_by_default()
    {
        // The supplied chain runs tempering straight into the interlayer.
        // Charging 45 here would quote 398.75.
        var p = BusinessChain();
        Assert.Equal(0m, p.SpacerPrice);
        Assert.Equal(344.75m, RateCalculator.Calculate(p).RatePerSqm);
    }

    [Fact]
    public void DguLam_pricing_a_spacer_raises_the_rate_by_amount_plus_margin()
    {
        // 45 enters the subtotal, so it carries the 20% margin too.
        var p = BusinessChain() with { SpacerPrice = 45m };
        Assert.Equal(344.75m + (45m * 1.20m), RateCalculator.Calculate(p).RatePerSqm);
    }

    [Fact]
    public void DguLam_interlayer_is_inside_the_margin()
    {
        var withPvb = RateCalculator.Calculate(BusinessChain()).RatePerSqm;
        var withoutPvb = RateCalculator.Calculate(
            BusinessChain() with { InterlayerSellPrice = 0m }).RatePerSqm;

        // If the interlayer were added after the margin the difference
        // would be exactly 110. It is inside, so it is 110 × 1.20.
        Assert.Equal(132m, withPvb - withoutPvb);
    }

    [Fact]
    public void DguLam_missing_interlayer_price_is_reported_not_silently_dropped()
    {
        var r = RateCalculator.Calculate(
            BusinessChain() with { InterlayerSellPrice = 0m });

        var warning = Assert.Single(r.Warnings);
        Assert.Equal(
            "⚠  No selling price set for the PVB interlayer, " +
            "so it is NOT included in this rate.",
            warning);
    }

    [Fact]
    public void DguLam_sgp_names_the_chemistry_in_the_warning()
    {
        // SGP ships 0/0 on purpose (no price was ever quoted for it) —
        // the rate call must warn, not invent.
        var r = RateCalculator.Calculate(
            BusinessChain() with { InterlayerType = "SGP", InterlayerSellPrice = 0m });

        Assert.Contains("SGP", Assert.Single(r.Warnings));
    }

    [Fact]
    public void DguLam_yield_divides_glass_only_not_the_interlayer()
    {
        var r = RateCalculator.Calculate(BusinessChain() with { YieldFactor = 1m });

        // Only the glass term changes: 98 − 115.294… = −17.294…, then
        // × 1.20. If yield also touched the interlayer or the processing
        // the delta would be far larger.
        decimal expected = (98m + 30m + 32m + 110m) * 1.20m;

        Assert.Equal(decimal.Round(expected, 2), r.RatePerSqm);
    }

    [Fact]
    public void DguLam_zero_yield_is_survived_and_reported()
    {
        var r = RateCalculator.Calculate(BusinessChain() with { YieldFactor = 0m });

        Assert.True(r.RatePerSqm > 0m);   // fell back to 1.0, no divide-by-zero

        var warning = Assert.Single(r.Warnings);
        Assert.Equal(
            "⚠  Yield factor must be greater than 0 (currently 0). Using 1.0 instead.",
            warning);
    }

    [Fact]
    public void DguLam_zero_yield_and_unpriced_interlayer_warn_twice()
    {
        var r = RateCalculator.Calculate(BusinessChain() with
        {
            YieldFactor = 0m,
            InterlayerSellPrice = 0m,
        });

        Assert.Equal(2, r.Warnings.Count);
        Assert.Contains(r.Warnings, w => w.Contains("Yield"));
        Assert.Contains(r.Warnings, w => w.Contains("interlayer"));
    }
}
