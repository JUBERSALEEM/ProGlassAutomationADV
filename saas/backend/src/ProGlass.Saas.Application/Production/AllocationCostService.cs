// ═══════════════════════════════════════════════════════════════════════
//  FILE: ProGlass.Saas.Application/Production/AllocationCostService.cs
//  PORTED from the WPF application (read 2026-09-27):
//    • Services/Production/MaterialAllocationService.cs — AllocateAsync
//      (idempotent on AllocationId = ReservationId|S{sheetNo}|{label};
//      live reservation required: Cancelled/Released/Consumed refused;
//      Reserved → Allocated), ConsumeAsync (idempotent; Cancelled/Released
//      refused; the reservation flips to Consumed only when ALL its
//      allocations are consumed, never overwriting Released/Cancelled),
//      GetByJob / GetByReservation.
//    • Services/Production/ActualCostService.cs — RecordCostAsync
//      (idempotent update — quantity/cost/selling refresh as consumption
//      progresses; FOC zeroes selling; the PI estimate is never
//      overwritten), GetByJob, GetSummaryByJob (TotalActual falls back to
//      the component sum; FocImpact = cost incurred with selling zero),
//      DeriveFromAllocationAsync (per allocation: piece sqm × the rack's
//      purchase price; unknown price → 0, allocation preserved).
//  SaaS differences (documented, mapping §1.7d): GUID ids, TEXT status
//  vocabularies; the WPF's cross-DB GlassSheet price read is a plain
//  single-DB lookup in the SaaS.
// ═══════════════════════════════════════════════════════════════════════

using System.Text.Json;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Production;

public sealed class AllocationCostService(
    ICuttingCostStore store, IReservationStore reservations,
    IProductionStore production, IAuditWriter audit)
{
    // ── Allocate (WPF AllocateAsync) ─────────────────────────────────

    /// <summary>
    /// POST /allocations — allocate a placed piece to a reservation.
    /// Idempotent on the deterministic AllocationId; the reservation must
    /// be live (Cancelled/Released/Consumed refused) and flips Reserved →
    /// Allocated.
    /// </summary>
    public async Task<ProductionResult<AllocationDto>> AllocateAsync(
        Guid tenantId, Guid actorId, AllocateRequest request,
        string? ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ReservationKey))
        {
            return ProductionResult<AllocationDto>.Fail(
                ProductionErrorCode.ValidationFailed, "ReservationKey required.");
        }
        // WPF AllocateAsync: quantity <= 0 becomes 1.
        int quantity = request.Quantity <= 0 ? 1 : request.Quantity;

        string allocationId = CuttingIds.BuildAllocationId(
            request.ReservationKey, request.SheetNo, request.PlacedPieceLabel);

        var existing = await store.FindAllocationAsync(tenantId, allocationId, ct);
        if (existing is not null)
        {
            return ProductionResult<AllocationDto>.Ok(ToDto(existing),
                "Allocation already exists (idempotent).");
        }

        var reservation = await reservations.FindByKeyAsync(
            tenantId, request.ReservationKey, ct);
        if (reservation is null)
        {
            return ProductionResult<AllocationDto>.Fail(
                ProductionErrorCode.NotFound,
                $"Reservation {request.ReservationKey} not found.");
        }
        if (!AllocationStatuses.CanAllocate(reservation.Status))
        {
            return ProductionResult<AllocationDto>.Fail(
                ProductionErrorCode.Conflict,
                $"Reservation {request.ReservationKey} is {reservation.Status} — cannot allocate.");
        }

        var allocation = new MaterialAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AllocationId = allocationId,
            ReservationId = reservation.Id,
            ReservationKey = request.ReservationKey,
            JobOrderId = request.JobOrderId,
            JobRef = "",
            SpecGroupNumber = request.SpecGroupNumber,
            SerialNumber = request.SerialNumber,
            OptimizationSessionId = request.OptimizationSessionId,
            SheetNo = request.SheetNo,
            PlacedPieceLabel = request.PlacedPieceLabel ?? string.Empty,
            GlassRef = request.GlassRef ?? string.Empty,
            WidthMm = request.WidthMm,
            HeightMm = request.HeightMm,
            Quantity = quantity,
            UtilizationPct = request.UtilizationPct,
            WastagePct = request.WastagePct,
            Status = AllocationStatuses.Allocated,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow,
            Notes = request.Notes ?? string.Empty
        };

        if (await store.InsertAllocationAsync(tenantId, allocation, ct) == 0)
        {
            // Unique race — the WPF re-reads and returns the raced row.
            var raced = await store.FindAllocationAsync(tenantId, allocationId, ct);
            if (raced is not null)
            {
                return ProductionResult<AllocationDto>.Ok(ToDto(raced),
                    "Allocation already exists (race idempotent).");
            }
            return ProductionResult<AllocationDto>.Fail(
                ProductionErrorCode.Conflict, "Allocation conflict (unique).");
        }

        // Move the reservation to Allocated if it was Reserved (WPF).
        if (string.Equals(reservation.Status, ReservationStatuses.Reserved,
                StringComparison.OrdinalIgnoreCase))
        {
            reservation.Status = ReservationStatuses.Allocated;
            reservation.UpdatedAt = DateTime.UtcNow;
            await reservations.TryUpdateAsync(reservation, ct);
        }

        await AuditAsync(tenantId, actorId, "production.allocation_allocate",
            "material_allocations", allocation.Id, null, new
            {
                allocationId,
                reservationKey = request.ReservationKey,
                sheetNo = request.SheetNo,
                placedPieceLabel = request.PlacedPieceLabel
            }, ip, ct);

        return ProductionResult<AllocationDto>.Ok(ToDto(allocation), "Allocated.");
    }

    // ── Consume (WPF ConsumeAsync) ───────────────────────────────────

    /// <summary>
    /// POST /allocations/{id}/consume — planning consume: Cancelled and
    /// Released allocations are refused; when every allocation of the
    /// reservation is consumed, the reservation flips to Consumed (never
    /// overwriting Released/Cancelled). Idempotent.
    /// </summary>
    public async Task<ProductionResult<string>> ConsumeAsync(
        Guid tenantId, Guid actorId, string allocationId, string? ip,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(allocationId))
        {
            return ProductionResult<string>.Fail(
                ProductionErrorCode.ValidationFailed, "AllocationId required.");
        }

        var allocation = await store.FindAllocationAsync(tenantId, allocationId, ct);
        if (allocation is null)
        {
            return ProductionResult<string>.Fail(
                ProductionErrorCode.NotFound, "Allocation not found.");
        }

        if (string.Equals(allocation.Status, AllocationStatuses.Consumed,
                StringComparison.OrdinalIgnoreCase))
        {
            return ProductionResult<string>.Ok("Already consumed (idempotent).");
        }

        if (!AllocationStatuses.CanConsume(allocation.Status))
        {
            return ProductionResult<string>.Fail(
                ProductionErrorCode.Conflict,
                $"Cannot consume {allocation.Status} allocation.");
        }

        allocation.Status = AllocationStatuses.Consumed;
        allocation.ConsumedAt = DateTime.UtcNow;
        if (!await store.TryUpdateAllocationAsync(allocation, ct))
        {
            return ProductionResult<string>.Fail(
                ProductionErrorCode.ConcurrencyConflict,
                "The allocation was modified by someone else — reload and retry.");
        }

        await CascadeReservationConsumedAsync(
            tenantId, allocation.ReservationKey, ct);

        await AuditAsync(tenantId, actorId, "production.allocation_consume",
            "material_allocations", allocation.Id, null,
            new { allocationId }, ip, ct);

        return ProductionResult<string>.Ok("Consumed.");
    }

    /// <summary>WPF cascade: reservation → Consumed only when ALL its
    /// allocations are consumed, and never overwriting
    /// Released/Cancelled.</summary>
    internal async Task CascadeReservationConsumedAsync(
        Guid tenantId, string reservationKey, CancellationToken ct)
    {
        var reservation = await reservations.FindByKeyAsync(tenantId, reservationKey, ct);
        if (reservation is null) return;
        if (string.Equals(reservation.Status, ReservationStatuses.Consumed,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(reservation.Status, ReservationStatuses.Released,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(reservation.Status, ReservationStatuses.Cancelled,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var allAllocations = await store.ListAllocationsByReservationAsync(
            tenantId, reservationKey, ct);
        bool allConsumed = allAllocations.Count > 0 && allAllocations.All(
            a => string.Equals(a.Status, AllocationStatuses.Consumed,
                StringComparison.OrdinalIgnoreCase));
        if (allConsumed)
        {
            reservation.Status = ReservationStatuses.Consumed;
            reservation.UpdatedAt = DateTime.UtcNow;
            await reservations.TryUpdateAsync(reservation, ct);
        }
    }

    /// <summary>GET /allocations?jobOrderId= — WPF GetByJobAsync order
    /// (SheetNo, PlacedPieceLabel).</summary>
    public async Task<ProductionResult<IReadOnlyList<AllocationDto>>> ListByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        var rows = await store.ListAllocationsByJobAsync(tenantId, jobOrderId, ct);
        return ProductionResult<IReadOnlyList<AllocationDto>>.Ok(
            rows.Select(ToDto).ToList());
    }

    /// <summary>GET /allocations?reservationKey= — WPF
    /// GetByReservationAsync.</summary>
    public async Task<ProductionResult<IReadOnlyList<AllocationDto>>> ListByReservationAsync(
        Guid tenantId, string reservationKey, CancellationToken ct)
    {
        var rows = await store.ListAllocationsByReservationAsync(
            tenantId, reservationKey, ct);
        return ProductionResult<IReadOnlyList<AllocationDto>>.Ok(
            rows.Select(ToDto).ToList());
    }

    // ── RecordCost (WPF RecordCostAsync) ─────────────────────────────

    /// <summary>
    /// POST /costs — record an actual cost. Idempotent: an existing row is
    /// UPDATED (actuals change as consumption progresses); FOC keeps the
    /// cost and zeroes the selling value. The PI estimate is never
    /// overwritten — Estimated is just another bucket.
    /// </summary>
    public async Task<ProductionResult<ActualCostDto>> RecordCostAsync(
        Guid tenantId, Guid actorId, RecordCostRequest request,
        string? ip, CancellationToken ct)
    {
        if (request.JobOrderId == Guid.Empty)
        {
            return ProductionResult<ActualCostDto>.Fail(
                ProductionErrorCode.ValidationFailed, "JobOrderId required.");
        }
        if (!ActualCostTypes.All.Contains(request.CostType?.Trim(),
                StringComparer.Ordinal))
        {
            return ProductionResult<ActualCostDto>.Fail(
                ProductionErrorCode.ValidationFailed,
                $"'{request.CostType}' is not an actual cost type " +
                $"({string.Join(", ", ActualCostTypes.All)}).");
        }

        var job = await production.FindJobAsync(tenantId, request.JobOrderId, ct);
        if (job is null)
        {
            return ProductionResult<ActualCostDto>.Fail(
                ProductionErrorCode.NotFound,
                $"Job order {request.JobOrderId} was not found.");
        }

        string costType = request.CostType.Trim();
        bool hasAllocation = !string.IsNullOrWhiteSpace(request.MaterialAllocationId);
        string costId = CuttingIds.BuildCostId(
            job.JobNo, request.SpecGroupNumber, request.SerialNumber, costType,
            hasAllocation ? request.MaterialAllocationId : null, request.IsFoc);

        decimal selling = request.IsFoc ? 0m : request.SellingValue;

        var existing = await store.FindCostAsync(tenantId, costId, ct);
        if (existing is not null)
        {
            existing.Quantity = request.Quantity;
            existing.UnitCost = request.UnitCost;
            existing.TotalCost = request.Quantity * request.UnitCost;
            existing.SellingValue = selling;
            existing.IsFoc = request.IsFoc;
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                existing.Notes = request.Notes;
            }
            if (!await store.TryUpdateCostAsync(existing, ct))
            {
                return ProductionResult<ActualCostDto>.Fail(
                    ProductionErrorCode.ConcurrencyConflict,
                    "The cost row was modified by someone else — reload and retry.");
            }
            return ProductionResult<ActualCostDto>.Ok(ToDto(existing),
                "Cost already exists — updated (idempotent).");
        }

        var cost = new ActualCost
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CostId = costId,
            JobOrderId = job.Id,
            JobRef = job.JobNo,
            SourceProformaId = job.ProformaId.ToString(),
            SpecGroupNumber = request.SpecGroupNumber,
            SerialNumber = request.SerialNumber,
            GlassRef = request.GlassRef ?? string.Empty,
            SpecName = request.SpecName ?? string.Empty,
            CostType = costType,
            StockSheetId = request.StockSheetId,
            MaterialAllocationId = request.MaterialAllocationId ?? string.Empty,
            Quantity = request.Quantity,
            UnitCost = request.UnitCost,
            TotalCost = request.Quantity * request.UnitCost,
            SellingValue = selling,
            IsFoc = request.IsFoc,
            CreatedBy = actorId,
            CreatedAt = DateTime.UtcNow,
            Notes = request.Notes ?? string.Empty
        };

        if (await store.InsertCostAsync(tenantId, cost, ct) == 0)
        {
            var raced = await store.FindCostAsync(tenantId, costId, ct);
            if (raced is not null)
            {
                return ProductionResult<ActualCostDto>.Ok(ToDto(raced),
                    "Cost already exists (race idempotent).");
            }
            return ProductionResult<ActualCostDto>.Fail(
                ProductionErrorCode.Conflict, "Cost conflict (unique).");
        }

        await AuditAsync(tenantId, actorId, "production.cost_record",
            "production_actual_costs", cost.Id, null, new
            {
                costId,
                costType,
                quantity = request.Quantity,
                unitCost = request.UnitCost,
                totalCost = cost.TotalCost,
                isFoc = request.IsFoc
            }, ip, ct);

        return ProductionResult<ActualCostDto>.Ok(ToDto(cost), "Cost recorded.");
    }

    /// <summary>GET /costs?jobOrderId= — WPF GetByJobAsync order (Spec,
    /// Serial, CostType).</summary>
    public async Task<ProductionResult<IReadOnlyList<ActualCostDto>>> ListCostsByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        var rows = await store.ListCostsByJobAsync(tenantId, jobOrderId, ct);
        return ProductionResult<IReadOnlyList<ActualCostDto>>.Ok(
            rows.Select(ToDto).ToList());
    }

    /// <summary>GET /costs/summary?jobOrderId= — WPF GetSummaryByJobAsync:
    /// per-bucket totals; TotalActual falls back to the component sum;
    /// FocImpact is the cost incurred with selling zero.</summary>
    public async Task<ProductionResult<CostSummaryDto>> GetSummaryByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        var costs = await store.ListCostsByJobAsync(tenantId, jobOrderId, ct);

        decimal Sum(string type) => costs
            .Where(c => string.Equals(c.CostType, type, StringComparison.Ordinal))
            .Sum(c => c.TotalCost);

        decimal estimated = Sum(ActualCostTypes.Estimated);
        decimal actualMat = Sum(ActualCostTypes.ActualMaterial);
        decimal prod = Sum(ActualCostTypes.Production);
        decimal wastage = Sum(ActualCostTypes.Wastage);
        decimal processing = Sum(ActualCostTypes.Processing);
        decimal totalActual = Sum(ActualCostTypes.TotalActual);
        if (totalActual == 0) totalActual = actualMat + prod + wastage + processing;

        decimal focImpact = costs.Where(c => c.IsFoc).Sum(c => c.TotalCost);

        return ProductionResult<CostSummaryDto>.Ok(new CostSummaryDto(
            estimated, actualMat, prod, wastage, processing, totalActual, focImpact));
    }

    // ── DeriveFromAllocation (WPF DeriveFromAllocationAsync) ─────────

    /// <summary>
    /// POST /costs/derive?jobOrderId= — per allocation: piece sqm × the
    /// rack's purchase price per sqm; FOC is detected from the linked work
    /// item; a missing rack price derives 0 (the allocation stays, the
    /// message says so — the WPF's cross-DB read failure behaves the
    /// same).
    /// </summary>
    public async Task<ProductionResult<string>> DeriveFromAllocationAsync(
        Guid tenantId, Guid actorId, Guid jobOrderId, string? ip,
        CancellationToken ct)
    {
        var job = await production.FindJobAsync(tenantId, jobOrderId, ct);
        if (job is null)
        {
            return ProductionResult<string>.Fail(
                ProductionErrorCode.NotFound,
                $"Job order {jobOrderId} was not found.");
        }

        var allocations = await store.ListAllocationsByJobAsync(
            tenantId, jobOrderId, ct);
        var live = allocations
            .Where(a => !string.Equals(a.Status, AllocationStatuses.Cancelled,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (live.Count == 0)
        {
            return ProductionResult<string>.Fail(
                ProductionErrorCode.Conflict, "No allocations found for job.");
        }

        // Price map: allocation → reservation → GlassSheet → price/sqm.
        var reservationKeys = live.Select(a => a.ReservationKey)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.Ordinal).ToList();
        var allReservations = await store.ListAllReservationsAsync(tenantId, ct);
        var resByKey = allReservations
            .Where(r => reservationKeys.Contains(r.ReservationKey,
                StringComparer.Ordinal))
            .ToDictionary(r => r.ReservationKey, r => r, StringComparer.Ordinal);

        var allSheets = await store.ListAllGlassSheetsAsync(tenantId, ct);
        var priceMap = allSheets.ToDictionary(
            s => s.Id, s => s.PurchasePricePerSqm);

        // FOC detection: the linked work item (WPF checks the work item's
        // IsFoc by spec/serial).
        var pieces = await production.ListByJobAsync(tenantId, jobOrderId, ct);
        var focSpecs = pieces
            .Where(p => p.IsFoc)
            .Select(p => (p.SpecGroupNumber, p.SerialNumber))
            .ToHashSet();

        int derived = 0;
        foreach (var alloc in live)
        {
            resByKey.TryGetValue(alloc.ReservationKey, out var reservation);
            Guid? sheetId = reservation?.GlassSheetId;
            decimal unitPrice = sheetId.HasValue
                && priceMap.TryGetValue(sheetId.Value, out var price) ? price : 0m;

            decimal qtySqm = (decimal)alloc.WidthMm * alloc.HeightMm
                / 1_000_000m * alloc.Quantity;
            decimal totalCost = qtySqm * unitPrice;
            bool isFoc = focSpecs.Contains((alloc.SpecGroupNumber, alloc.SerialNumber));

            string costId = CuttingIds.BuildCostId(
                job.JobNo, alloc.SpecGroupNumber, alloc.SerialNumber,
                ActualCostTypes.ActualMaterial, alloc.AllocationId, isFoc);

            var existing = await store.FindCostAsync(tenantId, costId, ct);
            if (existing is not null)
            {
                existing.Quantity = qtySqm;
                existing.UnitCost = unitPrice;
                existing.TotalCost = totalCost;
                existing.SellingValue = isFoc ? 0m : totalCost;
                existing.IsFoc = isFoc;
                await store.TryUpdateCostAsync(existing, ct);
            }
            else
            {
                await store.InsertCostAsync(tenantId, new ActualCost
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CostId = costId,
                    JobOrderId = job.Id,
                    JobRef = job.JobNo,
                    SourceProformaId = job.ProformaId.ToString(),
                    SpecGroupNumber = alloc.SpecGroupNumber,
                    SerialNumber = alloc.SerialNumber,
                    GlassRef = alloc.GlassRef,
                    SpecName = reservation?.SpecName ?? string.Empty,
                    CostType = ActualCostTypes.ActualMaterial,
                    StockSheetId = sheetId,
                    MaterialAllocationId = alloc.AllocationId,
                    Quantity = qtySqm,
                    UnitCost = unitPrice,
                    TotalCost = totalCost,
                    SellingValue = isFoc ? 0m : totalCost,
                    IsFoc = isFoc,
                    CreatedBy = actorId,
                    CreatedAt = DateTime.UtcNow,
                    Notes = $"Derived from allocation {alloc.AllocationId} " +
                            $"util {alloc.UtilizationPct:0.##}%"
                }, ct);
            }
            derived++;
        }

        await AuditAsync(tenantId, actorId, "production.costs_derive",
            "production_actual_costs", job.Id, null,
            new { jobOrderId, derived }, ip, ct);

        return ProductionResult<string>.Ok(
            $"Derived {derived} actual costs from allocations.");
    }

    // ── helpers ──────────────────────────────────────────────────────

    internal static AllocationDto ToDto(MaterialAllocation a) => new(
        a.Id,
        a.AllocationId,
        a.ReservationKey,
        a.JobOrderId,
        a.JobRef,
        a.SpecGroupNumber,
        a.SerialNumber,
        a.OptimizationSessionId,
        a.SheetNo,
        a.PlacedPieceLabel,
        a.GlassRef,
        a.WidthMm,
        a.HeightMm,
        a.Quantity,
        a.UtilizationPct,
        a.WastagePct,
        a.Status,
        a.ConsumedAt,
        a.CreatedAt,
        a.Notes);

    internal static ActualCostDto ToDto(ActualCost c) => new(
        c.Id,
        c.CostId,
        c.JobOrderId,
        c.JobRef,
        c.SourceProformaId,
        c.SpecGroupNumber,
        c.SerialNumber,
        c.GlassRef,
        c.SpecName,
        c.CostType,
        c.StockSheetId,
        c.MaterialAllocationId,
        c.Quantity,
        c.UnitCost,
        c.TotalCost,
        c.SellingValue,
        c.IsFoc,
        c.CreatedAt,
        c.Notes);

    private async Task AuditAsync(
        Guid tenantId, Guid? actorUserId, string action, string entityType,
        Guid entityId, object? before, object? after, string? ip, CancellationToken ct)
        => await audit.WriteAsync(tenantId, actorUserId, action, entityType,
            entityId, ip, null,
            before is null ? null : JsonSerializer.Serialize(before),
            after is null ? null : JsonSerializer.Serialize(after),
            ct).ConfigureAwait(false);
}
