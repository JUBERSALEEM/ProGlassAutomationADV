using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.JobOrders;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// /api/v1/job-orders — the Job Order module core (Phase 9): convert a
/// proforma invoice into a production job, track it through the status
/// machine, edit the production details, soft-close. Permissions follow
/// the WPF OperationGuard calls (JobOrder.Edit for create/update;
/// void is the soft-close edit path — the WPF gated it JobOrder.Edit).
/// </summary>
[Route("api/v1/job-orders")]
public sealed class JobOrdersController(JobOrderService jobOrders)
    : TenantControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search, [FromQuery] string? status,
        [FromQuery] bool includeVoid, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken ct)
        => Ok(await jobOrders.ListAsync(
            RequiredTenantId, search, status, includeVoid, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await jobOrders.GetAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("proforma/{proformaId:guid}")]
    public async Task<IActionResult> GetByProforma(Guid proformaId, CancellationToken ct)
    {
        var result = await jobOrders.GetByProformaAsync(
            RequiredTenantId, proformaId, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Convert a proforma invoice into a Job Order (WPF
    /// CreateJobOrderAsync). A 409 that lists existing job orders asks
    /// for the explicit allowExisting re-post (the WPF dialog's
    /// Yes/No).</summary>
    [HttpPost]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Create(
        [FromBody] CreateJobOrderRequest request, CancellationToken ct)
    {
        var result = await jobOrders.CreateFromProformaAsync(
            RequiredTenantId, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Edit the production details (WPF detail-view save) with
    /// the optimistic RowVersion check.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateJobOrderRequest request, CancellationToken ct)
    {
        var result = await jobOrders.UpdateAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpPost("{id:guid}/transition")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Transition(
        Guid id, [FromBody] JobOrderTransitionRequest request, CancellationToken ct)
    {
        var result = await jobOrders.TransitionAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Soft-close: the row survives (number + snapshot + audit),
    /// Status moves to the void target (Rejected). Idempotent.</summary>
    [HttpPost("{id:guid}/void")]
    [HasPermission(AppPermissions.JobOrderEdit)]
    public async Task<IActionResult> Void(
        Guid id, [FromBody] VoidJobOrderRequest request, CancellationToken ct)
    {
        var result = await jobOrders.VoidAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }
}
