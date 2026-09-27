using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// /api/v1/inquiries — the inquiry module's API surface. Phase 7b ships
/// the CONVERT flow (the quotation hand-off the mapping pinned to
/// `InquiryBoqConversion`): it creates a linked proforma draft and
/// imports the inquiry's BOQ into the Step 5 grid. The inquiry CRUD /
/// list / attachment endpoints (SAAS_API_DESIGN §inquiries) are their
/// own later phase; the convert endpoint is served first because the
/// proforma wizard depends on it.
///
/// WPF permission map: converting IS creating a proforma — the WPF's
/// convert path runs the PI-creation flow (StartNewInvoiceAsync →
/// InsertAsync, ProformaInvoice.Create). The inquiry-side guard shows
/// "Convert to PI" only for Prepared/Confirmed inquiries with no
/// existing PI; those gates live in the service (409s, typed).
/// </summary>
[Route("api/v1/inquiries")]
public sealed class InquiriesController(ProformaService proforma)
    : TenantControllerBase
{
    /// <summary>
    /// Convert the inquiry to a proforma invoice: a fresh linked Draft
    /// (client + project context copied), the BOQ's importable lines
    /// become the Step 5 grid (spec name + dimensions + quantity; a
    /// quoted rate is the customer's own), the inquiry is linked to the
    /// new PI and advances to Sent when its workflow allows it. A
    /// partial BOQ line (no spec name / dimensions / qty) stays on the
    /// inquiry as a note — reported in the response.
    /// </summary>
    [HttpPost("{id:guid}/convert-to-proforma")]
    [HasPermission(AppPermissions.ProformaCreate)]
    public async Task<IActionResult> ConvertToProforma(Guid id, CancellationToken ct)
    {
        var result = await proforma.ConvertInquiryToProformaAsync(
            RequiredTenantId, id, RequiredUserId, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }
}
