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


    // ═════════════════════════════ STEP 6 ══════════════════════════

    /// <summary>A DGU grid with one row: SQM1 6.00, SQM2 2.43,
    /// LM1 18.00, LM2 10.80 — outer + inner wastage sources.</summary>
    private static SaveSpecGroupsRequest Step6Grid() => new(
        new List<SpecGroupRequest>
        {
            new(1, "DGU", "6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass",
                46m, "formula", new List<SpecGroupItemRequest>
            {
                new(1, "Front", 1000, 2000, 900, 900, 3, null, ""),
            }),
        });

    private async Task<ProformaDto> CreateWithGridAsync()
    {
        var invoice = await CreateAsync();
        var grid = await _sut.SaveSpecGroupsAsync(
            Tenant, invoice.Id, Actor, null, Step6Grid(), CancellationToken.None);
        Assert.True(grid.Success, grid.Message);
        return invoice;
    }

    private static SaveStep6Request Step6(decimal rate = 45m) => new(
        new List<OtherChargeRowRequest>
        {
            new("ARGON_GAS", "Argon Gas (Sqm)",
                "Inert Argon gas infusion inside DGU cavity.", "Sqm", true,
                "SQM1", new List<int> { 1 }, rate),
        },
        new List<WastageRowRequest>
        {
            new(true, "Clear", 12.5m, true, false, 0m, 0m, 1,
                new List<string> { "1-Outer (W1/H1)" }),
            new(true, "Clear", 0m, false, true, 52m, 78m, 2,
                new List<string> { "1-Outer (W1/H1)", "1-Inner (W2/H2)" }),
        },
        new List<CustomChargeRowRequest>
        {
            new(true, "Crane rental", 2m, "Qty", 250m),
        });

    [Fact]
    public async Task Step6_save_derives_every_qty_and_amount_from_the_grid()
    {
        var invoice = await CreateWithGridAsync();

        var saved = await _sut.SaveStep6Async(
            Tenant, invoice.Id, Actor, null, Step6(), CancellationToken.None);
        Assert.True(saved.Success, saved.Message);

        // ARGON_GAS: SQM1 of spec 1 = 6.00 × 45 = 270.00.
        var argon = Assert.Single(saved.Value!.Charges);
        Assert.Equal("ARGON_GAS", argon.TemplateKey);
        Assert.Equal(6.000m, argon.MetricQty);
        Assert.Equal(270.00m, argon.Amount);

        // Manual wastage: 6.00 SQM × 12.5 = 75.00 (plain Math.Round).
        var manual = saved.Value.Wastages[0];
        Assert.Equal(6.000m, manual.TotalSqm);
        Assert.Equal(12.5m, manual.Rate);
        Assert.Equal(75.00m, manual.Amount);

        // Optimization wastage: rate is DERIVED (85% baseline gap), the
        // entered 0 is never stored — 5.490196 × 8.43 = 46.28.
        var optimization = saved.Value.Wastages[1];
        Assert.Equal(8.430m, optimization.TotalSqm);
        Assert.Equal(5.490196m, optimization.Rate);
        Assert.Equal(46.28m, optimization.Amount);

        // Custom: 2 × 250 = 500.
        var custom = Assert.Single(saved.Value.Customs);
        Assert.Equal(500.00m, custom.Amount);

        // RecalculateTotal lands on the invoice: 270 + 75 + 46.28 + 500.
        Assert.Equal(891.28m, saved.Value.Subtotal);
        var invoiceAfter = _store.Invoices.First(i => i.Id == invoice.Id);
        Assert.Equal(891.28m, invoiceAfter.OtherChargesSubtotal);

        // The WPF recalculates live; the SaaS saves the derivation.
        Assert.Contains(_audit.Events, e => e.Action == "proforma.step6_saved");
    }

    [Fact]
    public async Task Step6_delivery_is_one_trip_and_polish_uses_the_sub_type()
    {
        var invoice = await CreateWithGridAsync();

        var saved = await _sut.SaveStep6Async(
            Tenant, invoice.Id, Actor, null, new SaveStep6Request(
                new List<OtherChargeRowRequest>
                {
                    // Chat 7 Bug 4c: per-invoice, target selection ignored.
                    new("DELIVERY", "Delivery (per trip)", "", "Qty", true,
                        "QTY", new List<int>(), 150m),
                    // Polish: AllSidePolish over BOTH panes of the DGU —
                    // (6 + 3.6) × 3 = 28.8 LM.
                    new("POLISH", "Polish (lm)", "", "Lm", true,
                        "", new List<int> { 1 }, 6m,
                        PolishSubTypes.AllSidePolish),
                },
                new List<WastageRowRequest>(),
                new List<CustomChargeRowRequest>()),
            CancellationToken.None);

        Assert.True(saved.Success, saved.Message);
        Assert.Equal(1.000m, saved.Value!.Charges[0].MetricQty);
        Assert.Equal(150.00m, saved.Value.Charges[0].Amount);
        Assert.Equal(28.800m, saved.Value.Charges[1].MetricQty);
        Assert.Equal(172.80m, saved.Value.Charges[1].Amount); // 28.8 × 6
    }

    [Fact]
    public async Task Step6_inactive_rows_cost_nothing_but_are_kept()
    {
        var invoice = await CreateWithGridAsync();

        var saved = await _sut.SaveStep6Async(
            Tenant, invoice.Id, Actor, null, Step6() with { },
            CancellationToken.None);
        Assert.True(saved.Success);

        // Re-save with everything off: rows persist, subtotal = 0.
        var off = new SaveStep6Request(
            new List<OtherChargeRowRequest>
            {
                new("ARGON_GAS", "Argon Gas (Sqm)", "", "Sqm", false,
                    "SQM1", new List<int> { 1 }, 45m),
            },
            new List<WastageRowRequest>
            {
                new(false, "Clear", 12.5m, true, false, 0m, 0m, 1,
                    new List<string> { "1-Outer (W1/H1)" }),
            },
            new List<CustomChargeRowRequest>
            {
                new(false, "Crane rental", 2m, "Qty", 250m),
            });

        var result = await _sut.SaveStep6Async(
            Tenant, invoice.Id, Actor, null, off, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Single(result.Value!.Charges);
        Assert.Equal(0m, result.Value.Charges[0].Amount);
        Assert.Equal(0m, result.Value.Subtotal);
    }

    [Fact]
    public async Task Step6_rejects_unknown_metrics_and_stale_sources()
    {
        var invoice = await CreateWithGridAsync();

        var badMetric = await _sut.SaveStep6Async(
            Tenant, invoice.Id, Actor, null, new SaveStep6Request(
                new List<OtherChargeRowRequest>
                {
                    new("ARGON_GAS", "x", "", "Sqm", true, "SQM",
                        new List<int> { 1 }, 45m),
                }, null, null), CancellationToken.None);
        Assert.False(badMetric.Success);
        Assert.Contains("unknown metric key", badMetric.Message);

        var badSpec = await _sut.SaveStep6Async(
            Tenant, invoice.Id, Actor, null, new SaveStep6Request(
                new List<OtherChargeRowRequest>
                {
                    new("ARGON_GAS", "x", "", "Sqm", true, "SQM1",
                        new List<int> { 7 }, 45m),
                }, null, null), CancellationToken.None);
        Assert.False(badSpec.Success);
        Assert.Contains("spec group 7 does not exist", badSpec.Message);

        var staleSource = await _sut.SaveStep6Async(
            Tenant, invoice.Id, Actor, null, new SaveStep6Request(
                null,
                new List<WastageRowRequest>
                {
                    new(true, "", 10m, true, false, 0m, 0m, 1,
                        new List<string> { "1-Lam Inner (W2/H2)" }),
                }, null), CancellationToken.None);
        Assert.False(staleSource.Success);
        Assert.Contains("does not exist", staleSource.Message);
    }

    [Fact]
    public async Task Step6_refuses_voided_invoices()
    {
        var invoice = await CreateWithGridAsync();
        var voided = await _sut.VoidAsync(
            Tenant, invoice.Id, Actor, null, new VoidRequest("done"),
            CancellationToken.None);
        Assert.True(voided.Success, voided.Message);

        var result = await _sut.SaveStep6Async(
            Tenant, invoice.Id, Actor, null, Step6(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ProformaErrorCode.Conflict, result.ErrorCode);
    }

    [Fact]
    public async Task Step6_get_returns_the_derived_read_model()
    {
        var invoice = await CreateWithGridAsync();
        await _sut.SaveStep6Async(
            Tenant, invoice.Id, Actor, null, Step6(), CancellationToken.None);

        var section = await _sut.GetStep6Async(Tenant, invoice.Id, CancellationToken.None);

        Assert.True(section.Success, section.Message);
        // The per-spec metrics strip (the Step 6 top panel).
        var metrics = Assert.Single(section.Value!.SpecMetrics);
        Assert.True(metrics.IsDoubleGlass);
        Assert.Equal(2, metrics.PaneFactor);
        Assert.Equal(18.00m, metrics.Lm1);
        Assert.Equal(2.43m, metrics.Sqm2);
        // The available wastage sources for this grid.
        Assert.Equal(2, section.Value.WastageSources.Count);
        Assert.Contains(section.Value.WastageSources,
            s => s.Key == "1-Inner (W2/H2)" && s.SqmValue == 2.43m);
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
