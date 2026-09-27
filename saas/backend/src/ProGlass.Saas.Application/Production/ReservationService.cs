using System.Text.Json;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Production;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 9c service — the WPF MaterialReservationService port:
//    • ReserveAsync — idempotent on the deterministic key; a
//      cancelled/released row is REACTIVATED after a fresh availability
//      check; available = sheet Balance (Stock − Used) − other active
//      holds; "Not enough stock. Available: {n}, Requested: {m}".
//    • ReleaseAsync — consumed material is never released; idempotent.
//    • CancelAsync — consumed never cancelled; idempotent (allocation
//      cascade deferred with the allocation ledger).
//    • ConsumeAsync — Reserved/Allocated → Consumed (terminal; the
//      physical inventory consumption contract).
//  The GlassSheet row itself is never written by a reservation (the WPF
//  does not invent a Reserved column — holds live only in this ledger).
// ═══════════════════════════════════════════════════════════════════════

/// <summary>The material-reservation hold ledger.</summary>
public sealed class ReservationService(IReservationStore store, IAuditWriter audit)
{
    /// <summary>
    /// POST /api/v1/reservations — reserve sheet stock for a job line.
    /// Idempotent: the same (job, spec, serial, sheet, session) key
    /// returns the existing row.
    /// </summary>
    public async Task<ProductionResult<ReservationDto>> ReserveAsync(
        Guid tenantId, Guid actorId, ReserveRequest request, string? ip,
        CancellationToken ct)
    {
        if (request.Quantity <= 0)
        {
            return ProductionResult<ReservationDto>.Fail(
                ProductionErrorCode.ValidationFailed, "Quantity must be > 0.");
        }

        var job = await store.FindJobAsync(tenantId, request.JobOrderId, ct);
        if (job is null)
        {
            return ProductionResult<ReservationDto>.Fail(
                ProductionErrorCode.NotFound,
                $"Job order {request.JobOrderId} was not found.");
        }

        var sheet = await store.FindSheetAsync(tenantId, request.GlassSheetId, ct);
        if (sheet is null)
        {
            return ProductionResult<ReservationDto>.Fail(
                ProductionErrorCode.NotFound,
                $"Glass sheet {request.GlassSheetId} was not found.");
        }

        var key = ReservationKeys.Build(
            job.JobNo, request.SpecGroupNumber, request.SerialNumber,
            request.GlassSheetId, request.OptimizationSessionId);

        // Idempotency: return existing; reactivate cancelled/released.
        var existing = await store.FindByKeyAsync(tenantId, key, ct);
        if (existing is not null)
        {
            if (string.Equals(existing.Status, ReservationStatuses.Cancelled,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(existing.Status, ReservationStatuses.Released,
                    StringComparison.OrdinalIgnoreCase))
            {
                var reactivate = await EnsureAvailableAsync(
                    tenantId, sheet, request.Quantity, key, ct);
                if (!reactivate.Success)
                {
                    return ProductionResult<ReservationDto>.Fail(
                        ProductionErrorCode.Conflict, reactivate.Message!);
                }

                existing.Status = ReservationStatuses.Reserved;
                existing.ReleasedAt = null;
                existing.ReleaseReason = "";
                existing.Quantity = request.Quantity;
                existing.Notes = request.Notes ?? existing.Notes;
                existing.UpdatedAt = DateTime.UtcNow;
                if (!await store.TryUpdateAsync(existing, ct))
                {
                    return ProductionResult<ReservationDto>.Fail(
                        ProductionErrorCode.ConcurrencyConflict,
                        "The reservation was modified by someone else — reload and retry.");
                }
            }

            return ProductionResult<ReservationDto>.Ok(ToDto(existing, job.JobNo));
        }

        var available = await EnsureAvailableAsync(
            tenantId, sheet, request.Quantity, key, ct);
        if (!available.Success)
        {
            return ProductionResult<ReservationDto>.Fail(
                ProductionErrorCode.Conflict, available.Message!);
        }

        var now = DateTime.UtcNow;
        var reservation = new MaterialReservation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReservationKey = key,
            JobOrderId = job.Id,
            GlassSheetId = sheet.Id,
            StockSizeLabel =
                $"{sheet.WidthMm}×{sheet.HeightMm}mm {sheet.ThicknessMm}mm {sheet.Color}".Trim(),
            SpecGroupNumber = request.SpecGroupNumber,
            SerialNumber = request.SerialNumber,
            GlassRef = request.GlassRef ?? "",
            SpecName = request.SpecName ?? "",
            OptimizationSessionId = request.OptimizationSessionId,
            WidthMm = sheet.WidthMm,
            HeightMm = sheet.HeightMm,
            ThicknessMm = sheet.ThicknessMm,
            Color = sheet.Color,
            ReservedQty = request.Quantity,
            Status = ReservationStatuses.Reserved,
            ReservedBy = actorId,
            Notes = request.Notes ?? "",
            CreatedAt = now,
            UpdatedAt = now
        };

        if (!await store.TryInsertAsync(reservation, ct))
        {
            // Race: another request inserted the same key — idempotent.
            var raced = await store.FindByKeyAsync(tenantId, key, ct);
            if (raced is not null)
            {
                return ProductionResult<ReservationDto>.Ok(ToDto(raced, job.JobNo));
            }

            return ProductionResult<ReservationDto>.Fail(
                ProductionErrorCode.Conflict, "Reservation conflict (unique).");
        }

        await AuditAsync(tenantId, actorId, "reservation.created",
            "material_reservations", reservation.Id, null, new
            {
                reservationKey = key,
                jobOrderNo = job.JobNo,
                quantity = request.Quantity
            }, ip, ct);
        return ProductionResult<ReservationDto>.Ok(ToDto(reservation, job.JobNo));
    }

    /// <summary>
    /// WPF TryEnsureStockAvailableAsync: available = Balance
    /// (Stock − Used) − other active holds on the same sheet. Never
    /// writes the GlassSheet row.
    /// </summary>
    private async Task<(bool Success, string? Message)> EnsureAvailableAsync(
        Guid tenantId, GlassSheet sheet, int quantity, string excludeKey,
        CancellationToken ct)
    {
        var balance = await store.GetSheetBalanceAsync(tenantId, sheet.Id, ct);
        if (balance is null)
        {
            return (false, $"Glass sheet {sheet.Id} not found.");
        }

        var holds = await store.SumActiveHoldsAsync(
            tenantId, sheet.Id, excludeKey, ct);
        var available = balance.Value - holds;
        if (quantity > available)
        {
            return (false,
                $"Not enough stock. Available: {available}, Requested: {quantity}");
        }

        return (true, null);
    }

    /// <summary>POST /{id}/release — consumed material is never
    /// released; idempotent.</summary>
    public async Task<ProductionResult<ReservationDto>> ReleaseAsync(
        Guid tenantId, Guid id, Guid actorId, ReservationActionRequest request,
        string? ip, CancellationToken ct)
    {
        var reservation = await store.FindAsync(tenantId, id, ct);
        if (reservation is null)
        {
            return ProductionResult<ReservationDto>.Fail(
                ProductionErrorCode.NotFound, $"Reservation {id} was not found.");
        }

        if (string.Equals(reservation.Status, ReservationStatuses.Consumed,
                StringComparison.OrdinalIgnoreCase))
        {
            return ProductionResult<ReservationDto>.Fail(
                ProductionErrorCode.Conflict, "Cannot release consumed material.");
        }

        if (string.Equals(reservation.Status, ReservationStatuses.Released,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(reservation.Status, ReservationStatuses.Cancelled,
                StringComparison.OrdinalIgnoreCase))
        {
            return ProductionResult<ReservationDto>.Ok(ToDto(reservation, null));
        }

        reservation.Status = ReservationStatuses.Released;
        reservation.ReleasedAt = DateTime.UtcNow;
        reservation.ReleaseReason = request.Reason ?? "";
        reservation.UpdatedAt = DateTime.UtcNow;
        if (!await store.TryUpdateAsync(reservation, ct))
        {
            return ProductionResult<ReservationDto>.Fail(
                ProductionErrorCode.ConcurrencyConflict,
                "The reservation was modified by someone else — reload and retry.");
        }

        await AuditAsync(tenantId, actorId, "reservation.released",
            "material_reservations", reservation.Id, null, new
            {
                reservationKey = reservation.ReservationKey,
                reason = reservation.ReleaseReason
            }, ip, ct);
        return ProductionResult<ReservationDto>.Ok(ToDto(reservation, null));
    }

    /// <summary>POST /{id}/cancel — consumed never cancelled;
    /// idempotent.</summary>
    public async Task<ProductionResult<ReservationDto>> CancelAsync(
        Guid tenantId, Guid id, Guid actorId, ReservationActionRequest request,
        string? ip, CancellationToken ct)
    {
        var reservation = await store.FindAsync(tenantId, id, ct);
        if (reservation is null)
        {
            return ProductionResult<ReservationDto>.Fail(
                ProductionErrorCode.NotFound, $"Reservation {id} was not found.");
        }

        if (string.Equals(reservation.Status, ReservationStatuses.Consumed,
                StringComparison.OrdinalIgnoreCase))
        {
            return ProductionResult<ReservationDto>.Fail(
                ProductionErrorCode.Conflict, "Cannot cancel consumed material.");
        }

        if (string.Equals(reservation.Status, ReservationStatuses.Cancelled,
                StringComparison.OrdinalIgnoreCase))
        {
            return ProductionResult<ReservationDto>.Ok(ToDto(reservation, null));
        }

        reservation.Status = ReservationStatuses.Cancelled;
        reservation.ReleasedAt = DateTime.UtcNow;
        reservation.ReleaseReason = request.Reason ?? "";
        reservation.UpdatedAt = DateTime.UtcNow;
        if (!await store.TryUpdateAsync(reservation, ct))
        {
            return ProductionResult<ReservationDto>.Fail(
                ProductionErrorCode.ConcurrencyConflict,
                "The reservation was modified by someone else — reload and retry.");
        }

        await AuditAsync(tenantId, actorId, "reservation.cancelled",
            "material_reservations", reservation.Id, null, new
            {
                reservationKey = reservation.ReservationKey,
                reason = reservation.ReleaseReason
            }, ip, ct);
        return ProductionResult<ReservationDto>.Ok(ToDto(reservation, null));
    }

    /// <summary>POST /{id}/consume — Reserved/Allocated → Consumed
    /// (terminal; the physical inventory consumption contract).</summary>
    public async Task<ProductionResult<ReservationDto>> ConsumeAsync(
        Guid tenantId, Guid id, Guid actorId, ReservationActionRequest request,
        string? ip, CancellationToken ct)
    {
        var reservation = await store.FindAsync(tenantId, id, ct);
        if (reservation is null)
        {
            return ProductionResult<ReservationDto>.Fail(
                ProductionErrorCode.NotFound, $"Reservation {id} was not found.");
        }

        if (string.Equals(reservation.Status, ReservationStatuses.Consumed,
                StringComparison.OrdinalIgnoreCase))
        {
            return ProductionResult<ReservationDto>.Ok(ToDto(reservation, null));
        }

        if (!ReservationStatuses.IsActiveHold(reservation.Status))
        {
            return ProductionResult<ReservationDto>.Fail(
                ProductionErrorCode.Conflict,
                $"Cannot consume a {reservation.Status} reservation.");
        }

        reservation.Status = ReservationStatuses.Consumed;
        reservation.UpdatedAt = DateTime.UtcNow;
        if (!await store.TryUpdateAsync(reservation, ct))
        {
            return ProductionResult<ReservationDto>.Fail(
                ProductionErrorCode.ConcurrencyConflict,
                "The reservation was modified by someone else — reload and retry.");
        }

        await AuditAsync(tenantId, actorId, "reservation.consumed",
            "material_reservations", reservation.Id, null, new
            {
                reservationKey = reservation.ReservationKey,
                quantity = reservation.ReservedQty
            }, ip, ct);
        return ProductionResult<ReservationDto>.Ok(ToDto(reservation, null));
    }

    public async Task<ProductionResult<IReadOnlyList<ReservationDto>>> ListForJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        var job = await store.FindJobAsync(tenantId, jobOrderId, ct);
        if (job is null)
        {
            return ProductionResult<IReadOnlyList<ReservationDto>>.Fail(
                ProductionErrorCode.NotFound,
                $"Job order {jobOrderId} was not found.");
        }

        var rows = await store.ListByJobAsync(tenantId, jobOrderId, ct);
        return ProductionResult<IReadOnlyList<ReservationDto>>.Ok(
            rows.Select(r => ToDto(r, job.JobNo)).ToList());
    }

    /// <summary>GET /api/v1/reservations/sheets/{sheetId}/availability —
    /// the availability math, exposed for the floor UI.</summary>
    public async Task<ProductionResult<SheetAvailabilityDto>> GetAvailabilityAsync(
        Guid tenantId, Guid glassSheetId, CancellationToken ct)
    {
        var sheet = await store.FindSheetAsync(tenantId, glassSheetId, ct);
        if (sheet is null)
        {
            return ProductionResult<SheetAvailabilityDto>.Fail(
                ProductionErrorCode.NotFound,
                $"Glass sheet {glassSheetId} was not found.");
        }

        var balance = (await store.GetSheetBalanceAsync(tenantId, glassSheetId, ct))
            ?? 0;
        var holds = await store.SumActiveHoldsAsync(
            tenantId, glassSheetId, excludeKey: null, ct);
        return ProductionResult<SheetAvailabilityDto>.Ok(new SheetAvailabilityDto(
            sheet.Id,
            $"{sheet.WidthMm}×{sheet.HeightMm}mm {sheet.ThicknessMm}mm {sheet.Color}".Trim(),
            balance, holds, balance - holds));
    }

    internal static ReservationDto ToDto(MaterialReservation r, string? jobNo) => new(
        r.Id, r.ReservationKey, r.JobOrderId, jobNo ?? "", r.GlassSheetId,
        r.StockSizeLabel, r.SpecGroupNumber, r.SerialNumber, r.GlassRef,
        r.SpecName, r.WidthMm, r.HeightMm, r.ThicknessMm, r.Color,
        r.ReservedQty, r.Status, r.Notes, r.ReleasedAt, r.ReleaseReason,
        r.CreatedAt);

    private async Task AuditAsync(
        Guid tenantId, Guid? actorUserId, string action, string entityType,
        Guid entityId, object? before, object? after, string? ip, CancellationToken ct)
        => await audit.WriteAsync(tenantId, actorUserId, action, entityType,
            entityId, ip, null,
            before is null ? null : JsonSerializer.Serialize(before),
            after is null ? null : JsonSerializer.Serialize(after),
            ct).ConfigureAwait(false);
}
