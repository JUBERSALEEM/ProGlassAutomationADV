using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.Fiscal;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// The fiscal core (Phase 12) against live PostgreSQL: the Tax Invoice
/// lifecycle (Draft → Issued → paid/credited, the fiscal lock), family
/// numbering (same PI → -A revision), credit notes that reduce the
/// ledger, the UAE VAT report nets and customer statements.
/// </summary>
[Collection("database")]
public sealed class FiscalFlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly FiscalService _sut;
    private readonly ProformaService _proformas;

    public FiscalFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var factory = new SaasDbContextFactory(fixture.AppDb);
        var audit = new AuditWriter(factory, NullLogger<AuditWriter>.Instance);
        _sut = new FiscalService(new FiscalStore(factory), audit);
        _proformas = new ProformaService(
            new ProformaStore(factory), audit,
            new SpecificationsService(
                new SpecificationsStore(factory), audit));
    }

    // ── full fiscal flow: create → issue → credit → VAT → statement ──

    [Fact]
    public async Task FullFlow_Issue_CreditNote_VatReport_And_Statement()
    {
        var tenant = _fixture.CreateTenant("fisc1");
        var actor = Guid.NewGuid();

        // 1. A standalone invoice for 1,000 at 10% discount:
        //    discount 100, after 900, VAT 45, grand 945, Net 30 → +30d.
        var inv = await _sut.CreateAsync(tenant, actor,
            new CreateTaxInvoiceRequest(
                CustomerName: "Acme Glass Works",
                ProjectName: "Al Reem Tower",
                Subtotal: 1_000m,
                DiscountPercent: 10m),
            null, CancellationToken.None);
        Assert.True(inv.Success, inv.Message);
        var invoice = inv.Value!;
        Assert.StartsWith("INV-", invoice.InvoiceNo);
        Assert.Equal("Draft", invoice.Status);
        Assert.Equal(100m, invoice.DiscountAmount);
        Assert.Equal(45m, invoice.VatAmount);
        Assert.Equal(945m, invoice.GrandTotal);
        Assert.Equal(945m, invoice.BalanceAmount);
        Assert.Equal(invoice.IssueDate!.Value.AddDays(30), invoice.DueDate);

        // 2. Issue — from here the invoice is fiscally locked.
        var issued = await _sut.TransitionAsync(tenant, invoice.Id, actor,
            new TaxInvoiceTransitionRequest("Issued"), null, CancellationToken.None);
        Assert.True(issued.Success, issued.Message);
        Assert.Equal("Issued", issued.Value!.Status);

        // 3. A second invoice for a DIFFERENT amount (fresh base number)
        //    and a credit note against the first: CN 100 → VAT 5 → 105.
        var inv2 = await _sut.CreateAsync(tenant, actor,
            new CreateTaxInvoiceRequest(
                CustomerName: "Acme Glass Works",
                ProjectName: "Marina Shopfront",
                Subtotal: 2_000m),
            null, CancellationToken.None);
        Assert.True(inv2.Success, inv2.Message);
        await _sut.TransitionAsync(tenant, inv2.Value!.Id, actor,
            new TaxInvoiceTransitionRequest("Issued"), null, CancellationToken.None);
        Assert.NotEqual(invoice.InvoiceNo, inv2.Value!.InvoiceNo);

        var cn = await _sut.CreateCreditNoteAsync(tenant, actor,
            new CreateCreditNoteRequest(invoice.Id, 100m, 0m, "Broken panel credit"),
            null, CancellationToken.None);
        Assert.True(cn.Success, cn.Message);
        Assert.StartsWith("CN-", cn.Value!.CreditNoteNo);
        Assert.Equal(5m, cn.Value!.VatAmount);
        Assert.Equal(105m, cn.Value!.GrandTotal);
        Assert.Equal("Draft", cn.Value!.Status);

        // Draft credit notes are NOT in the VAT report; issue + apply it.
        await _sut.TransitionCreditNoteAsync(tenant, cn.Value!.Id, actor,
            new TaxInvoiceTransitionRequest("Issued"), null, CancellationToken.None);
        await _sut.TransitionCreditNoteAsync(tenant, cn.Value!.Id, actor,
            new TaxInvoiceTransitionRequest("Applied"), null, CancellationToken.None);

        // 4. VAT report: output VAT 45 + 100 = 145, minus CN VAT 5 → 140.
        //    (2,000 → VAT 100; supplies (900 + 2,000) = 2,900.)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var report = await _sut.GetVatReportAsync(tenant,
            today.AddDays(-7), today, CancellationToken.None);
        Assert.Equal(2, report.InvoiceCount);
        Assert.Equal(2_900m, report.StandardRatedSupplies);
        Assert.Equal(145m, report.OutputVat);
        Assert.Equal(1, report.CreditNoteCount);
        Assert.Equal(5m, report.CreditNoteVat);
        Assert.Equal(140m, report.NetVatPayable);

        // 5. Statement for Acme: debits 945 + 2,100 (2,000 + 5% VAT),
        //    credits 105 (the applied CN) → balance 2,940.
        var statement = await _sut.GenerateStatementAsync(tenant, actor,
            "Acme Glass Works", today.AddDays(-7), today,
            null, CancellationToken.None);
        Assert.True(statement.Success, statement.Message);
        var st = statement.Value!;
        Assert.Equal(2_940m, st.Balance);
        Assert.Equal(3_045m, st.TotalDebit);
        Assert.Equal(105m, st.TotalCredit);

        // Applied CN reduced the corrected invoice's balance:
        // 945 − 105 = 840.
        var after = await _sut.GetAsync(tenant, invoice.Id, CancellationToken.None);
        Assert.Equal(840m, after.Value!.BalanceAmount);

        // Read the statement back from storage — lines survive.
        var reread = await _sut.GetStatementAsync(tenant, st.Id, CancellationToken.None);
        Assert.True(reread.Success);
        Assert.Equal(st.Lines.Count, reread.Value!.Lines.Count);
        Assert.Equal(2_940m, reread.Value.Balance);
    }

    // ── family numbering: the same PI's second invoice is "-A" ──────

    [Fact]
    public async Task SameProforma_SecondInvoice_GetsRevisionLetter()
    {
        var tenant = _fixture.CreateTenant("fisc2");
        var actor = Guid.NewGuid();

        var pi = (await _proformas.CreateDraftAsync(
            tenant, Guid.NewGuid(), null,
            new CreateProformaRequest(null, null, null, null, null, null, null, null),
            CancellationToken.None)).Value!;

        var first = await _sut.CreateAsync(tenant, actor,
            new CreateTaxInvoiceRequest(
                ProformaId: pi.Id, CustomerName: "Family Co",
                ProjectName: "Villa 12", Subtotal: 500m),
            null, CancellationToken.None);
        Assert.True(first.Success, first.Message);
        Assert.Equal("INV-2026-001".Length, first.Value!.InvoiceNo.Length);

        var second = await _sut.CreateAsync(tenant, actor,
            new CreateTaxInvoiceRequest(
                ProformaId: pi.Id, CustomerName: "Family Co",
                ProjectName: "Villa 12", Subtotal: 625m),
            null, CancellationToken.None);
        Assert.True(second.Success, second.Message);

        // Same PI → same family base number, revision letter appended.
        Assert.StartsWith(first.Value!.InvoiceNo + "-", second.Value!.InvoiceNo);
        Assert.EndsWith("-A", second.Value!.InvoiceNo);
        Assert.Equal(first.Value!.InvoiceNo,
            second.Value!.InvoiceNo[..second.Value.InvoiceNo.LastIndexOf('-')]);
    }

    // ── the fiscal lock ─────────────────────────────────────────────

    [Fact]
    public async Task FiscalLock_IssuedInvoice_CannotGoBackToDraft_And_Voiding_KeepsNumber()
    {
        var tenant = _fixture.CreateTenant("fisc3");
        var actor = Guid.NewGuid();

        var inv = await _sut.CreateAsync(tenant, actor,
            new CreateTaxInvoiceRequest(
                CustomerName: "Locked Co", ProjectName: "P", Subtotal: 100m),
            null, CancellationToken.None);
        Assert.True(inv.Success, inv.Message);
        var invoice = inv.Value!;

        // Draft → Draft stays; Draft → Issued works.
        var issued = await _sut.TransitionAsync(tenant, invoice.Id, actor,
            new TaxInvoiceTransitionRequest("Issued"), null, CancellationToken.None);
        Assert.True(issued.Success);

        // THE FISCAL LOCK: an issued invoice is never edited — the only
        // way out is void or credit, never back to Draft.
        var back = await _sut.TransitionAsync(tenant, invoice.Id, actor,
            new TaxInvoiceTransitionRequest("Draft"), null, CancellationToken.None);
        Assert.False(back.Success);
        Assert.Equal(FiscalErrorCode.Conflict, back.ErrorCode);

        // Voiding records reason + is_void, number unchanged.
        var voided = await _sut.TransitionAsync(tenant, invoice.Id, actor,
            new TaxInvoiceTransitionRequest("Void", "Wrong customer"), null,
            CancellationToken.None);
        Assert.True(voided.Success, voided.Message);
        Assert.True(voided.Value!.IsVoid);
        Assert.Equal("Wrong customer", voided.Value.VoidReason);
        Assert.Equal(invoice.InvoiceNo, voided.Value.InvoiceNo);

        // Void is terminal.
        var again = await _sut.TransitionAsync(tenant, invoice.Id, actor,
            new TaxInvoiceTransitionRequest("Issued"), null, CancellationToken.None);
        Assert.False(again.Success);

        // A voided invoice is out of the VAT report entirely.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var report = await _sut.GetVatReportAsync(tenant,
            today.AddDays(-1), today, CancellationToken.None);
        Assert.Equal(0, report.InvoiceCount);
        Assert.Equal(0m, report.NetVatPayable);
    }

    // ── payments ────────────────────────────────────────────────────

    [Fact]
    public async Task Payments_PartiallyPaid_Then_Paid()
    {
        var tenant = _fixture.CreateTenant("fisc4");
        var actor = Guid.NewGuid();

        var inv = await _sut.CreateAsync(tenant, actor,
            new CreateTaxInvoiceRequest(
                CustomerName: "Pay Co", ProjectName: "P", Subtotal: 200m),
            null, CancellationToken.None);
        var invoice = inv.Value!;

        // A Draft invoice cannot receive payments.
        var tooEarly = await _sut.RecordPaymentAsync(tenant, invoice.Id, actor,
            new RecordPaymentRequest(10.50m), null, CancellationToken.None);
        Assert.False(tooEarly.Success);

        await _sut.TransitionAsync(tenant, invoice.Id, actor,
            new TaxInvoiceTransitionRequest("Issued"), null, CancellationToken.None);

        // Grand total is 210 (200 + 5% VAT); pay 10.50 → 199.50 left.
        var half = await _sut.RecordPaymentAsync(tenant, invoice.Id, actor,
            new RecordPaymentRequest(10.50m), null, CancellationToken.None);
        Assert.True(half.Success, half.Message);
        Assert.Equal("PartiallyPaid", half.Value!.Status);
        Assert.Equal(10.50m, half.Value.PaidAmount);
        Assert.Equal(210m - 10.50m, half.Value.BalanceAmount);

        var rest = await _sut.RecordPaymentAsync(tenant, invoice.Id, actor,
            new RecordPaymentRequest(half.Value.BalanceAmount), null,
            CancellationToken.None);
        Assert.True(rest.Success, rest.Message);
        Assert.Equal("Paid", rest.Value!.Status);
        Assert.Equal(0m, rest.Value.BalanceAmount);

        // Paid is terminal — no more payments.
        var extra = await _sut.RecordPaymentAsync(tenant, invoice.Id, actor,
            new RecordPaymentRequest(1m), null, CancellationToken.None);
        Assert.False(extra.Success);
    }

    // ── credit note guards ──────────────────────────────────────────

    [Fact]
    public async Task CreditNote_Guards_OverCredit_UnknownInvoice_ReasonRequired()
    {
        var tenant = _fixture.CreateTenant("fisc5");
        var actor = Guid.NewGuid();

        var inv = await _sut.CreateAsync(tenant, actor,
            new CreateTaxInvoiceRequest(
                CustomerName: "Guard Co", ProjectName: "P", Subtotal: 100m),
            null, CancellationToken.None);
        Assert.True(inv.Success);
        var invoice = inv.Value!;

        // Unknown invoice.
        var missing = await _sut.CreateCreditNoteAsync(tenant, actor,
            new CreateCreditNoteRequest(Guid.NewGuid(), 10m, 0m, "x"),
            null, CancellationToken.None);
        Assert.False(missing.Success);
        Assert.Equal(FiscalErrorCode.NotFound, missing.ErrorCode);

        // Reason required.
        var noReason = await _sut.CreateCreditNoteAsync(tenant, actor,
            new CreateCreditNoteRequest(invoice.Id, 10m, 0m, "  "),
            null, CancellationToken.None);
        Assert.False(noReason.Success);
        Assert.Equal(FiscalErrorCode.ValidationFailed, noReason.ErrorCode);

        // Cannot credit beyond the invoice's after-discount amount.
        var over = await _sut.CreateCreditNoteAsync(tenant, actor,
            new CreateCreditNoteRequest(invoice.Id, 101m, 0m, "too much"),
            null, CancellationToken.None);
        Assert.False(over.Success);

        // Draft credit notes cannot be Applied directly (machine-guarded).
        var cn = await _sut.CreateCreditNoteAsync(tenant, actor,
            new CreateCreditNoteRequest(invoice.Id, 40m, 0m, "short shipment"),
            null, CancellationToken.None);
        Assert.True(cn.Success, cn.Message);
        var applied = await _sut.TransitionCreditNoteAsync(tenant, cn.Value!.Id,
            actor, new TaxInvoiceTransitionRequest("Applied"), null,
            CancellationToken.None);
        Assert.False(applied.Success);
        Assert.Equal(FiscalErrorCode.Conflict, applied.ErrorCode);
    }

    // ── tenant isolation ────────────────────────────────────────────

    [Fact]
    public async Task TenantIsolation_InvoicesAndStatements()
    {
        var tenantA = _fixture.CreateTenant("fisc6a");
        var tenantB = _fixture.CreateTenant("fisc6b");
        var actor = Guid.NewGuid();

        var a = await _sut.CreateAsync(tenantA, actor,
            new CreateTaxInvoiceRequest(
                CustomerName: "Shared Name Co", ProjectName: "P", Subtotal: 300m),
            null, CancellationToken.None);
        Assert.True(a.Success);
        await _sut.TransitionAsync(tenantA, a.Value!.Id, actor,
            new TaxInvoiceTransitionRequest("Issued"), null, CancellationToken.None);

        // Tenant B sees none of A's invoices, numbers or reports.
        var bList = await _sut.SearchAsync(tenantB, null, null, false, 1, 50,
            CancellationToken.None);
        Assert.Empty(bList.Value!);

        var bReport = await _sut.GetVatReportAsync(tenantB,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);
        Assert.Equal(0, bReport.InvoiceCount);
        Assert.Equal(0m, bReport.NetVatPayable);

        var bStatement = await _sut.GenerateStatementAsync(tenantB, actor,
            "Shared Name Co", null, null, null, CancellationToken.None);
        Assert.True(bStatement.Success);
        Assert.Empty(bStatement.Value!.Lines);
        Assert.Equal(0m, bStatement.Value.Balance);

        // Tenant B starts its own numbering at 001 (RLS-scoped sequence).
        var b = await _sut.CreateAsync(tenantB, actor,
            new CreateTaxInvoiceRequest(
                CustomerName: "B Co", ProjectName: "P", Subtotal: 50m),
            null, CancellationToken.None);
        Assert.True(b.Success);
        Assert.EndsWith("-001", b.Value!.InvoiceNo);
    }
}
