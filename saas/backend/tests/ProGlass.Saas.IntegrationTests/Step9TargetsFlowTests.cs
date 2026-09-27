using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Domain.Proforma;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// The Step 9 target-price panel (WPF
/// ProfitLossStepViewModel.RebuildTargetResults) computed from the SAVED
/// document against live PostgreSQL — the customer's approved example:
/// DGU (25 + 25) ÷ 0.85 + 45 = 103.82 base, × 1.15 = 119.39 target.
/// </summary>
[Collection("database")]
public sealed class Step9TargetsFlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly ProformaService _sut;

    public Step9TargetsFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var factory = new SaasDbContextFactory(fixture.AppDb);
        var audit = new AuditWriter(factory, NullLogger<AuditWriter>.Instance);
        var specs = new SpecificationsService(
            new SpecificationsStore(factory), audit);
        _sut = new ProformaService(new ProformaStore(factory), audit, specs);
    }

    private async Task<ProformaResult<Step9Dto>> SaveDocumentAsync(
        Guid tenant, decimal? uInsertPrice = 10m, bool legacy = false)
    {
        var invoice = (await _sut.CreateDraftAsync(
            tenant, Guid.NewGuid(), null,
            new CreateProformaRequest(null, null, null, null, null, null, null, null),
            CancellationToken.None)).Value!;

        var grid = await _sut.SaveSpecGroupsAsync(
            tenant, invoice.Id, Guid.NewGuid(), null, new SaveSpecGroupsRequest(
                new List<SpecGroupRequest>
                {
                    new(1, "DGU", "6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass",
                        125m, "manual", new List<SpecGroupItemRequest>
                    {
                        new(1, "Front", 2000, 2500, 0, 0, 2, null, ""),
                    },
                    OuterGlassColor: legacy ? null : "Clear",
                    InnerGlassColor: legacy ? null : "Clear",
                    OuterSheetPricePerSqm: legacy ? null : 25m,
                    InnerSheetPricePerSqm: legacy ? null : 25m,
                    Step4SpacerPricePerSqm: legacy ? null : 45m,
                    UInsertPricePerSqm: uInsertPrice),
                }), CancellationToken.None);
        Assert.True(grid.Success, grid.Message);

        return await _sut.SaveStep9Async(
            tenant, invoice.Id, Guid.NewGuid(), null,
            new SaveStep9Request(15m, null), CancellationToken.None);
    }

    [Fact]
    public async Task Targets_read_the_saved_document()
    {
        var tenant = _fixture.CreateTenant("Tgt");
        var saved = await SaveDocumentAsync(tenant);
        Assert.True(saved.Success, saved.Message);

        var targets = saved.Value!.Targets;
        Assert.NotNull(targets);

        // Job-level: single spec ⇒ the aggregates are its own figures.
        Assert.Equal(1, targets.ValidSpecCount);
        Assert.Equal(0, targets.IncompleteSpecCount);

        var spec = Assert.Single(targets.Specs);
        Assert.Equal("Dgu", spec.BuildType);
        Assert.Equal(10m, spec.AreaSqm);
        Assert.Equal(125m, spec.GivenPricePerSqm);
        Assert.Equal(103.82m, spec.BasePricePerSqm);
        Assert.Equal(119.39m, Math.Round(spec.SpecificationTargetPerSqm, 2));
        Assert.Equal(119.39m, Math.Round(spec.FinalRequiredTargetPerSqm, 2));
        Assert.Equal("((25 + 25) ÷ 0.85 + 45) × 1.15", spec.FormulaText);

        // No Step 6 U-insert row was saved ⇒ the seed (10) is covered
        // inside the given price: judged 115.
        Assert.Equal("IncludedInStep5Price", spec.UInsertTreatment);
        Assert.Equal(10m, spec.UInsertRatePerSqm);
        Assert.Equal(115m, spec.GivenPriceJudgedPerSqm);
        Assert.Equal(11.18m, Math.Round(spec.ExtraProfitPerSqm!.Value, 2));
        Assert.Equal(10.77m, Math.Round(spec.ExtraProfitMarginPercent!.Value, 2));
        Assert.Equal(-3.68m, Math.Round(spec.MarginAgainstTargetPercent!.Value, 2));
        Assert.Equal("BelowTarget", spec.Status);

        Assert.Equal(103.82m, targets.AverageBasePricePerSqm);
        Assert.Equal(115m, targets.AverageGivenJudgedPerSqm);
        Assert.Equal(10.77m, Math.Round(targets.ExtraProfitMarginPercent!.Value, 2));
        // (1250 selling − 1038.20 base cost) ÷ 1250 = 16.94% — the OFF
        // U-insert contributes no cost.
        Assert.Equal(16.94m, targets.CostBasedProfitMarginPercent!.Value);
    }

    [Fact]
    public async Task An_active_step6_u_insert_row_bills_the_extra_separately()
    {
        var tenant = _fixture.CreateTenant("TgtOn");
        var invoice = await CreateWithGridAsync(tenant);

        // Step 6: an ACTIVE U-insert row targeting group 1 at rate 12.
        var step6 = await _sut.SaveStep6Async(
            tenant, invoice.Id, Guid.NewGuid(), null, new SaveStep6Request(
                new List<OtherChargeRowRequest>
                {
                    new("U_INSERT", "U-Insert (Sqm)", "", "Sqm", true, "SQM1",
                        new List<int> { 1 }, 12m),
                }, null, null), CancellationToken.None);
        Assert.True(step6.Success, step6.Message);

        var saved = await _sut.SaveStep9Async(
            tenant, invoice.Id, Guid.NewGuid(), null,
            new SaveStep9Request(15m, null), CancellationToken.None);
        var spec = Assert.Single(saved.Value!.Targets!.Specs);

        // Trigger ON: the whole 125 is the glass price (+20.40% over the
        // 103.82 base) and the U-insert cost counts for the cost margin.
        // The Step 4 seed (10) WINS over the typed row rate (12) — the
        // WPF dynamic rate order.
        Assert.Equal("Additional", spec.UInsertTreatment);
        Assert.Equal(10m, spec.UInsertRatePerSqm);
        Assert.Equal(125m, spec.GivenPriceJudgedPerSqm);
        Assert.Equal(21.18m, Math.Round(spec.ExtraProfitPerSqm!.Value, 2));
        Assert.Equal(20.40m, Math.Round(spec.ExtraProfitMarginPercent!.Value, 2));
        Assert.Equal("AboveTarget", spec.Status);
        // (1250 − 1038.20 − 100) ÷ 1250 = 8.94%.
        Assert.Equal(8.94m, saved.Value.Targets.CostBasedProfitMarginPercent!.Value);
    }

    [Fact]
    public async Task A_document_without_a_grid_has_no_targets()
    {
        var tenant = _fixture.CreateTenant("TgtEmpty");
        var invoice = (await _sut.CreateDraftAsync(
            tenant, Guid.NewGuid(), null,
            new CreateProformaRequest(null, null, null, null, null, null, null, null),
            CancellationToken.None)).Value!;

        var saved = await _sut.SaveStep9Async(
            tenant, invoice.Id, Guid.NewGuid(), null,
            new SaveStep9Request(15m, null), CancellationToken.None);

        Assert.True(saved.Success, saved.Message);
        Assert.Null(saved.Value!.Targets);
    }

    [Fact]
    public async Task Legacy_group_without_step4_handoff_is_honestly_missing()
    {
        var tenant = _fixture.CreateTenant("TgtLegacy");
        var saved = await SaveDocumentAsync(tenant, uInsertPrice: null, legacy: true);
        Assert.True(saved.Success, saved.Message);

        var spec = Assert.Single(saved.Value!.Targets!.Specs);

        // No Step 4 handoff columns, no live panel on a server: the
        // target model refuses to guess (never a fake 0%).
        Assert.Equal("MissingGlassRate", spec.Status);
        Assert.Null(spec.MarginAgainstTargetPercent);
        Assert.Equal("MissingGlassRate", saved.Value.Targets.Status);
        Assert.Equal(0, saved.Value.Targets.ValidSpecCount);
        Assert.Equal(1, saved.Value.Targets.IncompleteSpecCount);
    }

    private async Task<ProformaDto> CreateWithGridAsync(Guid tenant)
    {
        var invoice = (await _sut.CreateDraftAsync(
            tenant, Guid.NewGuid(), null,
            new CreateProformaRequest(null, null, null, null, null, null, null, null),
            CancellationToken.None)).Value!;

        var grid = await _sut.SaveSpecGroupsAsync(
            tenant, invoice.Id, Guid.NewGuid(), null, new SaveSpecGroupsRequest(
                new List<SpecGroupRequest>
                {
                    new(1, "DGU", "6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass",
                        125m, "manual", new List<SpecGroupItemRequest>
                    {
                        new(1, "Front", 2000, 2500, 0, 0, 2, null, ""),
                    },
                    OuterGlassColor: "Clear", InnerGlassColor: "Clear",
                    OuterSheetPricePerSqm: 25m, InnerSheetPricePerSqm: 25m,
                    Step4SpacerPricePerSqm: 45m,
                    UInsertPricePerSqm: 10m),
                }), CancellationToken.None);
        Assert.True(grid.Success, grid.Message);

        return invoice;
    }
}
