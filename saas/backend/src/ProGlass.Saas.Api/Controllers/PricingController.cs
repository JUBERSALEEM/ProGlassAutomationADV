using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.Pricing;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// /api/v1/pricing — the deferred Step 4 settings + rate preview
/// endpoints (master prompt Phase 6 / API design):
///
///   GET  /config   — the tenant's pricing/VAT settings and catalogs
///                    (authenticated read; a fresh tenant gets the WPF
///                    defaults, nothing is written).
///   PUT  /config   — save the settings (Settings.Manage — the WPF
///                    Company-Center-style guard: writes are
///                    admin-only, reads are for everyone).
///   POST /preview  — run the WPF rate formulas on panel values and get
///                    the rate, the cost basis and the SAME warnings the
///                    WPF surfaces (yield fallback, unpriced interlayer).
///                    Pure calculation — no permission beyond
///                    authentication (the WPF Step 4 panel is available
///                    to anyone who can open the estimator).
/// </summary>
[Route("api/v1/pricing")]
public sealed class PricingController(PricingService pricing) : TenantControllerBase
{
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct)
        => Ok(await pricing.GetConfigAsync(RequiredTenantId, ct));

    [HttpPut("config")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> SaveConfig(
        [FromBody] PricingConfigRequest request, CancellationToken ct)
    {
        var result = await pricing.SaveConfigAsync(
            RequiredTenantId, RequiredUserId, ClientIp, request, ct);
        return result.Success
            ? Ok(result.Value)
            : Fail(result);
    }

    [HttpPost("preview")]
    public IActionResult Preview([FromBody] PricingPreviewRequest request)
    {
        var result = pricing.Preview(request);
        return result.Success
            ? Ok(result.Value)
            : Fail(result);
    }
}
