// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ProfitLossIntegrityTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Covers the detector that finds profit figures produced BEFORE the
// inner-pane fix.
//
// BIAS UNDER TEST: this detector must prefer FALSE NEGATIVES. Telling
// someone their books are wrong when they are not has its own cost, so
// several tests below assert that it stays quiet in ambiguous cases
// rather than that it catches everything.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.ProformaInvoice;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class ProfitLossIntegrityTests
    {
        private const string DguSpec =
            "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass";

        private const string LamSpec =
            "6mm Clear FT Glass + 1.52mm Clear PVB + 6mm Clear FT Glass";

        private const string SguSpec = "6mm Clear FT Glass";

        private static ProfitLossSnapshot Snapshot(params (string Spec, string Kind)[] rows)
        {
            var snap = new ProfitLossSnapshot();

            foreach (var (spec, kind) in rows)
            {
                snap.Rows.Add(new ProfitLossRowSnapshot
                {
                    Kind = kind,
                    SpecName = spec,
                    Amount = 100m
                });
            }

            return snap;
        }

        // ══════════════════════════════════════════════════════════════
        //  IT CATCHES THE ACTUAL BUG
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Dgu_with_only_one_glass_row_is_flagged()
        {
            // The exact pre-fix signature: a spacer in the spec, but only
            // the outer pane was ever costed.
            var result = ProfitLossIntegrityCheck.ScanSnapshot(
                Snapshot((DguSpec, "Glass"), (DguSpec, "Spacer")));

            Assert.True(result.NeedsRecalculation);
            Assert.Contains(DguSpec, result.AffectedSpecs);
        }

        [Fact]
        public void Laminated_with_only_one_glass_row_is_flagged()
        {
            // LAM was hit by the same regex bug — the lazy wildcard crossed
            // ANY '+', not only the one before ASP. Checking DGU alone
            // would have left every laminated job silently mis-costed.
            var result = ProfitLossIntegrityCheck.ScanSnapshot(
                Snapshot((LamSpec, "Glass")));

            Assert.True(result.NeedsRecalculation);
        }

        [Fact]
        public void The_warning_says_what_to_do_about_it()
        {
            var result = ProfitLossIntegrityCheck.ScanSnapshot(
                Snapshot((DguSpec, "Glass")));

            Assert.Contains("Step 9", result.Reason);
            Assert.Contains("overstated", result.Reason,
                System.StringComparison.OrdinalIgnoreCase);
        }

        // ══════════════════════════════════════════════════════════════
        //  IT STAYS QUIET WHEN IT SHOULD
        // ══════════════════════════════════════════════════════════════

        [Fact]
        public void Correctly_costed_dgu_is_not_flagged()
        {
            var result = ProfitLossIntegrityCheck.ScanSnapshot(
                Snapshot((DguSpec, "Glass"), (DguSpec, "Glass"),
                         (DguSpec, "Spacer")));

            Assert.False(result.NeedsRecalculation);
        }

        [Fact]
        public void Single_glazed_with_one_pane_is_correct_not_suspect()
        {
            // An SGU legitimately has one pane. Flagging it would train the
            // user to ignore the warning.
            var result = ProfitLossIntegrityCheck.ScanSnapshot(
                Snapshot((SguSpec, "Glass")));

            Assert.False(result.NeedsRecalculation);
        }

        [Fact]
        public void Rows_saved_before_SpecName_existed_are_not_accused()
        {
            // Older snapshots have an empty SpecName. They cannot be judged,
            // so they must not be blamed.
            var result = ProfitLossIntegrityCheck.ScanSnapshot(
                Snapshot(("", "Glass")));

            Assert.False(result.NeedsRecalculation);
        }

        [Fact]
        public void An_uncosted_snapshot_is_not_this_checks_business()
        {
            // No rows means "never costed", which the UI already reports
            // separately as "not costed".
            Assert.False(ProfitLossIntegrityCheck
                .ScanSnapshot(new ProfitLossSnapshot()).NeedsRecalculation);
        }

        [Fact]
        public void A_null_snapshot_is_handled_not_thrown()
        {
            var result = ProfitLossIntegrityCheck.ScanSnapshot(null);

            Assert.False(result.NeedsRecalculation);
            Assert.Equal("", result.Reason);
        }

        [Fact]
        public void Production_rows_do_not_count_as_glass_panes()
        {
            // Only Kind == "Glass" is a pane. A spacer or production line
            // must not be mistaken for the missing inner pane.
            var result = ProfitLossIntegrityCheck.ScanSnapshot(
                Snapshot((DguSpec, "Glass"), (DguSpec, "Production")));

            Assert.True(result.NeedsRecalculation);
        }

        [Fact]
        public void Multi_word_interlayer_colours_are_recognised()
        {
            // "Milky White PVB" broke an earlier version of the pattern,
            // which allowed only a single colour word.
            var result = ProfitLossIntegrityCheck.ScanSnapshot(
                Snapshot(
                    ("6mm Grey FT Glass + 0.76mm Milky White PVB + 6mm Clear FT Glass",
                     "Glass")));

            Assert.True(result.NeedsRecalculation);
        }

        [Fact]
        public void Black_warm_edge_spacer_is_recognised()
        {
            var result = ProfitLossIntegrityCheck.ScanSnapshot(
                Snapshot(
                    ("6mm Clear FT Glass + 12mm Black Warm Edge ASP + 6mm Clear FT Glass",
                     "Glass")));

            Assert.True(result.NeedsRecalculation);
        }
    }
}
