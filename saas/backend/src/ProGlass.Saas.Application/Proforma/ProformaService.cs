using System.Text.Json;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Application.Specifications;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Pricing;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Proforma;

/// <summary>
/// The proforma document core — creation with WPF number allocation,
/// lifecycle transitions, void, revision chain, and the Step 5 spec-group
/// grid.
///
/// Source of truth (read 2026-09-27):
///   • Numbering — ProformaInvoiceRepository.SuggestNextIdAsync/InsertAsync
///     (BUG-08 batch load, BUG-09 GUID fallback, V-1 overflow degradation,
///     5-attempt unique-violation retry) + ProformaInvoiceStatus
///     (IsUnallocatedSeed "PI-{year}-000"). Ported to Domain.
///     ProformaNumberSequence / ProformaStatuses.
///   • Lifecycle — InvoiceLifecycleStateMachine.cs, verbatim in Domain.
///     InvoiceLifecycle. Status (6-word list vocabulary) is the operator's
///     display field; lifecycle_state is the machine. The WPF only writes
///     status from the Step 1 picker and from VoidAsync
///     (DocumentLifecyclePolicy → "Cancelled"); this service keeps exactly
///     that behaviour.
///   • Void — VoidAsync: ProformaInvoice.Delete gate, idempotent, blank
///     reason → "(no reason given)", status → "Cancelled".
///   • Revision — SavedInvoicesListViewModel.CreateRevisionAsync: gates
///     (Draft no-op, voided/cancelled terminal), root chain,
///     "{rootNo}-R{n}" with "-{guard}" collision suffix, header+totals+
///     snapshot copied, new row is Draft.
///   • Step 5 — SpecGroupService (phantom rows, dimension/qty caps) +
///     GlassItemRow.RecalculateAll (dual measurement, perimeter factor 2,
///     Value = MoneyRounding(Sqm1 × Qty × Rate)) + Step 7 summary
///     (GlassMaterialsSubtotal = Σ row values).
///
/// Differences from WPF (documented in WPF_TO_SAAS_MAPPING §1.2a):
///   • Silent no-ops → typed 409s (REST + API §0).
///   • Revision root is a UUID FK, not the root's number string; the
///     displayed number keeps the WPF "{root}-R{n}" shape.
///   • VoidedBy is the acting user's id (WPF stored a username string).
///   • Lifecycle timestamps (sent_at/approved_at/confirmed_at) are stamped
///     by the machine — schema 0003 columns the WPF did not have.
/// </summary>
public sealed class ProformaService
{
    private const int MaxPageSize = 200;
    private const int DefaultPageSize = 50;

    /// <summary>WPF insert retry cap ("Could not allocate a free
    /// ProformaId after 5 retries").</summary>
    private const int NumberAttempts = 5;

    private readonly IProformaStore _store;
    private readonly IAuditWriter _audit;
    private readonly SpecificationsService _specifications;

    public ProformaService(
        IProformaStore store, IAuditWriter audit, SpecificationsService specifications)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _specifications = specifications
            ?? throw new ArgumentNullException(nameof(specifications));
    }

    // ═════════════════════════════ CREATE ═════════════════════════

    public async Task<ProformaResult<ProformaDto>> CreateDraftAsync(
        Guid tenantId, Guid actorId, string? ip, CreateProformaRequest? request,
        CancellationToken ct)
    {
        if (request is null)
        {
            return ProformaResult<ProformaDto>.Fail(
                ProformaErrorCode.ValidationFailed, "Request body is required.");
        }

        var status = ProformaStatuses.Draft;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            status = ProformaStatuses.Normalize(request.Status);
            if (!ProformaStatuses.All.Contains(status))
            {
                return ProformaResult<ProformaDto>.Fail(
                    ProformaErrorCode.ValidationFailed,
                    $"Unknown status '{request.Status}'. Valid: {string.Join(", ", ProformaStatuses.All)}.");
            }
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var invoice = new ProformaInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            // Wizard placeholder — reallocated below, WPF InsertAsync.
            ProformaNo = ProformaNumberSequence.SeedFor(today.Year),
            InquiryId = request.InquiryId,
            CompanyId = request.CompanyId,
            ClientId = request.ClientId,
            ProjectId = request.ProjectId,
            SalesmanId = request.SalesmanId,
            Status = status,
            StatusText = "",
            LifecycleState = nameof(InvoiceLifecycleState.Draft),
            IssueDate = request.IssueDate ?? today,          // Step 1: today…
            ValidUntil = request.ValidUntil ?? today.AddDays(30), // …+ 30 days
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = actorId,
            UpdatedBy = actorId,
            FurthestStep = 1,
        };

        // WPF InsertAsync: reallocate the unallocated seed, then retry the
        // insert with the next free id when a concurrent writer steals the
        // number (unique violation), capped at 5 attempts.
        var allocated = false;
        for (var attempt = 0; attempt < NumberAttempts; attempt++)
        {
            var existing = await _store.GetNumbersWithPrefixAsync(
                tenantId, "PI-", ct);
            invoice.ProformaNo = ProformaNumberSequence.SuggestNext(
                invoice.ProformaNo, existing);

            if (await _store.TryInsertAsync(invoice, ct))
            {
                allocated = true;
                break;
            }

            // The number raced away — the next SuggestNext runs FROM the
            // collided number, exactly like the WPF bump-and-retry.
        }

        if (!allocated)
        {
            return ProformaResult<ProformaDto>.Fail(
                ProformaErrorCode.NumberExhausted,
                $"Could not allocate a free proforma number after {NumberAttempts} " +
                "retries. Another concurrent writer is racing this insert.");
        }

        await _audit.WriteAsync(
            tenantId, actorId, "proforma.created", "proforma_invoices",
            invoice.Id, ip, null, ct,
            afterJson: JsonSerializer.Serialize(new { invoice.ProformaNo, status }));

        return ProformaResult<ProformaDto>.Ok(ToDto(invoice));
    }

    // ═════════════════════════════ READ ══════════════════════════

    public async Task<ProformaResult<ProformaDto>> GetAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var invoice = await _store.FindByIdAsync(tenantId, id, ct);
        return invoice is null
            ? ProformaResult<ProformaDto>.Fail(
                ProformaErrorCode.NotFound, $"Proforma invoice {id} was not found.")
            : ProformaResult<ProformaDto>.Ok(ToDto(invoice));
    }

    public async Task<Application.MasterData.PagedResult<ProformaDto>> ListAsync(
        Guid tenantId, int? page, int? pageSize, string? status, string? search,
        CancellationToken ct)
    {
        var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        var current = Math.Max(page ?? 1, 1);

        var filter = string.IsNullOrWhiteSpace(status) ? null : ProformaStatuses.Normalize(status);
        var (items, total) = await _store.ListAsync(
            tenantId, current, size, filter, search, ct);

        return new Application.MasterData.PagedResult<ProformaDto>(
            items.Select(ToDto).ToList(), current, size, total);
    }

    /// <summary>The root row plus every "-R{n}" revision, WPF order.</summary>
    public async Task<ProformaResult<IReadOnlyList<ProformaDto>>> GetRevisionChainAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var invoice = await _store.FindByIdAsync(tenantId, id, ct);
        if (invoice is null)
        {
            return ProformaResult<IReadOnlyList<ProformaDto>>.Fail(
                ProformaErrorCode.NotFound, $"Proforma invoice {id} was not found.");
        }

        var rootId = invoice.RootProformaId ?? invoice.Id;
        var chain = await _store.GetRevisionChainAsync(tenantId, rootId, ct);
        return ProformaResult<IReadOnlyList<ProformaDto>>.Ok(
            chain.Select(ToDto).ToList());
    }

    // ═════════════════════════════ TRANSITIONS ════════════════════

    public async Task<ProformaResult<ProformaDto>> TransitionAsync(
        Guid tenantId, Guid id, Guid actorId, string? ip,
        TransitionRequest? request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ToState))
        {
            return ProformaResult<ProformaDto>.Fail(
                ProformaErrorCode.ValidationFailed, "Target state is required.");
        }

        if (!InvoiceLifecycle.TryParseState(request.ToState, out var target))
        {
            return ProformaResult<ProformaDto>.Fail(
                ProformaErrorCode.ValidationFailed,
                $"Unknown lifecycle state '{request.ToState}'. Valid: " +
                $"{string.Join(", ", Enum.GetNames<InvoiceLifecycleState>())}.");
        }

        var invoice = await _store.FindByIdAsync(tenantId, id, ct);
        if (invoice is null)
        {
            return ProformaResult<ProformaDto>.Fail(
                ProformaErrorCode.NotFound, $"Proforma invoice {id} was not found.");
        }

        if (invoice.IsVoid)
        {
            return ProformaResult<ProformaDto>.Fail(
                ProformaErrorCode.Conflict,
                "A voided proforma invoice is terminal history — it cannot transition.");
        }

        if (!InvoiceLifecycle.TryParseState(invoice.LifecycleState, out var current))
        {
            return ProformaResult<ProformaDto>.Fail(
                ProformaErrorCode.Conflict,
                $"Stored lifecycle state '{invoice.LifecycleState}' is not recognized.");
        }

        if (!InvoiceLifecycle.CanTransition(current, target))
        {
            var valid = string.Join(", ",
                InvoiceLifecycle.GetValidTransitions(current));
            return ProformaResult<ProformaDto>.Fail(
                ProformaErrorCode.Conflict,
                $"Cannot transition from {current} to {target}. Valid transitions: {valid}.");
        }

        var before = invoice.LifecycleState;
        invoice.LifecycleState = target.ToString();

        // Schema 0003 lifecycle timestamps (the WPF had no such columns);
        // stamped by the machine on the corresponding transition.
        var now = DateTime.UtcNow;
        if (target == InvoiceLifecycleState.Approved) invoice.ApprovedAt ??= now;
        if (target == InvoiceLifecycleState.Sent) invoice.SentAt ??= now;
        if (target == InvoiceLifecycleState.Confirmed) invoice.ConfirmedAt ??= now;

        await _store.UpdateAsync(invoice, actorId, ct);

        await _audit.WriteAsync(
            tenantId, actorId, "proforma.lifecycle", "proforma_invoices",
            invoice.Id, ip, null, ct,
            beforeJson: JsonSerializer.Serialize(new { lifecycleState = before }),
            afterJson: JsonSerializer.Serialize(new
            {
                lifecycleState = invoice.LifecycleState,
                reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason,
                requiresApproval = InvoiceLifecycle.RequiresApproval(current, target),
            }));

        return ProformaResult<ProformaDto>.Ok(ToDto(invoice));
    }

    // ═════════════════════════════ VOID ══════════════════════════

    /// <summary>
    /// Soft-close (void) instead of a physical delete — the row, number,
    /// snapshot and audit trail are kept (WPF ERP lifecycle hardening,
    /// Phase 1). Idempotent: voiding an already-voided invoice is a no-op.
    /// </summary>
    public async Task<ProformaResult<ProformaDto>> VoidAsync(
        Guid tenantId, Guid id, Guid actorId, string? ip, string? reason,
        CancellationToken ct)
    {
        var invoice = await _store.FindByIdAsync(tenantId, id, ct);
        if (invoice is null)
        {
            return ProformaResult<ProformaDto>.Fail(
                ProformaErrorCode.NotFound, $"Proforma invoice {id} was not found.");
        }

        if (invoice.IsVoid)
        {
            return ProformaResult<ProformaDto>.Ok(ToDto(invoice)); // idempotent
        }

        invoice.IsVoid = true;
        invoice.VoidedAt = DateTime.UtcNow;
        invoice.VoidedBy = actorId;
        invoice.VoidReason = string.IsNullOrWhiteSpace(reason)
            ? "(no reason given)"
            : reason.Trim();
        // DocumentLifecyclePolicy.VoidStatus[ProformaInvoice] — "Cancelled".
        invoice.Status = ProformaStatuses.Cancelled;
        invoice.LifecycleState = nameof(InvoiceLifecycleState.Cancelled);

        await _store.UpdateAsync(invoice, actorId, ct);

        await _audit.WriteAsync(
            tenantId, actorId, "proforma.voided", "proforma_invoices",
            invoice.Id, ip, null, ct,
            afterJson: JsonSerializer.Serialize(new
            {
                invoice.VoidReason,
                invoice.Status,
            }));

        return ProformaResult<ProformaDto>.Ok(ToDto(invoice));
    }

    // ═════════════════════════════ REVISION ═══════════════════════

    /// <summary>
    /// NEW Draft row sharing the original's root ("PI-2026-001-R1"),
    /// leaving the original byte-identical for audit. The revision is a
    /// fresh Draft and re-enters the normal save → send → approve flow.
    /// </summary>
    public async Task<ProformaResult<ProformaDto>> CreateRevisionAsync(
        Guid tenantId, Guid id, Guid actorId, string? ip, CancellationToken ct)
    {
        var source = await _store.FindByIdAsync(tenantId, id, ct);
        if (source is null)
        {
            return ProformaResult<ProformaDto>.Fail(
                ProformaErrorCode.NotFound, $"Proforma invoice {id} was not found.");
        }

        // Policy gates (WPF): drafts are already editable; voided /
        // cancelled documents are terminal history. The WPF returned
        // silently; the API answers 409 (documented difference).
        if (ProformaStatuses.Normalize(source.Status) == ProformaStatuses.Draft
            && !source.IsVoid)
        {
            return ProformaResult<ProformaDto>.Fail(
                ProformaErrorCode.Conflict,
                "A draft is already editable — edit it instead of revising it.");
        }

        if (source.IsVoid
            || ProformaStatuses.Normalize(source.Status) == ProformaStatuses.Cancelled)
        {
            return ProformaResult<ProformaDto>.Fail(
                ProformaErrorCode.Conflict,
                "A voided or cancelled proforma invoice is terminal history — " +
                "it cannot be revised.");
        }

        var rootId = source.RootProformaId ?? source.Id;

        // The chain INCLUDES the root (revision 0), so max + 1 is the next
        // revision — the WPF's GetRevisionChainAsync contract.
        var chain = await _store.GetRevisionChainAsync(tenantId, rootId, ct);
        var nextRev = chain.Count == 0 ? 1 : chain.Max(x => x.RevisionNumber) + 1;

        // WPF: "{root}-R{n}", with a "-{guard}" suffix on pathological
        // collisions (≤ 5 guards).
        var rootNo = await RootNumberAsync(tenantId, rootId, ct);
        var revisionNo = $"{rootNo}-R{nextRev}";
        for (var guard = 1; guard <= 5; guard++)
        {
            if (!await _store.ExistsByNumberAsync(tenantId, revisionNo, ct))
            {
                break;
            }

            revisionNo = $"{rootNo}-R{nextRev}-{guard}";
        }

        var now = DateTime.UtcNow;
        var revision = new ProformaInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProformaNo = revisionNo,
            RootProformaId = rootId,
            RevisionNumber = nextRev,
            InquiryId = source.InquiryId,
            CompanyId = source.CompanyId,
            ClientId = source.ClientId,
            ProjectId = source.ProjectId,
            SalesmanId = source.SalesmanId,
            Status = ProformaStatuses.Draft,
            StatusText = "",
            LifecycleState = nameof(InvoiceLifecycleState.Draft),
            IssueDate = source.IssueDate,
            ValidUntil = source.ValidUntil,
            GlassMaterialsSubtotal = source.GlassMaterialsSubtotal,
            OtherChargesSubtotal = source.OtherChargesSubtotal,
            DiscountAmount = source.DiscountAmount,
            VatAmount = source.VatAmount,
            GrandTotalVatInclusive = source.GrandTotalVatInclusive,
            SnapshotJson = source.SnapshotJson,
            FurthestStep = source.FurthestStep,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId,
        };

        if (!await _store.TryInsertAsync(revision, ct))
        {
            return ProformaResult<ProformaDto>.Fail(
                ProformaErrorCode.NumberExhausted,
                $"Revision number '{revisionNo}' was taken by a concurrent writer. " +
                "Retry the request.");
        }

        await _audit.WriteAsync(
            tenantId, actorId, "proforma.revision_created", "proforma_invoices",
            revision.Id, ip, null, ct,
            afterJson: JsonSerializer.Serialize(
                new { revision.ProformaNo, rootId, revision.RevisionNumber }));

        return ProformaResult<ProformaDto>.Ok(ToDto(revision));
    }

    private async Task<string> RootNumberAsync(Guid tenantId, Guid rootId,
        CancellationToken ct)
    {
        var root = await _store.FindByIdAsync(tenantId, rootId, ct);
        return root?.ProformaNo ?? "PI";
    }

    // ═════════════════════════════ STEP 5 GRID ════════════════════

    /// <summary>
    /// Replace the invoice's whole Step 5 grid. Only Draft/Rejected bodies
    /// are editable (WPF IsEditable + "Draft bodies are editable");
    /// phantom rows are dropped; every kept row is validated (dims > 0,
    /// ≤ 10000 mm; qty > 0, ≤ 10000) and priced by the WPF row math; the
    /// invoice's glass subtotal becomes Σ row values (Step 7 summary).
    /// </summary>
    public async Task<ProformaResult<IReadOnlyList<SpecGroupDto>>> SaveSpecGroupsAsync(
        Guid tenantId, Guid id, Guid actorId, string? ip,
        SaveSpecGroupsRequest? request, CancellationToken ct)
    {
        if (request is null)
        {
            return ProformaResult<IReadOnlyList<SpecGroupDto>>.Fail(
                ProformaErrorCode.ValidationFailed, "Request body is required.");
        }

        var invoice = await _store.FindByIdAsync(tenantId, id, ct);
        if (invoice is null)
        {
            return ProformaResult<IReadOnlyList<SpecGroupDto>>.Fail(
                ProformaErrorCode.NotFound, $"Proforma invoice {id} was not found.");
        }

        if (invoice.IsVoid)
        {
            return ProformaResult<IReadOnlyList<SpecGroupDto>>.Fail(
                ProformaErrorCode.Conflict,
                "A voided proforma invoice is terminal history — it cannot be edited.");
        }

        if (!InvoiceLifecycle.TryParseState(invoice.LifecycleState, out var current)
            || !InvoiceLifecycle.IsEditable(current))
        {
            return ProformaResult<IReadOnlyList<SpecGroupDto>>.Fail(
                ProformaErrorCode.Conflict,
                $"The Step 5 grid is only editable while the invoice is Draft or " +
                $"Rejected (currently {invoice.LifecycleState}).");
        }

        var error = ValidateGroups(request.Groups);
        if (error is not null)
        {
            return ProformaResult<IReadOnlyList<SpecGroupDto>>.Fail(
                ProformaErrorCode.ValidationFailed, error);
        }

        // A referenced registry spec must exist (the FK would otherwise
        // fail at the database with a 500).
        foreach (var g in request.Groups)
        {
            if (g.SpecId is null)
            {
                continue;
            }

            var spec = await _specifications.GetAsync(tenantId, g.SpecId.Value, ct);
            if (!spec.Success)
            {
                return ProformaResult<IReadOnlyList<SpecGroupDto>>.Fail(
                    ProformaErrorCode.ValidationFailed,
                    $"Group {g.GroupNumber}: specification {g.SpecId} was not found.");
            }
        }

        var groups = new List<SpecGroupWithItems>();
        var rowValues = new List<decimal>();

        foreach (var g in request.Groups)
        {
            var group = new SpecGroup
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProformaId = invoice.Id,
                GroupNumber = g.GroupNumber,
                GlassType = g.GlassType.Trim(),
                SpecName = g.SpecName?.Trim() ?? "",
                SpecId = g.SpecId,
                RatePerSqm = g.RatePerSqm,
                PriceSource = (g.PriceSource ?? "").Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };

            var items = new List<Domain.Entities.SpecGroupItem>();
            foreach (var item in g.Items)
            {
                // Phantom rows are allowed by validation but never stored
                // (WPF: "allowed, will be filtered"). A phantom is the
                // blank row the grid keeps for the next entry: no position
                // and every dimension zero. WPF IsPhantomRow also treats
                // qty ≤ 0 as phantom — validation already refuses those
                // for real rows, so the drop below is belt-and-braces.
                if (IsPhantom(item) || item.Qty <= 0)
                {
                    continue;
                }

                var math = SpecGroupRowMath.Calculate(
                    item.W1, item.H1, item.W2, item.H2, item.Qty,
                    item.PerimeterFactor ?? SpecGroupRowMath.DefaultPerimeterFactor,
                    g.RatePerSqm);

                items.Add(new Domain.Entities.SpecGroupItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SpecGroupId = group.Id,
                    RowSerial = item.RowSerial,
                    Position = item.Position?.Trim() ?? "",
                    WidthMm = item.W1,
                    HeightMm = item.H1,
                    W1 = item.W1,
                    H1 = item.H1,
                    W2 = item.W2,
                    H2 = item.H2,
                    Qty = item.Qty,
                    Sqm = math.Sqm1,
                    TotalSqm = math.TotalSqm,
                    UnitPrice = g.RatePerSqm,
                    Total = math.Value,
                    Notes = item.Notes?.Trim() ?? "",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = actorId,
                    UpdatedBy = actorId,
                });

                rowValues.Add(math.Value);
            }

            groups.Add(new SpecGroupWithItems(group, items));
        }

        // Step 7 summary: GlassMaterialsSubtotal = the grid's grand total.
        invoice.GlassMaterialsSubtotal =
            SpecGroupAggregation.GlassMaterialsSubtotal(rowValues);

        await _store.ReplaceSpecGroupsAsync(invoice, groups, actorId, ct);

        await _audit.WriteAsync(
            tenantId, actorId, "proforma.spec_groups_saved", "spec_groups",
            invoice.Id, ip, null, ct,
            afterJson: JsonSerializer.Serialize(new
            {
                groups = groups.Count,
                rows = groups.Sum(g => g.Items.Count),
                invoice.GlassMaterialsSubtotal,
            }));

        // LEDGER: derive verified usage from the just-saved groups. The
        // WPF ran this debounced after every invoice write and it NEVER
        // THROWS — a ledger failure is logged, never a save failure.
        try
        {
            await _specifications.ReconcileAsync(tenantId, actorId, ip, ct);
        }
        catch (Exception ex)
        {
            await _audit.WriteAsync(
                tenantId, actorId, "specifications.reconcile_failed",
                "specifications", null, ip, null, ct,
                afterJson: JsonSerializer.Serialize(new { error = ex.Message }));
        }

        var saved = await _store.GetSpecGroupsAsync(tenantId, invoice.Id, ct);
        return ProformaResult<IReadOnlyList<SpecGroupDto>>.Ok(
            saved.Select(ToDto).ToList());
    }

    public async Task<ProformaResult<IReadOnlyList<SpecGroupDto>>> GetSpecGroupsAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var invoice = await _store.FindByIdAsync(tenantId, id, ct);
        if (invoice is null)
        {
            return ProformaResult<IReadOnlyList<SpecGroupDto>>.Fail(
                ProformaErrorCode.NotFound, $"Proforma invoice {id} was not found.");
        }

        var groups = await _store.GetSpecGroupsAsync(tenantId, id, ct);
        return ProformaResult<IReadOnlyList<SpecGroupDto>>.Ok(
            groups.Select(ToDto).ToList());
    }

    /// <summary>A phantom row: no position and every dimension zero
    /// (WPF: blank glass ref + w == 0 && h == 0, extended to both
    /// measurements because the SaaS row carries two of them).</summary>
    private static bool IsPhantom(SpecGroupItemRequest item) =>
        string.IsNullOrWhiteSpace(item.Position)
        && item.W1 == 0 && item.H1 == 0
        && item.W2 == 0 && item.H2 == 0;

    private static string? ValidateGroups(IReadOnlyList<SpecGroupRequest> groups)
    {
        if (groups is null || groups.Count == 0)
        {
            // An empty grid is a legal save (cleared back to nothing).
            return null;
        }

        var seenNumbers = new HashSet<int>();
        foreach (var g in groups)
        {
            if (!GlassTypes.All.Contains((g.GlassType ?? "").Trim()))
            {
                return $"Group {g.GroupNumber}: unknown glass type '{g.GlassType}'. " +
                    $"Valid: {string.Join(", ", GlassTypes.All)}.";
            }

            if (g.GroupNumber < 1)
            {
                return "Group numbers must be 1 or greater.";
            }

            if (!seenNumbers.Add(g.GroupNumber))
            {
                return $"Group number {g.GroupNumber} appears twice.";
            }

            if (g.RatePerSqm < 0)
            {
                return $"Group {g.GroupNumber}: rate per SQM cannot be negative.";
            }

            var source = (g.PriceSource ?? "").Trim();
            if (source is not ("formula" or "manual" or "target"))
            {
                return $"Group {g.GroupNumber}: price source must be formula, " +
                    "manual or target.";
            }

            if (g.Items is null)
            {
                return $"Group {g.GroupNumber}: items are required (may be empty).";
            }

            foreach (var item in g.Items)
            {
                // Phantom: blank position + all dimensions zero (WPF rule).
                // Allowed and dropped before the save.
                if (IsPhantom(item))
                {
                    continue;
                }

                var message = SpecGroupValidation.ValidateRowMessage(
                    item.W1, item.H1, item.Qty, item.Position ?? "");
                if (message is not null)
                {
                    return $"Group {g.GroupNumber}: {message}";
                }

                // Secondary measurement is optional but, when present, sane.
                if ((item.W2 != 0 || item.H2 != 0)
                    && (item.W2 <= 0 || item.H2 <= 0
                        || item.W2 > SpecGroupValidation.MaxDimensionMm
                        || item.H2 > SpecGroupValidation.MaxDimensionMm))
                {
                    return $"Group {g.GroupNumber}: W2/H2 must be both zero or " +
                        $"both between 1 and {SpecGroupValidation.MaxDimensionMm}mm.";
                }

                if (item.PerimeterFactor is < 0 or > 10)
                {
                    return $"Group {g.GroupNumber}: perimeter factor must be 0–10.";
                }
            }
        }

        return null;
    }

    // ═════════════════════════════ MAPPING ════════════════════════

    private static ProformaDto ToDto(ProformaInvoice invoice)
    {
        InvoiceLifecycle.TryParseState(invoice.LifecycleState, out var state);
        return new ProformaDto(
            invoice.Id, invoice.ProformaNo,
            invoice.RootProformaId, invoice.RevisionNumber,
            invoice.InquiryId, invoice.CompanyId, invoice.ClientId,
            invoice.ProjectId, invoice.SalesmanId,
            invoice.Status, invoice.LifecycleState,
            invoice.IssueDate, invoice.ValidUntil,
            invoice.GlassMaterialsSubtotal, invoice.OtherChargesSubtotal,
            invoice.DiscountAmount, invoice.VatAmount,
            invoice.GrandTotalVatInclusive,
            invoice.IsVoid, invoice.VoidedAt, invoice.VoidedBy, invoice.VoidReason,
            invoice.FurthestStep, invoice.RowVersion,
            invoice.SentAt, invoice.ApprovedAt, invoice.ConfirmedAt,
            invoice.CreatedAt, invoice.UpdatedAt,
            InvoiceLifecycle.IsEditable(state) && !invoice.IsVoid,
            InvoiceLifecycle.GetValidTransitions(state)
                .Select(s => s.ToString()).ToList());
    }

    private static SpecGroupDto ToDto(SpecGroupWithItems group) => new(
        group.Group.Id, group.Group.GroupNumber, group.Group.GlassType,
        group.Group.SpecName, group.Group.RatePerSqm, group.Group.PriceSource,
        group.Items.Sum(i => i.Qty),
        Math.Round(group.Items.Sum(i => i.TotalSqm), 3),
        group.Items.Select(ToDto).ToList());

    private static SpecGroupItemDto ToDto(Domain.Entities.SpecGroupItem item)
    {
        var math = SpecGroupRowMath.Calculate(
            item.W1, item.H1, item.W2, item.H2, item.Qty,
            SpecGroupRowMath.DefaultPerimeterFactor, item.UnitPrice);
        return new SpecGroupItemDto(
            item.Id, item.RowSerial, item.Position,
            item.W1, item.H1, item.W2, item.H2, item.Qty,
            item.Sqm, item.TotalSqm,
            math.Sqm2, math.TotalSqm2,   // derived on read (W2/H2)
            math.Lm1, math.Lm2,
            item.UnitPrice, item.Total,
            item.Notes);
    }
}
