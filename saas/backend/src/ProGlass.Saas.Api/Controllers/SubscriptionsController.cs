using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.Subscriptions;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// /api/v1/subscriptions — Phase 13: the tenant's plan, features and
/// usage (the WPF About/licence page), plan changes and cancellation.
/// Reads are open to every authenticated user of the tenant; writes
/// gate Settings.Manage (the WPF licence screen is admin territory).
/// The platform side (suspend/reactivate/grant/overrides) is NOT
/// HTTP-exposed — it lives behind ISubscriptionAdminStore for the
/// future payment provider (mapping §1.11).
/// </summary>
[Route("api/v1/subscriptions")]
public sealed class SubscriptionsController(SubscriptionService subscriptions)
    : TenantControllerBase
{
    /// <summary>The current subscription: plan, resolved access state
    /// (the WPF LicenseStatus shape: Active/Trialing/ExpiringSoon/
    /// GracePeriod/TrialExpired/…), effective features and seat
    /// usage.</summary>
    [HttpGet("current")]
    public async Task<IActionResult> Current(CancellationToken ct)
    {
        var dto = await subscriptions.GetCurrentAsync(RequiredTenantId, ct);
        return Ok(dto);
    }

    /// <summary>The public plan catalog (the WPF edition tiers).</summary>
    [HttpGet("plans")]
    public async Task<IActionResult> Plans(CancellationToken ct)
    {
        var plans = await subscriptions.ListPlansAsync(ct);
        return Ok(plans.Select(p => new
        {
            p.Code, p.Name, p.Price, p.BillingPeriod
        }));
    }

    /// <summary>Switch the tenant's plan (period resets; a downgrade
    /// below the current seat count is refused).</summary>
    [HttpPost("current/change-plan")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> ChangePlan(
        [FromBody] ChangePlanRequest request, CancellationToken ct)
    {
        var result = await subscriptions.ChangePlanAsync(
            RequiredTenantId, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Cancel the subscription — the WPF soft-close pattern:
    /// reason recorded ("(no reason given)" default), reads stay
    /// available, recoverable by choosing a plan again.</summary>
    [HttpPost("current/cancel")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> Cancel(
        [FromBody] CancelRequest request, CancellationToken ct)
    {
        var result = await subscriptions.CancelAsync(
            RequiredTenantId, RequiredUserId, request?.Reason, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    public sealed record CancelRequest(string? Reason);
}
