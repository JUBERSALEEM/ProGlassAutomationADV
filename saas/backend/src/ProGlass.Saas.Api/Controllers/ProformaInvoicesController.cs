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

    /// <summary>
    /// Replace the invoice's whole Step 6 section — predefined charges,
    /// wastages and custom charges, saved as one unit like the grid.
    /// Metric quantities and amounts are recomputed server-side from the
    /// saved Step 5 grid (never trusted from the client); the wastage
    /// optimization rate is derived, not supplied.
    /// </summary>
    [HttpPut("{id:guid}/step6")]
    [HasPermission(AppPermissions.ProformaEdit)]
    public async Task<IActionResult> SaveStep6(
        Guid id, [FromBody] SaveStep6Request request, CancellationToken ct)
    {
        var result = await proforma.SaveStep6Async(
            RequiredTenantId, id, RequiredUserId, ClientIp, request, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>
    /// The saved Step 6 section + the derived read model: per-spec
    /// metrics strip (LM1/LM2/SQM1/SQM2/Qty/pane factor) and the
    /// available wastage pane sources ("{spec}-{SideLabel}" keys).
    /// </summary>
    [HttpGet("{id:guid}/step6")]
    public async Task<IActionResult> GetStep6(Guid id, CancellationToken ct)
    {
        var result = await proforma.GetStep6Async(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>
    /// Step 6 Advanced automation suggestions for the saved grid (WPF
    /// OtherChargeAutomationService): per spec group, the predefined
    /// charges the spec implies and the pane sides to preselect into
    /// the wastage grid. Read-only.
    /// </summary>
    [HttpGet("{id:guid}/step6/suggestions")]
    public async Task<IActionResult> GetStep6Suggestions(Guid id, CancellationToken ct)
    {
        var result = await proforma.GetStep6SuggestionsAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>
    /// Duplicate the invoice as a fresh DRAFT (WPF
    /// SavedInvoicesListViewModel.DuplicateAsync): the whole document is
    /// copied — grid, Step 6/Step 9 sections, financials — with the
    /// inquiry link deliberately detached (a duplicate is a fresh
    /// quotation, not another PI for the same inquiry) and a +7-day
    /// validity window.
    /// </summary>
    [HttpPost("{id:guid}/duplicate")]
    [HasPermission(AppPermissions.ProformaCreate)]
    public async Task<IActionResult> Duplicate(Guid id, CancellationToken ct)
    {
        var result = await proforma.DuplicateAsync(
            RequiredTenantId, id, RequiredUserId, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>
    /// Recompute and persist the Step 7 financial summary from the saved
    /// Step 5/Step 6 subtotals plus the operator inputs (discount type +
    /// value, VAT applicability + percent — the WPF mapper's persisted
    /// set). Amounts are derived server-side: discount comes off the net
    /// first (BUG-56) and VAT applies to the discounted value (BUG-58).
    /// </summary>
    [HttpPut("{id:guid}/step7")]
    [HasPermission(AppPermissions.ProformaEdit)]
    public async Task<IActionResult> SaveStep7(
        Guid id, [FromBody] SaveStep7Request request, CancellationToken ct)
    {
        var result = await proforma.SaveStep7Async(
            RequiredTenantId, id, RequiredUserId, ClientIp, request, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>
    /// The round-to-target tool (WPF InvoiceRoundTo): solve the
    /// fixed-amount discount whose grand total equals the target. Can
    /// only discount DOWN — a target above the undiscounted total is
    /// refused. The solved discount is persisted with the recomputed
    /// summary.
    /// </summary>
    [HttpPost("{id:guid}/step7/round-to")]
    [HasPermission(AppPermissions.ProformaEdit)]
    public async Task<IActionResult> RoundToStep7(
        Guid id, [FromBody] RoundToStep7Request request, CancellationToken ct)
    {
        var result = await proforma.RoundToStep7Async(
            RequiredTenantId, id, RequiredUserId, ClientIp, request, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>
    /// The saved Step 7 summary: operator inputs and the recomputed
    /// waterfall (net, discount, before-VAT, VAT, grand total).
    /// </summary>
    [HttpGet("{id:guid}/step7")]
    public async Task<IActionResult> GetStep7(Guid id, CancellationToken ct)
    {
        var result = await proforma.GetStep7Async(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>
    /// Recalculate and persist the Step 9 Profit &amp; Loss from the saved
    /// Steps 4–6 state, the tenant Sheet Store and the two operator inputs
    /// (wastage %, cost basis). Every row, quantity and amount is computed
    /// server-side (the port of the WPF RecalculateAsync) — a posted profit
    /// figure is never trusted.
    /// </summary>
    [HttpPut("{id:guid}/step9")]
    [HasPermission(AppPermissions.ProformaEdit)]
    public async Task<IActionResult> SaveStep9(
        Guid id, [FromBody] SaveStep9Request request, CancellationToken ct)
    {
        var result = await proforma.SaveStep9Async(
            RequiredTenantId, id, RequiredUserId, ClientIp, request, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>
    /// The saved Step 9 section: operator inputs, the KPI snapshot
    /// (gross/net, glass + production cost, profit, the three margins,
    /// U-insert and basis cost) and the calculated rows in build order.
    /// </summary>
    [HttpGet("{id:guid}/step9")]
    public async Task<IActionResult> GetStep9(Guid id, CancellationToken ct)
    {
        var result = await proforma.GetStep9Async(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }
}
