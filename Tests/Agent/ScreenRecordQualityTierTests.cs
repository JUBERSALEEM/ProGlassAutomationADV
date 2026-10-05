// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ScreenRecordQualityTierTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// PHASE 4 (2026-09-14) — pins the quality-tier × resolution CRF matrix.
// The critical contract: VeryHigh must reproduce the PRE-Phase-4 CRFs
// exactly (14/13/12/12), so the quality-first setting the app shipped
// with since 2026-09-04 remains one click away and nothing regresses
// for users who want it.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Demo.Recording;
using System.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class ScreenRecordQualityTierTests
    {
        [Fact]
        public void Default_tier_is_High()
        {
            var s = new ScreenRecordSettings();
            Assert.Equal(ScreenRecordQualityTier.High, s.QualityTier);
        }

        [Theory]
        // columns: tier, 720p, 1080p, 2K, 4K
        [InlineData(ScreenRecordQualityTier.Standard, 23, 22, 21, 21)]
        [InlineData(ScreenRecordQualityTier.High,     19, 18, 17, 17)]
        [InlineData(ScreenRecordQualityTier.VeryHigh, 14, 13, 12, 12)]
        public void Crf_matrix_matches_the_documented_values(
            ScreenRecordQualityTier tier, int c720, int c1080, int c2k, int c4k)
        {
            Assert.Equal(c720,
                new ScreenRecordSettings { QualityTier = tier, Quality = ScreenRecordQuality.Hd720 }.EncodeCrf);
            Assert.Equal(c1080,
                new ScreenRecordSettings { QualityTier = tier, Quality = ScreenRecordQuality.FullHd }.EncodeCrf);
            Assert.Equal(c2k,
                new ScreenRecordSettings { QualityTier = tier, Quality = ScreenRecordQuality.Qhd2K }.EncodeCrf);
            Assert.Equal(c4k,
                new ScreenRecordSettings { QualityTier = tier, Quality = ScreenRecordQuality.Uhd4K }.EncodeCrf);
        }

        [Fact]
        public void VeryHigh_is_exactly_the_pre_phase4_configuration()
        {
            // The shipped CRFs before Phase 4 were 14/13/12/12.
            int[] actual =
            {
                new ScreenRecordSettings { QualityTier = ScreenRecordQualityTier.VeryHigh, Quality = ScreenRecordQuality.Hd720 }.EncodeCrf,
                new ScreenRecordSettings { QualityTier = ScreenRecordQualityTier.VeryHigh, Quality = ScreenRecordQuality.FullHd }.EncodeCrf,
                new ScreenRecordSettings { QualityTier = ScreenRecordQualityTier.VeryHigh, Quality = ScreenRecordQuality.Qhd2K }.EncodeCrf,
                new ScreenRecordSettings { QualityTier = ScreenRecordQualityTier.VeryHigh, Quality = ScreenRecordQuality.Uhd4K }.EncodeCrf
            };
            Assert.Equal(new[] { 14, 13, 12, 12 }, actual);
        }

        [Theory]
        [InlineData(ScreenRecordQualityTier.Standard, ScreenRecordQuality.Hd720)]
        [InlineData(ScreenRecordQualityTier.Standard, ScreenRecordQuality.Uhd4K)]
        [InlineData(ScreenRecordQualityTier.High, ScreenRecordQuality.Hd720)]
        [InlineData(ScreenRecordQualityTier.High, ScreenRecordQuality.Uhd4K)]
        [InlineData(ScreenRecordQualityTier.VeryHigh, ScreenRecordQuality.Hd720)]
        [InlineData(ScreenRecordQualityTier.VeryHigh, ScreenRecordQuality.Uhd4K)]
        public void Crf_stays_inside_the_pipe_range(
            ScreenRecordQualityTier tier, ScreenRecordQuality quality)
        {
            int crf = new ScreenRecordSettings { QualityTier = tier, Quality = quality }.EncodeCrf;
            // FfmpegRawPipe clamps to [10, 28] — the settings must never
            // push it past those bounds and silently change the encode.
            Assert.InRange(crf, 10, 28);
        }

        [Fact]
        public void Unknown_tier_clamps_to_High()
        {
            var s = new ScreenRecordSettings { QualityTier = (ScreenRecordQualityTier)99 };
            s.Clamp();
            Assert.Equal(ScreenRecordQualityTier.High, s.QualityTier);
        }

        [Fact]
        public void Clone_preserves_the_tier()
        {
            var a = new ScreenRecordSettings
            {
                QualityTier = ScreenRecordQualityTier.VeryHigh,
                Quality = ScreenRecordQuality.Uhd4K
            };
            var b = a.Clone();
            a.QualityTier = ScreenRecordQualityTier.Standard;
            Assert.Equal(ScreenRecordQualityTier.VeryHigh, b.QualityTier);
        }

        [Fact]
        public void Summary_names_the_tier_and_crf()
        {
            var s = new ScreenRecordSettings
            {
                QualityTier = ScreenRecordQualityTier.High,
                Quality = ScreenRecordQuality.Uhd4K
            };
            Assert.Contains("High", s.Summary);
            Assert.Contains($"CRF{s.EncodeCrf}", s.Summary);
        }
    }
}
