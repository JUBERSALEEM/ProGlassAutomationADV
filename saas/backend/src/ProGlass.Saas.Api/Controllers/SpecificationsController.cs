using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.Specifications;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// /api/v1/specifications — the Step 4 pricing library and its
/// ledger-derived usage trail (the WPF Specifications Center).
///
/// WPF permission map:
///   • APPLY/save → ProformaEdit (a saved spec sets the rate Step 5 and
///     the P&amp;L price against — a pricing decision, not a preference)
///   • delete → ProformaDelete (WPF DeleteAsync demands exactly this)
///   • reads + reconcile → any authenticated user (the WPF Center opens
///     with ProformaView, which every role holds; the reconcile also ran
///     automatically at startup and after every save)
/// </summary>
[Route("api/v1/specifications")]
public sealed class SpecificationsController(SpecificationsService specifications)
    : TenantControllerBase
{
    /// <summary>Step 4 APPLY: upsert the library by (glassType, specName).
    /// Rate always refreshes; catalogue details only when supplied.</summary>
    [HttpPost]
    [HasPermission(AppPermissions.ProformaEdit)]
    public async Task<IActionResult> Apply(
        [FromBody] ApplySpecificationRequest request, CancellationToken ct)
    {
        var result = await specifications.ApplyAsync(
            RequiredTenantId, RequiredUserId, ClientIp, request, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>The library, smart-sorted (most-used first, then most
    /// recent). Optional ?glassType= filter for the Step 5 dropdown.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? glassType, CancellationToken ct)
        => Ok(await specifications.ListAsync(RequiredTenantId, glassType, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await specifications.GetAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Verified uses of one spec, newest first.</summary>
    [HttpGet("{id:guid}/usage-history")]
    public async Task<IActionResult> UsageHistory(Guid id, CancellationToken ct)
    {
        var result = await specifications.GetUsageHistoryAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Recent verified uses across all specs (the Center's
    /// last-activity card). Default limit 200 (WPF).</summary>
    [HttpGet("usage/recent")]
    public async Task<IActionResult> RecentUsage(
        [FromQuery] int? limit, CancellationToken ct)
        => Ok(await specifications.GetRecentUsageAsync(
            RequiredTenantId, limit, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(AppPermissions.ProformaDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await specifications.DeleteAsync(
            RequiredTenantId, id, RequiredUserId, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Re-derive counts, history and provenance from saved
    /// invoices' Step 5 groups (the WPF ledger's Refresh; it also runs
    /// automatically after every grid save).</summary>
    [HttpPost("reconcile")]
    public async Task<IActionResult> Reconcile(CancellationToken ct)
        => Ok(await specifications.ReconcileAsync(
            RequiredTenantId, RequiredUserId, ClientIp, ct));
}
