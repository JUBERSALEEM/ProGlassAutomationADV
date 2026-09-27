using System.Text.Json;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Application.Specifications;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Pricing;
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

        // WPF InquiryLinkSync.AfterUpdateAsync — the linked inquiry
        // follows the PI lifecycle (fire-and-forget, never fails this op).
        await SyncLinkedInquiryAsync(tenantId, invoice, target, actorId, ip, ct);

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

        // A voided PI is cancelled — the linked inquiry follows
        // (InquiryLinkSync.AfterUpdateAsync, fire-and-forget).
        await SyncLinkedInquiryAsync(
            tenantId, invoice, InvoiceLifecycleState.Cancelled, actorId, ip, ct);

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
                // Step 4 provenance (SpecGroupSnapshot Phase G): the
                // invoice re-costs against the sheets it was QUOTED on.
                OuterGlassColor = g.OuterGlassColor?.Trim() ?? "",
                InnerGlassColor = g.InnerGlassColor?.Trim() ?? "",
                ThirdGlassColor = g.ThirdGlassColor?.Trim() ?? "",
                OuterSheetWidthMm = g.OuterSheetWidthMm ?? 0,
                OuterSheetHeightMm = g.OuterSheetHeightMm ?? 0,
                InnerSheetWidthMm = g.InnerSheetWidthMm ?? 0,
                InnerSheetHeightMm = g.InnerSheetHeightMm ?? 0,
                ThirdSheetWidthMm = g.ThirdSheetWidthMm ?? 0,
                ThirdSheetHeightMm = g.ThirdSheetHeightMm ?? 0,
                OuterSheetPricePerSqm = g.OuterSheetPricePerSqm ?? 0,
                InnerSheetPricePerSqm = g.InnerSheetPricePerSqm ?? 0,
                ThirdSheetPricePerSqm = g.ThirdSheetPricePerSqm ?? 0,
                Step4SpacerPricePerSqm = g.Step4SpacerPricePerSqm,
                Step4InterlayerCostPerSqm = g.Step4InterlayerCostPerSqm,
                Step4CuttingCostPerSqm = g.Step4CuttingCostPerSqm,
                Step4TemperingCostPerSqm = g.Step4TemperingCostPerSqm,
                UInsertPricePerSqm = g.UInsertPricePerSqm,
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
                    PerimeterFactor = item.PerimeterFactor
                        ?? SpecGroupRowMath.DefaultPerimeterFactor,
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


    // ═════════════════════════════ STEP 6 ══════════════════════════

    /// <summary>
    /// Replace the invoice's whole Step 6 section (predefined charges +
    /// wastages + custom charges) in one transaction, WPF-verbatim:
    /// every metric qty and amount is RECOMPUTED here from the SAVED
    /// Step 5 grid (MetricHub → SpecTargetSelection → row.Recalculate —
    /// never trusted from the client), the wastage rate in optimization
    /// mode is the derived 85%-baseline gap, and the subtotal lands on
    /// the invoice (RecalculateTotal).
    /// </summary>
    public async Task<ProformaResult<Step6Dto>> SaveStep6Async(
        Guid tenantId, Guid id, Guid actorId, string? ip,
        SaveStep6Request? request, CancellationToken ct)
    {
        if (request is null)
        {
            return ProformaResult<Step6Dto>.Fail(
                ProformaErrorCode.ValidationFailed, "Request body is required.");
        }

        var invoice = await _store.FindByIdAsync(tenantId, id, ct);
        if (invoice is null)
        {
            return ProformaResult<Step6Dto>.Fail(
                ProformaErrorCode.NotFound, $"Proforma invoice {id} was not found.");
        }

        if (invoice.IsVoid)
        {
            return ProformaResult<Step6Dto>.Fail(
                ProformaErrorCode.Conflict,
                "A voided proforma invoice is terminal history — it cannot be edited.");
        }

        if (!InvoiceLifecycle.TryParseState(invoice.LifecycleState, out var current)
            || !InvoiceLifecycle.IsEditable(current))
        {
            return ProformaResult<Step6Dto>.Fail(
                ProformaErrorCode.Conflict,
                $"Step 6 is only editable while the invoice is Draft or " +
                $"Rejected (currently {invoice.LifecycleState}).");
        }

        var inputs = await LoadSpecGroupInputsAsync(tenantId, id, ct);
        var groupNumbers = inputs.Select(g => g.GroupNumber).ToHashSet();
        var availableSourceKeys = inputs
            .SelectMany(ChargeMetrics.SourcesForGroup)
            .Select(source => source.Key)
            .ToHashSet(StringComparer.Ordinal);

        var error = ValidateStep6(request, inputs, groupNumbers, availableSourceKeys);
        if (error is not null)
        {
            return ProformaResult<Step6Dto>.Fail(ProformaErrorCode.ValidationFailed, error);
        }

        var charges = new List<Domain.Entities.OtherCharge>();
        var wastages = new List<Domain.Entities.WastageRow>();
        var customs = new List<Domain.Entities.CustomCharge>();
        var sort = 0;

        // ── predefined rows: qty derived, amount = Round(qty × rate) ──
        foreach (var row in request.Charges ?? Array.Empty<OtherChargeRowRequest>())
        {
            var templateKey = (row.TemplateKey ?? "").Trim();
            var isDelivery = templateKey == ChargeTemplates.DeliveryKey;
            var isPolish = templateKey is ChargeTemplates.PolishKey
                or ChargeTemplates.MatchedLamPolishKey;
            var polishSubType = (row.PolishSubType ?? "").Trim();
            var checkedSpecs = (row.CheckedSpecGroupNumbers ?? Array.Empty<int>()).ToList();

            decimal metricQty;
            if (isDelivery)
            {
                // Chat 7 Bug 4c: delivery is per-invoice — 1 TRIP, the
                // target selection is ignored (multi-trip = custom charges).
                metricQty = 1m;
            }
            else if (isPolish && polishSubType == PolishSubTypes.Custom)
            {
                metricQty = row.CustomPolishQty ?? 0m;
            }
            else if (isPolish)
            {
                metricQty = ChargeMetrics.PolishLm(
                    inputs, checkedSpecs, polishSubType,
                    matchedSameGlassOnly: templateKey == ChargeTemplates.MatchedLamPolishKey);
            }
            else
            {
                metricQty = ChargeMetrics.TargetedMetric(
                    inputs, checkedSpecs, (row.MetricKey ?? "").Trim());
            }

            var amount = ChargeMetrics.ChargeAmount(metricQty, row.Rate, row.IsActive);

            charges.Add(new Domain.Entities.OtherCharge
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProformaId = invoice.Id,
                ChargeName = (row.ChargeName ?? "").Trim(),
                Description = (row.Description ?? "").Trim(),
                TemplateKey = templateKey,
                Unit = ParseUnit(row.Unit) ?? "Qty",
                MetricKey = MetricKeys.Normalize(row.MetricKey),
                CheckedSpecGroupNumbers = JsonSerializer.Serialize(checkedSpecs),
                Rate = row.Rate,
                MetricQty = metricQty,
                Amount = amount,
                IsActive = row.IsActive,
                PolishSubType = polishSubType,
                CustomPolishDescription = (row.CustomPolishDescription ?? "").Trim(),
                CustomPolishQty = row.CustomPolishQty ?? 0m,
                SortOrder = sort++,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            });
        }

        // ── wastage rows: SQM from the checked pane sources ───────────
        sort = 0;
        foreach (var row in request.Wastages ?? Array.Empty<WastageRowRequest>())
        {
            var checkedKeys = (row.CheckedSourceKeys ?? Array.Empty<string>())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim()).ToList();
            var totalSqm = ChargeMetrics.WastageSqm(inputs, checkedKeys);

            decimal rate;
            decimal amount;
            if (row.UseOptimizationFormula)
            {
                var result = WastageFormula.CalculateOptimization(
                    row.FormulaPricePerSqm, row.OptimizationPercent, totalSqm);
                rate = result.BaseResultValue; // derived — never client-owned
                amount = row.IsActive ? result.ResultAmount : 0m;
            }
            else
            {
                rate = row.Rate;
                amount = row.IsActive
                    ? WastageFormula.CalculateManual(totalSqm, row.Rate) : 0m;
            }

            wastages.Add(new Domain.Entities.WastageRow
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProformaId = invoice.Id,
                ColorName = (row.ColorName ?? "").Trim(),
                CheckedSourceKeys = JsonSerializer.Serialize(checkedKeys),
                TotalSqm = totalSqm,
                IsManuallyAdded = row.IsManuallyAdded,
                UseOptimizationFormula = row.UseOptimizationFormula,
                FormulaPricePerSqm = row.FormulaPricePerSqm,
                OptimizationPercent = row.OptimizationPercent,
                SheetQuantity = row.SheetQuantity < 1 ? 1 : row.SheetQuantity,
                Rate = rate,
                ResultAmount = amount,
                IsActive = row.IsActive,
                SortOrder = sort++,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            });
        }

        // ── custom rows ───────────────────────────────────────────────
        sort = 0;
        foreach (var row in request.Customs ?? Array.Empty<CustomChargeRowRequest>())
        {
            customs.Add(new Domain.Entities.CustomCharge
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProformaId = invoice.Id,
                ChargeName = (row.ChargeName ?? "").Trim(),
                Qty = row.Qty,
                Unit = ParseUnit(row.Unit) ?? "Qty",
                Rate = row.Rate,
                Amount = ChargeMetrics.ChargeAmount(row.Qty, row.Rate, row.IsActive),
                IsActive = row.IsActive,
                SortOrder = sort++,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            });
        }

        // RecalculateTotal: Σ active amounts, plain Round(x, 2) — the
        // WPF's banker's rounding on this one line, preserved verbatim.
        invoice.OtherChargesSubtotal = ChargeMetrics.OtherChargesSubtotal(
            charges.Sum(c => c.Amount),
            wastages.Sum(w => w.ResultAmount),
            customs.Sum(c => c.Amount));

        await _store.ReplaceStep6Async(invoice, charges, wastages, customs, actorId, ct);

        await _audit.WriteAsync(
            tenantId, actorId, "proforma.step6_saved", "proforma_invoices",
            invoice.Id, ip, null, ct,
            afterJson: JsonSerializer.Serialize(new
            {
                charges = charges.Count,
                wastages = wastages.Count,
                customs = customs.Count,
                invoice.OtherChargesSubtotal,
            }));

        return await GetStep6SectionAsync(tenantId, invoice.Id, ct);
    }

    /// <summary>GET — the saved section + the derived read model
    /// (per-spec metrics strip, available wastage sources, subtotal).</summary>
    public async Task<ProformaResult<Step6Dto>> GetStep6Async(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var invoice = await _store.FindByIdAsync(tenantId, id, ct);
        if (invoice is null)
        {
            return ProformaResult<Step6Dto>.Fail(
                ProformaErrorCode.NotFound, $"Proforma invoice {id} was not found.");
        }

        return await GetStep6SectionAsync(tenantId, id, ct);
    }

    private async Task<ProformaResult<Step6Dto>> GetStep6SectionAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var rows = await _store.GetStep6Async(tenantId, id, ct);
        var inputs = await LoadSpecGroupInputsAsync(tenantId, id, ct);

        var metrics = inputs
            .Select(g => (Input: g, Metrics: ChargeMetrics.ForGroup(g)))
            .ToList();

        var dto = new Step6Dto(
            rows.Charges.Select(ToDto).ToList(),
            rows.Wastages.Select(ToDto).ToList(),
            rows.Customs.Select(ToDto).ToList(),
            ChargeMetrics.OtherChargesSubtotal(
                rows.Charges.Sum(c => c.Amount),
                rows.Wastages.Sum(w => w.ResultAmount),
                rows.Customs.Sum(c => c.Amount)),
            metrics.Select(m => new SpecGroupMetricsDto(
                m.Input.GroupNumber, m.Input.EffectiveGlassType, m.Input.SpecName,
                m.Metrics.IsDoubleGlass, m.Metrics.PaneFactor,
                m.Metrics.IsMatchedSameGlass,
                m.Metrics.Lm1, m.Metrics.Lm2,
                m.Metrics.Sqm1, m.Metrics.Sqm2, m.Metrics.Qty)).ToList(),
            inputs
                .SelectMany(ChargeMetrics.SourcesForGroup)
                .Select(source => new WastageSourceDto(
                    source.Key, source.SpecNumber, source.SideLabel,
                    source.ColorName, source.SqmValue,
                    source.SheetWidthMm, source.SheetHeightMm,
                    source.PricePerSqm)).ToList());

        return ProformaResult<Step6Dto>.Ok(dto);
    }

    /// <summary>The Step 6 math's view of the saved grid (rows + Step 4
    /// provenance), shared by save and read.</summary>
    private async Task<IReadOnlyList<SpecGroupInput>> LoadSpecGroupInputsAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        var groups = await _store.GetSpecGroupsAsync(tenantId, proformaId, ct);
        return groups.Select(g => new SpecGroupInput(
            g.Group.GroupNumber,
            g.Group.GlassType,
            g.Group.SpecName,
            g.Group.OuterGlassColor,
            g.Group.InnerGlassColor,
            g.Items.Select(i => new GridRow(
                i.W1, i.H1, i.W2, i.H2, i.Qty, i.PerimeterFactor)).ToList(),
            g.Group.OuterSheetWidthMm, g.Group.OuterSheetHeightMm,
            g.Group.InnerSheetWidthMm, g.Group.InnerSheetHeightMm,
            g.Group.ThirdSheetWidthMm, g.Group.ThirdSheetHeightMm,
            g.Group.OuterSheetPricePerSqm, g.Group.InnerSheetPricePerSqm,
            g.Group.ThirdSheetPricePerSqm,
            g.Group.Step4SpacerPricePerSqm,
            g.Group.Step4InterlayerCostPerSqm,
            g.Group.Step4CuttingCostPerSqm,
            g.Group.Step4TemperingCostPerSqm,
            g.Group.UInsertPricePerSqm)).ToList();
    }

    // ═════════════════════════ DUPLICATE ═══════════════════════════

    /// <summary>
    /// POST /{id}/duplicate — port of
    /// SavedInvoicesListViewModel.DuplicateAsync: a fresh DRAFT copy of
    /// the whole document (the WPF copied the wizard state via
    /// FullSnapshotJson; the SaaS normalized tables are that snapshot),
    /// with the inquiry link deliberately DETACHED (a duplicate is a
    /// fresh quotation, not another PI for the same inquiry — senior
    /// audit fix Aug 2026) and the validity window restarted at +7 days.
    /// </summary>
    public async Task<ProformaResult<ProformaDto>> DuplicateAsync(
        Guid tenantId, Guid id, Guid actorId, string? ip, CancellationToken ct)
    {
        var original = await _store.FindByIdAsync(tenantId, id, ct);
        if (original is null)
        {
            return ProformaResult<ProformaDto>.Fail(
                ProformaErrorCode.NotFound, $"Proforma invoice {id} was not found.");
        }

        // The WPF refuses the duplicate when the snapshot cannot be
        // scrubbed — never insert the original JSON (it re-links the
        // copy to the source inquiry and bypasses the convert guard).
        if (!string.IsNullOrEmpty(original.SnapshotJson)
            && DuplicateSnapshotScrub.TryDetach(original.SnapshotJson, "probe") is null)
        {
            return ProformaResult<ProformaDto>.Fail(
                ProformaErrorCode.Conflict,
                "Duplicate was not created because the snapshot could not be " +
                "detached from the source inquiry.");
        }

        var groups = await _store.GetSpecGroupsAsync(tenantId, id, ct);
        var step6 = await _store.GetStep6Async(tenantId, id, ct);
        var pnlRows = await _store.GetStep9Async(tenantId, id, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;
        var copyId = Guid.NewGuid();

        var copy = new ProformaInvoice
        {
            Id = copyId,
            TenantId = tenantId,
            ProformaNo = original.ProformaNo, // reallocated below
            // InquiryId deliberately NOT copied (the scrub rule).
            CompanyId = original.CompanyId,
            ClientId = original.ClientId,
            ProjectId = original.ProjectId,
            SalesmanId = original.SalesmanId,
            Status = ProformaStatuses.Draft,
            StatusText = "",
            LifecycleState = nameof(InvoiceLifecycleState.Draft),
            IssueDate = today,
            ValidUntil = today.AddDays(7), // the WPF duplicate's window
            GlassMaterialsSubtotal = original.GlassMaterialsSubtotal,
            OtherChargesSubtotal = original.OtherChargesSubtotal,
            DiscountAmount = original.DiscountAmount,
            VatAmount = original.VatAmount,
            GrandTotalVatInclusive = original.GrandTotalVatInclusive,
            // The normalized equivalent of the snapshot's Step 7/Step 9
            // blocks (the WPF copy carried them inside FullSnapshotJson).
            DiscountType = original.DiscountType,
            DiscountValue = original.DiscountValue,
            IsVatApplicable = original.IsVatApplicable,
            VatPercent = original.VatPercent,
            PnlWastagePercent = original.PnlWastagePercent,
            PnlCostAtSellingPrice = original.PnlCostAtSellingPrice,
            PnlGrossVatInclusive = original.PnlGrossVatInclusive,
            PnlNetExVat = original.PnlNetExVat,
            PnlTotalGlassCost = original.PnlTotalGlassCost,
            PnlTotalProductionCost = original.PnlTotalProductionCost,
            PnlProfit = original.PnlProfit,
            PnlMarginPercent = original.PnlMarginPercent,
            PnlOverallMarginPercent = original.PnlOverallMarginPercent,
            PnlNetMarginPercent = original.PnlNetMarginPercent,
            PnlUInsertCost = original.PnlUInsertCost,
            PnlBasisCost = original.PnlBasisCost,
            PnlCalculatedAt = original.PnlCalculatedAt,
            FurthestStep = original.FurthestStep,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId,
        };

        var groupCopies = groups.Select(g =>
        {
            var groupId = Guid.NewGuid();
            return new SpecGroupWithItems(
                new SpecGroup
                {
                    Id = groupId,
                    TenantId = tenantId,
                    ProformaId = copyId,
                    GroupNumber = g.Group.GroupNumber,
                    GlassType = g.Group.GlassType,
                    SpecName = g.Group.SpecName,
                    SpecId = g.Group.SpecId,
                    RatePerSqm = g.Group.RatePerSqm,
                    PriceSource = g.Group.PriceSource,
                    OuterGlassColor = g.Group.OuterGlassColor,
                    InnerGlassColor = g.Group.InnerGlassColor,
                    ThirdGlassColor = g.Group.ThirdGlassColor,
                    OuterSheetWidthMm = g.Group.OuterSheetWidthMm,
                    OuterSheetHeightMm = g.Group.OuterSheetHeightMm,
                    InnerSheetWidthMm = g.Group.InnerSheetWidthMm,
                    InnerSheetHeightMm = g.Group.InnerSheetHeightMm,
                    ThirdSheetWidthMm = g.Group.ThirdSheetWidthMm,
                    ThirdSheetHeightMm = g.Group.ThirdSheetHeightMm,
                    OuterSheetPricePerSqm = g.Group.OuterSheetPricePerSqm,
                    InnerSheetPricePerSqm = g.Group.InnerSheetPricePerSqm,
                    ThirdSheetPricePerSqm = g.Group.ThirdSheetPricePerSqm,
                    Step4SpacerPricePerSqm = g.Group.Step4SpacerPricePerSqm,
                    Step4InterlayerCostPerSqm = g.Group.Step4InterlayerCostPerSqm,
                    Step4CuttingCostPerSqm = g.Group.Step4CuttingCostPerSqm,
                    Step4TemperingCostPerSqm = g.Group.Step4TemperingCostPerSqm,
                    UInsertPricePerSqm = g.Group.UInsertPricePerSqm,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = actorId,
                    UpdatedBy = actorId,
                },
                g.Items.Select(i => new SpecGroupItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SpecGroupId = groupId,
                    RowSerial = i.RowSerial,
                    Position = i.Position,
                    W1 = i.W1, H1 = i.H1, W2 = i.W2, H2 = i.H2,
                    Qty = i.Qty,
                    PerimeterFactor = i.PerimeterFactor,
                    Sqm1 = i.Sqm1,
                    TotalSqm = i.TotalSqm,
                    Notes = i.Notes,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = actorId,
                    UpdatedBy = actorId,
                }).ToList());
        }).ToList();

        var chargeCopies = step6.Charges.Select(c => CopyRow(c, copyId, tenantId, actorId, now)).ToList();
        var wastageCopies = step6.Wastages.Select(w => CopyRow(w, copyId, tenantId, actorId, now)).ToList();
        var customCopies = step6.Customs.Select(c => CopyRow(c, copyId, tenantId, actorId, now)).ToList();
        var pnlCopies = pnlRows.Select(r => CopyRow(r, copyId, tenantId, actorId, now)).ToList();

        // WPF bump-and-retry: suggest the next free id FROM the original's
        // number (PI-2026-0012 → PI-2026-0013), re-suggesting from the
        // collided number when a concurrent writer steals it, capped at 5.
        var allocated = false;
        for (var attempt = 0; attempt < NumberAttempts; attempt++)
        {
            var existing = await _store.GetNumbersWithPrefixAsync(tenantId, "PI-", ct);
            copy.ProformaNo = ProformaNumberSequence.SuggestNext(copy.ProformaNo, existing);
            copy.SnapshotJson = string.IsNullOrEmpty(original.SnapshotJson)
                ? original.SnapshotJson
                : DuplicateSnapshotScrub.TryDetach(original.SnapshotJson, copy.ProformaNo);

            if (await _store.TryDuplicateAsync(
                    copy, groupCopies,
                    new Step6Rows(chargeCopies, wastageCopies, customCopies),
                    pnlCopies, actorId, ct))
            {
                allocated = true;
                break;
            }
        }

        if (!allocated)
        {
            return ProformaResult<ProformaDto>.Fail(
                ProformaErrorCode.NumberExhausted,
                $"Could not allocate a free proforma number after {NumberAttempts} " +
                "retries. Another concurrent writer is racing this insert.");
        }

        await _audit.WriteAsync(
            tenantId, actorId, "proforma.duplicated", "proforma_invoices",
            copy.Id, ip, null, ct,
            beforeJson: JsonSerializer.Serialize(new { sourceNo = original.ProformaNo }),
            afterJson: JsonSerializer.Serialize(new
            {
                copy.ProformaNo,
                groups = groupCopies.Count,
                charges = chargeCopies.Count,
                wastages = wastageCopies.Count,
                customs = customCopies.Count,
                pnlRows = pnlCopies.Count,
            }));

        return ProformaResult<ProformaDto>.Ok(ToDto(copy));
    }

    // ── row-copy helpers (fresh ids + stamps, same content/order) ────

    private static OtherCharge CopyRow(OtherCharge c, Guid proformaId,
        Guid tenantId, Guid actorId, DateTime now) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, ProformaId = proformaId,
        ChargeName = c.ChargeName, Description = c.Description,
        TemplateKey = c.TemplateKey, Unit = c.Unit, MetricKey = c.MetricKey,
        CheckedSpecGroupNumbers = c.CheckedSpecGroupNumbers,
        Rate = c.Rate, MetricQty = c.MetricQty, Amount = c.Amount,
        IsActive = c.IsActive, PolishSubType = c.PolishSubType,
        CustomPolishDescription = c.CustomPolishDescription,
        CustomPolishQty = c.CustomPolishQty, SortOrder = c.SortOrder,
        CreatedAt = now, UpdatedAt = now, CreatedBy = actorId, UpdatedBy = actorId,
    };

    private static WastageRow CopyRow(WastageRow w, Guid proformaId,
        Guid tenantId, Guid actorId, DateTime now) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, ProformaId = proformaId,
        ColorName = w.ColorName, CheckedSourceKeys = w.CheckedSourceKeys,
        TotalSqm = w.TotalSqm, IsManuallyAdded = w.IsManuallyAdded,
        UseOptimizationFormula = w.UseOptimizationFormula,
        FormulaPricePerSqm = w.FormulaPricePerSqm,
        OptimizationPercent = w.OptimizationPercent,
        SheetQuantity = w.SheetQuantity, Rate = w.Rate,
        ResultAmount = w.ResultAmount, IsActive = w.IsActive,
        SortOrder = w.SortOrder,
        CreatedAt = now, UpdatedAt = now, CreatedBy = actorId, UpdatedBy = actorId,
    };

    private static CustomCharge CopyRow(CustomCharge c, Guid proformaId,
        Guid tenantId, Guid actorId, DateTime now) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, ProformaId = proformaId,
        ChargeName = c.ChargeName, Qty = c.Qty, Unit = c.Unit,
        Rate = c.Rate, Amount = c.Amount, IsActive = c.IsActive,
        SortOrder = c.SortOrder,
        CreatedAt = now, UpdatedAt = now, CreatedBy = actorId, UpdatedBy = actorId,
    };

    private static Domain.Entities.PnlRow CopyRow(Domain.Entities.PnlRow r, Guid proformaId,
        Guid tenantId, Guid actorId, DateTime now) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, ProformaId = proformaId,
        Kind = r.Kind, SpecNumber = r.SpecNumber, SpecName = r.SpecName,
        Description = r.Description,
        SheetWidthMm = r.SheetWidthMm, SheetHeightMm = r.SheetHeightMm,
        SheetSqm = r.SheetSqm, Quantity = r.Quantity,
        PricePerUnit = r.PricePerUnit, UnitLabel = r.UnitLabel,
        Amount = r.Amount, SheetSource = r.SheetSource, SortOrder = r.SortOrder,
        CreatedAt = now, UpdatedAt = now, CreatedBy = actorId, UpdatedBy = actorId,
    };

    // ═════════════════ INQUIRY → PROFORMA CONVERT ═══════════════════

    /// <summary>
    /// POST /api/v1/inquiries/{id}/convert-to-proforma — port of
    /// InquiryListViewModel.ConvertToPiAsync +
    /// ProformaInvoiceViewModel.StartFromInquiryAsync: guards (no existing
    /// PI, Prepared/Confirmed only), a fresh linked draft, the pure BOQ →
    /// Step 5 grid import (InquiryBoqConversion), the inquiry link write
    /// and the status advance to Sent (InquiryLinkSync.AfterInsertAsync —
    /// only when the workflow allows it).
    /// </summary>
    public async Task<ProformaResult<ConvertInquiryResultDto>> ConvertInquiryToProformaAsync(
        Guid tenantId, Guid inquiryId, Guid actorId, string? ip, CancellationToken ct)
    {
        var inquiry = await _store.FindInquiryAsync(tenantId, inquiryId, ct);
        if (inquiry is null)
        {
            return ProformaResult<ConvertInquiryResultDto>.Fail(
                ProformaErrorCode.NotFound, $"Inquiry {inquiryId} was not found.");
        }

        if (inquiry.ConvertedProformaId is not null)
        {
            return ProformaResult<ConvertInquiryResultDto>.Fail(
                ProformaErrorCode.Conflict,
                "This inquiry already has a proforma invoice. Delete the existing " +
                "PI first or open it directly.");
        }

        // Bug #7 defense-in-depth: the WPF guarded on denormalized link
        // columns that could go stale; the SaaS asks the source of truth.
        if (await _store.HasProformaForInquiryAsync(tenantId, inquiryId, ct))
        {
            return ProformaResult<ConvertInquiryResultDto>.Fail(
                ProformaErrorCode.Conflict,
                "A proforma invoice is already linked to this inquiry.");
        }

        var status = InquiryStatuses.Normalize(inquiry.Status);
        if (status is not (InquiryStatuses.Prepared or InquiryStatuses.Confirmed))
        {
            return ProformaResult<ConvertInquiryResultDto>.Fail(
                ProformaErrorCode.Conflict,
                "Only a Prepared or Confirmed Inquiry is eligible for " +
                $"Proforma creation (currently {status}).");
        }

        // The BOQ import decisions (pure port — partial rows stay on the
        // inquiry as notes; an unknown build never inherits a default).
        var lines = ParseBoq(inquiry.BoqJson);
        var imports = InquiryBoqConversion.ToImportRows(lines);
        var skipped = lines.Count - imports.Count;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;
        var invoice = new ProformaInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProformaNo = ProformaNumberSequence.SeedFor(today.Year),
            InquiryId = inquiry.Id,          // the link is the whole point
            ClientId = inquiry.CustomerId,   // Step 2 commercial context
            ProjectId = inquiry.ProjectId,   // Step 3 project context
            Status = ProformaStatuses.Draft,
            StatusText = "",
            LifecycleState = nameof(InvoiceLifecycleState.Draft),
            IssueDate = today,               // a fresh wizard start
            ValidUntil = today.AddDays(30),
            FurthestStep = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId,
        };

        var allocated = false;
        for (var attempt = 0; attempt < NumberAttempts; attempt++)
        {
            var existing = await _store.GetNumbersWithPrefixAsync(tenantId, "PI-", ct);
            invoice.ProformaNo = ProformaNumberSequence.SuggestNext(invoice.ProformaNo, existing);

            if (await _store.TryInsertAsync(invoice, ct))
            {
                allocated = true;
                break;
            }
        }

        if (!allocated)
        {
            return ProformaResult<ConvertInquiryResultDto>.Fail(
                ProformaErrorCode.NumberExhausted,
                $"Could not allocate a free proforma number after {NumberAttempts} " +
                "retries. Another concurrent writer is racing this insert.");
        }

        // The BOQ becomes the Step 5 grid (one spec group per importable
        // line, in BOQ order — the grid mirrors the BOQ grid). A quoted
        // rate is the customer's own ("manual"); an unquoted line keeps
        // the grid's own pricing ("formula", rate 0 until priced).
        if (imports.Count > 0)
        {
            var grid = await SaveSpecGroupsAsync(
                tenantId, invoice.Id, actorId, ip, new SaveSpecGroupsRequest(
                    imports.Select((row, index) => new SpecGroupRequest(
                        index + 1, row.GlassType, row.SpecName,
                        row.HasRate ? row.RatePerSqm : 0m,
                        row.HasRate ? "manual" : "formula",
                        [new SpecGroupItemRequest(
                            1, "", row.WidthMm, row.HeightMm, 0, 0, row.Qty, null, "")],
                        OuterGlassColor: row.OuterGlassColor,
                        InnerGlassColor: row.InnerGlassColor)).ToList()),
                ct);

            if (!grid.Success)
            {
                return ProformaResult<ConvertInquiryResultDto>.Fail(
                    grid.ErrorCode, grid.Message);
            }
        }

        // InquiryLinkSync.AfterInsertAsync: write the link, advance the
        // inquiry to Sent — but only when the workflow allows the
        // transition (a Confirmed inquiry stays Confirmed).
        await _store.TryLinkInquiryToProformaAsync(
            tenantId, inquiry.Id, invoice.Id, actorId, ct);
        if (InquiryWorkflow.CanTransition(status, InquiryStatuses.Sent))
        {
            await _store.TryAdvanceInquiryStatusAsync(
                tenantId, inquiry.Id, InquiryStatuses.Sent, actorId, ct);
        }

        // The WPF Phase 5 audit trail: the conversion carries the item
        // snapshot it carried in (first 12 lines).
        var trail = new StringBuilder(
            $"Inquiry {inquiry.InquiryNo} converted to PI {invoice.ProformaNo}" +
            $" — {imports.Count} BOQ line(s)");
        if (imports.Count > 0)
        {
            trail.Append(": ");
            foreach (var r in imports.Take(12))
            {
                trail.Append($"{r.SpecName} {r.WidthMm}x{r.HeightMm}x{r.Qty}; ");
            }

            if (imports.Count > 12)
            {
                trail.Append($"… +{imports.Count - 12} more");
            }
        }

        await _audit.WriteAsync(
            tenantId, actorId, "inquiry.converted_to_proforma", "inquiries",
            inquiry.Id, ip, null, ct,
            beforeJson: JsonSerializer.Serialize(new { inquiryStatus = status }),
            afterJson: JsonSerializer.Serialize(new
            {
                proformaId = invoice.Id,
                proformaNo = invoice.ProformaNo,
                imported = imports.Count,
                skipped,
                detail = trail.ToString(),
            }));

        var saved = await _store.FindByIdAsync(tenantId, invoice.Id, ct);
        return ProformaResult<ConvertInquiryResultDto>.Ok(
            new ConvertInquiryResultDto(
                ToDto(saved ?? invoice), imports.Count, skipped));
    }

    /// <summary>
    /// The inquiry's BOQ (boq_json) — tolerant parse: a missing/blank/
    /// malformed BOQ simply imports nothing (the WPF converted inquiries
    /// with empty line lists too).
    /// </summary>
    private static IReadOnlyList<InquiryBoqLine> ParseBoq(string? boqJson)
    {
        if (string.IsNullOrWhiteSpace(boqJson))
        {
            return Array.Empty<InquiryBoqLine>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<InquiryBoqLine>>(
                       boqJson, JsonOptions) ?? new List<InquiryBoqLine>();
        }
        catch (JsonException)
        {
            return Array.Empty<InquiryBoqLine>();
        }
    }

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    /// <summary>
    /// WPF InquiryLinkSync.AfterUpdateAsync — a PI lifecycle change
    /// advances the linked inquiry: Approved/Confirmed → Confirmed,
    /// Rejected/Cancelled (and void) → Cancelled. Fire-and-forget
    /// semantics: the sync must NEVER fail the PI operation.
    /// </summary>
    private async Task SyncLinkedInquiryAsync(
        Guid tenantId, ProformaInvoice invoice, InvoiceLifecycleState newState,
        Guid actorId, string? ip, CancellationToken ct)
    {
        if (invoice.InquiryId is null)
        {
            return;
        }

        var target = newState switch
        {
            InvoiceLifecycleState.Approved or InvoiceLifecycleState.Confirmed
                => InquiryStatuses.Confirmed,
            InvoiceLifecycleState.Rejected or InvoiceLifecycleState.Cancelled
                => InquiryStatuses.Cancelled,
            _ => null
        };

        if (target is null)
        {
            return;
        }

        try
        {
            var inquiry = await _store.FindInquiryAsync(
                tenantId, invoice.InquiryId.Value, ct);
            if (inquiry is null)
            {
                return;
            }

            var current = InquiryStatuses.Normalize(inquiry.Status);
            if (current == target || !InquiryWorkflow.CanTransition(current, target))
            {
                return; // skip silently — the PI operation still succeeded
            }

            if (await _store.TryAdvanceInquiryStatusAsync(
                    tenantId, inquiry.Id, target, actorId, ct))
            {
                await _audit.WriteAsync(
                    tenantId, actorId, "inquiry.status_auto_advanced", "inquiries",
                    inquiry.Id, ip, null, ct,
                    beforeJson: JsonSerializer.Serialize(new { status = current }),
                    afterJson: JsonSerializer.Serialize(new
                    {
                        status = target,
                        drivenByProforma = invoice.ProformaNo,
                    }));
            }
        }
        catch (Exception ex)
        {
            // WPF: swallowed and logged — never fail the PI operation.
            await _audit.WriteAsync(
                tenantId, actorId, "inquiry.link_sync_failed", "inquiries",
                invoice.InquiryId.Value, ip, null, ct,
                afterJson: JsonSerializer.Serialize(new
                {
                    error = ex.Message,
                    targetStatus = target,
                }));
        }
    }

    // ═════════════════════════ STEP 7: SUMMARY ═══════════════════════

    /// <summary>
    /// PUT step7 — recompute the financial summary from the saved Step 5
    /// and Step 6 subtotals plus the four operator inputs (the WPF
    /// mapper's persisted set). Port of
    /// InvoiceMainSummaryViewModel.Recalculate: discount off the net
    /// first (BUG-56 clamp), VAT on the discounted value (BUG-58).
    /// </summary>
    public async Task<ProformaResult<Step7Dto>> SaveStep7Async(
        Guid tenantId, Guid id, Guid actorId, string? ip,
        SaveStep7Request? request, CancellationToken ct)
    {
        var invoice = await _store.FindByIdAsync(tenantId, id, ct);
        if (invoice is null)
        {
            return ProformaResult<Step7Dto>.Fail(
                ProformaErrorCode.NotFound, $"Proforma invoice {id} was not found.");
        }

        if (invoice.IsVoid)
        {
            return ProformaResult<Step7Dto>.Fail(
                ProformaErrorCode.Conflict,
                "A voided proforma invoice is terminal history — it cannot be edited.");
        }

        if (!InvoiceLifecycle.TryParseState(invoice.LifecycleState, out var current)
            || !InvoiceLifecycle.IsEditable(current))
        {
            return ProformaResult<Step7Dto>.Fail(
                ProformaErrorCode.Conflict,
                $"Step 7 is only editable while the invoice is Draft or " +
                $"Rejected (currently {invoice.LifecycleState}).");
        }

        var inputs = ResolveStep7Inputs(invoice, request);
        if (inputs.Error is not null)
        {
            return ProformaResult<Step7Dto>.Fail(
                ProformaErrorCode.ValidationFailed, inputs.Error);
        }

        ApplySummary(invoice, inputs.Type, inputs.Value, inputs.VatApplicable, inputs.Percent);
        await _store.UpdateAsync(invoice, actorId, ct);

        await _audit.WriteAsync(
            tenantId, actorId, "proforma.step7_saved", "proforma_invoices",
            invoice.Id, ip, null, ct,
            afterJson: JsonSerializer.Serialize(new
            {
                discountType = invoice.DiscountType,
                discountValue = invoice.DiscountValue,
                isVatApplicable = invoice.IsVatApplicable,
                vatPercent = invoice.VatPercent,
                grandTotal = invoice.GrandTotalVatInclusive,
            }));

        return ProformaResult<Step7Dto>.Ok(ToStep7Dto(invoice));
    }

    /// <summary>
    /// POST step7/round-to — the WPF "round to target" tool: solve the
    /// fixed-amount discount whose grand total equals the target, switch
    /// the discount kind to FixedAmount and persist the recomputed
    /// summary. Can only discount DOWN (the WPF tool's own rule).
    /// </summary>
    public async Task<ProformaResult<RoundToStep7Dto>> RoundToStep7Async(
        Guid tenantId, Guid id, Guid actorId, string? ip,
        RoundToStep7Request? request, CancellationToken ct)
    {
        if (request is null)
        {
            return ProformaResult<RoundToStep7Dto>.Fail(
                ProformaErrorCode.ValidationFailed, "Request body is required.");
        }

        if (request.TargetGrandTotal < 0m)
        {
            return ProformaResult<RoundToStep7Dto>.Fail(
                ProformaErrorCode.ValidationFailed,
                "The target grand total cannot be negative.");
        }

        var invoice = await _store.FindByIdAsync(tenantId, id, ct);
        if (invoice is null)
        {
            return ProformaResult<RoundToStep7Dto>.Fail(
                ProformaErrorCode.NotFound, $"Proforma invoice {id} was not found.");
        }

        if (invoice.IsVoid)
        {
            return ProformaResult<RoundToStep7Dto>.Fail(
                ProformaErrorCode.Conflict,
                "A voided proforma invoice is terminal history — it cannot be edited.");
        }

        if (!InvoiceLifecycle.TryParseState(invoice.LifecycleState, out var current)
            || !InvoiceLifecycle.IsEditable(current))
        {
            return ProformaResult<RoundToStep7Dto>.Fail(
                ProformaErrorCode.Conflict,
                $"Step 7 is only editable while the invoice is Draft or " +
                $"Rejected (currently {invoice.LifecycleState}).");
        }

        var net = invoice.GlassMaterialsSubtotal + invoice.OtherChargesSubtotal;
        var maxGrand = InvoiceRoundTo.GrandFromDiscount(
            net, 0m, invoice.VatPercent, invoice.IsVatApplicable);
        if (request.TargetGrandTotal > maxGrand)
        {
            // WPF tool rule: it can only discount DOWN — a target above
            // the undiscounted total is refused with the total in hand.
            return ProformaResult<RoundToStep7Dto>.Fail(
                ProformaErrorCode.ValidationFailed,
                $"Cannot round up. Undiscounted total is AED {maxGrand:N2}.");
        }

        var solved = InvoiceRoundTo.DiscountOnNet(
            net, invoice.VatPercent, invoice.IsVatApplicable, request.TargetGrandTotal);
        var changed = solved != invoice.DiscountAmount
                      || invoice.DiscountType != nameof(DiscountKind.FixedAmount);

        // The tool switches the discount to a FIXED amount (WPF rule).
        ApplySummary(invoice, DiscountKind.FixedAmount, solved,
            invoice.IsVatApplicable, invoice.VatPercent);
        await _store.UpdateAsync(invoice, actorId, ct);

        await _audit.WriteAsync(
            tenantId, actorId, "proforma.step7_round_to", "proforma_invoices",
            invoice.Id, ip, null, ct,
            afterJson: JsonSerializer.Serialize(new
            {
                target = request.TargetGrandTotal,
                solvedDiscount = solved,
                grandTotal = invoice.GrandTotalVatInclusive,
            }));

        return ProformaResult<RoundToStep7Dto>.Ok(
            new RoundToStep7Dto(solved, changed, ToStep7Dto(invoice)));
    }

    /// <summary>GET step7 — the saved summary (readable in any state).</summary>
    public async Task<ProformaResult<Step7Dto>> GetStep7Async(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var invoice = await _store.FindByIdAsync(tenantId, id, ct);
        if (invoice is null)
        {
            return ProformaResult<Step7Dto>.Fail(
                ProformaErrorCode.NotFound, $"Proforma invoice {id} was not found.");
        }

        return ProformaResult<Step7Dto>.Ok(ToStep7Dto(invoice));
    }

    /// <summary>
    /// Resolve the effective inputs: request → saved invoice → WPF default
    /// (Percentage / 0 / VAT on / 5 — the WPF field initializers; a fresh
    /// invoice's column defaults are exactly these, so "null keeps the
    /// saved value" and "first save uses the default" coincide).
    /// </summary>
    private static (string? Error, DiscountKind Type, decimal Value,
        bool VatApplicable, decimal Percent) ResolveStep7Inputs(
        ProformaInvoice invoice, SaveStep7Request? request)
    {
        DiscountKind type;
        if (request?.DiscountType is null)
        {
            type = ParseDiscountKind(invoice.DiscountType) ?? DiscountKind.Percentage;
        }
        else if (!TryParseDiscountKind(request.DiscountType, out type))
        {
            return ($"Discount type must be 'Percentage' or 'FixedAmount' " +
                    $"(got '{request.DiscountType}').",
                default, default, default, default);
        }

        var value = request?.DiscountValue ?? invoice.DiscountValue;
        if (value < 0m)
        {
            return ("Discount value cannot be negative.",
                default, default, default, default);
        }

        var vatApplicable = request?.IsVatApplicable ?? invoice.IsVatApplicable;
        var percent = request?.VatPercent ?? invoice.VatPercent;
        if (percent is < 0m or > 100m)
        {
            return ("VAT percent must be between 0 and 100.",
                default, default, default, default);
        }

        return (null, type, value, vatApplicable, percent);
    }

    private static void ApplySummary(
        ProformaInvoice invoice, DiscountKind type, decimal value,
        bool vatApplicable, decimal percent)
    {
        var summary = InvoiceSummaryMath.Calculate(
            invoice.GlassMaterialsSubtotal, invoice.OtherChargesSubtotal,
            type, value, vatApplicable, percent);

        invoice.DiscountType = type.ToString();
        invoice.DiscountValue = value;
        invoice.IsVatApplicable = vatApplicable;
        invoice.VatPercent = percent;
        invoice.DiscountAmount = summary.DiscountAmount;
        invoice.VatAmount = summary.VatAmount;
        invoice.GrandTotalVatInclusive = summary.GrandTotalVatInclusive;
    }

    private static Step7Dto ToStep7Dto(ProformaInvoice invoice)
    {
        var summary = InvoiceSummaryMath.Calculate(
            invoice.GlassMaterialsSubtotal, invoice.OtherChargesSubtotal,
            ParseDiscountKind(invoice.DiscountType) ?? DiscountKind.Percentage,
            invoice.DiscountValue, invoice.IsVatApplicable, invoice.VatPercent);

        return new Step7Dto(
            invoice.DiscountType, invoice.DiscountValue,
            invoice.IsVatApplicable, invoice.VatPercent,
            invoice.GlassMaterialsSubtotal, invoice.OtherChargesSubtotal,
            summary.NetSubtotal, summary.DiscountAmount,
            summary.SubtotalBeforeVat, summary.VatAmount,
            summary.GrandTotalVatInclusive);
    }

    private static DiscountKind? ParseDiscountKind(string? text) =>
        TryParseDiscountKind(text, out var kind) ? kind : null;

    private static bool TryParseDiscountKind(string? text, out DiscountKind kind) =>
        Enum.TryParse((text ?? "").Trim(), ignoreCase: true, out kind);

    // ═════════════════════════ STEP 9: P&L ═════════════════════════

    /// <summary>
    /// POST step9 — recalculate the whole P&amp;L from the saved Steps 4–6
    /// state, the tenant Sheet Store and the two operator inputs, persist
    /// the rows + KPI snapshot. Port of
    /// ProfitLossStepViewModel.RecalculateAsync (WPF-verbatim; the domain
    /// PnLCalculator owns every formula — nothing numeric is client-owned).
    /// </summary>
    public async Task<ProformaResult<Step9Dto>> SaveStep9Async(
        Guid tenantId, Guid id, Guid actorId, string? ip,
        SaveStep9Request? request, CancellationToken ct)
    {
        var invoice = await _store.FindByIdAsync(tenantId, id, ct);
        if (invoice is null)
        {
            return ProformaResult<Step9Dto>.Fail(
                ProformaErrorCode.NotFound, $"Proforma invoice {id} was not found.");
        }

        if (invoice.IsVoid)
        {
            return ProformaResult<Step9Dto>.Fail(
                ProformaErrorCode.Conflict,
                "A voided proforma invoice is terminal history — it cannot be edited.");
        }

        if (!InvoiceLifecycle.TryParseState(invoice.LifecycleState, out var current)
            || !InvoiceLifecycle.IsEditable(current))
        {
            return ProformaResult<Step9Dto>.Fail(
                ProformaErrorCode.Conflict,
                $"Step 9 is only editable while the invoice is Draft or " +
                $"Rejected (currently {invoice.LifecycleState}).");
        }

        // Operator inputs: WPF defaults 15% / cost-at-selling on first run;
        // null keeps the saved value (a recalc without touching the knobs).
        var wastagePercent = request?.WastagePercent
            ?? (invoice.PnlCalculatedAt is null ? 15m : invoice.PnlWastagePercent);
        var costAtSelling = request?.CostAtSellingPrice
            ?? (invoice.PnlCalculatedAt is null ? true : invoice.PnlCostAtSellingPrice);

        if (wastagePercent is < 0m or > 95m)
        {
            return ProformaResult<Step9Dto>.Fail(
                ProformaErrorCode.ValidationFailed,
                "Wastage percent must be between 0 and 95.");
        }

        var result = await RecalculatePnlAsync(tenantId, invoice, wastagePercent, costAtSelling, ct);

        invoice.PnlWastagePercent = wastagePercent;
        invoice.PnlCostAtSellingPrice = costAtSelling;
        invoice.PnlGrossVatInclusive = result.Kpis.GrossVatInclusive;
        invoice.PnlNetExVat = result.Kpis.NetExVat;
        invoice.PnlTotalGlassCost = result.Kpis.TotalGlassCost;
        invoice.PnlTotalProductionCost = result.Kpis.TotalProductionCost;
        invoice.PnlProfit = result.Kpis.Profit;
        invoice.PnlMarginPercent = result.Kpis.MarginPercent;
        invoice.PnlOverallMarginPercent = result.Kpis.OverallMarginPercent;
        invoice.PnlNetMarginPercent = result.Kpis.NetMarginPercent;
        invoice.PnlUInsertCost = result.Kpis.UInsertCost;
        invoice.PnlBasisCost = result.Kpis.BasisCost;
        invoice.PnlCalculatedAt = DateTime.UtcNow;

        var entities = result.Rows.Select((row, index) => new Domain.Entities.PnlRow
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProformaId = invoice.Id,
            Kind = row.Kind,
            SpecNumber = row.SpecNumber,
            SpecName = row.SpecName,
            Description = row.Description,
            SheetWidthMm = row.SheetWidthMm,
            SheetHeightMm = row.SheetHeightMm,
            SheetSqm = row.SheetSqm,
            Quantity = row.Quantity,
            PricePerUnit = row.PricePerUnit,
            UnitLabel = row.UnitLabel,
            Amount = row.Amount,
            SheetSource = row.SheetSource,
            SortOrder = index,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = actorId,
            UpdatedBy = actorId,
        }).ToList();

        await _store.ReplaceStep9Async(invoice, entities, actorId, ct);

        await _audit.WriteAsync(
            tenantId, actorId, "proforma.step9_calculated", "proforma_invoices",
            invoice.Id, ip, null, ct,
            afterJson: JsonSerializer.Serialize(new
            {
                wastagePercent,
                costAtSelling,
                rows = entities.Count,
                result.Kpis.Profit,
                result.Kpis.OverallMarginPercent,
            }));

        return await GetStep9SectionAsync(tenantId, invoice.Id, ct);
    }

    /// <summary>GET step9 — the saved rows + KPI snapshot + operator
    /// inputs (readable in any lifecycle state — history stays visible).</summary>
    public async Task<ProformaResult<Step9Dto>> GetStep9Async(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var invoice = await _store.FindByIdAsync(tenantId, id, ct);
        if (invoice is null)
        {
            return ProformaResult<Step9Dto>.Fail(
                ProformaErrorCode.NotFound, $"Proforma invoice {id} was not found.");
        }

        return await GetStep9SectionAsync(tenantId, invoice.Id, ct);
    }

    private async Task<ProformaResult<Step9Dto>> GetStep9SectionAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var invoice = await _store.FindByIdAsync(tenantId, id, ct);
        var rows = await _store.GetStep9Async(tenantId, id, ct);

        PnlKpisDto? kpis = null;
        if (invoice?.PnlCalculatedAt is not null)
        {
            kpis = new PnlKpisDto(
                invoice.PnlGrossVatInclusive,
                invoice.PnlNetExVat,
                invoice.PnlTotalGlassCost,
                invoice.PnlTotalProductionCost,
                invoice.PnlProfit,
                invoice.PnlProfit < 0m && invoice.PnlGrossVatInclusive > 0m,
                invoice.PnlMarginPercent,
                invoice.PnlOverallMarginPercent,
                invoice.PnlNetMarginPercent,
                invoice.PnlUInsertCost,
                invoice.PnlBasisCost);
        }

        var dto = new Step9Dto(
            invoice?.PnlWastagePercent ?? 15m,
            invoice?.PnlCostAtSellingPrice ?? true,
            invoice?.PnlCalculatedAt is not null,
            kpis,
            rows.Select(r => new PnlRowDto(
                r.Id, r.Kind, r.SpecNumber, r.SpecName, r.Description,
                r.SheetWidthMm, r.SheetHeightMm, r.SheetSqm,
                r.Quantity, r.PricePerUnit, r.UnitLabel, r.Amount,
                r.SheetSource, r.SortOrder)).ToList());

        return ProformaResult<Step9Dto>.Ok(dto);
    }

    /// <summary>Runs the domain P&amp;L port over the saved document.</summary>
    private async Task<PnlResult> RecalculatePnlAsync(
        Guid tenantId, ProformaInvoice invoice,
        decimal wastagePercent, bool costAtSelling, CancellationToken ct)
    {
        var groups = await LoadSpecGroupInputsAsync(tenantId, invoice.Id, ct);
        var step6 = await _store.GetStep6Async(tenantId, invoice.Id, ct);
        var sheets = await _store.GetSheetStockAsync(tenantId, ct);

        var charges = step6.Charges.Select(c => new PnlStep6Charge(
            c.TemplateKey,
            c.ChargeName,
            c.MetricKey,
            c.Unit,
            DeserializeNumbers(c.CheckedSpecGroupNumbers),
            c.Rate,
            c.PolishSubType,
            // The WPF marks the matched-lam double-charge row by template.
            c.TemplateKey == ChargeTemplates.MatchedLamPolishKey,
            c.IsActive)).ToList();

        var wastages = step6.Wastages.Select(w => new PnlStep6Wastage(
            DeserializeStrings(w.CheckedSourceKeys),
            w.SheetQuantity,
            w.OptimizationPercent,
            w.IsActive)).ToList();

        var customs = step6.Customs.Select(c => new PnlStep6Custom(
            c.ChargeName, c.Qty, c.Rate, c.Unit, c.IsActive)).ToList();

        // Gross: the WPF binds the Step 7 grand total (VAT-inclusive) as
        // the provider — including the vestigial net-provider quirk, net
        // stays gross / 1.05 inside the calculator.
        return PnLCalculator.Recalculate(new PnLCalculator.Inputs(
            groups, charges, wastages, customs,
            new SheetStockIndex(sheets),
            wastagePercent,
            costAtSelling,
            invoice.GrandTotalVatInclusive));
    }

    private static string? ParseUnit(string? unit) =>
        Enum.TryParse<OtherChargeUnit>((unit ?? "").Trim(), ignoreCase: true, out var parsed)
            ? parsed.ToString()
            : null;

    private static string? ValidateStep6(
        SaveStep6Request request,
        IReadOnlyList<SpecGroupInput> inputs,
        HashSet<int> groupNumbers,
        HashSet<string> availableSourceKeys)
    {
        var index = 0;
        foreach (var row in request.Charges ?? Array.Empty<OtherChargeRowRequest>())
        {
            index++;

            if (ParseUnit(row.Unit) is null)
            {
                return $"Charge {index}: unit must be Sqm, Lm or Qty.";
            }

            var templateKey = (row.TemplateKey ?? "").Trim();
            if (templateKey.Length > 0 && ChargeTemplates.Find(templateKey) is null)
            {
                return $"Charge {index}: unknown template key '{templateKey}'.";
            }

            if (row.Rate < 0)
            {
                return $"Charge {index}: rate cannot be negative.";
            }

            // Polish rows are driven by their sub-type and delivery rows
            // are a fixed 1 TRIP — neither needs a metric key (WPF:
            // "Polish uses sub-type, not source dropdown"). Any key that
            // IS supplied must still be a real one.
            var isPolishRow = templateKey is ChargeTemplates.PolishKey
                or ChargeTemplates.MatchedLamPolishKey;
            var isDeliveryRow = templateKey == ChargeTemplates.DeliveryKey;
            var metricKey = MetricKeys.Normalize(row.MetricKey);
            if (metricKey.Length == 0 && !isPolishRow && !isDeliveryRow)
            {
                return $"Charge {index}: a metric key is required. " +
                       $"Valid: {string.Join(", ", MetricKeys.All)}.";
            }

            if (metricKey.Length > 0 && !MetricKeys.All.Contains(metricKey))
            {
                return $"Charge {index}: unknown metric key '{row.MetricKey}'. " +
                       $"Valid: {string.Join(", ", MetricKeys.All)}.";
            }

            var polishSubType = (row.PolishSubType ?? "").Trim();
            if (polishSubType.Length > 0
                && !PolishSubTypes.All.Contains(polishSubType))
            {
                return $"Charge {index}: unknown polish sub-type '{row.PolishSubType}'.";
            }

            foreach (var spec in row.CheckedSpecGroupNumbers ?? Array.Empty<int>())
            {
                if (!groupNumbers.Contains(spec))
                {
                    return $"Charge {index}: spec group {spec} does not exist " +
                           "on this invoice's Step 5 grid.";
                }
            }
        }

        index = 0;
        foreach (var row in request.Wastages ?? Array.Empty<WastageRowRequest>())
        {
            index++;

            if (row.Rate < 0 || row.FormulaPricePerSqm < 0 || row.OptimizationPercent < 0)
            {
                return $"Wastage {index}: rates and percentages cannot be negative.";
            }

            foreach (var key in row.CheckedSourceKeys ?? Array.Empty<string>())
            {
                if (!availableSourceKeys.Contains((key ?? "").Trim()))
                {
                    return $"Wastage {index}: source key '{key}' does not exist " +
                           "on this invoice's Step 5 grid — re-fetch the section " +
                           "after saving the grid.";
                }
            }
        }

        index = 0;
        foreach (var row in request.Customs ?? Array.Empty<CustomChargeRowRequest>())
        {
            index++;

            if (ParseUnit(row.Unit) is null)
            {
                return $"Custom charge {index}: unit must be Sqm, Lm or Qty.";
            }

            if (row.Qty < 0 || row.Rate < 0)
            {
                return $"Custom charge {index}: qty and rate cannot be negative.";
            }
        }

        return null;
    }

    private static OtherChargeDto ToDto(Domain.Entities.OtherCharge c) => new(
        c.Id, c.TemplateKey, c.ChargeName, c.Description, c.Unit, c.IsActive,
        c.MetricKey,
        DeserializeNumbers(c.CheckedSpecGroupNumbers),
        c.Rate, c.MetricQty, c.Amount,
        c.PolishSubType, c.CustomPolishDescription, c.CustomPolishQty,
        c.SortOrder);

    private static WastageRowDto ToDto(Domain.Entities.WastageRow w) => new(
        w.Id, w.IsActive, w.ColorName,
        DeserializeStrings(w.CheckedSourceKeys),
        w.TotalSqm, w.IsManuallyAdded, w.UseOptimizationFormula,
        w.FormulaPricePerSqm, w.OptimizationPercent, w.SheetQuantity,
        w.Rate, w.ResultAmount, w.SortOrder);

    private static CustomChargeDto ToDto(Domain.Entities.CustomCharge c) => new(
        c.Id, c.IsActive, c.ChargeName, c.Qty, c.Unit, c.Rate, c.Amount,
        c.SortOrder);

    private static IReadOnlyList<int> DeserializeNumbers(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<int>>(json ?? "[]") ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> DeserializeStrings(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(json ?? "[]") ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
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
        group.Group.SpecId,
        group.Group.OuterGlassColor, group.Group.InnerGlassColor,
        group.Group.ThirdGlassColor,
        group.Group.OuterSheetWidthMm, group.Group.OuterSheetHeightMm,
        group.Group.InnerSheetWidthMm, group.Group.InnerSheetHeightMm,
        group.Group.ThirdSheetWidthMm, group.Group.ThirdSheetHeightMm,
        group.Group.OuterSheetPricePerSqm, group.Group.InnerSheetPricePerSqm,
        group.Group.ThirdSheetPricePerSqm,
        group.Items.Sum(i => i.Qty),
        Math.Round(group.Items.Sum(i => i.TotalSqm), 3),
        group.Items.Select(ToDto).ToList());

    private static SpecGroupItemDto ToDto(Domain.Entities.SpecGroupItem item)
    {
        // The saved per-row factor (0009) — LM/SQM2 must round-trip exactly
        // as they were computed at save time.
        var math = SpecGroupRowMath.Calculate(
            item.W1, item.H1, item.W2, item.H2, item.Qty,
            item.PerimeterFactor <= 0
                ? SpecGroupRowMath.DefaultPerimeterFactor : item.PerimeterFactor,
            item.UnitPrice);
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
