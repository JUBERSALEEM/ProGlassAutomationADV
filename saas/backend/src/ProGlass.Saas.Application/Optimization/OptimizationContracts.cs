using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Application.Optimization;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 11 — Optimization (core slice): the WPF GuillotinePacker QUICK
//  base pass, grouped by glass composition (PaneDisplayName — the merge
//  key), persisted as sessions/sheets/placements/cuts/leftovers.
//  The WPF QUICK portfolio's extra candidates and the OPTIMIZE
//  multi-start are later slices (documented).
// ═══════════════════════════════════════════════════════════════════════

/// <summary>Optimization failure codes (API §0 error contract).</summary>
public enum OptimizationErrorCode
{
    None = 0,
    ValidationFailed,
    NotFound,
    Conflict,
    /// <summary>Phase 18: the per-period optimization-jobs plan limit was
    /// reached — HTTP 402.</summary>
    SubscriptionLimitExceeded
}

public sealed record OptimizationResult<T>(
    bool Success, T? Value, OptimizationErrorCode ErrorCode, string Message)
{
    public static OptimizationResult<T> Ok(T value) =>
        new(true, value, OptimizationErrorCode.None, "");

    public static OptimizationResult<T> Fail(
        OptimizationErrorCode code, string message) =>
        new(false, default, code, message);
}

/// <summary>One piece in the run request. Either supply pieces directly
/// or derive them from a job order's cutting plan (jobOrderId).</summary>
public sealed record OptimizationPieceRequest(
    string GlassRef,
    string SpecLabel,
    string Position,
    int WidthMm,
    int HeightMm,
    int Qty = 1,
    bool AllowRotate = true,
    int ThicknessMm = 0,
    string Color = "",
    int SpecGroupNumber = 0,
    int SourceRowSerial = 0);

/// <summary>One stock rack in the run request. Empty stocks = every
/// Sheet Store rack of the group's thickness.</summary>
public sealed record OptimizationStockRequest(
    int WidthMm,
    int HeightMm,
    int ThicknessMm,
    string Color = "",
    string Label = "",
    int MaxSheets = 0);

/// <summary>Body for POST /api/v1/optimization/run.</summary>
public sealed record RunOptimizationRequest(
    Guid? JobOrderId = null,
    IReadOnlyList<OptimizationPieceRequest>? Pieces = null,
    IReadOnlyList<OptimizationStockRequest>? Stocks = null,
    string Rotation = "Free90",
    string Split = "HorizontalFirst",
    int? MinBreakoutMm = null);

/// <summary>One persisted run (header).</summary>
public sealed record OptimizationSessionDto(
    Guid Id,
    string DocumentType,
    Guid? DocumentId,
    string CompositionKey,
    string Status,
    string Strategy,
    int SheetsUsed,
    int UnplacedCount,
    decimal TotalUsedSqm,
    decimal TotalStockSqm,
    decimal UtilizationPct,
    int SheetsLowerBound,
    bool ConservationVerified,
    bool GeometryVerified,
    IReadOnlyList<string> ValidationProblems,
    IReadOnlyList<string> ScaleNotes,
    DateTime CreatedAt);

/// <summary>One placed piece (persisted).</summary>
public sealed record PlacementDto(
    Guid Id,
    int SheetNo,
    string PieceRef,
    string GlassRef,
    string SpecLabel,
    string Position,
    int X,
    int Y,
    int W,
    int H,
    bool Rotated);

/// <summary>One cut (persisted).</summary>
public sealed record CutDto(
    int Seq, string Axis, int AtMm, int FromX, int FromY, int FromW,
    int FromH, int Stage, bool IsTrim);

/// <summary>One leftover region.</summary>
public sealed record LeftoverDto(
    int SheetNo, int X, int Y, int W, int H, bool IsReusable);

/// <summary>One sheet with everything on it.</summary>
public sealed record OptimizationSheetDto(
    Guid Id,
    int SheetNo,
    int StockW,
    int StockH,
    int ThicknessMm,
    string Color,
    int UsableX,
    int UsableY,
    int UsableW,
    int UsableH,
    decimal UsedSqm,
    decimal StockSqm,
    decimal UtilizationPct,
    int TrimCutCount,
    IReadOnlyList<PlacementDto> Placements,
    IReadOnlyList<CutDto> Cuts,
    IReadOnlyList<LeftoverDto> Leftovers);

/// <summary>The full run: one session per glass composition group.</summary>
public sealed record OptimizationRunDto(
    IReadOnlyList<OptimizationSessionDto> Sessions,
    IReadOnlyList<OptimizationSheetDto> Sheets,
    IReadOnlyList<string> Warnings);

/// <summary>Storage port for the Optimization module.</summary>
public interface IOptimizationStore
{
    Task<JobOrder?> FindJobAsync(Guid tenantId, Guid jobOrderId, CancellationToken ct);

    /// <summary>ALL Sheet Store racks (the default pool).</summary>
    Task<IReadOnlyList<GlassSheet>> ListSheetsAsync(
        Guid tenantId, CancellationToken ct);

    /// <summary>Persist one group's session + sheets + placements + cuts
    /// + leftovers in one transaction.</summary>
    /// <summary>Phase 18 limit counter — sessions since the current
    /// billing period start.</summary>
    Task<int> CountSessionsSinceAsync(
        Guid tenantId, DateOnly since, CancellationToken ct);

    Task<OptimizationSession> PersistAsync(
        Guid tenantId, Guid actorId,
        string documentType, Guid? documentId, string compositionKey,
        Domain.Optimization.OptimizationResult plan,
        string paramsJson, CancellationToken ct);

    Task<IReadOnlyList<Domain.Entities.OptimizationSession>> ListSessionsForJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct);

    Task<OptimizationSession?> FindSessionAsync(
        Guid tenantId, Guid id, CancellationToken ct);

    Task<IReadOnlyList<OptimizationSession>> ListSessionsAsync(
        Guid tenantId, Guid? jobOrderId, int page, int pageSize,
        CancellationToken ct);

    Task<IReadOnlyList<OptimizationSheet>> ListSheetsForSessionAsync(
        Guid tenantId, Guid sessionId, CancellationToken ct);

    Task<IReadOnlyList<OptimizationPlacement>> ListPlacementsAsync(
        Guid tenantId, IReadOnlyList<Guid> sheetIds, CancellationToken ct);

    Task<IReadOnlyList<OptimizationCut>> ListCutsAsync(
        Guid tenantId, IReadOnlyList<Guid> sheetIds, CancellationToken ct);

    Task<IReadOnlyList<OptimizationLeftover>> ListLeftoversAsync(
        Guid tenantId, IReadOnlyList<Guid> sheetIds, CancellationToken ct);
}
