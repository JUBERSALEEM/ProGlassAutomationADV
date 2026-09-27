// ═══════════════════════════════════════════════════════════════════════
// GOLDEN VALUES — the Step 7 invoice summary, pinned to the WPF sources
// (read 2026-09-27):
//   ViewModels/…/Step7PrintPreview/InvoiceMainSummary/
//     InvoiceMainSummaryViewModel.Recalculate (BUG-56 discount cap,
//     BUG-57 batch restore, BUG-58 UAE VAT law)
//   Services/Reporting/InvoiceRoundTo.cs (the round-to-target solver)
// Expected numbers computed independently with Python decimal
// (devbox/derive_step9_goldens.py) — never re-derived from the port.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Proforma;
using Xunit;

namespace ProGlass.Saas.Domain.Tests;

public class Step7SummaryTests
{
    // ── the waterfall ──────────────────────────────────────────────────

    [Fact]
    public void Percentage_discount_then_vat_on_the_discounted_value()
    {
        // net 1200, 5% discount, VAT 5% applicable.
        var s = InvoiceSummaryMath.Calculate(
            glassMaterialsSubtotal: 1000m, otherChargesSubtotal: 200m,
            DiscountKind.Percentage, 5m, isVatApplicable: true, vatPercent: 5m);

        Assert.Equal(1200m, s.NetSubtotal);
        Assert.Equal(60.00m, s.DiscountAmount);
        Assert.Equal(1140m, s.SubtotalBeforeVat);
        Assert.Equal(57.00m, s.VatAmount);      // VAT on 1140, not 1200
        Assert.Equal(1197.00m, s.GrandTotalVatInclusive);
    }

    [Fact]
    public void A_fixed_discount_can_never_make_the_subtotal_negative()
    {
        // BUG-56: AED 1500 off a 1200 invoice clamps to 1200.
        var s = InvoiceSummaryMath.Calculate(
            1000m, 200m, DiscountKind.FixedAmount, 1500m, true, 5m);

        Assert.Equal(1200m, s.DiscountAmount);
        Assert.Equal(0m, s.SubtotalBeforeVat);
        Assert.Equal(0m, s.VatAmount);
        Assert.Equal(0m, s.GrandTotalVatInclusive);
    }

    [Fact]
    public void A_negative_discount_is_clamped_to_zero()
    {
        var s = InvoiceSummaryMath.Calculate(
            1000m, 200m, DiscountKind.FixedAmount, -50m, true, 5m);

        Assert.Equal(0m, s.DiscountAmount);
        Assert.Equal(1200m, s.GrandTotalVatInclusive);
    }

    [Fact]
    public void Vat_off_means_the_grand_total_is_the_discounted_net()
    {
        var s = InvoiceSummaryMath.Calculate(
            1000m, 200m, DiscountKind.Percentage, 5m, false, 5m);

        Assert.Equal(0m, s.VatAmount);
        Assert.Equal(1140m, s.GrandTotalVatInclusive);
    }

    // ── the round-to-target solver ────────────────────────────────────

    [Fact]
    public void Round_to_target_solves_the_vat_inverse_exactly()
    {
        // 1200 net, 5% VAT, target grand 1000:
        // beforeVat = Round(1000 / 1.05) = 952.38 → discount 247.62
        // grand = Round(952.38 + Round(952.38 × 5%)) = 1000.00
        var d = InvoiceRoundTo.DiscountOnNet(1200m, 5m, true, 1000m);

        Assert.Equal(247.62m, d);
        Assert.Equal(1000.00m, InvoiceRoundTo.GrandFromDiscount(1200m, d, 5m, true));
    }

    [Fact]
    public void Round_to_target_above_the_maximum_returns_no_discount()
    {
        // No discount: grand = Round(1200 × 1.05) = 1260. A target at or
        // above it needs no discount — and the solver can only go down.
        Assert.Equal(0m, InvoiceRoundTo.DiscountOnNet(1200m, 5m, true, 1260m));
        Assert.Equal(0m, InvoiceRoundTo.DiscountOnNet(1200m, 5m, true, 1300m));
    }

    [Fact]
    public void Round_to_zero_targets_the_full_net_as_discount()
    {
        Assert.Equal(1200.00m, InvoiceRoundTo.DiscountOnNet(1200m, 5m, true, 0m));
    }

    [Fact]
    public void Round_to_target_on_a_zero_net_is_no_discount()
    {
        Assert.Equal(0m, InvoiceRoundTo.DiscountOnNet(0m, 5m, true, 500m));
    }

    [Fact]
    public void Grand_from_discount_never_goes_negative()
    {
        // A discount larger than the net clamps the after-value at zero.
        Assert.Equal(0m, InvoiceRoundTo.GrandFromDiscount(100m, 500m, 5m, true));
        Assert.Equal(105.00m, InvoiceRoundTo.GrandFromDiscount(100m, 0m, 5m, true));
    }
}
