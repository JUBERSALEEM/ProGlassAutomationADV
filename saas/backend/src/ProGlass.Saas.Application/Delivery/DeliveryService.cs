using System.Text.Json;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Delivery;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 10 service — the WPF delivery core:
//    • MasterDeliveryNoteService.EnsureForJobOrderAsync (the balance
//      tracker: all JO lines, remaining qty)
//    • DeliveryNoteCreateService.CreateOrOpenDispatchingAsync (ONE path
//      for every entry point — never open a confirmed note; open the
//      existing open note; otherwise create from the remaining balance;
//      line serials are copied from the master, never reassigned;
//      remaining qty is CONFIRMED deliveries only)
//    • DeliveryNoteMapper.FromJobOrder (line building: dedup by serial,
//      skip blank rows, OrderedQty = MAX per serial — BUG-23/24 guards)
//    • the StatusTransitions DeliveryNote table + the soft-close void
//  Later slices (documented): attachments, PDF/export snapshots,
//  analytics, the Tax Invoice link, the weight calculator.
// ═══════════════════════════════════════════════════════════════════════

/// <summary>The Delivery module (core lifecycle + balance tracking).</summary>
public sealed class DeliveryService(IDeliveryStore store, IAuditWriter audit)
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    // ── Queries ─────────────────────────────────────────────────────

    public async Task<MasterData.PagedResult<DeliveryNoteDto>> ListAsync(
        Guid tenantId, string? search, string? status, bool includeVoid,
        bool mastersOnly, int? page, int? pageSize, CancellationToken ct)
    {
        var (p, size) = NormalizePaging(page, pageSize);
        var result = await store.SearchAsync(
            tenantId, search, status, includeVoid, mastersOnly, p, size, ct);
        return new MasterData.PagedResult<DeliveryNoteDto>(
            result.Items.Select(ToDto).ToList(),
            result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<DeliveryResult<DeliveryNoteDto>> GetAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var agg = await store.FindAsync(tenantId, id, ct);
        return agg is null
            ? DeliveryResult<DeliveryNoteDto>.Fail(
                DeliveryErrorCode.NotFound, $"Delivery note {id} was not found.")
            : DeliveryResult<DeliveryNoteDto>.Ok(ToDto(agg));
    }

    public async Task<DeliveryResult<IReadOnlyList<DeliveryNoteDto>>> ListForJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        var job = await store.FindJobAsync(tenantId, jobOrderId, ct);
        if (job is null)
        {
            return DeliveryResult<IReadOnlyList<DeliveryNoteDto>>.Fail(
                DeliveryErrorCode.NotFound, $"Job order {jobOrderId} was not found.");
        }

        var notes = await store.ListForJobAsync(tenantId, jobOrderId, ct);
        return DeliveryResult<IReadOnlyList<DeliveryNoteDto>>.Ok(
            notes.Select(ToDto).ToList());
    }

    /// <summary>The master DN (balance tracker) for a job order.</summary>
    public async Task<DeliveryResult<DeliveryNoteDto>> GetMasterAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        var job = await store.FindJobAsync(tenantId, jobOrderId, ct);
        if (job is null)
        {
            return DeliveryResult<DeliveryNoteDto>.Fail(
                DeliveryErrorCode.NotFound, $"Job order {jobOrderId} was not found.");
        }

        await store.RecomputeMasterAsync(tenantId, jobOrderId, ct);
        var master = await store.FindMasterAsync(tenantId, jobOrderId, ct);
        return master is null
            ? DeliveryResult<DeliveryNoteDto>.Fail(DeliveryErrorCode.NotFound,
                $"No master delivery note exists for job order {job.JobNo}.")
            : DeliveryResult<DeliveryNoteDto>.Ok(ToDto(master));
    }

    // ── The one-path create-or-open ─────────────────────────────────

    /// <summary>
    /// POST /job-orders/{id}/delivery-notes — the workspace NEW DN /
    /// CREATE DN path, one rule set for every entry point.
    /// </summary>
    public async Task<DeliveryResult<CreateOrOpenDnDto>> CreateOrOpenDispatchingAsync(
        Guid tenantId, Guid actorId, Guid jobOrderId, string? ip, CancellationToken ct)
    {
        var job = await store.FindJobAsync(tenantId, jobOrderId, ct);
        if (job is null)
        {
            return DeliveryResult<CreateOrOpenDnDto>.Fail(
                DeliveryErrorCode.NotFound, $"Job order {jobOrderId} was not found.");
        }

        // 1. Ensure the master (the balance tracker) exists.
        await EnsureMasterAsync(tenantId, actorId, job, ct);

        // 2. Refresh the master's balances from CONFIRMED deliveries
        //    (in-progress drafts never consume remaining qty).
        await store.RecomputeMasterAsync(tenantId, job.Id, ct);

        // 3. Never open a confirmed note — open the existing open note.
        var all = await store.ListForJobAsync(tenantId, job.Id, ct);
        var open = all
            .Where(d => !d.IsMasterDn
                        && !string.Equals(d.DeliveryType, DeliveryTypes.Dummy,
                            StringComparison.OrdinalIgnoreCase)
                        && !d.IsVoid
                        && DeliveryNoteWorkflow.IsOpenForDispatch(d.Status))
            .OrderByDescending(d => d.SequenceNumber)
            .FirstOrDefault();
        if (open is not null)
        {
            var opened = await store.FindAsync(tenantId, open.Id, ct);
            if (opened is not null
                && !opened.Note.IsMasterDn
                && !string.Equals(opened.Note.DeliveryType, DeliveryTypes.Dummy,
                    StringComparison.OrdinalIgnoreCase)
                && !opened.Note.IsVoid
                && DeliveryNoteWorkflow.IsOpenForDispatch(opened.Note.Status))
            {
                return DeliveryResult<CreateOrOpenDnDto>.Ok(new CreateOrOpenDnDto(
                    DnCreateOutcome.OpenExisting, ToDto(opened),
                    $"Opened existing note {opened.Note.DeliveryNo}."));
            }
        }

        // 4. Build the next note from the master's remaining balance.
        var master = await store.FindMasterAsync(tenantId, job.Id, ct);
        List<DeliveryNoteItem> lines;
        string deliveryType;

        if (master is null || master.Items.Count == 0)
        {
            // No master could be built — first note straight from the
            // grid (WPF CreateFirstFromJobOrderAsync).
            lines = await BuildLinesFromGrid(tenantId, job, ct);
            deliveryType = DeliveryTypes.Original;
        }
        else
        {
            var remaining = master.Items.Where(i => ComputedBalance(i) > 0).ToList();
            if (remaining.Count == 0)
            {
                return DeliveryResult<CreateOrOpenDnDto>.Fail(
                    DeliveryErrorCode.FullyDelivered,
                    "All confirmed deliveries for this job order are complete.\n\n" +
                    $"Job Order : {job.JobNo}\n" +
                    $"Ordered   : {master.Note.TotalOrderedQty} pcs\n" +
                    $"Delivered : {master.Note.TotalDeliveredQty} pcs\n" +
                    "Balance   : 0 pcs\n\n" +
                    "Open God View to inspect dispatched delivery notes.");
            }

            // WPF FromMasterBalance: lines carry the confirmed-so-far as
            // Previously, DeliveredQty = 0 (the operator fills it in the
            // workspace before dispatch), Balance = the remaining qty.
            lines = remaining.Select(i =>
            {
                var balance = ComputedBalance(i);
                return new DeliveryNoteItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    DeliveryNoteId = Guid.Empty,
                    SpecGroupItemId = i.SpecGroupItemId,
                    SpecGroupNumber = i.SpecGroupNumber,
                    SerialNumber = i.SerialNumber,   // copied, never reassigned
                    SpecGroupName = i.SpecGroupName,
                    GlassRef = i.GlassRef,
                    W1 = i.W1, H1 = i.H1, W2 = i.W2, H2 = i.H2,
                    Description = i.Description,
                    Qty = i.Qty,
                    Sqm = i.Sqm,
                    OrderedQty = i.OrderedQty,
                    PreviouslyDeliveredQty = i.PreviouslyDeliveredQty,
                    DeliveredQty = 0,
                    BalanceQty = balance,
                    WeightPerPiece = i.WeightPerPiece,
                    JobOrderId = job.Id
                };
            }).ToList();
            deliveryType = DeliveryTypes.Original;
        }

        if (lines.Count == 0)
        {
            return DeliveryResult<CreateOrOpenDnDto>.Fail(
                DeliveryErrorCode.ValidationFailed,
                $"Job order {job.JobNo} has no deliverable lines " +
                "(the source grid is empty).");
        }

        var seq = await store.GetNextSequenceAsync(tenantId, job.Id, ct);
        var now = DateTime.UtcNow;
        var note = new DeliveryNote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DeliveryNo = DeliveryNoteNumbers.DeliveryNoteNo(job.JobNo, seq),
            JobOrderId = job.Id,
            CustomerId = job.CustomerId,
            ProjectId = job.ProjectId,
            CustomerName = job.CustomerName,
            ProjectName = job.ProjectName,
            Status = DeliveryNoteStatuses.Dispatching,
            DeliveryType = deliveryType,
            IsFoc = false,
            IsMasterDn = false,
            SequenceNumber = seq,
            Driver = "",
            Vehicle = "",
            ReceiverName = "",
            DeliveryDate = DateOnly.FromDateTime(now),
            DeliveryTime = now.ToString("HH:mm"),
            Notes = "",
            RowVersion = 0,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId
        };

        foreach (var line in lines)
        {
            line.DeliveryNoteId = note.Id;
            note.TotalOrderedQty += line.OrderedQty;
            note.TotalDeliveredQty += line.DeliveredQty;
            note.TotalBalanceQty += line.BalanceQty;
        }

        if (!await store.TryInsertAsync(note, lines, ct))
        {
            return DeliveryResult<CreateOrOpenDnDto>.Fail(
                DeliveryErrorCode.Conflict,
                $"Delivery note '{note.DeliveryNo}' already exists. " +
                "Please refresh and try again.");
        }

        await AuditAsync(tenantId, actorId, "deliverynote.created",
            "delivery_notes", note.Id, null, new
            {
                deliveryNo = note.DeliveryNo,
                jobOrderNo = job.JobNo,
                lines = lines.Count
            }, ip, ct);
        return DeliveryResult<CreateOrOpenDnDto>.Ok(new CreateOrOpenDnDto(
            DnCreateOutcome.Created, ToDto(new DeliveryNoteAggregate(note, lines)), ""));
    }

    /// <summary>
    /// The master DN: every JO line with its remaining balance (WPF
    /// MasterDeliveryNoteService + Mapper.FromJobOrder path A). Line
    /// identity = spec group + CONTINUOUS global serial (BUG-24 empty
    /// guard, BUG-23 dedup-by-serial with OrderedQty = MAX).
    /// </summary>
    private async Task EnsureMasterAsync(
        Guid tenantId, Guid actorId, JobOrder job, CancellationToken ct)
    {
        var existing = await store.FindMasterAsync(tenantId, job.Id, ct);
        if (existing is not null)
        {
            return;
        }

        var lines = await BuildLinesFromGrid(tenantId, job, ct);
        if (lines.Count == 0)
        {
            return;   // nothing deliverable — no master (WPF guard)
        }

        var now = DateTime.UtcNow;
        var master = new DeliveryNote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DeliveryNo = DeliveryNoteNumbers.MasterDnNo(job.JobNo),
            JobOrderId = job.Id,
            CustomerId = job.CustomerId,
            ProjectId = job.ProjectId,
            CustomerName = job.CustomerName,
            ProjectName = job.ProjectName,
            // The master is a tracker, never dispatched (WPF
            // CreateMasterDnAsync: Status Draft, type Dummy).
            Status = DeliveryNoteStatuses.Draft,
            DeliveryType = DeliveryTypes.Dummy,
            IsMasterDn = true,
            SequenceNumber = 0,
            DeliveryDate = DateOnly.FromDateTime(now),
            DeliveryTime = now.ToString("HH:mm"),
            Notes = "Master DN — auto-generated reference tracker",
            RowVersion = 0,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId
        };
        foreach (var line in lines)
        {
            line.DeliveryNoteId = master.Id;
            master.TotalOrderedQty += line.OrderedQty;
            master.TotalBalanceQty += line.BalanceQty;
        }

        await store.TryInsertAsync(master, lines, ct);
        await AuditAsync(tenantId, actorId, "deliverynote.master_created",
            "delivery_notes", master.Id, null, new
            {
                deliveryNo = master.DeliveryNo,
                jobOrderNo = job.JobNo,
                lines = lines.Count
            }, null, ct);
    }

    private async Task<List<DeliveryNoteItem>> BuildLinesFromGrid(
        Guid tenantId, JobOrder job, CancellationToken ct)
    {
        var groups = await store.GetProformaGridAsync(tenantId, job.ProformaId, ct);
        var lines = new List<DeliveryNoteItem>();
        var serial = 0;

        foreach (var group in groups.OrderBy(g => g.Group.GroupNumber))
        {
            // BUG-24 guard: skip empty rows; a row needs a qty and not
            // be a dimension-less placeholder to be deliverable.
            var rowsBySerial = group.Items
                .Where(r => !(string.IsNullOrWhiteSpace(r.Position) && r.W1 == 0 && r.H1 == 0))
                .Where(r => r.Qty > 0)
                .GroupBy(r => r.RowSerial);

            foreach (var rowGroup in rowsBySerial)
            {
                var row = rowGroup.First();
                serial++;   // continuous global numbering across all groups
                var ordered = rowGroup.Max(r => r.Qty);
                lines.Add(new DeliveryNoteItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    DeliveryNoteId = Guid.Empty,
                    SpecGroupItemId = row.Id,
                    SpecGroupNumber = group.Group.GroupNumber,
                    SerialNumber = serial,
                    SpecGroupName = !string.IsNullOrWhiteSpace(group.Group.SpecName)
                        ? group.Group.SpecName
                        : $"Spec {group.Group.GroupNumber}",
                    GlassRef = row.Position ?? "",
                    W1 = row.W1, H1 = row.H1, W2 = row.W2, H2 = row.H2,
                    Description = group.Group.SpecName ?? "",
                    Qty = ordered,
                    Sqm = row.TotalSqm,
                    OrderedQty = ordered,
                    PreviouslyDeliveredQty = 0,
                    DeliveredQty = 0,
                    BalanceQty = ordered,
                    WeightPerPiece = 0m,   // weight calc: later slice
                    JobOrderId = job.Id
                });
            }
        }

        return lines;
    }

    // ── Dispatch edit ───────────────────────────────────────────────

    /// <summary>
    /// PUT /{id} — the workspace's dispatch edit: the confirm block
    /// (driver/vehicle/receiver/date/time/notes/FOC) + the line delivered
    /// quantities. OPEN notes only (confirmed notes are history).
    /// </summary>
    public async Task<DeliveryResult<DeliveryNoteDto>> UpdateDispatchAsync(
        Guid tenantId, Guid id, Guid actorId, UpdateDispatchRequest request,
        string? ip, CancellationToken ct)
    {
        var agg = await store.FindAsync(tenantId, id, ct);
        if (agg is null)
        {
            return DeliveryResult<DeliveryNoteDto>.Fail(
                DeliveryErrorCode.NotFound, $"Delivery note {id} was not found.");
        }

        var note = agg.Note;
        if (note.IsMasterDn)
        {
            return DeliveryResult<DeliveryNoteDto>.Fail(DeliveryErrorCode.Conflict,
                "The master delivery note is the balance tracker — it cannot be " +
                "edited; deliver through a dispatch note.");
        }

        if (note.IsVoid || !DeliveryNoteWorkflow.IsOpenForDispatch(note.Status))
        {
            return DeliveryResult<DeliveryNoteDto>.Fail(DeliveryErrorCode.Conflict,
                $"Delivery note {note.DeliveryNo} is {note.Status} — confirmed " +
                "deliveries are history and cannot be edited.");
        }

        note.Driver = request.Driver ?? note.Driver;
        note.Vehicle = request.Vehicle ?? note.Vehicle;
        note.ReceiverName = request.ReceiverName ?? note.ReceiverName;
        note.DeliveryDate = request.DeliveryDate ?? note.DeliveryDate;
        note.DeliveryTime = request.DeliveryTime ?? note.DeliveryTime;
        note.Notes = request.Notes ?? note.Notes;
        note.IsFoc = request.IsFoc ?? note.IsFoc;
        note.UpdatedAt = DateTime.UtcNow;
        note.UpdatedBy = actorId;

        if (request.Items is { Count: > 0 })
        {
            var byId = agg.Items.ToDictionary(i => i.Id);
            foreach (var edit in request.Items)
            {
                if (!byId.TryGetValue(edit.ItemId, out var line))
                {
                    return DeliveryResult<DeliveryNoteDto>.Fail(
                        DeliveryErrorCode.ValidationFailed,
                        $"Line {edit.ItemId} is not part of note {note.DeliveryNo}.");
                }

                var balance = Math.Max(0, line.OrderedQty - line.PreviouslyDeliveredQty);
                if (edit.DeliveredQty < 0 || edit.DeliveredQty > balance)
                {
                    return DeliveryResult<DeliveryNoteDto>.Fail(
                        DeliveryErrorCode.ValidationFailed,
                        $"Line {line.SerialNumber} ({line.SpecGroupName}): delivered " +
                        $"quantity {edit.DeliveredQty} is outside the balance 0–{balance}.");
                }

                line.DeliveredQty = edit.DeliveredQty;
                line.BalanceQty = Math.Max(0, balance - edit.DeliveredQty);
                if (edit.Notes is not null)
                {
                    line.LineNotes = edit.Notes;
                }
            }
        }

        note.TotalOrderedQty = agg.Items.Sum(i => i.OrderedQty);
        note.TotalDeliveredQty = agg.Items.Sum(i => i.DeliveredQty);
        note.TotalBalanceQty = agg.Items.Sum(i => i.BalanceQty);

        if (!await store.TryUpdateAsync(agg, request.ExpectedRowVersion, ct))
        {
            return DeliveryResult<DeliveryNoteDto>.Fail(DeliveryErrorCode.ConcurrencyConflict,
                $"Delivery note {note.DeliveryNo} was modified by someone else — " +
                "reload and retry (RowVersion mismatch).");
        }

        await AuditAsync(tenantId, actorId, "deliverynote.updated",
            "delivery_notes", note.Id, null, new
            {
                deliveryNo = note.DeliveryNo,
                totalDelivered = note.TotalDeliveredQty
            }, ip, ct);
        return DeliveryResult<DeliveryNoteDto>.Ok(ToDto(agg));
    }

    // ── Lifecycle ───────────────────────────────────────────────────

    public async Task<DeliveryResult<DeliveryNoteDto>> TransitionAsync(
        Guid tenantId, Guid id, Guid actorId, DeliveryTransitionRequest request,
        string? ip, CancellationToken ct)
    {
        var agg = await store.FindAsync(tenantId, id, ct);
        if (agg is null)
        {
            return DeliveryResult<DeliveryNoteDto>.Fail(
                DeliveryErrorCode.NotFound, $"Delivery note {id} was not found.");
        }

        var note = agg.Note;
        if (note.IsMasterDn)
        {
            return DeliveryResult<DeliveryNoteDto>.Fail(DeliveryErrorCode.Conflict,
                "The master delivery note tracks the balance — it is never dispatched.");
        }

        var next = (request.ToStatus ?? "").Trim();
        if (!DeliveryNoteStateMachine.CanTransition(note.Status, next))
        {
            return DeliveryResult<DeliveryNoteDto>.Fail(DeliveryErrorCode.Conflict,
                DeliveryNoteStateMachine.Reason(note.Status, next));
        }

        var before = note.Status;
        note.Status = next;
        note.UpdatedAt = DateTime.UtcNow;
        note.UpdatedBy = actorId;

        if (!await store.TryUpdateAsync(agg, null, ct))
        {
            return DeliveryResult<DeliveryNoteDto>.Fail(DeliveryErrorCode.ConcurrencyConflict,
                $"Delivery note {note.DeliveryNo} was modified by someone else — " +
                "reload and retry.");
        }

        // A transition into a confirmed status changes the confirmed set
        // → the master's remaining balance must be refreshed.
        if (DeliveryNoteWorkflow.IsConfirmedDelivery(next))
        {
            await store.RecomputeMasterAsync(tenantId, note.JobOrderId, ct);
        }

        await AuditAsync(tenantId, actorId, "deliverynote.status_changed",
            "delivery_notes", note.Id, null, new
            {
                deliveryNo = note.DeliveryNo,
                from = before,
                to = next,
                reason = request.Reason
            }, ip, ct);
        return DeliveryResult<DeliveryNoteDto>.Ok(ToDto(agg));
    }

    /// <summary>
    /// POST /{id}/void — the soft-close (Cancelled is the void target,
    /// reachable from every non-terminal state; the row survives for the
    /// audit trail). The master's balances are refreshed afterwards.
    /// </summary>
    public async Task<DeliveryResult<DeliveryNoteDto>> VoidAsync(
        Guid tenantId, Guid id, Guid actorId, VoidDeliveryRequest request,
        string? ip, CancellationToken ct)
    {
        var agg = await store.FindAsync(tenantId, id, ct);
        if (agg is null)
        {
            return DeliveryResult<DeliveryNoteDto>.Fail(
                DeliveryErrorCode.NotFound, $"Delivery note {id} was not found.");
        }

        var note = agg.Note;
        if (!note.IsVoid)
        {
            note.IsVoid = true;
            note.VoidedAt = DateTime.UtcNow;
            note.VoidedBy = actorId;
            note.VoidReason = string.IsNullOrWhiteSpace(request.Reason)
                ? "(no reason given)"
                : request.Reason!.Trim();
            note.Status = DeliveryNoteStatuses.Cancelled;
            note.UpdatedAt = DateTime.UtcNow;
            note.UpdatedBy = actorId;

            if (!await store.TryUpdateAsync(agg, null, ct))
            {
                return DeliveryResult<DeliveryNoteDto>.Fail(
                    DeliveryErrorCode.ConcurrencyConflict,
                    $"Delivery note {note.DeliveryNo} was modified by someone else — " +
                    "reload and retry.");
            }

            await store.RecomputeMasterAsync(tenantId, note.JobOrderId, ct);
            await AuditAsync(tenantId, actorId, "deliverynote.voided",
                "delivery_notes", note.Id, null, new
                {
                    deliveryNo = note.DeliveryNo,
                    reason = note.VoidReason
                }, ip, ct);
        }

        return DeliveryResult<DeliveryNoteDto>.Ok(ToDto(agg));
    }

    // ── helpers ─────────────────────────────────────────────────────

    /// <summary>WPF DeliveryNoteItem.ComputedBalanceQty (BUG-05): always
    /// fresh, never a stale stored value.</summary>
    private static int ComputedBalance(DeliveryNoteItem i) =>
        Math.Max(0, i.OrderedQty - i.PreviouslyDeliveredQty - i.DeliveredQty);

    private static (int Page, int Size) NormalizePaging(int? page, int? pageSize)
    {
        var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        return (Math.Max(page ?? 1, 1), size);
    }

    internal static DeliveryNoteDto ToDto(DeliveryNote n) => new(
        n.Id, n.DeliveryNo, n.JobOrderId, n.JobOrder?.JobNo ?? "",
        n.CustomerName, n.ProjectName, n.Status, n.DeliveryType,
        n.IsFoc, n.IsMasterDn, n.SequenceNumber,
        n.Driver, n.Vehicle, n.ReceiverName, n.DeliveryDate, n.DeliveryTime,
        n.Notes, n.TotalOrderedQty, n.TotalDeliveredQty, n.TotalBalanceQty,
        n.IsVoid, n.VoidedAt, n.VoidReason, n.RowVersion,
        new List<DeliveryNoteItemDto>());

    internal static DeliveryNoteDto ToDto(DeliveryNoteAggregate agg) => new(
        agg.Note.Id, agg.Note.DeliveryNo, agg.Note.JobOrderId,
        agg.Note.JobOrder?.JobNo ?? "",
        agg.Note.CustomerName, agg.Note.ProjectName, agg.Note.Status,
        agg.Note.DeliveryType, agg.Note.IsFoc, agg.Note.IsMasterDn,
        agg.Note.SequenceNumber, agg.Note.Driver, agg.Note.Vehicle,
        agg.Note.ReceiverName, agg.Note.DeliveryDate, agg.Note.DeliveryTime,
        agg.Note.Notes, agg.Note.TotalOrderedQty, agg.Note.TotalDeliveredQty,
        agg.Note.TotalBalanceQty, agg.Note.IsVoid, agg.Note.VoidedAt,
        agg.Note.VoidReason, agg.Note.RowVersion,
        agg.Items
            .OrderBy(i => i.SpecGroupNumber).ThenBy(i => i.SerialNumber)
            .Select(ToDto).ToList());

    private static DeliveryNoteItemDto ToDto(DeliveryNoteItem i) => new(
        i.Id, i.SpecGroupNumber, i.SerialNumber, i.SpecGroupName, i.GlassRef,
        i.W1, i.H1, i.W2, i.H2,
        i.OrderedQty, i.PreviouslyDeliveredQty, i.DeliveredQty, i.BalanceQty,
        i.ReworkQty, i.ReturnedQty, i.WeightPerPiece,
        i.WeightPerPiece * i.DeliveredQty, i.LineNotes);

    private async Task AuditAsync(
        Guid tenantId, Guid? actorUserId, string action, string entityType,
        Guid entityId, object? before, object? after, string? ip, CancellationToken ct)
        => await audit.WriteAsync(tenantId, actorUserId, action, entityType,
            entityId, ip, null,
            before is null ? null : JsonSerializer.Serialize(before),
            after is null ? null : JsonSerializer.Serialize(after),
            ct).ConfigureAwait(false);
}
