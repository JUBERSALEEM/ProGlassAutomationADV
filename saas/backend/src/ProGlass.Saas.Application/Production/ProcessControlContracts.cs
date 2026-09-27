using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Production;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 26a — production process + control contracts (WPF Phase 16/17):
//    • ProcessExecutionService (idempotent start, piece-flow completion,
//      breakage → replacement execution lineage)
//    • PairingService + DoublingLaminationService (deterministic pairing,
//      assembly with preserved component lineage)
//    • ProductionControlService (controlled transitions, holds, operator/
//      workstation assignment, priority/due-date overrides)
//    • ProductionQueueService + ProductionDashboardService (unified queue,
//      KPIs, pressure, workstation load, progress)
//    • WorkstationService (the ERP-level station master)
//  Storage rides IProcessControlStore; the pure halves live in
//  Domain/Proforma/ProductionProcess.cs (routing, readiness, identity,
//  queue ordering, timing, progress models).
// ═══════════════════════════════════════════════════════════════════════

// ── requests ─────────────────────────────────────────────────────────

/// <summary>Body for POST /executions/start — the caller names the work
/// item; PieceId is the pane-instance identity when known (WPF callers
/// pass the cutting-expansion identity).</summary>
public sealed record StartExecutionRequest(
    Guid WorkItemId,
    Guid? InputWorkItemId = null,
    string? PieceId = null,
    int? QtyRequired = null,
    string? ProcessDetailJson = null,
    bool IsFoc = false,
    string? Notes = null);

public sealed record PauseExecutionRequest(string? Reason = null);

/// <summary>Body for POST /executions/{id}/complete — QtyDone is
/// INCREMENTAL and clamped to QtyRequired (piece-flow, no destructive
/// overwrite).</summary>
public sealed record CompleteExecutionRequest(
    int QtyDone,
    string? Notes = null,
    string? ProcessDetailJson = null);

/// <summary>Body for POST /executions/{id}/breakage — EntryType 1 =
/// Breakage, 2 = Rework (WPF RecordBreakageAsync).</summary>
public sealed record ExecutionBreakageRequest(
    string Reason,
    int EntryType = 1,
    bool IsFoc = false);

/// <summary>Body for POST /pairings — PairAsync(job, spec, serial).</summary>
public sealed record PairUnitRequest(Guid JobOrderId, int SpecGroupNumber, int SerialNumber);

/// <summary>Body for POST /assemblies — AssembleAsync(job, spec, serial,
/// type, spacer/interlayer/sealant).</summary>
public sealed record AssembleUnitRequest(
    Guid JobOrderId,
    int SpecGroupNumber,
    int SerialNumber,
    string AssemblyType = "DGU",
    string? SpacerInfo = null,
    string? InterlayerInfo = null,
    string? SealantInfo = null);

public sealed record ControlStartRequest(string? OperatorName = null);
public sealed record ControlPauseRequest(string? Reason = null);
public sealed record ControlHoldRequest(string HoldReason, string? Notes = null);
public sealed record ReleaseHoldRequest(string HoldId, string? Notes = null);
public sealed record AssignRequest(
    string WorkstationId, string? OperatorName = null, string? Notes = null);
public sealed record UnassignRequest;
public sealed record SetPriorityRequest(int Priority, string? Notes = null);
public sealed record SetDueDateRequest(DateTime? DueDate, string? Notes = null);

/// <summary>Body for POST /control/{id}/complete — delegates to the stage
/// flow (WPF reuses SetDoneQtyAsync).</summary>
public sealed record ControlCompleteRequest(int Qty);

public sealed record CreateWorkstationRequest(
    string Code, string? Name, int ProcessType, string? Description = null);

// ── read models ──────────────────────────────────────────────────────

public sealed record ProcessExecutionDto(
    Guid Id,
    string ExecutionId,
    string JobRef,
    Guid? JobOrderId,
    string SourceProformaId,
    int SpecGroupNumber,
    string SpecName,
    int SerialNumber,
    string Position,
    string PieceId,
    string InputPieceId,
    string OutputPieceId,
    int ProcessType,
    string ProcessName,
    int Sequence,
    string Status,
    Guid? ProductionPieceId,
    Guid? InputProductionPieceId,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int QtyRequired,
    int QtyDone,
    string EntryType,
    string Reason,
    string Notes,
    string QcStatus,
    string QcId,
    string ParentExecutionId,
    int ReworkSequence,
    bool IsFoc,
    string PreviousStage,
    string NextStage,
    string ProcessDetail,
    string ProcessDetailSummary,
    string IdentityClass,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record ProcessPairingDto(
    Guid Id,
    string PairingId,
    string JobRef,
    Guid? JobOrderId,
    int SpecGroupNumber,
    int SerialNumber,
    string Position,
    string PairId,
    string OuterPieceId,
    string InnerPieceId,
    string OuterGlassRef,
    string InnerGlassRef,
    Guid? OuterWorkItemId,
    Guid? InnerWorkItemId,
    string OuterExecutionId,
    string InnerExecutionId,
    string Status,
    string Notes,
    DateTime CreatedAt);

public sealed record AssemblyComponentDto(
    string PieceId,
    string GlassRef,
    string Position,
    string Role);

public sealed record ProcessAssemblyDto(
    Guid Id,
    string AssemblyId,
    string JobRef,
    Guid? JobOrderId,
    int SpecGroupNumber,
    int SerialNumber,
    string Position,
    string AssemblyLabel,
    string AssemblyType,
    IReadOnlyList<AssemblyComponentDto> Components,
    string SpacerInfo,
    string InterlayerInfo,
    string SealantInfo,
    string PairingId,
    string Status,
    int QtyRequired,
    int QtyDone,
    string Traceability,
    string Notes,
    DateTime? CompletedAt,
    DateTime CreatedAt);

public sealed record ProductionHoldDto(
    Guid Id,
    string HoldId,
    Guid ProductionPieceId,
    string JobRef,
    int SpecGroupNumber,
    int SerialNumber,
    string Position,
    string Stage,
    string HoldReason,
    DateTime HeldAt,
    DateTime? ReleasedAt,
    bool IsReleased,
    string Notes);

public sealed record ProductionAssignmentDto(
    Guid Id,
    string AssignmentId,
    Guid ProductionPieceId,
    string JobRef,
    int SpecGroupNumber,
    int SerialNumber,
    string Position,
    string Stage,
    string WorkstationId,
    string OperatorName,
    DateTime AssignedAt,
    DateTime? UnassignedAt,
    bool IsActive,
    string Notes);

public sealed record ProductionOverrideDto(
    Guid Id,
    string ControlId,
    Guid ProductionPieceId,
    string JobRef,
    int SpecGroupNumber,
    int SerialNumber,
    string Position,
    string Stage,
    int Priority,
    DateTime? DueDate,
    DateTime PrioritySetAt,
    string Notes);

public sealed record WorkstationDto(
    Guid Id,
    string WorkstationId,
    string Code,
    string Name,
    int ProcessType,
    string ProcessName,
    bool IsActive,
    string Description);

public sealed record QueueItemDto(
    Guid WorkItemId,
    string UniqueKey,
    string JobRef,
    Guid? JobOrderId,
    string SourceProformaId,
    string CustomerName,
    string ProjectName,
    string Salesman,
    int SpecGroupNumber,
    string SpecName,
    string BuildType,
    int SerialNumber,
    string GlassRef,
    string Position,
    int WidthMm,
    int HeightMm,
    int Qty,
    int QtyDone,
    int Remaining,
    string Stage,
    string StageLabel,
    string? PreviousStage,
    string? NextStage,
    IReadOnlyList<string> RequiredStages,
    string Status,
    string Readiness,
    string BlockReason,
    string Priority,
    DateTime? DueDate,
    string DueRisk,
    string QcResult,
    string QcId,
    bool IsRework,
    bool IsFoc,
    bool IsCancelled,
    int ReplacementSequence,
    string Operator,
    string WorkstationId,
    string WorkstationName,
    string AssignedBy,
    DateTime? AssignedAt,
    bool IsOnHold,
    string HoldReason,
    string HeldBy,
    DateTime? HeldAt,
    int MaterialRequired,
    int MaterialReserved,
    int MaterialAllocated,
    int MaterialConsumed,
    int MaterialAvailable,
    int MaterialShortage,
    bool HasMaterialShortage,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt,
    string CompletedBy,
    double? QueueWaitHours,
    double? ProcessingHours,
    string TraceabilityLabel,
    string OuterPieceId,
    string InnerPieceId,
    string PairId,
    string AssemblyId,
    string Notes);

public sealed record ProductionQueueDto(
    IReadOnlyList<QueueItemDto> Items,
    int Total,
    QueueFiltersDto AppliedFilters);

public sealed record QueueFiltersDto(
    string? JobRef,
    string? Stage,
    string? Readiness,
    string? Priority,
    string? Workstation,
    string? Search,
    bool IncludeCompleted,
    bool IncludeCancelled,
    int Take);

public sealed record DashboardKpisDto(
    int JobsInProduction,
    int ReadyToStart,
    int InProgress,
    int OnHold,
    int Blocked,
    int QcPending,
    int QcFailed,
    int Rework,
    int CompletedToday,
    int ReadyForDelivery,
    int OverdueJobs,
    int DueSoon,
    int MaterialShortages,
    string BottleneckProcess,
    int BottleneckCount,
    IReadOnlyDictionary<int, int> ProcessLoad);

public sealed record QueuePressureDto(
    string ProcessName,
    int ProcessType,
    int ReadyQueue,
    int BlockedQueue,
    int OverdueQueue,
    int QcBacklog,
    int ReworkBacklog,
    double AvgWaitHours,
    int Total,
    string PressureLabel);

public sealed record WorkstationLoadDto(
    string WorkstationId,
    string Code,
    string Name,
    int ProcessType,
    string ProcessName,
    int AssignedCount,
    int ReadyCount,
    int InProgressCount,
    int BlockedCount);

public sealed record ProductionProgressDto(
    string JobRef,
    string SpecName,
    string BuildType,
    int SpecGroupNumber,
    int SerialNumber,
    int Required,
    int Completed,
    int InProgress,
    int Blocked,
    int Paused,
    int Rework,
    int QcPending,
    int QcFailed,
    int OnHold,
    int Cancelled,
    int Remaining,
    double ProgressPct,
    string Label);

public sealed record ExecutionProgressDto(
    int Required,
    int Completed,
    int Remaining,
    int Failed,
    int Rework,
    int QcPending);

public sealed record WorkstationSeedResultDto(int Seeded, int Total);

// ── storage port ─────────────────────────────────────────────────────

/// <summary>
/// Storage port for the Phase 26a production extras. Coarse transactional
/// operations mirror the WPF's serializable transactions; deterministic
/// string identities make every write idempotent (the UNIQUE indexes in
/// 0021 are the backstop).
/// </summary>
public interface IProcessControlStore
{
    // Work items + job context
    Task<JobOrder?> FindJobAsync(Guid tenantId, Guid jobOrderId, CancellationToken ct);
    Task<ProductionPiece?> FindPieceAsync(Guid tenantId, Guid pieceId, CancellationToken ct);
    Task<IReadOnlyList<ProductionPiece>> ListPiecesByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct);

    // Process executions (WPF SavedProcessExecution)
    Task<ProcessExecution?> FindExecutionAsync(
        Guid tenantId, string executionId, CancellationToken ct);
    Task<ProcessExecution?> FindExecutionByPieceAsync(
        Guid tenantId, Guid pieceId, int processType, int reworkSequence,
        CancellationToken ct);
    Task<IReadOnlyList<ProcessExecution>> ListExecutionsByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct);
    Task<IReadOnlyList<ProcessExecution>> ListExecutionsByProcessAsync(
        Guid tenantId, int processType, int take, CancellationToken ct);

    /// <summary>Insert one execution (idempotent on execution_id — an
    /// existing row is returned instead).</summary>
    Task<ProcessExecution> UpsertExecutionAsync(
        Guid tenantId, ProcessExecution execution, CancellationToken ct);

    Task<bool> TryUpdateExecutionAsync(
        ProcessExecution execution, CancellationToken ct);

    /// <summary>WPF ProjectJobAsync — one Pending execution per live work
    /// item, re-linking existing rows and removing orphaned untouched
    /// ones. Returns how many rows were added.</summary>
    Task<int> ProjectPendingExecutionsAsync(
        Guid tenantId, Guid jobOrderId, Guid actorId, CancellationToken ct);

    /// <summary>WPF SyncLinkedExecutionQty — the work item is
    /// authoritative; its Qty/QtyDone/status mirror onto the sequence-0
    /// execution.</summary>
    Task SyncLinkedExecutionsAsync(
        Guid tenantId, ProductionPiece piece, CancellationToken ct);

    /// <summary>WPF control transitions (Start/Pause/Resume) — one
    /// serializable transaction: re-fetch the piece, idempotent when it
    /// already carries the target status mid-flight, flip the status,
    /// append the control event, mirror the linked execution.</summary>
    Task<(bool Ok, string Message)> TransitionPieceAsync(
        Guid tenantId, Guid pieceId, Guid actorId, string toStatus,
        string entryReason, string entryNotes, bool syncExecutions,
        CancellationToken ct);

    // Pairings (WPF SavedProcessPairing)
    Task<IReadOnlyList<ProcessPairing>> ListPairingsByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct);
    Task<IReadOnlyList<ProcessPairing>> ListPairingsByUnitAsync(
        Guid tenantId, Guid jobOrderId, int specGroupNumber, int serialNumber,
        CancellationToken ct);

    /// <summary>Insert the computed pairings in one serializable
    /// transaction, skipping existing pairing_ids (idempotent) and
    /// re-checking the gating inputs (outer/inner cutting QtyDone) —
    /// WPF PairAsync. Returns the stored rows for the unit.</summary>
    Task<IReadOnlyList<ProcessPairing>> InsertPairingsAsync(
        Guid tenantId, Guid jobOrderId, int specGroupNumber, int serialNumber,
        IReadOnlyList<ProcessPairing> rows, CancellationToken ct);

    // Assemblies (WPF SavedProcessAssembly)
    Task<IReadOnlyList<ProcessAssembly>> ListAssembliesByJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct);
    Task<IReadOnlyList<ProcessAssembly>> ListAssembliesByUnitAsync(
        Guid tenantId, Guid jobOrderId, int specGroupNumber, int serialNumber,
        CancellationToken ct);

    /// <summary>Insert the computed assemblies in one serializable
    /// transaction, skipping existing assembly_ids (idempotent) — WPF
    /// AssembleAsync. Returns the stored rows for the unit.</summary>
    Task<IReadOnlyList<ProcessAssembly>> InsertAssembliesAsync(
        Guid tenantId, Guid jobOrderId, int specGroupNumber, int serialNumber,
        IReadOnlyList<ProcessAssembly> rows, CancellationToken ct);

    // Control holds (WPF SavedProductionControlHold)
    Task<ProductionControlHold?> FindHoldAsync(
        Guid tenantId, string holdId, CancellationToken ct);
    Task<IReadOnlyList<ProductionControlHold>> ListHoldsByPieceAsync(
        Guid tenantId, Guid pieceId, CancellationToken ct);
    Task<ProductionControlHold?> FindActiveHoldByPieceAsync(
        Guid tenantId, Guid pieceId, CancellationToken ct);
    Task<int> CountHoldsByPieceAsync(Guid tenantId, Guid pieceId, CancellationToken ct);
    Task InsertHoldAsync(
        Guid tenantId, ProductionControlHold hold, CancellationToken ct);
    Task<bool> TryUpdateHoldAsync(
        ProductionControlHold hold, CancellationToken ct);

    // Operator/workstation assignments (WPF SavedProductionControlAssignment)
    Task<IReadOnlyList<ProductionControlAssignment>> ListAssignmentsByPieceAsync(
        Guid tenantId, Guid pieceId, bool activeOnly, CancellationToken ct);
    Task<int> CountAssignmentsByPieceAsync(
        Guid tenantId, Guid pieceId, CancellationToken ct);

    /// <summary>WPF AssignAsync in one transaction: idempotent when the
    /// same workstation+operator is already active, previous actives are
    /// deactivated (history preserved). Returns the assignment id.</summary>
    Task<(bool Ok, string Message, string AssignmentId)> AssignAsync(
        Guid tenantId, ProductionPiece piece, string workstationId,
        string operatorName, string notes, Guid actorId, CancellationToken ct);

    /// <summary>WPF UnassignAsync — deactivate all active assignments for
    /// the piece (idempotent when none).</summary>
    Task<(bool Ok, string Message)> UnassignAsync(
        Guid tenantId, Guid pieceId, Guid actorId, CancellationToken ct);

    // Priority/due-date overrides (WPF SavedProductionControlOverride)
    Task<ProductionControlOverride?> FindOverrideAsync(
        Guid tenantId, string controlId, CancellationToken ct);
    Task<ProductionControlOverride> UpsertOverrideAsync(
        Guid tenantId, ProductionControlOverride overrideRow, CancellationToken ct);

    // Workstations (WPF SavedWorkstation)
    Task<IReadOnlyList<Workstation>> ListWorkstationsAsync(
        Guid tenantId, bool activeOnly, CancellationToken ct);
    Task<Workstation?> FindWorkstationAsync(
        Guid tenantId, string workstationId, CancellationToken ct);

    /// <summary>WPF CreateAsync — idempotent on the upper-invariant
    /// code.</summary>
    Task<Workstation> CreateWorkstationAsync(
        Guid tenantId, string code, string name, int processType,
        string description, Guid? actorId, CancellationToken ct);

    /// <summary>WPF EnsureSeededAsync — the ten default stations, only
    /// when the tenant has none.</summary>
    Task<int> EnsureWorkstationsSeededAsync(Guid tenantId, CancellationToken ct);

    // Queue bundle — one batched load for the queue/dashboard/progress
    // readers (WPF GetQueueAsync's batched supporting reads).
    Task<ProductionQueueData> LoadQueueDataAsync(
        Guid tenantId, Guid? jobOrderId, bool includeCancelled,
        bool includeCompleted, CancellationToken ct);
}

/// <summary>The batched queue context (WPF GetQueueAsync loads work
/// items + QC + pairings + assemblies + holds + assignments + overrides +
/// workstations + reservations before building items).</summary>
public sealed record ProductionQueueData(
    IReadOnlyList<ProductionPiece> Pieces,
    IReadOnlyList<JobOrder> Jobs,
    IReadOnlyList<MaterialReservation> Reservations,
    IReadOnlyList<ProductionControlHold> Holds,
    IReadOnlyList<ProductionControlAssignment> Assignments,
    IReadOnlyList<ProductionControlOverride> Overrides,
    IReadOnlyList<Workstation> Workstations,
    IReadOnlyList<ProductionEvent> QcEvents,
    IReadOnlyList<ProcessPairing> Pairings,
    IReadOnlyList<ProcessAssembly> Assemblies);
