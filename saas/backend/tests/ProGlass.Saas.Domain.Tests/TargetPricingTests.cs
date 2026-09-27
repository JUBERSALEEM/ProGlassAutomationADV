using ProGlass.Saas.Domain.Proforma;
using Xunit;
using static ProGlass.Saas.Domain.Proforma.SpecificationTargetCalculator;

namespace ProGlass.Saas.Domain.Tests;

/// <summary>
/// WPF TargetPriceModelTests goldens (read 2026-09-27): the approved
/// target-price model — base price as the KPI trigger, the revised
/// U-insert rule (the required target never moves), never a fake 0%.
/// </summary>
public sealed class TargetPricingTests
{
    private const string SpecName =
        "6mm HD Grey FT Glass (47) + 12mm ASP (45) + 6mm Clear Low E FT Glass (47)";
    private const decimal GivenPrice = 160m;
    private const decimal Area = 36.6891m;

    private static SpecificationRateCard DguCard(decimal outer = 47m,
        decimal inner = 47m, decimal asp = 45m, decimal yield = 0.85m) =>
        new()
        {
            BuildType = GlassBuildType.Dgu,
            OuterGlassRatePerSqm = outer,
            InnerGlassRatePerSqm = inner,
            AspRatePerSqm = asp,
            YieldFactor = yield,
            TargetUpliftPercent = 15m
        };

    private static SpecTargetPriceResult CalculateDgu(
        SpecificationRateCard? card = null,
        decimal given = GivenPrice,
        decimal area = Area) =>
        Calculate(47, SpecName, "DGU", GlassBuildType.Dgu, card ?? DguCard(), given, area);

    private static UInsertResolution Included(decimal rate = 0m) => new()
    {
        Treatment = UInsertTreatment.IncludedInStep5Price,
        RatePerSqm = rate
    };

    private static UInsertResolution Additional(decimal rate) => new()
    {
        Treatment = UInsertTreatment.Additional,
        RatePerSqm = rate
    };

    private static SpecTargetPriceResult CalculateUserExample(
        UInsertResolution uInsert)
    {
        var card = DguCard(outer: 25m, inner: 25m, asp: 45m, yield: 0.85m);
        var spec = Calculate(
            9, "6mm Clear FT + 12mm ASP (45) + 6mm Clear FT",
            "DGU", GlassBuildType.Dgu, card, givenPricePerSqm: 125m, areaSqm: 10m);
        ApplyUInsert(spec, uInsert);
        return spec;
    }

    // ── Approved formula vector ──────────────────────────────────────

    [Fact]
    public void Dgu_specification_target_matches_approved_vector_178_93()
    {
        var spec = CalculateDgu();

        Assert.Equal(178.93m, Math.Round(spec.SpecificationTargetPerSqm, 2));
        Assert.Equal(178.93m, Math.Round(spec.FinalRequiredTargetPerSqm, 2));
        Assert.Contains("0.85", spec.FormulaText);
        Assert.Contains("1.15", spec.FormulaText);
        // Given 160 vs target 178.93 → −10.58% is a genuine BELOW TARGET.
        Assert.Equal(TargetStatus.BelowTarget, spec.Status);
    }

    [Fact]
    public void User_example_base_trigger_and_target_103_82_and_119_39()
    {
        var spec = CalculateUserExample(Additional(10m));

        Assert.Equal(103.82m, spec.BasePricePerSqm);
        Assert.Equal(119.39m, Math.Round(spec.SpecificationTargetPerSqm, 2));
        Assert.Equal(119.39m, Math.Round(spec.FinalRequiredTargetPerSqm, 2));
    }

    [Fact]
    public void User_example_toggle_on_u_insert_separate_extra_21_18_and_20_40_pct()
    {
        var spec = CalculateUserExample(Additional(10m));

        // Step 6 bills the U-insert on top: the whole 125 is the glass
        // price, judged against the 103.82 base.
        Assert.Equal(125m, spec.GivenPriceJudgedPerSqm);
        Assert.Equal(21.18m, Math.Round(spec.ExtraProfitPerSqm!.Value, 2));
        Assert.Equal(20.40m, Math.Round(spec.ExtraProfitMarginPercent!.Value, 2));
        Assert.Equal(4.70m, Math.Round(spec.MarginAgainstTargetPercent!.Value, 2));
        Assert.Equal(TargetStatus.AboveTarget, spec.Status);
    }

    [Fact]
    public void User_example_toggle_off_u_insert_inside_given_extra_11_18_and_10_77_pct()
    {
        var spec = CalculateUserExample(Included(10m));

        // The U-insert is paid out of the same 125, so only 115 is the
        // glass price — judged against the 103.82 base.
        Assert.Equal(115m, spec.GivenPriceJudgedPerSqm);
        Assert.Equal(11.18m, Math.Round(spec.ExtraProfitPerSqm!.Value, 2));
        Assert.Equal(10.77m, Math.Round(spec.ExtraProfitMarginPercent!.Value, 2));
        Assert.Equal(-3.68m, Math.Round(spec.MarginAgainstTargetPercent!.Value, 2));
        Assert.Equal(TargetStatus.BelowTarget, spec.Status);
    }

    [Fact]
    public void U_insert_rate_never_moves_the_target_for_either_treatment()
    {
        var included = CalculateUserExample(Included(10m));
        var additional = CalculateUserExample(Additional(10m));

        Assert.Equal(0m, included.AppliedUInsertPerSqm);
        Assert.Equal(0m, additional.AppliedUInsertPerSqm);
        Assert.Equal(included.SpecificationTargetPerSqm, included.FinalRequiredTargetPerSqm);
        Assert.Equal(additional.SpecificationTargetPerSqm, additional.FinalRequiredTargetPerSqm);
    }

    [Fact]
    public void User_example_job_aggregate_matches_the_single_spec_figures()
    {
        var job = TargetProfitabilityAggregator.AggregateJob(
            new[] { CalculateUserExample(Included(10m)) });

        Assert.Equal(103.82m, job.AverageBasePricePerSqm);
        Assert.Equal(115m, job.AverageGivenJudgedPerSqm);
        Assert.Equal(11.18m, Math.Round(job.AverageExtraProfitPerSqm!.Value, 2));
        Assert.Equal(10.77m, Math.Round(job.ExtraProfitMarginPercent!.Value, 2));
    }

    [Fact]
    public void Cost_based_profit_margin_excludes_off_u_insert_and_includes_active_dynamic_rate()
    {
        var off = TargetProfitabilityAggregator.AggregateJob(
            new[] { CalculateUserExample(Included(10m)) });
        var on = TargetProfitabilityAggregator.AggregateJob(
            new[] { CalculateUserExample(Additional(10m)) });

        Assert.Equal(16.94m, off.CostBasedProfitMarginPercent!.Value);
        Assert.Equal(8.94m, on.CostBasedProfitMarginPercent!.Value);
    }

    [Fact]
    public void Cost_based_profit_margin_is_unavailable_for_unresolved_u_insert()
    {
        var unresolved = new UInsertResolution
        {
            Treatment = UInsertTreatment.Unresolved,
            Warning = "U-insert cost is unresolved."
        };
        var job = TargetProfitabilityAggregator.AggregateJob(
            new[] { CalculateUserExample(unresolved) });

        Assert.Null(job.CostBasedProfitMarginPercent);
        Assert.Equal(TargetStatus.MissingUInsertTreatment, job.Status);
    }

    // ── Resolver seed order + treatments ─────────────────────────────

    [Fact]
    public void Resolver_prefers_step4_seed_then_row_then_template_default()
    {
        decimal Resolve(decimal seed, decimal rowRate, decimal templateDefault,
            bool rowExists = true, bool rowActive = true, bool requires = true) =>
            Step6ChargeResolver.ResolveUInsert(new Step6UInsertQuery
            {
                SpecRequiresUInsert = requires,
                RowExists = rowExists,
                RowActiveForSpec = rowActive,
                Step4SeedRate = seed,
                RowRate = rowRate,
                TemplateDefaultRate = templateDefault
            }).RatePerSqm;

        Assert.Equal(8m, Resolve(8m, 7m, 10m));
        Assert.Equal(7m, Resolve(0m, 7m, 10m));
        Assert.Equal(10m, Resolve(0m, 0m, 10m));
    }

    [Fact]
    public void Resolver_treats_inactive_u_insert_row_as_included_in_step5_price()
    {
        var r = Step6ChargeResolver.ResolveUInsert(new Step6UInsertQuery
        {
            SpecRequiresUInsert = true,
            RowExists = true,
            RowActiveForSpec = false,
            RowRate = 7m
        });

        Assert.Equal(UInsertTreatment.IncludedInStep5Price, r.Treatment);
        Assert.Equal(7m, r.RatePerSqm);
    }

    [Fact]
    public void Resolver_treats_active_u_insert_row_as_separately_billed_additional()
    {
        var r = Step6ChargeResolver.ResolveUInsert(new Step6UInsertQuery
        {
            SpecRequiresUInsert = true,
            RowExists = true,
            RowActiveForSpec = true,
            RowRate = 7m
        });

        Assert.Equal(UInsertTreatment.Additional, r.Treatment);
        Assert.Equal(7m, r.RatePerSqm);
    }

    [Fact]
    public void Resolver_flags_unresolved_when_u_insert_required_but_no_rate()
    {
        var r = Step6ChargeResolver.ResolveUInsert(new Step6UInsertQuery
        {
            SpecRequiresUInsert = true
        });

        Assert.Equal(UInsertTreatment.Unresolved, r.Treatment);
        Assert.True(r.HasWarning);
    }

    [Fact]
    public void Resolver_returns_not_applicable_when_spec_has_no_u_insert()
    {
        var r = Step6ChargeResolver.ResolveUInsert(new Step6UInsertQuery
        {
            SpecRequiresUInsert = false,
            RowExists = true,
            RowActiveForSpec = true,
            RowRate = 7m
        });

        Assert.Equal(UInsertTreatment.NotApplicable, r.Treatment);
    }

    // ── Never a fake 0% margin when inputs are missing ───────────────

    [Fact]
    public void Missing_base_price_reports_missing_and_null_margin()
    {
        var spec = CalculateDgu(given: 0m);
        ApplyUInsert(spec, Included());

        Assert.Equal(TargetStatus.MissingBasePrice, spec.Status);
        Assert.Null(spec.MarginAgainstTargetPercent);
    }

    [Fact]
    public void Missing_glass_rate_reports_missing_and_null_margin()
    {
        var spec = CalculateDgu(DguCard(outer: 0m, inner: 0m));

        Assert.Equal(TargetStatus.MissingGlassRate, spec.Status);
        Assert.Null(spec.MarginAgainstTargetPercent);
        Assert.Equal(0m, spec.SpecificationTargetPerSqm);
    }

    [Fact]
    public void Missing_asp_rate_reports_missing_and_null_margin()
    {
        var spec = CalculateDgu(DguCard(asp: 0m));

        Assert.Equal(TargetStatus.MissingAspRate, spec.Status);
        Assert.Null(spec.MarginAgainstTargetPercent);
    }

    [Fact]
    public void Missing_yield_reports_missing_and_null_margin()
    {
        var spec = CalculateDgu(DguCard(yield: 0m));

        Assert.Equal(TargetStatus.MissingYield, spec.Status);
        Assert.Null(spec.MarginAgainstTargetPercent);
    }

    // ── SaaS adapter (SpecificationTargetBuilder) ────────────────────

    private static SpecGroupInput Group(
        string glassType = "DGU",
        decimal rate = 125m,
        decimal? outerPrice = 25m,
        decimal? innerPrice = 25m,
        decimal? spacerPrice = 45m,
        decimal? uInsertPrice = null,
        decimal? cutting = null,
        decimal? tempering = null,
        string specName = "6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass",
        IReadOnlyList<Domain.Proforma.GridRow>? rows = null) =>
        new(1, glassType, specName, "Clear", "Clear",
            rows ?? new List<Domain.Proforma.GridRow>
            {
                new(2000, 2500, 0, 0, 2, null)
            },
            OuterSheetPricePerSqm: outerPrice,
            InnerSheetPricePerSqm: innerPrice,
            Step4SpacerPricePerSqm: spacerPrice,
            UInsertPricePerSqm: uInsertPrice,
            RatePerSqm: rate,
            Step4CuttingCostPerSqm: cutting,
            Step4TemperingCostPerSqm: tempering);

    [Fact]
    public void Builder_reproduces_the_user_example_from_grid_inputs()
    {
        // Given 125, U-insert seed 10 with NO Step 6 row (toggle OFF →
        // covered inside the given price): base 103.82, target 119.39,
        // judged 115, extra +11.18 (+10.77%), status −3.68% BELOW.
        var spec = SpecificationTargetBuilder.CalculateGroup(
            Group(uInsertPrice: 10m),
            new Step6UInsertQuery { SpecRequiresUInsert = true, Step4SeedRate = 10m },
            wastagePercent: 15m);

        Assert.Equal(GlassBuildType.Dgu, spec.BuildType);
        Assert.Equal(10m, spec.AreaSqm);
        Assert.Equal(103.82m, spec.BasePricePerSqm);
        Assert.Equal(119.39m, Math.Round(spec.SpecificationTargetPerSqm, 2));
        Assert.Equal(UInsertTreatment.IncludedInStep5Price, spec.UInsertTreatment);
        Assert.Equal(115m, spec.GivenPriceJudgedPerSqm);
        Assert.Equal(11.18m, Math.Round(spec.ExtraProfitPerSqm!.Value, 2));
        Assert.Equal(10.77m, Math.Round(spec.ExtraProfitMarginPercent!.Value, 2));
        Assert.Equal(TargetStatus.BelowTarget, spec.Status);
    }

    [Fact]
    public void Builder_uses_the_step6_row_when_active_for_the_spec()
    {
        var rows = new List<(bool, IReadOnlyList<int>?, decimal)>
        {
            (true, new List<int> { 1 }, 12m)
        };

        var query = SpecificationTargetBuilder.BuildUInsertQuery(
            Group(uInsertPrice: 0m, specName: "6mm Clear FT Glass + 12mm ASP with U-Insert"),
            rows, templateDefault: 10m);

        Assert.True(query.SpecRequiresUInsert);       // spec-name evidence
        Assert.True(query.RowExists);
        Assert.True(query.RowActiveForSpec);          // group 1 is checked
        Assert.Equal(12m, query.RowRate);
    }

    [Fact]
    public void Untargeted_u_insert_row_is_not_active_for_any_spec()
    {
        // Same semantics as the WPF TargetSelection with nothing checked
        // (and the SaaS Step 6 TargetedMetric): empty targeting = no spec.
        var rows = new List<(bool, IReadOnlyList<int>?, decimal)>
        {
            (true, new List<int>(), 12m)
        };

        var query = SpecificationTargetBuilder.BuildUInsertQuery(
            Group(uInsertPrice: 10m), rows, templateDefault: 10m);

        Assert.True(query.RowExists);
        Assert.False(query.RowActiveForSpec);
        // The seed (10) wins over the row rate for the DISPLAY rate.
        Assert.Equal(10m, Step6ChargeResolver.ResolveUInsert(query).RatePerSqm);
    }

    [Fact]
    public void Yield_factor_derives_from_the_wastage_box_with_a_floor()
    {
        Assert.Equal(0.85m, SpecificationTargetBuilder.EffectiveYieldFactor(15m));
        Assert.Equal(0.05m, SpecificationTargetBuilder.EffectiveYieldFactor(100m));
        Assert.Equal(1m, SpecificationTargetBuilder.EffectiveYieldFactor(0m));
    }

    [Fact]
    public void Sgu_target_uses_cutting_and_tempering_as_processing()
    {
        var spec = SpecificationTargetBuilder.CalculateGroup(
            Group(glassType: "SGU",
                specName: "6mm Clear FT Glass",
                outerPrice: 20m, innerPrice: null, spacerPrice: null,
                uInsertPrice: null, cutting: 5m, tempering: 8m),
            new Step6UInsertQuery(),
            wastagePercent: 20m);   // yield 0.8

        // (20 ÷ 0.8 + 5 + 8) = 38 → × 1.15 = 43.70.
        Assert.Equal(38m, spec.BasePricePerSqm);
        Assert.Equal(43.70m, Math.Round(spec.SpecificationTargetPerSqm, 2));
    }

    [Fact]
    public void BuildJob_skips_groups_without_cut_area()
    {
        var empty = new SpecGroupInput(1, "SGU", "6mm Clear FT Glass", "Clear", "",
            new List<Domain.Proforma.GridRow>(), RatePerSqm: 100m);

        var job = SpecificationTargetBuilder.BuildJob(
            new[] { empty }, _ => new Step6UInsertQuery(), 15m);

        Assert.Empty(job.Specifications);
        Assert.Equal(TargetStatus.CalculationIncomplete, job.Status);
    }
}
