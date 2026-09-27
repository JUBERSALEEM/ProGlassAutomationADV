using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.Production;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// /api/v1/production — the Production module core (Phase 9b): the
/// deterministic plan, the release (unique-key preservation), the floor
/// lifecycle (stage progress, QC, breakage, cancellation) and the
/// immutable event log. The WPF production services carry no
/// OperationGuard (the UI gates the floor through the Production role) —
/// the SaaS gates writes with JobOrder.Edit, the Production role's floor
/// permission (documented difference).
/// </summary>
[Route("api/v1/production")]
public sealed class ProductionController(ProductionService production)
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
}
