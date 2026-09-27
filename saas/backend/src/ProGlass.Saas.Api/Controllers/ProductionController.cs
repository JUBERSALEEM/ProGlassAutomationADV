using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.Production;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// /api/v1/production — the Production module core (Phase 9b): the
/// deterministic plan, the release (unique-key preservation), the floor
/// lifecycle (stage progress, QC, breakage, cancellation) and the
/// immutable event log. Phase 26a adds the process execution, pairing/
/// assembly, control (start/pause/resume/hold/assign/priority), queue/
/// dashboard/progress and workstation endpoints. The WPF production
/// services carry no OperationGuard (the UI gates the floor through the
/// Production role) — the SaaS gates writes with JobOrder.Edit, the
/// Production role's floor permission (documented difference).
/// </summary>
[Route("api/v1/production")]
public sealed class ProductionController(
    ProductionService production,
    ProcessExecutionService executions,
    PairingAssemblyService pairing,
    ProductionControlService control,
    ProductionQueueService queue,
    WorkstationService workstations)
    : TenantControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? jobOrderId, [FromQuery] string? stage,
        [FromQuery] string? status, [FromQuery] bool includeCancelled,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
        => Ok(await production.ListAsync(
            RequiredTenantId, jobOrderId, stage, status, includeCancelled,
            page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await production.GetAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("{id:guid}/events")]
    public async Task<IActionResult> GetEvents(Guid id, CancellationToken ct)
    {
        var result = await production.GetEventsAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>The deterministic plan — every spec row fanned out into
    /// stage work items (nothing persists until release).</summary>
    [HttpGet("job-orders/{jobOrderId:guid}/plan")]
    public async Task<IActionResult> Plan(Guid jobOrderId, CancellationToken ct)
    {
        var result = await production.PlanAsync(
            RequiredTenantId, jobOrderId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Release the plan to the floor. Deterministic unique keys
    /// mean a re-release only inserts NEW items — existing rows and their
    /// statuses survive.</summary>
    [HttpPost("job-orders/{jobOrderId:guid}/release")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Release(Guid jobOrderId, CancellationToken ct)
    {
        var result = await production.ReleaseAsync(
            RequiredTenantId, RequiredUserId, jobOrderId, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("job-orders/{jobOrderId:guid}")]
    public async Task<IActionResult> ListForJob(
        Guid jobOrderId, CancellationToken ct)
    {
        var result = await production.ListForJobAsync(
            RequiredTenantId, jobOrderId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Floor progress: QtyDone (auto-status) or an explicit
    /// status.</summary>
    [HttpPost("{id:guid}/stage")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Stage(
        Guid id, [FromBody] StageProgressRequest request, CancellationToken ct)
    {
        var result = await production.RecordStageAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Record QC (Passed/Failed/Rework — WPF
    /// RecordQcAsync rules).</summary>
    [HttpPost("{id:guid}/qc")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Qc(
        Guid id, [FromBody] QcRequest request, CancellationToken ct)
    {
        var result = await production.RecordQcAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Soft-cancel one work item (idempotent; the history row
    /// survives).</summary>
    [HttpPost("{id:guid}/cancel")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Cancel(
        Guid id, [FromBody] CancelPieceRequest request, CancellationToken ct)
    {
        var result = await production.CancelAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Record breakage (qty + reason; the replacement workflow
    /// is a later slice).</summary>
    [HttpPost("{id:guid}/breakage")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Breakage(
        Guid id, [FromBody] BreakageRequest request, CancellationToken ct)
    {
        var result = await production.RecordBreakageAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    // ── Phase 26a — process executions (WPF Phase 16) ───────────────

    /// <summary>Executions by job (WPF GetByJobAsync) or by process type
    /// (WPF GetByProcessAsync, most recent first).</summary>
    [HttpGet("executions")]
    public async Task<IActionResult> Executions(
        [FromQuery] Guid? jobOrderId, [FromQuery] int? processType,
        CancellationToken ct)
    {
        if (jobOrderId is not null)
        {
            var byJob = await executions.ListByJobAsync(
                RequiredTenantId, jobOrderId, ct);
            return byJob.Success ? Ok(byJob.Value) : Fail(byJob);
        }
        if (processType is not null)
        {
            var byProcess = await executions.ListByProcessAsync(
                RequiredTenantId, processType.Value, ct);
            return byProcess.Success ? Ok(byProcess.Value) : Fail(byProcess);
        }
        return BadRequest(new
        {
            code = "production.validation_failed",
            message = "jobOrderId or processType is required."
        });
    }

    [HttpGet("executions/progress")]
    public async Task<IActionResult> ExecutionProgress(
        [FromQuery] Guid jobOrderId, CancellationToken ct)
    {
        var result = await executions.GetProgressByJobAsync(
            RequiredTenantId, jobOrderId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("executions/{executionId}")]
    public async Task<IActionResult> Execution(
        string executionId, CancellationToken ct)
    {
        var result = await executions.GetAsync(RequiredTenantId, executionId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Start an execution (idempotent; the route predecessor
    /// must be complete).</summary>
    [HttpPost("executions/start")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> StartExecution(
        [FromBody] StartExecutionRequest request, CancellationToken ct)
    {
        var result = await executions.StartAsync(
            RequiredTenantId, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Pause an execution (completed rows cannot pause).</summary>
    [HttpPost("executions/{executionId}/pause")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> PauseExecution(
        string executionId, [FromBody] PauseExecutionRequest? request,
        CancellationToken ct)
    {
        var result = await executions.PauseAsync(
            RequiredTenantId, RequiredUserId, executionId,
            request ?? new PauseExecutionRequest(), ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Complete an execution (incremental QtyDone, clamped —
    /// piece-flow, no destructive overwrite).</summary>
    [HttpPost("executions/{executionId}/complete")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> CompleteExecution(
        string executionId, [FromBody] CompleteExecutionRequest request,
        CancellationToken ct)
    {
        var result = await executions.CompleteAsync(
            RequiredTenantId, RequiredUserId, executionId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Record breakage/rework during a process (EntryType 1
    /// Breakage / 2 Rework; creates the traceable replacement
    /// execution).</summary>
    [HttpPost("executions/{executionId}/breakage")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> ExecutionBreakage(
        string executionId, [FromBody] ExecutionBreakageRequest request,
        CancellationToken ct)
    {
        var result = await executions.RecordBreakageAsync(
            RequiredTenantId, RequiredUserId, executionId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    // ── Phase 26a — pairing + assembly (WPF Phase 16) ───────────────

    /// <summary>Deterministic pairing for DGU (Outer + Inner by identity,
    /// never by dimension match).</summary>
    [HttpPost("pairings")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Pair(
        [FromBody] PairUnitRequest request, CancellationToken ct)
    {
        var result = await pairing.PairAsync(
            RequiredTenantId, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("pairings")]
    public async Task<IActionResult> Pairings(
        [FromQuery] Guid jobOrderId, CancellationToken ct)
    {
        var result = await pairing.ListPairingsAsync(
            RequiredTenantId, jobOrderId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>DGU/Lamination assembly with preserved component
    /// lineage.</summary>
    [HttpPost("assemblies")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Assemble(
        [FromBody] AssembleUnitRequest request, CancellationToken ct)
    {
        var result = await pairing.AssembleAsync(
            RequiredTenantId, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("assemblies")]
    public async Task<IActionResult> Assemblies(
        [FromQuery] Guid jobOrderId, CancellationToken ct)
    {
        var result = await pairing.ListAssembliesAsync(
            RequiredTenantId, jobOrderId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    // ── Phase 26a — production control (WPF Phase 17) ───────────────

    /// <summary>Controlled start — the readiness engine must say READY.
    /// </summary>
    [HttpPost("control/{id:guid}/start")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> ControlStart(
        Guid id, [FromBody] ControlStartRequest? request, CancellationToken ct)
    {
        var result = await control.StartAsync(
            RequiredTenantId, RequiredUserId, id,
            request ?? new ControlStartRequest(), ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Pause (only InProgress can pause).</summary>
    [HttpPost("control/{id:guid}/pause")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> ControlPause(
        Guid id, [FromBody] ControlPauseRequest? request, CancellationToken ct)
    {
        var result = await control.PauseAsync(
            RequiredTenantId, RequiredUserId, id,
            request ?? new ControlPauseRequest(), ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Resume (only Paused can resume).</summary>
    [HttpPost("control/{id:guid}/resume")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> ControlResume(
        Guid id, CancellationToken ct)
    {
        var result = await control.ResumeAsync(
            RequiredTenantId, RequiredUserId, id, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Hold (idempotent; the history row is kept when
    /// released).</summary>
    [HttpPost("control/{id:guid}/hold")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Hold(
        Guid id, [FromBody] ControlHoldRequest request, CancellationToken ct)
    {
        var result = await control.HoldAsync(
            RequiredTenantId, RequiredUserId, id, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Release a hold (appends " | Release: …" to the notes).
    /// </summary>
    [HttpPost("control/{id:guid}/release-hold")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> ReleaseHold(
        Guid id, [FromBody] ReleaseHoldRequest request, CancellationToken ct)
    {
        var result = await control.ReleaseHoldAsync(
            RequiredTenantId, RequiredUserId, id, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("control/{id:guid}/holds")]
    public async Task<IActionResult> Holds(Guid id, CancellationToken ct)
    {
        var result = await control.ListHoldsAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Assign an operator + workstation (previous actives are
    /// deactivated, never deleted).</summary>
    [HttpPost("control/{id:guid}/assign")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Assign(
        Guid id, [FromBody] AssignRequest request, CancellationToken ct)
    {
        var result = await control.AssignAsync(
            RequiredTenantId, RequiredUserId, id, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Unassign (idempotent).</summary>
    [HttpPost("control/{id:guid}/unassign")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Unassign(Guid id, CancellationToken ct)
    {
        var result = await control.UnassignAsync(
            RequiredTenantId, RequiredUserId, id, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("control/{id:guid}/assignments")]
    public async Task<IActionResult> Assignments(
        Guid id, [FromQuery] bool activeOnly, CancellationToken ct)
    {
        var result = await control.ListAssignmentsAsync(
            RequiredTenantId, id, activeOnly, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Set priority 0/1/2 — influences queue ordering only,
    /// never bypasses safety dependencies.</summary>
    [HttpPost("control/{id:guid}/priority")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> SetPriority(
        Guid id, [FromBody] SetPriorityRequest request, CancellationToken ct)
    {
        var result = await control.SetPriorityAsync(
            RequiredTenantId, RequiredUserId, id, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Set or clear (null) the due-date override.</summary>
    [HttpPost("control/{id:guid}/due-date")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> SetDueDate(
        Guid id, [FromBody] SetDueDateRequest request, CancellationToken ct)
    {
        var result = await control.SetDueDateAsync(
            RequiredTenantId, RequiredUserId, id, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Complete via the piece flow (WPF reuses SetDoneQtyAsync).
    /// </summary>
    [HttpPost("control/{id:guid}/complete")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> ControlComplete(
        Guid id, [FromBody] ControlCompleteRequest request, CancellationToken ct)
    {
        var result = await control.CompleteAsync(
            RequiredTenantId, RequiredUserId, id, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    // ── Phase 26a — the unified queue + dashboard + progress ────────

    /// <summary>The unified production queue — deterministic ordering
    /// (Ready → InProgress → other → Blocked, priority, due date,
    /// process sequence, JobRef, piece identity).</summary>
    [HttpGet("queue")]
    public async Task<IActionResult> Queue(
        [FromQuery] Guid? jobOrderId, [FromQuery] string? stage,
        [FromQuery] string? readiness, [FromQuery] string? priority,
        [FromQuery] string? workstation, [FromQuery] string? search,
        [FromQuery] string? customer, [FromQuery] bool includeCompleted,
        [FromQuery] bool includeCancelled, [FromQuery] int? take,
        CancellationToken ct)
    {
        var result = await queue.GetQueueAsync(
            RequiredTenantId, jobOrderId, stage, readiness, priority,
            workstation, search, customer, includeCompleted,
            includeCancelled, take ?? 200, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Ready-only queue (deterministic).</summary>
    [HttpGet("queue/executable")]
    public async Task<IActionResult> ExecutableQueue(
        [FromQuery] int? take, CancellationToken ct)
    {
        var result = await queue.GetExecutableQueueAsync(
            RequiredTenantId, take ?? 100, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>The blocked queue with explainable block reasons.</summary>
    [HttpGet("queue/blocked")]
    public async Task<IActionResult> BlockedQueue(
        [FromQuery] int? take, CancellationToken ct)
    {
        var result = await queue.GetBlockedQueueAsync(
            RequiredTenantId, take ?? 100, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Factory KPIs (real production data, no duplicated
    /// counters).</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var result = await queue.GetKpisAsync(RequiredTenantId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Per-stage queue pressure.</summary>
    [HttpGet("dashboard/pressure")]
    public async Task<IActionResult> Pressure(CancellationToken ct)
    {
        var result = await queue.GetQueuePressureAsync(RequiredTenantId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Assigned/ready/in-progress/blocked per active station.
    /// </summary>
    [HttpGet("dashboard/workstation-load")]
    public async Task<IActionResult> WorkstationLoad(CancellationToken ct)
    {
        var result = await queue.GetWorkstationLoadAsync(RequiredTenantId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Progress by level: job (default), overall or process —
    /// physical pieces, DGU components never double-counted.</summary>
    [HttpGet("progress")]
    public async Task<IActionResult> Progress(
        [FromQuery] string level = "job", [FromQuery] Guid? jobOrderId = null,
        CancellationToken ct = default)
    {
        switch ((level ?? "job").Trim().ToLowerInvariant())
        {
            case "overall":
                var overall = await queue.GetOverallProgressAsync(RequiredTenantId, ct);
                return overall.Success ? Ok(overall.Value) : Fail(overall);
            case "process":
                var process = await queue.GetProcessProgressAsync(RequiredTenantId, ct);
                return process.Success ? Ok(process.Value) : Fail(process);
            default:
                var jobs = await queue.GetJobProgressAsync(
                    RequiredTenantId, jobOrderId, ct);
                return jobs.Success ? Ok(jobs.Value) : Fail(jobs);
        }
    }

    // ── Phase 26a — the workstation master (ERP-level) ──────────────

    [HttpGet("workstations")]
    public async Task<IActionResult> WorkstationList(
        [FromQuery] bool activeOnly, CancellationToken ct)
    {
        var result = await workstations.ListAsync(RequiredTenantId, activeOnly, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("workstations/{workstationId}")]
    public async Task<IActionResult> WorkstationGet(
        string workstationId, CancellationToken ct)
    {
        var result = await workstations.GetAsync(
            RequiredTenantId, workstationId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Create a station (idempotent on the code).</summary>
    [HttpPost("workstations")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> WorkstationCreate(
        [FromBody] CreateWorkstationRequest request, CancellationToken ct)
    {
        var result = await workstations.CreateAsync(
            RequiredTenantId, RequiredUserId, request, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Seed the ten default stations (idempotent — only when
    /// the tenant has none).</summary>
    [HttpPost("workstations/seed")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> WorkstationSeed(CancellationToken ct)
    {
        var result = await workstations.EnsureSeededAsync(RequiredTenantId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }
}
