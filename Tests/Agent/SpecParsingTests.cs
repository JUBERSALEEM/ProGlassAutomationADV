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

using System.Linq;
using System.Text.RegularExpressions;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class SpecParsingTests
    {
        // Production grammar — not a mirror. If these tests fail, the
        // shipped parser is wrong, not a stale copy.
        private static readonly Regex GlassPattern = PnLSpecParser.GlassPattern;
        private static readonly Regex SpacerPattern = PnLSpecParser.SpacerPattern;
        private static readonly Regex PvbPattern = PnLSpecParser.InterlayerPattern;

        private static (string Outer, string Inner) ResolvePanes(string spec)
        {
            var (outer, inner) = PnLSpecParser.ResolveTwoPanes(spec);
            string Describe(PnLPane? p) => p == null
                ? ""
                : $"{p.ThicknessMm}mm {p.Color}";
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
        public void Colour_class_still_excludes_plus_and_digits()
        {
            Assert.Contains("[^+", PnLSpecParser.GlassPattern.ToString());
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
            Assert.Matches(SpacerPattern, HybridSpec);
            Assert.Matches(PvbPattern, HybridSpec);
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

        // ══════════════════════════════════════════════════════════════
        //  2026-09-05 — WASTAGE COLOUR LOSS (STEP 6 REPORTED DEFECTS)
        //  (a) digits inside a glass colour (brand codes) killed the
        //      whole pane match → colour dropdowns read only "Clear";
        //  (b) the inner pane of a 3+ pane DGU was the MIDDLE pane
        //      (first pane after the first spacer) — never the real
        //      inner; the middle pane was invisible everywhere.
        // ══════════════════════════════════════════════════════════════

        [Theory]
        [InlineData("6mm Kibbing Nova 55 FT Glass + 12mm ASP + 6mm Clear FT Glass",
                    "6mm Kibbing Nova 55", "6mm Clear")]
        [InlineData("6mm Cool Lite 365 FT Glass + 12mm ASP + 8mm Enermax 20 FT Glass",
                    "6mm Cool Lite 365", "8mm Enermax 20")]
        public void Brands_with_digits_are_not_dropped(
            string spec, string expectedOuter, string expectedInner)
        {
            // The ROOT-REPORTED bug: with [^+\d] the first pane never
            // matched; "both dropdowns only show Clear".
            var (outer, inner) = ResolvePanes(spec);

            Assert.Equal(expectedOuter, outer);
            Assert.Equal(expectedInner, inner);
        }

        [Fact]
        public void Triple_glazed_dgu_yields_all_three_panes()
        {
            // "… + 6mm Mid Grey FT Glass + 12mm ASP + …" has TWO spacers.
            var panes = PnLSpecParser.ResolveAllPanes(
                "6mm Kibbing Nova 55 FT Glass + 12mm ASP " +
                "+ 6mm Mid Grey FT Glass + 12mm ASP " +
                "+ 6mm Clear FT Glass");

            Assert.Equal(3, panes.Count);
            Assert.Equal("Outer", panes[0].Side);
            Assert.Equal("Middle", panes[1].Side);
            Assert.Equal("Inner", panes[2].Side);
            Assert.Equal("Kibbing Nova 55", panes[0].Color);
            Assert.Equal("Mid Grey", panes[1].Color);
            Assert.Equal("Clear", panes[2].Color);
        }

        [Fact]
        public void ResolveTwoPanes_takes_the_REAL_inner_pane()
        {
            // With two spacers, "Inner" must be the LAST pane — the old
            // rule returned the MIDDLE pane (first pane after the first
            // spacer), so Step 9 costed outer+middle and missed inner.
            var (_, inner) = PnLSpecParser.ResolveTwoPanes(
                "6mm Kibbing Nova 55 FT Glass + 12mm ASP " +
                "+ 6mm Mid Grey FT Glass + 12mm ASP " +
                "+ 6mm Clear FT Glass");

            Assert.NotNull(inner);
            Assert.Equal("Mid Grey",
                PnLSpecParser.ResolveAllPanes(
                    "6mm Kibbing Nova 55 FT Glass + 12mm ASP " +
                    "+ 6mm Mid Grey FT Glass + 12mm ASP + 6mm Clear FT Glass")[1].Color);
            Assert.Equal("Clear", inner!.Color);
        }

        [Fact]
        public void Colour_never_swallows_a_thickness_fragment()
        {
            // The tempered token must still refuse "\d+mm" inside a
            // colour — the fix opened digits, not a new corruption lane.
            var matches = GlassPattern.Matches("6mm Nova 55 4mm FT Glass")
                .Cast<Match>().ToList();
            Assert.Empty(matches);

            matches = GlassPattern.Matches("6mm FT Glass").Cast<Match>().ToList();
            Assert.Empty(matches); // colourless pane: no match (parity)
        }

        [Fact]
        public void Four_pane_build_labels_middle_panes_with_ordinals()
        {
            // Docker hybrid: outer + middle + middle + inner. Ordinals
            // keep two identical middle colours distinguishable on rows.
            var panes = PnLSpecParser.ResolveAllPanes(
                "6mm Clear FT Glass + 12mm ASP " +
                "+ 6mm HD Grey FT Glass + 12mm ASP " +
                "+ 6mm Bronze FT Glass + 12mm ASP " +
                "+ 6mm Kibbing Nova 55 FT Glass");

            Assert.Equal(4, panes.Count);
            Assert.Equal("Outer", panes[0].Side);
            Assert.Equal("Middle 1", panes[1].Side);
            Assert.Equal("Middle 2", panes[2].Side);
            Assert.Equal("Inner", panes[3].Side);
        }

        [Fact]
        public void Two_pane_build_has_no_middle()
        {
            // The classic build must keep producing ONLY Outer/Inner —
            // any phantom middle would invent a wastage row and a Step 9
            // material row from nothing.
            var panes = PnLSpecParser.ResolveAllPanes(
                "6mm Clear FT Glass + 12mm ASP + 6mm HD Grey FT Glass");

            Assert.Equal(2, panes.Count);
            Assert.Equal("Outer", panes[0].Side);
            Assert.Equal("Inner", panes[1].Side);
        }

        [Fact]
        public void Single_pane_build_returns_no_inner()
        {
            // Colourless middle/lam layers name no second pane — a SGU
            // must never gain a phantom inner row.
            var panes = PnLSpecParser.ResolveAllPanes("10mm Clear FT Glass");

            Assert.Single(panes);
            Assert.Equal("Outer", panes[0].Side);
        }

        // ══════════════════════════════════════════════════════════════
        //  2026-09-05 — BUSINESS DECISION: every ASP cavity pays.
        //  SpacerCount drives the per-cavity ASP multiplier in Step 9
        //  costing AND the rate-sanity basis. It must count tokens —
        //  never glass panes, never zero on a classic build.
        // ══════════════════════════════════════════════════════════════

        [Theory]
        [InlineData("6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass", 1)]
        [InlineData(
            "6mm Kibbing Nova 55 FT Glass + 12mm ASP " +
            "+ 6mm Mid Grey FT Glass + 16mm ASP + 6mm Clear FT Glass", 2)]
        [InlineData(
            "6mm Clear FT Glass + 8mm ASP " +
            "+ 6mm HD Grey FT Glass + 10mm ASP " +
            "+ 6mm Bronze FT Glass + 12mm ASP + 6mm Clear FT Glass", 3)]
        [InlineData("10mm Clear FT Glass", 0)]
        [InlineData("6mm Clear FT Glass + 1.52mm Clear PVB + 6mm Clear FT Glass", 0)]
        [InlineData("6mm Clear FT Glass + 16mm Black Warm Edge ASP + 6mm Clear FT Glass", 1)]
        public void SpacerCount_counts_every_cavity_token(string spec, int expected)
        {
            // Classic = 1 (pay once, as before); triple = 2 (pay twice);
            // lam/SGU = 0 (no spacer at all). Multi-word descriptors
            // (Black Warm Edge) still count as ONE cavity.
            Assert.Equal(expected, PnLSpecParser.SpacerCount(spec));
        }

        [Fact]
        public void SpacerCount_never_confuses_pvb_thickness_for_a_cavity()
        {
            // The PVB/mm and spacer/mm lanes share the mm motor: without
            // an ASP/SGU-specific descriptor, a "'1.52mm Clear PVB'" must
            // not inflate the cavity count.
            Assert.Equal(0, PnLSpecParser.SpacerCount(
                "6mm Clear FT Glass + 3.04mm Clear PVB + 6mm Clear FT Glass"));
        }
    }
}
