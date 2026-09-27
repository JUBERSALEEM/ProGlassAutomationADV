using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.Delivery;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// /api/v1/delivery-notes — the Delivery module core (Phase 10): the
/// master DN balance tracker, the one-path create-or-open, the status
/// machine and the soft-close void. Permissions follow the WPF
/// OperationGuard calls (DeliveryNote.Edit for every write — the WPF
/// gated create/update/dispatch/void the same way).
/// </summary>
[Route("api/v1/delivery-notes")]
public sealed class DeliveryNotesController(DeliveryService delivery)
    : TenantControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search, [FromQuery] string? status,
        [FromQuery] bool includeVoid, [FromQuery] bool mastersOnly,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
        => Ok(await delivery.ListAsync(
            RequiredTenantId, search, status, includeVoid, mastersOnly,
            page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await delivery.GetAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>The one-path NEW DN / CREATE DN (WPF
    /// DeliveryNoteCreateService.CreateOrOpenDispatchingAsync): opens
    /// the existing open note or creates the next one from the master's
    /// remaining balance — one rule set for every entry point.</summary>
    [HttpPost("job-orders/{jobOrderId:guid}")]
    [HasPermission(AppPermissions.DeliveryNoteEdit)]
    public async Task<IActionResult> CreateOrOpen(
        Guid jobOrderId, CancellationToken ct)
    {
        var result = await delivery.CreateOrOpenDispatchingAsync(
            RequiredTenantId, RequiredUserId, jobOrderId, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>The master DN (balance tracker) for a job order.</summary>
    [HttpGet("job-orders/{jobOrderId:guid}/master")]
    public async Task<IActionResult> GetMaster(Guid jobOrderId, CancellationToken ct)
    {
        var result = await delivery.GetMasterAsync(RequiredTenantId, jobOrderId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>God View: every note of a job order (master, dispatch,
    /// voided — newest first).</summary>
    [HttpGet("job-orders/{jobOrderId:guid}")]
    public async Task<IActionResult> ListForJob(Guid jobOrderId, CancellationToken ct)
    {
        var result = await delivery.ListForJobAsync(RequiredTenantId, jobOrderId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>The workspace's dispatch edit: the confirm block + line
    /// delivered quantities (open notes only).</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(AppPermissions.DeliveryNoteEdit)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateDispatchRequest request, CancellationToken ct)
    {
        var result = await delivery.UpdateDispatchAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpPost("{id:guid}/transition")]
    [HasPermission(AppPermissions.DeliveryNoteEdit)]
    public async Task<IActionResult> Transition(
        Guid id, [FromBody] DeliveryTransitionRequest request, CancellationToken ct)
    {
        var result = await delivery.TransitionAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Soft-close: the row survives for the audit trail, Status
    /// moves to the void target (Cancelled), the master's remaining
    /// balance is refreshed. Idempotent.</summary>
    [HttpPost("{id:guid}/void")]
    [HasPermission(AppPermissions.DeliveryNoteEdit)]
    public async Task<IActionResult> Void(
        Guid id, [FromBody] VoidDeliveryRequest request, CancellationToken ct)
    {
        var result = await delivery.VoidAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }
}
