// SpecificationsService unit tests: the Step 4 APPLY upsert semantics
// (dedup key, rate-always, preserve-on-blank catalogue, counts never
// touched) and the WPF list/usage contracts. The ledger's set-based
// derivation is pinned by the integration tests on live PostgreSQL.

using ProGlass.Saas.Application.Specifications;
using Xunit;

namespace ProGlass.Saas.Application.Tests.Specifications;

public class SpecificationsServiceTests
{
    private readonly FakeSpecificationsStore _store = new();
    private readonly FakeAuditWriter _audit = new();
    private readonly SpecificationsService _sut;

    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    public SpecificationsServiceTests()
    {
        _sut = new SpecificationsService(_store, _audit);
    }

    private static ApplySpecificationRequest Apply(
        decimal rate = 46m, string name = "6mm Clear FT Glass",
        string glassType = "SGU") =>
        new(glassType, name, rate, null, null, null, null, null, null, null, null, null);

    // ═════════════════════════════ APPLY ══════════════════════════

    [Fact]
    public async Task Apply_creates_a_new_spec_with_zero_usage()
    {
        var result = await _sut.ApplyAsync(
            Tenant, Actor, null, Apply(), CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal("6mm Clear FT Glass", result.Value!.SpecName);
        Assert.Equal("SGU", result.Value.GlassType);
        Assert.Equal(46m, result.Value.RatePerSqm);
        // Being built in Step 4 is not being used — counts are
        // ledger-derived from SAVED invoices.
        Assert.Equal(0, result.Value.UsageCount);
        Assert.NotNull(result.Value.LastUsedAt);
    }

    [Fact]
    public async Task Apply_updates_the_rate_but_never_the_count()
    {
        await _sut.ApplyAsync(Tenant, Actor, null, Apply(46m), CancellationToken.None);

        var updated = await _sut.ApplyAsync(
            Tenant, Actor, null, Apply(52m), CancellationToken.None);

        Assert.True(updated.Success);
        Assert.Equal(52m, updated.Value!.RatePerSqm);
        Assert.Equal(0, updated.Value.UsageCount); // APPLY never touches counts
        Assert.Single(_store.Specs);               // dedup by (type, name)
    }

    [Fact]
    public async Task Apply_preserves_catalogue_details_a_blank_call_would_wipe()
    {
        // A well-informed APPLY records the catalogue details…
        var informed = await _sut.ApplyAsync(
            Tenant, Actor, null,
            Apply() with
            {
                Category = "Clear",
                Color = "Clear",
                ThicknessMm = 6,
                SheetWidthMm = 3210,
                SheetHeightMm = 2250,
            },
            CancellationToken.None);
        Assert.True(informed.Success);

        // …a later 3-arg-style APPLY (no catalogue values) must NOT wipe
        // them — only the rate moves.
        var blank = await _sut.ApplyAsync(
            Tenant, Actor, null, Apply(50m), CancellationToken.None);

        Assert.True(blank.Success);
        Assert.Equal(50m, blank.Value!.RatePerSqm);
        Assert.Equal("Clear", blank.Value.Category);
        Assert.Equal("Clear", blank.Value.Color);
        Assert.Equal(6, blank.Value.ThicknessMm);
        Assert.Equal(3210, blank.Value.SheetWidthMm);
        Assert.Equal(2250, blank.Value.SheetHeightMm);
    }

    [Fact]
    public async Task Apply_same_name_different_glass_type_is_a_different_spec()
    {
        await _sut.ApplyAsync(
            Tenant, Actor, null, Apply(name: "6mm Clear FT"), CancellationToken.None);
        await _sut.ApplyAsync(
            Tenant, Actor, null,
            Apply(name: "6mm Clear FT", glassType: "DGU"),
            CancellationToken.None);

        Assert.Equal(2, _store.Specs.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Apply_requires_a_spec_name(string name)
    {
        var result = await _sut.ApplyAsync(
            Tenant, Actor, null, Apply(name: name), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(SpecificationErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Apply_rejects_unknown_glass_types()
    {
        var result = await _sut.ApplyAsync(
            Tenant, Actor, null, Apply(glassType: "TRIPLE"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("glass type", result.Message);
    }

    [Fact]
    public async Task Apply_rejects_negative_rates()
    {
        var result = await _sut.ApplyAsync(
            Tenant, Actor, null, Apply(rate: -1m), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("negative", result.Message);
    }

    [Fact]
    public async Task Apply_is_audited()
    {
        await _sut.ApplyAsync(Tenant, Actor, null, Apply(), CancellationToken.None);
        await _sut.ApplyAsync(Tenant, Actor, null, Apply(50m), CancellationToken.None);

        Assert.Equal(2, _audit.Events.Count(e => e.Action == "specifications.applied"));
    }

    // ═════════════════════════════ LISTS ══════════════════════════

    [Fact]
    public async Task List_smart_sorts_most_used_first_then_most_recent()
    {
        await _sut.ApplyAsync(
            Tenant, Actor, null, Apply(name: "A"), CancellationToken.None);
        await _sut.ApplyAsync(
            Tenant, Actor, null, Apply(name: "B"), CancellationToken.None);
        await _sut.ApplyAsync(
            Tenant, Actor, null, Apply(name: "C", glassType: "DGU"),
            CancellationToken.None);

        // Simulate ledger healing: B is the most-used, A recent.
        _store.Specs.First(s => s.SpecName == "B").UsageCount = 7;
        _store.Specs.First(s => s.SpecName == "A").UsageCount = 7;
        _store.Specs.First(s => s.SpecName == "A").LastUsedAt = DateTime.UtcNow;

        var all = await _sut.ListAsync(Tenant, null, CancellationToken.None);
        Assert.Equal(new[] { "A", "B", "C" }, all.Select(s => s.SpecName));

        var dguOnly = await _sut.ListAsync(Tenant, "DGU", CancellationToken.None);
        Assert.Single(dguOnly);
        Assert.Equal("C", dguOnly[0].SpecName);
    }

    // ═════════════════════════════ DELETE ═════════════════════════

    [Fact]
    public async Task Delete_missing_id_is_not_found()
    {
        var result = await _sut.DeleteAsync(
            Tenant, Guid.NewGuid(), Actor, null, CancellationToken.None);

        Assert.Equal(SpecificationErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_removes_the_spec_and_its_usage_rows()
    {
        var created = await _sut.ApplyAsync(
            Tenant, Actor, null, Apply(), CancellationToken.None);

        var deleted = await _sut.DeleteAsync(
            Tenant, created.Value!.Id, Actor, null, CancellationToken.None);

        Assert.True(deleted.Success);
        Assert.Empty(_store.Specs);
    }

    // ═════════════════════════════ LEDGER ═════════════════════════

    [Fact]
    public async Task Reconcile_is_audited_with_the_outcome()
    {
        await _sut.ReconcileAsync(Tenant, Actor, null, CancellationToken.None);

        Assert.Equal(1, _store.ReconcileCalls);
        var entry = Assert.Single(_audit.Events);
        Assert.Equal("specifications.reconciled", entry.Action);
        Assert.Contains("usageRows", entry.AfterJson);
    }
}
