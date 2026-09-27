using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.MasterData;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// Shared plumbing for tenant-scoped module controllers: the tenant and
/// user ids come from the authenticated PRINCIPAL (never the request
/// body/query — master prompt §24), and failures use the shared
/// { code, message } error envelope (API design §0).
/// </summary>
[ApiController]
[Authorize]
public abstract class TenantControllerBase : ControllerBase
{
    protected Guid RequiredTenantId =>
        Guid.TryParse(User.FindFirstValue("tid"), out var tid)
            ? tid
            : throw new InvalidOperationException(
                "Authenticated principal is missing the tid claim.");

    protected Guid RequiredUserId =>
        Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"),
            out var sub)
            ? sub
            : throw new InvalidOperationException(
                "Authenticated principal is missing the sub claim.");

    protected string? ClientIp =>
        HttpContext.Connection.RemoteIpAddress?.ToString();

    protected IActionResult Fail<T>(MasterDataResult<T> result) => result.ErrorCode switch
    {
        MasterDataErrorCode.NotFound =>
            NotFound(new { code = "masterdata.not_found", message = result.Message }),
        MasterDataErrorCode.RenameConflict =>
            Conflict(new { code = "masterdata.rename_conflict", message = result.Message }),
        _ => BadRequest(new { code = "masterdata.validation_failed", message = result.Message })
    };
}
