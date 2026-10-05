// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/SqmCalculatorTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins the centralized SQM calculations (Utilities/SqmCalculator) so the
// business rules cannot silently drift:
//   • Panel SQM = (W × H) / 1,000,000, with a 0.5 m² billable minimum,
//     rounded to 3 dp.
//   • Sheet area = (W × H) / 1,000,000 (no floor), rounded to 4 dp.
//   • Invalid (zero/negative) dimensions return 0.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Utilities;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class SqmCalculatorTests
    {
        // ── Panel SQM ─────────────────────────────────────────────────────

        [Theory]
        [InlineData(1000, 1000, 1.000)]   // exactly 1 m²
        [InlineData(2000, 1000, 2.000)]   // 2 m²
        [InlineData(3210, 2250, 7.222)]   // 7.2225 → 7.222 (round-3)
        [InlineData(600, 400, 0.500)]     // 0.24 → floor 0.5
        [InlineData(500, 500, 0.500)]     // 0.25 → floor 0.5
        public void CalculatePanel_returns_expected_sqm(
            int w, int h, decimal expected)
        {
            Assert.Equal(expected, SqmCalculator.CalculatePanel(w, h));
        }

        [Theory]
        [InlineData(0, 1000)]
        [InlineData(1000, 0)]
        [InlineData(-100, 500)]
        [InlineData(0, 0)]
        public void CalculatePanel_invalid_dimensions_return_zero(
            int w, int h)
        {
            Assert.Equal(0m, SqmCalculator.CalculatePanel(w, h));
        }

        // ── Sheet area (physical, no floor) ───────────────────────────────

        [Theory]
        [InlineData(2440, 1830, 4.4652)]  // 4.4652 m²
        [InlineData(1000, 1000, 1.0000)]
        [InlineData(600, 400, 0.2400)]    // below 0.5 — NO floor for sheets
        public void CalculateSheetArea_returns_physical_area(
            int w, int h, decimal expected)
        {
            Assert.Equal(expected, SqmCalculator.CalculateSheetArea(w, h));
        }

        [Theory]
        [InlineData(0, 1000)]
        [InlineData(-500, 500)]
        [InlineData(0, 0)]
        public void CalculateSheetArea_invalid_dimensions_return_zero(
            int w, int h)
        {
            Assert.Equal(0m, SqmCalculator.CalculateSheetArea(w, h));
        }

        // ── Physical panel area (no floor) ────────────────────────────────

        [Fact]
        public void CalculatePhysicalPanelArea_no_floor_and_not_rounded()
        {
            // 1000×1000 → 1.0 exactly
            Assert.Equal(1.0m, SqmCalculator.CalculatePhysicalPanelArea(1000, 1000));
            // 3210×2250 → 7.2225 (kept exact, not rounded)
            Assert.Equal(7.2225m, SqmCalculator.CalculatePhysicalPanelArea(3210, 2250));
            // 600×400 → 0.24 (below 0.5 - NO floor for physical area)
            Assert.Equal(0.24m, SqmCalculator.CalculatePhysicalPanelArea(600, 400));
        }

        [Theory]
        [InlineData(0, 500)]
        [InlineData(-100, 500)]
        [InlineData(0, 0)]
        public void CalculatePhysicalPanelArea_invalid_dimensions_return_zero(int w, int h)
        {
            Assert.Equal(0m, SqmCalculator.CalculatePhysicalPanelArea(w, h));
        }

        // ── Total ─────────────────────────────────────────────────────────

        [Fact]
        public void CalculateTotal_multiplies_panel_sqm_by_quantity()
        {
            // 1000×1000 = 1.0 m² × 4 = 4.0
            Assert.Equal(4.000m, SqmCalculator.CalculateTotal(1000, 1000, 4));
            // quantity 0 → 0
            Assert.Equal(0m, SqmCalculator.CalculateTotal(1000, 1000, 0));
        }
    }
}
