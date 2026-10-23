// ═══════════════════════════════════════
// FILE: Tests/Agent/CrispDemoPolicyTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// CRISP-DEMO PASS (Phase 16) — pins the PURE policy added for the
// professional-demo quality work. Everything here is side-effect free:
// no ffmpeg, no display, no Windows, no hardware. The hardware-dependent
// questions (which preset actually sustains which frame rate, what CRF
// costs in Mbps, HEVC vs H.264 at equal quality) are NOT answered here
// and deliberately cannot be — they are answered by running
// tools/RecordingQualitySweep.ps1 on the target machine.
//
// What IS pinned here:
//   • persisted enum values never move (backward compatibility)
//   • the existing quality tiers keep their exact CRFs (no regression)
//   • preset selection is MEASUREMENT-driven and never guesses
//   • the GOP is two seconds, not one
//   • -bf is suppressed exactly where x264 makes it a no-op
//   • capture resolution and output resolution stay distinguishable
//   • 4:4:4 is refused on hardware encoders instead of failing the encode
//   • codec mapping is idempotent (no silent NVENC→CPU downgrade)
// ═══════════════════════════════════════

using ProGlassAutomation.Agent.Demo.Recording;
using ProGlassAutomation.Agent.Demo.Video;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class CrispDemoPolicyTests
    {
        // ══════════════════════════════════════════════════════════
        //  PERSISTED VALUES MUST NEVER MOVE (Phase 3 / Phase 20)
        // ══════════════════════════════════════════════════════════

        [Fact]
        public void PersistedTierValues_AreUnchanged_AndNewTierIsAdditive()
        {
            // A config written by an older build must still resolve, so the
            // three original tiers keep their numbers and SoftwareDemo is
            // appended — never inserted.
            Assert.Equal(0, (int)ScreenRecordQualityTier.Standard);
            Assert.Equal(1, (int)ScreenRecordQualityTier.High);
            Assert.Equal(2, (int)ScreenRecordQualityTier.VeryHigh);
            Assert.Equal(3, (int)ScreenRecordQualityTier.SoftwareDemo);
        }

        [Fact]
        public void PersistedCodecAndPixelFormatValues_StartAtZero()
        {
            Assert.Equal(0, (int)ScreenRecordCodec.H264);
            Assert.Equal(1, (int)ScreenRecordCodec.Hevc);
            Assert.Equal(0, (int)ScreenRecordPixelFormat.Yuv420);
            Assert.Equal(1, (int)ScreenRecordPixelFormat.Yuv444);
        }

        // ══════════════════════════════════════════════════════════
        //  NO REGRESSION ON THE EXISTING QUALITY MATRIX
        // ══════════════════════════════════════════════════════════

        [Fact]
        public void ExistingTierCrfBaselines_AreUntouched()
        {
            Assert.Equal(22, BaseCrf(ScreenRecordQualityTier.Standard));
            Assert.Equal(18, BaseCrf(ScreenRecordQualityTier.High));
            Assert.Equal(13, BaseCrf(ScreenRecordQualityTier.VeryHigh));
        }

        [Fact]
        public void NewTier_IsAppended_AndDefaultsAreUnchanged()
        {
            Assert.Equal(4, ScreenRecordSettings.QualityTiers.Count);
            Assert.Equal(18, BaseCrf(ScreenRecordQualityTier.SoftwareDemo));

            var s = new ScreenRecordSettings();
            Assert.Equal(ScreenRecordQualityTier.High, s.QualityTier);   // still High
            Assert.Equal(ScreenRecordCodec.H264, s.Codec);               // still H.264
            Assert.Equal(ScreenRecordPixelFormat.Yuv420, s.PixelFormat); // still 4:2:0
        }

        [Theory]
        [InlineData(ScreenRecordQuality.Hd720, 19)]
        [InlineData(ScreenRecordQuality.FullHd, 18)]
        [InlineData(ScreenRecordQuality.Qhd2K, 17)]
        [InlineData(ScreenRecordQuality.Uhd4K, 17)]
        public void NewTier_FollowsTheSameResolutionOffset(ScreenRecordQuality q, int expected)
        {
            var s = new ScreenRecordSettings { QualityTier = ScreenRecordQualityTier.SoftwareDemo, Quality = q };
            Assert.Equal(expected, s.EncodeCrf);
            Assert.InRange(s.EncodeCrf, 10, 28);
        }

        [Fact]
        public void Clone_RoundTripsTheNewSettings()
        {
            var s = new ScreenRecordSettings
            {
                QualityTier = ScreenRecordQualityTier.SoftwareDemo,
                Codec = ScreenRecordCodec.Hevc,
                PixelFormat = ScreenRecordPixelFormat.Yuv444
            };
            var c = s.Clone();
            Assert.Equal(ScreenRecordQualityTier.SoftwareDemo, c.QualityTier);
            Assert.Equal(ScreenRecordCodec.Hevc, c.Codec);
            Assert.Equal(ScreenRecordPixelFormat.Yuv444, c.PixelFormat);
        }

        [Fact]
        public void Clamp_ResetsUnrecognisedCodecAndChroma_ToCompatibleDefaults()
        {
            var s = new ScreenRecordSettings
            {
                Codec = (ScreenRecordCodec)99,
                PixelFormat = (ScreenRecordPixelFormat)99
            };
            s.Clamp();
            Assert.Equal(ScreenRecordCodec.H264, s.Codec);
            Assert.Equal(ScreenRecordPixelFormat.Yuv420, s.PixelFormat);
        }

        [Fact]
        public void Clamp_KeepsTheNewTier_InsteadOfResettingIt()
        {
            var s = new ScreenRecordSettings { QualityTier = ScreenRecordQualityTier.SoftwareDemo };
            s.Clamp();
            Assert.Equal(ScreenRecordQualityTier.SoftwareDemo, s.QualityTier);
        }

        [Fact]
        public void Summary_ShowsOptIns_OnlyWhenTheyAreOn()
        {
            var plain = new ScreenRecordSettings { QualityTier = ScreenRecordQualityTier.SoftwareDemo };
            Assert.Contains("Crisp Demo", plain.Summary);
            Assert.Contains("CRF" + plain.EncodeCrf, plain.Summary);
            Assert.DoesNotContain("HEVC", plain.Summary);
            Assert.DoesNotContain("4:4:4", plain.Summary);

            var opted = new ScreenRecordSettings
            {
                Codec = ScreenRecordCodec.Hevc,
                PixelFormat = ScreenRecordPixelFormat.Yuv444
            };
            Assert.Contains("HEVC", opted.Summary);
            Assert.Contains("4:4:4", opted.Summary);
        }

        // ══════════════════════════════════════════════════════════
        //  PRESET MAPPING (Phase 5)
        // ══════════════════════════════════════════════════════════

        [Theory]
        // 4K was "ultrafast" until the 2026-10-05 quality pass: ultrafast
        // disables 8x8 DCT, CABAC, deblocking and aq-mode, which is what made
        // 4K screen text go soft, and it left "-tune stillimage" inert
        // (psy-rd needs subme >= 6; ultrafast is subme 0). superfast restores
        // all four. AdjustPreset still demotes to ultrafast on a measurement
        // that says the machine cannot hold the rate.
        [InlineData(3840L * 2160, "superfast")]
        [InlineData(3840L * 2160 - 1, "superfast")]   // just below 4K
        [InlineData(2560L * 1440, "superfast")]
        [InlineData(2560L * 1440 - 1, "veryfast")]    // just below 1440p
        [InlineData(1920L * 1080, "veryfast")]
        [InlineData(1280L * 720, "veryfast")]
        public void DefaultPresetForOutput_MatchesTheLadder(long pixels, string expected)
        {
            Assert.Equal(expected, VideoEncoder.DefaultPresetForOutput(pixels));
        }

        [Fact]
        public void PresetLadder_IsOrderedCheapestToRichest_AndStopsAtMedium()
        {
            var l = VideoEncoder.X264PresetLadder;
            Assert.Equal(
                new[] { "ultrafast", "superfast", "veryfast", "faster", "fast", "medium" }, l);
            Assert.Equal(0, VideoEncoder.PresetIndex("ultrafast"));
            Assert.Equal(5, VideoEncoder.PresetIndex("medium"));
        }

        [Theory]
        [InlineData("ultrafast", 0)]
        [InlineData("ULTRAFAST", 0)]      // case-insensitive
        [InlineData("medium", 5)]
        [InlineData("slow", -1)]          // deliberately NOT on the ladder
        [InlineData("placebo", -1)]
        [InlineData("", -1)]
        [InlineData(null, -1)]
        public void PresetIndex_RejectsAnythingOffTheLadder(string? preset, int expected)
        {
            Assert.Equal(expected, VideoEncoder.PresetIndex(preset));
        }

        [Fact]
        public void PresetAt_ClampsAtBothEnds()
        {
            Assert.Equal("ultrafast", VideoEncoder.PresetAt(-5));
            Assert.Equal("ultrafast", VideoEncoder.PresetAt(0));
            Assert.Equal("medium", VideoEncoder.PresetAt(5));
            Assert.Equal("medium", VideoEncoder.PresetAt(99));
        }

        // ── the heart of Phase 5: never guess, always measure ──

        [Fact]
        public void AdjustPreset_WithoutAMeasurement_ChangesNothing()
        {
            // No probe result ⇒ no opinion. This is the guarantee that the
            // preset is never chosen on theory.
            Assert.Equal("veryfast", VideoEncoder.AdjustPreset("veryfast", (double?)null, 60));
            Assert.Equal("veryfast", VideoEncoder.AdjustPreset("veryfast", 0, 60));
            Assert.Equal("veryfast", VideoEncoder.AdjustPreset("veryfast", -12, 60));
            Assert.Equal("veryfast", VideoEncoder.AdjustPreset("veryfast", 999, 0));
        }

        [Fact]
        public void AdjustPreset_StepsCheaper_WhenTheRateCannotBeHeld()
        {
            Assert.Equal("superfast", VideoEncoder.AdjustPreset("veryfast", 50, 60));
            Assert.Equal("ultrafast", VideoEncoder.AdjustPreset("superfast", 10, 30));
        }

        [Fact]
        public void AdjustPreset_StepsRicher_OnlyAboveTheHeadroom()
        {
            // 60 × 1.15 is EXACTLY 69.0 in IEEE-754, so this boundary is exact
            // and needs no epsilon.
            Assert.Equal(69.0, 60 * VideoEncoder.PresetHeadroom, 6);
            Assert.Equal("veryfast", VideoEncoder.AdjustPreset("veryfast", 68.99, 60)); // below ⇒ hold
            Assert.Equal("faster", VideoEncoder.AdjustPreset("veryfast", 69.0, 60));    // at ⇒ step up
            Assert.Equal("faster", VideoEncoder.AdjustPreset("veryfast", 200, 60));
        }

        [Fact]
        public void AdjustPreset_HoldsInsideTheDeadBand()
        {
            // Sustains the request but without spare capacity ⇒ leave it alone.
            Assert.Equal("veryfast", VideoEncoder.AdjustPreset("veryfast", 65, 60));
            Assert.Equal("veryfast", VideoEncoder.AdjustPreset("veryfast", 60, 60));
        }

        [Fact]
        public void AdjustPreset_ClampsAtBothEnds_OfTheLadder()
        {
            Assert.Equal("ultrafast", VideoEncoder.AdjustPreset("ultrafast", 10, 60));
            Assert.Equal("medium", VideoEncoder.AdjustPreset("medium", 500, 60));
        }

        [Fact]
        public void AdjustPreset_NeverPicksAPresetOffTheLadder()
        {
            // An unknown rung is passed through untouched rather than being
            // silently reinterpreted as something else.
            Assert.Equal("bogus", VideoEncoder.AdjustPreset("bogus", 100, 60));
            Assert.Equal("slow", VideoEncoder.AdjustPreset("slow", 100, 60));
        }

        [Fact]
        public void AdjustPreset_NeverSelectsSlowOrSlower_OnItsOwn()
        {
            // Phase 5 forbids blindly choosing "slow": the richest rung
            // reachable is "medium", and only a measurement can put it there.
            for (int i = 0; i < VideoEncoder.X264PresetLadder.Length; i++)
            {
                string r = VideoEncoder.AdjustPreset(VideoEncoder.X264PresetLadder[i], 100000, 30);
                Assert.DoesNotContain("slow", r);
                Assert.DoesNotContain("placebo", r);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  GOP (Phase 8)
        // ══════════════════════════════════════════════════════════

        [Fact]
        public void KeyframeInterval_IsTwoSeconds_NotOne()
        {
            Assert.Equal(2.0, VideoEncoder.KeyframeIntervalSeconds, 6);
        }

        [Theory]
        [InlineData(30, 60)]     // the two cases the brief names explicitly
        [InlineData(60, 120)]
        [InlineData(24, 48)]
        [InlineData(40, 80)]
        [InlineData(50, 100)]
        public void GopFor_IsTwoSecondsWorthOfFrames(int fps, int expected)
        {
            Assert.Equal(expected, VideoEncoder.GopFor(fps));
        }

        [Theory]
        [InlineData(0, 60)]      // non-positive falls back to a 30 fps assumption
        [InlineData(-30, 60)]
        [InlineData(1, 2)]       // clamped to the minimum
        [InlineData(400, 600)]   // clamped to the maximum
        public void GopFor_ClampsSaneBounds(int fps, int expected)
        {
            Assert.Equal(expected, VideoEncoder.GopFor(fps));
        }

        // ══════════════════════════════════════════════════════════
        //  DEAD-OPTION REMOVAL (Phase 8)
        // ══════════════════════════════════════════════════════════

        [Theory]
        [InlineData("ultrafast", false)]   // x264 sets bframes=0 internally here
        [InlineData("superfast", true)]    // …but NOT here (the old comment was wrong)
        [InlineData("veryfast", true)]
        [InlineData("faster", true)]
        [InlineData("fast", true)]
        [InlineData("medium", true)]
        [InlineData("slow", false)]        // unknown rung ⇒ emit nothing rather than lie
        [InlineData(null, false)]
        public void PresetSupportsBFrames_OnlyUltrafastDisablesThem(string? preset, bool expected)
        {
            Assert.Equal(expected, VideoEncoder.PresetSupportsBFrames(preset));
        }

        [Fact]
        public void BFramesArg_IsEmptyExactlyWhereItWouldBeANoOp()
        {
            Assert.Equal("", VideoEncoder.BFramesArg("ultrafast"));
            Assert.Equal("-bf 3", VideoEncoder.BFramesArg("veryfast"));
            Assert.Equal("-bf 3", VideoEncoder.BFramesArg("medium"));
            Assert.Equal("", VideoEncoder.BFramesArg("slow"));
            Assert.Equal(3, VideoEncoder.BFrameCount);
        }

        // ══════════════════════════════════════════════════════════
        //  RESOLUTION PROVENANCE (Phase 10 — the most important fix)
        // ══════════════════════════════════════════════════════════

        [Theory]
        [InlineData(1920, 1080, 3840, 2160, true)]   // the real-world case: 4K out of 1080p
        [InlineData(1920, 1080, 2560, 1440, true)]
        [InlineData(3840, 2160, 3840, 2160, false)]  // genuinely native
        [InlineData(3840, 2160, 1920, 1080, false)]  // downscaled
        [InlineData(0, 0, 3840, 2160, false)]        // unknown capture ⇒ never claim upscaled
        public void IsUpscaled_SeparatesCaptureFromOutput(
            int cw, int ch, int ow, int oh, bool expected)
        {
            Assert.Equal(expected, VideoEncoder.IsUpscaled(cw, ch, ow, oh));
        }

        [Fact]
        public void ResolutionProvenance_NeverCallsAnUpscaleNative()
        {
            Assert.Equal(
                "3840x2160 output UPSCALED from 1920x1080 capture",
                VideoEncoder.ResolutionProvenance(1920, 1080, 3840, 2160));

            Assert.Equal(
                "native 3840x2160 capture (1:1, no rescale)",
                VideoEncoder.ResolutionProvenance(3840, 2160, 3840, 2160));

            Assert.Equal(
                "1920x1080 output downscaled from 3840x2160 capture",
                VideoEncoder.ResolutionProvenance(3840, 2160, 1920, 1080));

            Assert.Equal(
                "resolution provenance unknown",
                VideoEncoder.ResolutionProvenance(0, 0, 3840, 2160));
        }

        [Fact]
        public void ResolutionProvenance_DoesNotContainTheWordNative_WhenUpscaled()
        {
            string p = VideoEncoder.ResolutionProvenance(1920, 1080, 3840, 2160);
            Assert.DoesNotContain("native", p, System.StringComparison.OrdinalIgnoreCase);
            Assert.Contains("UPSCALED", p);
        }

        // ══════════════════════════════════════════════════════════
        //  CHROMA (Phase 7)
        // ══════════════════════════════════════════════════════════

        [Fact]
        public void PixelFormatName_MapsToTheFfmpegTokens()
        {
            Assert.Equal("yuv420p", VideoEncoder.PixelFormatName(ScreenRecordPixelFormat.Yuv420));
            Assert.Equal("yuv444p", VideoEncoder.PixelFormatName(ScreenRecordPixelFormat.Yuv444));
        }

        [Fact]
        public void IsChromaCompatible_Allows420Everywhere_And444OnlyOnSoftware()
        {
            // 4:2:0 is universally fine.
            Assert.True(VideoEncoder.IsChromaCompatible(
                ScreenRecordPixelFormat.Yuv420, VideoEncoderSelection.Nvenc));
            Assert.True(VideoEncoder.IsChromaCompatible(
                ScreenRecordPixelFormat.Yuv420, VideoEncoderSelection.Libx264));

            // 4:4:4 needs the High 4:4:4 profile — software encoders only.
            Assert.True(VideoEncoder.IsChromaCompatible(
                ScreenRecordPixelFormat.Yuv444, VideoEncoderSelection.Libx264));
            Assert.True(VideoEncoder.IsChromaCompatible(
                ScreenRecordPixelFormat.Yuv444, VideoEncoderSelection.Libx265));
            Assert.False(VideoEncoder.IsChromaCompatible(
                ScreenRecordPixelFormat.Yuv444, VideoEncoderSelection.Nvenc));
            Assert.False(VideoEncoder.IsChromaCompatible(
                ScreenRecordPixelFormat.Yuv444, VideoEncoderSelection.Qsv));
            Assert.False(VideoEncoder.IsChromaCompatible(
                ScreenRecordPixelFormat.Yuv444, VideoEncoderSelection.HevcNvenc));
            Assert.False(VideoEncoder.IsChromaCompatible(
                ScreenRecordPixelFormat.Yuv444, VideoEncoderSelection.HevcQsv));
        }

        // ══════════════════════════════════════════════════════════
        //  CODEC SELECTION (Phase 4)
        // ══════════════════════════════════════════════════════════

        [Fact]
        public void WithCodec_IsANoOpForH264()
        {
            Assert.Same(VideoEncoderSelection.Libx264,
                VideoEncoder.WithCodec(VideoEncoderSelection.Libx264, ScreenRecordCodec.H264));
            Assert.Same(VideoEncoderSelection.Nvenc,
                VideoEncoder.WithCodec(VideoEncoderSelection.Nvenc, ScreenRecordCodec.H264));
            Assert.Same(VideoEncoderSelection.Qsv,
                VideoEncoder.WithCodec(VideoEncoderSelection.Qsv, ScreenRecordCodec.H264));
        }

        [Fact]
        public void WithCodec_MapsEachHardwareVendorToItsHevcCounterpart()
        {
            Assert.Same(VideoEncoderSelection.Libx265,
                VideoEncoder.WithCodec(VideoEncoderSelection.Libx264, ScreenRecordCodec.Hevc));
            Assert.Same(VideoEncoderSelection.HevcNvenc,
                VideoEncoder.WithCodec(VideoEncoderSelection.Nvenc, ScreenRecordCodec.Hevc));
            Assert.Same(VideoEncoderSelection.HevcQsv,
                VideoEncoder.WithCodec(VideoEncoderSelection.Qsv, ScreenRecordCodec.Hevc));
        }

        [Fact]
        public void WithCodec_IsIdempotent_AndNeverDowngradesHardwareToCpu()
        {
            // Mapping twice must not turn NVENC HEVC into a CPU encode.
            var once = VideoEncoder.WithCodec(VideoEncoderSelection.Nvenc, ScreenRecordCodec.Hevc);
            var twice = VideoEncoder.WithCodec(once, ScreenRecordCodec.Hevc);
            Assert.Same(VideoEncoderSelection.HevcNvenc, once);
            Assert.Same(VideoEncoderSelection.HevcNvenc, twice);
            Assert.True(twice.Hardware);

            Assert.Same(VideoEncoderSelection.HevcQsv,
                VideoEncoder.WithCodec(VideoEncoderSelection.HevcQsv, ScreenRecordCodec.Hevc));
            Assert.Same(VideoEncoderSelection.Libx265,
                VideoEncoder.WithCodec(VideoEncoderSelection.Libx265, ScreenRecordCodec.Hevc));
        }

        [Fact]
        public void HevcEncoderNames_AreTheFfmpegTokens()
        {
            Assert.Equal("libx265", VideoEncoderSelection.Libx265.Name);
            Assert.Equal("hevc_nvenc", VideoEncoderSelection.HevcNvenc.Name);
            Assert.Equal("hevc_qsv", VideoEncoderSelection.HevcQsv.Name);
            Assert.Equal(ScreenRecordCodec.Hevc, VideoEncoderSelection.Libx265.Codec);
            Assert.Equal(ScreenRecordCodec.H264, VideoEncoderSelection.Libx264.Codec);
        }

        [Theory]
        [InlineData("libx265")]
        [InlineData("hevc_nvenc")]
        [InlineData("hevc_qsv")]
        public void ByName_RoundTripsTheHevcSelections(string name)
        {
            Assert.Equal(name, VideoEncoderSelection.ByName(name).Name);
            Assert.Equal(ScreenRecordCodec.Hevc, VideoEncoderSelection.ByName(name).Codec);
        }

        [Fact]
        public void ByName_StillDefaultsToLibx264_ForUnknownAndNull()
        {
            Assert.Same(VideoEncoderSelection.Libx264, VideoEncoderSelection.ByName("nonsense"));
            Assert.Same(VideoEncoderSelection.Libx264, VideoEncoderSelection.ByName(null));
            Assert.Same(VideoEncoderSelection.Libx264, VideoEncoderSelection.ByName(""));
        }

        [Fact]
        public void HevcQualityOffset_IsExplicitlyUncalibrated_Zero()
        {
            // Pinned at 0 so that enabling HEVC cannot silently shift quality
            // before the sweep has produced a real number. Changing this
            // constant is a deliberate, reviewed act.
            Assert.Equal(0, VideoEncoder.HevcQualityOffset);
        }

        // ══════════════════════════════════════════════════════════
        //  CRF / CQ SELECTION — quality-based, never a fixed bitrate
        // ══════════════════════════════════════════════════════════

        [Fact]
        public void BuildQualityArgs_H264Paths_AreUnchangedByThisPass()
        {
            // Guards the pre-existing contract (see VideoEncoderSelectionTests).
            Assert.Equal("-crf 18", VideoEncoder.BuildQualityArgs(VideoEncoderSelection.Libx264, 18).Args);
            Assert.Equal(18, VideoEncoder.BuildQualityArgs(VideoEncoderSelection.Libx264, 18).QualityValue);
            Assert.Equal("CRF", VideoEncoder.BuildQualityArgs(VideoEncoderSelection.Libx264, 18).RateControl);

            Assert.Equal("-rc vbr -b:v 0 -cq 20 -preset p5",
                VideoEncoder.BuildQualityArgs(VideoEncoderSelection.Nvenc, 18).Args);
            Assert.Equal("-rc cqp -global_quality 21",
                VideoEncoder.BuildQualityArgs(VideoEncoderSelection.Qsv, 18).Args);
        }

        [Theory]
        [InlineData(13, "-crf 13", 13)]
        [InlineData(17, "-crf 17", 17)]
        [InlineData(18, "-crf 18", 18)]
        [InlineData(22, "-crf 22", 22)]
        public void BuildQualityArgs_Libx265_StaysQualityBased(int crf, string args, int value)
        {
            var r = VideoEncoder.BuildQualityArgs(VideoEncoderSelection.Libx265, crf);
            Assert.Equal(args, r.Args);
            Assert.Equal("CRF", r.RateControl);
            Assert.Equal(value, r.QualityValue);
            Assert.DoesNotContain("-b:v", r.Args);       // no forced bitrate
            Assert.DoesNotContain("-maxrate", r.Args);
            Assert.DoesNotContain("-bufsize", r.Args);
        }

        [Theory]
        [InlineData(13, 15, 16)]
        [InlineData(18, 20, 21)]
        [InlineData(22, 24, 25)]
        public void BuildQualityArgs_HevcHardware_KeepsTheSameOffsetsAsH264(int crf, int cq, int gq)
        {
            var n = VideoEncoder.BuildQualityArgs(VideoEncoderSelection.HevcNvenc, crf);
            Assert.Equal($"-rc vbr -b:v 0 -cq {cq} -preset p5", n.Args);
            Assert.Equal(cq, n.QualityValue);
            Assert.InRange(n.QualityValue, 8, 46);

            var q = VideoEncoder.BuildQualityArgs(VideoEncoderSelection.HevcQsv, crf);
            Assert.Equal($"-rc cqp -global_quality {gq}", q.Args);
            Assert.Equal(gq, q.QualityValue);
            Assert.InRange(q.QualityValue, 5, 50);
        }

        [Fact]
        public void NoQualityMapping_IntroducesABitrateCeiling()
        {
            // Phase 2: CRF/CQ stays the primary mechanism; nothing here may
            // smuggle in -maxrate/-bufsize to chase a file-size target. The
            // "-b:v 0" on NVENC is the unconstrained-VBR idiom, not a cap.
            foreach (var enc in new[]
                     {
                         VideoEncoderSelection.Libx264, VideoEncoderSelection.Nvenc,
                         VideoEncoderSelection.Qsv, VideoEncoderSelection.Libx265,
                         VideoEncoderSelection.HevcNvenc, VideoEncoderSelection.HevcQsv
                     })
            {
                string a = VideoEncoder.BuildQualityArgs(enc, 18).Args;
                Assert.DoesNotContain("-maxrate", a);
                Assert.DoesNotContain("-bufsize", a);
                Assert.DoesNotContain("-minrate", a);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ENCODER FALLBACK STILL TERMINATES IN A WORKING ENCODER
        // ══════════════════════════════════════════════════════════

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void Resolve_AlwaysEndsAtAUsableEncoder_EvenWithHevcRequested(
            bool nvencUsable, bool qsvUsable)
        {
            foreach (ScreenRecordEncoder req in new[]
                     {
                         ScreenRecordEncoder.Auto, ScreenRecordEncoder.Cpu,
                         ScreenRecordEncoder.Nvenc, ScreenRecordEncoder.Qsv
                     })
            {
                var r = VideoEncoder.Resolve(req, nvencUsable, qsvUsable);
                var mapped = VideoEncoder.WithCodec(r.Selection, ScreenRecordCodec.Hevc);

                // Never empty, never an unknown name.
                Assert.False(string.IsNullOrWhiteSpace(mapped.Name));
                Assert.True(
                    ReferenceEquals(mapped, VideoEncoderSelection.Libx265)
                    || ReferenceEquals(mapped, VideoEncoderSelection.HevcNvenc)
                    || ReferenceEquals(mapped, VideoEncoderSelection.HevcQsv));

                // A hardware request with no hardware available must land on
                // the CPU encoder — the chain always terminates.
                if (!nvencUsable && !qsvUsable)
                    Assert.Same(VideoEncoderSelection.Libx265, mapped);
            }
        }

        [Fact]
        public void EstimateEncodeFps_ScalesByPixelCount_ForThePresetDecision()
        {
            // A 1080p measurement of 200 fps estimates to 50 fps at 4K
            // (one quarter the pixels) — which is BELOW a 60 fps request, so
            // the ladder must step cheaper rather than hold.
            double est = VideoEncoder.EstimateEncodeFps(200, 1920, 1080, 3840, 2160);
            Assert.Equal(50.0, est, 6);
            Assert.Equal("superfast", VideoEncoder.AdjustPreset("veryfast", est, 60));
            Assert.Equal("ultrafast",
                VideoEncoder.AdjustPreset("ultrafast", est, 60));  // already cheapest ⇒ clamps
        }

        // ══════════════════════════════════════════════════════════
        //  STAGE ATTRIBUTION (Phase 5) — which stage owns the frame budget
        // ══════════════════════════════════════════════════════════

        [Theory]
        [InlineData(0.0, 0.0, 0.0, 0.0, "no-data")]
        [InlineData(10.0, 10.0, 10.0, 10.0, "distributed")]
        [InlineData(5.0, 5.0, 60.0, 5.0, "encoder-write")]
        [InlineData(60.0, 5.0, 5.0, 5.0, "gdi-blit")]
        [InlineData(5.0, 60.0, 5.0, 5.0, "marshal-copy")]
        [InlineData(5.0, 5.0, 5.0, 60.0, "loop-overhead")]
        [InlineData(0.0, 0.0, 0.0, 10.0, "loop-overhead")]
        [InlineData(0.0, 30.0, 0.0, 0.0, "marshal-copy")]
        [InlineData(0.0, 0.0, 30.0, 0.0, "encoder-write")]
        [InlineData(30.0, 0.0, 0.0, 0.0, "gdi-blit")]
        [InlineData(49.0, 25.0, 25.0, 1.0, "distributed")]   // 49/100 < half
        [InlineData(50.0, 25.0, 25.0, 0.0, "gdi-blit")]      // 50/100 == half
        public void DominantCaptureStage_AttributesTheFrameBudget(
            double blit, double copy, double write, double other, string expected)
        {
            Assert.Equal(expected,
                WindowScreenRecorder.DominantCaptureStage(blit, copy, write, other));
        }

        [Fact]
        public void DominantCaptureStage_RefusesToNameABottleneck_WhenCostIsSpread()
        {
            // An even split reports "distributed". Naming a single stage there
            // would send the next engineering phase after the wrong thing —
            // which is exactly the mistake this phase exists to avoid.
            Assert.Equal("distributed", WindowScreenRecorder.DominantCaptureStage(20, 20, 20, 20));
            Assert.Equal("distributed", WindowScreenRecorder.DominantCaptureStage(0.4, 0.3, 0.2, 0.1));
        }

        [Fact]
        public void DominantCaptureStage_TieAtExactlyHalf_BreaksTowardTheActionableStage()
        {
            // 37.5 / 75 == 0.5 exactly, so it is attributed rather than
            // "distributed"; the documented tie-break prefers blit over copy.
            Assert.Equal("gdi-blit", WindowScreenRecorder.DominantCaptureStage(37.5, 37.5, 0, 0));
        }

        [Fact]
        public void DominantCaptureStage_HandlesDegenerateInput()
        {
            Assert.Equal("no-data", WindowScreenRecorder.DominantCaptureStage(0, 0, 0, 0));
            Assert.Equal("no-data", WindowScreenRecorder.DominantCaptureStage(-1, -1, -1, -1));
            Assert.Equal("gdi-blit", WindowScreenRecorder.DominantCaptureStage(1e-9, 0, 0, 0));
        }

        [Fact]
        public void WriteBlockThreshold_IsExactlyOneFortiethOfASecond()
        {
            // 25 ms is one 40 fps frame interval, so a write at or above it has
            // cost at least a whole frame — the definition of "blocked".
            Assert.Equal(25.0, FfmpegRawPipe.WriteBlockThresholdMs, 6);
            Assert.Equal(25.0, 1000.0 / 40.0, 6);
        }

        // ── helpers ──────────────────────────────────────────────
        private static int BaseCrf(ScreenRecordQualityTier tier)
        {
            foreach (var o in ScreenRecordSettings.QualityTiers)
            {
                if (o.Tier == tier) return o.CrfBase;
            }
            return -1;   // no such tier ⇒ the caller's Assert.Equal fails loudly
        }
    }
}
