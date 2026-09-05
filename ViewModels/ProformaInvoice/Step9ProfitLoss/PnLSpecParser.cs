// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/ProformaInvoice/Step9ProfitLoss/PnLSpecParser.cs
// PROJECT: ProGlassAutomation
// MODULE: Wizard Step 9 — spec-name grammar (single source of truth)
//
// Step 4 writes a human spec string. Step 9 has to read it back into
// physical layers. That grammar used to live as private regexes inside
// ProfitLossStepViewModel, with a hand-copied mirror in SpecParsingTests.
// The two drifted once already and silently dropped every DGU inner pane.
//
// This type IS the grammar. The view-model and the tests both call it.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ProGlassAutomation.Utilities;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss
{
    /// <summary>One glass pane recovered from a specification name.</summary>
    public sealed class PnLPane
    {
        public string Side { get; init; } = "";
        public int ThicknessMm { get; init; }
        public string Color { get; init; } = "";
        public int Index { get; init; }
        public int Length { get; init; }
    }

    /// <summary>
    /// Parses a Step 4 / Step 5 specification name into panes, spacer and
    /// interlayer. Pure and deterministic — no Sheet Store, no UI.
    /// </summary>
    public static class PnLSpecParser
    {
        private const string TreatmentAlternation =
            @"(?:FT|Annealed|Heat\s+Strengthened|Heat\s+Soaked)";

        /// <summary>
        /// One glass pane, e.g. "6mm HD Grey FT Glass".
        ///
        /// The colour class is a TEMPERED token: it excludes '+' (so a
        /// match can never cross into the spacer layer — the classic
        /// DGU inner-pane bug) and it refuses the fragment "\d+mm" (so
        /// a colour never swallows the next pane's thickness).
        ///
        /// FIX-2026-09-05 (wastage colour loss): the earlier class
        /// [^+\d] also rejected DIGITS anywhere in the colour, so a
        /// real brand with numbers — "Kibbing Nova 55", "Cool Lite 365",
        /// "Enermax 20" — NEVER matched. That pane silently vanished
        /// from the colour dropdowns, wastage rows and every Step-9
        /// pane split: a "6mm Kibbing Nova 55 FT Glass + 12mm ASP +
        /// 6mm Clear FT Glass" read back as outer=Clear (both panes
        /// Clear). Digits belong to the brand name; only the thickness
        /// fragment is dangerous.
        /// </summary>
        public static readonly Regex GlassPattern = new(
            @"(?<thk>\d+)\s*mm\s+(?<color>(?:(?!\d+\s*mm\b)[^+])+?)\s+" + TreatmentAlternation + @"\s+Glass",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Air spacer. Allows an optional descriptor so
        /// "+ 12mm Black Warm Edge ASP" matches, not only "+ 12mm ASP".
        /// </summary>
        public static readonly Regex SpacerPattern = new(
            @"\+\s*(?<mm>\d+)\s*mm\s+(?:[^+\d]*?\s+)?ASP\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Laminated interlayer — PVB, SGP or EVA.
        /// </summary>
        public static readonly Regex InterlayerPattern = new(
            @"\+\s*(?<mm>[\d\.]+)\s*mm\s+(?<color>[^+\d]+?)\s+(?<type>PVB|SGP|EVA)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static IReadOnlyList<Match> GlassMatches(string? specName) =>
            GlassPattern.Matches(specName ?? "")
                .Cast<Match>()
                .ToList();

        public static Match Spacer(string? specName) =>
            SpacerPattern.Match(specName ?? "");

        public static Match Interlayer(string? specName) =>
            InterlayerPattern.Match(specName ?? "");

        public static PnLPane FromMatch(Match match, string side)
        {
            return new PnLPane
            {
                Side = side,
                ThicknessMm = TextParsing.ParseInt(match.Groups["thk"].Value),
                Color = (match.Groups["color"].Value ?? "").Trim(),
                Index = match.Index,
                Length = match.Length
            };
        }

        public static PnLPane FromParts(string side, int thicknessMm, string? color) =>
            new()
            {
                Side = side,
                ThicknessMm = thicknessMm,
                Color = (color ?? "").Trim()
            };

        /// <summary>
        /// Outer = first glass match. Inner = LAST glass match.
        /// FIX-2026-09-05 (3+ pane builds): the previous rule took the
        /// first pane AFTER THE FIRST middle layer — in a triple-glazed
        /// unit ("… ASP + 6mm B FT Glass + 12mm ASP + 6mm C FT Glass")
        /// that is the MIDDLE pane, silently relabelled "Inner" while
        /// the true inner pane was never read. With ≥2 panes the inner
        /// pane is the last one; with one pane there is no inner.
        /// </summary>
        public static (PnLPane? Outer, PnLPane? Inner) ResolveTwoPanes(string? specName)
        {
            var matches = GlassMatches(specName);
            if (matches.Count == 0) return (null, null);

            var outer = FromMatch(matches[0], "Outer");
            var inner = matches.Count >= 2
                ? FromMatch(matches[^1], "Inner")
                : null;

            return (outer, inner);
        }

        /// <summary>
        /// Full ordered pane list for builds with three or more panes
        /// (e.g. triple-glazed DGU with two spacers). Panes between the
        /// first and last are labelled "Middle" (1-based when several).
        /// Pure and deterministic; two-pane builds return the classic
        /// Outer/Inner pair with an empty middle list.
        /// </summary>
        public static IReadOnlyList<PnLPane> ResolveAllPanes(string? specName)
        {
            var matches = GlassMatches(specName);
            if (matches.Count == 0)
                return [];

            var panes = new List<PnLPane>(matches.Count);
            for (int i = 0; i < matches.Count; i++)
            {
                string side = i == 0
                    ? "Outer"
                    : i == matches.Count - 1
                        ? "Inner"
                        : matches.Count == 3
                            ? "Middle"
                            : $"Middle {i}";
                panes.Add(FromMatch(matches[i], side));
            }

            return panes;
        }
    }
}
