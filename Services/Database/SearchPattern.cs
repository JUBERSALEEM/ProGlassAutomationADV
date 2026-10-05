// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Database/SearchPattern.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Services.Database
//
// PURPOSE:
//   One home for the rule "turn a user's search term into a SQLite LIKE
//   pattern that means: contains this text, literally, ignoring case".
//
// WHY IT EXISTS:
//   Added 2026-09-08 while pushing the global-search box's filters into SQL
//   (Phase 2, category D). Two repositories already did this
//   (ProformaInvoiceRepository.SearchAsync, JobOrderRepository.SearchAsync)
//   and each escaped wildcards with SQL Server's bracket syntax, [%] and [_].
//
//   SQLite does not implement that. In SQLite a bracket is an ordinary
//   character, so LIKE '%[%]%' means "a [, then anything, then a ]" — the %
//   inside the brackets is still a wildcard. A term containing % therefore
//   matches the wrong rows, and a term containing _ (very common in invoice
//   and job numbers, e.g. PI_2026_001) matches any single character instead of
//   an underscore.
//
//   The correct SQLite form passes an explicit escape character, which
//   EF.Functions.Like supports via its three-argument overload and the SQLite
//   provider renders as `LIKE @p ESCAPE '\'`.
//
//   Those two existing searches were NOT changed. Rewriting them alters two
//   features that work today, and without a compiler in this environment that
//   is a trade worth making deliberately rather than in passing. Recorded in
//   docs/REMEDIATION_PLAN.md.
//
// KNOWN DEVIATION (both here and in the two existing searches):
//   SQLite's LIKE folds case for ASCII only. A non-ASCII term matches
//   case-sensitively, where the in-memory IndexOf(OrdinalIgnoreCase) these
//   replace did not.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Services.Database
{
    public static class SearchPattern
    {
        /// <summary>
        /// The escape character passed to <c>EF.Functions.Like</c>. Declared
        /// as a <c>const</c> so the compiler inlines it into the expression
        /// tree rather than leaving a captured variable for EF to
        /// parameterise.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A <c>string</c>, not a <c>char</c>, because that is the type the
        /// overload takes: <c>Like(this DbFunctions, string matchExpression,
        /// string pattern, string? escapeCharacter)</c>. It was declared
        /// <c>const char</c> until 2026-09-09, which failed to compile at all
        /// eight call sites with CS1503 — and note the error names <i>argument
        /// 4</i>, not 3, because the implicit <c>this DbFunctions</c> receiver
        /// of the extension method counts as the first argument.
        /// </para>
        /// <para>
        /// Single backslash, matching what <see cref="Contains"/> escapes with.
        /// </para>
        /// </remarks>
        public const string EscapeChar = "\\";

        /// <summary>
        /// Builds a <c>%…%</c> pattern matching rows that contain
        /// <paramref name="term"/> literally, with the three characters that
        /// LIKE treats as metacharacters escaped.
        ///
        /// Returns an empty string for a blank term so callers can short-circuit
        /// without a database round trip.
        /// </summary>
        public static string Contains(string? term)
        {
            var q = (term ?? "").Trim();
            if (q.Length == 0) return string.Empty;

            var escaped = q
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");

            return "%" + escaped + "%";
        }

        /// <summary>True when <see cref="Contains"/> produced a usable pattern.</summary>
        public static bool IsUsable(string? term) =>
            !string.IsNullOrWhiteSpace(term);
    }
}
