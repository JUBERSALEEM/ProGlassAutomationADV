// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/GlassAssemblyShowcaseTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// The Step 4 showcase maps live spec fields to a manufacturing drawing.
// These tests lock the parser / tint helpers so a DGU caption still
// yields two glass colours and a spacer millimetre, not a blank pane.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Controls.GlassShowcase;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class GlassAssemblyShowcaseTests
    {
        [Theory]
        [InlineData("6mm", 6)]
        [InlineData("12mm ASP", 12)]
        [InlineData("1.52mm", 1.52)]
        [InlineData("  8 mm Clear", 8)]
        [InlineData("", 0)]
        [InlineData(null, 0)]
        public void ParseMm_reads_the_first_number(string? raw, double expected)
        {
            Assert.Equal(expected, GlassAssemblyShowcase.ParseMm(raw), 3);
        }

        [Fact]
        public void GlassTint_distinguishes_clear_grey_bronze_and_green()
        {
            var clear = GlassAssemblyShowcase.GlassTint("Clear");
            var grey = GlassAssemblyShowcase.GlassTint("HD Grey");
            var bronze = GlassAssemblyShowcase.GlassTint("Bronze");
            var green = GlassAssemblyShowcase.GlassTint("Green");

            Assert.NotEqual(clear, grey);
            Assert.NotEqual(clear, bronze);
            Assert.NotEqual(grey, green);
            Assert.True(bronze.R > bronze.B);
            Assert.True(green.G > green.R);
        }

        [Fact]
        public void InterlayerTint_defaults_to_safety_amber_for_pvb()
        {
            var pvb = GlassAssemblyShowcase.InterlayerTint("Clear", "PVB");
            Assert.True(pvb.R > pvb.B);
            Assert.True(pvb.G > 100);
        }
    }
}
