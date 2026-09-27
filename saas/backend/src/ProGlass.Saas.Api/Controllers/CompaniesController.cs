using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.MasterData;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// /api/v1/companies — the WPF Company Center + Step 1 auto-fill cards.
/// Save/delete company requires Settings.Manage; reads are available to
/// every authenticated user (the WPF repository guards only writes, with
/// the same permission).
/// </summary>
[Route("api/v1/companies")]
public sealed class CompaniesController(MasterDataService masterData)
    : TenantControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken ct)
        => Ok(await masterData.ListCompaniesAsync(
            RequiredTenantId, search, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await masterData.GetCompanyAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> Create(
        [FromBody] CompanyRequest request, CancellationToken ct)
    {
        var result = await masterData.SaveCompanyAsync(
            RequiredTenantId, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] CompanyRequest request, CancellationToken ct)
    {
        var result = await masterData.UpdateCompanyAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await masterData.DeleteCompanyAsync(
            RequiredTenantId, id, RequiredUserId, ClientIp, ct);
        return result.Success ? NoContent() : Fail(result);
    }
}
