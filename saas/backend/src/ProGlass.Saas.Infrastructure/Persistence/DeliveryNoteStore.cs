using Microsoft.EntityFrameworkCore;
using ProGlass.Saas.Application.Delivery;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Infrastructure.Persistence;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 10 storage — the WPF DeliveryNoteRepository port:
//    • MDN-{core} / DN-{core}-{seq:D2} numbering (UNIQUE guard)
//    • master recompute = remaining qty from CONFIRMED deliveries only
//    • optimistic RowVersion (WPF ConcurrencyConflictException)
//  RLS-scoped contexts per tenant (SaasDbContextFactory).
// ═══════════════════════════════════════════════════════════════════════

public sealed class DeliveryNoteStore(SaasDbContextFactory factory) : IDeliveryStore
{
    public async Task<JobOrder?> FindJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.JobOrders.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobOrderId, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Proforma.SpecGroupWithItems>> GetProformaGridAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var groups = await db.SpecGroups.AsNoTracking()
            .Where(g => g.ProformaId == proformaId)
            .OrderBy(g => g.GroupNumber)
            .ToListAsync(ct).ConfigureAwait(false);
        if (groups.Count == 0)
        {
            return Array.Empty<Proforma.SpecGroupWithItems>();
        }

        var groupIds = groups.Select(g => g.Id).ToList();
        var items = await db.SpecGroupItems.AsNoTracking()
            .Where(i => groupIds.Contains(i.SpecGroupId))
            .OrderBy(i => i.RowSerial)
            .ToListAsync(ct).ConfigureAwait(false);

        return groups
            .Select(g => new Proforma.SpecGroupWithItems(
                g, items.Where(i => i.SpecGroupId == g.Id).ToList()))
            .ToList();
    }

    public async Task<DeliveryNoteAggregate?> FindAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var note = await db.DeliveryNotes.AsNoTracking()
            .Include(d => d.JobOrder)
            .FirstOrDefaultAsync(d => d.Id == id, ct).ConfigureAwait(false);
        if (note is null)
        {
            return null;
        }

        var items = await db.DeliveryNoteItems.AsNoTracking()
            .Where(i => i.DeliveryNoteId == id)
            .OrderBy(i => i.SpecGroupNumber).ThenBy(i => i.SerialNumber)
            .ToListAsync(ct).ConfigureAwait(false);
        return new DeliveryNoteAggregate(note, items);
    }

    public async Task<DeliveryNoteAggregate?> FindMasterAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var master = await db.DeliveryNotes.AsNoTracking()
            .Include(d => d.JobOrder)
            .Where(d => d.JobOrderId == jobOrderId && d.IsMasterDn)
            .OrderBy(d => d.SequenceNumber)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (master is null)
        {
            return null;
        }

        var items = await db.DeliveryNoteItems.AsNoTracking()
            .Where(i => i.DeliveryNoteId == master.Id)
            .OrderBy(i => i.SpecGroupNumber).ThenBy(i => i.SerialNumber)
            .ToListAsync(ct).ConfigureAwait(false);
        return new DeliveryNoteAggregate(master, items);
    }

    public async Task<IReadOnlyList<DeliveryNote>> ListForJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.DeliveryNotes.AsNoTracking()
            .Where(d => d.JobOrderId == jobOrderId)
            .OrderByDescending(d => d.SequenceNumber)
            .ThenBy(d => d.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<MasterData.PagedResult<DeliveryNote>> SearchAsync(
        Guid tenantId, string? search, string? status, bool includeVoid,
        bool mastersOnly, int page, int pageSize, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var query = db.DeliveryNotes.AsNoTracking().AsQueryable();

        if (!includeVoid)
        {
            query = query.Where(d => !d.IsVoid);
        }

        if (mastersOnly)
        {
            query = query.Where(d => d.IsMasterDn);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(d => d.Status == status.Trim());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(d =>
                d.DeliveryNo.ToLower().Contains(s)
                || d.CustomerName.ToLower().Contains(s)
                || d.ProjectName.ToLower().Contains(s)
                || d.JobOrder.JobNo.ToLower().Contains(s));
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct).ConfigureAwait(false);
        return new MasterData.PagedResult<DeliveryNote>(items, page, pageSize, total);
    }

    public async Task<int> GetNextSequenceAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var max = await db.DeliveryNotes.AsNoTracking()
            .Where(d => d.JobOrderId == jobOrderId && !d.IsMasterDn)
            .MaxAsync(d => (int?)d.SequenceNumber, ct).ConfigureAwait(false);
        return (max ?? 0) + 1;
    }

    public async Task<bool> TryInsertAsync(
        DeliveryNote note, IReadOnlyList<DeliveryNoteItem> items, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(note.TenantId);
        db.DeliveryNotes.Add(note);
        db.DeliveryNoteItems.AddRange(items);
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return false;
        }
    }

    public async Task<bool> TryUpdateAsync(
        DeliveryNoteAggregate aggregate, int? expectedRowVersion, CancellationToken ct)
    {
        var note = aggregate.Note;
        using var db = factory.CreateForTenant(note.TenantId);

        var stored = await db.DeliveryNotes
            .FirstOrDefaultAsync(d => d.Id == note.Id, ct).ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        // Optimistic-concurrency check: a moved-on counter means someone
        // else saved first — never silently overwrite (WPF
        // ConcurrencyConflictException).
        if (expectedRowVersion.HasValue
            && stored.RowVersion != expectedRowVersion.Value)
        {
            return false;
        }

        stored.Status = note.Status;
        stored.DeliveryType = note.DeliveryType;
        stored.IsFoc = note.IsFoc;
        stored.Driver = note.Driver;
        stored.Vehicle = note.Vehicle;
        stored.ReceiverName = note.ReceiverName;
        stored.DeliveryDate = note.DeliveryDate;
        stored.DeliveryTime = note.DeliveryTime;
        stored.Notes = note.Notes;
        stored.TotalOrderedQty = note.TotalOrderedQty;
        stored.TotalDeliveredQty = note.TotalDeliveredQty;
        stored.TotalBalanceQty = note.TotalBalanceQty;
        stored.IsVoid = note.IsVoid;
        stored.VoidedAt = note.VoidedAt;
        stored.VoidedBy = note.VoidedBy;
        stored.VoidReason = note.VoidReason;
        stored.RowVersion++;
        stored.UpdatedAt = note.UpdatedAt;
        stored.UpdatedBy = note.UpdatedBy;

        // Line edits: the aggregate's list is authoritative (lines are
        // created with the note and only their quantities/notes change).
        var lineIds = aggregate.Items.Select(i => i.Id).ToList();
        var storedLines = await db.DeliveryNoteItems
            .Where(i => i.DeliveryNoteId == note.Id)
            .ToListAsync(ct).ConfigureAwait(false);
        var byId = storedLines.ToDictionary(i => i.Id);
        foreach (var line in aggregate.Items)
        {
            if (byId.TryGetValue(line.Id, out var storedLine))
            {
                storedLine.DeliveredQty = line.DeliveredQty;
                storedLine.BalanceQty = line.BalanceQty;
                storedLine.ReworkQty = line.ReworkQty;
                storedLine.ReturnedQty = line.ReturnedQty;
                storedLine.WeightPerPiece = line.WeightPerPiece;
                storedLine.LineNotes = line.LineNotes;
                storedLine.UpdatedAt = note.UpdatedAt;
                storedLine.UpdatedBy = note.UpdatedBy;
            }
        }

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            note.RowVersion = stored.RowVersion;
            note.UpdatedAt = stored.UpdatedAt;
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return false;
        }
    }

    /// <summary>
    /// WPF UpdateMasterDnAsync — the master's per-line previously-
    /// delivered and balance figures are recomputed from the CONFIRMED
    /// non-master, non-void notes of the job (Dispatched/Delivered/
    /// Signed/Received — in-progress drafts never consume remaining
    /// qty). Line match = spec group + serial (DeliveryNoteWorkflow.
    /// LineKey).
    /// </summary>
    public async Task RecomputeMasterAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var master = await db.DeliveryNotes
            .FirstOrDefaultAsync(
                d => d.JobOrderId == jobOrderId && d.IsMasterDn, ct)
            .ConfigureAwait(false);
        if (master is null)
        {
            return;
        }

        var masterLines = await db.DeliveryNoteItems
            .Where(i => i.DeliveryNoteId == master.Id)
            .ToListAsync(ct).ConfigureAwait(false);
        if (masterLines.Count == 0)
        {
            return;
        }

        // Confirmed deliveries only — and real notes only: Dummy DNs are
        // balance scratchpads, never confirmed deliveries (WPF
        // UpdateMasterDnAsync filter).
        var confirmedNotes = await db.DeliveryNotes.AsNoTracking()
            .Where(d => d.JobOrderId == jobOrderId
                        && !d.IsMasterDn
                        && !d.IsVoid
                        && d.DeliveryType != DeliveryTypes.Dummy
                        && (d.Status == DeliveryNoteStatuses.Dispatched
                            || d.Status == DeliveryNoteStatuses.Delivered
                            || d.Status == DeliveryNoteStatuses.Signed
                            || d.Status == DeliveryNoteStatuses.Received))
            .Select(d => d.Id)
            .ToListAsync(ct).ConfigureAwait(false);
        if (confirmedNotes.Count == 0)
        {
            foreach (var line in masterLines)
            {
                line.PreviouslyDeliveredQty = 0;
                line.DeliveredQty = 0;
                line.BalanceQty = Math.Max(0, line.OrderedQty);
                line.UpdatedAt = DateTime.UtcNow;
            }

            master.TotalDeliveredQty = 0;
            master.TotalBalanceQty = masterLines.Sum(l => l.BalanceQty);
            master.TotalOrderedQty = masterLines.Sum(l => l.OrderedQty);
            master.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return;
        }

        var deliveredByLine = await db.DeliveryNoteItems.AsNoTracking()
            .Where(i => confirmedNotes.Contains(i.DeliveryNoteId)
                        && i.JobOrderId == jobOrderId)
            .GroupBy(i => new { i.SpecGroupNumber, i.SerialNumber })
            .Select(g => new
            {
                g.Key.SpecGroupNumber,
                g.Key.SerialNumber,
                Delivered = g.Sum(x => x.DeliveredQty)
            })
            .ToListAsync(ct).ConfigureAwait(false);
        var delivered = deliveredByLine
            .GroupBy(x => DeliveryNoteWorkflow.LineKey(x.SpecGroupNumber, x.SerialNumber))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Delivered));

        var totalDelivered = 0;
        foreach (var line in masterLines)
        {
            delivered.TryGetValue(
                DeliveryNoteWorkflow.LineKey(line.SpecGroupNumber, line.SerialNumber),
                out var confirmed);
            // Master-tracker semantics (WPF UpdateMasterDnAsync): the
            // confirmed-so-far total lives in PreviouslyDeliveredQty,
            // DeliveredQty stays 0, balance = Ordered − confirmed.
            line.PreviouslyDeliveredQty = confirmed;
            line.DeliveredQty = 0;
            line.BalanceQty = Math.Max(0, line.OrderedQty - confirmed);
            totalDelivered += confirmed;
            line.UpdatedAt = DateTime.UtcNow;
        }

        master.TotalOrderedQty = masterLines.Sum(l => l.OrderedQty);
        master.TotalDeliveredQty = totalDelivered;
        master.TotalBalanceQty = masterLines.Sum(l => l.BalanceQty);
        master.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true
        || ex.InnerException?.Message.Contains(
            "duplicate key value violates unique constraint",
            StringComparison.OrdinalIgnoreCase) == true;
}
