// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   Models/TaxInvoice/SavedTaxInvoice/SavedTaxInvoice.cs (status vocab,
//     the fiscal lock: once Issued never edited, only voided or
//     credited; family numbering INV-2026-001 / -A revisions).
//   Services/Workflow/StatusTransitions.cs (the TaxInvoice and
//     CreditNote tables, verbatim — self-transitions allowed, terminal
//     states enforced).
//   Services/TaxInvoice/PaymentTermsResolver.cs (Immediate 0, Net
//     7/15/30/45/60/90, unknown → 30).
//   Services/TaxInvoice/InvoiceTotalsCalculator.cs (the UAE VAT rule:
//     DiscountAmount = round(Subtotal × %/100); VatAmount =
//     round(AfterDiscount × 5%); GrandTotal = AfterDiscount + Vat —
//     MoneyRounding 2dp midpoint away from zero so a half-fil never
//     banker's-rounds down and disagrees with the line values).
//   Services/CreditNote (CN-{year}-{seq} numbering; a credit note never
//     exists on its own — it always corrects a Tax Invoice).
// The WPF files are the source of truth; behaviour preserved verbatim.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Money;

namespace ProGlass.Saas.Domain.Proforma;

/// <summary>Tax Invoice status values (WPF SavedTaxInvoice).</summary>
public static class TaxInvoiceStatuses
{
    public const string Draft = "Draft";
    public const string Issued = "Issued";
    public const string Paid = "Paid";
    public const string PartiallyPaid = "PartiallyPaid";
    public const string Overdue = "Overdue";
    public const string Cancelled = "Cancelled";
    public const string Void = "Void";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Draft, Issued, Paid, PartiallyPaid, Overdue, Cancelled, Void
    };

    /// <summary>States in which the fiscal figures may still be edited
    /// (a Tax Invoice is locked once Issued).</summary>
    public static bool IsEditable(string? status) =>
        string.Equals(status, Draft, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Credit Note status values (WPF).</summary>
public static class CreditNoteStatuses
{
    public const string Draft = "Draft";
    public const string Issued = "Issued";
    public const string Applied = "Applied";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Draft, Issued, Applied, Cancelled
    };
}

/// <summary>The Tax Invoice status machine (WPF StatusTransitions,
/// Kind.TaxInvoice — verbatim; self-transitions allowed).</summary>
public static class TaxInvoiceStateMachine
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Table =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [TaxInvoiceStatuses.Draft] = new[]
            {
                TaxInvoiceStatuses.Draft, TaxInvoiceStatuses.Issued,
                TaxInvoiceStatuses.Cancelled, TaxInvoiceStatuses.Void
            },
            [TaxInvoiceStatuses.Issued] = new[]
            {
                TaxInvoiceStatuses.Issued, TaxInvoiceStatuses.Paid,
                TaxInvoiceStatuses.PartiallyPaid, TaxInvoiceStatuses.Overdue,
                TaxInvoiceStatuses.Cancelled, TaxInvoiceStatuses.Void
            },
            [TaxInvoiceStatuses.PartiallyPaid] = new[]
            {
                TaxInvoiceStatuses.PartiallyPaid, TaxInvoiceStatuses.Paid,
                TaxInvoiceStatuses.Overdue, TaxInvoiceStatuses.Cancelled,
                TaxInvoiceStatuses.Void
            },
            [TaxInvoiceStatuses.Paid] = new[] { TaxInvoiceStatuses.Paid },
            [TaxInvoiceStatuses.Overdue] = new[]
            {
                TaxInvoiceStatuses.Overdue, TaxInvoiceStatuses.Paid,
                TaxInvoiceStatuses.PartiallyPaid, TaxInvoiceStatuses.Cancelled,
                TaxInvoiceStatuses.Void
            },
            [TaxInvoiceStatuses.Cancelled] = new[] { TaxInvoiceStatuses.Cancelled },
            [TaxInvoiceStatuses.Void] = new[] { TaxInvoiceStatuses.Void }
        };

    public static bool CanTransition(string? current, string next)
    {
        if (!Table.TryGetValue((current ?? "").Trim(), out var allowed))
        {
            return false;
        }

        return allowed.Contains(next.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public static string Reason(string? current, string next) =>
        $"Status change from '{(current ?? "").Trim()}' to '{next.Trim()}' is not allowed.";
}

/// <summary>The Credit Note status machine (WPF StatusTransitions,
/// Kind.CreditNote — verbatim: a fiscal correction; Draft is editable;
/// Issued is committed; Applied is terminal (its value reduced the
/// ledger); Cancelled is the void target).</summary>
public static class CreditNoteStateMachine
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Table =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [CreditNoteStatuses.Draft] = new[]
            {
                CreditNoteStatuses.Draft, CreditNoteStatuses.Issued,
                CreditNoteStatuses.Cancelled
            },
            [CreditNoteStatuses.Issued] = new[]
            {
                CreditNoteStatuses.Issued, CreditNoteStatuses.Applied,
                CreditNoteStatuses.Cancelled
            },
            [CreditNoteStatuses.Applied] = new[] { CreditNoteStatuses.Applied },
            [CreditNoteStatuses.Cancelled] = new[] { CreditNoteStatuses.Cancelled }
        };

    public static bool CanTransition(string? current, string next)
    {
        if (!Table.TryGetValue((current ?? "").Trim(), out var allowed))
        {
            return false;
        }

        return allowed.Contains(next.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public static string Reason(string? current, string next) =>
        $"Status change from '{(current ?? "").Trim()}' to '{next.Trim()}' is not allowed.";
}

/// <summary>
/// Maps payment-terms display labels to the days used to compute the
/// due date (WPF PaymentTermsResolver, verbatim; unknown → 30).
/// </summary>
public static class PaymentTermsResolver
{
    public static int ResolveDays(string? terms) => (terms ?? "").Trim() switch
    {
        "Immediate" => 0,
        "Net 7" => 7,
        "Net 15" => 15,
        "Net 30" => 30,
        "Net 45" => 45,
        "Net 60" => 60,
        "Net 90" => 90,
        _ => 30
    };

    public static readonly IReadOnlyList<string> Labels = new[]
    {
        "Immediate", "Net 7", "Net 15", "Net 30", "Net 45", "Net 60", "Net 90"
    };
}

/// <summary>
/// Pure financial-total computation for tax invoices (WPF
/// InvoiceTotalsCalculator — the single source of truth for the UAE VAT
/// rule).
/// </summary>
public static class InvoiceTotalsCalculator
{
    /// <summary>UAE standard VAT rate (5%).</summary>
    public const decimal VatRate = 0.05m;

    public readonly record struct Totals(
        decimal DiscountAmount,
        decimal AfterDiscount,
        decimal VatAmount,
        decimal GrandTotal);

    public static Totals Compute(decimal subtotal, decimal discountPercent)
    {
        var discountAmt = MoneyRounding.Round(
            subtotal * (discountPercent / 100m));
        var afterDiscount = subtotal - discountAmt;
        var vatAmount = MoneyRounding.Round(afterDiscount * VatRate);
        var grandTotal = afterDiscount + vatAmount;

        return new Totals(discountAmt, afterDiscount, vatAmount, grandTotal);
    }
}

/// <summary>
/// Tax Invoice numbering (WPF INV-{year}-{seq:D3}; all invoices for one
/// PI share the family — the second invoice for a PI is a revision with
/// a sequence letter: INV-2026-001-A). Credit notes: CN-{year}-{seq:D3}.
/// </summary>
public static class TaxInvoiceNumbers
{
    public static string Build(int year, int seq) => $"INV-{year}-{seq:D3}";

    public static string Revision(string baseNumber, int letterIndex) =>
        letterIndex <= 0 ? baseNumber : $"{baseNumber}-{(char)('A' + letterIndex - 1)}";

    public static int SuggestNext(int current) => current + 1;

    /// <summary>Extracts the sequence from INV-2026-0042[-A] → 42.</summary>
    public static int ExtractSeq(string invoiceNo)
    {
        var parts = (invoiceNo ?? "").Split('-');
        return parts.Length >= 3 && int.TryParse(parts[2], out var n) ? n : 0;
    }
}

public static class CreditNoteNumbers
{
    public static string Build(int year, int seq) => $"CN-{year}-{seq:D3}";

    public static int SuggestNext(int current) => current + 1;

    public static int ExtractSeq(string creditNoteNo)
    {
        var parts = (creditNoteNo ?? "").Split('-');
        return parts.Length >= 3 && int.TryParse(parts[2], out var n) ? n : 0;
    }
}
