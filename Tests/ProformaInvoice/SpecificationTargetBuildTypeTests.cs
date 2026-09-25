// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/ProformaInvoice/SpecificationTargetBuildTypeTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Step 9 — specification target pricing, per build type
//
// WHY THIS FILE EXISTS
// ────────────────────
// TargetPriceModelTests.cs is thorough — approved vectors, the shop's own
// 103.82 → 119.39 worked example, every missing-input status, the U-insert
// treatments. But every single case in it is a DGU. Measured 2026-09-09:
//
//     GlassBuildType.Sgu      0 occurrences anywhere under Tests/
//     GlassBuildType.Lam      0
//     GlassBuildType.DguLam   0
//
// Three of the four build types have never been exercised. They are not
// cosmetic variants: GlassRateSum, HasRequiredGlassRates and RequiresAsp all
// switch on the build type, and they decide three things that change the
// number on the card — how many lites are summed, whether a missing third
// lite blocks the calculation, and whether an ASP/spacer rate is demanded at
// all. An SGU silently picking up the DGU branch would add a zero inner rate
// and pass; a LAM wrongly requiring ASP would refuse to price a valid spec.
//
// Also untested before this file: the TargetUpliftPercent <= 0 fallback to the
// 15% default, and the Math.Max(0m, areaSqm) clamp.
//
// Every expected value was computed independently in base-10 decimal with
// ROUND_HALF_EVEN, which is what Math.Round(value, 2) does. None is estimated.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.ProformaInvoice.TargetPricing;
using Xunit;

namespace ProGlassAutomation.Tests.ProformaInvoice
{
    public sealed class SpecificationTargetBuildTypeTests
    {
        private static SpecificationRateCard Card(
            GlassBuildType buildType,
            decimal outer,
            decimal inner = 0m,
            decimal third = 0m,
            decimal asp = 0m,
            decimal processing = 0m,
            decimal yieldFactor = 0.85m,
            decimal upliftPercent = 15m) => new()
        {
            BuildType = buildType,
            OuterGlassRatePerSqm = outer,
            InnerGlassRatePerSqm = inner,
            ThirdGlassRatePerSqm = third,
            AspRatePerSqm = asp,
            ProcessingRatePerSqm = processing,
            YieldFactor = yieldFactor,
            TargetUpliftPercent = upliftPercent
        };

        // ───────────────────────────────────────────────────────────────
        // Glass sum per build type
        // ───────────────────────────────────────────────────────────────

        [Fact]
        public void Sgu_sums_only_the_outer_lite_and_needs_no_asp()
        {
            // 40 ÷ 0.90 = 44.4444… → 44.44, × 1.15 = 51.106 → 51.11.
            // The inner rate is deliberately non-zero here: if the SGU branch
            // ever picked up the DGU sum, this figure would move.
            var result = SpecificationTargetCalculator.Calculate(
                specNumber: 1, specName: "SGU 6mm", glassTypeCode: "G1",
                buildType: GlassBuildType.Sgu,
                card: Card(GlassBuildType.Sgu, outer: 40m, inner: 99m,
                           yieldFactor: 0.90m),
                givenPricePerSqm: 60m, areaSqm: 10m);

            Assert.Equal(TargetStatus.UpToDate, result.Status);
            Assert.Equal(44.44m, result.BasePricePerSqm);
            Assert.Equal(51.11m, result.SpecificationTargetPerSqm);
            Assert.Equal(511.10m, result.RequiredTargetValue);
        }

        [Fact]
        public void Lam_sums_outer_and_inner_and_needs_no_asp()
        {
            // (30 + 20) ÷ 0.80 + 5 processing = 67.50 → × 1.15 = 77.625.
            //
            // 77.625 is an exact half-fil. Phase 2 now routes this rate through
            // MoneyRounding (AwayFromZero), so it is 77.63, not the old
            // banker's 77.62. The line value is that rate times area, rounded
            // again: 77.63 × 10 = 776.30.
            var result = SpecificationTargetCalculator.Calculate(
                specNumber: 2, specName: "LAM 6.38", glassTypeCode: "G2",
                buildType: GlassBuildType.Lam,
                card: Card(GlassBuildType.Lam, outer: 30m, inner: 20m,
                           processing: 5m, yieldFactor: 0.80m),
                givenPricePerSqm: 90m, areaSqm: 10m);

            Assert.Equal(TargetStatus.UpToDate, result.Status);
            Assert.Equal(67.50m, result.BasePricePerSqm);
            Assert.Equal(77.63m, result.SpecificationTargetPerSqm);
            Assert.Equal(776.30m, result.RequiredTargetValue);
        }

        [Fact]
        public void DguLam_adds_the_third_lite()
        {
            // (25 + 25 + 18) ÷ 0.85 + 40 = 80 + 40 = 120.00 → × 1.15 = 138.00.
            // Dropping the third lite from the sum would give 108.82, so this
            // asserts the branch rather than the arithmetic alone.
            var result = SpecificationTargetCalculator.Calculate(
                specNumber: 3, specName: "DGU+LAM", glassTypeCode: "G3",
                buildType: GlassBuildType.DguLam,
                card: Card(GlassBuildType.DguLam, outer: 25m, inner: 25m,
                           third: 18m, asp: 40m, yieldFactor: 0.85m),
                givenPricePerSqm: 150m, areaSqm: 10m);

            Assert.Equal(TargetStatus.UpToDate, result.Status);
            Assert.Equal(120.00m, result.BasePricePerSqm);
            Assert.Equal(138.00m, result.SpecificationTargetPerSqm);
            Assert.Equal(1380.00m, result.RequiredTargetValue);
        }

        // ───────────────────────────────────────────────────────────────
        // Required inputs differ by build type
        // ───────────────────────────────────────────────────────────────

        [Theory]
        [InlineData(GlassBuildType.Sgu, 0, 0, 0, TargetStatus.MissingGlassRate)]
        [InlineData(GlassBuildType.Lam, 30, 0, 0, TargetStatus.MissingGlassRate)]
        [InlineData(GlassBuildType.DguLam, 25, 25, 0, TargetStatus.MissingGlassRate)]
        public void A_missing_required_lite_blocks_the_calculation(
            GlassBuildType buildType, decimal outer, decimal inner, decimal third,
            TargetStatus expected)
        {
            // An SGU with no outer rate is a hole in the rate card, not a zero
            // price. The calculator must say so rather than price the spec at
            // the ASP and processing alone.
            var result = SpecificationTargetCalculator.Calculate(
                specNumber: 4, specName: "X", glassTypeCode: "G4",
                buildType: buildType,
                card: Card(buildType, outer, inner, third, asp: 40m,
                           yieldFactor: 0.85m),
                givenPricePerSqm: 100m, areaSqm: 10m);

            Assert.Equal(expected, result.Status);
            Assert.Equal(0m, result.SpecificationTargetPerSqm);
        }

        [Fact]
        public void Asp_is_required_for_dgu_and_dguLam_but_not_for_sgu_or_lam()
        {
            // The single behavioural difference between the four branches that
            // is easiest to get wrong by analogy. A LAM has no cavity, so it
            // has no spacer bar and no ASP rate to require.
            var dgu = SpecificationTargetCalculator.Calculate(
                5, "DGU", "G5", GlassBuildType.Dgu,
                Card(GlassBuildType.Dgu, outer: 25m, inner: 25m, asp: 0m),
                100m, 10m);
            Assert.Equal(TargetStatus.MissingAspRate, dgu.Status);

            var dguLam = SpecificationTargetCalculator.Calculate(
                6, "DGU+LAM", "G6", GlassBuildType.DguLam,
                Card(GlassBuildType.DguLam, outer: 25m, inner: 25m, third: 18m, asp: 0m),
                100m, 10m);
            Assert.Equal(TargetStatus.MissingAspRate, dguLam.Status);

            var sgu = SpecificationTargetCalculator.Calculate(
                7, "SGU", "G7", GlassBuildType.Sgu,
                Card(GlassBuildType.Sgu, outer: 40m, asp: 0m, yieldFactor: 0.90m),
                100m, 10m);
            Assert.Equal(TargetStatus.UpToDate, sgu.Status);

            var lam = SpecificationTargetCalculator.Calculate(
                8, "LAM", "G8", GlassBuildType.Lam,
                Card(GlassBuildType.Lam, outer: 30m, inner: 20m, asp: 0m,
                     yieldFactor: 0.80m),
                100m, 10m);
            Assert.Equal(TargetStatus.UpToDate, lam.Status);
        }

        // ───────────────────────────────────────────────────────────────
        // Inputs the calculator normalises
        // ───────────────────────────────────────────────────────────────

        [Fact]
        public void A_zero_uplift_falls_back_to_the_fifteen_percent_default()
        {
            // A rate card that has never had its uplift set stores 0, not 15.
            // Pricing at 0% uplift would quote the bare base price and lose the
            // margin the whole target mechanism exists to protect, so the
            // calculator substitutes the default. Asserted by comparing against
            // an explicit 15% card rather than by hard-coding the figure twice.
            var defaulted = SpecificationTargetCalculator.Calculate(
                9, "DGU", "G9", GlassBuildType.Dgu,
                Card(GlassBuildType.Dgu, outer: 25m, inner: 25m, asp: 45m,
                     upliftPercent: 0m),
                130m, 10m);

            var explicitFifteen = SpecificationTargetCalculator.Calculate(
                9, "DGU", "G9", GlassBuildType.Dgu,
                Card(GlassBuildType.Dgu, outer: 25m, inner: 25m, asp: 45m,
                     upliftPercent: SpecificationTargetCalculator.DefaultTargetUpliftPercent),
                130m, 10m);

            Assert.Equal(SpecificationTargetCalculator.DefaultTargetUpliftPercent,
                         defaulted.TargetUpliftPercent);
            Assert.Equal(explicitFifteen.SpecificationTargetPerSqm,
                         defaulted.SpecificationTargetPerSqm);
            Assert.Equal(119.39m, defaulted.SpecificationTargetPerSqm);
        }

        [Fact]
        public void A_negative_area_is_clamped_to_zero()
        {
            // Math.Max(0m, areaSqm). A negative area reaching here would
            // otherwise produce a negative required value and a negative
            // difference, which reads as a credit on a report.
            var result = SpecificationTargetCalculator.Calculate(
                10, "DGU", "G10", GlassBuildType.Dgu,
                Card(GlassBuildType.Dgu, outer: 25m, inner: 25m, asp: 45m),
                givenPricePerSqm: 130m, areaSqm: -5m);

            Assert.Equal(0m, result.AreaSqm);
            Assert.Equal(0m, result.RequiredTargetValue);
            Assert.Equal(0m, result.GivenValue);
            Assert.Equal(0m, result.DifferenceValue);

            // The per-SQM target is still computed — the rate is sound, only the
            // quantity is missing.
            Assert.Equal(119.39m, result.SpecificationTargetPerSqm);
        }
    }
}
