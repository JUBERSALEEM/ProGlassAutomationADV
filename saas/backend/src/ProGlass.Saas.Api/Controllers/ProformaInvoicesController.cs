using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// /api/v1/proforma-invoices — the 9-step wizard's document core
/// (Phase 7a). The WPF permission map:
///
///   • create / revise  → ProformaInvoice.Create (WPF OperationGuard on
///     InsertAsync + CreateRevisionAsync)
///   • transitions, spec-group saves → ProformaInvoice.Edit (the WPF has
///     no separate "approve" grant — the manager roles hold Edit; the
///     state machine's RequiresApproval flag travels in the audit entry)
///   • void → ProformaInvoice.Delete (WPF VoidAsync demands exactly this)
///   • reads → any authenticated user (WPF list needs ProformaInvoice.View,
///     which every role holds)
///
/// Later Phase 7 slices (spec registry apply, other charges, P&amp;L, PDF)
/// extend this controller; the document core ships first.
/// </summary>
[Route("api/v1/proforma-invoices")]
public sealed class ProformaInvoicesController(ProformaService proforma)
    : TenantControllerBase
{
    [HttpPost]
    [HasPermission(AppPermissions.ProformaCreate)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProformaRequest request, CancellationToken ct)
    {
        var result = await proforma.CreateDraftAsync(
            RequiredTenantId, RequiredUserId, ClientIp, request, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int? page, [FromQuery] int? pageSize,
        [FromQuery] string? status, [FromQuery] string? search,
        CancellationToken ct)
        => Ok(await proforma.ListAsync(
            RequiredTenantId, page, pageSize, status, search, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await proforma.GetAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("{id:guid}/revisions")]
    public async Task<IActionResult> GetRevisions(Guid id, CancellationToken ct)
    {
        var result = await proforma.GetRevisionChainAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Run one lifecycle transition ("PendingApproval", "Sent",
    /// …). Invalid transitions answer 409 with the valid set.</summary>
    [HttpPost("{id:guid}/transitions")]
    [HasPermission(AppPermissions.ProformaEdit)]
    public async Task<IActionResult> Transition(
        Guid id, [FromBody] TransitionRequest request, CancellationToken ct)
    {
        var result = await proforma.TransitionAsync(
            RequiredTenantId, id, RequiredUserId, ClientIp, request, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Soft-close: the row, number and audit trail are kept.
    /// Idempotent (voiding a voided invoice succeeds without writing).</summary>
    [HttpPost("{id:guid}/void")]
    [HasPermission(AppPermissions.ProformaDelete)]
    public async Task<IActionResult> Void(
        Guid id, [FromBody] VoidRequest? request, CancellationToken ct)
    {
        var result = await proforma.VoidAsync(
            RequiredTenantId, id, RequiredUserId, ClientIp, request?.Reason, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>New Draft sharing the original's root ("PI-…-R1"),
    /// original left byte-identical for audit.</summary>
    [HttpPost("{id:guid}/revisions")]
    [HasPermission(AppPermissions.ProformaCreate)]
    public async Task<IActionResult> CreateRevision(Guid id, CancellationToken ct)
    {
        var result = await proforma.CreateRevisionAsync(
            RequiredTenantId, id, RequiredUserId, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Replace the invoice's whole Step 5 grid (one unit of
    /// work). Only Draft/Rejected bodies are editable.</summary>
    [HttpPut("{id:guid}/spec-groups")]
    [HasPermission(AppPermissions.ProformaEdit)]
    public async Task<IActionResult> SaveSpecGroups(
        Guid id, [FromBody] SaveSpecGroupsRequest request, CancellationToken ct)
    {
        var result = await proforma.SaveSpecGroupsAsync(
            RequiredTenantId, id, RequiredUserId, ClientIp, request, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("{id:guid}/spec-groups")]
    public async Task<IActionResult> GetSpecGroups(Guid id, CancellationToken ct)
    {
        var result = await proforma.GetSpecGroupsAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }
}
