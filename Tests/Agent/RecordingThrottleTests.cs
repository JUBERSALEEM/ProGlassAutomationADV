using ProGlassAutomation.Agent.Demo.Recording;
using System;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    /// <summary>
    /// Pins the adaptive-throttle policy behind the "a 6-minute 4K/60
    /// recording produced only ~33 MB" defect.
    ///
    /// The capture loop measures what each frame costs and halves the frame
    /// rate when capture overruns its budget, so the UI thread keeps real
    /// gaps. That protection is correct. What made it a defect is that the
    /// rate could only ever go DOWN: one slow stretch - a GC pause, a
    /// PrintWindow fallback, one busy moment - locked the entire remainder of
    /// the recording at the 8 fps floor.
    ///
    /// The file size followed from that, not from CRF. ~8 fps over 360 s is
    /// ~2880 frames instead of ~21600, and mostly-static ERP UI at CRF 17
    /// compresses to roughly the reported 33 MB. The save path then retimes
    /// to the true elapsed duration (<see
    /// cref="WindowScreenRecorder.ComputeRetimeScale"/>), so the MP4 was the
    /// right LENGTH and passed the mandatory duration validation while
    /// containing an eighth of the frames it claimed. Duration was never the
    /// symptom; smoothness was, and nothing validated it.
    ///
    /// These tests exercise the policy through the same pure functions and
    /// constants the loop uses, so they hold without a window, a dispatcher
    /// or ffmpeg.
    /// </summary>
    public class RecordingThrottleTests
    {
        /// <summary>
        /// Mirrors RawLoopAsync's streak logic exactly, using the recorder's
        /// own constants and step functions. <paramref name="overBudget"/> is
        /// capture costing more than <see
        /// cref="WindowScreenRecorder.UiBudgetFraction"/> of the interval;
        /// <paramref name="comfortable"/> is costing no more than <see
        /// cref="WindowScreenRecorder.RecoveryBudgetFraction"/> of it.
        /// </summary>
        private static void Step(
            ref int fps, ref double slowStreak, ref double fastStreak,
            int requested, bool overBudget, bool comfortable)
        {
            if (overBudget)
            {
                slowStreak++;
                if (slowStreak >= WindowScreenRecorder.SlowStreakToHalve
                    && fps > WindowScreenRecorder.MinThrottledFps)
                {
                    fps = WindowScreenRecorder.StepThrottleDown(fps);
                    slowStreak = 0;
                    fastStreak = 0;
                }
                return;
            }

            slowStreak = Math.Max(0, slowStreak - 0.25);
            if (comfortable) fastStreak++;
            else fastStreak = Math.Max(0, fastStreak - 1);

            if (fastStreak >= WindowScreenRecorder.FastStreakToRecover
                && fps < requested)
            {
                fps = WindowScreenRecorder.StepThrottleUp(fps, requested);
                fastStreak = 0;
                slowStreak = 0;
            }
        }

        // ── the step functions in isolation ────────────────────────────

        [Theory]
        [InlineData(60, 30)]
        [InlineData(30, 15)]
        [InlineData(16, 8)]
        public void BackOff_halves_the_rate(int current, int expected)
            => Assert.Equal(expected, WindowScreenRecorder.StepThrottleDown(current));

        [Theory]
        [InlineData(15, 8)]    // halving would give 7; the floor wins
        [InlineData(10, 8)]
        [InlineData(8, 8)]     // already at the floor: stays put
        [InlineData(1, 8)]
        public void BackOff_never_drops_below_the_floor(int current, int expected)
            => Assert.Equal(expected, WindowScreenRecorder.StepThrottleDown(current));

        [Fact]
        public void Recovery_climbs_toward_the_request_and_stops_there()
        {
            int fps = WindowScreenRecorder.MinThrottledFps;
            var path = new System.Collections.Generic.List<int> { fps };

            for (int i = 0; i < 32; i++)
            {
                int next = WindowScreenRecorder.StepThrottleUp(fps, 60);
                Assert.True(next >= fps, "recovery must never lower the rate");
                Assert.True(next <= 60, "recovery must never exceed the request");
                if (next == fps) break;
                fps = next;
                path.Add(fps);
            }

            Assert.Equal(60, fps);
            // 8 -> 12 -> 18 -> 27 -> 41 -> 60: a modest, monotone climb.
            Assert.Equal(new[] { 8, 12, 18, 27, 41, 60 }, path);
        }

        [Theory]
        [InlineData(60, 60, 60)]
        [InlineData(90, 60, 60)]   // above the request: clamped down to it
        [InlineData(1, 1, 1)]
        public void Recovery_never_exceeds_the_requested_rate(
            int current, int requested, int expected)
            => Assert.Equal(expected, WindowScreenRecorder.StepThrottleUp(current, requested));

        [Fact]
        public void Recovery_from_a_rate_of_one_still_climbs()
        {
            // The +1 floor matters: 1 * 1.5 rounds to 2, but a rate of 0 or a
            // pathological 1 must still make progress rather than stick.
            Assert.Equal(2, WindowScreenRecorder.StepThrottleUp(1, 60));
        }

        // ── THE REGRESSION ─────────────────────────────────────────────

        [Fact]
        public void A_transient_slowdown_no_longer_locks_the_recording_to_the_floor()
        {
            const int requested = 60;
            int fps = requested;
            double slow = 0, fast = 0;

            // A 30-frame expensive burst (e.g. the PrintWindow fallback
            // kicking in while a dialog animates). This is enough to walk
            // 60 -> 30 -> 15 -> 8.
            for (int i = 0; i < 30; i++)
                Step(ref fps, ref slow, ref fast, requested, overBudget: true, comfortable: false);

            Assert.Equal(WindowScreenRecorder.MinThrottledFps, fps);

            // Capture then settles for the rest of a 6-minute session.
            // Before the fix this loop left fps pinned at 8 forever.
            for (int i = 0; i < requested * 300; i++)
                Step(ref fps, ref slow, ref fast, requested, overBudget: false, comfortable: true);

            Assert.Equal(requested, fps);
        }

        [Fact]
        public void A_machine_that_is_slow_the_whole_time_stays_throttled()
        {
            // Recovery must NOT defeat the UI protection: if every frame keeps
            // overrunning, the rate stays down. Responsiveness outranks fps.
            const int requested = 60;
            int fps = requested;
            double slow = 0, fast = 0;

            for (int i = 0; i < requested * 300; i++)
                Step(ref fps, ref slow, ref fast, requested, overBudget: true, comfortable: false);

            Assert.Equal(WindowScreenRecorder.MinThrottledFps, fps);
        }

        [Fact]
        public void Marginal_frames_that_are_under_budget_but_not_comfortable_do_not_recover()
        {
            // Sitting between RecoveryBudgetFraction and UiBudgetFraction is
            // exactly the oscillation the hysteresis exists to prevent: the
            // rate must hold, not creep upward and immediately fall back.
            const int requested = 60;
            int fps = requested;
            double slow = 0, fast = 0;

            // Drop to the floor first.
            for (int i = 0; i < 30; i++)
                Step(ref fps, ref slow, ref fast, requested, overBudget: true, comfortable: false);
            Assert.Equal(WindowScreenRecorder.MinThrottledFps, fps);

            // Then run a long session of merely-acceptable frames.
            for (int i = 0; i < requested * 300; i++)
                Step(ref fps, ref slow, ref fast, requested, overBudget: false, comfortable: false);

            Assert.Equal(WindowScreenRecorder.MinThrottledFps, fps);
        }

        [Fact]
        public void BackOff_is_faster_than_recovery()
        {
            // Documented intent: oscillating between rates looks worse than
            // holding a lower one, so reaching the floor must take far fewer
            // frames than climbing back.
            const int requested = 60;

            int down = 0, fps = requested;
            double slow = 0, fast = 0;
            while (fps > WindowScreenRecorder.MinThrottledFps && down < 1000)
            {
                Step(ref fps, ref slow, ref fast, requested, true, false);
                down++;
            }

            int up = 0;
            fps = WindowScreenRecorder.MinThrottledFps;
            slow = 0; fast = 0;
            while (fps < requested && up < 100000)
            {
                Step(ref fps, ref slow, ref fast, requested, false, true);
                up++;
            }

            Assert.True(up > down * 10,
                $"recovery took {up} frames vs {down} to back off - the hysteresis " +
                "is too weak and the rate will oscillate");
        }
    }
}
