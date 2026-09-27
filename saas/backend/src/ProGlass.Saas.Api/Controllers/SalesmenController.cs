using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.MasterData;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// /api/v1/salesmen — salesman lookup for the Step 2 dropdown and sales
/// analytics. Save and delete both require Settings.Manage (WPF guards,
/// verbatim — a salesman is managed data, not invoice data).
/// </summary>
[Route("api/v1/salesmen")]
public sealed class SalesmenController(MasterDataService masterData)
    : TenantControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken ct)
        => Ok(await masterData.ListSalesmenAsync(
            RequiredTenantId, search, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await masterData.GetSalesmanAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> Create(
        [FromBody] SalesmanRequest request, CancellationToken ct)
    {
        var result = await masterData.SaveSalesmanAsync(
            RequiredTenantId, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] SalesmanRequest request, CancellationToken ct)
    {
        var result = await masterData.UpdateSalesmanAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await masterData.DeleteSalesmanAsync(
            RequiredTenantId, id, RequiredUserId, ClientIp, ct);
        return result.Success ? NoContent() : Fail(result);
    }
}
