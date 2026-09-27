// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   Models/Production/SavedProduction/SavedProductionPhase14Entities.cs
//     (ReservationStatus — Available/Reserved/Allocated/Consumed/
//     Released/Cancelled).
//   Services/Production/MaterialReservationService.cs (BuildReservationId
//     — the deterministic idempotency key; the availability contract:
//     sheet Balance (Stock − Used) − other active holds; "Not enough
//     stock. Available: {n}, Requested: {m}"; consumed material is never
//     released and never cancelled).
// The WPF files are the source of truth; behaviour preserved verbatim.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlass.Saas.Domain.Proforma;

/// <summary>Material reservation lifecycle (WPF ReservationStatus).</summary>
public static class ReservationStatuses
{
    public const string Available = "Available";
    public const string Reserved = "Reserved";
    public const string Allocated = "Allocated";
    public const string Consumed = "Consumed";
    public const string Released = "Released";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Available, Reserved, Allocated, Consumed, Released, Cancelled
    };

    /// <summary>An ACTIVE hold consumes availability (WPF availability
    /// filter: Reserved or Allocated).</summary>
    public static bool IsActiveHold(string? status) =>
        string.Equals(status, Reserved, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Allocated, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Deterministic reservation identity for idempotency (WPF
/// BuildReservationId: JobRef|Spec|Serial|GlassSheetId|OptSession|SheetNo).
/// The SaaS sheet id is a GUID, so the key embeds that instead of the WPF
/// int id — same determinism, same fields.
/// </summary>
public static class ReservationKeys
{
    public static string Build(
        string jobRef, int specGroup, int serial,
        Guid glassSheetId, Guid? optimizationSessionId) =>
        string.Join("|",
            (jobRef ?? "").Trim(),
            specGroup,
            serial,
            glassSheetId,
            optimizationSessionId?.ToString("D") ?? "");
}
