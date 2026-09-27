// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   Models/ProformaInvoice/Common/ProformaInvoiceStatus.cs
//   Tests/ProformaInvoice/ProformaIdHardeningTests.cs (golden cases)
// The WPF file is the source of truth; this port changes no behaviour.
// ═══════════════════════════════════════════════════════════════════════

using System.Text.RegularExpressions;

namespace ProGlass.Saas.Domain.Proforma;

/// <summary>
/// The saved-invoice status vocabulary (the Step 1 picker / list filter).
/// NOTE: "Void" is NOT a status — void is a separate flag (IsVoid) on the
/// document, which is why it is absent from <see cref="All"/>.
/// </summary>
public static partial class ProformaStatuses
{
    public const string Draft = "Draft";
    public const string Sent = "Sent";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Expired = "Expired";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All =
        new[] { Draft, Sent, Approved, Rejected, Expired, Cancelled };

    /// <summary>
    /// Wizard placeholder <c>PI-{year}-000</c> (and blank) is not a
    /// persistable number. Insert reallocates before write. Custom /
    /// import IDs such as <c>PI-2026-001</c> or <c>INV-42</c> return false.
    /// </summary>
    [GeneratedRegex(@"^PI-\d{4}-000$", RegexOptions.IgnoreCase)]
    private static partial Regex UnallocatedSeedPattern();

    public static bool IsUnallocatedSeed(string? proformaNo)
    {
        if (string.IsNullOrWhiteSpace(proformaNo)) return true;
        return UnallocatedSeedPattern().IsMatch(proformaNo.Trim());
    }

    /// <summary>
    /// Single normalize home. Canonicalizes any case/spacing variant of a
    /// KNOWN status; an UNRECOGNIZED value passes through as-typed (never
    /// silently re-mapped to Draft — an unexpected status is something to
    /// surface, not to hide).
    /// </summary>
    public static string Normalize(string? value)
    {
        var text = value?.Trim() ?? "";
        foreach (var known in All)
        {
            if (string.Equals(known, text, StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }

        return text;
    }
}
