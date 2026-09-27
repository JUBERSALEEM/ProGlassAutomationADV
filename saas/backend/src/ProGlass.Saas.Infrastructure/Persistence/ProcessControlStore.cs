using System.Data;
using Microsoft.EntityFrameworkCore;
using ProGlass.Saas.Application.Production;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Infrastructure.Persistence;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 26a storage — the WPF production.db process/control tables:
//    • process_executions / process_pairings / process_assemblies
//      (deterministic identities UNIQUE per tenant; rework lineage)
//    • production_control_holds / _assignments / _overrides (history
//      preserved — releases flag, assignments deactivate)
//    • workstations (per-tenant master data, seeded defaults)
//    • the release → execution projection (WPF ProjectJobAsync) and the
//      work-item → execution sync (WPF SyncLinkedExecutionQty)
//  Serializable transactions wrap the multi-row flows exactly as the WPF
//  does; RLS-scoped contexts per tenant.
// ═══════════════════════════════════════════════════════════════════════

public sealed class ProcessControlStore(SaasDbContextFactory factory) : IProcessControlStore
{
    // ── work items + job context ─────────────────────────────────────

    public async Task<JobOrder?> FindJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.JobOrders.AsNoTracking()
            .Include(j => j.Salesman)
            .FirstOrDefaultAsync(j => j.Id == jobOrderId, ct)
            .ConfigureAwait(false);
    }

    public async Task<ProductionPiece?> FindPieceAsync(
        Guid tenantId, Guid pieceId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProductionPieces.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pieceId, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProductionPiece>> ListPiecesByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var rows = await db.ProductionPieces.AsNoTracking()
            .Where(p => p.JobOrderId == jobOrderId)
            .ToListAsync(ct).ConfigureAwait(false);
        // Stage codes are a Domain mapping — the sort runs in memory.
        return rows
            .OrderBy(p => p.SpecGroupNumber)
            .ThenBy(p => p.SerialNumber)
            .ThenBy(p => ProductionStages.Code(p.Stage))
            .ThenBy(p => p.Position)
            .ToList();
    }

    // ── process executions ───────────────────────────────────────────

    public async Task<ProcessExecution?> FindExecutionAsync(
        Guid tenantId, string executionId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProcessExecutions.AsNoTracking()
            .FirstOrDefaultAsync(e => e.ExecutionId == executionId, ct)
            .ConfigureAwait(false);
    }

    public async Task<ProcessExecution?> FindExecutionByPieceAsync(
        Guid tenantId, Guid pieceId, int processType, int reworkSequence,
        CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProcessExecutions.AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.ProductionPieceId == pieceId
                && e.ProcessType == processType
                && e.ReworkSequence == reworkSequence, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessExecution>> ListExecutionsByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProcessExecutions.AsNoTracking()
            .Where(e => e.JobOrderId == jobOrderId)
            .OrderBy(e => e.SpecGroupNumber)
            .ThenBy(e => e.SerialNumber)
            .ThenBy(e => e.ProcessType)
            .ThenBy(e => e.Sequence)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessExecution>> ListExecutionsByProcessAsync(
        Guid tenantId, int processType, int take, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProcessExecutions.AsNoTracking()
            .Where(e => e.ProcessType == processType)
            .OrderByDescending(e => e.CreatedAt)
            .Take(take)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<ProcessExecution> UpsertExecutionAsync(
        Guid tenantId, ProcessExecution execution, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct).ConfigureAwait(false);

        var existing = await db.ProcessExecutions
            .FirstOrDefaultAsync(e => e.ExecutionId == execution.ExecutionId, ct)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return existing; // idempotent — the deterministic id already ran
        }

        db.ProcessExecutions.Add(execution);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return execution;
    }

    public async Task<bool> TryUpdateExecutionAsync(
        ProcessExecution execution, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(execution.TenantId);
        var stored = await db.ProcessExecutions
            .FirstOrDefaultAsync(e => e.Id == execution.Id, ct).ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        stored.Status = execution.Status;
        stored.StartedBy = execution.StartedBy;
        stored.StartedAt = execution.StartedAt;
        stored.CompletedBy = execution.CompletedBy;
        stored.CompletedAt = execution.CompletedAt;
        stored.QtyRequired = execution.QtyRequired;
        stored.QtyDone = execution.QtyDone;
        stored.EntryType = execution.EntryType;
        stored.Reason = execution.Reason;
        stored.Notes = execution.Notes;
        stored.QcStatus = execution.QcStatus;
        stored.IsFoc = execution.IsFoc;
        stored.ProcessDetailJson = execution.ProcessDetailJson;
        stored.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>WPF ProjectJobAsync — inserts missing Pending executions
    /// for one job, re-links existing rows to their work items, refreshes
    /// QtyRequired only on untouched Pending rows and removes orphaned
    /// untouched rows (re-release never resets floor status).</summary>
    public async Task<int> ProjectPendingExecutionsAsync(
        Guid tenantId, Guid jobOrderId, Guid actorId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct).ConfigureAwait(false);

        var job = await db.JobOrders.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobOrderId, ct).ConfigureAwait(false);
        if (job is null)
        {
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return 0;
        }

        var items = await db.ProductionPieces
            .Where(p => p.JobOrderId == jobOrderId
                && !p.IsCancelled
                && p.Status != WorkItemStatuses.Cancelled)
            .ToListAsync(ct).ConfigureAwait(false);

        var existing = await db.ProcessExecutions
            .Where(e => e.JobOrderId == jobOrderId)
            .ToListAsync(ct).ConfigureAwait(false);

        var byPiece = new Dictionary<Guid, ProcessExecution>();
        var byId = new Dictionary<string, ProcessExecution>(StringComparer.Ordinal);
        foreach (var exec in existing)
        {
            if (!string.IsNullOrEmpty(exec.ExecutionId) && !byId.ContainsKey(exec.ExecutionId))
            {
                byId[exec.ExecutionId] = exec;
            }
            if (exec.ProductionPieceId is { } pid && exec.ReworkSequence == 0
                && !byPiece.ContainsKey(pid))
            {
                byPiece[pid] = exec;
            }
        }

        var units = items
            .GroupBy(w => (w.SpecGroupNumber, w.SerialNumber))
            .ToDictionary(g => g.Key, g => g.ToList());

        int added = 0;
        var now = DateTime.UtcNow;
        foreach (var wi in items)
        {
            int process = ProductionStages.Code(wi.Stage);
            if (process < 1 || process > 8) continue;

            string projectedId = ProcessRouting.BuildProjectedExecutionId(
                job.JobNo, wi.SpecGroupNumber, wi.SerialNumber, process, wi.Position);
            string legacyId = ProcessRouting.BuildExecutionId(
                job.JobNo, wi.SpecGroupNumber, wi.SerialNumber, process, 0);

            ProcessExecution? match = null;
            if (!byPiece.TryGetValue(wi.Id, out match))
            {
                byId.TryGetValue(projectedId, out match);
            }
            if (match is null)
            {
                match = existing.FirstOrDefault(e =>
                    e.ReworkSequence == 0
                    && string.IsNullOrEmpty(e.ParentExecutionId)
                    && e.ProcessType == process
                    && e.SpecGroupNumber == wi.SpecGroupNumber
                    && e.SerialNumber == wi.SerialNumber
                    && string.Equals(e.Position ?? "", wi.Position ?? "",
                        StringComparison.OrdinalIgnoreCase)
                    && (e.ExecutionId == legacyId || e.ExecutionId == projectedId));
            }

            if (match is not null)
            {
                if (match.ProductionPieceId is null)
                {
                    match.ProductionPieceId = wi.Id;
                }
                if (match.Status == WorkItemStatuses.Pending
                    && match.StartedAt is null && match.QtyDone == 0
                    && match.QtyRequired != wi.Qty)
                {
                    match.QtyRequired = wi.Qty;
                    match.UpdatedAt = now;
                }
                byPiece[wi.Id] = match;
                continue;
            }

            if (!units.TryGetValue((wi.SpecGroupNumber, wi.SerialNumber), out var unit))
            {
                unit = new List<ProductionPiece> { wi };
            }

            var route = ProcessRouting.BuildRouteForWorkItem(
                ToRoute(wi, job.JobNo), unit.Select(p => ToRoute(p, job.JobNo)).ToList());
            string prev = route.PreviousRequiredBefore(wi.Stage) ?? wi.PredecessorStage ?? "";
            string next = route.NextRequiredAfter(wi.Stage) ?? "";
            string pieceId = string.IsNullOrWhiteSpace(wi.UniqueKey)
                ? $"{job.JobNo}|{wi.SpecGroupNumber}|{wi.SerialNumber}|{wi.Position}"
                : wi.UniqueKey;

            var row = new ProcessExecution
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ExecutionId = projectedId,
                JobOrderId = jobOrderId,
                JobRef = job.JobNo,
                SourceProformaId = job.ProformaId.ToString(),
                ProductionPieceId = wi.Id,
                SpecGroupNumber = wi.SpecGroupNumber,
                SpecName = wi.SpecName,
                SerialNumber = wi.SerialNumber,
                Position = wi.Position,
                PieceId = pieceId,
                InputPieceId = pieceId,
                OutputPieceId = pieceId,
                ProcessType = process,
                Sequence = 0,
                Status = WorkItemStatuses.Pending,
                QtyRequired = wi.Qty,
                QtyDone = 0,
                EntryType = ProductionEntryTypes.Normal,
                Notes = "Released — pending start",
                QcStatus = QcResults.Pending,
                IsFoc = wi.IsFoc,
                PreviousStage = prev,
                NextStage = next,
                ReworkSequence = 0,
                CreatedBy = actorId,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.ProcessExecutions.Add(row);
            byId[projectedId] = row;
            byPiece[wi.Id] = row;
            existing.Add(row);
            added++;
        }

        // Orphaned untouched Pending rows whose work item is gone.
        var liveIds = items.Select(i => i.Id).ToHashSet();
        foreach (var exec in existing)
        {
            if (exec.Status != WorkItemStatuses.Pending) continue;
            if (exec.StartedAt is not null || exec.QtyDone > 0) continue;
            if (exec.ReworkSequence != 0 || !string.IsNullOrEmpty(exec.ParentExecutionId)) continue;
            if (exec.ProductionPieceId is not { } pid || liveIds.Contains(pid)) continue;
            db.ProcessExecutions.Remove(exec);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return added;
    }

    public async Task SyncLinkedExecutionsAsync(
        Guid tenantId, ProductionPiece piece, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var linked = await db.ProcessExecutions
            .Where(e => e.ProductionPieceId == piece.Id && e.ReworkSequence == 0)
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var exec in linked)
        {
            exec.QtyRequired = piece.Qty;
            exec.QtyDone = piece.QtyDone;
            bool frozen = exec.Status == WorkItemStatuses.Rework
                || exec.Status == WorkItemStatuses.Rejected
                || exec.Status == WorkItemStatuses.QcFailed
                || exec.Status == WorkItemStatuses.Cancelled;
            bool startedEmpty = exec.Status == WorkItemStatuses.InProgress
                && piece.Status == WorkItemStatuses.Pending;
            if (!frozen && !startedEmpty)
            {
                exec.Status = piece.Status;
            }
            if (exec.Status == WorkItemStatuses.InProgress && exec.StartedAt is null)
            {
                exec.StartedAt = DateTime.UtcNow;
                exec.StartedBy = piece.UpdatedBy;
            }
            if (piece.Status == WorkItemStatuses.Completed)
            {
                exec.CompletedAt = piece.CompletedAt;
                exec.CompletedBy = piece.CompletedBy;
            }
            exec.UpdatedAt = DateTime.UtcNow;
        }
        if (linked.Count > 0)
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>WPF control transitions — one serializable transaction
    /// with a re-fetch, the control event row and the linked-execution
    /// mirror.</summary>
    public async Task<(bool Ok, string Message)> TransitionPieceAsync(
        Guid tenantId, Guid pieceId, Guid actorId, string toStatus,
        string entryReason, string entryNotes, bool syncExecutions,
        CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct).ConfigureAwait(false);

        var fresh = await db.ProductionPieces
            .FirstOrDefaultAsync(p => p.Id == pieceId, ct).ConfigureAwait(false);
        if (fresh is null)
        {
            return (false, "WorkItem not found");
        }
        if (fresh.Status == toStatus)
        {
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return (true, "Already in progress"); // WPF re-fetch idempotency
        }

        fresh.Status = toStatus;
        fresh.UpdatedAt = DateTime.UtcNow;
        fresh.UpdatedBy = actorId;

        db.ProductionEvents.Add(new ProductionEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductionPieceId = fresh.Id,
            EventType = ProductionEntryTypes.Stage,
            JobOrderId = fresh.JobOrderId,
            Stage = fresh.Stage,
            SpecGroupNumber = fresh.SpecGroupNumber,
            SerialNumber = fresh.SerialNumber,
            Position = fresh.Position,
            SpecName = fresh.SpecName,
            GlassRef = fresh.GlassRef,
            Qty = fresh.QtyDone,
            DoneBy = actorId,
            Reason = entryReason,
            Notes = entryNotes,
            CreatedAt = DateTime.UtcNow
        });

        if (syncExecutions)
        {
            await SyncLinkedExecutionsTrackedAsync(db, fresh, ct);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return (true, "Transitioned");
    }

    private static async Task SyncLinkedExecutionsTrackedAsync(
        SaasDbContext db, ProductionPiece piece, CancellationToken ct)
    {
        var linked = await db.ProcessExecutions
            .Where(e => e.ProductionPieceId == piece.Id && e.ReworkSequence == 0)
            .ToListAsync(ct).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        foreach (var exec in linked)
        {
            exec.QtyRequired = piece.Qty;
            exec.QtyDone = piece.QtyDone;
            bool frozen = exec.Status == WorkItemStatuses.Rework
                || exec.Status == WorkItemStatuses.Rejected
                || exec.Status == WorkItemStatuses.QcFailed
                || exec.Status == WorkItemStatuses.Cancelled;
            bool startedEmpty = exec.Status == WorkItemStatuses.InProgress
                && piece.Status == WorkItemStatuses.Pending;
            if (!frozen && !startedEmpty)
            {
                exec.Status = piece.Status;
            }
            if (exec.Status == WorkItemStatuses.InProgress && exec.StartedAt is null)
            {
                exec.StartedAt = now;
                exec.StartedBy = piece.UpdatedBy;
            }
            if (piece.Status == WorkItemStatuses.Completed)
            {
                exec.CompletedAt = piece.CompletedAt;
                exec.CompletedBy = piece.CompletedBy;
            }
            exec.UpdatedAt = now;
        }
    }

    // ── pairings ─────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ProcessPairing>> ListPairingsByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProcessPairings.AsNoTracking()
            .Where(p => p.JobOrderId == jobOrderId)
            .OrderBy(p => p.SpecGroupNumber)
            .ThenBy(p => p.SerialNumber)
            .ThenBy(p => p.PairId)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessPairing>> ListPairingsByUnitAsync(
        Guid tenantId, Guid jobOrderId, int specGroupNumber, int serialNumber,
        CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProcessPairings.AsNoTracking()
            .Where(p => p.JobOrderId == jobOrderId
                && p.SpecGroupNumber == specGroupNumber
                && p.SerialNumber == serialNumber)
            .OrderBy(p => p.PairId)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessPairing>> InsertPairingsAsync(
        Guid tenantId, Guid jobOrderId, int specGroupNumber, int serialNumber,
        IReadOnlyList<ProcessPairing> rows, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct).ConfigureAwait(false);

        // Re-check the gating inputs under the lock (WPF re-reads inside
        // the serializable transaction).
        var live = await db.ProductionPieces.AsNoTracking()
            .Where(p => p.JobOrderId == jobOrderId
                && p.SpecGroupNumber == specGroupNumber
                && p.SerialNumber == serialNumber
                && !p.IsCancelled)
            .ToListAsync(ct).ConfigureAwait(false);
        var outerCutting = live.FirstOrDefault(p =>
            p.Stage == ProductionStages.CuttingName && PairingAssemblyService.IsOuterPosition(p.Position));
        var innerCutting = live.FirstOrDefault(p =>
            p.Stage == ProductionStages.CuttingName && PairingAssemblyService.IsInnerPosition(p.Position));
        if ((outerCutting is not null && outerCutting.QtyDone < outerCutting.Qty)
            || (innerCutting is not null && innerCutting.QtyDone < innerCutting.Qty))
        {
            return Array.Empty<ProcessPairing>();
        }

        var existingIds = (await db.ProcessPairings.AsNoTracking()
                .Where(p => p.JobOrderId == jobOrderId
                    && p.SpecGroupNumber == specGroupNumber
                    && p.SerialNumber == serialNumber)
                .Select(p => p.PairingId)
                .ToListAsync(ct).ConfigureAwait(false))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (existingIds.Contains(row.PairingId)) continue; // idempotent
            db.ProcessPairings.Add(row);
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return await ListPairingsByUnitAsync(
            tenantId, jobOrderId, specGroupNumber, serialNumber, ct);
    }

    // ── assemblies ───────────────────────────────────────────────────

    public async Task<IReadOnlyList<ProcessAssembly>> ListAssembliesByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProcessAssemblies.AsNoTracking()
            .Where(a => a.JobOrderId == jobOrderId)
            .OrderBy(a => a.SpecGroupNumber)
            .ThenBy(a => a.SerialNumber)
            .ThenBy(a => a.AssemblyLabel)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessAssembly>> ListAssembliesByUnitAsync(
        Guid tenantId, Guid jobOrderId, int specGroupNumber, int serialNumber,
        CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProcessAssemblies.AsNoTracking()
            .Where(a => a.JobOrderId == jobOrderId
                && a.SpecGroupNumber == specGroupNumber
                && a.SerialNumber == serialNumber)
            .OrderBy(a => a.AssemblyLabel)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessAssembly>> InsertAssembliesAsync(
        Guid tenantId, Guid jobOrderId, int specGroupNumber, int serialNumber,
        IReadOnlyList<ProcessAssembly> rows, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct).ConfigureAwait(false);

        var existingIds = (await db.ProcessAssemblies.AsNoTracking()
                .Where(a => a.JobOrderId == jobOrderId
                    && a.SpecGroupNumber == specGroupNumber
                    && a.SerialNumber == serialNumber)
                .Select(a => a.AssemblyId)
                .ToListAsync(ct).ConfigureAwait(false))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (existingIds.Contains(row.AssemblyId)) continue; // idempotent
            db.ProcessAssemblies.Add(row);
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return await ListAssembliesByUnitAsync(
            tenantId, jobOrderId, specGroupNumber, serialNumber, ct);
    }

    // ── control holds ────────────────────────────────────────────────

    public async Task<ProductionControlHold?> FindHoldAsync(
        Guid tenantId, string holdId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProductionControlHolds.AsNoTracking()
            .FirstOrDefaultAsync(h => h.HoldId == holdId, ct).ConfigureAwait(false);
    }

    public async Task<ProductionControlHold?> FindActiveHoldByPieceAsync(
        Guid tenantId, Guid pieceId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProductionControlHolds.AsNoTracking()
            .FirstOrDefaultAsync(h => h.ProductionPieceId == pieceId && !h.IsReleased, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProductionControlHold>> ListHoldsByPieceAsync(
        Guid tenantId, Guid pieceId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProductionControlHolds.AsNoTracking()
            .Where(h => h.ProductionPieceId == pieceId)
            .OrderByDescending(h => h.HeldAt)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> CountHoldsByPieceAsync(
        Guid tenantId, Guid pieceId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProductionControlHolds.AsNoTracking()
            .CountAsync(h => h.ProductionPieceId == pieceId, ct).ConfigureAwait(false);
    }

    public async Task InsertHoldAsync(
        Guid tenantId, ProductionControlHold hold, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        db.ProductionControlHolds.Add(hold);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> TryUpdateHoldAsync(
        ProductionControlHold hold, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(hold.TenantId);
        var stored = await db.ProductionControlHolds
            .FirstOrDefaultAsync(h => h.Id == hold.Id, ct).ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        stored.IsReleased = hold.IsReleased;
        stored.ReleasedBy = hold.ReleasedBy;
        stored.ReleasedAt = hold.ReleasedAt;
        stored.Notes = hold.Notes;
        stored.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    // ── assignments ──────────────────────────────────────────────────

    public async Task<IReadOnlyList<ProductionControlAssignment>> ListAssignmentsByPieceAsync(
        Guid tenantId, Guid pieceId, bool activeOnly, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var query = db.ProductionControlAssignments.AsNoTracking()
            .Where(a => a.ProductionPieceId == pieceId);
        if (activeOnly)
        {
            query = query.Where(a => a.IsActive);
        }
        return await query
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> CountAssignmentsByPieceAsync(
        Guid tenantId, Guid pieceId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProductionControlAssignments.AsNoTracking()
            .CountAsync(a => a.ProductionPieceId == pieceId, ct).ConfigureAwait(false);
    }

    public async Task<(bool Ok, string Message, string AssignmentId)> AssignAsync(
        Guid tenantId, ProductionPiece piece, string workstationId,
        string operatorName, string notes, Guid actorId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, ct).ConfigureAwait(false);

        // Idempotency: same workstation + operator already active.
        var sameActive = await db.ProductionControlAssignments
            .FirstOrDefaultAsync(a => a.ProductionPieceId == piece.Id
                && a.IsActive
                && a.WorkstationId == workstationId
                && a.OperatorName == operatorName, ct)
            .ConfigureAwait(false);
        if (sameActive is not null)
        {
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return (true, "Already assigned — idempotent", sameActive.AssignmentId);
        }

        // Deactivate previous actives (history preserved, never deleted).
        var prevActives = await db.ProductionControlAssignments
            .Where(a => a.ProductionPieceId == piece.Id && a.IsActive)
            .ToListAsync(ct).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        foreach (var prev in prevActives)
        {
            prev.IsActive = false;
            prev.UnassignedBy = actorId;
            prev.UnassignedAt = now;
            prev.UpdatedAt = now;
        }

        int seq = await db.ProductionControlAssignments.AsNoTracking()
            .CountAsync(a => a.ProductionPieceId == piece.Id, ct).ConfigureAwait(false);
        var jobRef = await db.JobOrders.AsNoTracking()
            .Where(j => j.Id == piece.JobOrderId)
            .Select(j => j.JobNo)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false) ?? "";
        string assignmentId = ProcessRouting.BuildAssignmentId(
            jobRef, piece.SpecGroupNumber, piece.SerialNumber,
            ProductionStages.Code(piece.Stage), seq);

        db.ProductionControlAssignments.Add(new ProductionControlAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AssignmentId = assignmentId,
            ProductionPieceId = piece.Id,
            JobOrderId = piece.JobOrderId,
            JobRef = jobRef,
            SpecGroupNumber = piece.SpecGroupNumber,
            SerialNumber = piece.SerialNumber,
            Position = piece.Position,
            Stage = piece.Stage,
            WorkstationId = workstationId,
            OperatorName = operatorName,
            AssignedBy = actorId,
            AssignedAt = now,
            IsActive = true,
            Notes = notes,
            CreatedAt = now,
            UpdatedAt = now
        });

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return (true, "Assigned", assignmentId);
    }

    public async Task<(bool Ok, string Message)> UnassignAsync(
        Guid tenantId, Guid pieceId, Guid actorId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var actives = await db.ProductionControlAssignments
            .Where(a => a.ProductionPieceId == pieceId && a.IsActive)
            .ToListAsync(ct).ConfigureAwait(false);
        if (actives.Count == 0)
        {
            return (true, "No active assignment — idempotent");
        }

        var now = DateTime.UtcNow;
        foreach (var a in actives)
        {
            a.IsActive = false;
            a.UnassignedBy = actorId;
            a.UnassignedAt = now;
            a.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return (true, "Unassigned");
    }

    // ── overrides ────────────────────────────────────────────────────

    public async Task<ProductionControlOverride?> FindOverrideAsync(
        Guid tenantId, string controlId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProductionControlOverrides.AsNoTracking()
            .FirstOrDefaultAsync(o => o.ControlId == controlId, ct).ConfigureAwait(false);
    }

    public async Task<ProductionControlOverride> UpsertOverrideAsync(
        Guid tenantId, ProductionControlOverride overrideRow, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var stored = await db.ProductionControlOverrides
            .FirstOrDefaultAsync(o => o.ControlId == overrideRow.ControlId, ct)
            .ConfigureAwait(false);
        if (stored is null)
        {
            db.ProductionControlOverrides.Add(overrideRow);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return overrideRow;
        }

        stored.ProductionPieceId = overrideRow.ProductionPieceId;
        stored.Priority = overrideRow.Priority;
        stored.DueDate = overrideRow.DueDate;
        stored.PrioritySetBy = overrideRow.PrioritySetBy;
        stored.PrioritySetAt = overrideRow.PrioritySetAt;
        stored.Notes = overrideRow.Notes;
        stored.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return stored;
    }

    // ── workstations ─────────────────────────────────────────────────

    public async Task<IReadOnlyList<Workstation>> ListWorkstationsAsync(
        Guid tenantId, bool activeOnly, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var query = db.Workstations.AsNoTracking().AsQueryable();
        if (activeOnly)
        {
            query = query.Where(w => w.IsActive);
        }
        return await query
            .OrderBy(w => w.ProcessType).ThenBy(w => w.Code)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<Workstation?> FindWorkstationAsync(
        Guid tenantId, string workstationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(workstationId)) return null;
        using var db = factory.CreateForTenant(tenantId);
        return await db.Workstations.AsNoTracking()
            .FirstOrDefaultAsync(w => w.WorkstationId == workstationId, ct)
            .ConfigureAwait(false);
    }

    public async Task<Workstation> CreateWorkstationAsync(
        Guid tenantId, string code, string name, int processType,
        string description, Guid? actorId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var existing = await db.Workstations
            .FirstOrDefaultAsync(w => w.WorkstationId == code, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var ws = new Workstation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkstationId = code,
            Code = code,
            Name = name,
            ProcessType = processType,
            ProcessName = ProductionStages.Name(processType),
            IsActive = true,
            Description = description,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Workstations.Add(ws);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return ws;
    }

    /// <summary>WPF EnsureSeededAsync — the ten default stations, only
    /// when the tenant has none.</summary>
    public async Task<int> EnsureWorkstationsSeededAsync(
        Guid tenantId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var existing = await db.Workstations.AsNoTracking()
            .CountAsync(ct).ConfigureAwait(false);
        if (existing > 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        var defaults = new List<Workstation>
        {
            New(tenantId, "CUT-01", "Cutting Station 01", 1, "Primary cutting table", now),
            New(tenantId, "CUT-02", "Cutting Station 02", 1, "Secondary cutting table", now),
            New(tenantId, "CNC-01", "CNC Holes/Cutouts 01", 2, "CNC for holes and cutouts", now),
            New(tenantId, "POLISH-01", "Polish Station 01", 3, "Edge polish", now),
            New(tenantId, "EDGEDEL-01", "Edge Deletion 01", 4, "Edge deletion", now),
            New(tenantId, "MITRING-01", "Mitring Station 01", 5, "Mitring", now),
            New(tenantId, "TEMPER-01", "Tempering Furnace 01", 6, "Tempering furnace", now),
            New(tenantId, "PAIR-01", "Pairing Station 01", 7, "DGU pairing", now),
            New(tenantId, "LAM-01", "Lamination Station 01", 8, "Doubling/Lamination", now),
            New(tenantId, "DGU-01", "DGU Assembly 01", 8, "DGU doubling", now)
        };
        db.Workstations.AddRange(defaults);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return defaults.Count;

        static Workstation New(Guid tenantId, string code, string name,
            int processType, string description, DateTime now) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkstationId = code,
            Code = code,
            Name = name,
            ProcessType = processType,
            ProcessName = ProductionStages.Name(processType),
            IsActive = true,
            Description = description,
            CreatedBy = null,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    // ── the queue bundle ─────────────────────────────────────────────

    public async Task<ProductionQueueData> LoadQueueDataAsync(
        Guid tenantId, Guid? jobOrderId, bool includeCancelled,
        bool includeCompleted, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);

        var piecesQuery = db.ProductionPieces.AsNoTracking().AsQueryable();
        if (!includeCancelled)
        {
            piecesQuery = piecesQuery.Where(p => !p.IsCancelled);
        }
        if (!includeCompleted)
        {
            piecesQuery = piecesQuery.Where(
                p => p.Status != WorkItemStatuses.Completed || p.QtyDone < p.Qty);
        }
        if (jobOrderId is { } job)
        {
            piecesQuery = piecesQuery.Where(p => p.JobOrderId == job);
        }

        var pieces = await piecesQuery
            .OrderBy(p => p.JobOrderId)
            .ThenBy(p => p.SpecGroupNumber)
            .ThenBy(p => p.SerialNumber)
            .ThenBy(p => p.Position)
            .ToListAsync(ct).ConfigureAwait(false);
        // Deterministic stage ordering (in memory — ProductionStages.Code
        // is a Domain mapping EF cannot translate).
        pieces = pieces
            .OrderBy(p => p.JobOrderId)
            .ThenBy(p => p.SpecGroupNumber)
            .ThenBy(p => p.SerialNumber)
            .ThenBy(p => p.Position)
            .ThenBy(p => ProductionStages.Code(p.Stage))
            .ToList();

        var jobIds = pieces.Select(p => p.JobOrderId).Distinct().ToList();
        if (jobIds.Count == 0)
        {
            return new ProductionQueueData(
                pieces, Array.Empty<JobOrder>(),
                Array.Empty<MaterialReservation>(),
                Array.Empty<ProductionControlHold>(),
                Array.Empty<ProductionControlAssignment>(),
                Array.Empty<ProductionControlOverride>(),
                Array.Empty<Workstation>(),
                Array.Empty<ProductionEvent>(),
                Array.Empty<ProcessPairing>(),
                Array.Empty<ProcessAssembly>());
        }

        var jobs = await db.JobOrders.AsNoTracking()
            .Include(j => j.Salesman)
            .Where(j => jobIds.Contains(j.Id))
            .ToListAsync(ct).ConfigureAwait(false);

        var reservations = await db.MaterialReservations.AsNoTracking()
            .Where(r => jobIds.Contains(r.JobOrderId))
            .ToListAsync(ct).ConfigureAwait(false);

        var holds = await db.ProductionControlHolds.AsNoTracking()
            .Where(h => jobIds.Contains(h.JobOrderId) && !h.IsReleased)
            .ToListAsync(ct).ConfigureAwait(false);

        var assignments = await db.ProductionControlAssignments.AsNoTracking()
            .Where(a => jobIds.Contains(a.JobOrderId) && a.IsActive)
            .ToListAsync(ct).ConfigureAwait(false);

        var overrides = await db.ProductionControlOverrides.AsNoTracking()
            .Where(o => jobIds.Contains(o.JobOrderId))
            .ToListAsync(ct).ConfigureAwait(false);

        var workstations = await db.Workstations.AsNoTracking()
            .ToListAsync(ct).ConfigureAwait(false);

        // The typed QC events feed the readiness engine's QC gate.
        var qcEvents = await db.ProductionEvents.AsNoTracking()
            .Where(e => jobIds.Contains(e.JobOrderId)
                && (e.EventType == ProductionEntryTypes.QcPassed
                    || e.EventType == ProductionEntryTypes.QcFailed
                    || e.EventType == ProductionEntryTypes.Rework))
            .ToListAsync(ct).ConfigureAwait(false);

        var pairings = await db.ProcessPairings.AsNoTracking()
            .Where(p => jobIds.Contains(p.JobOrderId))
            .ToListAsync(ct).ConfigureAwait(false);

        var assemblies = await db.ProcessAssemblies.AsNoTracking()
            .Where(a => jobIds.Contains(a.JobOrderId))
            .ToListAsync(ct).ConfigureAwait(false);

        return new ProductionQueueData(
            pieces, jobs, reservations, holds, assignments, overrides,
            workstations, qcEvents, pairings, assemblies);
    }

    private static RouteWorkItem ToRoute(ProductionPiece p, string jobRef) => new(
        jobRef,
        jobRef,
        p.SpecGroupNumber,
        p.SpecName,
        p.SerialNumber,
        p.Position,
        p.BuildType,
        p.GlassRef,
        p.Stage,
        p.Status,
        p.IsCancelled,
        p.Qty,
        p.QtyDone,
        p.UniqueKey,
        p.WidthMm,
        p.HeightMm,
        p.ReplacementSequence);
}
