// ProformaService unit tests: number allocation with the WPF retry
// semantics, the lifecycle gate, void, the revision chain and the Step 5
// grid save. The pure math and transition tables are pinned in
// ProGlass.Saas.Domain.Tests.ProformaGoldenTests — these tests prove the
// SERVICE applies them.

using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Application.Specifications;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;
using Xunit;

namespace ProGlass.Saas.Application.Tests.Proforma;

public class ProformaServiceTests
{
    private readonly FakeProformaStore _store = new();
    private readonly FakeAuditWriter _audit = new();
    private readonly FakeSpecificationsStore _specsStore = new();
    private readonly ProformaService _sut;

    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    public ProformaServiceTests()
    {
        _sut = new ProformaService(
            _store, _audit, new SpecificationsService(_specsStore, _audit));
    }

    private static CreateProformaRequest CreateRequest() => new(
        null, null, null, null, null, null, null, null);

    private async Task<ProformaDto> CreateAsync()
    {
        var result = await _sut.CreateDraftAsync(
            Tenant, Actor, null, CreateRequest(), CancellationToken.None);
        Assert.True(result.Success, result.Message);
        return result.Value!;
    }

    // ═════════════════════════════ CREATE ═════════════════════════

    [Fact]
    public async Task Create_allocates_PI_year_001_from_the_seed()
    {
        var invoice = await CreateAsync();

        Assert.Equal($"PI-{DateTime.UtcNow.Year}-001", invoice.ProformaNo);
        Assert.Equal("Draft", invoice.Status);
        Assert.Equal("Draft", invoice.LifecycleState);
        Assert.True(invoice.IsEditable);
        Assert.Contains("PendingApproval", invoice.ValidTransitions);
        Assert.Contains("Cancelled", invoice.ValidTransitions);
    }

    [Fact]
    public async Task Create_defaults_dates_to_today_plus_30()
    {
        var invoice = await CreateAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal(today, invoice.IssueDate);
        Assert.Equal(today.AddDays(30), invoice.ValidUntil);
    }

    [Fact]
    public async Task Create_with_known_status_normalizes_it()
    {
        var result = await _sut.CreateDraftAsync(
            Tenant, Actor, null,
            CreateRequest() with { Status = "sent" },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Sent", result.Value!.Status);
        Assert.Equal("Draft", result.Value.LifecycleState); // machine starts at Draft
    }

    [Fact]
    public async Task Create_with_unknown_status_is_refused()
    {
        var result = await _sut.CreateDraftAsync(
            Tenant, Actor, null,
            CreateRequest() with { Status = "Frozen" },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ProformaErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Contains("Unknown status", result.Message);
    }

    [Fact]
    public async Task Numbers_allocate_sequentially()
    {
        var first = await CreateAsync();
        var second = await CreateAsync();

        Assert.EndsWith("-001", first.ProformaNo);
        Assert.EndsWith("-002", second.ProformaNo);
    }

    [Fact]
    public async Task A_raced_number_retries_with_the_next_free_id()
    {
        // A concurrent writer steals PI-{year}-001 between the check and
        // the insert (the WPF unique-violation retry): the next attempt
        // re-suggests FROM the collided number and lands on 002.
        _store.RefuseNextInsert = true;

        var invoice = await CreateAsync();

        Assert.EndsWith("-002", invoice.ProformaNo);
    }

    [Fact]
    public async Task When_every_number_races_away_the_service_reports_exhaustion()
    {
        _store.AlwaysRefuseInserts = true;

        var result = await _sut.CreateDraftAsync(
            Tenant, Actor, null, CreateRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ProformaErrorCode.NumberExhausted, result.ErrorCode);
        Assert.Contains("5 retries", result.Message);
    }

    [Fact]
    public async Task Create_is_audited()
    {
        await CreateAsync();

        var entry = Assert.Single(_audit.Events);
        Assert.Equal("proforma.created", entry.Action);
        Assert.Equal("proforma_invoices", entry.EntityType);
    }

    // ═════════════════════════════ LIFECYCLE ══════════════════════

    private Task<ProformaResult<ProformaDto>> TransitionAsync(
        ProformaDto invoice, string toState) =>
        _sut.TransitionAsync(
            Tenant, invoice.Id, Actor, null, new TransitionRequest(toState, null),
            CancellationToken.None);

    [Fact]
    public async Task The_full_happy_path_walks_the_machine()
    {
        var invoice = await CreateAsync();

        invoice = (await TransitionAsync(invoice, "PendingApproval")).Value!;
        Assert.Equal("PendingApproval", invoice.LifecycleState);

        invoice = (await TransitionAsync(invoice, "Approved")).Value!;
        Assert.Equal("Approved", invoice.LifecycleState);
        Assert.NotNull(invoice.ApprovedAt);

        invoice = (await TransitionAsync(invoice, "Sent")).Value!;
        Assert.Equal("Sent", invoice.LifecycleState);
        Assert.NotNull(invoice.SentAt);

        invoice = (await TransitionAsync(invoice, "Confirmed")).Value!;
        Assert.Equal("Confirmed", invoice.LifecycleState);
        Assert.NotNull(invoice.ConfirmedAt);
        Assert.Empty(invoice.ValidTransitions); // terminal
    }

    [Fact]
    public async Task An_invalid_transition_is_a_conflict_naming_the_valid_set()
    {
        var invoice = await CreateAsync();

        var result = await TransitionAsync(invoice, "Approved");

        Assert.False(result.Success);
        Assert.Equal(ProformaErrorCode.Conflict, result.ErrorCode);
        Assert.Contains("Cannot transition from Draft to Approved", result.Message);
        Assert.Contains("PendingApproval", result.Message);
    }

    [Fact]
    public async Task Rejection_returns_to_draft_for_resubmission()
    {
        var invoice = await CreateAsync();
        invoice = (await TransitionAsync(invoice, "PendingApproval")).Value!;

        var rejected = (await TransitionAsync(invoice, "Rejected")).Value!;
        Assert.Equal("Rejected", rejected.LifecycleState);
        Assert.True(rejected.IsEditable); // revise and resubmit

        var back = (await TransitionAsync(rejected, "Draft")).Value!;
        Assert.Equal("Draft", back.LifecycleState);
    }

    [Fact]
    public async Task Unknown_target_state_is_validation_not_conflict()
    {
        var invoice = await CreateAsync();

        var result = await TransitionAsync(invoice, "Bogus");

        Assert.Equal(ProformaErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Missing_invoice_is_not_found()
    {
        var result = await _sut.GetAsync(Tenant, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(ProformaErrorCode.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Lifecycle_writes_are_audited_before_and_after()
    {
        var invoice = await CreateAsync();
        await TransitionAsync(invoice, "PendingApproval");

        var entry = _audit.Events.Last();
        Assert.Equal("proforma.lifecycle", entry.Action);
        Assert.NotNull(entry.BeforeJson);
        Assert.NotNull(entry.AfterJson);
        Assert.Contains("PendingApproval", entry.AfterJson);
    }

    /// <summary>Create and walk the machine to Sent (the state a
    /// revision is normally taken from).</summary>
    private async Task<ProformaDto> CreateSentAsync()
    {
        var invoice = await CreateAsync();
        invoice = (await TransitionAsync(invoice, "PendingApproval")).Value!;
        invoice = (await TransitionAsync(invoice, "Approved")).Value!;
        return (await TransitionAsync(invoice, "Sent")).Value!;
    }

    // ═════════════════════════════ VOID ══════════════════════════

    [Fact]
    public async Task Void_stamps_the_row_and_moves_status_to_cancelled()
    {
        var invoice = await CreateAsync();
        invoice = (await TransitionAsync(invoice, "PendingApproval")).Value!;
        invoice = (await TransitionAsync(invoice, "Approved")).Value!;

        var voided = (await _sut.VoidAsync(
            Tenant, invoice.Id, Actor, null, "Customer walked away",
            CancellationToken.None)).Value!;

        Assert.True(voided.IsVoid);
        Assert.NotNull(voided.VoidedAt);
        Assert.Equal(Actor, voided.VoidedBy);
        Assert.Equal("Customer walked away", voided.VoidReason);
        Assert.Equal("Cancelled", voided.Status); // DocumentLifecyclePolicy
        Assert.Equal("Cancelled", voided.LifecycleState);
        Assert.False(voided.IsEditable);
    }

    [Fact]
    public async Task Blank_void_reason_is_stored_as_no_reason_given()
    {
        var invoice = await CreateAsync();

        var voided = (await _sut.VoidAsync(
            Tenant, invoice.Id, Actor, null, "  ",
            CancellationToken.None)).Value!;

        Assert.Equal("(no reason given)", voided.VoidReason);
    }

    [Fact]
    public async Task Voiding_twice_is_idempotent()
    {
        var invoice = await CreateAsync();

        var first = await _sut.VoidAsync(
            Tenant, invoice.Id, Actor, null, "r1", CancellationToken.None);
        var second = await _sut.VoidAsync(
            Tenant, invoice.Id, Actor, null, "r2", CancellationToken.None);

        Assert.True(first.Success);
        Assert.True(second.Success);       // no-op, still 200
        Assert.Equal(1, _store.UpdateCalls); // nothing written the second time
        Assert.Equal("r1", second.Value!.VoidReason);
    }

    [Fact]
    public async Task A_voided_invoice_cannot_transition()
    {
        var invoice = await CreateAsync();
        await _sut.VoidAsync(Tenant, invoice.Id, Actor, null, "gone",
            CancellationToken.None);

        var result = await TransitionAsync(invoice, "PendingApproval");

        Assert.Equal(ProformaErrorCode.Conflict, result.ErrorCode);
        Assert.Contains("terminal history", result.Message);
    }

    // ═════════════════════════════ REVISION ═══════════════════════

    [Fact]
    public async Task Revision_copies_the_header_and_restarts_as_draft()
    {
        var invoice = await CreateAsync();
        invoice = (await TransitionAsync(invoice, "PendingApproval")).Value!;
        invoice = (await TransitionAsync(invoice, "Approved")).Value!;
        invoice = (await TransitionAsync(invoice, "Sent")).Value!;

        var revision = (await _sut.CreateRevisionAsync(
            Tenant, invoice.Id, Actor, null, CancellationToken.None)).Value!;

        Assert.Equal($"{invoice.ProformaNo}-R1", revision.ProformaNo);
        Assert.Equal(invoice.Id, revision.RootProformaId);
        Assert.Equal(1, revision.RevisionNumber);
        Assert.Equal("Draft", revision.Status);
        Assert.Equal("Draft", revision.LifecycleState);

        // The original is untouched for audit.
        var original = await _sut.GetAsync(Tenant, invoice.Id, CancellationToken.None);
        Assert.Equal("Sent", original.Value!.LifecycleState);

        var chain = await _sut.GetRevisionChainAsync(
            Tenant, invoice.Id, CancellationToken.None);
        Assert.Equal(2, chain.Value!.Count);
        Assert.Equal(0, chain.Value[0].RevisionNumber); // root first (WPF order)
        Assert.Equal(1, chain.Value[1].RevisionNumber);
    }

    [Fact]
    public async Task Second_revision_numbers_R2()
    {
        var invoice = await CreateSentAsync();

        await _sut.CreateRevisionAsync(Tenant, invoice.Id, Actor, null,
            CancellationToken.None);
        var second = (await _sut.CreateRevisionAsync(
            Tenant, invoice.Id, Actor, null, CancellationToken.None)).Value!;

        Assert.Equal($"{invoice.ProformaNo}-R2", second.ProformaNo);
        Assert.Equal(2, second.RevisionNumber);
    }

    [Fact]
    public async Task A_draft_cannot_be_revised()
    {
        var invoice = await CreateAsync();

        var result = await _sut.CreateRevisionAsync(
            Tenant, invoice.Id, Actor, null, CancellationToken.None);

        Assert.Equal(ProformaErrorCode.Conflict, result.ErrorCode);
        Assert.Contains("already editable", result.Message);
    }

    [Fact]
    public async Task A_voided_invoice_cannot_be_revised()
    {
        var invoice = await CreateSentAsync();
        await _sut.VoidAsync(Tenant, invoice.Id, Actor, null, "x",
            CancellationToken.None);

        var result = await _sut.CreateRevisionAsync(
            Tenant, invoice.Id, Actor, null, CancellationToken.None);

        Assert.Equal(ProformaErrorCode.Conflict, result.ErrorCode);
        Assert.Contains("terminal history", result.Message);
    }

    [Fact]
    public async Task A_colliding_revision_number_gets_a_guard_suffix()
    {
        var invoice = await CreateSentAsync();

        // Pathological pre-existing "-R1" (WPF: "-R{n}-{guard}").
        _store.Invoices.Add(new ProformaInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = Tenant,
            ProformaNo = $"{invoice.ProformaNo}-R1",
        });

        var revision = (await _sut.CreateRevisionAsync(
            Tenant, invoice.Id, Actor, null, CancellationToken.None)).Value!;

        Assert.Equal($"{invoice.ProformaNo}-R1-1", revision.ProformaNo);
    }

    // ═════════════════════════════ STEP 5 GRID ════════════════════

    private static SaveSpecGroupsRequest Grid(decimal rate = 46m) => new(
        new List<SpecGroupRequest>
        {
            new(1, "SGU", "6mm Clear FT", rate, "formula", new List<SpecGroupItemRequest>
            {
                new(1, "Front", 1000, 1000, 0, 0, 3, null, ""),
                new(2, "", 0, 0, 0, 0, 1, null, ""), // phantom — dropped
                new(3, "Side", 300, 300, 0, 0, 2, null, "small panels"),
            }),
        });

    [Fact]
    public async Task Grid_save_prices_rows_and_drops_phantoms()
    {
        var invoice = await CreateAsync();

        var saved = (await _sut.SaveSpecGroupsAsync(
            Tenant, invoice.Id, Actor, null, Grid(), CancellationToken.None)).Value!;

        var group = Assert.Single(saved);
        Assert.Equal("SGU", group.GlassType);
        Assert.Equal(2, group.Items.Count); // phantom dropped

        // Row 1: 1.000 m² × 3 × 46 = 138.00
        var row1 = group.Items[0];
        Assert.Equal(1.000m, row1.Sqm);
        Assert.Equal(3.000m, row1.TotalSqm);
        Assert.Equal(138.00m, row1.Total);
        Assert.Equal(6.000m, row1.Lm1);

        // Row 3: 0.09 m² → 0.5 floor × 2 × 46 = 46.00
        var row3 = group.Items[1];
        Assert.Equal(0.500m, row3.Sqm);
        Assert.Equal(46.00m, row3.Total);

        Assert.Equal(5, group.TotalQty);
        Assert.Equal(4.000m, group.TotalSqm);

        // Step 7 summary: the invoice's glass subtotal = Σ row values.
        var updated = await _sut.GetAsync(Tenant, invoice.Id, CancellationToken.None);
        Assert.Equal(184.00m, updated.Value!.GlassMaterialsSubtotal);
    }

    [Fact]
    public async Task Grid_save_replaces_the_previous_grid()
    {
        var invoice = await CreateAsync();
        await _sut.SaveSpecGroupsAsync(
            Tenant, invoice.Id, Actor, null, Grid(), CancellationToken.None);

        // Clear back to nothing — a legal save.
        var cleared = (await _sut.SaveSpecGroupsAsync(
            Tenant, invoice.Id, Actor, null,
            new SaveSpecGroupsRequest(new List<SpecGroupRequest>()),
            CancellationToken.None)).Value!;

        Assert.Empty(cleared);
        var updated = await _sut.GetAsync(Tenant, invoice.Id, CancellationToken.None);
        Assert.Equal(0m, updated.Value!.GlassMaterialsSubtotal);
    }

    [Fact]
    public async Task Grid_only_saves_on_editable_bodies()
    {
        var invoice = await CreateAsync();
        await TransitionAsync(invoice, "PendingApproval");

        var result = await _sut.SaveSpecGroupsAsync(
            Tenant, invoice.Id, Actor, null, Grid(), CancellationToken.None);

        Assert.Equal(ProformaErrorCode.Conflict, result.ErrorCode);
        Assert.Contains("Draft or Rejected", result.Message);
    }

    [Fact]
    public async Task A_rejected_body_is_editable_again()
    {
        var invoice = await CreateAsync();
        invoice = (await TransitionAsync(invoice, "PendingApproval")).Value!;
        invoice = (await TransitionAsync(invoice, "Rejected")).Value!;

        var result = await _sut.SaveSpecGroupsAsync(
            Tenant, invoice.Id, Actor, null, Grid(), CancellationToken.None);

        Assert.True(result.Success, result.Message);
    }

    [Theory]
    [InlineData("TRIPLE")]
    [InlineData("sgu")] // not canonical — the DB CHECK is exact
    public async Task Grid_rejects_unknown_glass_types(string glassType)
    {
        var invoice = await CreateAsync();
        var grid = Grid() with
        {
            Groups = new List<SpecGroupRequest>
            {
                new(1, glassType, "x", 10m, "formula", new List<SpecGroupItemRequest>()),
            },
        };

        var result = await _sut.SaveSpecGroupsAsync(
            Tenant, invoice.Id, Actor, null, grid, CancellationToken.None);

        Assert.Equal(ProformaErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Contains("glass type", result.Message);
    }

    [Fact]
    public async Task Grid_rejects_duplicate_group_numbers()
    {
        var invoice = await CreateAsync();
        var grid = Grid() with
        {
            Groups = new List<SpecGroupRequest>
            {
                new(1, "SGU", "a", 10m, "formula", new List<SpecGroupItemRequest>()),
                new(1, "DGU", "b", 10m, "formula", new List<SpecGroupItemRequest>()),
            },
        };

        var result = await _sut.SaveSpecGroupsAsync(
            Tenant, invoice.Id, Actor, null, grid, CancellationToken.None);

        Assert.Equal(ProformaErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Contains("appears twice", result.Message);
    }

    [Theory]
    [InlineData(0, 500, 2)]      // zero width
    [InlineData(500, 0, 2)]      // zero height
    [InlineData(500, 500, 0)]    // zero qty
    [InlineData(10001, 500, 2)]  // over the dimension cap
    [InlineData(500, 500, 10001)] // over the qty cap
    public async Task Grid_rejects_invalid_rows(int w, int h, int qty)
    {
        var invoice = await CreateAsync();
        var grid = Grid() with
        {
            Groups = new List<SpecGroupRequest>
            {
                new(1, "SGU", "x", 10m, "formula", new List<SpecGroupItemRequest>
                {
                    new(1, "Front", w, h, 0, 0, qty, null, ""),
                }),
            },
        };

        var result = await _sut.SaveSpecGroupsAsync(
            Tenant, invoice.Id, Actor, null, grid, CancellationToken.None);

        Assert.Equal(ProformaErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Grid_rejects_half_filled_secondary_measurement()
    {
        var invoice = await CreateAsync();
        var grid = Grid() with
        {
            Groups = new List<SpecGroupRequest>
            {
                new(1, "SGU", "x", 10m, "formula", new List<SpecGroupItemRequest>
                {
                    new(1, "Front", 1000, 1000, 900, 0, 2, null, ""), // W2 without H2
                }),
            },
        };

        var result = await _sut.SaveSpecGroupsAsync(
            Tenant, invoice.Id, Actor, null, grid, CancellationToken.None);

        Assert.Equal(ProformaErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Contains("W2/H2", result.Message);
    }

    [Fact]
    public async Task Grid_rejects_unknown_price_sources()
    {
        var invoice = await CreateAsync();
        var grid = Grid() with
        {
            Groups = new List<SpecGroupRequest>
            {
                new(1, "SGU", "x", 10m, "guess", new List<SpecGroupItemRequest>()),
            },
        };

        var result = await _sut.SaveSpecGroupsAsync(
            Tenant, invoice.Id, Actor, null, grid, CancellationToken.None);

        Assert.Equal(ProformaErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Contains("price source", result.Message);
    }

    [Fact]
    public async Task Grid_reads_back_with_computed_lm()
    {
        var invoice = await CreateAsync();
        await _sut.SaveSpecGroupsAsync(
            Tenant, invoice.Id, Actor, null, Grid(), CancellationToken.None);

        var groups = await _sut.GetSpecGroupsAsync(
            Tenant, invoice.Id, CancellationToken.None);

        var row = groups.Value!.Single().Items[0];
        Assert.Equal(6.000m, row.Lm1); // (1000+1000)/1000 × 2 × 3
        Assert.Equal(0m, row.Lm2);
    }

    [Fact]
    public async Task Grid_save_stamps_the_referenced_spec_and_reconciles()
    {
        var spec = await new SpecificationsService(_specsStore, _audit).ApplyAsync(
            Tenant, Actor, null,
            new ApplySpecificationRequest(
                "SGU", "6mm Clear FT", 46m, null, null, null, null, null,
                null, null, null, null),
            CancellationToken.None);
        Assert.True(spec.Success, spec.Message);

        var invoice = await CreateAsync();
        var grid = Grid() with
        {
            Groups = new List<SpecGroupRequest>
            {
                new(1, "SGU", "6mm Clear FT", 46m, "formula",
                    new List<SpecGroupItemRequest>
                    {
                        new(1, "Front", 1000, 1000, 0, 0, 3, null, ""),
                    },
                    SpecId: spec.Value!.Id),
            },
        };

        var saved = await _sut.SaveSpecGroupsAsync(
            Tenant, invoice.Id, Actor, null, grid, CancellationToken.None);
        Assert.True(saved.Success, saved.Message);

        // The ledger ran after the save (WPF: after every invoice write).
        Assert.Equal(1, _specsStore.ReconcileCalls);
    }

    [Fact]
    public async Task Grid_save_refuses_an_unknown_spec_reference()
    {
        var invoice = await CreateAsync();
        var grid = Grid() with
        {
            Groups = new List<SpecGroupRequest>
            {
                new(1, "SGU", "x", 46m, "formula",
                    new List<SpecGroupItemRequest>(),
                    SpecId: Guid.NewGuid()),
            },
        };

        var result = await _sut.SaveSpecGroupsAsync(
            Tenant, invoice.Id, Actor, null, grid, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ProformaErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Contains("was not found", result.Message);
        Assert.Equal(0, _specsStore.ReconcileCalls); // nothing was saved
    }
}
