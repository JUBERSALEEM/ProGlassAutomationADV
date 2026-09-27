// ─────────────────────────────────────────────────────────────────────────────
// ProGlass SaaS — Tax Invoice print snapshot (Phase 17)
//
// WPF source of truth:
//   Models/TaxInvoice/SavedTaxInvoice/TaxInvoiceSnapshot.cs (the shape),
//   Services/TaxInvoice/Mapper/TaxInvoiceMapper.cs (BuildSnapshot +
//   SetHeaderStatus + AmountToWords — the population rules).
//
// The shape is kept VERBATIM (section names, property names, defaults) so a
// snapshot written by the SaaS API and a WPF FullSnapshotJson imported by the
// migration tool are the same document. The WPF serializes with default
// System.Text.Json options (PascalCase, no naming policy) — the SaaS does the
// same, and reads case-insensitively like the WPF mapper.
//
// Documented differences (mapping §1.11):
//   • SpecGroups is always empty — the SaaS tax invoice is a totals document
//     (no invoice line items yet); the item rows live on the linked proforma
//     and delivery notes. The section exists for shape compatibility.
//   • Company logo fields (LogoBase64/LogoPath/LogoLayout/widths) and the
//     PreparedBy signature are part of the shape with their WPF defaults;
//     the SaaS has no logo/signature store yet (PDF/print deferred).
//   • Attachments is always empty — the attachments module is deferred
//     (object storage).
//   • Header.ProformaId / Header.JobOrderId carry the linked document's
//     NUMBER (the WPF stores its PI/JO identifiers as strings there).
// ─────────────────────────────────────────────────────────────────────────────

using System.Text.Json;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Domain.Fiscal;

/// <summary>The immutable print snapshot stored in tax_invoices.snapshot_json
/// (jsonb). SchemaVersion 1, like the WPF.</summary>
public sealed class TaxInvoiceSnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public InvHeaderSnapshot Header { get; set; } = new();
    public InvCompanySnapshot Company { get; set; } = new();
    public InvCustomerSnapshot Customer { get; set; } = new();
    public InvProjectSnapshot Project { get; set; } = new();
    public List<InvSpecGroupSnapshot> SpecGroups { get; set; } = new();
    public InvFinancialSnapshot Financial { get; set; } = new();
    public InvBankSnapshot Bank { get; set; } = new();
    public InvPreparedBySnapshot PreparedBy { get; set; } = new();
    public List<InvAttachmentSnapshot> Attachments { get; set; } = new();
}

public sealed class InvHeaderSnapshot
{
    public string InvoiceNumber { get; set; } = "";
    public string ProformaId { get; set; } = "";
    public string DeliveryNoteReference { get; set; } = "";
    public string JobOrderId { get; set; } = "";
    public string PurchaseOrderNumber { get; set; } = "";
    public string InvoiceType { get; set; } = "Full";
    public string SequenceLetter { get; set; } = "";
    public int SequenceNumber { get; set; } = 1;
    public string Status { get; set; } = "Draft";
    public DateTime IssueDate { get; set; } = default;
    public DateTime DueDate { get; set; } = default;
}

public sealed class InvCompanySnapshot
{
    public string CompanyName { get; set; } = "";
    public string CompanyTrn { get; set; } = "";
    public string CompanyAddress { get; set; } = "";
    public string CompanyLocation { get; set; } = "";
    public string CompanyPhone { get; set; } = "";
    public string CompanyEmail { get; set; } = "";
    public string LogoBase64 { get; set; } = "";
    public string LogoPath { get; set; } = "";
    public CompanyLogoLayout LogoLayout { get; set; } = CompanyLogoLayout.None;
    public double LogoWidthMm { get; set; } = 22;
    public double LogoHeightMm { get; set; } = 26;
}

public sealed class InvCustomerSnapshot
{
    public string CustomerName { get; set; } = "";
    public string CustomerTrn { get; set; } = "";
    public string CustomerAddress { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string ContactPerson { get; set; } = "";
}

public sealed class InvProjectSnapshot
{
    public string ProjectIdentifierName { get; set; } = "";
    public string ProjectContractNo { get; set; } = "";
    public string ProjectReference { get; set; } = "";
    public string SiteDeliveryLocation { get; set; } = "";
}

public sealed class InvSpecGroupSnapshot
{
    public int GroupNumber { get; set; }
    public string GroupName { get; set; } = "";
    public string GlassType { get; set; } = "";
    public string SpecName { get; set; } = "";
    public decimal BaseRate { get; set; }
    public List<InvGlassItemRowSnapshot> Rows { get; set; } = new();
    public int GroupTotalQty { get; set; }
    public decimal GroupTotalSqm { get; set; }
    public decimal GroupTotalValue { get; set; }
}

public sealed class InvGlassItemRowSnapshot
{
    public int SerialNumber { get; set; }
    public string GlassRef { get; set; } = "";
    public int W1 { get; set; }
    public int H1 { get; set; }
    public int W2 { get; set; }
    public int H2 { get; set; }
    public int Qty { get; set; }
    public decimal Sqm { get; set; }
    public decimal TotalSqm { get; set; }
    public decimal Rate { get; set; }
    public decimal Value { get; set; }
    public string Notes { get; set; } = "";
}

public sealed class InvFinancialSnapshot
{
    public decimal Subtotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal AfterDiscount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string GrandTotalInWords { get; set; } = "";
    public decimal PreviouslyInvoicedTotal { get; set; }
    public decimal RemainingBalance { get; set; }
}

public sealed class InvBankSnapshot
{
    public string BankName { get; set; } = "";
    public string BankAccountNumber { get; set; } = "";
    public string Iban { get; set; } = "";
    public string SwiftCode { get; set; } = "";
    public string PaymentTerms { get; set; } = "Net 30";
    public int PaymentTermsDays { get; set; } = 30;
    public string PaymentNotes { get; set; } = "";
}

public sealed class InvPreparedBySnapshot
{
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public string Department { get; set; } = "";
    public string SignatureBase64 { get; set; } = "";
}

public sealed class InvAttachmentSnapshot
{
    public string AttachmentType { get; set; } = "Other";
    public string FileName { get; set; } = "";
    public string FileBase64 { get; set; } = "";
    public long FileSize { get; set; }
    public string Description { get; set; } = "";
    public DateTime UploadedAt { get; set; } = default;
}

/// <summary>WPF Models/MasterData/CompanyLogoLayout.cs — serialized as the
/// underlying number by System.Text.Json, same as the WPF.</summary>
public enum CompanyLogoLayout
{
    None = 0,
    LeftPortrait = 1,
    TopPanel = 2,
}

/// <summary>Snapshot (de)serialization + population rules — the WPF
/// TaxInvoiceMapper paths A/B/C, translated to the SaaS entity.</summary>
public static class TaxInvoiceSnapshotCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>WPF TaxInvoiceMapper.DeserializeSnapshot: null when empty or
    /// malformed — callers handle null defensively.</summary>
    public static TaxInvoiceSnapshot? Deserialize(string? snapshotJson)
    {
        if (string.IsNullOrWhiteSpace(snapshotJson) || snapshotJson == "{}")
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TaxInvoiceSnapshot>(
                snapshotJson, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string Serialize(TaxInvoiceSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, Options);

    /// <summary>WPF SetHeaderStatus: write the new status onto the row AND
    /// the snapshot header so a later load cannot resurrect the old one.
    /// Returns the re-serialized JSON, or null when there is no snapshot to
    /// update (the WPF returns without touching the field).</summary>
    public static string? WithHeaderStatus(string? snapshotJson, string status)
    {
        var snapshot = Deserialize(snapshotJson);
        if (snapshot is null)
        {
            return null;
        }

        snapshot.Header.Status = status;
        return Serialize(snapshot);
    }

    /// <summary>WPF AmountToWords — verbatim ("AED Zero Only" floor, fils
    /// only when positive, "Only" suffix).</summary>
    public static string AmountToWords(decimal amount)
    {
        if (amount <= 0m) return "AED Zero Only";

        long dirhams = (long)Math.Truncate(amount);
        int fils = (int)Math.Round((amount - dirhams) * 100);

        string result = $"AED {NumberToWords(dirhams)} Dirhams";

        if (fils > 0)
            result += $" and {NumberToWords(fils)} Fils";

        return result + " Only";
    }

    // WPF NumberToWords — verbatim.
    private static string NumberToWords(long number)
    {
        if (number == 0) return "Zero";
        if (number < 0) return "Minus " + NumberToWords(-number);

        string[] ones = {
            "", "One", "Two", "Three", "Four", "Five", "Six", "Seven",
            "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen",
            "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen",
            "Nineteen"
        };
        string[] tens = {
            "", "", "Twenty", "Thirty", "Forty", "Fifty",
            "Sixty", "Seventy", "Eighty", "Ninety"
        };

        if (number < 20) return ones[number];
        if (number < 100)
            return tens[number / 10] +
                   (number % 10 > 0 ? " " + ones[number % 10] : "");
        if (number < 1_000)
            return ones[number / 100] + " Hundred" +
                   (number % 100 > 0 ? " " + NumberToWords(number % 100) : "");
        if (number < 1_000_000)
            return NumberToWords(number / 1_000) + " Thousand" +
                   (number % 1_000 > 0
                       ? " " + NumberToWords(number % 1_000) : "");
        if (number < 1_000_000_000)
            return NumberToWords(number / 1_000_000) + " Million" +
                   (number % 1_000_000 > 0
                       ? " " + NumberToWords(number % 1_000_000) : "");

        return NumberToWords(number / 1_000_000_000) + " Billion" +
               (number % 1_000_000_000 > 0
                   ? " " + NumberToWords(number % 1_000_000_000) : "");
    }
}
