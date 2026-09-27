using ProGlass.Saas.Domain.Proforma;
using Xunit;
using static ProGlass.Saas.Domain.Proforma.ChargeAutomation;
using static ProGlass.Saas.Domain.Proforma.MetricKeys;
using static ProGlass.Saas.Domain.Proforma.PolishSubTypes;

namespace ProGlass.Saas.Domain.Tests;

/// <summary>
/// WPF OtherChargeAutomationRules goldens (read 2026-09-27): which
/// predefined Step 6 charges a spec implies, which pane sides to
/// preselect for wastage, and the polish sub-type detection order.
/// </summary>
public sealed class ChargeAutomationTests
{
    // ── Charge suggestions ─────────────────────────────────────────────

    [Fact]
    public void U_insert_is_sqm_on_single_glass_and_cavity_on_dgu()
    {
        var single = GetChargeSuggestions("SGU", "6mm Clear FT Glass with U-Insert", false);
        var dgu = GetChargeSuggestions("DGU", "6mm Clear FT Glass + 12mm ASP", false);

        Assert.Equal(("U_INSERT", Sqm1), Key(single[0]));
        Assert.Equal(("U_INSERT", DguCavity), Key(dgu[0]));
    }

    [Fact]
    public void Overlap_on_single_glass_is_perimeter_only()
    {
        var rows = GetChargeSuggestions("SGU", "6mm Clear FT Glass with Overlap", false);

        var overlap = Assert.Single(rows);
        Assert.Equal(("OVERLAP", Lm1), Key(overlap));
    }

    [Fact]
    public void DGU_overlap_also_charges_the_silicon_bed()
    {
        var rows = GetChargeSuggestions(
            "DGU", "6mm Clear FT Glass + 12mm ASP + 6mm Bronze FT Glass with Overlap", false);

        Assert.Equal(2, rows.Count);
        Assert.Equal(("OVERLAP", Lm1), Key(rows[0]));
        Assert.Equal(("SILICON_BED", Lm1), Key(rows[1]));
        Assert.Contains("silicon bed as a separate charge", rows[1].Reason);
    }

    [Fact]
    public void Overlap_sqm_mode_never_charges_the_silicon_bed()
    {
        var rows = GetChargeSuggestions(
            "DGU", "6mm Clear FT Glass + 12mm ASP with Overlap (SQM)", false);

        var overlap = Assert.Single(rows);
        Assert.Equal(("OVERLAP_SQM", Sqm1), Key(overlap));
        Assert.Contains("charged by area", overlap.Reason);
    }

    [Fact]
    public void Simple_keyword_rules()
    {
        Assert.Equal(("ARGON_GAS", DguCavity),
            Key(Assert.Single(GetChargeSuggestions("DGU", "Argon Gas filled unit", false))));
        Assert.Equal(("SHUTTER_BONDING", LmCombined),
            Key(Assert.Single(GetChargeSuggestions("SGU", "Shutter Bonding", false))));
        Assert.Equal(("FAN_HOLES", Qty),
            Key(Assert.Single(GetChargeSuggestions("SGU", "With Fanhole", false))));
        Assert.Equal(("MITRING", Lm1),
            Key(Assert.Single(GetChargeSuggestions("SGU", "Mitring required", false))));
        Assert.Equal(("SANDBLASTING", Sqm1),
            Key(Assert.Single(GetChargeSuggestions("SGU", "Sandblasted logo", false))));
    }

    [Fact]
    public void Cutout_and_holes_together_turn_on_both_rows()
    {
        var rows = GetChargeSuggestions("SGU", "Cutout And Holes", false);

        Assert.Equal(2, rows.Count);
        Assert.Equal(("CUTOUT", Qty), Key(rows[0]));
        Assert.Equal(("HOLES", Qty), Key(rows[1]));
    }

    [Theory]
    [InlineData("With Cutout")]
    [InlineData("With Cutouts")]
    public void Cutout_alone_turns_on_only_the_cutout_row(string spec)
    {
        var rows = GetChargeSuggestions("SGU", spec, false);

        Assert.Equal(("CUTOUT", Qty), Key(Assert.Single(rows)));
    }

    [Fact]
    public void Holes_alone_turns_on_only_the_holes_row()
    {
        var rows = GetChargeSuggestions("SGU", "Drilled Holes", false);

        Assert.Equal(("HOLES", Qty), Key(Assert.Single(rows)));
    }

    [Fact]
    public void Polish_on_single_glass_is_lm1()
    {
        var rows = GetChargeSuggestions("SGU", "6mm Clear FT Glass, All Side Polish", false);

        var polish = Assert.Single(rows);
        Assert.Equal(("POLISH", Lm1), Key(polish));
        Assert.Equal(AllSidePolish, polish.PolishSubType);
    }

    [Fact]
    public void Polish_on_dgu_uses_the_combined_perimeter()
    {
        var rows = GetChargeSuggestions(
            "DGU", "6mm Clear FT Glass + 12mm ASP, Both Height Side Polish", false);

        var polish = Assert.Single(rows);
        Assert.Equal(("POLISH", LmCombined), Key(polish));
        Assert.Equal(BothHeightSide, polish.PolishSubType);
    }

    [Fact]
    public void Matched_same_glass_dgu_polish_switches_to_the_2x_row()
    {
        var rows = GetChargeSuggestions(
            "DGU", "6mm Clear FT Glass + 12mm ASP, All Side Polish", matchedSameGlass: true);

        var polish = Assert.Single(rows);
        Assert.Equal((ChargeTemplates.MatchedLamPolishKey, LmCombined), Key(polish));
        Assert.Contains("2× polish row", polish.Reason);
    }

    [Fact]
    public void Beveled_implies_mitring()
    {
        var rows = GetChargeSuggestions("SGU", "Beveled edge detail", false);

        Assert.Equal(("MITRING", Lm1), Key(Assert.Single(rows)));
    }

    // ── Polish sub-type detection (longest phrase first) ───────────────

    [Theory]
    [InlineData("All Side Polish As Per DWG", AllSidePolishAsPerDwg)]
    [InlineData("All Side Polish", AllSidePolish)]
    [InlineData("Polished Edges", AllSidePolish)]
    [InlineData("Both Height Side Polish As Per DWG", BothHeightSideAsPerDwg)]
    [InlineData("Both Width Side Polish", BothWidthSide)]
    [InlineData("Both Height + One Width Side Polish As Per DWG", BothHeightOneWidthAsPerDwg)]
    [InlineData("One Height + One Width Side Polish", OneHeightOneWidth)]
    [InlineData("One Height Side Polish", OneHeightSide)]
    [InlineData("One Width Side Polish As Per DWG", OneWidthSideAsPerDwg)]
    public void DetectPolish_matches_the_declared_sub_type(string spec, string expected)
    {
        Assert.Equal(expected, DetectPolish(spec));
    }

    [Fact]
    public void DetectPolish_returns_null_without_a_polish_phrase()
    {
        Assert.Null(DetectPolish("6mm Clear FT Glass"));
        Assert.Null(DetectPolish(null));
    }

    // ── Wastage suggestions ────────────────────────────────────────────

    [Fact]
    public void Single_glass_suggests_only_the_outer_side()
    {
        var rows = GetWastageSuggestions(1, "SGU", "6mm Clear FT Glass", "Clear", "");

        var row = Assert.Single(rows);
        Assert.Equal("Outer (W1/H1)", row.SourceSideLabel);
        Assert.Equal("Clear", row.ColorName);
        Assert.Equal("Clear glass — optional (offcuts often reusable).", row.Reason);
    }

    [Fact]
    public void Colored_glass_offcuts_are_not_reusable()
    {
        var rows = GetWastageSuggestions(1, "SGU", "6mm Bronze FT Glass", "Bronze", "");

        Assert.Equal("Colored/coated glass — offcuts not reusable.", Assert.Single(rows).Reason);
    }

    [Fact]
    public void Dgu_suggests_outer_and_inner()
    {
        var rows = GetWastageSuggestions(
            2, "DGU", "6mm Grey FT Glass + 12mm ASP + 6mm Bronze FT Glass", "", "");

        Assert.Equal(2, rows.Count);
        Assert.Equal("Outer (W1/H1)", rows[0].SourceSideLabel);
        Assert.Equal("Grey", rows[0].ColorName);
        Assert.Equal("Inner (W2/H2)", rows[1].SourceSideLabel);
        Assert.Equal("Bronze", rows[1].ColorName);
        Assert.Equal(2, rows[1].SpecGroupNumber);
    }

    [Fact]
    public void Triple_glazed_suggests_every_pane_including_the_middle()
    {
        var rows = GetWastageSuggestions(
            1, "DGU",
            "6mm Grey FT Glass + 12mm ASP + 6mm Clear FT Glass + 12mm ASP + 6mm Bronze FT Glass",
            "", "");

        Assert.Equal(3, rows.Count);
        Assert.Equal("Outer (W1/H1)", rows[0].SourceSideLabel);
        // FIX-2026-09-05: the middle pane must not silently escape its
        // wastage charge.
        Assert.Equal("Middle (W2/H2)", rows[1].SourceSideLabel);
        Assert.Equal("Clear", rows[1].ColorName);
        Assert.Equal("Inner (W2/H2)", rows[2].SourceSideLabel);
        Assert.Equal("Bronze", rows[2].ColorName);
    }

    [Fact]
    public void Dgu_lam_suggests_outer_lam_outer_and_lam_inner()
    {
        // DGU+LAM = outer pane + spacer + a laminated leaf (two panes
        // around the PVB) — three glass matches.
        var rows = GetWastageSuggestions(
            3, "DGU+LAM",
            "6mm Clear FT Glass + 12mm ASP + 6mm Grey FT Glass + 1.52mm Clear PVB + 6mm Bronze FT Glass",
            "", "");

        Assert.Equal(3, rows.Count);
        Assert.Equal("Outer (W1/H1)", rows[0].SourceSideLabel);
        Assert.Equal("Clear", rows[0].ColorName);
        Assert.Equal("Lam Outer (W2/H2)", rows[1].SourceSideLabel);
        Assert.Equal("Grey", rows[1].ColorName);
        Assert.Equal("Lam Inner (W2/H2)", rows[2].SourceSideLabel);
        Assert.Equal("Bronze", rows[2].ColorName);
    }

    [Fact]
    public void Empty_spec_and_colors_yield_no_wastage_rows()
    {
        Assert.Empty(GetWastageSuggestions(1, "SGU", null, "", ""));
        Assert.Empty(GetWastageSuggestions(1, "DGU", null, "", ""));
    }

    [Fact]
    public void Fallback_colors_are_used_when_the_spec_has_no_glass_grammar()
    {
        var rows = GetWastageSuggestions(1, "SGU", "Opaque cladding", "Bronze", "Clear");

        var row = Assert.Single(rows);
        Assert.Equal("Bronze", row.ColorName);
    }

    private static (string Template, string Metric) Key(AutoChargeSuggestion s) =>
        (s.TemplateKey, s.MetricKey);
}
