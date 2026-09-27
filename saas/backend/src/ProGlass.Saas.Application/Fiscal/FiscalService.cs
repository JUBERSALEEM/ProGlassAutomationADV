using System.Text.Json;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Fiscal;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 12 — Fiscal core (WPF Services/TaxInvoice + CreditNote +
//  Reporting.Vat + Statement): the Tax Invoice lifecycle (a FISCAL
//  document — once Issued it is never edited, only voided or credited),
//  credit notes (fiscal corrections, Applied is terminal), the UAE VAT
//  report and customer statements. PDF/print and the full reporting
//  analytics suite are later slices (documented).
// ═══════════════════════════════════════════════════════════════════════

public enum FiscalErrorCode
{
    None = 0,
    ValidationFailed,
    NotFound,
    Conflict,
    ConcurrencyConflict,
    NumberExhausted
}

public sealed record FiscalResult<T>(
    bool Success, T? Value, FiscalErrorCode ErrorCode, string Message)
{
    public static FiscalResult<T> Ok(T value) =>
        new(true, value, FiscalErrorCode.None, "");

    public static FiscalResult<T> Fail(FiscalErrorCode code, string message) =>
        new(false, default, code, message);
}

// ── Requests / DTOs ─────────────────────────────────────────────────

public sealed record CreateTaxInvoiceRequest(
    Guid? ProformaId = null,
    Guid? JobOrderId = null,
    string? DeliveryNoteReference = null,
    string? PurchaseOrderNumber = null,
    string CustomerName = "",
    string ProjectName = "",
    decimal Subtotal,
    decimal DiscountPercent = 0,
    string PaymentTerms = "Net 30",
    DateOnly? IssueDate = null);

public sealed record TaxInvoiceTransitionRequest(string ToStatus, string? Reason = null);
public sealed record RecordPaymentRequest(decimal Amount, string? Notes = null);
public sealed record VoidFiscalRequest(string? Reason = null);

public sealed record CreateCreditNoteRequest(
    Guid TaxInvoiceId,
    decimal Subtotal,
    decimal DiscountPercent = 0,
    string Reason = "");

public sealed record GenerateStatementRequest(
    string CustomerName, DateOnly? From = null, DateOnly? To = null);

public sealed record TaxInvoiceDto(
    Guid Id,
    string InvoiceNo,
    Guid? ProformaId,
    Guid? JobOrderId,
    string DeliveryNoteReference,
    string CustomerName,
    string ProjectName,
    string Status,
    string PaymentTerms,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    decimal Subtotal,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal VatAmount,
    decimal GrandTotal,
    decimal PaidAmount,
    decimal BalanceAmount,
    bool IsVoid,
    string VoidReason,
    int RowVersion);

public sealed record CreditNoteDto(
    Guid Id,
    string CreditNoteNo,
    Guid TaxInvoiceId,
    string TaxInvoiceNo,
    string Reason,
    string Status,
    decimal Subtotal,
    decimal VatAmount,
    decimal GrandTotal,
    DateTime? IssuedAt);

/// <summary>One statement line (the customer ledger).</summary>
public sealed record StatementLineDto(
    string LineType,
    string DocumentNo,
    DateOnly? DocumentDate,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance);

public sealed record StatementDto(
    Guid Id,
    string StatementNo,
    string Title,
    string Customer,
    DateOnly? From,
    DateOnly? To,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Balance,
    IReadOnlyList<StatementLineDto> Lines);

/// <summary>The UAE VAT return for a period (WPF VatSubmissionView core:
/// standard-rated output VAT minus credit-note adjustments).</summary>
public sealed record VatReportDto(
    DateOnly From,
    DateOnly To,
    int InvoiceCount,
    decimal StandardRatedSupplies,
    decimal OutputVat,
    int CreditNoteCount,
    decimal CreditNoteSupplies,
    decimal CreditNoteVat,
    decimal NetVatPayable);

// ── Port ────────────────────────────────────────────────────────────

public interface IFiscalStore
{
    Task<ProformaInvoice?> FindProformaAsync(
        Guid tenantId, Guid id, CancellationToken ct);

    Task<JobOrder?> FindJobAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<TaxInvoice?> FindInvoiceAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<IReadOnlyList<TaxInvoice>> ListInvoicesForProformaAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct);

    Task<IReadOnlyList<TaxInvoice>> SearchInvoicesAsync(
        Guid tenantId, string? search, string? status, bool includeVoid,
        int page, int pageSize, CancellationToken ct);

    /// <summary>The tenant's max invoice sequence for a year + every
    /// number ever used (for allocation).</summary>
    Task<(int MaxSeq, HashSet<string> UsedNumbers)> GetInvoiceNumbersAsync(
        Guid tenantId, int year, CancellationToken ct);

    Task<bool> TryInsertInvoiceAsync(TaxInvoice invoice, CancellationToken ct);

    Task<bool> TryUpdateInvoiceAsync(
        TaxInvoice invoice, int? expectedRowVersion, CancellationToken ct);

    Task<CreditNote?> FindCreditNoteAsync(
        Guid tenantId, Guid id, CancellationToken ct);

    Task<IReadOnlyList<CreditNote>> ListCreditNotesAsync(
        Guid tenantId, Guid? taxInvoiceId, int page, int pageSize,
        CancellationToken ct);

    Task<(int MaxSeq, HashSet<string> UsedNumbers)> GetCreditNoteNumbersAsync(
        Guid tenantId, int year, CancellationToken ct);

    Task<bool> TryInsertCreditNoteAsync(CreditNote note, CancellationToken ct);

    Task<bool> TryUpdateCreditNoteAsync(CreditNote note, CancellationToken ct);

    /// <summary>Invoices in the period that count toward output VAT
    /// (Issued/PartiallyPaid/Paid/Overdue — never Draft/Cancelled/Void),
    /// with customer name for the statement filter.</summary>
    Task<IReadOnlyList<TaxInvoice>> ListVatableInvoicesAsync(
        Guid tenantId, DateOnly from, DateOnly to, CancellationToken ct);

    Task<IReadOnlyList<CreditNote>> ListVatableCreditNotesAsync(
        Guid tenantId, DateOnly from, DateOnly to, CancellationToken ct);

    Task<IReadOnlyList<TaxInvoice>> ListCustomerInvoicesAsync(
        Guid tenantId, string customerName, DateOnly? from, DateOnly? to,
        CancellationToken ct);

    /// <summary>Persist statement + lines in one transaction.</summary>
    Task<Statement> PersistStatementAsync(
        Guid tenantId, Guid actorId, Statement statement,
        IReadOnlyList<StatementLine> lines, CancellationToken ct);

    Task<Statement?> FindStatementAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<IReadOnlyList<StatementLine>> ListStatementLinesAsync(
        Guid tenantId, Guid statementId, CancellationToken ct);
}

// ── Service ─────────────────────────────────────────────────────────

/// <summary>
/// The fiscal module: Tax Invoices, Credit Notes, the VAT report and
/// customer statements.
/// </summary>
public sealed class FiscalService(IFiscalStore store, IAuditWriter audit)
{
    private const int AllocationAttempts = 5;
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    // ── Tax Invoices ────────────────────────────────────────────────

    /// <summary>
    /// POST /api/v1/tax-invoices — a Draft with the family number: the
    /// FIRST invoice for a PI takes INV-{year}-{seq:D3}; every later
    /// invoice for the same PI is a revision with a sequence letter
    /// (INV-2026-001-A). Totals always come from
    /// InvoiceTotalsCalculator (the single source of truth).
    /// </summary>
    public async Task<FiscalResult<TaxInvoiceDto>> CreateAsync(
        Guid tenantId, Guid actorId, CreateTaxInvoiceRequest request,
        string? ip, CancellationToken ct)
    {
        if (request.Subtotal < 0)
        {
            return FiscalResult<TaxInvoiceDto>.Fail(
                FiscalErrorCode.ValidationFailed,
                "Subtotal cannot be negative.");
        }

        if (request.DiscountPercent is < 0 or > 100)
        {
            return FiscalResult<TaxInvoiceDto>.Fail(
                FiscalErrorCode.ValidationFailed,
                "Discount percent must be between 0 and 100.");
        }

        ProformaInvoice? pi = null;
        if (request.ProformaId.HasValue)
        {
            pi = await store.FindProformaAsync(
                tenantId, request.ProformaId.Value, ct);
            if (pi is null)
            {
                return FiscalResult<TaxInvoiceDto>.Fail(FiscalErrorCode.NotFound,
                    $"Proforma invoice {request.ProformaId} was not found.");
            }
        }

        JobOrder? job = null;
        if (request.JobOrderId.HasValue)
        {
            job = await store.FindJobAsync(tenantId, request.JobOrderId.Value, ct);
            if (job is null)
            {
                return FiscalResult<TaxInvoiceDto>.Fail(FiscalErrorCode.NotFound,
                    $"Job order {request.JobOrderId} was not found.");
            }
        }

        var year = (request.IssueDate ?? DateOnly.FromDateTime(DateTime.UtcNow)).Year;
        var family = pi is null
            ? Array.Empty<TaxInvoice>()
            : await store.ListInvoicesForProformaAsync(tenantId, pi.Id, ct);

        var (maxSeq, used) = await store.GetInvoiceNumbersAsync(tenantId, year, ct);

        // Family numbering: the PI's existing invoices share one base
        // number; the next one for the same PI is a lettered revision
        // (first revision "-A", then "-B"…). Letter index derives from
        // every number already used tenant-wide (race-safe).
        string invoiceNo;
        if (family.Count > 0)
        {
            var baseNo = family
                .Select(i => i.InvoiceNo.Split('-').Length > 3
                    ? i.InvoiceNo[..i.InvoiceNo.LastIndexOf('-')]
                    : i.InvoiceNo)
                .OrderBy(b => b, StringComparer.Ordinal)
                .First();
            invoiceNo = TaxInvoiceNumbers.Revision(baseNo, NextLetter(baseNo, used));
        }
        else
        {
            invoiceNo = Allocate(ref maxSeq, used, year);
        }

        var totals = InvoiceTotalsCalculator.Compute(
            request.Subtotal, request.DiscountPercent);
        var issueDate = request.IssueDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dueDate = issueDate.AddDays(
            PaymentTermsResolver.ResolveDays(request.PaymentTerms));

        var now = DateTime.UtcNow;
        var invoice = new TaxInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNo = invoiceNo,
            ProformaId = pi?.Id,
            JobOrderId = job?.Id,
            DeliveryNoteReference = request.DeliveryNoteReference ?? "",
            CustomerName = request.CustomerName,
            ProjectName = request.ProjectName,
            Status = TaxInvoiceStatuses.Draft,
            PaymentTerms = request.PaymentTerms,
            IssueDate = issueDate,
            DueDate = dueDate,
            Subtotal = request.Subtotal,
            DiscountPercent = request.DiscountPercent,
            DiscountAmount = totals.DiscountAmount,
            VatAmount = totals.VatAmount,
            GrandTotal = totals.GrandTotal,
            PaidAmount = 0m,
            BalanceAmount = totals.GrandTotal,
            RowVersion = 0,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId
        };

        var attempts = 0;
        while (!await store.TryInsertInvoiceAsync(invoice, ct))
        {
            if (++attempts >= AllocationAttempts)
            {
                return FiscalResult<TaxInvoiceDto>.Fail(
                    FiscalErrorCode.Conflict,
                    $"Invoice number '{invoice.InvoiceNo}' already exists. " +
                    "Please refresh and try again.");
            }

            // Family revision lost a race → try the next letter; a fresh
            // number takes the next sequence.
            used.Add(invoice.InvoiceNo);
            invoice.InvoiceNo = family.Count > 0
                ? TaxInvoiceNumbers.Revision(
                    invoice.InvoiceNo[..invoice.InvoiceNo.LastIndexOf('-')],
                    NextLetter(
                        invoice.InvoiceNo[..invoice.InvoiceNo.LastIndexOf('-')],
                        used))
                : Allocate(ref maxSeq, used, year);
        }

        await AuditAsync(tenantId, actorId, "taxinvoice.created",
            "tax_invoices", invoice.Id, null, new
            {
                invoiceNo = invoice.InvoiceNo,
                grandTotal = invoice.GrandTotal
            }, ip, ct);
        return FiscalResult<TaxInvoiceDto>.Ok(ToDto(invoice));
    }

    /// <summary>The next free revision letter for a family base:
    /// 1 past the highest letter (or past 0 when only the base exists).
    /// </summary>
    private static int NextLetter(string baseNo, HashSet<string> used)
    {
        var max = 0;
        foreach (var n in used)
        {
            if (n == baseNo)
            {
                continue;
            }

            if (n.Length == baseNo.Length + 2
                && n.StartsWith(baseNo + "-", StringComparison.Ordinal)
                && n[^1] is >= 'A' and <= 'Z')
            {
                max = Math.Max(max, n[^1] - 'A' + 1);
            }
        }

        return max + 1;
    }

    private static string Allocate(ref int maxSeq, HashSet<string> used, int year)
    {
        string candidate;
        do
        {
            maxSeq = TaxInvoiceNumbers.SuggestNext(maxSeq);
            candidate = TaxInvoiceNumbers.Build(year, maxSeq);
        }
        while (used.Contains(candidate));
        return candidate;
    }

    public async Task<FiscalResult<TaxInvoiceDto>> GetAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var invoice = await store.FindInvoiceAsync(tenantId, id, ct);
        return invoice is null
            ? FiscalResult<TaxInvoiceDto>.Fail(FiscalErrorCode.NotFound,
                $"Tax invoice {id} was not found.")
            : FiscalResult<TaxInvoiceDto>.Ok(ToDto(invoice));
    }

    public async Task<FiscalResult<TaxInvoiceDto>> TransitionAsync(
        Guid tenantId, Guid id, Guid actorId,
        TaxInvoiceTransitionRequest request, string? ip, CancellationToken ct)
    {
        var invoice = await store.FindInvoiceAsync(tenantId, id, ct);
        if (invoice is null)
        {
            return FiscalResult<TaxInvoiceDto>.Fail(FiscalErrorCode.NotFound,
                $"Tax invoice {id} was not found.");
        }

        var next = (request.ToStatus ?? "").Trim();
        if (!TaxInvoiceStateMachine.CanTransition(invoice.Status, next))
        {
            return FiscalResult<TaxInvoiceDto>.Fail(FiscalErrorCode.Conflict,
                TaxInvoiceStateMachine.Reason(invoice.Status, next));
        }

        var before = invoice.Status;
        invoice.Status = next;
        if (next == TaxInvoiceStatuses.Void)
        {
            invoice.IsVoid = true;
            invoice.VoidedAt = DateTime.UtcNow;
            invoice.VoidReason = string.IsNullOrWhiteSpace(request.Reason)
                ? "(no reason given)"
                : request.Reason!.Trim();
        }
        invoice.UpdatedAt = DateTime.UtcNow;
        invoice.UpdatedBy = actorId;

        if (!await store.TryUpdateInvoiceAsync(invoice, null, ct))
        {
            return FiscalResult<TaxInvoiceDto>.Fail(
                FiscalErrorCode.ConcurrencyConflict,
                "The invoice was modified by someone else — reload and retry.");
        }

        await AuditAsync(tenantId, actorId, "taxinvoice.status_changed",
            "tax_invoices", invoice.Id, null, new
            {
                invoiceNo = invoice.InvoiceNo,
                from = before,
                to = next
            }, ip, ct);
        return FiscalResult<TaxInvoiceDto>.Ok(ToDto(invoice));
    }

    /// <summary>POST /{id}/payments — records a payment; Paid when the
    /// balance reaches zero, PartiallyPaid before that.</summary>
    public async Task<FiscalResult<TaxInvoiceDto>> RecordPaymentAsync(
        Guid tenantId, Guid id, Guid actorId, RecordPaymentRequest request,
        string? ip, CancellationToken ct)
    {
        var invoice = await store.FindInvoiceAsync(tenantId, id, ct);
        if (invoice is null)
        {
            return FiscalResult<TaxInvoiceDto>.Fail(FiscalErrorCode.NotFound,
                $"Tax invoice {id} was not found.");
        }

        if (request.Amount <= 0)
        {
            return FiscalResult<TaxInvoiceDto>.Fail(FiscalErrorCode.ValidationFailed,
                "Payment amount must be greater than zero.");
        }

        if (!TaxInvoiceStateMachine.CanTransition(
                invoice.Status, TaxInvoiceStatuses.PartiallyPaid)
            && invoice.Status != TaxInvoiceStatuses.Paid)
        {
            return FiscalResult<TaxInvoiceDto>.Fail(FiscalErrorCode.Conflict,
                $"A {invoice.Status} invoice cannot receive payments.");
        }

        invoice.PaidAmount += request.Amount;
        invoice.BalanceAmount = Math.Max(0, invoice.GrandTotal - invoice.PaidAmount);
        invoice.Status = invoice.BalanceAmount == 0
            ? TaxInvoiceStatuses.Paid
            : TaxInvoiceStatuses.PartiallyPaid;
        invoice.UpdatedAt = DateTime.UtcNow;
        invoice.UpdatedBy = actorId;

        if (!await store.TryUpdateInvoiceAsync(invoice, null, ct))
        {
            return FiscalResult<TaxInvoiceDto>.Fail(
                FiscalErrorCode.ConcurrencyConflict,
                "The invoice was modified by someone else — reload and retry.");
        }

        await AuditAsync(tenantId, actorId, "taxinvoice.payment_recorded",
            "tax_invoices", invoice.Id, null, new
            {
                invoiceNo = invoice.InvoiceNo,
                amount = request.Amount,
                balance = invoice.BalanceAmount
            }, ip, ct);
        return FiscalResult<TaxInvoiceDto>.Ok(ToDto(invoice));
    }

    // ── Credit Notes ────────────────────────────────────────────────

    /// <summary>
    /// POST /api/v1/credit-notes — a fiscal correction. A credit note
    /// NEVER exists on its own: it always corrects a Tax Invoice (Draft;
    /// its value only reduces the ledger once Applied).
    /// </summary>
    public async Task<FiscalResult<CreditNoteDto>> CreateCreditNoteAsync(
        Guid tenantId, Guid actorId, CreateCreditNoteRequest request,
        string? ip, CancellationToken ct)
    {
        var invoice = await store.FindInvoiceAsync(tenantId, request.TaxInvoiceId, ct);
        if (invoice is null)
        {
            return FiscalResult<CreditNoteDto>.Fail(FiscalErrorCode.NotFound,
                $"Tax invoice {request.TaxInvoiceId} was not found.");
        }

        if (request.Subtotal <= 0)
        {
            return FiscalResult<CreditNoteDto>.Fail(FiscalErrorCode.ValidationFailed,
                "Credit note amount must be greater than zero.");
        }

        if (request.Subtotal > invoice.Subtotal - invoice.DiscountAmount)
        {
            return FiscalResult<CreditNoteDto>.Fail(FiscalErrorCode.ValidationFailed,
                "Credit note cannot exceed the invoice's after-discount amount.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return FiscalResult<CreditNoteDto>.Fail(FiscalErrorCode.ValidationFailed,
                "A credit note reason is required.");
        }

        var year = DateTime.UtcNow.Year;
        var (maxSeq, used) = await store.GetCreditNoteNumbersAsync(tenantId, year, ct);
        var totals = InvoiceTotalsCalculator.Compute(request.Subtotal, 0m);
        var now = DateTime.UtcNow;

        var note = new CreditNote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CreditNoteNo = "",
            TaxInvoiceId = invoice.Id,
            Reason = request.Reason.Trim(),
            Subtotal = request.Subtotal,
            VatAmount = totals.VatAmount,
            GrandTotal = totals.GrandTotal,
            Status = CreditNoteStatuses.Draft,
            RowVersion = 0,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId
        };

        var attempts = 0;
        while (true)
        {
            maxSeq = CreditNoteNumbers.SuggestNext(maxSeq);
            note.CreditNoteNo = CreditNoteNumbers.Build(year, maxSeq);
            if (await store.TryInsertCreditNoteAsync(note, ct))
            {
                break;
            }

            if (++attempts >= AllocationAttempts)
            {
                return FiscalResult<CreditNoteDto>.Fail(
                    FiscalErrorCode.NumberExhausted,
                    "Could not allocate a credit note number. Please retry.");
            }
        }

        await AuditAsync(tenantId, actorId, "creditnote.created",
            "credit_notes", note.Id, null, new
            {
                creditNoteNo = note.CreditNoteNo,
                corrects = invoice.InvoiceNo,
                grandTotal = note.GrandTotal
            }, ip, ct);
        return FiscalResult<CreditNoteDto>.Ok(ToDto(note, invoice.InvoiceNo));
    }

    public async Task<FiscalResult<CreditNoteDto>> TransitionCreditNoteAsync(
        Guid tenantId, Guid id, Guid actorId,
        TaxInvoiceTransitionRequest request, string? ip, CancellationToken ct)
    {
        var note = await store.FindCreditNoteAsync(tenantId, id, ct);
        if (note is null)
        {
            return FiscalResult<CreditNoteDto>.Fail(FiscalErrorCode.NotFound,
                $"Credit note {id} was not found.");
        }

        var next = (request.ToStatus ?? "").Trim();
        if (!CreditNoteStateMachine.CanTransition(note.Status, next))
        {
            return FiscalResult<CreditNoteDto>.Fail(FiscalErrorCode.Conflict,
                CreditNoteStateMachine.Reason(note.Status, next));
        }

        // Applied settles against the invoice: the invoice's balance
        // absorbs the correction.
        if (next == CreditNoteStatuses.Applied)
        {
            var invoice = await store.FindInvoiceAsync(
                tenantId, note.TaxInvoiceId!.Value, ct);
            if (invoice is null)
            {
                return FiscalResult<CreditNoteDto>.Fail(FiscalErrorCode.NotFound,
                    "The corrected tax invoice no longer exists.");
            }

            invoice.BalanceAmount = Math.Max(
                0, invoice.BalanceAmount - note.GrandTotal);
            invoice.UpdatedAt = DateTime.UtcNow;
            invoice.UpdatedBy = actorId;
            await store.TryUpdateInvoiceAsync(invoice, null, ct);
        }

        var before = note.Status;
        note.Status = next;
        if (next == CreditNoteStatuses.Issued)
        {
            note.IssuedAt = DateTime.UtcNow;
        }
        if (next == CreditNoteStatuses.Cancelled)
        {
            note.VoidedAt = DateTime.UtcNow;
            note.VoidedBy = actorId;
            note.VoidReason = string.IsNullOrWhiteSpace(request.Reason)
                ? "(no reason given)"
                : request.Reason!.Trim();
        }
        note.UpdatedAt = DateTime.UtcNow;
        note.UpdatedBy = actorId;

        if (!await store.TryUpdateCreditNoteAsync(note, ct))
        {
            return FiscalResult<CreditNoteDto>.Fail(
                FiscalErrorCode.ConcurrencyConflict,
                "The credit note was modified by someone else — reload and retry.");
        }

        await AuditAsync(tenantId, actorId, "creditnote.status_changed",
            "credit_notes", note.Id, null, new
            {
                creditNoteNo = note.CreditNoteNo,
                from = before,
                to = next
            }, ip, ct);
        return FiscalResult<CreditNoteDto>.Ok(ToDto(note, null));
    }

    // ── VAT report ──────────────────────────────────────────────────

    /// <summary>
    /// GET /api/v1/reports/vat?from&amp;to — the UAE VAT return for a
    /// period: standard-rated supplies and output VAT over the vatable
    /// invoices, minus the credit-note adjustments.
    /// </summary>
    public async Task<VatReportDto> GetVatReportAsync(
        Guid tenantId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var invoices = await store.ListVatableInvoicesAsync(tenantId, from, to, ct);
        var notes = await store.ListVatableCreditNotesAsync(tenantId, from, to, ct);

        var supplies = invoices.Sum(i => i.Subtotal - i.DiscountAmount);
        var outputVat = invoices.Sum(i => i.VatAmount);
        var cnSupplies = notes.Sum(n => n.Subtotal);
        var cnVat = notes.Sum(n => n.VatAmount);

        return new VatReportDto(
            from, to,
            invoices.Count, supplies, outputVat,
            notes.Count, cnSupplies, cnVat,
            outputVat - cnVat);
    }

    // ── Statements ──────────────────────────────────────────────────

    /// <summary>
    /// POST /api/v1/reports/statements — the customer ledger: invoices
    /// as debits, payments and applied credit notes as credits, with the
    /// running balance. Persisted as statements + statement_lines.
    /// </summary>
    public async Task<FiscalResult<StatementDto>> GenerateStatementAsync(
        Guid tenantId, Guid actorId, string customerName,
        DateOnly? from, DateOnly? to, string? ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            return FiscalResult<StatementDto>.Fail(FiscalErrorCode.ValidationFailed,
                "A customer name is required.");
        }

        var customer = customerName.Trim();
        var invoices = await store.ListCustomerInvoicesAsync(
            tenantId, customer, from, to, ct);
        invoices = invoices
            .Where(i => !string.Equals(i.Status, TaxInvoiceStatuses.Draft,
                StringComparison.OrdinalIgnoreCase)
                && !string.Equals(i.Status, TaxInvoiceStatuses.Cancelled,
                    StringComparison.OrdinalIgnoreCase)
                && !string.Equals(i.Status, TaxInvoiceStatuses.Void,
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(i => i.IssueDate ?? DateOnly.MinValue)
            .ThenBy(i => i.InvoiceNo, StringComparer.Ordinal)
            .ToList();

        // All applied credit notes (paginated walk) — each settles against
        // the invoice it corrects on the ledger.
        var allNotes = new List<CreditNote>();
        var page = 1;
        while (true)
        {
            var batch = await store.ListCreditNotesAsync(
                tenantId, null, page, MaxPageSize, ct);
            allNotes.AddRange(batch);
            if (batch.Count < MaxPageSize)
            {
                break;
            }

            page++;
        }

        allNotes = allNotes
            .Where(n => string.Equals(n.Status, CreditNoteStatuses.Applied,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        var lines = new List<StatementLineDto>();
        var rows = new List<(string Type, string No, DateOnly? Date,
            string Desc, decimal Debit, decimal Credit)>();
        foreach (var invoice in invoices)
        {
            rows.Add(("Invoice", invoice.InvoiceNo, invoice.IssueDate,
                $"Tax invoice — {invoice.CustomerName}",
                invoice.GrandTotal, 0m));
            if (invoice.PaidAmount > 0)
            {
                rows.Add(("Payment", invoice.InvoiceNo, invoice.IssueDate,
                    "Payment received", 0m, invoice.PaidAmount));
            }

            foreach (var note in allNotes.Where(
                         n => n.TaxInvoiceId == invoice.Id))
            {
                rows.Add(("CreditNote", note.CreditNoteNo,
                    DateOnly.FromDateTime(note.IssuedAt ?? note.CreatedAt),
                    $"Credit note — {note.Reason}", 0m, note.GrandTotal));
            }
        }

        var balance = 0m;
        var now = DateTime.UtcNow;
        var statement = new Statement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StatementNo = $"ST-{now:yyyyMMddHHmmss}-{customer.GetHashCode():x8}",
            Title = $"Statement — {customer}",
            FilterCustomer = customer,
            FilterDateFrom = from,
            FilterDateTo = to,
            GeneratedAt = now,
            TotalsJson = "",
            StorageKey = "",
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId
        };

        var statementLines = new List<StatementLine>();
        var lineNo = 0;
        foreach (var row in rows)
        {
            balance += row.Debit - row.Credit;
            var runningBalance = Math.Round(
                balance, 2, MidpointRounding.AwayFromZero);
            lines.Add(new StatementLineDto(
                row.Type, row.No, row.Date, row.Desc, row.Debit, row.Credit,
                runningBalance));
            statementLines.Add(new StatementLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                StatementId = statement.Id,
                LineNo = ++lineNo,
                LineType = row.Type,
                DocumentNo = row.No,
                DocumentDate = row.Date,
                Description = row.Desc,
                Debit = row.Debit,
                Credit = row.Credit,
                RunningBalance = runningBalance,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId
            });
        }

        var totalDebit = statementLines.Sum(l => l.Debit);
        var totalCredit = statementLines.Sum(l => l.Credit);
        statement.TotalsJson = JsonSerializer.Serialize(new
        {
            totalDebit,
            totalCredit,
            balance = Math.Round(totalDebit - totalCredit, 2,
                MidpointRounding.AwayFromZero),
            lines = statementLines.Count
        });

        var persisted = await store.PersistStatementAsync(
            tenantId, actorId, statement, statementLines, ct);
        await AuditAsync(tenantId, actorId, "statement.generated",
            "statements", persisted.Id, null, new
            {
                statementNo = persisted.StatementNo,
                customer,
                balance = totalDebit - totalCredit
            }, ip, ct);

        return FiscalResult<StatementDto>.Ok(new StatementDto(
            persisted.Id, persisted.StatementNo, persisted.Title, customer,
            from, to, totalDebit, totalCredit,
            Math.Round(totalDebit - totalCredit, 2, MidpointRounding.AwayFromZero),
            lines));
    }

    public async Task<FiscalResult<StatementDto>> GetStatementAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var statement = await store.FindStatementAsync(tenantId, id, ct);
        if (statement is null)
        {
            return FiscalResult<StatementDto>.Fail(FiscalErrorCode.NotFound,
                $"Statement {id} was not found.");
        }

        var rows = await store.ListStatementLinesAsync(tenantId, id, ct);
        return FiscalResult<StatementDto>.Ok(new StatementDto(
            statement.Id, statement.StatementNo, statement.Title,
            statement.FilterCustomer, statement.FilterDateFrom,
            statement.FilterDateTo,
            rows.Sum(r => r.Debit), rows.Sum(r => r.Credit),
            rows.Count == 0 ? 0 : rows[^1].RunningBalance,
            rows.Select(r => new StatementLineDto(
                r.LineType, r.DocumentNo, r.DocumentDate, r.Description,
                r.Debit, r.Credit, r.RunningBalance)).ToList()));
    }

    // ── list ────────────────────────────────────────────────────────

    public async Task<FiscalResult<IReadOnlyList<TaxInvoiceDto>>> SearchAsync(
        Guid tenantId, string? search, string? status, bool includeVoid,
        int? page, int? pageSize, CancellationToken ct)
    {
        var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        var p = Math.Max(page ?? 1, 1);
        var result = await store.SearchInvoicesAsync(
            tenantId, search, status, includeVoid, p, size, ct);
        return FiscalResult<IReadOnlyList<TaxInvoiceDto>>.Ok(
            result.Select(ToDto).ToList());
    }

    public async Task<FiscalResult<IReadOnlyList<CreditNoteDto>>> ListCreditNotesAsync(
        Guid tenantId, Guid? taxInvoiceId, int? page, int? pageSize,
        CancellationToken ct)
    {
        var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        var p = Math.Max(page ?? 1, 1);
        var notes = await store.ListCreditNotesAsync(
            tenantId, taxInvoiceId, p, size, ct);
        return FiscalResult<IReadOnlyList<CreditNoteDto>>.Ok(
            notes.Select(n => ToDto(n, null)).ToList());
    }

    // ── helpers ─────────────────────────────────────────────────────

    internal static TaxInvoiceDto ToDto(TaxInvoice i) => new(
        i.Id, i.InvoiceNo, i.ProformaId, i.JobOrderId,
        i.DeliveryNoteReference, i.CustomerName, i.ProjectName, i.Status,
        i.PaymentTerms, i.IssueDate, i.DueDate, i.Subtotal,
        i.DiscountPercent, i.DiscountAmount, i.VatAmount, i.GrandTotal,
        i.PaidAmount, i.BalanceAmount, i.IsVoid, i.VoidReason, i.RowVersion);

    private static CreditNoteDto ToDto(CreditNote n, string? invoiceNo) => new(
        n.Id, n.CreditNoteNo, n.TaxInvoiceId ?? Guid.Empty,
        invoiceNo ?? n.TaxInvoice?.InvoiceNo ?? "", n.Reason, n.Status,
        n.Subtotal, n.VatAmount, n.GrandTotal, n.IssuedAt);

    private async Task AuditAsync(
        Guid tenantId, Guid? actorUserId, string action, string entityType,
        Guid entityId, object? before, object? after, string? ip, CancellationToken ct)
        => await audit.WriteAsync(tenantId, actorUserId, action, entityType,
            entityId, ip, null,
            before is null ? null : JsonSerializer.Serialize(before),
            after is null ? null : JsonSerializer.Serialize(after),
            ct).ConfigureAwait(false);
}
