using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.JobOrders;
using ProGlass.Saas.Application.Optimization;
using ProGlass.Saas.Application.Production;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Application.Stock;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// The Optimization core (Phase 11) against live PostgreSQL. The job's
/// DGU spec ("6mm Clear FT + 12mm ASP + 6mm Green Annealed") yields TWO
/// cutting compositions (FT outer, annealed inner) → two sessions, each
/// packed against the tenant's 6mm racks.
/// </summary>
[Collection("database")]
public sealed class OptimizationFlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly OptimizationService _sut;
    private readonly StockService _stock;
    private readonly JobOrderService _jobs;
    private readonly ProformaService _proformas;

    public OptimizationFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var factory = new SaasDbContextFactory(fixture.AppDb);
        var audit = new AuditWriter(factory, NullLogger<AuditWriter>.Instance);
        var specs = new SpecificationsService(
            new SpecificationsStore(factory), audit);
        _sut = new OptimizationService(
            new OptimizationStore(factory), audit,
            new ProductionStore(factory));
        _stock = new StockService(new StockStore(factory), audit);
        _jobs = new JobOrderService(new JobOrderStore(factory), audit);
        _proformas = new ProformaService(new ProformaStore(factory), audit, specs);
    }

    private async Task<(Guid TenantId, Guid JobOrderId)> CreateJobAsync(string prefix)
    {
        var tenant = _fixture.CreateTenant(prefix);

        // One 6mm rack, balance 10.
        var sheet = await _stock.CreateSheetAsync(tenant, Guid.NewGuid(),
            new GlassSheetRequest("Float", 6, "Clear", 3210, 2250, "GlasImport",
                95m, 140m, Stock: 10, Used: 0),
            null, CancellationToken.None);
        Assert.True(sheet.Success, sheet.Message);

        var pi = (await _proformas.CreateDraftAsync(
            tenant, Guid.NewGuid(), null,
            new CreateProformaRequest(null, null, null, null, null, null, null, null),
            CancellationToken.None)).Value!;
        var grid = new SaveSpecGroupsRequest(new List<SpecGroupRequest>
        {
            new(1, "DGU", "6mm Clear FT Glass + 12mm ASP + 6mm Green Annealed Glass",
                119.40m, "formula", new List<SpecGroupItemRequest>
                {
                    new(1, "Front", 1000, 2000, 900, 1800, 4, null, ""),
                }),
        });
        await _proformas.SaveSpecGroupsAsync(
            tenant, pi.Id, Guid.NewGuid(), null, grid, CancellationToken.None);
        var job = await _jobs.CreateFromProformaAsync(
            tenant, Guid.NewGuid(), new CreateJobOrderRequest(pi.Id),
            null, CancellationToken.None);
        Assert.True(job.Success, job.Message);
        return (tenant, job.Value!.Id);
    }

    [Fact]
    public async Task Run_groups_by_composition_and_persists_the_plan()
    {
        var (tenant, jobId) = await CreateJobAsync("Opt1");

        var run = await _sut.RunAsync(
            tenant, Guid.NewGuid(), new RunOptimizationRequest(JobOrderId: jobId),
            null, CancellationToken.None);
        Assert.True(run.Success, run.Message);

        // Two compositions → two sessions.
        Assert.Equal(2, run.Value!.Sessions.Count);
        Assert.All(run.Value.Sessions, s =>
        {
            Assert.Equal("completed", s.Status);
            Assert.StartsWith("QUICK SHELF", s.Strategy);
            Assert.True(s.GeometryVerified, string.Join("; ", s.ValidationProblems));
            Assert.True(s.ConservationVerified);
            Assert.True(s.UtilizationPct > 0);
        });

        // 4 outer + 4 inner panes, all placed (rack balance 10 is ample).
        var totalPlacements = run.Value.Sheets.Sum(s => s.Placements.Count);
        Assert.Equal(8, totalPlacements);
        Assert.All(run.Value.Sheets, s =>
        {
            Assert.NotEmpty(s.Cuts);
            Assert.NotEmpty(s.Leftovers);
            // Every placement inside the usable rectangle.
            Assert.All(s.Placements, p =>
            {
                Assert.True(p.X >= s.UsableX && p.Y >= s.UsableY);
                Assert.True(p.X + p.W <= s.UsableX + s.UsableW);
                Assert.True(p.Y + p.H <= s.UsableY + s.UsableH);
            });
        });

        // The sessions read back.
        var session = run.Value.Sessions[0];
        var readBack = await _sut.GetSessionAsync(tenant, session.Id, CancellationToken.None);
        Assert.True(readBack.Success, readBack.Message);
        Assert.Equal(session.CompositionKey, readBack.Value!.CompositionKey);

        var sheets = await _sut.GetSheetsAsync(
            tenant, session.Id, CancellationToken.None);
        Assert.True(sheets.Success, sheets.Message);
        Assert.Equal(session.SheetsUsed, sheets.Value!.Count);
    }

    [Fact]
    public async Task Run_with_explicit_pieces_and_guarded_requests()
    {
        var tenant = _fixture.CreateTenant("Opt2");

        // No pieces and no job → validation error.
        var empty = await _sut.RunAsync(
            tenant, Guid.NewGuid(), new RunOptimizationRequest(),
            null, CancellationToken.None);
        Assert.False(empty.Success);

        // Explicit pieces + explicit stocks, no Sheet Store needed.
        var run = await _sut.RunAsync(
            tenant, Guid.NewGuid(), new RunOptimizationRequest(
                Pieces: new List<OptimizationPieceRequest>
                {
                    new("A", "6mm Clear Annealed Glass", "Glass",
                        1000, 2000, Qty: 2),
                    new("B", "6mm Clear Annealed Glass", "Glass",
                        950, 1150, Qty: 2),
                },
                Stocks: new List<OptimizationStockRequest>
                {
                    new(3210, 2250, 6, "Clear"),
                }),
            null, CancellationToken.None);
        Assert.True(run.Success, run.Message);
        Assert.Single(run.Value!.Sessions);
        Assert.Equal(4,
            run.Value.Sheets.Sum(s => s.Placements.Count));

        // A job that does not exist → 404.
        var missing = await _sut.RunAsync(
            tenant, Guid.NewGuid(),
            new RunOptimizationRequest(JobOrderId: Guid.NewGuid()),
            null, CancellationToken.None);
        Assert.False(missing.Success);
        Assert.Equal(OptimizationErrorCode.NotFound, missing.ErrorCode);
    }

    [Fact]
    public async Task Rack_balance_caps_the_pool()
    {
        var (tenant, jobId) = await CreateJobAsync("Opt3");

        // Balance 10 racks exist, but cap the pool to 1 sheet explicitly.
        var run = await _sut.RunAsync(
            tenant, Guid.NewGuid(), new RunOptimizationRequest(
                JobOrderId: jobId,
                Stocks: new List<OptimizationStockRequest>
                {
                    new(3210, 2250, 6, "Clear", MaxSheets: 1),
                }),
            null, CancellationToken.None);
        Assert.True(run.Success, run.Message);

        // One sheet per composition; the rest is reported unplaced.
        Assert.Equal(2, run.Value!.Sessions.Count);
        Assert.All(run.Value.Sessions, s => Assert.Equal(1, s.SheetsUsed));
        Assert.All(run.Value.Sessions, s => Assert.True(s.UnplacedCount > 0));
    }

    [Fact]
    public async Task Tenant_isolation_hides_other_tenants_sessions()
    {
        var (tenant, jobId) = await CreateJobAsync("Opt4a");
        var run = await _sut.RunAsync(
            tenant, Guid.NewGuid(), new RunOptimizationRequest(JobOrderId: jobId),
            null, CancellationToken.None);
        Assert.True(run.Success, run.Message);
        var session = run.Value!.Sessions[0];

        var other = _fixture.CreateTenant("Opt4b");
        var foreign = await _sut.GetSessionAsync(
            other, session.Id, CancellationToken.None);
        Assert.False(foreign.Success);
        var foreignSheets = await _sut.GetSheetsAsync(
            other, session.Id, CancellationToken.None);
        Assert.False(foreignSheets.Success);
        var foreignRun = await _sut.RunAsync(
            other, Guid.NewGuid(),
            new RunOptimizationRequest(JobOrderId: jobId),
            null, CancellationToken.None);
        Assert.False(foreignRun.Success);
    }
}
