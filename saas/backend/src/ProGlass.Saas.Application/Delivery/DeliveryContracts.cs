namespace ProGlass.Saas.Application.Delivery;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 10 — Delivery core (WPF Services/DeliveryNote: the master DN
//  balance tracker + the one-path create-or-open + the status machine +
//  the soft-close void). Attachments, PDF/export snapshots, analytics,
//  the Tax Invoice link and the weight calculator are later slices.
// ═══════════════════════════════════════════════════════════════════════

/// <summary>Delivery failure codes (API §0 error contract).</summary>
public enum DeliveryErrorCode
{
    None = 0,
    ValidationFailed,
    NotFound,
    Conflict,
    ConcurrencyConflict,
    FullyDelivered
}

/// <summary>Typed result: a value or a code + operator-readable message.</summary>
public sealed record DeliveryResult<T>(
    bool Success, T? Value, DeliveryErrorCode ErrorCode, string Message)
{
    public static DeliveryResult<T> Ok(T value) =>
        new(true, value, DeliveryErrorCode.None, "");

    public static DeliveryResult<T> Fail(DeliveryErrorCode code, string message) =>
        new(false, default, code, message);
}

/// <summary>The create-or-open outcome (WPF DnCreateResult.Kind).</summary>
public enum DnCreateOutcome
{
    OpenExisting = 0,
    Created,
    FullyDelivered
}

/// <summary>Response of POST /job-orders/{id}/delivery-notes (the
/// workspace NEW DN / CREATE DN path).</summary>
public sealed record CreateOrOpenDnDto(
    DnCreateOutcome Outcome,
    DeliveryNoteDto? Note,
    string Message);

/// <summary>One delivery note line (WPF DeliveryNoteItem).</summary>
public sealed record DeliveryNoteItemDto(
    Guid Id,
    int SpecGroupNumber,
    int SerialNumber,
    string SpecGroupName,
    string GlassRef,
    int W1,
    int H1,
    int W2,
    int H2,
    int OrderedQty,
    int PreviouslyDeliveredQty,
    int DeliveredQty,
    int BalanceQty,
    int ReworkQty,
    int ReturnedQty,
    decimal WeightPerPiece,
    decimal TotalWeight,
    string Notes);

/// <summary>A delivery note (WPF SavedDeliveryNote).</summary>
public sealed record DeliveryNoteDto(
    Guid Id,
    string DeliveryNo,
    Guid JobOrderId,
    string JobOrderNo,
    string CustomerName,
    string ProjectName,
    string Status,
    string DeliveryType,
    bool IsFoc,
    bool IsMasterDn,
    int SequenceNumber,
    string Driver,
    string Vehicle,
    string ReceiverName,
    DateOnly? DeliveryDate,
    string DeliveryTime,
    string Notes,
    int TotalOrderedQty,
    int TotalDeliveredQty,
    int TotalBalanceQty,
    bool IsVoid,
    DateTime? VoidedAt,
    string VoidReason,
    int RowVersion,
    IReadOnlyList<DeliveryNoteItemDto> Items);

/// <summary>A delivery note with its lines — the generated EF model has
/// no collection nav from DeliveryNote to DeliveryNoteItem, so the pair
/// travels explicitly (same pattern as SpecGroupWithItems).</summary>
public sealed record DeliveryNoteAggregate(
    Domain.Entities.DeliveryNote Note,
    List<Domain.Entities.DeliveryNoteItem> Items);

/// <summary>Status transition body.</summary>
public sealed record DeliveryTransitionRequest(string ToStatus, string? Reason = null);

/// <summary>Soft-close body.</summary>
public sealed record VoidDeliveryRequest(string? Reason = null);

/// <summary>
/// PUT body — the workspace's dispatch edit (open notes only): the
/// confirm block + the line delivered quantities. Null keeps the saved
/// value; items are optional (keep the saved lines when omitted).
/// </summary>
public sealed record UpdateDispatchRequest(
    string? Driver,
    string? Vehicle,
    string? ReceiverName,
    DateOnly? DeliveryDate,
    string? DeliveryTime,
    string? Notes,
    bool? IsFoc,
    IReadOnlyList<DispatchLineRequest>? Items = null,
    int? ExpectedRowVersion = null);

/// <summary>One line's edited delivered quantity (0 ≤ qty ≤ balance).</summary>
public sealed record DispatchLineRequest(Guid ItemId, int DeliveredQty, string? Notes = null);
