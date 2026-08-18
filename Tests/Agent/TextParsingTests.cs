// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/TextParsingTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins the shared TextParsing helpers (CleanNum, ParseInt, NullIfEmpty,
// SplitClipboardRows, BuildColumnValues) extracted from the DataGrid /
// ProfitLoss / Statement God VMs.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Utilities;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class TextParsingTests
    {
        // ── CleanNum ─────────────────────────────────────────────────────

        [Theory]
        [InlineData("1,234", "1234")]
        [InlineData("1 234", "1234")]
        [InlineData("  42  ", "42")]
        [InlineData("", "0")]
        [InlineData(null, "0")]
        public void CleanNum_strips_separators_and_whitespace(
            string? input, string expected)
        {
            Assert.Equal(expected, TextParsing.CleanNum(input!));
        }

        // ── ParseInt ─────────────────────────────────────────────────────

        [Theory]
        [InlineData("42", 42)]
        [InlineData("  7  ", 7)]
        [InlineData("", 0)]
        [InlineData(null, 0)]
        [InlineData("not-a-number", 0)]
        public void ParseInt_parses_or_falls_back_to_zero(string? input, int expected)
        {
            Assert.Equal(expected, TextParsing.ParseInt(input));
        }

        // ── NullIfEmpty ──────────────────────────────────────────────────

        [Fact]
        public void NullIfEmpty_returns_null_for_blank()
        {
            Assert.Null(TextParsing.NullIfEmpty(""));
            Assert.Null(TextParsing.NullIfEmpty("   "));
        }

        [Fact]
        public void NullIfEmpty_trims_non_blank()
        {
            Assert.Equal("Acme", TextParsing.NullIfEmpty("  Acme  "));
        }

        // ── SplitClipboardRows ───────────────────────────────────────────

        [Fact]
        public void SplitClipboardRows_drops_blank_lines_and_pads()
        {
            var rows = TextParsing.SplitClipboardRows(
                "a\tb\n\nc\td\te\n\n");

            Assert.Equal(2, rows.Count);
            Assert.Equal(new[] { "a", "b" }, rows[0]);
            Assert.Equal(new[] { "c", "d", "e" }, rows[1]);
        }

        // ── BuildColumnValues ────────────────────────────────────────────

        [Fact]
        public void BuildColumnValues_stops_at_max_column_index()
        {
            // Clipboard row of 4 cells starting at column 2, max column 4.
            var map = TextParsing.BuildColumnValues(
                new[] { "a", "b", "c", "d" }, 2, 4);

            Assert.Equal(3, map.Count);   // cols 2, 3, 4 only
            Assert.Equal("a", map[2]);
            Assert.Equal("b", map[3]);
            Assert.Equal("c", map[4]);
            Assert.False(map.ContainsKey(5));
        }
    }
}
