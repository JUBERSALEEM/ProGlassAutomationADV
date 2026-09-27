// ═══════════════════════════════════════════════════════════════════════
//  FILE: ProGlass.Saas.Application/Production/CuttingContracts.cs
//  Phase 26b — the cutting workflow family (WPF Services/Production/
//  Cutting/*), the allocation ledger + actual costs (WPF
//  MaterialAllocationService + ActualCostService), replacements/FOC (WPF
//  ProductionBreakageService), inventory consistency scanner (WPF
//  ProductionInventoryConsistencyScanner) and snapshot integrity (WPF
//  ProductionSnapshotIntegrity). Pure halves live in Domain/Proforma/
//  ProductionCutting.cs.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Production;

// ── requirements + grouping read models ──────────────────────────────

public sealed record CuttingRequirementDto(
    string PieceId,
    string PieceLabel,
    int PieceIndex,
    int PieceSequence,
    string JobRef,
    int SpecGroupNumber,
    int SerialNumber,
    string SpecName,
    string GlassRef,
    string Position,
    int WidthMm,
    int HeightMm,
    string GlassFamily,
    string Color,
    int ThicknessMm,
    string Coating,
    string Treatment,
    bool RequiresTempering,
    bool IsFoc,
    int ReplacementSequence,
    string MaterialKey);

public sealed record MaterialGroupDto(
    string GroupId,
    string DisplayLabel,
    string GlassFamily,
    string Color,
    int ThicknessMm,
    string Coating,
    string Treatment,
    int TotalPieces,
    decimal TotalAreaSqm,
    IReadOnlyList<CuttingRequirementDto> Requirements);

// ── cutting sessions / sheets / placed pieces ────────────────────────

public sealed record CuttingSessionDto(
    Guid Id,
    string CuttingSessionId,
    Guid? JobOrderId,
    string JobRef,
    string SourceProformaId,
    Guid? OptimizationSessionId,
    string MaterialGroupId,
    int TotalPieces,
    int SheetsUsed,
    decimal UtilizationPct,
    decimal WastagePct,
    string Status,
    string RotationPolicy,
    string SplitMode,
    int EffortLevel,
    bool IsVerified,
    string VerificationResult,
    DateTime? CompletedAt,
    DateTime CreatedAt,
    string Notes);

public sealed record CuttingSheetDto(
    Guid Id,
    string CuttingSheetId,
    Guid CuttingSessionId,
    string JobRef,
    int SheetNo,
    Guid? StockSheetId,
    string StockSizeLabel,
    int StockWidthMm,
    int StockHeightMm,
    int ThicknessMm,
    string Color,
    int UsableX,
    int UsableY,
    int UsableWidthMm,
    int UsableHeightMm,
    decimal UsedSqm,
    decimal StockSqm,
    decimal UtilizationPct,
    decimal WastagePct,
    bool IsVerified,
    int PlacedPieceCount,
    DateTime CreatedAt);

public sealed record CuttingPlacedPieceDto(
    Guid Id,
    string PlacedPieceId,
    Guid CuttingSheetId,
    Guid CuttingSessionId,
    string JobRef,
    int SpecGroupNumber,
    int SerialNumber,
    string GlassRef,
    string SpecName,
    string Position,
    string PieceId,
    string PieceLabel,
    int WidthMm,
    int HeightMm,
    int X,
    int Y,
    bool IsRotated,
    int CutSequence,
    string Status,
    Guid? SourcePieceId,
    string ReservationKey,
    string AllocationId,
    bool IsFoc,
    string Notes);

/// <summary>One material group's outcome inside a run (WPF
/// CuttingWorkflowResult narrowed per group).</summary>
public sealed record CuttingGroupResultDto(
    string GroupId,
    bool Ok,
    string Message,
    string? CuttingSessionId,
    int Sheets,
    int Pieces,
    IReadOnlyList<string> Warnings);

public sealed record CuttingRunResultDto(
    bool Success,
    string Message,
    string JobRef,
    IReadOnlyList<CuttingGroupResultDto> Groups,
    int Sessions,
    int Sheets,
    int Pieces,
    int Reservations,
    int Allocations,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);

// ── workflow requests ────────────────────────────────────────────────

/// <summary>POST /cutting/run — WPF RunAsync(jobRef, rotation, split,
/// effortLevel).</summary>
public sealed record RunCuttingRequest(
    Guid JobOrderId,
    string? Rotation = null,
    string? Split = null,
    int? EffortLevel = null,
    string? Notes = null);

public sealed record CompletePieceRequest(string? Notes = null);

/// <summary>Breakage during cutting (WPF RecordBreakageDuringCutting).</summary>
public sealed record CuttingBreakageRequest(
    string Reason,
    bool IsFoc = false,
    string? Notes = null);

public sealed record CuttingProgressDto(
    int Required,
    int Completed,
    int Remaining,
    int Breakage,
    int Foc,
    int Rework,
    int QcPending);

// ── allocations (WPF MaterialAllocationService) ──────────────────────

public sealed record AllocationDto(
    Guid Id,
    string AllocationId,
    string ReservationKey,
    Guid? JobOrderId,
    string JobRef,
    int SpecGroupNumber,
    int SerialNumber,
    Guid? OptimizationSessionId,
    int SheetNo,
    string PlacedPieceLabel,
    string GlassRef,
    int WidthMm,
    int HeightMm,
    int Quantity,
    decimal UtilizationPct,
    decimal WastagePct,
    string Status,
    DateTime? ConsumedAt,
    DateTime CreatedAt,
    string Notes);

public sealed record AllocateRequest(
    Guid JobOrderId,
    string ReservationKey,
    int SheetNo,
    string PlacedPieceLabel,
    int SpecGroupNumber = 0,
    int SerialNumber = 0,
    Guid? OptimizationSessionId = null,
    string? GlassRef = null,
    int WidthMm = 0,
    int HeightMm = 0,
    int Quantity = 1,
    decimal UtilizationPct = 0m,
    decimal WastagePct = 0m,
    string? Notes = null);

// ── actual costs (WPF ActualCostService) ─────────────────────────────

public sealed record ActualCostDto(
    Guid Id,
    string CostId,
    Guid? JobOrderId,
    string JobRef,
    string SourceProformaId,
    int SpecGroupNumber,
    int SerialNumber,
    string GlassRef,
    string SpecName,
    string CostType,
    Guid? StockSheetId,
    string MaterialAllocationId,
    decimal Quantity,
    decimal UnitCost,
    decimal TotalCost,
    decimal SellingValue,
    bool IsFoc,
    DateTime CreatedAt,
    string Notes);

public sealed record RecordCostRequest(
    Guid JobOrderId,
    int SpecGroupNumber,
    int SerialNumber,
    string CostType,
    string? GlassRef = null,
    string? SpecName = null,
    Guid? StockSheetId = null,
    string? MaterialAllocationId = null,
    decimal Quantity = 0m,
    decimal UnitCost = 0m,
    decimal SellingValue = 0m,
    bool IsFoc = false,
    string? Notes = null);

/// <summary>WPF GetSummaryByJobAsync tuple.</summary>
public sealed record CostSummaryDto(
    decimal Estimated,
    decimal ActualMaterial,
    decimal Production,
    decimal Wastage,
    decimal Processing,
    decimal TotalActual,
    decimal FocImpact);

// ── replacements / FOC (WPF ProductionBreakageService) ───────────────

public sealed record ReplacementDto(
    Guid Id,
    string ReplacementId,
    Guid? JobOrderId,
    string JobRef,
    string SourceProformaId,
    int SpecGroupNumber,
    int SerialNumber,
    string GlassRef,
    string SpecName,
    string Position,
    string Stage,
    Guid? OriginalPieceId,
    Guid? ReplacementPieceId,
    string EntryType,
    string Reason,
    int Quantity,
    bool IsFoc,
    int ReplacementSequence,
    string Status,
    DateTime CreatedAt);

/// <summary>POST /replacements/breakage — WPF RecordBreakageAsync (full:
/// breakage event + replacement piece + replacement row; the original is
/// NEVER silently modified).</summary>
public sealed record ReplacementBreakageRequest(
    Guid PieceId,
    int Quantity,
    string Reason,
    bool IsFoc = false,
    string? Notes = null);

public sealed record ReplacementReworkRequest(
    Guid PieceId,
    int Quantity,
    string Reason,
    bool IsFoc = false,
    string? Notes = null);

// ── inventory consistency scanner (WPF ProductionInventoryConsistency
//    — read-only, never writes, never releases holds) ─────────────────

public sealed record ConsistencyFindingDto(
    string Kind,
    string Code,
    string Message,
    Guid? GlassSheetId,
    string? ReservationKey,
    string? AllocationId,
    string? JobRef,
    int Quantity);

public sealed record ConsistencyScanDto(
    DateTime ScannedAtUtc,
    int ReservationRows,
    int AllocationRows,
    int GlassSheetRows,
    int ActiveHoldQuantity,
    int ConsumedQuantity,
    IReadOnlyList<ConsistencyFindingDto> Findings,
    IReadOnlyList<string> Limitations);

// ── snapshot integrity (WPF ProductionSnapshotIntegrity) ─────────────

public sealed record SnapshotIntegrityDto(
    Guid JobOrderId,
    string JobRef,
    string Kind,
    string Message,
    bool HasCutRows);

// ── store port ───────────────────────────────────────────────────────

public interface ICuttingCostStore
{
    // ── allocations
    Task<MaterialAllocation?> FindAllocationAsync(
        Guid tenantId, string allocationId, CancellationToken ct);

    Task<IReadOnlyList<MaterialAllocation>> ListAllocationsByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct);

    Task<IReadOnlyList<MaterialAllocation>> ListAllocationsByReservationAsync(
        Guid tenantId, string reservationKey, CancellationToken ct);

    Task<bool> TryUpdateAllocationAsync(
        MaterialAllocation allocation, CancellationToken ct);

    Task<int> InsertAllocationAsync(
        Guid tenantId, MaterialAllocation allocation, CancellationToken ct);

    // ── actual costs
    Task<ActualCost?> FindCostAsync(
        Guid tenantId, string costId, CancellationToken ct);

    Task<IReadOnlyList<ActualCost>> ListCostsByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct);

    Task<bool> TryUpdateCostAsync(ActualCost cost, CancellationToken ct);

    Task<int> InsertCostAsync(
        Guid tenantId, ActualCost cost, CancellationToken ct);

    // ── replacements
    Task<IReadOnlyList<ProductionReplacement>> ListReplacementsByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct);

    Task<IReadOnlyList<ProductionReplacement>> ListReplacementsByPieceAsync(
        Guid tenantId, Guid pieceId, CancellationToken ct);

    Task<ProductionReplacement?> FindReplacementAsync(
        Guid tenantId, string replacementId, CancellationToken ct);

    /// <summary>Insert the replacement row + the replacement work-item
    /// piece in one transaction. Returns 0 rows when the deterministic
    /// replacement id already exists (idempotent — the WPF returns the
    /// existing row).</summary>
    Task<(ProductionReplacement? Existing, ProductionReplacement? Created)>
        InsertReplacementWithPieceAsync(
            Guid tenantId, ProductionReplacement replacement,
            ProductionPiece replacementPiece, CancellationToken ct);

    // ── cutting sessions
    Task<CuttingSession?> FindCuttingSessionAsync(
        Guid tenantId, string cuttingSessionId, CancellationToken ct);

    Task<IReadOnlyList<CuttingSession>> ListCuttingSessionsByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct);

    Task<bool> TryUpdateCuttingSessionAsync(
        CuttingSession session, CancellationToken ct);

    Task<int> InsertCuttingSessionAsync(
        Guid tenantId, CuttingSession session, CancellationToken ct);

    // ── cutting sheets + placed pieces
    Task<IReadOnlyList<CuttingSheet>> ListCuttingSheetsBySessionAsync(
        Guid tenantId, Guid cuttingSessionId, CancellationToken ct);

    Task<IReadOnlyList<CuttingPlacedPiece>> ListPlacedPiecesBySessionAsync(
        Guid tenantId, Guid cuttingSessionId, CancellationToken ct);

    Task<CuttingPlacedPiece?> FindPlacedPieceAsync(
        Guid tenantId, string placedPieceId, CancellationToken ct);

    Task<bool> TryUpdatePlacedPieceAsync(
        CuttingPlacedPiece piece, CancellationToken ct);

    /// <summary>Persist the verified group layout in one transaction:
    /// sheets + placed pieces + per-piece allocations (the WPF loop of
    /// AllocateAsync + AddRange). Allocation idempotency rides on
    /// AllocationId; existing rows are kept. Returns rows written.</summary>
    Task<int> PersistCuttingLayoutAsync(
        Guid tenantId,
        IReadOnlyList<CuttingSheet> sheets,
        IReadOnlyList<CuttingPlacedPiece> pieces,
        IReadOnlyList<MaterialAllocation> allocations,
        CancellationToken ct);

    // ── reservations + placed pieces the workflow needs
    Task<MaterialReservation?> FindReservationByKeyAsync(
        Guid tenantId, string reservationKey, CancellationToken ct);

    Task<bool> TryUpdateReservationAsync(
        MaterialReservation reservation, CancellationToken ct);

    Task<IReadOnlyList<CuttingPlacedPiece>> ListPlacedPiecesByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct);

    /// <summary>Distinct job ids that have production pieces — the WPF
    /// Releases table equivalent (the release IS the job's floor).</summary>
    Task<IReadOnlyList<Guid>> ListProductionPieceJobIdsAsync(
        Guid tenantId, CancellationToken ct);

    // ── consistency scanner reads (read-only, whole tenant)
    Task<IReadOnlyList<MaterialReservation>> ListAllReservationsAsync(
        Guid tenantId, CancellationToken ct);

    Task<IReadOnlyList<MaterialAllocation>> ListAllAllocationsAsync(
        Guid tenantId, CancellationToken ct);

    Task<IReadOnlyList<GlassSheet>> ListAllGlassSheetsAsync(
        Guid tenantId, CancellationToken ct);

    /// <summary>Sheet-history rows for the planning-consumed observation
    /// (the WPF reads SheetHistories; the SaaS reads stock history).</summary>
    Task<IReadOnlyList<(Guid SheetId, string Action, string DocumentReference,
        int QuantityChanged)>> ListSheetHistoryAsync(
        Guid tenantId, CancellationToken ct);
}
