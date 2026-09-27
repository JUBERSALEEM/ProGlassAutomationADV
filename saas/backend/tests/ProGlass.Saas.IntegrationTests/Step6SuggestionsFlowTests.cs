using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Domain.Proforma;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;
using static ProGlass.Saas.Domain.Proforma.MetricKeys;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// GET step6/suggestions (the Step 6 Advanced automation read model,
/// WPF OtherChargeAutomationService) against live PostgreSQL: the
/// decisions must read the SAVED grid, not the request.
/// </summary>
[Collection("database")]
public sealed class Step6SuggestionsFlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly ProformaService _sut;

    public Step6SuggestionsFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var factory = new SaasDbContextFactory(fixture.AppDb);
        var audit = new AuditWriter(factory, NullLogger<AuditWriter>.Instance);
        var specs = new SpecificationsService(
            new SpecificationsStore(factory), audit);
        _sut = new ProformaService(new ProformaStore(factory), audit, specs);
    }

    [Fact]
    public async Task Suggestions_read_the_saved_grid()
    {
        var tenant = _fixture.CreateTenant("Step6Sug");
        var invoice = (await _sut.CreateDraftAsync(
            tenant, Guid.NewGuid(), null,
            new CreateProformaRequest(null, null, null, null, null, null, null, null),
            CancellationToken.None)).Value!;

        // Group 1: matched-colour LAM with polish — the 2× polish row.
        // Group 2: single glass with overlap — perimeter charge only.
        var grid = await _sut.SaveSpecGroupsAsync(
            tenant, invoice.Id, Guid.NewGuid(), null, new SaveSpecGroupsRequest(
                new List<SpecGroupRequest>
                {
                    new(1, "LAM", "6mm Grey FT Glass + 1.52mm Clear PVB + 6mm Grey FT Glass, All Side Polish",
                        120m, "formula", new List<SpecGroupItemRequest>
                    {
                        new(1, "Front", 1000, 2000, 0, 0, 2, null, ""),
                    },
                    OuterGlassColor: "Grey", InnerGlassColor: "Grey"),
                    new(2, "SGU", "6mm Clear FT Glass with Overlap",
                        90m, "formula", new List<SpecGroupItemRequest>
                    {
                        new(1, "Front", 800, 1200, 0, 0, 1, null, ""),
                    },
                    OuterGlassColor: "Clear"),
                }), CancellationToken.None);
        Assert.True(grid.Success, grid.Message);

        var result = await _sut.GetStep6SuggestionsAsync(tenant, invoice.Id, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal(2, result.Value!.Groups.Count);
        var groups = result.Value.Groups;

        // Group 1 — matched same glass (equal pane colours on a laminate)
        // + double glass ⇒ the POLISH_MATCHED_LAM row on the combined
        // perimeter, plus outer and inner wastage sides.
        var lam = groups[0];
        Assert.True(lam.MatchedSameGlass);
        var polish = Assert.Single(lam.Charges);
        Assert.Equal(ChargeTemplates.MatchedLamPolishKey, polish.TemplateKey);
        Assert.Equal(LmCombined, polish.MetricKey);
        Assert.Equal(PolishSubTypes.AllSidePolish, polish.PolishSubType);
        Assert.Equal(2, lam.Wastages.Count);
        Assert.Equal("Grey", lam.Wastages[0].ColorName);
        Assert.Equal("Inner (W2/H2)", lam.Wastages[1].SourceSideLabel);

        // Group 2 — single glass overlap: perimeter only, one wastage
        // side, and CLEAR glass is flagged optional.
        var sgu = groups[1];
        Assert.False(sgu.MatchedSameGlass);
        var overlap = Assert.Single(sgu.Charges);
        Assert.Equal("OVERLAP", overlap.TemplateKey);
        Assert.Equal(Lm1, overlap.MetricKey);
        var wastage = Assert.Single(sgu.Wastages);
        Assert.Equal("Outer (W1/H1)", wastage.SourceSideLabel);
        Assert.Equal("Clear", wastage.ColorName);
        Assert.Contains("optional", wastage.Reason);
    }

    [Fact]
    public async Task Unknown_invoice_is_404()
    {
        var tenant = _fixture.CreateTenant("Step6Sug404");

        var result = await _sut.GetStep6SuggestionsAsync(tenant, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ProformaErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Tenants_cannot_read_each_others_suggestions()
    {
        var owner = _fixture.CreateTenant("Step6SugA");
        var other = _fixture.CreateTenant("Step6SugB");
        var invoice = (await _sut.CreateDraftAsync(
            owner, Guid.NewGuid(), null,
            new CreateProformaRequest(null, null, null, null, null, null, null, null),
            CancellationToken.None)).Value!;

        var mine = await _sut.GetStep6SuggestionsAsync(owner, invoice.Id, CancellationToken.None);
        var theirs = await _sut.GetStep6SuggestionsAsync(other, invoice.Id, CancellationToken.None);

        Assert.True(mine.Success);
        Assert.False(theirs.Success);
        Assert.Equal(ProformaErrorCode.NotFound, theirs.ErrorCode);
    }
}
