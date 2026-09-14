// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/VideoEncoderSelectionTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// PHASE 5 (2026-09-14) — pins the hardware-encoder selection contract:
// the fallback chain must ALWAYS end in libx264 (the pipeline that has
// always worked), explicit hardware failures must be reported with a
// reason, and the per-encoder quality mapping must stay QUALITY-BASED
// (CRF / VBR+CQ / CQP — never a fixed bitrate).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Demo.Video;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class VideoEncoderSelectionTests
    {
        // ── Auto walks NVENC → QSV → libx264 ──────────────────────────

        [Fact]
        public void Auto_prefers_NVENC_when_both_available()
        {
            var r = VideoEncoder.Resolve(ScreenRecordEncoder.Auto, nvencUsable: true, qsvUsable: true);
            Assert.Equal(VideoEncoderSelection.Nvenc, r.Selection);
            Assert.True(r.Selection.Hardware);
            Assert.False(r.Fallback);
        }

        [Fact]
        public void Auto_uses_QSV_when_NVENC_unavailable()
        {
            var r = VideoEncoder.Resolve(ScreenRecordEncoder.Auto, nvencUsable: false, qsvUsable: true);
            Assert.Equal(VideoEncoderSelection.Qsv, r.Selection);
            Assert.False(r.Fallback); // Auto walking its chain is normal
        }

        [Fact]
        public void Auto_falls_back_to_libx264_when_no_hardware()
        {
            var r = VideoEncoder.Resolve(ScreenRecordEncoder.Auto, nvencUsable: false, qsvUsable: false);
            Assert.Equal(VideoEncoderSelection.Libx264, r.Selection);
            Assert.False(r.Selection.Hardware);
            Assert.False(r.Fallback);
        }

        // ── Explicit choices ───────────────────────────────────────────

        [Fact]
        public void Cpu_always_selects_libx264()
        {
            var r = VideoEncoder.Resolve(ScreenRecordEncoder.Cpu, nvencUsable: true, qsvUsable: true);
            Assert.Equal(VideoEncoderSelection.Libx264, r.Selection);
            Assert.False(r.Fallback);
        }

        [Fact]
        public void Nvenc_requested_and_available_selects_nvenc()
        {
            var r = VideoEncoder.Resolve(ScreenRecordEncoder.Nvenc, nvencUsable: true, qsvUsable: false);
            Assert.Equal(VideoEncoderSelection.Nvenc, r.Selection);
            Assert.False(r.Fallback);
        }

        [Fact]
        public void Nvenc_requested_unavailable_falls_back_to_qsv_with_reason()
        {
            var r = VideoEncoder.Resolve(ScreenRecordEncoder.Nvenc, nvencUsable: false, qsvUsable: true);
            Assert.Equal(VideoEncoderSelection.Qsv, r.Selection);
            Assert.True(r.Fallback);
            Assert.Contains("NVENC", r.Reason);
        }

        [Fact]
        public void Nvenc_requested_unavailable_falls_back_to_cpu_with_reason()
        {
            var r = VideoEncoder.Resolve(ScreenRecordEncoder.Nvenc, nvencUsable: false, qsvUsable: false);
            Assert.Equal(VideoEncoderSelection.Libx264, r.Selection);
            Assert.True(r.Fallback);
            Assert.Contains("NVENC", r.Reason);
        }

        [Fact]
        public void Qsv_requested_unavailable_falls_back_to_cpu_with_reason()
        {
            var r = VideoEncoder.Resolve(ScreenRecordEncoder.Qsv, nvencUsable: false, qsvUsable: false);
            Assert.Equal(VideoEncoderSelection.Libx264, r.Selection);
            Assert.True(r.Fallback);
            Assert.Contains("QSV", r.Reason);
        }

        // ── Quality mapping: quality-based for EVERY encoder ──────────

        [Fact]
        public void Libx264_uses_crf()
        {
            var (args, rc, v) = VideoEncoder.BuildQualityArgs(VideoEncoderSelection.Libx264, 18);
            Assert.Equal("CRF", rc);
            Assert.Equal(18, v);
            Assert.Contains("-crf 18", args);
            Assert.DoesNotContain("-b:v", args);   // never a fixed bitrate
        }

        [Fact]
        public void Nvenc_uses_vbr_cq_without_bitrate_ceiling()
        {
            var (args, rc, v) = VideoEncoder.BuildQualityArgs(VideoEncoderSelection.Nvenc, 18);
            Assert.Equal("VBR+CQ", rc);
            Assert.Equal(20, v);                     // crf+2, documented offset
            Assert.Contains("-rc vbr", args);
            Assert.Contains("-b:v 0", args);         // no bitrate ceiling
            Assert.Contains("-cq 20", args);
        }

        [Fact]
        public void Qsv_uses_cqp()
        {
            var (args, rc, v) = VideoEncoder.BuildQualityArgs(VideoEncoderSelection.Qsv, 18);
            Assert.Equal("CQP", rc);
            Assert.Equal(21, v);                     // crf+3, documented offset
            Assert.Contains("-rc cqp", args);
            Assert.Contains("-global_quality 21", args);
        }

        [Fact]
        public void Quality_values_stay_in_sane_ranges()
        {
            foreach (int crf in new[] { 10, 13, 18, 23, 28 })
            {
                Assert.InRange(VideoEncoder.BuildQualityArgs(VideoEncoderSelection.Nvenc, crf).QualityValue, 8, 46);
                Assert.InRange(VideoEncoder.BuildQualityArgs(VideoEncoderSelection.Qsv, crf).QualityValue, 5, 50);
                Assert.InRange(VideoEncoder.BuildQualityArgs(VideoEncoderSelection.Libx264, crf).QualityValue, 10, 28);
            }
        }

        // ── Throughput estimate (the FPS guard's input) ───────────────

        [Fact]
        public void Estimate_scales_with_pixel_count()
        {
            // Measured 12 fps at 1080p → 4K has 4× the pixels → ~3 fps.
            double est4k = VideoEncoder.EstimateEncodeFps(12, 1920, 1080, 3840, 2160);
            Assert.Equal(3.0, est4k, 1);

            // Measured 30 fps at 4K → 1080p has 1/4 the pixels → clamps
            // toward the (capped) 120, not a raw 120×... it is 120 here.
            double est1080 = VideoEncoder.EstimateEncodeFps(30, 3840, 2160, 1920, 1080);
            Assert.Equal(120.0, est1080, 1);
        }

        [Fact]
        public void Estimate_returns_zero_on_bogus_input()
        {
            Assert.Equal(0, VideoEncoder.EstimateEncodeFps(0, 1920, 1080, 3840, 2160));
            Assert.Equal(0, VideoEncoder.EstimateEncodeFps(30, 0, 1080, 3840, 2160));
        }
    }
}
