using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.MasterData;
using ProGlass.Saas.Application.Pricing;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Application.Specifications;
using ProGlass.Saas.Application.JobOrders;
using ProGlass.Saas.Application.Stock;
using ProGlass.Saas.Application.Fiscal;

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

    protected IActionResult Fail<T>(JobOrderResult<T> result) => result.ErrorCode switch
    {
        JobOrderErrorCode.NotFound =>
            NotFound(new { code = "joborders.not_found", message = result.Message }),
        JobOrderErrorCode.Conflict =>
            Conflict(new { code = "joborders.conflict", message = result.Message }),
        JobOrderErrorCode.NumberExhausted =>
            Conflict(new { code = "joborders.number_exhausted", message = result.Message }),
        JobOrderErrorCode.ConcurrencyConflict =>
            Conflict(new { code = "joborders.concurrency_conflict", message = result.Message }),
        _ => BadRequest(new { code = "joborders.validation_failed", message = result.Message })
    };

    protected IActionResult Fail<T>(ProductionResult<T> result) => result.ErrorCode switch
    {
        ProductionErrorCode.NotFound =>
            NotFound(new { code = "production.not_found", message = result.Message }),
        ProductionErrorCode.Conflict =>
            Conflict(new { code = "production.conflict", message = result.Message }),
        ProductionErrorCode.ConcurrencyConflict =>
            Conflict(new { code = "production.concurrency_conflict", message = result.Message }),
        _ => BadRequest(new { code = "production.validation_failed", message = result.Message })
    };

    protected IActionResult Fail<T>(OptimizationResult<T> result) => result.ErrorCode switch
    {
        OptimizationErrorCode.NotFound =>
            NotFound(new { code = "optimization.not_found", message = result.Message }),
        OptimizationErrorCode.Conflict =>
            Conflict(new { code = "optimization.conflict", message = result.Message }),
        _ => BadRequest(new { code = "optimization.validation_failed", message = result.Message })
    };

    protected IActionResult Fail<T>(FiscalResult<T> result) => result.ErrorCode switch
    {
        FiscalErrorCode.NotFound =>
            NotFound(new { code = "fiscal.not_found", message = result.Message }),
        FiscalErrorCode.Conflict or FiscalErrorCode.ConcurrencyConflict =>
            Conflict(new { code = "fiscal.conflict", message = result.Message }),
        FiscalErrorCode.NumberExhausted =>
            Conflict(new { code = "fiscal.number_exhausted", message = result.Message }),
        _ => BadRequest(new { code = "fiscal.validation_failed", message = result.Message })
    };

    protected IActionResult Fail<T>(DeliveryResult<T> result) => result.ErrorCode switch
    {
        DeliveryErrorCode.NotFound =>
            NotFound(new { code = "delivery.not_found", message = result.Message }),
        DeliveryErrorCode.Conflict =>
            Conflict(new { code = "delivery.conflict", message = result.Message }),
        DeliveryErrorCode.ConcurrencyConflict =>
            Conflict(new { code = "delivery.concurrency_conflict", message = result.Message }),
        DeliveryErrorCode.FullyDelivered =>
            Conflict(new { code = "delivery.fully_delivered", message = result.Message }),
        _ => BadRequest(new { code = "delivery.validation_failed", message = result.Message })
    };

    protected IActionResult Fail<T>(StockResult<T> result) => result.ErrorCode switch
    {
        StockErrorCode.NotFound =>
            NotFound(new { code = "stock.not_found", message = result.Message }),
        StockErrorCode.InsufficientStock =>
            Conflict(new { code = "stock.insufficient_stock", message = result.Message }),
        StockErrorCode.DuplicateSheet =>
            Conflict(new { code = "stock.duplicate_sheet", message = result.Message }),
        _ => BadRequest(new { code = "stock.validation_failed", message = result.Message })
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
