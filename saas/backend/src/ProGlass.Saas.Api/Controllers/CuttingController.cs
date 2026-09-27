using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.Production;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// /api/v1/cutting — the Phase 15/26b cutting workflow family: the
/// requirement build (JO → per-piece pane instances), the per-group
/// workflow (stock selection → reservation → optimization → verified
/// layout → allocation, with compensation releases), the session/sheet/
/// placed-piece reads, cutting completion (work-item QtyDone + allocation
/// consumption), breakage during cutting (Phase 14 replacement) and the
/// job progress tuple. Reads are tenant-auth; writes gate with
/// JobOrder.Edit like the rest of the production floor (mapping §1.7d).
/// </summary>
[Route("api/v1/cutting")]
public sealed class CuttingController(
    CuttingService cutting)
    : TenantControllerBase
{
    /// <summary>The read-only requirement build — every live Cutting work
    /// item expanded into per-piece requirements with pane-instance
    /// identity (WPF BuildFromProductionAsync).</summary>
    [HttpGet("requirements")]
    public async Task<IActionResult> Requirements(
        [FromQuery] Guid jobOrderId, CancellationToken ct)
    {
        var result = await cutting.BuildRequirementsAsync(
            RequiredTenantId, jobOrderId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Run the full per-group workflow. Groups that fail are
    /// skipped with their reservations released (compensation); the run
    /// succeeds when no group errored and at least one session
    /// exists.</summary>
    [HttpPost("run")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Run(
        RunCuttingRequest request, CancellationToken ct)
    {
        var result = await cutting.RunAsync(
            RequiredTenantId, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> Sessions(
        [FromQuery] Guid jobOrderId, CancellationToken ct)
    {
        var result = await cutting.ListSessionsByJobAsync(
            RequiredTenantId, jobOrderId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("sessions/{cuttingSessionId}")]
    public async Task<IActionResult> GetSession(
        string cuttingSessionId, CancellationToken ct)
    {
        var result = await cutting.GetSessionAsync(
            RequiredTenantId, cuttingSessionId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("sessions/{cuttingSessionId}/sheets")]
    public async Task<IActionResult> Sheets(
        string cuttingSessionId, CancellationToken ct)
    {
        var result = await cutting.ListSheetsBySessionAsync(
            RequiredTenantId, cuttingSessionId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("sessions/{cuttingSessionId}/pieces")]
    public async Task<IActionResult> Pieces(
        string cuttingSessionId, CancellationToken ct)
    {
        var result = await cutting.ListPiecesBySessionAsync(
            RequiredTenantId, cuttingSessionId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Complete every Allocated piece of the session, then flip
    /// the session to Completed (WPF CompleteSessionAsync).</summary>
    [HttpPost("sessions/{cuttingSessionId}/complete")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> CompleteSession(
        string cuttingSessionId, CancellationToken ct)
    {
        var result = await cutting.CompleteSessionAsync(
            RequiredTenantId, RequiredUserId, cuttingSessionId, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Complete one placed piece: piece → Completed, work item
    /// QtyDone +1 (clamped), allocation → Consumed with the reservation
    /// cascade. Idempotent.</summary>
    [HttpPost("pieces/{placedPieceId}/complete")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> CompletePiece(
        string placedPieceId, [FromBody] CompletePieceRequest? request,
        CancellationToken ct)
    {
        var result = await cutting.CompletePieceAsync(
            RequiredTenantId, RequiredUserId, placedPieceId,
            request ?? new CompletePieceRequest(null), ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Breakage during cutting: the placed piece flips to
    /// Breakage/FOC and the Phase 14 replacement flow creates the
    /// replacement work item (the original is never silently
    /// modified).</summary>
    [HttpPost("pieces/{placedPieceId}/breakage")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Breakage(
        string placedPieceId, CuttingBreakageRequest request,
        CancellationToken ct)
    {
        var result = await cutting.RecordBreakageDuringCuttingAsync(
            RequiredTenantId, RequiredUserId, placedPieceId, request,
            ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>The job's cutting progress tuple: required / completed /
    /// remaining / breakage / foc / rework / qcPending (WPF
    /// GetProgressByJobAsync).</summary>
    [HttpGet("progress")]
    public async Task<IActionResult> Progress(
        [FromQuery] Guid jobOrderId, CancellationToken ct)
    {
        var result = await cutting.GetProgressByJobAsync(
            RequiredTenantId, jobOrderId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }
}
