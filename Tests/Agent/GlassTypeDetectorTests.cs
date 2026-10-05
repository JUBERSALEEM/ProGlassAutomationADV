// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/GlassTypeDetectorTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// REGRESSION SUITE FOR THE STEP 6 ADVANCED AUTO-CHARGE MISCLASSIFICATION.
//
// WHAT WENT WRONG
//   GlassTypeDetector.Detect() returned "SGU" for any spec name without a
//   spacer/interlayer marker, even when the name listed TWO glass panes
//   ("6mm HD Grey FT Glass + 6mm Clear FT Glass" — a DGU shorthand whose
//   "+ 12mm ASP" token is missing). SpecGroup.HandleSpecCommit then
//   auto-switched the authoritative glass type to SGU, and Step 6 advanced
//   automation suggested only the OUTER wastage side: cavity charges
//   (argon / U-Insert) and inner wastage vanished — "auto charge is not
//   triggering itself".
//
// WHY THE DETECTOR MUST SAY UNKNOWN HERE
//   Two panes without a marker is genuinely ambiguous between DGU, LAM and
//   a hybrid; text alone cannot pick. Guessing SGU actively mislabels a
//   multi-pane build as monolithic. Unknown keeps the Step 4 selection.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Utilities;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class GlassTypeDetectorTests
    {
        [Fact]
        public void Single_pane_without_markers_is_sgu()
        {
            Assert.Equal("SGU",
                GlassTypeDetector.Detect("6mm Clear FT Glass"));
        }

        [Fact]
        public void Multi_pane_without_markers_is_unknown_not_sgu()
        {
            Assert.Equal("Unknown",
                GlassTypeDetector.Detect(
                    "6mm HD Grey FT Glass + 6mm Clear FT Glass"));
        }

        [Fact]
        public void Multi_pane_with_spacer_is_dgu()
        {
            Assert.Equal("DGU",
                GlassTypeDetector.Detect(
                    "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass"));
        }

        [Fact]
        public void Multi_pane_with_interlayer_is_lam()
        {
            Assert.Equal("LAM",
                GlassTypeDetector.Detect(
                    "6mm Clear FT Glass + 1.52mm Clear PVB + 6mm Clear FT Glass"));
        }

        [Fact]
        public void Hybrid_markers_win_over_pane_count()
        {
            Assert.Equal("DGU+LAM",
                GlassTypeDetector.Detect(
                    "6mm Clear FT Glass + 12mm ASP + 6mm Grey FT Glass " +
                    "+ 1.52mm Clear PVB + 6mm Clear FT Glass"));
        }

        [Fact]
        public void Empty_spec_is_unknown()
        {
            Assert.Equal("Unknown", GlassTypeDetector.Detect(""));
            Assert.Equal("Unknown", GlassTypeDetector.Detect(null!));
            Assert.Equal("Unknown", GlassTypeDetector.Detect("   "));
        }

        [Fact]
        public void Ambiguity_helper_flags_only_markerless_multi_pane_specs()
        {
            Assert.True(GlassTypeDetector.IsMultiPaneWithoutBuildMarkers(
                "6mm HD Grey FT Glass + 6mm Clear FT Glass"));
            Assert.False(GlassTypeDetector.IsMultiPaneWithoutBuildMarkers(
                "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass"));
            Assert.False(GlassTypeDetector.IsMultiPaneWithoutBuildMarkers(
                "6mm Clear FT Glass + 1.52mm Clear PVB + 6mm Clear FT Glass"));
            Assert.False(GlassTypeDetector.IsMultiPaneWithoutBuildMarkers(
                "6mm Clear FT Glass"));
            Assert.False(GlassTypeDetector.IsMultiPaneWithoutBuildMarkers(""));
        }
    }
}
