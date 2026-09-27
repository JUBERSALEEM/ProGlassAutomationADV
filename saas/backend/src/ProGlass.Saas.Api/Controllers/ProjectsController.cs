using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.MasterData;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// /api/v1/projects — project profiles (identifier, contract no,
/// descriptive reference, site delivery, LPO). Save requires
/// ProformaInvoice.Edit, delete Settings.Manage (WPF guards, verbatim).
/// ?customerId= scopes the list to one client (the WPF
/// GetProjectsByCustomerAsync card behavior).
/// </summary>
[Route("api/v1/projects")]
public sealed class ProjectsController(MasterDataService masterData)
    : TenantControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? customerId, [FromQuery] string? search,
        [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken ct)
        => Ok(await masterData.ListProjectsAsync(
            RequiredTenantId, customerId, search, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await masterData.GetProjectAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.ProformaEdit)]
    public async Task<IActionResult> Create(
        [FromBody] ProjectRequest request, CancellationToken ct)
    {
        var result = await masterData.SaveProjectAsync(
            RequiredTenantId, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(AppPermissions.ProformaEdit)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] ProjectRequest request, CancellationToken ct)
    {
        var result = await masterData.UpdateProjectAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await masterData.DeleteProjectAsync(
            RequiredTenantId, id, RequiredUserId, ClientIp, ct);
        return result.Success ? NoContent() : Fail(result);
    }
}
