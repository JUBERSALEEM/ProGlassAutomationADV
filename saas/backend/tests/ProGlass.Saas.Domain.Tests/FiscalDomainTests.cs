using ProGlass.Saas.Domain.Money;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Domain.Tests;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 12 fiscal domain goldens — the WPF TaxInvoice/CreditNote
//  StatusTransitions tables, PaymentTermsResolver, InvoiceTotalsCalculator
//  (UAE VAT 5%, half-fil rounds AWAY from zero), family numbering with
//  revision letters and CN- numbering, hand-traced from the ported code.
// ═══════════════════════════════════════════════════════════════════════

public class FiscalDomainTests
{
    // ── TaxInvoiceStateMachine (WPF StatusTransitions verbatim) ─────

    [Fact]
    public void TaxInvoice_Draft_CanReach_Issued_Cancelled_Void()
    {
        Assert.True(TaxInvoiceStateMachine.CanTransition("Draft", "Issued"));
        Assert.True(TaxInvoiceStateMachine.CanTransition("Draft", "Cancelled"));
        Assert.True(TaxInvoiceStateMachine.CanTransition("Draft", "Void"));
        Assert.True(TaxInvoiceStateMachine.CanTransition("Draft", "Draft"));
    }

    [Fact]
    public void TaxInvoice_Issued_FiscalLock_OnlyMovesForwardOrEnds()
    {
        // Once Issued, an invoice is NEVER edited — only paid, overdue,
        // voided or cancelled. Draft is unreachable (the fiscal lock).
        Assert.True(TaxInvoiceStateMachine.CanTransition("Issued", "Issued"));
        Assert.True(TaxInvoiceStateMachine.CanTransition("Issued", "Paid"));
        Assert.True(TaxInvoiceStateMachine.CanTransition("Issued", "PartiallyPaid"));
        Assert.True(TaxInvoiceStateMachine.CanTransition("Issued", "Overdue"));
        Assert.True(TaxInvoiceStateMachine.CanTransition("Issued", "Cancelled"));
        Assert.True(TaxInvoiceStateMachine.CanTransition("Issued", "Void"));
        Assert.False(TaxInvoiceStateMachine.CanTransition("Issued", "Draft"));
    }

    [Fact]
    public void TaxInvoice_Paid_IsTerminal()
    {
        foreach (var next in TaxInvoiceStatuses.All)
        {
            Assert.Equal(next == "Paid",
                TaxInvoiceStateMachine.CanTransition("Paid", next));
        }
    }

    [Fact]
    public void TaxInvoice_Overdue_CanRecoverToPaidOrPartiallyPaid()
    {
        Assert.True(TaxInvoiceStateMachine.CanTransition("Overdue", "Paid"));
        Assert.True(TaxInvoiceStateMachine.CanTransition("Overdue", "PartiallyPaid"));
        Assert.True(TaxInvoiceStateMachine.CanTransition("Overdue", "Cancelled"));
        Assert.True(TaxInvoiceStateMachine.CanTransition("Overdue", "Void"));
        Assert.False(TaxInvoiceStateMachine.CanTransition("Overdue", "Issued"));
        Assert.False(TaxInvoiceStateMachine.CanTransition("Overdue", "Draft"));
    }

    [Fact]
    public void TaxInvoice_Cancelled_And_Void_AreTerminal()
    {
        Assert.False(TaxInvoiceStateMachine.CanTransition("Cancelled", "Issued"));
        Assert.False(TaxInvoiceStateMachine.CanTransition("Cancelled", "Draft"));
        Assert.False(TaxInvoiceStateMachine.CanTransition("Void", "Issued"));
        Assert.False(TaxInvoiceStateMachine.CanTransition("Void", "Draft"));
        Assert.True(TaxInvoiceStateMachine.CanTransition("Cancelled", "Cancelled"));
    }

    [Fact]
    public void TaxInvoice_PartiallyPaid_Paths()
    {
        Assert.True(TaxInvoiceStateMachine.CanTransition("PartiallyPaid", "Paid"));
        Assert.True(TaxInvoiceStateMachine.CanTransition("PartiallyPaid", "PartiallyPaid"));
        Assert.True(TaxInvoiceStateMachine.CanTransition("PartiallyPaid", "Overdue"));
        Assert.True(TaxInvoiceStateMachine.CanTransition("PartiallyPaid", "Void"));
        Assert.False(TaxInvoiceStateMachine.CanTransition("PartiallyPaid", "Issued"));
        Assert.False(TaxInvoiceStateMachine.CanTransition("PartiallyPaid", "Draft"));
    }

    [Fact]
    public void TaxInvoice_Reason_MentionsTheBlockedMove()
    {
        var reason = TaxInvoiceStateMachine.Reason("Issued", "Draft");
        Assert.Contains("Issued", reason);
        Assert.Contains("Draft", reason);
    }

    [Fact]
    public void TaxInvoice_IsEditable_OnlyDraft()
    {
        Assert.True(TaxInvoiceStatuses.IsEditable("Draft"));
        Assert.False(TaxInvoiceStatuses.IsEditable("Issued"));
        Assert.False(TaxInvoiceStatuses.IsEditable(null));
    }

    // ── CreditNoteStateMachine ──────────────────────────────────────

    [Fact]
    public void CreditNote_Draft_To_Issued_Or_Cancelled()
    {
        Assert.True(CreditNoteStateMachine.CanTransition("Draft", "Issued"));
        Assert.True(CreditNoteStateMachine.CanTransition("Draft", "Cancelled"));
        Assert.False(CreditNoteStateMachine.CanTransition("Draft", "Applied"));
    }

    [Fact]
    public void CreditNote_Issued_Applies_Or_Cancels()
    {
        Assert.True(CreditNoteStateMachine.CanTransition("Issued", "Applied"));
        Assert.True(CreditNoteStateMachine.CanTransition("Issued", "Cancelled"));
        Assert.False(CreditNoteStateMachine.CanTransition("Issued", "Draft"));
    }

    [Fact]
    public void CreditNote_Applied_And_Cancelled_AreTerminal()
    {
        // Applied means "the value already reduced the ledger" — there is
        // no way back.
        Assert.False(CreditNoteStateMachine.CanTransition("Applied", "Draft"));
        Assert.False(CreditNoteStateMachine.CanTransition("Applied", "Issued"));
        Assert.False(CreditNoteStateMachine.CanTransition("Applied", "Cancelled"));
        Assert.True(CreditNoteStateMachine.CanTransition("Applied", "Applied"));
        Assert.False(CreditNoteStateMachine.CanTransition("Cancelled", "Issued"));
        Assert.True(CreditNoteStateMachine.CanTransition("Cancelled", "Cancelled"));
    }

    [Fact]
    public void CreditNote_Reason_MentionsTheBlockedMove()
    {
        var reason = CreditNoteStateMachine.Reason("Applied", "Issued");
        Assert.Contains("Applied", reason);
        Assert.Contains("Issued", reason);
    }

    // ── PaymentTermsResolver ────────────────────────────────────────

    [Theory]
    [InlineData("Immediate", 0)]
    [InlineData("Net 7", 7)]
    [InlineData("Net 15", 15)]
    [InlineData("Net 30", 30)]
    [InlineData("Net 45", 45)]
    [InlineData("Net 60", 60)]
    [InlineData("Net 90", 90)]
    [InlineData("Net 30 days", 30)]          // prefix match
    [InlineData("Something else", 30)]       // unknown → 30
    [InlineData("Custom", 30)]               // Custom → 30
    [InlineData("", 30)]
    [InlineData(null, 30)]
    public void PaymentTerms_Resolve_WpfTable(string? terms, int expectedDays)
    {
        Assert.Equal(expectedDays, PaymentTermsResolver.ResolveDays(terms));
    }

    [Fact]
    public void PaymentTerms_Labels_AreTheWpfPicker()
    {
        Assert.Equal(
            new[] { "Immediate", "Net 7", "Net 15", "Net 30", "Net 45",
                "Net 60", "Net 90" },
            PaymentTermsResolver.Labels);
    }

    // ── InvoiceTotalsCalculator (UAE VAT 5%) ────────────────────────

    [Fact]
    public void Totals_NoDiscount_Vat5Percent()
    {
        // 10,000 → VAT 500 → grand 10,500.
        var t = InvoiceTotalsCalculator.Compute(10_000m, 0m);
        Assert.Equal(0m, t.DiscountAmount);
        Assert.Equal(10_000m, t.AfterDiscount);
        Assert.Equal(500m, t.VatAmount);
        Assert.Equal(10_500m, t.GrandTotal);
    }

    [Fact]
    public void Totals_Discount10Percent_On1000()
    {
        // subtotal 1000, 10%: discount 100, after 900, VAT 45, grand 945.
        var t = InvoiceTotalsCalculator.Compute(1_000m, 10m);
        Assert.Equal(100m, t.DiscountAmount);
        Assert.Equal(900m, t.AfterDiscount);
        Assert.Equal(45m, t.VatAmount);
        Assert.Equal(945m, t.GrandTotal);
    }

    [Fact]
    public void Totals_HalfFil_RoundsAwayFromZero()
    {
        // subtotal 10.05: VAT on 10.05 = 0.5025 → 0.50 exactly; the
        // classic half-fil case is subtotal 10.10 → VAT 0.505 → 0.51
        // (a half-fil does NOT banker's-round down).
        var t = InvoiceTotalsCalculator.Compute(10.10m, 0m);
        Assert.Equal(0.51m, t.VatAmount);
        Assert.Equal(10.61m, t.GrandTotal);

        // And the mirrored case: subtotal 10.30 → VAT 0.515 → 0.52.
        var t2 = InvoiceTotalsCalculator.Compute(10.30m, 0m);
        Assert.Equal(0.52m, t2.VatAmount);
    }

    [Fact]
    public void Totals_DiscountProduceHalfFil_RoundsAway()
    {
        // subtotal 99.99 at 5%: discount 4.9995 → 5.00 (away from zero),
        // after 94.99, VAT 4.7495 → 4.75, grand 99.74.
        var t = InvoiceTotalsCalculator.Compute(99.99m, 5m);
        Assert.Equal(5.00m, t.DiscountAmount);
        Assert.Equal(94.99m, t.AfterDiscount);
        Assert.Equal(4.75m, t.VatAmount);
        Assert.Equal(99.74m, t.GrandTotal);
    }

    [Fact]
    public void Totals_VatRate_Is5Percent_Uae()
    {
        Assert.Equal(0.05m, InvoiceTotalsCalculator.VatRate);
    }

    [Fact]
    public void Totals_MatchMoneyRounding_Policy()
    {
        // The calculator must agree with the shared MoneyRounding port
        // (2dp, midpoint away from zero) on an awkward value.
        var t = InvoiceTotalsCalculator.Compute(333.33m, 3.33m);
        var expectedDiscount = MoneyRounding.Round(333.33m * 3.33m / 100m);
        Assert.Equal(expectedDiscount, t.DiscountAmount);
        var expectedVat = MoneyRounding.Round((333.33m - expectedDiscount) * 0.05m);
        Assert.Equal(expectedVat, t.VatAmount);
        Assert.Equal(t.AfterDiscount + t.VatAmount, t.GrandTotal);
    }

    // ── Numbering (family + revisions + CN) ─────────────────────────

    [Fact]
    public void InvoiceNumbers_Build_PadsTo3()
    {
        Assert.Equal("INV-2026-001", TaxInvoiceNumbers.Build(2026, 1));
        Assert.Equal("INV-2026-042", TaxInvoiceNumbers.Build(2026, 42));
        Assert.Equal("INV-2026-999", TaxInvoiceNumbers.Build(2026, 999));
        Assert.Equal("INV-2026-1000", TaxInvoiceNumbers.Build(2026, 1000));
    }

    [Fact]
    public void InvoiceNumbers_Revision_AppendsSequenceLetter()
    {
        // The SECOND invoice for one PI is the "-A" revision, the third
        // "-B" (the family numbering).
        Assert.Equal("INV-2026-001-A", TaxInvoiceNumbers.Revision("INV-2026-001", 1));
        Assert.Equal("INV-2026-001-B", TaxInvoiceNumbers.Revision("INV-2026-001", 2));
        Assert.Equal("INV-2026-001-Z", TaxInvoiceNumbers.Revision("INV-2026-001", 26));
    }

    [Fact]
    public void InvoiceNumbers_ExtractSeq_ParsesBaseNumber()
    {
        Assert.Equal(7, TaxInvoiceNumbers.ExtractSeq("INV-2026-007"));
        Assert.Equal(42, TaxInvoiceNumbers.ExtractSeq("INV-2026-042-A"));
        Assert.Equal(0, TaxInvoiceNumbers.ExtractSeq("INV-2026"));
        Assert.Equal(0, TaxInvoiceNumbers.ExtractSeq("garbage"));
    }

    [Fact]
    public void CreditNoteNumbers_BuildAndExtract()
    {
        Assert.Equal("CN-2026-001", CreditNoteNumbers.Build(2026, 1));
        Assert.Equal("CN-2026-042", CreditNoteNumbers.Build(2026, 42));
        Assert.Equal(42, CreditNoteNumbers.ExtractSeq("CN-2026-042"));
        Assert.Equal(0, CreditNoteNumbers.ExtractSeq("CN-2026"));
    }

    [Fact]
    public void Numbering_SuggestNext_Increments()
    {
        Assert.Equal(1, TaxInvoiceNumbers.SuggestNext(0));
        Assert.Equal(43, TaxInvoiceNumbers.SuggestNext(42));
        Assert.Equal(1, CreditNoteNumbers.SuggestNext(0));
    }
}
