using ProGlassAutomation.Agent.Demo.Recording;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    /// <summary>
    /// Pins the timing contract behind the "3 minutes records as 1 minute"
    /// fix. The raw capture pipe stamps frames at a fixed nominal fps, so a
    /// file it produces CLAIMS a duration of framesWritten / nominalFps.
    /// When the encoder cannot sustain nominal fps (always true at 4K/60),
    /// that claimed duration is shorter than reality and playback races.
    ///
    /// <see cref="WindowScreenRecorder.ComputeRetimeScale"/> returns the
    /// -itsscale factor that stretches the claimed duration back onto the
    /// real elapsed time, so the saved MP4 is always the true length.
    /// </summary>
    public class RecordingRetimeTests
    {
        // The exact reported defect: 3 real minutes, but a 60 fps nominal
        // stream in which the encoder only managed ~1 minute of frames
        // (3600 frames / 60 = 60 s claimed). The scale must restore 3×.
        [Fact]
        public void Three_minutes_captured_as_one_is_rescaled_back_to_three()
        {
            // 3600 frames at nominal 60 -> claims 60s; real was 180s.
            double scale = WindowScreenRecorder.ComputeRetimeScale(
                encodedFrames: 3600, nominalFps: 60, realSeconds: 180.0);

            Assert.Equal(3.0, scale, 3);

            // Applying the scale reproduces the real duration.
            double claimed = 3600 / 60.0;
            Assert.Equal(180.0, claimed * scale, 3);
        }

        [Theory]
        // encoder kept up exactly -> no rescale needed
        [InlineData(1800, 60, 30.0, 1.0)]
        // encoder at half rate over 60s -> 2x stretch
        [InlineData(1800, 60, 60.0, 2.0)]
        // 30fps nominal, encoder at ~10fps over 90s (900 frames) -> 3x
        [InlineData(900, 30, 90.0, 3.0)]
        public void Scale_is_real_over_claimed(
            int frames, int nominalFps, double realSeconds, double expected)
        {
            double scale = WindowScreenRecorder.ComputeRetimeScale(
                frames, nominalFps, realSeconds);
            Assert.Equal(expected, scale, 3);
        }

        [Theory]
        [InlineData(0, 60, 180.0)]      // no frames yet
        [InlineData(3600, 0, 180.0)]    // no nominal fps yet
        [InlineData(3600, 60, 0.0)]     // no real duration yet
        [InlineData(3600, 60, 0.01)]    // sub-threshold duration
        public void Meaningless_inputs_return_identity(
            int frames, int nominalFps, double realSeconds)
        {
            Assert.Equal(
                1.0,
                WindowScreenRecorder.ComputeRetimeScale(frames, nominalFps, realSeconds),
                6);
        }

        // PHASE 3 (2026-09-14): the reported defect at full severity —
        // 5 real minutes, ~300 frames written at nominal 60 (~1 fps
        // sustained) → raw file claims 5 s, needs ×60. The old 50× clamp
        // delivered 250 s (an acceptance-test FAILURE); the 3600× bound
        // corrects it to the full 300 s.
        [Fact]
        public void Five_minutes_captured_as_five_is_rescaled_back_to_five()
        {
            double scale = WindowScreenRecorder.ComputeRetimeScale(
                encodedFrames: 300, nominalFps: 60, realSeconds: 300.0);

            Assert.Equal(60.0, scale, 3);

            double claimed = 300 / 60.0;
            Assert.Equal(300.0, claimed * scale, 3);
        }

        [Fact]
        public void Scale_is_clamped_to_sane_bounds()
        {
            // Absurdly few frames for a long recording would imply a huge
            // stretch; clamp keeps it at the 3600× bound (PHASE 3 — was
            // 50×, which silently under-corrected the 5-min-as-5-s case).
            double hi = WindowScreenRecorder.ComputeRetimeScale(
                encodedFrames: 1, nominalFps: 60, realSeconds: 100000.0);
            Assert.Equal(3600.0, hi, 6);

            // Far more frames than real time (impossible in practice) would
            // imply a tiny stretch; clamp floors it at 0.02.
            double lo = WindowScreenRecorder.ComputeRetimeScale(
                encodedFrames: 1_000_000, nominalFps: 60, realSeconds: 1.0);
            Assert.Equal(0.02, lo, 6);
        }

        // When the encoder keeps up (scale within ~1.5% of 1.0) the save
        // path must treat it as a no-op so we never pay for a pointless
        // rescale. This asserts the boundary the service relies on.
        [Theory]
        [InlineData(6000, 60, 100.0, true)]   // exactly kept up -> identity
        [InlineData(5950, 60, 100.0, true)]   // 0.8% off -> still ~1
        [InlineData(5000, 60, 100.0, false)]  // 20% short -> needs rescale
        public void Near_unity_is_effectively_identity(
            int frames, int nominalFps, double realSeconds, bool nearOne)
        {
            double scale = WindowScreenRecorder.ComputeRetimeScale(
                frames, nominalFps, realSeconds);
            Assert.Equal(nearOne, System.Math.Abs(scale - 1.0) <= 0.015);
        }
    }
}
