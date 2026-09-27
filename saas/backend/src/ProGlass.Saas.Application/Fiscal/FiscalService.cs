using System.Text.Json;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Application.Subscriptions;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Fiscal;
using ProGlass.Saas.Domain.Money;
using ProGlass.Saas.Domain.Pricing;
using ProGlass.Saas.Domain.Proforma;
using ProGlass.Saas.Domain.Subscriptions;

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
    NumberExhausted,
    /// <summary>Phase 18: a per-period plan limit (tax invoices, reports)
    /// was reached — HTTP 402, the architecture doc's subscription-limit
    /// semantics.</summary>
    SubscriptionLimitExceeded
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
    DateOnly? IssueDate = null,
    InvoicePrintData? Print = null,
    /// <summary>Phase 19 — line items (the WPF TaxInvoiceItems). When
    /// present, the WPF BuildItemsAndSubtotal math DERIVES the subtotal
    /// (Σ MoneyRounding.Round(rate × qty × sqm)) and the request's
    /// Subtotal is ignored — the WPF wizard never trusts a client
    /// total.</summary>
    IReadOnlyList<TaxInvoiceItemInput>? Items = null);

/// <summary>One invoice line item (WPF TaxInvoiceItem; sqm/value are
/// computed server-side with the WPF row math).</summary>
public sealed record TaxInvoiceItemInput(
    int SpecGroupNumber,
    int SerialNumber,
    string SpecGroupName = "",
    string GlassType = "",
    string GlassRef = "",
    int W1 = 0,
    int H1 = 0,
    int W2 = 0,
    int H2 = 0,
    int Qty = 1,
    decimal Rate = 0,
    string Notes = "",
    Guid? SpecGroupItemId = null);

/// <summary>Print-surface data for a tax invoice (Phase 17) — the WPF
/// InvWizardState fields that feed the snapshot's Company/Customer/Project/
/// Bank/PreparedBy sections and the SavedTaxInvoice print columns. All
/// fields default to "" (the WPF defaults); every field is optional.</summary>
public sealed record InvoicePrintData(
    string CompanyName = "",
    string CompanyTrn = "",
    string CompanyAddress = "",
    string CompanyLocation = "",
    string CompanyPhone = "",
    string CompanyEmail = "",
    string CustomerTrn = "",
    string CustomerAddress = "",
    string CustomerPhone = "",
    string CustomerEmail = "",
    string ContactPerson = "",
    string ProjectIdentifierName = "",
    string ProjectContractNo = "",
    string ProjectReference = "",
    string SiteDeliveryLocation = "",
    string BankName = "",
    string BankAccountNumber = "",
    string Iban = "",
    string SwiftCode = "",
    string PaymentNotes = "",
    string PreparedByName = "",
    string PreparedByTitle = "",
    string PreparedByDepartment = "");

public sealed record TaxInvoiceTransitionRequest(string ToStatus, string? Reason = null);
public sealed record RecordPaymentRequest(decimal Amount, string? Notes = null);
public sealed record VoidFiscalRequest(string? Reason = null);

public sealed record CreateCreditNoteRequest(
    Guid TaxInvoiceId,
    decimal Subtotal,
    decimal DiscountPercent = 0,
    string Reason = "");

/// <summary>Statement filters (Phase 17) — the WPF StatementFilter axes:
/// company/customer/project/salesman contain, date range on IssueDate, and
/// a status multi-select (empty = all). At least one axis must be set (the
/// WPF gates Generate on HasAnyFilter).</summary>
public sealed record GenerateStatementRequest(
    string? Customer = null,
    string? Company = null,
    string? Project = null,
    string? Salesman = null,
    IReadOnlyList<string>? Statuses = null,
    DateOnly? From = null,
    DateOnly? To = null);

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
    int RowVersion,
    string CompanyName = "",
    string CompanyTrn = "",
    string PurchaseOrderNumber = "",
    string BankName = "",
    string BankAccountNumber = "",
    string Iban = "",
    string SwiftCode = "",
    string? SnapshotJson = null);

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
    IReadOnlyList<StatementLineDto> Lines,
    string Company = "",
    string Project = "",
    string Salesman = "",
    IReadOnlyList<string>? Statuses = null);

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

    Task<bool> TryInsertInvoiceAsync(TaxInvoice invoice,
        IReadOnlyList<TaxInvoiceItem>? items, CancellationToken ct);

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

    /// <summary>Phase 18 limit counters — usage since the current billing
    /// period start.</summary>
    Task<int> CountInvoicesSinceAsync(
        Guid tenantId, DateOnly since, CancellationToken ct);

    /// <summary>Phase 19 reconciliation queries.</summary>
    Task<IReadOnlyList<ProformaLine>> ListProformaLinesAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct);

    Task<IReadOnlyList<(Guid JobOrderId, string JobNo)>>
        ListJobOrderNosForProformaAsync(
            Guid tenantId, Guid proformaId, CancellationToken ct);

    /// <summary>Σ delivered_qty by (spec_group_number, serial_number) over
    /// the job orders' dispatch DNs — excludes Draft, Dummy and master
    /// DNs (the WPF "confirmed" filter).</summary>
    Task<Dictionary<(int Group, int Serial), int>> SumDeliveredByLineAsync(
        Guid tenantId, IReadOnlyList<Guid> jobOrderIds, CancellationToken ct);

    /// <summary>Σ qty by (spec_group_number, serial_number) over the PI's
    /// family invoices — excludes Draft, Cancelled, Void.</summary>
    Task<Dictionary<(int Group, int Serial), int>> SumInvoicedByLineAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct);

    Task<int> CountStatementsSinceAsync(
        Guid tenantId, DateOnly since, CancellationToken ct);

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
public sealed class FiscalService(IFiscalStore store, IAuditWriter audit,
    PlanLimitGuard limits)
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

        // Phase 18 — per-period invoice limit (plan §6; catalog value,
        // NULL = unlimited, hard-coded nowhere).
        var limitCheck = await limits.ResolveAsync(
            tenantId, ModuleFeatureMap.TaxInvoices, ct);
        if (limitCheck?.Limit is int cap)
        {
            var used = await store.CountInvoicesSinceAsync(
                tenantId, limitCheck.PeriodStart, ct);
            if (used >= cap)
            {
                return FiscalResult<TaxInvoiceDto>.Fail(
                    FiscalErrorCode.SubscriptionLimitExceeded,
                    $"The current plan's invoice limit ({cap} per billing " +
                    $"period) has been reached — {used} invoices since " +
                    $"{limitCheck.PeriodStart:yyyy-MM-dd}. Choose a plan " +
                    "with a higher limit in Settings → Subscription.");
            }
        }

        var totals = InvoiceTotalsCalculator.Compute(
            request.Subtotal, request.DiscountPercent);
        var issueDate = request.IssueDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dueDate = issueDate.AddDays(
            PaymentTermsResolver.ResolveDays(request.PaymentTerms));
        var print = request.Print ?? new InvoicePrintData();

        // Phase 19 — line items (WPF BuildItemsAndSubtotal): when items are
        // present the subtotal is DERIVED with the WPF row math
        // (SqmCalculator 3 dp + the 0.5 m² floor; value = MoneyRounding
        // 2 dp away-from-zero of rate × qty × sqm); the request's Subtotal
        // is ignored — the WPF wizard never trusts a client total.
        var items = new List<TaxInvoiceItem>();
        if (request.Items is { Count: > 0 })
        {
            foreach (var input in request.Items)
            {
                if (input.Qty <= 0)
                {
                    return FiscalResult<TaxInvoiceDto>.Fail(
                        FiscalErrorCode.ValidationFailed,
                        $"Item {input.SpecGroupNumber}/{input.SerialNumber}: " +
                        "quantity must be positive.");
                }

                var sqm = SqmCalculator.CalculatePanel(input.W1, input.H1);
                items.Add(new TaxInvoiceItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    TaxInvoiceId = Guid.Empty,   // set below, once allocated
                    SpecGroupItemId = input.SpecGroupItemId,
                    SpecGroupNumber = input.SpecGroupNumber,
                    SerialNumber = input.SerialNumber,
                    SpecGroupName = input.SpecGroupName ?? "",
                    GlassType = input.GlassType ?? "",
                    GlassRef = input.GlassRef ?? "",
                    W1Mm = input.W1,
                    H1Mm = input.H1,
                    W2Mm = input.W2,
                    H2Mm = input.H2,
                    Qty = input.Qty,
                    Sqm = sqm,
                    TotalSqm = Math.Round(sqm * input.Qty, 3,
                        MidpointRounding.ToEven),
                    Rate = input.Rate,
                    Value = MoneyRounding.Round(input.Rate * input.Qty * sqm),
                    Notes = input.Notes ?? "",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = actorId,
                    UpdatedBy = actorId
                });
            }

            request = request with { Subtotal = items.Sum(i => i.Value) };
        }

        // WPF financial snapshot inputs: the family's PRIOR invoices are the
        // "previously invoiced" total; the balance left on the PI follows
        // the WPF formula (floored at 0).
        var previouslyInvoiced = family.Sum(i => i.GrandTotal);
        var proformaTotal = pi?.GrandTotalVatInclusive ?? 0m;

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
            CompanyName = print.CompanyName,
            CompanyTrn = print.CompanyTrn,
            PurchaseOrderNumber = request.PurchaseOrderNumber ?? "",
            BankName = print.BankName,
            BankAccountNumber = print.BankAccountNumber,
            Iban = print.Iban,
            SwiftCode = print.SwiftCode,
            RowVersion = 0,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId
        };

        // Phase 17 — the print snapshot (WPF TaxInvoiceMapper.BuildSnapshot):
        // built for the CURRENT number and rebuilt whenever the allocation
        // race forces a different one.
        void AttachSnapshot()
        {
            var (sequenceLetter, sequenceNumber) = ParseSequence(invoice.InvoiceNo);

            // Phase 19 — SpecGroups populated from the line items when
            // present (WPF BuildSnapshot), still empty for totals-only
            // invoices (documented).
            var snapshotSpecGroups = items
                .GroupBy(i => (i.SpecGroupNumber, i.SpecGroupName,
                    i.GlassType))
                .Select(g => new InvSpecGroupSnapshot
                {
                    GroupNumber = g.Key.SpecGroupNumber,
                    GroupName = g.Key.SpecGroupName,
                    GlassType = g.Key.GlassType,
                    Rows = g.Select(i => new InvGlassItemRowSnapshot
                    {
                        SerialNumber = i.SerialNumber,
                        GlassRef = i.GlassRef,
                        W1 = i.W1Mm,
                        H1 = i.H1Mm,
                        W2 = i.W2Mm,
                        H2 = i.H2Mm,
                        Qty = i.Qty,
                        Sqm = i.Sqm,
                        TotalSqm = i.TotalSqm,
                        Rate = i.Rate,
                        Value = i.Value,
                        Notes = i.Notes
                    }).ToList(),
                    GroupTotalQty = g.Sum(i => i.Qty),
                    GroupTotalSqm = Math.Round(g.Sum(i => i.TotalSqm), 3),
                    GroupTotalValue = MoneyRounding.Round(g.Sum(i => i.Value))
                })
                .ToList();

            invoice.SnapshotJson = TaxInvoiceSnapshotCodec.Serialize(
                new TaxInvoiceSnapshot
                {
                    SpecGroups = snapshotSpecGroups,
                    Header = new InvHeaderSnapshot
                    {
                        InvoiceNumber = invoice.InvoiceNo,
                        ProformaId = pi?.ProformaNo ?? "",
                        DeliveryNoteReference = invoice.DeliveryNoteReference,
                        JobOrderId = job?.JobNo ?? "",
                        PurchaseOrderNumber = invoice.PurchaseOrderNumber,
                        InvoiceType = sequenceLetter == "" ? "Full" : "Partial",
                        SequenceLetter = sequenceLetter,
                        SequenceNumber = sequenceNumber,
                        Status = invoice.Status,
                        IssueDate = issueDate.ToDateTime(TimeOnly.MinValue),
                        DueDate = dueDate.ToDateTime(TimeOnly.MinValue)
                    },
                    Company = new InvCompanySnapshot
                    {
                        CompanyName = print.CompanyName,
                        CompanyTrn = print.CompanyTrn,
                        CompanyAddress = print.CompanyAddress,
                        CompanyLocation = print.CompanyLocation,
                        CompanyPhone = print.CompanyPhone,
                        CompanyEmail = print.CompanyEmail
                    },
                    Customer = new InvCustomerSnapshot
                    {
                        CustomerName = request.CustomerName,
                        CustomerTrn = print.CustomerTrn,
                        CustomerAddress = print.CustomerAddress,
                        CustomerPhone = print.CustomerPhone,
                        CustomerEmail = print.CustomerEmail,
                        ContactPerson = print.ContactPerson
                    },
                    Project = new InvProjectSnapshot
                    {
                        ProjectIdentifierName = print.ProjectIdentifierName,
                        ProjectContractNo = print.ProjectContractNo,
                        ProjectReference = print.ProjectReference,
                        SiteDeliveryLocation = print.SiteDeliveryLocation
                    },
                    Financial = new InvFinancialSnapshot
                    {
                        Subtotal = invoice.Subtotal,
                        DiscountPercent = invoice.DiscountPercent,
                        DiscountAmount = invoice.DiscountAmount,
                        AfterDiscount = invoice.Subtotal - invoice.DiscountAmount,
                        VatAmount = invoice.VatAmount,
                        GrandTotal = invoice.GrandTotal,
                        GrandTotalInWords =
                            TaxInvoiceSnapshotCodec.AmountToWords(invoice.GrandTotal),
                        PreviouslyInvoicedTotal = previouslyInvoiced,
                        RemainingBalance = Math.Max(0m,
                            proformaTotal - previouslyInvoiced - invoice.GrandTotal)
                    },
                    Bank = new InvBankSnapshot
                    {
                        BankName = print.BankName,
                        BankAccountNumber = print.BankAccountNumber,
                        Iban = print.Iban,
                        SwiftCode = print.SwiftCode,
                        PaymentTerms = request.PaymentTerms,
                        PaymentTermsDays = PaymentTermsResolver.ResolveDays(
                            request.PaymentTerms),
                        PaymentNotes = print.PaymentNotes
                    },
                    PreparedBy = new InvPreparedBySnapshot
                    {
                        Name = print.PreparedByName,
                        Title = print.PreparedByTitle,
                        Department = print.PreparedByDepartment
                    }
                });
        }

        AttachSnapshot();
        foreach (var item in items)
        {
            item.TaxInvoiceId = invoice.Id;
        }

        var attempts = 0;
        while (!await store.TryInsertInvoiceAsync(invoice, items, ct))
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
            AttachSnapshot();
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

    /// <summary>Derives the WPF Header.SequenceLetter / SequenceNumber from
    /// the family number: INV-2026-007[-A] → ("", 7) / ("A", 7). The WPF
    /// stores both on the entity; the SaaS encodes them in the number.</summary>
    private static (string Letter, int Sequence) ParseSequence(string invoiceNo)
    {
        var parts = (invoiceNo ?? "").Split('-');
        var seq = parts.Length >= 3 && int.TryParse(parts[2], out var n) ? n : 1;
        var letter = parts.Length == 4 && parts[3].Length == 1
            && parts[3][0] is >= 'A' and <= 'Z'
                ? parts[3]
                : "";
        return (letter, seq == 0 ? 1 : seq);
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
        // Phase 17 — WPF SetHeaderStatus: the new status is written onto the
        // snapshot header too, so a later load cannot resurrect the old one.
        var reserialized = TaxInvoiceSnapshotCodec.WithHeaderStatus(
            invoice.SnapshotJson, next);
        if (reserialized is not null)
        {
            invoice.SnapshotJson = reserialized;
        }

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
        Guid tenantId, Guid actorId, GenerateStatementRequest request,
        string? ip, CancellationToken ct)
    {
        // Phase 17 — the WPF StatementFilter axes. At least one axis is
        // required (the WPF gates Generate on HasAnyFilter).
        var customer = request.Customer?.Trim();
        var company = request.Company?.Trim();
        var project = request.Project?.Trim();
        var salesman = request.Salesman?.Trim();
        var statuses = (request.Statuses ?? Array.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .ToHashSet();

        var hasAnyFilter =
            !string.IsNullOrWhiteSpace(customer) ||
            !string.IsNullOrWhiteSpace(company) ||
            !string.IsNullOrWhiteSpace(project) ||
            !string.IsNullOrWhiteSpace(salesman) ||
            request.From.HasValue || request.To.HasValue ||
            statuses.Count > 0;
        if (!hasAnyFilter)
        {
            return FiscalResult<StatementDto>.Fail(FiscalErrorCode.ValidationFailed,
                "At least one statement filter is required " +
                "(customer, company, project, salesman, statuses or date range).");
        }

        // Phase 18 — per-period reports limit (statements are the report
        // generator; catalog value, NULL = unlimited).
        var reportsLimit = await limits.ResolveAsync(
            tenantId, ModuleFeatureMap.Reports, ct);
        if (reportsLimit?.Limit is int reportsCap)
        {
            var statementsUsed = await store.CountStatementsSinceAsync(
                tenantId, reportsLimit.PeriodStart, ct);
            if (statementsUsed >= reportsCap)
            {
                return FiscalResult<StatementDto>.Fail(
                    FiscalErrorCode.SubscriptionLimitExceeded,
                    $"The current plan's report limit ({reportsCap} per " +
                    $"billing period) has been reached — {statementsUsed} " +
                    $"statements since {reportsLimit.PeriodStart:yyyy-MM-dd}. " +
                    "Choose a plan with a higher limit in Settings → " +
                    "Subscription.");
            }
        }

        // Invoice walk (paginated) — the WPF aggregator loads the invoice
        // list and applies its filters in memory; the SaaS does the same so
        // the semantics (case-insensitive contains, the salesman's snapshot
        // parse) stay verbatim.
        var invoices = new List<TaxInvoice>();
        var page = 1;
        while (true)
        {
            var batch = await store.SearchInvoicesAsync(
                tenantId, null, null, true, page, MaxPageSize, ct);
            invoices.AddRange(batch);
            if (batch.Count < MaxPageSize)
            {
                break;
            }

            page++;
        }

        // The ledger universe (Phase 12 = the WPF aggregator's validity
        // filter): non-draft, non-cancelled, non-void invoices.
        IEnumerable<TaxInvoice> q = invoices
            .Where(i => !string.Equals(i.Status, TaxInvoiceStatuses.Draft,
                StringComparison.OrdinalIgnoreCase)
                && !string.Equals(i.Status, TaxInvoiceStatuses.Cancelled,
                    StringComparison.OrdinalIgnoreCase)
                && !string.Equals(i.Status, TaxInvoiceStatuses.Void,
                    StringComparison.OrdinalIgnoreCase));

        // WPF StatementAggregator.ApplyFilters — SafeContains semantics.
        if (!string.IsNullOrWhiteSpace(company))
        {
            q = q.Where(i => SafeContains(i.CompanyName, company));
        }

        if (!string.IsNullOrWhiteSpace(customer))
        {
            q = q.Where(i => SafeContains(i.CustomerName, customer));
        }

        if (!string.IsNullOrWhiteSpace(project))
        {
            // WPF Chat 20: bidirectional contains on the indexed column.
            q = q.Where(i => SafeContains(i.ProjectName, project)
                || SafeContains(project, i.ProjectName));
        }

        if (!string.IsNullOrWhiteSpace(salesman))
        {
            // WPF: resolved from TaxInvoiceSnapshot.PreparedBy.Name — a
            // post-query JSON parse in the aggregator; same here.
            q = q.Where(i => SafeContains(
                TaxInvoiceSnapshotCodec.Deserialize(i.SnapshotJson)
                    ?.PreparedBy?.Name, salesman));
        }

        if (request.From.HasValue)
        {
            var df = request.From.Value;
            q = q.Where(i => (i.IssueDate ?? DateOnly.MinValue) >= df);
        }

        if (request.To.HasValue)
        {
            var dt = request.To.Value;
            q = q.Where(i => (i.IssueDate ?? DateOnly.MinValue) <= dt);
        }

        if (statuses.Count > 0)
        {
            q = q.Where(i => statuses.Contains(
                (i.Status ?? "").Trim().ToLowerInvariant()));
        }

        var selected = q
            .OrderBy(i => i.IssueDate ?? DateOnly.MinValue)
            .ThenBy(i => i.InvoiceNo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // All applied credit notes (paginated walk) — each settles against
        // the invoice it corrects on the ledger.
        var allNotes = new List<CreditNote>();
        page = 1;
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
        foreach (var invoice in selected)
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
            Title = StatementTitle(customer, project, company, salesman,
                request.From, request.To),
            FilterCustomer = customer ?? "",
            FilterCompany = company ?? "",
            FilterProject = project ?? "",
            FilterSalesman = salesman ?? "",
            FilterDateFrom = request.From,
            FilterDateTo = request.To,
            FilterStatuses = statuses.Count == 0
                ? null
                : JsonSerializer.Serialize(statuses.OrderBy(s => s).ToList()),
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
                company,
                project,
                salesman,
                balance = totalDebit - totalCredit
            }, ip, ct);

        return FiscalResult<StatementDto>.Ok(new StatementDto(
            persisted.Id, persisted.StatementNo, persisted.Title,
            customer ?? "", request.From, request.To, totalDebit, totalCredit,
            Math.Round(totalDebit - totalCredit, 2, MidpointRounding.AwayFromZero),
            lines, company ?? "", project ?? "", salesman ?? "",
            statuses.Count == 0 ? null : statuses.OrderBy(s => s).ToList()));
    }

    /// <summary>WPF StatementFilter.GetStatementTitle — priority customer >
    /// project > company > salesman > date range > "Full Account
    /// Statement".</summary>
    private static string StatementTitle(
        string? customer, string? project, string? company, string? salesman,
        DateOnly? from, DateOnly? to)
    {
        if (!string.IsNullOrWhiteSpace(customer))
            return $"Account Statement — {customer}";
        if (!string.IsNullOrWhiteSpace(project))
            return $"Project Statement — {project}";
        if (!string.IsNullOrWhiteSpace(company))
            return $"Company Statement — {company}";
        if (!string.IsNullOrWhiteSpace(salesman))
            return $"Sales Report — {salesman}";
        if (from.HasValue || to.HasValue)
        {
            var f = from?.ToString("MMM yyyy") ?? "—";
            var t = to?.ToString("MMM yyyy") ?? "—";
            return $"Statement — {f} to {t}";
        }

        return "Full Account Statement";
    }

    /// <summary>WPF StatementAggregator.SafeContains — null-safe,
    /// trimmed, case-insensitive contains ("" needle matches
    /// everything).</summary>
    private static bool SafeContains(string? haystack, string? needle)
    {
        if (string.IsNullOrWhiteSpace(needle)) return true;
        if (string.IsNullOrEmpty(haystack)) return false;

        return haystack
            .Trim()
            .Contains(needle.Trim(), StringComparison.OrdinalIgnoreCase);
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
                r.Debit, r.Credit, r.RunningBalance)).ToList(),
            statement.FilterCompany, statement.FilterProject,
            statement.FilterSalesman,
            DeserializeStatuses(statement.FilterStatuses)));
    }

    /// <summary>Reads the persisted status multi-select back (null when the
    /// statement was generated with "all statuses").</summary>
    private static IReadOnlyList<string>? DeserializeStatuses(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
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
        i.PaidAmount, i.BalanceAmount, i.IsVoid, i.VoidReason, i.RowVersion,
        i.CompanyName, i.CompanyTrn, i.PurchaseOrderNumber, i.BankName,
        i.BankAccountNumber, i.Iban, i.SwiftCode, i.SnapshotJson);

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
