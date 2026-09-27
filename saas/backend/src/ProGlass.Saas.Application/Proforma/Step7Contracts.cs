using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Proforma;

// ═══════════════════════════════════════════════════════════════════════
//  Step 7 — the print-preview financial summary.
//
//  The WPF recalculated this live in the wizard; the SaaS persists the
//  four operator inputs (the WPF mapper's DiscountType / DiscountValue /
//  IsVatApplicable / VatPercent) and recomputes the amounts server-side
//  with InvoiceSummaryMath — the port of
//  InvoiceMainSummaryViewModel.Recalculate (BUG-56/57/58).
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// PUT body: the four operator inputs. Every field is optional — null
/// keeps the saved value (WPF defaults on a first save: Percentage, 0,
/// VAT applicable, the tenant's configured VAT rate or the WPF's 5%).
/// </summary>
public sealed record SaveStep7Request(
    string? DiscountType,
    decimal? DiscountValue,
    bool? IsVatApplicable,
    decimal? VatPercent);

/// <summary>
/// ROUND-TO body: the target grand total. The solver sets a FIXED-AMOUNT
/// discount (the WPF tool switches the discount kind) and can only
/// discount DOWN — a target above the no-discount grand is a no-op.
/// </summary>
public sealed record RoundToStep7Request(decimal TargetGrandTotal);

/// <summary>The Step 7 summary — inputs + the recomputed waterfall.</summary>
public sealed record Step7Dto(
    string DiscountType,
    decimal DiscountValue,
    bool IsVatApplicable,
    decimal VatPercent,
    decimal GlassMaterialsSubtotal,
    decimal OtherChargesSubtotal,
    decimal NetSubtotal,
    decimal DiscountAmount,
    decimal SubtotalBeforeVat,
    decimal VatAmount,
    decimal GrandTotalVatInclusive);

/// <summary>The round-to result: the solved discount + new summary.</summary>
public sealed record RoundToStep7Dto(
    decimal SolvedDiscount,
    bool Changed,
    Step7Dto Summary);
