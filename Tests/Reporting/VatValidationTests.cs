using System;
using ProGlassAutomation.Services.Reporting;
using Xunit;

namespace ProGlassAutomation.Tests.Reporting;

/// <summary>
/// Phase 3 (2026-09-09) — <see cref="VatCalculator.ValidateInvoice"/> could not
/// fail.
///
/// <para>
/// <c>VatValidationResult.IsValid</c> was initialised to <c>true</c> and no code
/// path ever assigned it anything else; <c>Errors</c> was never populated. Every
/// problem the method detected — negative amounts, future dates, arithmetic that
/// did not reconcile — was recorded as a <c>Warning</c> and the invoice passed.
/// </para>
///
/// <para>
/// The consequence was invisible. <c>ReportingDataService.GetVatSubmissionAsync</c>
/// guards on <c>if (!validation.IsValid)</c>, incremented <c>invalidCount</c>, and
/// printed <c>"(filtered from N, 0 invalid)"</c> on every filing. A gate on a tax
/// submission that can never close reads as checked, which is worse than having no
/// gate at all.
/// </para>
///
/// <para>
/// Not one of these tests existed before, which is why the defect survived: the
/// method had coverage for the shapes it accepted and none for the shapes it was
/// supposed to reject.
/// </para>
/// </summary>
public sealed class VatValidationTests
{
    private static readonly DateTime Today = new DateTime(2026, 9, 9);

    // ── Rejections ────────────────────────────────────────────────────

    [Fact]
    public void ValidateInvoice_rejects_when_stored_vat_does_not_reconcile_with_gross()
    {
        // Gross 1050.00 implies VAT of 50.00. Stored 99.00 is 49.00 out —
        // far beyond the one-fils tolerance.
        var result = VatCalculator.ValidateInvoice(1050.00m, 99.00m, Today);

        Assert.False(result.IsValid);
        Assert.True(result.HasErrors);
        Assert.Contains(result.Errors, e => e.Contains("does not reconcile"));
    }

    [Fact]
    public void ValidateInvoice_rejects_when_vat_is_charged_on_a_zero_total()
    {
        var result = VatCalculator.ValidateInvoice(0m, 50.00m, Today);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("zero grand total"));
    }

    [Fact]
    public void ValidateInvoice_accepts_vat_within_one_fil_of_the_expected_value()
    {
        // Gross 1050.00 → expected VAT 50.00. One fil of drift must pass;
        // rounding across many lines legitimately produces this.
        var result = VatCalculator.ValidateInvoice(1050.00m, 50.00m, Today);

        Assert.True(result.IsValid);
        Assert.False(result.HasErrors);
    }

    // ── Warnings that must NOT reject ─────────────────────────────────

    [Fact]
    public void ValidateInvoice_warns_but_accepts_a_positive_total_with_no_vat()
    {
        // Zero-rated and exempt supplies legitimately carry no VAT. This
        // calculator has no notion of tax treatment yet (Phase 5), so
        // rejecting here would silently drop valid invoices from a filing.
        var result = VatCalculator.ValidateInvoice(1000.00m, 0m, Today);

        Assert.True(result.IsValid);
        Assert.True(result.HasWarnings);
        Assert.Contains(result.Warnings, w => w.Contains("no VAT"));
    }

    [Fact]
    public void ValidateInvoice_warns_but_accepts_negative_amounts()
    {
        // Credit notes and corrections are legitimate and must still reach
        // the filing. Reconciling figures is what matters, not the sign.
        var result = VatCalculator.ValidateInvoice(-1050.00m, -50.00m, Today);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("Negative VAT amount"));
        Assert.Contains(result.Warnings, w => w.Contains("Negative grand total"));
    }

    [Fact]
    public void ValidateInvoice_warns_on_a_future_issue_date_but_still_accepts()
    {
        var result = VatCalculator.ValidateInvoice(
            1050.00m, 50.00m, Today.AddDays(30));

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("future"));
    }

    [Fact]
    public void ValidateInvoice_treats_an_all_zero_invoice_as_valid_and_empty()
    {
        var result = VatCalculator.ValidateInvoice(0m, 0m, Today);

        Assert.True(result.IsValid);
        Assert.False(result.HasWarnings);
        Assert.False(result.HasErrors);
    }

    // ── The structural fix ────────────────────────────────────────────

    [Fact]
    public void IsValid_is_derived_from_Errors_so_the_two_cannot_drift()
    {
        // The original defect was a stored bool that no longer described the
        // list beside it. A fresh result with no errors is valid; the moment
        // an error is added it is not, with no separate assignment to forget.
        var fresh = new VatValidationResult();
        Assert.True(fresh.IsValid);

        fresh.Errors.Add("something unreconcilable");
        Assert.False(fresh.IsValid);
        Assert.True(fresh.HasErrors);
    }

    [Fact]
    public void Round_trip_from_subtotal_produces_a_reconciling_invoice()
    {
        // The common path: a subtotal is priced, VAT added, gross stored.
        // That invoice must always validate, for every subtotal in a wide
        // range — if this fails, the validator is rejecting correct data.
        for (decimal subtotal = 0.01m; subtotal < 100000m; subtotal += 137.37m)
        {
            var vat = VatCalculator.CalculateVatFromSubtotal(subtotal);
            var gross = VatCalculator.CalculateGrossFromSubtotal(subtotal);

            var result = VatCalculator.ValidateInvoice(gross, vat, Today);

            Assert.True(
                result.IsValid,
                $"subtotal={subtotal} vat={vat} gross={gross} -> " +
                string.Join(" | ", result.Errors));
        }
    }
}
