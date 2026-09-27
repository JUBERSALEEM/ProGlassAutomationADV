using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.MasterData;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// /api/v1/clients — customer profiles for proforma/job-order/delivery
/// references and the Step 2 auto-fill card. Save requires
/// ProformaInvoice.Edit (the WPF guard for saving a client); delete
/// requires Settings.Manage — both verbatim from
/// Services/MasterData/Repository/MasterDataRepository.cs.
/// </summary>
[Route("api/v1/clients")]
public sealed class ClientsController(MasterDataService masterData)
    : TenantControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken ct)
        => Ok(await masterData.ListClientsAsync(
            RequiredTenantId, search, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await masterData.GetClientAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.ProformaEdit)]
    public async Task<IActionResult> Create(
        [FromBody] ClientRequest request, CancellationToken ct)
    {
        var result = await masterData.SaveClientAsync(
            RequiredTenantId, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(AppPermissions.ProformaEdit)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] ClientRequest request, CancellationToken ct)
    {
        var result = await masterData.UpdateClientAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await masterData.DeleteClientAsync(
            RequiredTenantId, id, RequiredUserId, ClientIp, ct);
        return result.Success ? NoContent() : Fail(result);
    }
}
