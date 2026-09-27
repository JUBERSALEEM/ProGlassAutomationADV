using System.Text.Json.Serialization;

namespace ProGlass.Saas.Application.JobOrders;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 9 — Job Order core (WPF Models/JobOrder + Services/JobOrder).
//  The production stages/pieces engine (Phase 9 follow-up) and the
//  master Delivery Note (Phase 10) are separate slices.
// ═══════════════════════════════════════════════════════════════════════

/// <summary>Job Order failure codes (API §0 error contract).</summary>
public enum JobOrderErrorCode
{
    None = 0,
    ValidationFailed,
    NotFound,
    Conflict,
    NumberExhausted,
    ConcurrencyConflict
}

/// <summary>Typed result: a value or a code + operator-readable message.</summary>
public sealed record JobOrderResult<T>(
    bool Success, T? Value, JobOrderErrorCode ErrorCode, string Message)
{
    public static JobOrderResult<T> Ok(T value) =>
        new(true, value, JobOrderErrorCode.None, "");

    public static JobOrderResult<T> Fail(JobOrderErrorCode code, string message) =>
        new(false, default, code, message);
}

/// <summary>
/// POST body: convert a proforma invoice into a Job Order. AllowExisting
/// re-posts after the 409 "already has job order(s)" warning — the WPF
/// asks Yes/No in a dialog because a quote can legitimately be built
/// twice (a revised order, a split delivery); the API makes that consent
/// explicit.
/// </summary>
public sealed record CreateJobOrderRequest(
    Guid ProformaId,
    bool AllowExisting = false);

/// <summary>
/// PUT body: the WPF ProductionDetails editable fields. Null keeps the
/// saved value. ExpectedRowVersion carries the optimistic-concurrency
/// token (the WPF's ERP-hardening counter — mismatch ⇒ 409, never a
/// silent overwrite).
/// </summary>
public sealed record UpdateJobOrderRequest(
    string? Priority,
    DateOnly? DeliveryDate,
    string? SiteDeliveryContact,
    string? SiteDeliveryPhone,
    string? SiteDeliveryAddress,
    string? AssignedTo,
    string? CuttingInstructions,
    string? DrawingReference,
    string? InternalNotes,
    string? Notes,
    int? ExpectedRowVersion = null);

/// <summary>Status transition body (WPF ChangeStatusAsync).</summary>
public sealed record JobOrderTransitionRequest(string ToStatus, string? Reason = null);

/// <summary>Soft-close body (WPF VoidAsync — reason defaults to
/// "(no reason given)").</summary>
public sealed record VoidJobOrderRequest(string? Reason = null);

/// <summary>A Job Order (WPF SavedJobOrder + ProductionDetails).</summary>
public sealed record JobOrderDto(
    Guid Id,
    string JobOrderNo,
    Guid SourceProformaId,
    string SourceProformaNo,
    string CustomerName,
    string ProjectName,
    decimal GrandTotalVatInclusive,
    string Status,
    string StatusDisplay,
    string Priority,
    DateOnly? DeliveryDate,
    string SiteDeliveryContact,
    string SiteDeliveryPhone,
    string SiteDeliveryAddress,
    string AssignedTo,
    string CuttingInstructions,
    string DrawingReference,
    string InternalNotes,
    string Notes,
    bool IsVoid,
    DateTime? VoidedAt,
    string VoidReason,
    int RowVersion,
    DateTime CreatedAt,
    DateTime UpdatedAt);
