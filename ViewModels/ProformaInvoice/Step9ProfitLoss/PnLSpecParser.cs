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
        /// The colour class excludes '+' and digits so a match cannot
        /// swallow the spacer ("+ 12mm ASP") or the next pane's thickness.
        /// </summary>
        public static readonly Regex GlassPattern = new(
            @"(?<thk>\d+)\s*mm\s+(?<color>[^+\d]+?)\s+" + TreatmentAlternation + @"\s+Glass",
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
        /// Outer = first glass match. Inner = first match that starts after
        /// the middle layer (ASP or PVB/SGP/EVA), so a two-pane unit never
        /// double-counts the outer pane.
        /// </summary>
        public static (PnLPane? Outer, PnLPane? Inner) ResolveTwoPanes(string? specName)
        {
            var matches = GlassMatches(specName);
            if (matches.Count == 0) return (null, null);

            var outerMatch = matches[0];
            var outer = FromMatch(outerMatch, "Outer");

            var spacer = Spacer(specName);
            var pvb = Interlayer(specName);

            int after =
                spacer.Success ? spacer.Index + spacer.Length
              : pvb.Success    ? pvb.Index + pvb.Length
              : outerMatch.Index + outerMatch.Length;

            var innerMatch = matches.FirstOrDefault(m => m.Index >= after);
            var inner = innerMatch == null ? null : FromMatch(innerMatch, "Inner");

            return (outer, inner);
        }
    }
}
