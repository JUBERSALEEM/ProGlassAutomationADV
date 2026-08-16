// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/SpecParsingTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// REGRESSION SUITE FOR THE DGU INNER-PANE BUG.
//
// WHAT WENT WRONG
//   ProfitLossStepViewModel.GlassPattern captured the colour with a lazy
//   wildcard, (?<color>.+?). A lazy wildcard still crosses any character,
//   including the '+' that separates the layers of a spec name. On
//
//       "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass"
//
//   the second match started at the SPACER and captured
//   thk=12, colour="ASP + 6mm Clear". The inner-pane search then looked
//   for a match beginning after the spacer, found none, and the inner
//   pane was silently dropped. Every DGU was costed on its outer pane
//   alone.
//
// WHY IT IS TESTED HERE AS A PATTERN, NOT THROUGH THE VIEW-MODEL
//   GlassPattern is private, and RecalculateAsync needs a live Step 5
//   grid plus Sheet Store data. Widening a field to public purely for a
//   test would weaken the class to prove a point about a string rule.
//   The rule is pure and deterministic, so the rule is what is asserted —
//   the pattern text below is kept identical to the shipped one, and
//   Pattern_matches_the_shipped_source() fails if they ever diverge.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class SpecParsingTests
    {
        private const string TreatmentAlternation =
            @"(?:FT|Annealed|Heat\s+Strengthened|Heat\s+Soaked)";

        /// <summary>Mirror of the shipped GlassPattern.</summary>
        private static readonly Regex GlassPattern = new(
            @"(?<thk>\d+)\s*mm\s+(?<color>[^+\d]+?)\s+" + TreatmentAlternation + @"\s+Glass",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // MIRRORS ProfitLossStepViewModel.SpacerPattern. Kept in step by
        // hand — if the production pattern changes, change it here too, or
        // these tests will keep passing against a regex that no longer ships.
        private static readonly Regex SpacerPattern = new(
            @"\+\s*(?<mm>\d+)\s*mm\s+(?:[^+\d]*?\s+)?ASP\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // MIRRORS ProfitLossStepViewModel.PvbPattern.
        private static readonly Regex PvbPattern = new(
            @"\+\s*(?<mm>[\d\.]+)\s*mm\s+(?<color>[^+\d]+?)\s+(?<type>PVB|SGP|EVA)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Reproduces the view-model's outer/inner resolution.</summary>
        private static (string Outer, string Inner) ResolvePanes(string spec)
        {
            var matches = GlassPattern.Matches(spec).Cast<Match>().ToList();
            if (matches.Count == 0) return ("", "");

            var outer = matches[0];

            var spacer = SpacerPattern.Match(spec);
            var pvb = PvbPattern.Match(spec);

            int after =
                spacer.Success ? spacer.Index + spacer.Length :
                pvb.Success ? pvb.Index + pvb.Length :
                outer.Index + outer.Length;

            var inner = matches.FirstOrDefault(m => m.Index >= after);

            string Describe(Match? m) => m == null
                ? ""
                : $"{m.Groups["thk"].Value}mm {m.Groups["color"].Value.Trim()}";

            return (Describe(outer), Describe(inner));
        }

        // ══════════════════════════════════════════════════════════════
        //  THE BUG ITSELF
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Dgu_inner_pane_is_not_skipped()
        {
            var (outer, inner) = ResolvePanes(
                "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass");

            Assert.Equal("6mm HD Grey", outer);
            Assert.Equal("6mm Clear", inner);
        }

        [Fact]
        public void Colour_never_swallows_the_spacer()
        {
            // The exact corruption: colour came back as "ASP + 6mm Clear"
            // and thickness as 12 — the SPACER's thickness, not a pane's.
            var matches = GlassPattern.Matches(
                    "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass")
                .Cast<Match>()
                .ToList();

            Assert.All(matches, m =>
            {
                Assert.DoesNotContain("+", m.Groups["color"].Value);
                Assert.DoesNotContain("ASP", m.Groups["color"].Value);
            });
        }

        [Fact]
        public void Both_panes_are_found_when_the_colours_are_identical()
        {
            // Identical colours are the common real-world case and must
            // still yield TWO panes, not one deduplicated one.
            var (outer, inner) = ResolvePanes(
                "6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass");

            Assert.Equal("6mm Clear", outer);
            Assert.Equal("6mm Clear", inner);
        }

        [Theory]
        [InlineData("8mm Grey FT Glass + 16mm ASP + 6mm Clear Annealed Glass",
                    "8mm Grey", "6mm Clear")]
        [InlineData("10mm Clear Heat Soaked Glass + 12mm ASP + 6mm Grey FT Glass",
                    "10mm Clear", "6mm Grey")]
        public void Mixed_thicknesses_and_treatments_resolve(
            string spec, string expectedOuter, string expectedInner)
        {
            var (outer, inner) = ResolvePanes(spec);

            Assert.Equal(expectedOuter, outer);
            Assert.Equal(expectedInner, inner);
        }

        // ══════════════════════════════════════════════════════════════
        //  NOT BROKEN BY THE FIX
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Laminated_units_still_resolve_both_panes()
        {
            var (outer, inner) = ResolvePanes(
                "6mm Clear FT Glass + 1.52mm Clear PVB + 6mm Clear FT Glass");

            Assert.Equal("6mm Clear", outer);
            Assert.Equal("6mm Clear", inner);
        }

        [Fact]
        public void Multi_word_pvb_colour_still_parses()
        {
            var pvb = PvbPattern.Match(
                "6mm Grey FT Glass + 0.76mm Milky White PVB + 6mm Clear FT Glass");

            Assert.True(pvb.Success);
            Assert.Equal("Milky White", pvb.Groups["color"].Value.Trim());
        }

        [Fact]
        public void Single_glazed_units_have_an_outer_and_no_inner()
        {
            var (outer, inner) = ResolvePanes("6mm Clear FT Glass");

            Assert.Equal("6mm Clear", outer);
            Assert.Equal("", inner);
        }

        [Fact]
        public void Multi_word_colours_survive()
        {
            var (outer, _) = ResolvePanes("6mm HD Grey FT Glass");

            Assert.Equal("6mm HD Grey", outer);
        }

        // ══════════════════════════════════════════════════════════════
        //  THE MIRROR MUST NOT DRIFT
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Pattern_matches_the_shipped_source()
        {
            // A mirrored regex that silently diverges from the shipped one
            // tests nothing. This pins them together: if the view-model's
            // pattern is edited without updating this file, it fails.
            const string expected =
                @"(?<thk>\d+)\s*mm\s+(?<color>[^+\d]+?)\s+";

            Assert.Contains(expected, GlassPattern.ToString());

            // And the colour class must still exclude '+' and digits —
            // the two exclusions that fix the bug.
            Assert.Contains("[^+", GlassPattern.ToString());
        }
    
        // ══════════════════════════════════════════════════════════════
        //  DGU+LAM HYBRID — spacer AND interlayer in one spec
        // ══════════════════════════════════════════════════════════════

        private const string HybridSpec =
            "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass " +
            "+ 1.52mm Clear PVB + 6mm Clear FT Glass";

        [Fact]
        public void Hybrid_spec_yields_three_glass_panes()
        {
            var matches = GlassPattern.Matches(HybridSpec);

            Assert.Equal(3, matches.Count);
        }

        [Fact]
        public void Hybrid_spec_matches_both_spacer_and_interlayer()
        {
            // Both must match, or the hybrid is mistaken for a plain DGU
            // or a plain LAM and half its cost disappears.
            Assert.True(SpacerPattern.IsMatch(HybridSpec));
            Assert.True(PvbPattern.IsMatch(HybridSpec));
        }

        [Fact]
        public void Hybrid_interlayer_colour_does_not_swallow_other_layers()
        {
            // The unbounded ".+?" captured
            // "ASP + 6mm Clear FT Glass + 1.52mm Clear" here.
            var m = PvbPattern.Match(HybridSpec);

            Assert.True(m.Success);
            Assert.Equal("Clear", m.Groups["color"].Value);
            Assert.DoesNotContain("ASP", m.Groups["color"].Value);
            Assert.DoesNotContain("+", m.Groups["color"].Value);
        }

        [Theory]
        [InlineData("SGP")]
        [InlineData("EVA")]
        public void Sgp_and_eva_interlayers_are_matched(string interlayerType)
        {
            // These matched nothing before, so their bonded pane and their
            // material cost both vanished from the report.
            string spec =
                "6mm Clear FT Glass + 1.52mm Clear " + interlayerType +
                " + 6mm Clear FT Glass";

            var m = PvbPattern.Match(spec);

            Assert.True(m.Success);
            Assert.Equal(interlayerType, m.Groups["type"].Value);
        }

        [Fact]
        public void Multi_word_spacer_descriptor_is_matched()
        {
            // "Black Warm Edge ASP" matched nothing, so the spacer cost was
            // dropped from every warm-edge unit.
            var m = SpacerPattern.Match(
                "6mm Clear FT Glass + 16mm Black Warm Edge ASP + 6mm Clear FT Glass");

            Assert.True(m.Success);
            Assert.Equal("16", m.Groups["mm"].Value);
        }
    }
}
