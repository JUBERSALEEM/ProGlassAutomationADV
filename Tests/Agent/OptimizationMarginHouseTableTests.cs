// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/OptimizationMarginHouseTableTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// PHASE 1 (2026-09-20) — pins the owner-specified house margin table.
//
// Owner's specification, verbatim:
//   "set fall back lm rm bm tm and minimum breakout.
//    6mm  - lm rm bm tm and minimum breakout each 15,
//    8mm  - lm rm bm tm and minimum breakout each 30,
//    10mm and 12mm - lm 50, rm 50, bm 0, tm 50 and minimum breakout 50
//                    or 40 ... by default minimum breakout 50 and keep
//                    switchable to 40, auto select as per thickness trigger."
//
// The 6mm row is unchanged from the previous table, which is what keeps the
// calibration benchmarks (OptimizationBenchmarkTests — 17 sheets @ 82.77%)
// valid: every reference figure in that file is 6mm glass.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class OptimizationMarginHouseTableTests
    {
        // ── the three house profiles ───────────────────────────────────

        [Fact]
        public void House_6mm_AllFifteen()
        {
            var m = MarginProfile.DefaultFor(6);

            Assert.Equal(6, m.ThicknessMm);
            Assert.Equal(15, m.LeftMm);
            Assert.Equal(15, m.RightMm);
            Assert.Equal(15, m.TopMm);
            Assert.Equal(15, m.BottomMm);
            Assert.Equal(15, m.MinBreakoutMm);
            Assert.Equal(3, m.KerfMm);
        }

        [Fact]
        public void House_8mm_AllThirty()
        {
            var m = MarginProfile.DefaultFor(8);

            Assert.Equal(30, m.LeftMm);
            Assert.Equal(30, m.RightMm);
            Assert.Equal(30, m.TopMm);
            Assert.Equal(30, m.BottomMm);
            Assert.Equal(30, m.MinBreakoutMm);
            Assert.Equal(3, m.KerfMm);
        }

        [Theory]
        [InlineData(10)]
        [InlineData(12)]
        public void House_ThickGlass_FiftyFiftyFiftyZero(int thickness)
        {
            var m = MarginProfile.DefaultFor(thickness);

            Assert.Equal(50, m.LeftMm);
            Assert.Equal(50, m.RightMm);
            Assert.Equal(50, m.TopMm);
            // The thick profile is deliberately ASYMMETRIC: the bottom edge is
            // cut on the float line, so no trim is taken there.
            Assert.Equal(0, m.BottomMm);
            Assert.Equal(MarginProfile.BreakoutPrimaryMm, m.MinBreakoutMm);
            Assert.Equal(50, m.MinBreakoutMm);
            Assert.Equal(4, m.KerfMm);
        }

        // ── bucketing of thicknesses the owner did not name ────────────

        [Theory]
        [InlineData(0, ThicknessBucket.Thin)]     // unparseable label → safe fallback
        [InlineData(-1, ThicknessBucket.Thin)]
        [InlineData(4, ThicknessBucket.Thin)]
        [InlineData(5, ThicknessBucket.Thin)]
        [InlineData(6, ThicknessBucket.Thin)]
        [InlineData(7, ThicknessBucket.Medium)]   // rounds UP to the next known bucket
        [InlineData(8, ThicknessBucket.Medium)]
        [InlineData(9, ThicknessBucket.Thick)]    // rounds UP
        [InlineData(10, ThicknessBucket.Thick)]
        [InlineData(11, ThicknessBucket.Thick)]
        [InlineData(12, ThicknessBucket.Thick)]
        [InlineData(15, ThicknessBucket.Thick)]
        [InlineData(19, ThicknessBucket.Thick)]   // thicker than anything named → stays thick
        public void BucketFor_RoundsUpToTheNextKnownProfile(int thickness, ThicknessBucket expected)
        {
            Assert.Equal(expected, MarginProfile.BucketFor(thickness));
        }

        [Fact]
        public void House_7mm_TakesThe8mmProfile_NotThe6mm()
        {
            // Rounding up is the conservative direction: thicker glass needs
            // more trim, so an unlisted 7mm must never fall back to 15mm.
            var m = MarginProfile.DefaultFor(7);

            Assert.Equal(30, m.LeftMm);
            Assert.Equal(30, m.MinBreakoutMm);
        }

        [Fact]
        public void House_UnknownThickness_TakesTheSafeThinProfile()
        {
            // A label the thickness parser could not read yields 0. The fallback
            // is the thin profile — the same 6mm default the module has always
            // used — never a zero-margin profile.
            var m = MarginProfile.DefaultFor(0);

            Assert.Equal(15, m.LeftMm);
            Assert.Equal(15, m.RightMm);
            Assert.Equal(15, m.TopMm);
            Assert.Equal(15, m.BottomMm);
            Assert.Equal(15, m.MinBreakoutMm);
        }

        // ── the switchable 50 / 40 breakout ────────────────────────────

        [Theory]
        [InlineData(6, false)]
        [InlineData(8, false)]
        [InlineData(9, true)]
        [InlineData(10, true)]
        [InlineData(12, true)]
        [InlineData(19, true)]
        public void HasBreakoutChoice_OnlyOnThickGlass(int thickness, bool expected)
        {
            Assert.Equal(expected, MarginProfile.HasBreakoutChoice(thickness));
        }

        [Theory]
        [InlineData(10)]
        [InlineData(12)]
        public void Breakout_SwitchToForty_IsHonouredOnThickGlass(int thickness)
        {
            var m = MarginProfile.DefaultFor(thickness, MarginProfile.BreakoutAlternateMm);

            Assert.Equal(40, m.MinBreakoutMm);
            // The switch changes ONLY the breakout — margins and kerf stay house.
            Assert.Equal(50, m.LeftMm);
            Assert.Equal(50, m.RightMm);
            Assert.Equal(50, m.TopMm);
            Assert.Equal(0, m.BottomMm);
            Assert.Equal(4, m.KerfMm);
        }

        [Theory]
        [InlineData(6)]
        [InlineData(8)]
        public void Breakout_SwitchIsIgnoredWhereTheFloorHasNoChoice(int thickness)
        {
            // On thin and medium glass the breakout equals the margin. Asking
            // for 40 there must NOT silently produce a 40mm breakout on a 15mm
            // margin — the request is refused and the bucket value stands.
            var m = MarginProfile.DefaultFor(thickness, MarginProfile.BreakoutAlternateMm);

            Assert.Equal(MarginProfile.DefaultFor(thickness).MinBreakoutMm, m.MinBreakoutMm);
        }

        [Fact]
        public void Breakout_UnsanctionedValue_ClampsBackToTheBucketDefault()
        {
            // A stray 0 or 5 must never disable breakout protection.
            var m = MarginProfile.DefaultFor(12, 0);

            Assert.Equal(MarginProfile.BreakoutPrimaryMm, m.MinBreakoutMm);
        }

        [Fact]
        public void BreakoutConstants_AreTheOwnersTwoValues()
        {
            Assert.Equal(50, MarginProfile.BreakoutPrimaryMm);
            Assert.Equal(40, MarginProfile.BreakoutAlternateMm);
        }

        // ── customization is an explicit fact, not an inference ────────

        [Fact]
        public void HouseProfile_IsNotCustomized()
        {
            Assert.False(MarginProfile.DefaultFor(10).IsCustomized);
        }

        [Fact]
        public void DeliberateZeroes_AreDistinguishableFromHouseDefaults()
        {
            // Finding 3: ResolveGroupMargins used to infer "the operator
            // customized this" from `LeftMm != 0 || TopMm != 0`. A deliberate
            // Left = 0 / Top = 0 was therefore read as NOT customized and house
            // defaults were substituted with no note at all. The flag now makes
            // that state representable.
            var deliberate = new MarginProfile
            {
                ThicknessMm = 6,
                LeftMm = 0,
                RightMm = 15,
                TopMm = 0,
                BottomMm = 15,
                KerfMm = 3,
                MinBreakoutMm = 15,
                IsCustomized = true
            };

            Assert.True(deliberate.IsCustomized);
            Assert.False(deliberate.SameGeometryAs(MarginProfile.DefaultFor(6)));
        }

        [Fact]
        public void SameGeometryAs_IgnoresThicknessAndTheCustomizedFlag()
        {
            // It compares what the CUT uses, so two profiles with the same
            // numbers are the same geometry regardless of which label they carry.
            var a = MarginProfile.DefaultFor(10);
            var b = new MarginProfile
            {
                ThicknessMm = 999,
                LeftMm = a.LeftMm,
                RightMm = a.RightMm,
                TopMm = a.TopMm,
                BottomMm = a.BottomMm,
                KerfMm = a.KerfMm,
                MinBreakoutMm = a.MinBreakoutMm,
                IsCustomized = true
            };

            Assert.True(a.SameGeometryAs(b));
            Assert.True(b.SameGeometryAs(a));
            Assert.False(a.SameGeometryAs(null));
        }

        [Fact]
        public void SameGeometryAs_DetectsAKerfChange()
        {
            var a = MarginProfile.DefaultFor(6);
            var b = new MarginProfile
            {
                ThicknessMm = 6,
                LeftMm = a.LeftMm,
                RightMm = a.RightMm,
                TopMm = a.TopMm,
                BottomMm = a.BottomMm,
                KerfMm = a.KerfMm + 1,
                MinBreakoutMm = a.MinBreakoutMm
            };

            Assert.False(a.SameGeometryAs(b));
        }

        // ── the unlimited-stock sentinel ───────────────────────────────

        [Fact]
        public void UnlimitedSheets_IsTheValueThePackerTreatsAsNoLimit()
        {
            // Finding 1: MaxSheets == 0 means unlimited in all three packer
            // strategies. Naming it keeps a call site from reading a bare 0 as
            // "infinite glass" by accident, and gives the input parser one
            // constant to reach for when — and only when — a field is blank.
            Assert.Equal(0, StockSize.UnlimitedSheets);
        }

        [Fact]
        public void StockWithABalance_IsNotUnlimited()
        {
            var s = new StockSize { WidthMm = 3210, HeightMm = 2250, MaxSheets = 12 };

            Assert.NotEqual(StockSize.UnlimitedSheets, s.MaxSheets);
            Assert.Equal(12, s.MaxSheets);
        }
    }
}
