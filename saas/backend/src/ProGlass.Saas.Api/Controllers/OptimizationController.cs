using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.Optimization;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// /api/v1/optimization — the glass cutting optimizer (Phase 11 core):
/// run (one session per glass composition group, cut sequences and
/// leftovers persisted), session and sheet reads. Writes gated
/// JobOrder.Edit (the floor/optimizer permission the Production role
/// carries — the WPF optimization UI lives behind the same role).
/// </summary>
[Route("api/v1/optimization")]
public sealed class OptimizationController(OptimizationService optimization)
    : TenantControllerBase
{
    /// <summary>Run the optimizer: pieces from a job order's cutting
    /// plan (or explicit), grouped by glass composition, packed against
    /// the Sheet Store racks of the group's thickness.</summary>
    [HttpPost("run")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Run(
        [FromBody] RunOptimizationRequest request, CancellationToken ct)
    {
        var result = await optimization.RunAsync(
            RequiredTenantId, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("sessions/{id:guid}")]
    public async Task<IActionResult> GetSession(Guid id, CancellationToken ct)
    {
        var result = await optimization.GetSessionAsync(
            RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>The session's sheets with placements, cut sequences and
    /// leftovers (the 2D view's data).</summary>
    [HttpGet("sessions/{id:guid}/sheets")]
    public async Task<IActionResult> GetSheets(Guid id, CancellationToken ct)
    {
        var result = await optimization.GetSheetsAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }
}
