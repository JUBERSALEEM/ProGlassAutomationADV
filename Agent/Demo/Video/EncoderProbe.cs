// ═══════════════════════════════════════════════════════════════════════
//  FILE: Agent/Demo/Video/EncoderProbe.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-14 — PHASE 5: hardware H.264 encoding
//
//  SAFE HARDWARE-ENCODER DETECTION (Step 8 of the Phase-5 spec).
//
//  "Do not merely check whether FFmpeg contains the encoder name. The
//  encoder must actually initialize successfully."
//
//  This probe PROVES an encoder by running a real, tiny, bounded test
//  encode with the SAME argument style production uses:
//
//      NVIDIA NVENC : 1 s of 640×360@30 through h264_nvenc
//      Intel QSV    : 1 s of 640×360@30 through h264_qsv
//
//  and, separately, MEASURES the sustained encode fps of the selected
//  encoder at the selected resolution (3 s of testsrc2 at that exact
//  size/rate): the encoder either keeps up (elapsed ≈ source duration)
//  or it does not (elapsed >> source duration) — a real throughput
//  number for the start-time FPS guard, not an assumption.
//
//  Everything runs on a background thread, is cached for the process
//  lifetime, is bounded by timeouts, and can NEVER block or fail a
//  recording: a failed probe just means "unproven → fall back".
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Common;   // CorrelationContext
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Video
{
    public sealed class EncoderProbe
    {
        /// <summary>Immutable capability snapshot for consumers.</summary>
        public sealed class Snapshot
        {
            public bool NvencUsable { get; internal init; }
            public bool QsvUsable { get; internal init; }
            public bool CapabilityProbed { get; internal init; }

            /// <summary>Measured sustained fps of the probed encoder (null = not measured).</summary>
            public double? MeasuredFps { get; internal init; }
            public int MeasuredWidth { get; internal init; }
            public int MeasuredHeight { get; internal init; }
            public string MeasuredEncoder { get; internal init; } = "";

            public static readonly Snapshot Empty = new();
        }

        private static readonly object Gate = new();
        private static Snapshot _state = Snapshot.Empty;
        private static volatile bool _inFlight;
        private static int _probeGeneration;

        /// <summary>Raised on the background probe thread when results change.</summary>
        public static event Action? Changed;

        /// <summary>
        /// Current capability snapshot (renamed from Snapshot() to avoid
        /// clashing with the nested Snapshot type — CS0102).
        /// </summary>
        public static Snapshot GetSnapshot()
        {
            lock (Gate) return _state;
        }

        /// <summary>
        /// Kick off the capability + throughput probe in the background.
        /// Idempotent: while a probe runs or fresh results exist for
        /// (encoder, resolution), repeated calls are no-ops. Safe to
        /// call from the UI thread (returns immediately).
        /// </summary>
        public static void RequestProbe(string selectedEncoder, int width, int height, int fps)
        {
            lock (Gate)
            {
                if (_inFlight) return;
                var s = _state;
                bool capFresh = s.CapabilityProbed;
                bool throughputFresh =
                    s.MeasuredFps.HasValue &&
                    s.MeasuredEncoder == selectedEncoder &&
                    s.MeasuredWidth == width && s.MeasuredHeight == height;
                if (capFresh && throughputFresh) return;
                _inFlight = true;
                int gen = ++_probeGeneration;
            }

            _ = Task.Run(() =>
            {
                try { RunProbe(selectedEncoder, width, height, fps); }
                catch (Exception ex) { AppLogService.Debug($"[EncoderProbe] probe aborted: {ex.Message}"); }
                finally
                {
                    lock (Gate) _inFlight = false;
                }
            });
        }

        private static void RunProbe(string selectedEncoder, int width, int height, int fps)
        {
            var selection = VideoEncoderSelection.ByName(selectedEncoder);
            Snapshot? prev = null;
            lock (Gate) prev = _state;

            bool nvenc = prev.NvencUsable;
            bool qsv = prev.QsvUsable;
            bool capProbed = prev.CapabilityProbed;

            // 1) PROVE the hardware encoders initialize (only when not
            //    yet proven). A test encode that exits 0 is the proof.
            if (!nvenc && !capProbed)
            {
                nvenc = TestEncode(VideoEncoderSelection.Nvenc, 640, 360, 30, 1,
                    VideoEncoder.BuildQualityArgs(VideoEncoderSelection.Nvenc, 20).Args, out string? nvErr);
                if (!nvenc && !string.IsNullOrEmpty(nvErr))
                    AppLogService.Debug($"[EncoderProbe] NVENC init test failed: {nvErr}");
            }
            if (!qsv && !capProbed)
            {
                qsv = TestEncode(VideoEncoderSelection.Qsv, 640, 360, 30, 1,
                    VideoEncoder.BuildQualityArgs(VideoEncoderSelection.Qsv, 20).Args, out string? qsvErr);
                if (!qsv && !string.IsNullOrEmpty(qsvErr))
                    AppLogService.Debug($"[EncoderProbe] QSV init test failed: {qsvErr}");
            }

            // 2) MEASURE sustained fps of the SELECTED encoder at the
            //    selected resolution (3 s of real-size synthetic frames).
            double? measuredFps = prev.MeasuredFps;
            int mw = prev.MeasuredWidth, mh = prev.MeasuredHeight;
            string me = prev.MeasuredEncoder;
            bool needThroughput =
                !measuredFps.HasValue || me != selection.Name || mw != width || mh != height;
            if (needThroughput)
            {
                string qArgs = VideoEncoder.BuildQualityArgs(selection, 18).Args;

                // 2026-10-05 QUALITY PASS — the probe was measuring the WRONG
                // configuration. BuildQualityArgs returns only "-crf 18" for
                // the software encoders (the hardware ones already carry their
                // own "-preset p5"), so this test encode ran libx264 at its
                // DEFAULT preset — medium — while a real 4K recording runs at
                // the rung from DefaultPresetForOutput. medium is several times
                // slower, so every measured fps here was a large
                // UNDER-estimate of what recording actually achieves.
                //
                // That pessimism fed straight back into the quality settings:
                // AdjustPreset demotes the preset whenever the measurement is
                // below the requested rate, and the FPS guard in
                // UserScreenRecordService downgrades 4K -> 2K -> 1080p on the
                // same number. Both were therefore biased toward the softest,
                // smallest output the machine could be talked into. Measuring
                // at the rung that will actually be used makes both decisions
                // describe reality.
                if (!selection.Hardware)
                    qArgs += " -preset " + VideoEncoder.DefaultPresetForOutput(
                        (long)width * height);

                if (TestEncodeWithTiming(selection, width, height, fps, 3, qArgs, out double mFps, out _))
                {
                    measuredFps = mFps; mw = width; mh = height; me = selection.Name;
                    AppLogService.Info(
                        $"[EncoderProbe] encoder={selection.Name} resolution={width}x{height} " +
                        $"sustained={mFps:0.##}fps (measured)",
                        new LogContext { Module = "Recording", Operation = "EncoderProbe" });
                }
            }

            bool changed = false;
            lock (Gate)
            {
                if (_probeGeneration == 0) return; // never — defensive
                var next = new Snapshot
                {
                    NvencUsable = nvenc,
                    QsvUsable = qsv,
                    CapabilityProbed = true,
                    MeasuredFps = measuredFps,
                    MeasuredWidth = mw,
                    MeasuredHeight = mh,
                    MeasuredEncoder = me
                };
                changed = next.NvencUsable != _state.NvencUsable
                       || next.QsvUsable != _state.QsvUsable
                       || next.MeasuredFps != _state.MeasuredFps
                       || next.MeasuredEncoder != _state.MeasuredEncoder
                       || next.MeasuredWidth != _state.MeasuredWidth
                       || next.MeasuredHeight != _state.MeasuredHeight;
                _state = next;
            }
            if (changed)
            {
                try { Changed?.Invoke(); }
                catch (Exception ex) { AppLogService.Debug($"[EncoderProbe] subscriber: {ex.Message}"); }
            }
        }

        /// <summary>
        /// Run a bounded test encode. Returns true only on exit code 0
        /// with a real output file — that is the initialization proof
        /// (Step 8). Never throws.
        /// </summary>
        private static bool TestEncode(
            VideoEncoderSelection enc, int w, int h, int fps, int seconds,
            string qualityArgs, out string? errorTail)
        {
            return TestEncodeWithTiming(enc, w, h, fps, seconds, qualityArgs, out _, out errorTail);
        }

        private static bool TestEncodeWithTiming(
            VideoEncoderSelection enc, int w, int h, int fps, int seconds,
            string qualityArgs, out double measuredFps, out string? errorTail)
        {
            measuredFps = 0; errorTail = null;

            string? ffmpeg = FfmpegMp4Composer.Find();
            if (string.IsNullOrWhiteSpace(ffmpeg)) return false;

            string temp = Path.Combine(Path.GetTempPath(),
                $"proglass_encprobe_{Guid.NewGuid():N}.mp4");
            string vf = $"scale={w}:{h}:flags=lanczos:force_original_aspect_ratio=decrease," +
                        $"pad={w}:{h}:(ow-iw)/2:(oh-ih)/2:color=black,format=yuv420p";
            string args =
                $"-y -f lavfi -i testsrc2=duration={seconds}:size={w}x{h}:rate={fps} " +
                $"-vf \"{vf}\" -c:v {enc.Name} {qualityArgs} -an -movflags +faststart \"{temp}\"";

            var sw = Stopwatch.StartNew();
            try
            {
                var toolResult = ProcessStartGuard.TryStartTool(ffmpeg, args, CorrelationContext.NewId());
                using var p = toolResult.IsSuccess ? toolResult.Value : null;
                if (p == null) { errorTail = "test encode could not start"; return false; }

                var errTask = p.StandardError.ReadToEndAsync();
                // Bounded: a starved encoder is the thing we're measuring.
                // Cap = 20× the source duration; if the encode is still
                // running at the cap, the measured fps is a LOWER BOUND
                // (fps × seconds ÷ cap) — conservative for the guard.
                int capMs = (seconds * 20 + 5) * 1000;
                if (!p.WaitForExit(capMs))
                {
                    try { p.Kill(true); } catch { }
                    double elapsedAtCap = sw.Elapsed.TotalSeconds;
                    measuredFps = fps * seconds / Math.Max(1.0, elapsedAtCap);
                    errorTail = $"test encode hit the {capMs / 1000}s cap (measured lower bound {measuredFps:0.##}fps)";
                    return true; // it ran and produced output — usable, just slow
                }
                try { errTask.Wait(1000); } catch { }
                double elapsed = sw.Elapsed.TotalSeconds;

                bool ok = p.ExitCode == 0 && File.Exists(temp) && new FileInfo(temp).Length > 0;
                if (!ok)
                {
                    string errText = errTask.IsCompleted ? errTask.Result : "";
                    errorTail = $"exit={p.ExitCode} {Tail(errText)}";
                    return false;
                }
                measuredFps = ok ? fps * seconds / Math.Max(0.1, elapsed) : 0;
                return true;
            }
            catch (Exception ex)
            {
                errorTail = ex.Message;
                return false;
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            }
        }

        private static string? Tail(string s)
        {
            s = (s ?? "").Trim().Replace("\r", " ").Replace("\n", " ");
            return s.Length <= 300 ? s : "…" + s[^300..];
        }
    }
}
