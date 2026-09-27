// ═══════════════════════════════════════════════════════════════════════
//  FILE: ProGlass.Saas.Application/Production/ProductionIntegrityService.cs
//  PORTED from the WPF application (read 2026-09-27):
//    • Services/Production/ProductionInventoryConsistencyScanner.cs —
//      read-only Production ↔ Sheet Store consistency scan. NEVER writes,
//      NEVER calls UseStock, NEVER releases holds, NEVER treats
//      Consumed-without-Used as a defect (planning consume ≠ physical
//      Used is the product contract). Findings: UsedExceedsStock,
//      MissingGlassSheet, ActiveHoldExceedsPhysical,
//      AllocationMissingReservation, AllocConsumedReservationReleased,
//      ReservationWithoutRelease, PlanningConsumedNoMatchingHistory
//      (observation), RetryVsNewConsumption (not detectable).
//    • Services/Production/ProductionSnapshotIntegrity.cs — Missing /
//      Corrupt / ValidEmpty / Valid over the JO snapshot so Production
//      never treats corrupt JSON as "a job with no glass".
//  SaaS differences (documented, mapping §1.7d): the WPF's two SQLite
//  files are one database here — the "not one transaction" limitation
//  becomes "point-in-time read"; the WPF's SheetHistories rows map to
//  stock_transactions (item_type 'sheet', transaction_type 'issue');
//  the WPF's Releases table is the job_orders row — a reservation whose
//  job has no production pieces is the without-release finding; the JO
//  snapshot in the SaaS is a conversion header, so ValidEmpty/Valid read
//  the source PI's Step 5 grid (W1>0/H1>0) instead of the frozen
//  embedded invoice.
// ═══════════════════════════════════════════════════════════════════════

using System.Text.Json;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Production;

public sealed class ProductionIntegrityService(
    ICuttingCostStore store, IProductionStore production)
{
    // ── the scanner (WPF ScanAsync — read-only) ──────────────────────

    /// <summary>
    /// GET /production/consistency-scan — the whole-tenant point-in-time
    /// consistency report. Never writes.
    /// </summary>
    public async Task<ProductionResult<ConsistencyScanDto>> ScanAsync(
        Guid tenantId, CancellationToken ct)
    {
        var reservations = await store.ListAllReservationsAsync(tenantId, ct);
        var allocations = await store.ListAllAllocationsAsync(tenantId, ct);
        var sheets = await store.ListAllGlassSheetsAsync(tenantId, ct);
        var history = await store.ListSheetHistoryAsync(tenantId, ct);
        var releasedJobIds = await store.ListProductionPieceJobIdsAsync(tenantId, ct);
        var releasedJobs = new HashSet<Guid>(releasedJobIds);

        var jobs = new Dictionary<Guid, JobOrder>();
        foreach (var jobId in reservations.Select(r => r.JobOrderId).Distinct())
        {
            var job = await production.FindJobAsync(tenantId, jobId, ct);
            if (job is not null) jobs[jobId] = job;
        }

        var findings = new List<ConsistencyFindingDto>();
        var sheetById = sheets.ToDictionary(s => s.Id);

        var report = new ConsistencyScanDto(
            DateTime.UtcNow,
            reservations.Count,
            allocations.Count,
            sheets.Count,
            reservations
                .Where(r => ReservationStatuses.IsActiveHold(r.Status))
                .Sum(r => r.ReservedQty),
            reservations
                .Where(r => string.Equals(r.Status, ReservationStatuses.Consumed,
                    StringComparison.OrdinalIgnoreCase))
                .Sum(r => r.ReservedQty),
            findings,
            new[]
            {
                "Cannot distinguish a retried sheet issue from a new legitimate use — no physical EventId.",
                "Cannot prove Consumed-without-Issue is an error — Consumed is planning-only.",
                "Cannot join a stock issue row to a ReservationKey — the transaction notes carry the job, not the reservation.",
                "Cannot auto-repair; this scanner never writes.",
                "Stale Reserved cannot be proven without TTL (none exists). Visibility only.",
                "Single database, but every table is read at its own instant — the scan is a point-in-time read, not a snapshot."
            });

        void Add(ConsistencyKind kind, string code, string message,
            Guid? sheetId = null, string? reservationKey = null,
            string? allocationId = null, string? jobRef = null, int quantity = 0) =>
            findings.Add(new ConsistencyFindingDto(
                kind.ToString(), code, message, sheetId, reservationKey,
                allocationId, jobRef, quantity));

        // UsedExceedsStock.
        foreach (var s in sheets)
        {
            if (s.Used > s.Stock)
            {
                Add(ConsistencyKind.Fact, ConsistencyCodes.UsedExceedsStock,
                    $"Sheet {s.Id}: Used {s.Used} > Stock {s.Stock}.",
                    sheetId: s.Id, quantity: s.Used - s.Stock);
            }
        }

        // ActiveHoldExceedsPhysical / MissingGlassSheet.
        var holdBySheet = reservations
            .Where(r => ReservationStatuses.IsActiveHold(r.Status))
            .GroupBy(r => r.GlassSheetId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.ReservedQty));

        foreach (var kv in holdBySheet)
        {
            if (!sheetById.TryGetValue(kv.Key, out var sheet))
            {
                Add(ConsistencyKind.Fact, ConsistencyCodes.MissingGlassSheet,
                    $"Active hold {kv.Value} on missing GlassSheetId {kv.Key}.",
                    sheetId: kv.Key, quantity: kv.Value);
                continue;
            }

            int physical = sheet.Stock - sheet.Used;
            if (kv.Value > physical)
            {
                Add(ConsistencyKind.Fact, ConsistencyCodes.ActiveHoldExceedsPhysical,
                    $"Sheet {kv.Key}: planning hold {kv.Value} > PhysicalAvailable " +
                    $"{physical} (Stock {sheet.Stock} Used {sheet.Used}).",
                    sheetId: kv.Key, quantity: kv.Value);
            }
        }

        // AllocationMissingReservation.
        var reservationKeys = reservations
            .Select(r => r.ReservationKey)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var a in allocations)
        {
            if (!reservationKeys.Contains(a.ReservationKey))
            {
                Add(ConsistencyKind.Fact, ConsistencyCodes.AllocationMissingReservation,
                    $"Allocation {a.AllocationId} references missing reservation " +
                    $"{a.ReservationKey}.",
                    reservationKey: a.ReservationKey,
                    allocationId: a.AllocationId,
                    jobRef: a.JobRef);
            }
        }

        // AllocConsumedReservationReleased.
        var resByKey = reservations.ToDictionary(
            r => r.ReservationKey, r => r, StringComparer.Ordinal);
        foreach (var a in allocations.Where(x => string.Equals(x.Status,
                     AllocationStatuses.Consumed, StringComparison.OrdinalIgnoreCase)))
        {
            if (!resByKey.TryGetValue(a.ReservationKey, out var res)) continue;
            if (string.Equals(res.Status, ReservationStatuses.Released,
                    StringComparison.OrdinalIgnoreCase))
            {
                Add(ConsistencyKind.Fact, ConsistencyCodes.AllocConsumedReservationReleased,
                    $"Allocation {a.AllocationId} is Consumed while reservation " +
                    $"{res.ReservationKey} is Released.",
                    sheetId: res.GlassSheetId,
                    reservationKey: res.ReservationKey,
                    allocationId: a.AllocationId,
                    jobRef: res.ReservationKey.Length > 0
                        ? jobs.TryGetValue(res.JobOrderId, out var j) ? j.JobNo : ""
                        : "");
            }
        }

        // ReservationWithoutRelease (the WPF's Releases table = the SaaS
        // job's production pieces).
        foreach (var r in reservations)
        {
            if (!releasedJobs.Contains(r.JobOrderId))
            {
                Add(ConsistencyKind.Fact, ConsistencyCodes.ReservationWithoutRelease,
                    $"Reservation {r.ReservationKey} job {r.JobOrderId} has no " +
                    "released production work items.",
                    sheetId: r.GlassSheetId,
                    reservationKey: r.ReservationKey,
                    jobRef: jobs.TryGetValue(r.JobOrderId, out var j) ? j.JobNo : "");
            }
        }

        // Observation only: Consumed rows exist; physical Issue may not.
        // Current contract — Consumed does not write Used/Issue.
        int consumedNoHistory = 0;
        foreach (var r in reservations.Where(x => string.Equals(x.Status,
                     ReservationStatuses.Consumed, StringComparison.OrdinalIgnoreCase)))
        {
            string jobNo = jobs.TryGetValue(r.JobOrderId, out var j) ? j.JobNo : "";
            bool tagged = history.Any(h =>
                h.SheetId == r.GlassSheetId
                && string.Equals(h.Action, "issue", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(h.DocumentReference)
                && (h.DocumentReference.IndexOf(jobNo, StringComparison.OrdinalIgnoreCase) >= 0
                    || h.DocumentReference.IndexOf(r.ReservationKey, StringComparison.Ordinal) >= 0));
            if (!tagged) consumedNoHistory++;
        }

        if (consumedNoHistory > 0)
        {
            Add(ConsistencyKind.Observation,
                ConsistencyCodes.PlanningConsumedNoMatchingHistory,
                $"{consumedNoHistory} Consumed reservation(s) have no sheet issue " +
                "row tagged with that job. Not proof of missing physical use — " +
                "Consumed does not write Used, and issue notes are often empty.",
                quantity: consumedNoHistory);
        }

        Add(ConsistencyKind.NotDetectable, ConsistencyCodes.RetryVsNewConsumption,
            "Sheet issue has no per-event key; only the Stock-Used CAS guard.");

        return ProductionResult<ConsistencyScanDto>.Ok(report);
    }

    // ── snapshot integrity (WPF ReadJobOrderJson) ────────────────────

    /// <summary>
    /// GET /production/snapshot-integrity?jobOrderId= — Missing / Corrupt
    /// / ValidEmpty / Valid so Production never treats corrupt JSON (or a
    /// job with no glass) as the other one.
    /// </summary>
    public async Task<ProductionResult<SnapshotIntegrityDto>> CheckSnapshotAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        var job = await production.FindJobAsync(tenantId, jobOrderId, ct);
        if (job is null)
        {
            return ProductionResult<SnapshotIntegrityDto>.Fail(
                ProductionErrorCode.NotFound,
                $"Job order {jobOrderId} was not found.");
        }

        var (kind, message, hasCutRows) = await ClassifySnapshotAsync(
            tenantId, job, ct);

        return ProductionResult<SnapshotIntegrityDto>.Ok(new SnapshotIntegrityDto(
            job.Id, job.JobNo, kind.ToString(), message, hasCutRows));
    }

    internal async Task<(ProductionSnapshotKind Kind, string Message, bool HasCutRows)>
        ClassifySnapshotAsync(Guid tenantId, JobOrder job, CancellationToken ct)
    {
        // Missing: no snapshot at all.
        if (string.IsNullOrWhiteSpace(job.SnapshotJson))
        {
            return (ProductionSnapshotKind.Missing, "snapshot is missing", false);
        }

        // Corrupt: not valid JSON. The SaaS snapshot is a conversion
        // header (the WPF's is the full embedded invoice — documented
        // difference), but corrupt is corrupt either way.
        try
        {
            using var _ = JsonDocument.Parse(job.SnapshotJson);
        }
        catch (JsonException)
        {
            return (ProductionSnapshotKind.Corrupt,
                "snapshot JSON is corrupt — not a valid empty job", false);
        }

        // ValidEmpty vs Valid: does the source PI have cut rows
        // (W1 > 0 and H1 > 0)? The WPF reads the frozen embedded invoice;
        // the SaaS reads the live Step 5 grid its snapshot header points
        // at (mapping §1.7d).
        var specs = await production.GetSpecInputsAsync(
            tenantId, job.ProformaId, ct);
        bool hasCutRow = specs.Any(g =>
            g.Rows.Any(r => r.W1 > 0 && r.H1 > 0));

        if (!hasCutRow)
        {
            return (ProductionSnapshotKind.ValidEmpty,
                "no specification rows", false);
        }

        return (ProductionSnapshotKind.Valid, string.Empty, true);
    }
}
