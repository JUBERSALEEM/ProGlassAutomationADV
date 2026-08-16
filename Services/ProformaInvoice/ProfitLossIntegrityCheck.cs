// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/ProformaInvoice/ProfitLossIntegrityCheck.cs
// PROJECT: ProGlassAutomation
//
// DETECTS PROFIT FIGURES PRODUCED BY THE PRE-FIX DGU COSTING.
//
// ─────────────────────────────────────────────────────────────────────
//  THE PROBLEM THIS EXISTS TO SOLVE
// ─────────────────────────────────────────────────────────────────────
//  Step 9's glass-pane regex used a lazy wildcard for the colour, which
//  crossed the '+' separating layers. On
//
//      "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass"
//
//  the second match began inside the SPACER and swallowed the inner pane,
//  so every double-glazed unit was costed on its outer pane alone. The
//  regex is fixed, but the P&L snapshot is written at SAVE time — so
//  every invoice saved before the fix still carries an understated cost
//  and an overstated profit.
//
//  Silently rewriting stored financial history would be worse than the
//  bug. Instead this identifies the affected records so they can be
//  recalculated deliberately, by a person.
//
// ─────────────────────────────────────────────────────────────────────
//  THE DETECTION SIGNATURE
// ─────────────────────────────────────────────────────────────────────
//  A snapshot is suspect when a spec group is clearly a DGU (its spec
//  name contains an air spacer) yet contributed only ONE glass row.
//  Correctly costed DGUs always produce two — outer and inner.
//
//  This is a HEURISTIC over saved text, not a proof. It is deliberately
//  biased toward false negatives: it only flags what it can justify from
//  the stored rows, because telling someone their books are wrong when
//  they are not is its own kind of damage. See ScanSnapshot for the cases
//  it will not flag.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProGlassAutomation.Services.ProformaInvoice
{
    /// <summary>Outcome of checking one saved invoice's P&amp;L snapshot.</summary>
    public sealed class ProfitLossIntegrityResult
    {
        /// <summary>True when this invoice looks like it was costed pre-fix.</summary>
        public bool NeedsRecalculation { get; init; }

        /// <summary>Plain-language reason, empty when nothing is wrong.</summary>
        public string Reason { get; init; } = "";

        /// <summary>Spec names that appear to be missing their inner pane.</summary>
        public IReadOnlyList<string> AffectedSpecs { get; init; } =
            Array.Empty<string>();

        public static ProfitLossIntegrityResult Clean() => new();

        public static ProfitLossIntegrityResult Suspect(
            string reason, IReadOnlyList<string> specs) =>
            new()
            {
                NeedsRecalculation = true,
                Reason = reason,
                AffectedSpecs = specs
            };
    }

    /// <summary>Read-only inspector. Changes nothing, ever.</summary>
    public static class ProfitLossIntegrityCheck
    {
        /// <summary>
        /// A middle layer means the unit has TWO panes: an air spacer (DGU)
        /// or a PVB interlayer (laminated).
        ///
        /// PVB is included because the same regex bug affected laminated
        /// units — the lazy colour wildcard crossed any '+', not just the
        /// one before ASP. Checking only ASP would have left every LAM job
        /// silently mis-costed while reporting the DGUs as handled.
        /// </summary>
        private static readonly Regex TwoPaneMarker = new(
            @"\+\s*[\d\.]+\s*mm\s+(?:[^+\d]*?\s+)?(?:ASP|PVB)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Inspects one saved snapshot. Never throws — a check that can
        /// crash the screen it is warning on is worse than no check.
        /// </summary>
        public static ProfitLossIntegrityResult ScanSnapshot(
            ProfitLossSnapshot? snapshot)
        {
            try
            {
                if (snapshot == null) return ProfitLossIntegrityResult.Clean();

                var rows = snapshot.Rows;
                if (rows == null || rows.Count == 0)
                {
                    // No rows at all is "never costed", which the UI already
                    // reports as "not costed". Not this check's business.
                    return ProfitLossIntegrityResult.Clean();
                }

                // Group glass rows by the spec they were costed against.
                var glassBySpec = new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (var row in rows)
                {
                    if (row == null) continue;

                    if (!string.Equals(row.Kind, "Glass",
                            StringComparison.OrdinalIgnoreCase)) continue;

                    string spec = (row.SpecName ?? "").Trim();

                    // A row with no spec name predates SpecName being stored.
                    // Cannot be judged, so it is not accused.
                    if (spec.Length == 0) continue;

                    glassBySpec.TryGetValue(spec, out int n);
                    glassBySpec[spec] = n + 1;
                }

                var affected = glassBySpec
                    .Where(kv => TwoPaneMarker.IsMatch(kv.Key) && kv.Value < 2)
                    .Select(kv => kv.Key)
                    .ToList();

                if (affected.Count == 0)
                    return ProfitLossIntegrityResult.Clean();

                return ProfitLossIntegrityResult.Suspect(
                    $"{affected.Count} two-pane spec(s) — double-glazed or " +
                    "laminated — were costed with only one glass pane. The " +
                    "inner pane is missing, so the " +
                    "cost is understated and the profit overstated. Open this " +
                    "invoice, recalculate Step 9, and save.",
                    affected);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfitLossIntegrityCheck] {ex.Message}");

                // Fail quiet: an inspector that cannot decide must not
                // accuse. A false "your books are wrong" has its own cost.
                return ProfitLossIntegrityResult.Clean();
            }
        }
    }
}
