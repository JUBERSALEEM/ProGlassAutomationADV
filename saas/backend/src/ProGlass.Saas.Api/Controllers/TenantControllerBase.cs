using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.MasterData;
using ProGlass.Saas.Application.Pricing;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Application.Specifications;

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

    protected IActionResult Fail<T>(PricingResult<T> result) => result.ErrorCode switch
    {
        _ => BadRequest(new { code = "pricing.validation_failed", message = result.Message })
    };

    protected IActionResult Fail<T>(SpecificationResult<T> result) => result.ErrorCode switch
    {
        SpecificationErrorCode.NotFound =>
            NotFound(new { code = "specifications.not_found", message = result.Message }),
        _ => BadRequest(new { code = "specifications.validation_failed", message = result.Message })
    };

    protected IActionResult Fail<T>(ProformaResult<T> result) => result.ErrorCode switch
    {
        ProformaErrorCode.NotFound =>
            NotFound(new { code = "proforma.not_found", message = result.Message }),
        ProformaErrorCode.Conflict =>
            Conflict(new { code = "proforma.conflict", message = result.Message }),
        ProformaErrorCode.NumberExhausted =>
            Conflict(new { code = "proforma.number_exhausted", message = result.Message }),
        _ => BadRequest(new { code = "proforma.validation_failed", message = result.Message })
    };
}
