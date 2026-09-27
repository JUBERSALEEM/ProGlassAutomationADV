using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Domain.Proforma;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// Proforma document core against live PostgreSQL: number allocation with
/// the real UNIQUE(tenant_id, proforma_no), lifecycle transitions under
/// the schema CHECKs, void, revision chains, the Step 5 grid replace
/// (cascade + one transaction) and tenant isolation. Uses the real EF
/// store (query filters + RLS via the tenant-bound factory).
/// </summary>
[Collection("database")]
public sealed class ProformaFlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly ProformaService _sut;

    public ProformaFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var factory = new SaasDbContextFactory(fixture.AppDb);
        _sut = new ProformaService(
            new ProformaStore(factory),
            new AuditWriter(factory, NullLogger<AuditWriter>.Instance));
    }

    private async Task<ProformaDto> CreateAsync(Guid tenant)
    {
        var result = await _sut.CreateDraftAsync(
            tenant, Guid.NewGuid(), null,
            new CreateProformaRequest(null, null, null, null, null, null, null, null),
            CancellationToken.None);
        Assert.True(result.Success, result.Message);
        return result.Value!;
    }

    [DatabaseFact]
    public async Task Create_allocates_sequential_numbers_on_live_pg()
    {
        var tenant = _fixture.CreateTenant("Proforma");

        var first = await CreateAsync(tenant);
        var second = await CreateAsync(tenant);

        Assert.Equal($"PI-{DateTime.UtcNow.Year}-001", first.ProformaNo);
        Assert.Equal($"PI-{DateTime.UtcNow.Year}-002", second.ProformaNo);

        var readBack = await _sut.GetAsync(tenant, second.Id, CancellationToken.None);
        Assert.True(readBack.Success);
        Assert.Equal(second.ProformaNo, readBack.Value!.ProformaNo);
    }

    [DatabaseFact]
    public async Task Same_number_is_allowed_across_tenants()
    {
        // UNIQUE(tenant_id, proforma_no) — the WPF's per-database
        // uniqueness becomes per-tenant in the SaaS.
        var tenantA = _fixture.CreateTenant("ProformaA");
        var tenantB = _fixture.CreateTenant("ProformaB");

        await CreateAsync(tenantA);
        var inB = await CreateAsync(tenantB);

        Assert.Equal($"PI-{DateTime.UtcNow.Year}-001", inB.ProformaNo);
    }

    [DatabaseFact]
    public async Task Lifecycle_walks_the_machine_and_stamps_timestamps()
    {
        var tenant = _fixture.CreateTenant("Proforma");
        var invoice = await CreateAsync(tenant);

        async Task<ProformaDto> Go(string to)
        {
            var r = await _sut.TransitionAsync(
                tenant, invoice.Id, Guid.NewGuid(), null,
                new TransitionRequest(to, null), CancellationToken.None);
            Assert.True(r.Success, r.Message);
            return r.Value!;
        }

        invoice = await Go("PendingApproval");
        invoice = await Go("Approved");
        Assert.NotNull(invoice.ApprovedAt);
        invoice = await Go("Sent");
        Assert.NotNull(invoice.SentAt);
        invoice = await Go("Confirmed");
        Assert.NotNull(invoice.ConfirmedAt);

        // Terminal: nothing further.
        var dead = await _sut.TransitionAsync(
            tenant, invoice.Id, Guid.NewGuid(), null,
            new TransitionRequest("Draft", null), CancellationToken.None);
        Assert.False(dead.Success);
        Assert.Equal(ProformaErrorCode.Conflict, dead.ErrorCode);

        // The schema CHECKs accepted every machine state along the way.
        var readBack = await _sut.GetAsync(tenant, invoice.Id, CancellationToken.None);
        Assert.Equal("Confirmed", readBack.Value!.LifecycleState);
    }

    [DatabaseFact]
    public async Task Void_is_idempotent_and_moves_status_to_cancelled()
    {
        var tenant = _fixture.CreateTenant("Proforma");
        var actor = Guid.NewGuid();
        var invoice = await CreateAsync(tenant);

        var voided = await _sut.VoidAsync(
            tenant, invoice.Id, actor, "127.0.0.1", null, CancellationToken.None);
        Assert.True(voided.Success, voided.Message);
        Assert.True(voided.Value!.IsVoid);
        Assert.Equal("Cancelled", voided.Value.Status);
        Assert.Equal("(no reason given)", voided.Value.VoidReason); // blank reason

        // Second void: 200, nothing written.
        var again = await _sut.VoidAsync(
            tenant, invoice.Id, actor, null, "different reason",
            CancellationToken.None);
        Assert.True(again.Success);
        Assert.Equal("(no reason given)", again.Value!.VoidReason);
    }

    [DatabaseFact]
    public async Task Revision_chain_on_live_pg()
    {
        var tenant = _fixture.CreateTenant("Proforma");
        var actor = Guid.NewGuid();

        var original = await CreateAsync(tenant);
        foreach (var to in new[] { "PendingApproval", "Approved", "Sent" })
        {
            var r = await _sut.TransitionAsync(
                tenant, original.Id, actor, null, new TransitionRequest(to, null),
                CancellationToken.None);
            Assert.True(r.Success, r.Message);
        }

        var revision = await _sut.CreateRevisionAsync(
            tenant, original.Id, actor, null, CancellationToken.None);
        Assert.True(revision.Success, revision.Message);
        Assert.Equal($"{original.ProformaNo}-R1", revision.Value!.ProformaNo);

        var chain = await _sut.GetRevisionChainAsync(
            tenant, original.Id, CancellationToken.None);
        Assert.Equal(2, chain.Value!.Count);
        Assert.Equal(original.ProformaNo, chain.Value[0].ProformaNo);
        Assert.Equal(0, chain.Value[0].RevisionNumber);
        Assert.Equal(1, chain.Value[1].RevisionNumber);
    }

    [DatabaseFact]
    public async Task Spec_group_grid_save_readback_and_cascade()
    {
        var tenant = _fixture.CreateTenant("Proforma");
        var actor = Guid.NewGuid();
        var invoice = await CreateAsync(tenant);

        var grid = new SaveSpecGroupsRequest(new List<SpecGroupRequest>
        {
            new(1, "DGU", "6mm Clear FT + 12mm ASP + 6mm Clear FT", 119.40m,
                "formula", new List<SpecGroupItemRequest>
                {
                    new(1, "Front", 1000, 2000, 900, 1800, 4, null, ""),
                    new(2, "", 0, 0, 0, 0, 1, null, ""), // phantom — dropped
                }),
            new(2, "SGU", "6mm Clear FT", 46m, "manual", new List<SpecGroupItemRequest>
            {
                new(1, "Side", 300, 300, 0, 0, 2, null, ""),
            }),
        });

        var saved = await _sut.SaveSpecGroupsAsync(
            tenant, invoice.Id, actor, null, grid, CancellationToken.None);
        Assert.True(saved.Success, saved.Message);

        var groups = saved.Value!;
        Assert.Equal(2, groups.Count);

        // Group 1: dual-measurement row — money on the PRIMARY side only.
        var row = groups[0].Items.Single();
        Assert.Equal(2.000m, row.Sqm);
        Assert.Equal(8.000m, row.TotalSqm);
        Assert.Equal(1.620m, row.Sqm2);     // inner 900×1800
        Assert.Equal(6.480m, row.TotalSqm2);
        Assert.Equal(24.000m, row.Lm1);
        Assert.Equal(21.600m, row.Lm2);
        Assert.Equal(955.20m, row.Total);   // 2 × 4 × 119.40 — primary only

        // Group 2: 0.09 m² → 0.5 floor × 2 × 46.
        var small = groups[1].Items.Single();
        Assert.Equal(0.500m, small.Sqm);
        Assert.Equal(46.00m, small.Total);

        // Glass subtotal = 955.20 + 46.00.
        var updated = await _sut.GetAsync(tenant, invoice.Id, CancellationToken.None);
        Assert.Equal(1001.20m, updated.Value!.GlassMaterialsSubtotal);

        // Whole-grid replace: the old groups (and their items, by cascade)
        // die in the same transaction.
        var replacement = new SaveSpecGroupsRequest(new List<SpecGroupRequest>
        {
            new(1, "LAM", "6mm + 1.52mm PVB + 6mm", 228.65m, "formula",
                new List<SpecGroupItemRequest>
                {
                    new(1, "All", 1200, 800, 0, 0, 5, null, ""),
                }),
        });
        var resaved = await _sut.SaveSpecGroupsAsync(
            tenant, invoice.Id, actor, null, replacement, CancellationToken.None);
        Assert.True(resaved.Success, resaved.Message);

        Assert.Single(resaved.Value!);
        var lamRow = resaved.Value![0].Items.Single();
        Assert.Equal(0.960m, lamRow.Sqm);
        Assert.Equal(4.800m, lamRow.TotalSqm);
        Assert.Equal(1097.52m, lamRow.Total); // 0.96 × 5 × 228.65

        var final = await _sut.GetAsync(tenant, invoice.Id, CancellationToken.None);
        Assert.Equal(1097.52m, final.Value!.GlassMaterialsSubtotal);
    }

    [DatabaseFact]
    public async Task Grid_save_is_refused_past_draft()
    {
        var tenant = _fixture.CreateTenant("Proforma");
        var actor = Guid.NewGuid();
        var invoice = await CreateAsync(tenant);

        var submitted = await _sut.TransitionAsync(
            tenant, invoice.Id, actor, null,
            new TransitionRequest("PendingApproval", null), CancellationToken.None);
        Assert.True(submitted.Success, submitted.Message);

        var grid = new SaveSpecGroupsRequest(new List<SpecGroupRequest>());
        var refused = await _sut.SaveSpecGroupsAsync(
            tenant, invoice.Id, actor, null, grid, CancellationToken.None);

        Assert.False(refused.Success);
        Assert.Equal(ProformaErrorCode.Conflict, refused.ErrorCode);
    }

    [DatabaseFact]
    public async Task Proformas_are_tenant_isolated()
    {
        var tenantA = _fixture.CreateTenant("ProformaIsoA");
        var tenantB = _fixture.CreateTenant("ProformaIsoB");

        var invoiceA = await CreateAsync(tenantA);

        // Tenant B cannot see it — read, list, or touch.
        var read = await _sut.GetAsync(tenantB, invoiceA.Id, CancellationToken.None);
        Assert.Equal(ProformaErrorCode.NotFound, read.ErrorCode);

        var listed = await _sut.ListAsync(tenantB, 1, 50, null, null, CancellationToken.None);
        Assert.Equal(0, listed.TotalCount);

        var transition = await _sut.TransitionAsync(
            tenantB, invoiceA.Id, Guid.NewGuid(), null,
            new TransitionRequest("PendingApproval", null), CancellationToken.None);
        Assert.Equal(ProformaErrorCode.NotFound, transition.ErrorCode);

        // …and its number is still free for tenant B to use.
        var own = await CreateAsync(tenantB);
        Assert.Equal(invoiceA.ProformaNo, own.ProformaNo); // both are -001
    }

    [DatabaseFact]
    public async Task List_filters_by_status_and_searches_by_number()
    {
        var tenant = _fixture.CreateTenant("Proforma");

        await CreateAsync(tenant);
        var invoice = await CreateAsync(tenant);
        await _sut.VoidAsync(tenant, invoice.Id, Guid.NewGuid(), null, "cleanup",
            CancellationToken.None);

        var all = await _sut.ListAsync(tenant, 1, 50, null, null, CancellationToken.None);
        Assert.Equal(2, all.TotalCount);

        var cancelled = await _sut.ListAsync(
            tenant, 1, 50, "Cancelled", null, CancellationToken.None);
        Assert.Equal(1, cancelled.TotalCount);
        Assert.Equal("Cancelled", cancelled.Items[0].Status);

        var found = await _sut.ListAsync(
            tenant, 1, 50, null, invoice.ProformaNo[^3..], CancellationToken.None);
        Assert.Single(found.Items);
        Assert.Equal(invoice.Id, found.Items[0].Id);
    }
}
