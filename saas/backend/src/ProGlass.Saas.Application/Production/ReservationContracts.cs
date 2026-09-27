using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Application.Production;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 9c — Material reservations: the Sheet-Store hold ledger (WPF
//  MaterialReservationService). Reserve is idempotent on the
//  deterministic key; availability = sheet Balance (Stock − Used) −
//  other active holds; consumed material is terminal. The allocation
//  ledger (which sessions consumed which sheets) is the optimization
//  slice — documented.
// ═══════════════════════════════════════════════════════════════════════

/// <summary>Body for POST /api/v1/reservations (WPF ReserveAsync).</summary>
public sealed record ReserveRequest(
    Guid JobOrderId,
    Guid GlassSheetId,
    int SpecGroupNumber,
    int SerialNumber,
    string? GlassRef,
    string? SpecName,
    Guid? OptimizationSessionId,
    int Quantity,
    string? Notes = null);

/// <summary>Body for the release/cancel endpoints.</summary>
public sealed record ReservationActionRequest(string? Reason = null);

/// <summary>One reservation row.</summary>
public sealed record ReservationDto(
    Guid Id,
    string ReservationKey,
    Guid JobOrderId,
    string JobOrderNo,
    Guid GlassSheetId,
    string StockSizeLabel,
    int SpecGroupNumber,
    int SerialNumber,
    string GlassRef,
    string SpecName,
    int WidthMm,
    int HeightMm,
    int ThicknessMm,
    string Color,
    int Quantity,
    string Status,
    string Notes,
    DateTime? ReleasedAt,
    string ReleaseReason,
    DateTime CreatedAt);

/// <summary>Availability read model for one sheet (WPF
/// TryEnsureStockAvailableAsync math, exposed for the UI).</summary>
public sealed record SheetAvailabilityDto(
    Guid GlassSheetId,
    string StockSizeLabel,
    int Balance,
    int ActiveHolds,
    int Available);

/// <summary>Storage port for reservations.</summary>
public interface IReservationStore
{
    Task<JobOrder?> FindJobAsync(Guid tenantId, Guid jobOrderId, CancellationToken ct);

    Task<MaterialReservation?> FindAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<MaterialReservation?> FindByKeyAsync(
        Guid tenantId, string reservationKey, CancellationToken ct);

    Task<IReadOnlyList<MaterialReservation>> ListByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct);

    /// <summary>Sheet balance (Stock − Used); null when the sheet does
    /// not exist.</summary>
    Task<int?> GetSheetBalanceAsync(
        Guid tenantId, Guid glassSheetId, CancellationToken ct);

    Task<GlassSheet?> FindSheetAsync(
        Guid tenantId, Guid glassSheetId, CancellationToken ct);

    /// <summary>Sum of ACTIVE holds (Reserved/Allocated) on the sheet,
    /// excluding the given key.</summary>
    Task<int> SumActiveHoldsAsync(
        Guid tenantId, Guid glassSheetId, string? excludeKey, CancellationToken ct);

    /// <summary>Insert; false on the (tenant, reservation_key) UNIQUE
    /// violation (race — the caller re-reads by key).</summary>
    Task<bool> TryInsertAsync(MaterialReservation reservation, CancellationToken ct);

    Task<bool> TryUpdateAsync(MaterialReservation reservation, CancellationToken ct);
}
