using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.Production;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// /api/v1/reservations — the material-reservation hold ledger (Phase
/// 9c, WPF MaterialReservationService). Writes gated JobOrder.Edit (the
/// Production role's floor permission — the WPF reserved from the
/// optimization/floor UIs).
/// </summary>
[Route("api/v1/reservations")]
public sealed class ReservationsController(ReservationService reservations)
    : TenantControllerBase
{
    /// <summary>Reserve sheet stock for a job line. Idempotent on the
    /// deterministic (job, spec, serial, sheet, session) key.</summary>
    [HttpPost]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Reserve(
        [FromBody] ReserveRequest request, CancellationToken ct)
    {
        var result = await reservations.ReserveAsync(
            RequiredTenantId, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("job-orders/{jobOrderId:guid}")]
    public async Task<IActionResult> ListForJob(
        Guid jobOrderId, CancellationToken ct)
    {
        var result = await reservations.ListForJobAsync(
            RequiredTenantId, jobOrderId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Availability for one sheet: Balance (Stock − Used) −
    /// active holds.</summary>
    [HttpGet("sheets/{sheetId:guid}/availability")]
    public async Task<IActionResult> Availability(Guid sheetId, CancellationToken ct)
    {
        var result = await reservations.GetAvailabilityAsync(
            RequiredTenantId, sheetId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Release a hold (consumed material is never
    /// released).</summary>
    [HttpPost("{id:guid}/release")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Release(
        Guid id, [FromBody] ReservationActionRequest request, CancellationToken ct)
    {
        var result = await reservations.ReleaseAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Cancel a hold (consumed material is never cancelled;
    /// idempotent).</summary>
    [HttpPost("{id:guid}/cancel")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Cancel(
        Guid id, [FromBody] ReservationActionRequest request, CancellationToken ct)
    {
        var result = await reservations.CancelAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Consume a hold (terminal — the physical inventory
    /// consumption contract).</summary>
    [HttpPost("{id:guid}/consume")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Consume(
        Guid id, [FromBody] ReservationActionRequest request, CancellationToken ct)
    {
        var result = await reservations.ConsumeAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }
}
