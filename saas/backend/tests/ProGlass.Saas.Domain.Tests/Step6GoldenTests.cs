// ═══════════════════════════════════════════════════════════════════════
// GOLDEN VALUES — the Step 6 math, pinned to the WPF sources (read
// 2026-09-27):
//   Models/…/Step6OtherCharges/MetricOption.cs (metric registry)
//   Models/…/Step6OtherCharges/Wastages/WastageRow.cs (the 85% formula)
//   ViewModels/…/MetricDiscoveryHubViewModel.Refresh (per-spec metrics)
//   Services/ProformaInvoice/Export/ProformaChargeMetricsCalculator
//     (CalcPolishSingle / ComputePolishLm / UsesOuterDimensions)
//   ViewModels/…/Step9ProfitLoss/PnLSpecParser.cs (the spec grammar)
// Expected numbers were computed independently with Python decimal from
// the pinned formulas — never re-derived from the port itself.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Proforma;
using Xunit;

namespace ProGlass.Saas.Domain.Tests;

public class SpecGrammarTests
{
    // ── the grammar (PnLSpecParser) ─────────────────────────────────

    [Theory]
    [InlineData("6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass", 2)]
    [InlineData("6mm Clear FT Glass + 12mm Black Warm Edge ASP + 6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass", 3)]
    [InlineData("6mm Kibbing Nova 55 FT Glass + 12mm ASP + 6mm Clear FT Glass", 2)] // digits in brand names
    [InlineData("8mm Clear Annealed Glass", 1)]
    [InlineData("10mm Reflective Grey Heat Strengthened Glass + 1.52 PVB + 10mm Clear FT Glass", 2)]
    [InlineData("hand-written free text", 0)]
    public void GlassMatches_counts_panes(string spec, int expected)
    {
        Assert.Equal(expected, SpecGrammar.GlassMatches(spec).Count);
    }

    [Fact]
    public void Two_panes_resolve_outer_first_inner_last()
    {
        var (outer, inner) = SpecGrammar.ResolveTwoPanes(
            "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass");

        Assert.Equal("HD Grey", outer!.Color);
        Assert.Equal(6, outer.ThicknessMm);
        Assert.Equal("FT", outer.Treatment);
        Assert.True(outer.RequiresTempering);
        Assert.Equal("Clear", inner!.Color);
        Assert.Equal("Inner", inner.Side);
    }

    [Fact]
    public void Triple_glazed_inner_is_the_last_pane_not_the_middle()
    {
        var panes = SpecGrammar.ResolveAllPanes(
            "6mm A FT Glass + 12mm ASP + 6mm B FT Glass + 12mm ASP + 6mm C FT Glass");

        Assert.Equal(3, panes.Count);
        Assert.Equal("Outer", panes[0].Side);
        Assert.Equal("Middle", panes[1].Side); // count == 3 → "Middle"
        Assert.Equal("B", panes[1].Color);
        Assert.Equal("Inner", panes[2].Side);
        Assert.Equal("C", panes[2].Color);
    }

    // ── effective glass type (ResolveEffectiveGlassType) ────────────

    [Theory]
    [InlineData("SGU", "6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass", "DGU")] // spacer marker wins
    [InlineData("", "6mm Clear FT Glass + 1.52 PVB + 6mm Clear FT Glass", "LAM")]
    [InlineData("", "6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass + 1.52 PVB + 6mm Clear FT Glass", "DGU+LAM")]
    [InlineData("DGU", "6mm Clear FT Glass", "DGU")] // type authoritative when ambiguous
    [InlineData("dgu", "6mm Clear FT Glass", "DGU")] // case-normalised
    [InlineData("", "6mm Clear FT Glass", "SGU")]    // has glass, no marker → SGU
    [InlineData("", "", "")]                          // nothing at all
    public void Effective_type_markers_win_then_type_field(string type, string spec, string expected)
    {
        Assert.Equal(expected, SpecGrammar.ResolveEffectiveGlassType(type, spec));
    }

    // ── matched same-glass laminate (LaminateGlassMatcher) ──────────

    [Fact]
    public void Same_parsed_colours_match()
    {
        Assert.True(SpecGrammar.IsMatchedSameGlass(
            "6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass", "DGU"));
    }

    [Fact]
    public void Mixed_parsed_colours_do_not_match()
    {
        Assert.False(SpecGrammar.IsMatchedSameGlass(
            "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass", "DGU"));
    }

    [Fact]
    public void Explicit_matching_colours_match_and_win()
    {
        Assert.True(SpecGrammar.IsMatchedSameGlass(
            "anything", "LAM", "Clear", "clear")); // case-insensitive
    }

    [Fact]
    public void Explicit_mismatching_colours_never_fall_back_to_the_name()
    {
        Assert.False(SpecGrammar.IsMatchedSameGlass(
            "6mm Clear FT Glass + 1.52 PVB + 6mm Clear FT Glass", "LAM",
            "HD Grey", "Clear"));
    }

    [Fact]
    public void Non_laminate_types_never_match()
    {
        Assert.False(SpecGrammar.IsMatchedSameGlass(
            "6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass", "DGU",
            "Clear", "Clear"));
    }
}

public class ChargeMetricsTests
{
    private static readonly SpecGroupInput Group = new(
        1, "SGU", "6mm Clear FT Glass", "", "",
        new List<GridRow>
        {
            new(1000, 2000, 0, 0, 3, 2), // sqm1 2.000 × 3 = 6.000; lm1 18.000
            new(1000, 2000, 900, 900, 2, 2), // adds W2/H2 pane
        });

    [Fact]
    public void Per_group_metrics_match_the_wpf_footer_hub()
    {
        var m = ChargeMetrics.ForGroup(Group);

        Assert.False(m.IsDoubleGlass);
        Assert.Equal(1, m.PaneFactor);
        Assert.Equal(18.00m, m.Lm1);
        Assert.Equal(7.20m, m.Lm2);   // (900+900)/1000 × 2 × 2
        Assert.Equal(6.00m, m.Sqm1);
        Assert.Equal(1.62m, m.Sqm2);  // 0.810 × 2
        Assert.Equal(5, m.Qty);
    }

    [Fact]
    public void Pane_factor_follows_the_effective_type()
    {
        var dgu = Group with { GlassType = "DGU" };
        var dguLam = Group with { GlassType = "DGU+LAM" };

        Assert.True(ChargeMetrics.ForGroup(dgu).IsDoubleGlass);
        Assert.Equal(2, ChargeMetrics.ForGroup(dgu).PaneFactor);
        Assert.Equal(3, ChargeMetrics.ForGroup(dguLam).PaneFactor);
    }

    [Fact]
    public void Targeted_metric_sums_only_the_checked_specs()
    {
        var groups = new List<SpecGroupInput> { Group, Group with { GroupNumber = 2 } };

        var both = ChargeMetrics.TargetedMetric(groups, new[] { 1, 2 }, MetricKeys.Sqm1);
        var one = ChargeMetrics.TargetedMetric(groups, new[] { 1 }, MetricKeys.Sqm1);

        Assert.Equal(12.00m, both);
        Assert.Equal(6.00m, one);
    }

    [Fact]
    public void Dgu_cavity_is_the_outer_area_of_a_double_build_only()
    {
        var sgu = ChargeMetrics.ForGroup(Group).GetMetricValue(MetricKeys.DguCavity);
        var dgu = ChargeMetrics.ForGroup(Group with { GlassType = "DGU" })
            .GetMetricValue(MetricKeys.DguCavity);

        Assert.Equal(0m, sgu);
        Assert.Equal(6.00m, dgu);
    }

    [Fact]
    public void Legacy_metric_aliases_normalize()
    {
        Assert.Equal(MetricKeys.Qty, MetricKeys.Normalize("TOTAL_PIECES"));
        Assert.Equal(MetricKeys.Qty, MetricKeys.Normalize("TOTAL_QTY"));
        Assert.Equal("", MetricKeys.Normalize("  "));
    }

    // ── polish (CalcPolishSingle / ComputePolishLm) ─────────────────

    [Theory]
    [InlineData(1000, 2000, PolishSubTypes.AllSidePolish, 6.0)]      // (1+2)×2
    [InlineData(1000, 2000, PolishSubTypes.BothHeightSide, 4.0)]     // 2×2
    [InlineData(1000, 2000, PolishSubTypes.BothWidthSide, 2.0)]      // 1×2
    [InlineData(1000, 2000, PolishSubTypes.BothHeightOneWidth, 5.0)] // 2×2+1
    [InlineData(1000, 2000, PolishSubTypes.OneHeightOneWidth, 3.0)]  // 2+1
    [InlineData(1000, 2000, PolishSubTypes.OneHeightSide, 2.0)]
    [InlineData(1000, 2000, PolishSubTypes.OneWidthSide, 1.0)]
    [InlineData(1000, 2000, PolishSubTypes.AllSidePolishAsPerDwg, 6.0)] // shares base
    [InlineData(1000, 2000, PolishSubTypes.Custom, 0.0)]              // manual qty
    public void Polish_single_pane_formulas(int w, int h, string subType, decimal expected)
    {
        Assert.Equal(expected, PolishSubTypes.CalcPolishSingle(w, h, subType));
    }

    [Fact]
    public void Polish_lm_covers_both_panes_of_a_double_build()
    {
        var dgu = new List<SpecGroupInput> { Group with { GlassType = "DGU" } };

        // SGU: W1 pane only → 18.000. DGU: + W2 pane → 25.200.
        var sgu = ChargeMetrics.PolishLm(
            new List<SpecGroupInput> { Group }, new[] { 1 },
            PolishSubTypes.AllSidePolish, matchedSameGlassOnly: false);
        var both = ChargeMetrics.PolishLm(
            dgu, new[] { 1 }, PolishSubTypes.AllSidePolish, matchedSameGlassOnly: false);

        Assert.Equal(18.000m, sgu);
        Assert.Equal(25.200m, both);
    }

    [Fact]
    public void Matched_same_glass_polish_doubles_the_single_measurement()
    {
        // A same-glass LAM whose rows only carry W1/H1 still polishes both
        // panes (WPF ComputePolishLm matched branch).
        var lam = new List<SpecGroupInput>
        {
            new(1, "LAM", "6mm Clear FT Glass + 1.52 PVB + 6mm Clear FT Glass",
                "Clear", "Clear",
                new List<GridRow> { new(1000, 2000, 0, 0, 3, 2) }),
        };

        var lm = ChargeMetrics.PolishLm(
            lam, new[] { 1 }, PolishSubTypes.AllSidePolish, matchedSameGlassOnly: true);

        Assert.Equal(36.000m, lm); // 6.0 per pane × 2 panes × 3 qty
    }

    // ── wastage sources ─────────────────────────────────────────────

    [Fact]
    public void Sgu_offers_the_outer_source_only()
    {
        var sources = ChargeMetrics.SourcesForGroup(Group);

        Assert.Single(sources);
        Assert.Equal("1-Outer (W1/H1)", sources[0].Key);
        Assert.Equal(6.00m, sources[0].SqmValue);
    }

    [Fact]
    public void Dgu_offers_outer_and_inner_sources()
    {
        var sources = ChargeMetrics.SourcesForGroup(Group with { GlassType = "DGU" });

        Assert.Equal(2, sources.Count);
        Assert.Equal("1-Outer (W1/H1)", sources[0].Key);
        Assert.Equal("1-Inner (W2/H2)", sources[1].Key);
        Assert.Equal(1.62m, sources[1].SqmValue);
    }

    [Fact]
    public void Explicit_W2_H2_rows_offer_inner_even_without_a_type()
    {
        var sources = ChargeMetrics.SourcesForGroup(Group); // SGU but has W2/H2

        Assert.Equal(2, sources.Count);
        Assert.Equal("1-Inner (W2/H2)", sources[1].Key);
    }

    [Fact]
    public void Triple_glazed_offers_a_middle_source()
    {
        var triple = new SpecGroupInput(
            2, "DGU",
            "6mm A FT Glass + 12mm ASP + 6mm B FT Glass + 12mm ASP + 6mm C FT Glass",
            "", "", new List<GridRow> { new(1000, 2000, 900, 900, 2, 2) });

        var sources = ChargeMetrics.SourcesForGroup(triple);

        Assert.Equal(3, sources.Count);
        Assert.Equal("2-Middle (W2/H2)", sources[1].Key);
        Assert.Equal("B", sources[1].ColorName);
        Assert.Equal("2-Inner (W2/H2)", sources[2].Key);
    }

    [Fact]
    public void Dgu_lam_splits_three_sources()
    {
        var dguLam = new SpecGroupInput(
            3, "DGU+LAM",
            "6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass + 1.52 PVB + 6mm Clear FT Glass",
            "", "", new List<GridRow> { new(1000, 2000, 900, 900, 2, 2) });

        var sources = ChargeMetrics.SourcesForGroup(dguLam);

        Assert.Equal(3, sources.Count);
        Assert.Equal("3-Outer (W1/H1)", sources[0].Key);
        Assert.Equal("3-Lam Outer (W2/H2)", sources[1].Key);
        Assert.Equal("3-Lam Inner (W2/H2)", sources[2].Key);
    }

    [Fact]
    public void Wastage_sqm_sums_only_the_checked_sources()
    {
        var groups = new List<SpecGroupInput> { Group with { GlassType = "DGU" } };

        var outer = ChargeMetrics.WastageSqm(groups, new[] { "1-Outer (W1/H1)" });
        var both = ChargeMetrics.WastageSqm(groups,
            new[] { "1-Outer (W1/H1)", "1-Inner (W2/H2)" });
        var none = ChargeMetrics.WastageSqm(groups, Array.Empty<string>());

        Assert.Equal(6.00m, outer);
        Assert.Equal(7.62m, both);
        Assert.Equal(0m, none);
    }

    // ── amounts ─────────────────────────────────────────────────────

    [Fact]
    public void Charge_amount_is_money_rounded_when_active()
    {
        // 3.335 × 30 = 100.05 — half away from zero (MoneyRounding), NOT
        // banker's (100.04): the WPF OtherChargeRow uses MoneyRounding.
        Assert.Equal(100.05m, ChargeMetrics.ChargeAmount(3.335m, 30m, true));
        Assert.Equal(0m, ChargeMetrics.ChargeAmount(3.335m, 30m, false));
    }

    [Fact]
    public void Subtotal_uses_the_wpfs_plain_rounding()
    {
        // RecalculateTotal uses Math.Round(x, 2) — banker's on the exact
        // half, preserved verbatim (documented difference vs the rows).
        Assert.Equal(100.00m, ChargeMetrics.OtherChargesSubtotal(100.005m, 0m, 0m));
    }
}
