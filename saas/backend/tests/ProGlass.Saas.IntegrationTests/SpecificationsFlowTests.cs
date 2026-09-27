using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Application.Specifications;
using ProGlass.Saas.Domain.Proforma;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// The specifications ledger against live PostgreSQL: Step 4 APPLY
/// upserts (natural key, preserve-on-blank), the set-based reconcile
/// that derives verified usage from SAVED Step 5 groups (counts, history,
/// provenance, purge), and cascade behaviour.
/// </summary>
[Collection("database")]
public sealed class SpecificationsFlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly SpecificationsService _specs;
    private readonly ProformaService _proforma;

    public SpecificationsFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var factory = new SaasDbContextFactory(fixture.AppDb);
        var audit = new AuditWriter(factory, NullLogger<AuditWriter>.Instance);
        _specs = new SpecificationsService(new SpecificationsStore(factory), audit);
        _proforma = new ProformaService(new ProformaStore(factory), audit, _specs);
    }

    private Task<ProformaResult<ProformaDto>> CreateAsync(Guid tenant) =>
        _proforma.CreateDraftAsync(
            tenant, Guid.NewGuid(), null,
            new CreateProformaRequest(null, null, null, null, null, null, null, null),
            CancellationToken.None);

    private static ApplySpecificationRequest Apply(
        string name, decimal rate, string glassType = "SGU") =>
        new(glassType, name, rate, null, null, null, null, null, null, null, null, null);

    [DatabaseFact]
    public async Task Apply_upserts_by_natural_key_and_preserves_catalogue()
    {
        var tenant = _fixture.CreateTenant("Specs");

        var first = await _specs.ApplyAsync(
            tenant, Guid.NewGuid(), null,
            Apply("6mm Clear FT Glass", 46m) with
            {
                Category = "Clear",
                ThicknessMm = 6,
                SheetWidthMm = 3210,
                SheetHeightMm = 2250,
            },
            CancellationToken.None);
        Assert.True(first.Success, first.Message);
        Assert.Equal(0, first.Value!.UsageCount); // APPLY never counts

        var blank = await _specs.ApplyAsync(
            tenant, Guid.NewGuid(), null,
            Apply("6mm Clear FT Glass", 50m), CancellationToken.None);
        Assert.True(blank.Success);
        Assert.Equal(50m, blank.Value!.RatePerSqm);       // rate always moves
        Assert.Equal("Clear", blank.Value!.Category);    // catalogue kept
        Assert.Equal(6, blank.Value.ThicknessMm);
        Assert.Equal(3210, blank.Value.SheetWidthMm);
        Assert.Equal(first.Value.Id, blank.Value.Id);    // same row (dedup key)

        var list = await _specs.ListAsync(tenant, null, CancellationToken.None);
        Assert.Single(list); // no duplicate row
    }

    [DatabaseFact]
    public async Task Ledger_derives_usage_from_saved_groups_only()
    {
        var tenant = _fixture.CreateTenant("SpecsLedger");

        // APPLY alone counts for nothing…
        var spec = await _specs.ApplyAsync(
            tenant, Guid.NewGuid(), null,
            Apply("6mm Clear FT Glass", 46m), CancellationToken.None);
        Assert.True(spec.Success, spec.Message);

        await _specs.ReconcileAsync(tenant, null, null, CancellationToken.None);
        var afterApply = await _specs.GetAsync(tenant, spec.Value!.Id, CancellationToken.None);
        Assert.Equal(0, afterApply.Value!.UsageCount);

        // …a saved invoice's Step 5 group quoting the spec is a verified use.
        var invoice = (await CreateAsync(tenant)).Value!;
        var grid = new SaveSpecGroupsRequest(new List<SpecGroupRequest>
        {
            new(1, "SGU", "6mm Clear FT Glass", 46m, "formula",
                new List<SpecGroupItemRequest>
                {
                    new(1, "Front", 1000, 1000, 0, 0, 3, null, ""),
                },
                SpecId: spec.Value.Id),
        });

        var saved = await _proforma.SaveSpecGroupsAsync(
            tenant, invoice.Id, Guid.NewGuid(), null, grid, CancellationToken.None);
        Assert.True(saved.Success, saved.Message);

        // SaveSpecGroupsAsync already reconciled (WPF: after every write).
        var healed = await _specs.GetAsync(tenant, spec.Value.Id, CancellationToken.None);
        Assert.Equal(1, healed.Value!.UsageCount);
        Assert.Equal(invoice.ProformaNo, healed.Value.LastProformaNo);
        Assert.NotNull(healed.Value.LastUsedAt);

        // A second invoice quoting the same spec → 2 verified uses.
        var invoice2 = (await CreateAsync(tenant)).Value!;
        var grid2 = new SaveSpecGroupsRequest(new List<SpecGroupRequest>
        {
            new(1, "SGU", "6mm Clear FT Glass", 46m, "formula",
                new List<SpecGroupItemRequest>
                {
                    new(1, "Side", 900, 900, 0, 0, 2, null, ""),
                },
                SpecId: spec.Value.Id),
        });
        var saved2 = await _proforma.SaveSpecGroupsAsync(
            tenant, invoice2.Id, Guid.NewGuid(), null, grid2, CancellationToken.None);
        Assert.True(saved2.Success, saved2.Message);

        healed = await _specs.GetAsync(tenant, spec.Value.Id, CancellationToken.None);
        Assert.Equal(2, healed.Value!.UsageCount); // "2 PIs × 1 spec = 2 uses"
        Assert.Equal(invoice2.ProformaNo, healed.Value.LastProformaNo);

        // The history carries both, newest first, with the invoice numbers.
        var history = await _specs.GetUsageHistoryAsync(
            tenant, spec.Value.Id, CancellationToken.None);
        Assert.Equal(2, history.Value!.Count);
        Assert.Equal(invoice2.ProformaNo, history.Value[0].ProformaNo);
        Assert.Equal(46m, history.Value[0].PricePerSqm);

        // Editing invoice 2 to drop the spec PURGES its derived use —
        // fiction removed, counts fall back.
        var cleared = await _proforma.SaveSpecGroupsAsync(
            tenant, invoice2.Id, Guid.NewGuid(), null,
            new SaveSpecGroupsRequest(new List<SpecGroupRequest>()),
            CancellationToken.None);
        Assert.True(cleared.Success, cleared.Message);

        var purged = await _specs.GetAsync(tenant, spec.Value.Id, CancellationToken.None);
        Assert.Equal(1, purged.Value!.UsageCount);
        Assert.Equal(invoice.ProformaNo, purged.Value.LastProformaNo);
    }

    [DatabaseFact]
    public async Task Ledger_rows_are_one_per_spec_invoice_pair()
    {
        var tenant = _fixture.CreateTenant("SpecsPair");

        var spec = await _specs.ApplyAsync(
            tenant, Guid.NewGuid(), null,
            Apply("8mm Low-E DGU", 150m, "DGU"), CancellationToken.None);
        Assert.True(spec.Success, spec.Message);

        var invoice = (await CreateAsync(tenant)).Value!;
        var grid = new SaveSpecGroupsRequest(new List<SpecGroupRequest>
        {
            new(1, "DGU", "8mm Low-E DGU", 150m, "formula",
                new List<SpecGroupItemRequest>
                {
                    new(1, "A", 1000, 1000, 0, 0, 1, null, ""),
                    new(2, "B", 1200, 800, 0, 0, 1, null, ""),
                },
                SpecId: spec.Value.Id),
        });

        // Re-saving the same grid must not double the derived uses.
        await _proforma.SaveSpecGroupsAsync(
            tenant, invoice.Id, Guid.NewGuid(), null, grid, CancellationToken.None);
        await _proforma.SaveSpecGroupsAsync(
            tenant, invoice.Id, Guid.NewGuid(), null, grid, CancellationToken.None);

        var healed = await _specs.GetAsync(tenant, spec.Value.Id, CancellationToken.None);
        Assert.Equal(1, healed.Value!.UsageCount); // one verified use per pair

        var history = await _specs.GetUsageHistoryAsync(
            tenant, spec.Value.Id, CancellationToken.None);
        Assert.Single(history.Value!);
    }

    [DatabaseFact]
    public async Task Delete_cascades_usage_rows()
    {
        var tenant = _fixture.CreateTenant("SpecsDelete");

        var spec = await _specs.ApplyAsync(
            tenant, Guid.NewGuid(), null,
            Apply("12mm Toughened", 95m), CancellationToken.None);
        Assert.True(spec.Success, spec.Message);

        var invoice = (await CreateAsync(tenant)).Value!;
        await _proforma.SaveSpecGroupsAsync(
            tenant, invoice.Id, Guid.NewGuid(), null,
            new SaveSpecGroupsRequest(new List<SpecGroupRequest>
            {
                new(1, "SGU", "12mm Toughened", 95m, "formula",
                    new List<SpecGroupItemRequest>
                    {
                        new(1, "A", 1000, 1000, 0, 0, 1, null, ""),
                    },
                    SpecId: spec.Value.Id),
            }),
            CancellationToken.None);

        var deleted = await _specs.DeleteAsync(
            tenant, spec.Value.Id, Guid.NewGuid(), null, CancellationToken.None);
        Assert.True(deleted.Success);

        var gone = await _specs.GetAsync(tenant, spec.Value.Id, CancellationToken.None);
        Assert.Equal(SpecificationErrorCode.NotFound, gone.ErrorCode);
    }

    [DatabaseFact]
    public async Task Specs_and_usage_are_tenant_isolated()
    {
        var tenantA = _fixture.CreateTenant("SpecsIsoA");
        var tenantB = _fixture.CreateTenant("SpecsIsoB");

        var specA = await _specs.ApplyAsync(
            tenantA, Guid.NewGuid(), null, Apply("10mm Grey", 60m), CancellationToken.None);
        Assert.True(specA.Success, specA.Message);

        // The same (glassType, specName) is legal in another tenant.
        var specB = await _specs.ApplyAsync(
            tenantB, Guid.NewGuid(), null, Apply("10mm Grey", 70m), CancellationToken.None);
        Assert.True(specB.Success);
        Assert.NotEqual(specA.Value!.Id, specB.Value!.Id);

        var listB = await _specs.ListAsync(tenantB, null, CancellationToken.None);
        Assert.Single(listB);
        Assert.Equal(70m, listB[0].RatePerSqm);

        // Tenant B cannot read tenant A's spec or its history.
        var read = await _specs.GetAsync(tenantB, specA.Value.Id, CancellationToken.None);
        Assert.Equal(SpecificationErrorCode.NotFound, read.ErrorCode);

        var history = await _specs.GetUsageHistoryAsync(
            tenantB, specA.Value.Id, CancellationToken.None);
        Assert.Equal(SpecificationErrorCode.NotFound, history.ErrorCode);
    }
}
