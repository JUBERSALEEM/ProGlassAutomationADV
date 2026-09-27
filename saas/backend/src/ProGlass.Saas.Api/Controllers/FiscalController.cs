using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.Fiscal;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// /api/v1/fiscal — Phase 12 core: Tax Invoices (a FISCAL document:
/// once Issued it is never edited — only voided or credited, the WPF
/// fiscal lock), Credit Notes (corrections that reduce the ledger when
/// Applied), the UAE VAT report and customer statements.
/// </summary>
[Route("api/v1/fiscal")]
public sealed class FiscalController(FiscalService fiscal) : TenantControllerBase
{
    // ── Tax Invoices ────────────────────────────────────────────────

    /// <summary>Create a Draft tax invoice — from a proforma (family
    /// numbering: the same PI's later invoices become -A/-B revisions)
    /// or standalone. Totals always from InvoiceTotalsCalculator.</summary>
    [HttpPost("tax-invoices")]
    [HasPermission(AppPermissions.TaxInvoiceEdit)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTaxInvoiceRequest request, CancellationToken ct)
    {
        var result = await fiscal.CreateAsync(
            RequiredTenantId, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("tax-invoices")]
    [HasPermission(AppPermissions.TaxInvoiceView)]
    public async Task<IActionResult> Search(
        [FromQuery] string? search, [FromQuery] string? status,
        [FromQuery] bool includeVoid = false,
        [FromQuery] int? page = 1, [FromQuery] int? pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await fiscal.SearchAsync(
            RequiredTenantId, search, status, includeVoid, page, pageSize, ct);
        return Ok(result.Value);
    }

    [HttpGet("tax-invoices/{id:guid}")]
    [HasPermission(AppPermissions.TaxInvoiceView)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await fiscal.GetAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Machine-guarded status change (the WPF
    /// StatusTransitions tables). Voiding sets is_void + reason — the
    /// only way an Issued invoice ever leaves its Issued state.</summary>
    [HttpPost("tax-invoices/{id:guid}/status")]
    [HasPermission(AppPermissions.TaxInvoiceEdit)]
    public async Task<IActionResult> Transition(
        Guid id, [FromBody] TaxInvoiceTransitionRequest request,
        CancellationToken ct)
    {
        var result = await fiscal.TransitionAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Record a payment — Paid at zero balance, PartiallyPaid
    /// before that.</summary>
    [HttpPost("tax-invoices/{id:guid}/payments")]
    [HasPermission(AppPermissions.TaxInvoiceEdit)]
    public async Task<IActionResult> RecordPayment(
        Guid id, [FromBody] RecordPaymentRequest request, CancellationToken ct)
    {
        var result = await fiscal.RecordPaymentAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    // ── Credit Notes ────────────────────────────────────────────────

    /// <summary>A credit note never exists on its own — it always
    /// corrects a tax invoice (TaxInvoiceReference required in the
    /// WPF).</summary>
    [HttpPost("credit-notes")]
    [HasPermission(AppPermissions.TaxInvoiceEdit)]
    public async Task<IActionResult> CreateCreditNote(
        [FromBody] CreateCreditNoteRequest request, CancellationToken ct)
    {
        var result = await fiscal.CreateCreditNoteAsync(
            RequiredTenantId, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("credit-notes")]
    [HasPermission(AppPermissions.TaxInvoiceView)]
    public async Task<IActionResult> ListCreditNotes(
        [FromQuery] Guid? taxInvoiceId, [FromQuery] int? page = 1,
        [FromQuery] int? pageSize = 50, CancellationToken ct = default)
    {
        var result = await fiscal.ListCreditNotesAsync(
            RequiredTenantId, taxInvoiceId, page, pageSize, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    /// <summary>Draft→Issued→Applied (settles against the invoice's
    /// balance) or Cancel; Applied is terminal.</summary>
    [HttpPost("credit-notes/{id:guid}/status")]
    [HasPermission(AppPermissions.TaxInvoiceEdit)]
    public async Task<IActionResult> TransitionCreditNote(
        Guid id, [FromBody] TaxInvoiceTransitionRequest request,
        CancellationToken ct)
    {
        var result = await fiscal.TransitionCreditNoteAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    // ── Reports ─────────────────────────────────────────────────────

    /// <summary>The UAE VAT return for a period: standard-rated supplies
    /// and output VAT over vatable invoices, minus credit-note
    /// adjustments.</summary>
    [HttpGet("vat-report")]
    [HasPermission(AppPermissions.ReportsView)]
    public async Task<IActionResult> VatReport(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? new DateOnly(end.Year, 1, 1);
        var report = await fiscal.GetVatReportAsync(
            RequiredTenantId, start, end, ct);
        return Ok(report);
    }

    /// <summary>Generate a customer statement — the ledger of invoices
    /// (debits) and payments/applied credit notes (credits) with a
    /// running balance; persisted to statements + statement_lines.</summary>
    [HttpPost("statements")]
    [HasPermission(AppPermissions.StatementGenerate)]
    public async Task<IActionResult> GenerateStatement(
        [FromBody] GenerateStatementRequest request, CancellationToken ct)
    {
        var result = await fiscal.GenerateStatementAsync(
            RequiredTenantId, RequiredUserId, request.CustomerName,
            request.From, request.To, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("statements/{id:guid}")]
    [HasPermission(AppPermissions.ReportsView)]
    public async Task<IActionResult> GetStatement(Guid id, CancellationToken ct)
    {
        var result = await fiscal.GetStatementAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }
}
