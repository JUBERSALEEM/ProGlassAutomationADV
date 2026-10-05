using ProGlassAutomation.Agent.Demo.Recording;
using ProGlassAutomation.Agent.Demo.Video;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    /// <summary>
    /// Pins the PHASE 7 delivered-fidelity verdict: comparing what ffprobe
    /// MEASURED in the finished MP4 against what was requested.
    ///
    /// The defect this exists for: the save path validated DURATION
    /// mandatorily, and duration is correct by construction because the
    /// -itsscale retime stretches whatever arrived onto the real elapsed
    /// time. A 4K/60 session that collapsed to 8 real fps therefore produced
    /// a six-minute file that passed every existing check and was reported as
    /// a clean success, while holding ~2880 frames instead of ~21600 and
    /// measuring 0.77 Mbps. Nothing measured smoothness.
    ///
    /// Every expected value below was computed independently and matches the
    /// production functions exactly.
    /// </summary>
    public class RecordingFidelityTests
    {
        private const WindowScreenRecorder.RecordingFidelity Pass =
            WindowScreenRecorder.RecordingFidelity.Pass;
        private const WindowScreenRecorder.RecordingFidelity Degraded =
            WindowScreenRecorder.RecordingFidelity.Degraded;
        private const WindowScreenRecorder.RecordingFidelity Unknown =
            WindowScreenRecorder.RecordingFidelity.Unknown;

        // ── frame-rate verdict ─────────────────────────────────────────

        [Theory]
        [InlineData(60, 60.0, Pass)]
        [InlineData(60, 59.94, Pass)]        // NTSC-style 60000/1001
        [InlineData(60, 57.0, Pass)]         // the accepted lower bound
        [InlineData(60, 56.9, Degraded)]     // just outside it
        [InlineData(60, 30.0, Degraded)]     // one throttle step down
        [InlineData(60, 15.0, Degraded)]
        [InlineData(60, 8.0, Degraded)]      // the throttle floor: the real incident
        [InlineData(30, 30.0, Pass)]
        [InlineData(30, 28.5, Pass)]         // 95% of 30
        [InlineData(30, 28.4, Degraded)]
        public void FrameRate_is_judged_against_a_95_percent_band(
            int requested, double actual, WindowScreenRecorder.RecordingFidelity expected)
            => Assert.Equal(expected, WindowScreenRecorder.ClassifyFrameRate(requested, actual));

        [Fact]
        public void An_unmeasured_frame_rate_is_never_promoted_to_a_pass()
        {
            Assert.Equal(Unknown, WindowScreenRecorder.ClassifyFrameRate(60, null));
            Assert.Equal(Unknown, WindowScreenRecorder.ClassifyFrameRate(60, 0.0));
            Assert.Equal(Unknown, WindowScreenRecorder.ClassifyFrameRate(60, -1.0));
            Assert.Equal(Unknown, WindowScreenRecorder.ClassifyFrameRate(0, 60.0));
        }

        [Fact]
        public void The_tolerance_band_is_exactly_the_one_specified()
        {
            // "Requested 60 FPS: accept approximately 57-60."
            Assert.Equal(0.95, WindowScreenRecorder.FpsPassFraction, 3);
            Assert.Equal(57.0, 60 * WindowScreenRecorder.FpsPassFraction, 3);
        }

        // ── resolution verdict ─────────────────────────────────────────

        [Fact]
        public void Resolution_must_match_exactly()
        {
            Assert.Equal(Pass, WindowScreenRecorder.ClassifyResolution(3840, 2160, 3840, 2160));
            Assert.Equal(Degraded, WindowScreenRecorder.ClassifyResolution(3840, 2160, 1920, 1080));
            // One dimension wrong is still wrong.
            Assert.Equal(Degraded, WindowScreenRecorder.ClassifyResolution(3840, 2160, 3840, 1080));
            Assert.Equal(Degraded, WindowScreenRecorder.ClassifyResolution(3840, 2160, 1920, 2160));
        }

        [Fact]
        public void An_unmeasured_resolution_is_unknown_not_pass()
        {
            Assert.Equal(Unknown, WindowScreenRecorder.ClassifyResolution(3840, 2160, null, null));
            Assert.Equal(Unknown, WindowScreenRecorder.ClassifyResolution(3840, 2160, 3840, null));
            Assert.Equal(Unknown, WindowScreenRecorder.ClassifyResolution(0, 0, 3840, 2160));
        }

        // ── combining verdicts ─────────────────────────────────────────

        [Fact]
        public void One_measured_failure_is_never_masked_by_an_unmeasurable_check()
        {
            Assert.Equal(Degraded, WindowScreenRecorder.WorstFidelity(Pass, Degraded));
            Assert.Equal(Degraded, WindowScreenRecorder.WorstFidelity(Degraded, Pass));
            Assert.Equal(Degraded, WindowScreenRecorder.WorstFidelity(Unknown, Degraded));
            Assert.Equal(Degraded, WindowScreenRecorder.WorstFidelity(Degraded, Unknown));
            Assert.Equal(Unknown, WindowScreenRecorder.WorstFidelity(Pass, Unknown));
            Assert.Equal(Unknown, WindowScreenRecorder.WorstFidelity(Unknown, Unknown));
            Assert.Equal(Pass, WindowScreenRecorder.WorstFidelity(Pass, Pass));
        }

        // ── bitrate ────────────────────────────────────────────────────

        [Fact]
        public void The_reported_incident_measures_well_below_any_real_4K60_capture()
        {
            // 33 MiB over 360.02 s. This is the number that proves the small
            // file was a SYMPTOM of frame count rather than of CRF: a genuine
            // 4K/60 UI capture at CRF 17 lands an order of magnitude higher.
            double? mbps = WindowScreenRecorder.AverageBitrateMbps(33L * 1024 * 1024, 360.02);
            Assert.NotNull(mbps);
            Assert.Equal(0.768913, mbps!.Value, 4);
        }

        [Fact]
        public void Bitrate_is_unknown_when_size_or_duration_is_unusable()
        {
            Assert.Null(WindowScreenRecorder.AverageBitrateMbps(0, 60.0));
            Assert.Null(WindowScreenRecorder.AverageBitrateMbps(-1, 60.0));
            Assert.Null(WindowScreenRecorder.AverageBitrateMbps(1000, 0.0));
            Assert.Null(WindowScreenRecorder.AverageBitrateMbps(1000, -5.0));
        }

        // ── ffprobe rational parsing ───────────────────────────────────

        [Theory]
        [InlineData("60000/1001", 59.94005994)]   // NTSC 59.94
        [InlineData("60/1", 60.0)]
        [InlineData("60", 60.0)]
        [InlineData("30000/1001", 29.97002997)]
        [InlineData("24000/1001", 23.976023976)]
        public void Rational_frame_rates_parse(string text, double expected)
        {
            double? got = FfmpegMp4Composer.ParseRational(text);
            Assert.NotNull(got);
            Assert.Equal(expected, got!.Value, 5);
        }

        [Theory]
        [InlineData("N/A")]      // ffprobe's placeholder when a field is absent
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("0/0")]      // would otherwise divide by zero
        [InlineData("0")]        // a zero rate is not a measurement
        [InlineData("garbage")]
        [InlineData("1/0")]
        public void Unusable_frame_rate_text_is_null_never_zero(string text)
            => Assert.Null(FfmpegMp4Composer.ParseRational(text));

        [Fact]
        public void Null_input_is_null()
            => Assert.Null(FfmpegMp4Composer.ParseRational(null));
    }
}
