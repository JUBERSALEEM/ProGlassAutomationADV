using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Production;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 9b — Production core (WPF Services/Production: the
//  deterministic distribution engine → work items, the stage/entry
//  lifecycle, QC, breakage and cancellation). Process executions,
//  workstations, control holds, actual costs and replacements are later
//  slices (documented in the mapping).
// ═══════════════════════════════════════════════════════════════════════

/// <summary>Production failure codes (API §0 error contract).</summary>
public enum ProductionErrorCode
{
    None = 0,
    ValidationFailed,
    NotFound,
    Conflict,
    ConcurrencyConflict
}

/// <summary>Typed result: a value or a code + operator-readable message.</summary>
public sealed record ProductionResult<T>(
    bool Success, T? Value, ProductionErrorCode ErrorCode, string Message)
{
    public static ProductionResult<T> Ok(T value) =>
        new(true, value, ProductionErrorCode.None, "");

    /// <summary>Ok with the idempotency/gating message the WPF surfaces
    /// ("Already completed (idempotent)." etc.).</summary>
    public static ProductionResult<T> Ok(T value, string message) =>
        new(true, value, ProductionErrorCode.None, message);

    public static ProductionResult<T> Fail(ProductionErrorCode code, string message) =>
        new(false, default, code, message);
}

/// <summary>The deterministic plan (read model — nothing persisted).</summary>
public sealed record ProductionPlanDto(
    string JobOrderNo,
    string CustomerName,
    string ProjectName,
    IReadOnlyList<ProductionWorkItemDto> Items,
    IReadOnlyList<string> Warnings);

/// <summary>One work item (WPF ProductionWorkItem / SavedProductionWorkItem).</summary>
public sealed record ProductionWorkItemDto(
    Guid Id,
    string UniqueKey,
    int SpecGroupNumber,
    string SpecName,
    string BuildType,
    int SerialNumber,
    string GlassRef,
    string Position,
    string Stage,
    string StageLabel,
    string Status,
    int WidthMm,
    int HeightMm,
    int Qty,
    int QtyDone,
    decimal UnitSqm,
    decimal TotalSqm,
    decimal TotalLm,
    string Treatment,
    string PredecessorStage,
    string ChargeTemplateKey,
    decimal ChargeMetricQty,
    string ChargeMetricUnit,
    string PolishSubTypeLabel,
    bool IsFoc,
    bool IsCancelled,
    string CancelReason,
    string Notes);

/// <summary>Release result: what was inserted, what survived untouched.</summary>
public sealed record ReleaseResultDto(
    string JobOrderNo,
    int PlannedItems,
    int InsertedItems,
    int ExistingItems,
    IReadOnlyList<string> Warnings,
    // Phase 26a — one Pending process execution projected per released
    // work item (WPF ProjectJobAsync; the process board shows the job
    // before Start).
    int ProjectedExecutions = 0);

/// <summary>Body for POST /{id}/stage — floor progress on one item.</summary>
public sealed record StageProgressRequest(
    int QtyDone,
    string? Status = null,
    string? Reason = null);

/// <summary>Body for POST /{id}/qc (WPF RecordQcAsync).</summary>
public sealed record QcRequest(
    string Result,          // Passed | Failed | Rework (WPF QcResult)
    string? Reason = null,
    string? Notes = null);

/// <summary>Body for POST /{id}/cancel (WPF CancelWorkItemAsync).</summary>
public sealed record CancelPieceRequest(string? Reason = null);

/// <summary>Body for POST /{id}/breakage (WPF RecordBreakageAsync core).</summary>
public sealed record BreakageRequest(
    int QtyBroken,
    string Reason,
    string? Notes = null);

/// <summary>One production entry (the immutable event log row).</summary>
public sealed record ProductionEventDto(
    Guid Id,
    string EventType,
    string Stage,
    int SpecGroupNumber,
    int SerialNumber,
    string Position,
    string GlassRef,
    int Qty,
    string Reason,
    DateTime CreatedAt);
