// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   ViewModels/ProformaInvoice/Step7PrintPreview/InvoiceMainSummary/
//     InvoiceMainSummaryViewModel.Recalculate — the financial waterfall
//     (BUG-56 discount cap, BUG-57 batch restore, BUG-58 UAE VAT law)
//   Services/Reporting/InvoiceRoundTo.cs — the round-to-target solver
// The WPF files are the source of truth; behaviour preserved verbatim.
//
//   UAE Federal Decree-Law No. 8 of 2017 on VAT:
//     Article 34: value of supply excludes discounts.
//     Article 3:  standard rate 5% on taxable supplies.
//
//   Waterfall:
//     NetSubtotal        = GlassMaterialsSubtotal + OtherChargesSubtotal
//     DiscountAmount     = Round(net × value/100) or fixed AED,
//                          capped to [0, net] (never a negative subtotal)
//     SubtotalBeforeVat  = NetSubtotal − DiscountAmount
//     VatAmount          = IsVatApplicable ? Round(beforeVat × pct/100) : 0
//     GrandTotal         = Round(beforeVat + VatAmount)
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Money;

namespace ProGlass.Saas.Domain.Proforma;

/// <summary>Discount input (WPF DiscountType enum).</summary>
public enum DiscountKind
{
    /// <summary>Percentage of subtotal (e.g. 5% off).</summary>
    Percentage,

    /// <summary>Flat AED amount off (e.g. AED 500 off).</summary>
    FixedAmount
}

/// <summary>The Step 7 invoice summary, WPF-verbatim.</summary>
public sealed record InvoiceSummaryMath(
    decimal NetSubtotal,
    decimal DiscountAmount,
    decimal SubtotalBeforeVat,
    decimal VatAmount,
    decimal GrandTotalVatInclusive)
{
    /// <summary>
    /// The waterfall. Discount is clamped the WPF way: a fixed amount may
    /// never exceed the net subtotal (UAE VAT Article 34 — the discount
    /// cannot exceed the taxable value of the supply), and VAT applies to
    /// the AFTER-discount value, not gross (FTA tax-invoice guidance).
    /// </summary>
    public static InvoiceSummaryMath Calculate(
        decimal glassMaterialsSubtotal,
        decimal otherChargesSubtotal,
        DiscountKind discountType,
        decimal discountValue,
        bool isVatApplicable,
        decimal vatPercent)
    {
        var net = glassMaterialsSubtotal + otherChargesSubtotal;

        var discount = discountType switch
        {
            DiscountKind.Percentage => MoneyRounding.Round(net * (discountValue / 100m)),
            DiscountKind.FixedAmount => discountValue,
            _ => 0m
        };

        // SINGLE place where the discount is capped at net (BUG-56).
        if (discount > net)
        {
            discount = net;
        }

        if (discount < 0)
        {
            discount = 0;
        }

        var beforeVat = net - discount;

        // VAT on (net − discount), NOT gross (BUG-58).
        var vat = isVatApplicable
            ? MoneyRounding.Round(beforeVat * (vatPercent / 100m))
            : 0m;

        return new InvoiceSummaryMath(
            MoneyRounding.Round(net),
            MoneyRounding.Round(discount),
            MoneyRounding.Round(beforeVat),
            vat,
            MoneyRounding.Round(beforeVat + vat));
    }
}

/// <summary>
/// Round-to-target (WPF InvoiceRoundTo, verbatim): sets an AED discount on
/// the net subtotal so the grand total equals the target. Can only discount
/// DOWN — never raise the total. The cent-stepping loop exists because the
/// VAT rounding makes the closed form approximate.
/// </summary>
public static class InvoiceRoundTo
{
    public static decimal DiscountOnNet(
        decimal netSubtotal, decimal vatPercent, bool vatApplicable, decimal targetGrand)
    {
        netSubtotal = MoneyRounding.Round(netSubtotal);
        targetGrand = MoneyRounding.Round(targetGrand);
        if (netSubtotal <= 0m)
        {
            return 0m;
        }

        if (targetGrand < 0m)
        {
            targetGrand = 0m;
        }

        var maxGrand = GrandFromDiscount(netSubtotal, 0m, vatPercent, vatApplicable);
        if (targetGrand >= maxGrand)
        {
            return 0m;
        }

        var factor = vatApplicable ? 1m + vatPercent / 100m : 1m;
        var beforeVat = factor <= 0m ? 0m : MoneyRounding.Round(targetGrand / factor);
        if (beforeVat < 0m)
        {
            beforeVat = 0m;
        }

        if (beforeVat > netSubtotal)
        {
            beforeVat = netSubtotal;
        }

        var discount = MoneyRounding.Round(netSubtotal - beforeVat);

        for (var i = 0; i < 40; i++)
        {
            var grand = GrandFromDiscount(netSubtotal, discount, vatPercent, vatApplicable);
            if (grand == targetGrand)
            {
                break;
            }

            discount += grand > targetGrand ? 0.01m : -0.01m;
            if (discount < 0m)
            {
                discount = 0m;
                break;
            }

            if (discount > netSubtotal)
            {
                discount = netSubtotal;
                break;
            }
        }

        return MoneyRounding.Round(discount);
    }

    public static decimal GrandFromDiscount(
        decimal netSubtotal, decimal discountAed, decimal vatPercent, bool vatApplicable)
    {
        var after = netSubtotal - discountAed;
        if (after < 0m)
        {
            after = 0m;
        }

        var vat = vatApplicable ? MoneyRounding.Round(after * vatPercent / 100m) : 0m;
        return MoneyRounding.Round(after + vat);
    }
}
