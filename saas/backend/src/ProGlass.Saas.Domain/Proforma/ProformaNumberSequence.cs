// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   Services/ProformaInvoice/ProformaInvoiceRepository/
//     ProformaInvoiceRepository.cs — SuggestNextIdAsync (L116–177)
// The WPF file is the source of truth. All the hardening survives:
//   BUG-08  single batch query (the caller supplies the existing ids)
//   BUG-09  GUID fallback — never collides
//   BUG-11  regex with named groups + non-greedy prefix
//   V-1     int.TryParse degradation — "PI-99999999999" restarts numbering
//           instead of crashing with OverflowException
// Only the shape changed: a pure function over the existing-id set
// instead of a repository method that opens its own DbContext.
// ═══════════════════════════════════════════════════════════════════════

using System.Text.RegularExpressions;

namespace ProGlass.Saas.Domain.Proforma;

/// <summary>
/// PI-{year}-NNN allocation, WPF-verbatim. Examples:
///   "PI-2026-001" → "PI-2026-002" (or 003, 004... if those also exist)
///   "INVOICE-42"  → "INVOICE-43"
///   "ABC"         → "ABC-001"
/// </summary>
public static partial class ProformaNumberSequence
{
    /// <summary>WPF _idPattern: non-greedy prefix, trailing digit run.</summary>
    [GeneratedRegex(@"^(?<prefix>.*?)(?<num>\d+)$")]
    private static partial Regex IdPattern();

    /// <summary>
    /// The wizard's unallocated seed every new invoice starts from.
    /// Insert reallocates before write (see ProformaStatuses.IsUnallocatedSeed).
    /// </summary>
    public static string SeedFor(int year) => $"PI-{year}-000";

    /// <summary>
    /// Next free id after <paramref name="currentId"/>, given the ids that
    /// already exist (case-insensitive). Never queries the database — the
    /// caller supplies the prefix-matching id set (BUG-08: one query).
    /// </summary>
    /// <param name="existingIds">All stored numbers starting with the
    /// candidate prefix (the caller loads them once per allocation).</param>
    public static string SuggestNext(
        string? currentId, IReadOnlyCollection<string> existingIds)
    {
        if (string.IsNullOrWhiteSpace(currentId))
            currentId = "PI-000";

        var match = IdPattern().Match(currentId);

        string prefix;
        int currentNumber;
        int padWidth;

        if (match.Success)
        {
            prefix = match.Groups["prefix"].Value;
            var numText = match.Groups["num"].Value;

            // AUDIT V-1: \d+ is unbounded, so a legal id such as
            // "PI-99999999999" must degrade to a restart, not crash with
            // OverflowException (the original bug made invoice creation
            // silently impossible).
            if (!int.TryParse(numText, out currentNumber))
                currentNumber = 0;

            padWidth = numText.Length;
        }
        else
        {
            prefix = currentId + "-";
            currentNumber = 0;
            padWidth = 3;
        }

        var existingSet = new HashSet<string>(existingIds, StringComparer.OrdinalIgnoreCase);

        // Find next unused number by simple in-memory loop
        for (var i = 1; i <= 100_000; i++)
        {
            var candidate =
                $"{prefix}{(currentNumber + i).ToString().PadLeft(padWidth, '0')}";
            if (!existingSet.Contains(candidate))
                return candidate;
        }

        // BUG-09: GUID fallback — never collides. WPF caps the column at
        // 50 chars (prefix + 32 hex).
        return $"{prefix}{Guid.NewGuid():N}".Substring(0, Math.Min(50,
            prefix.Length + 32));
    }
}
