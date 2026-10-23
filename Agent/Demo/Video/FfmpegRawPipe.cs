// ═══════════════════════════════════════════════════════════════════════
//  FILE: Agent/Demo/Video/FfmpegRawPipe.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-02 — real screen recording, slice B
//
//  SINGLE-ENCODE VIDEO PIPE
//  ---------------------------------------------------------------------
//  The old pipeline encoded every frame TWICE:
//
//      capture → JPEG (q84-98) → write to disk → ffmpeg reads JPEGs
//              → decode → H.264
//
//  That is generation loss on top of generation loss, thousands of small
//  file writes, and a stop path that had to wait for the disk. JPEG is
//  also the worst possible intermediate for screen content: it rings
//  around the sharp, high-contrast text that UI recordings are made of.
//
//  This pipe takes RAW BGRA straight from the capture surface and lets
//  x264 perform the ONLY compression in the chain:
//
//      capture → raw BGRA over stdin → H.264
//
//  Nothing hits the disk except the final MP4, so STOP is fast and there
//  are no frame files to clean up.
//
//  BACKPRESSURE IS THE POINT
//  Writing to ffmpeg's stdin blocks when the encoder falls behind. That
//  is desirable — it throttles the capture loop instead of letting an
//  unbounded queue eat RAM — but it must NEVER block the UI thread. The
//  capture loop already runs on a background thread, so the block lands
//  where it is harmless.
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
    public sealed class FfmpegRawPipe : IDisposable
    {
        private Process? _process;
        private Stream? _stdin;
        private readonly object _writeGate = new();
        private bool _disposed;

        public string VideoPath { get; }
        public bool IsOpen { get; private set; }
        public int FramesPiped { get; private set; }
        public long BytesPiped { get; private set; }
        public string? Error { get; private set; }

        // ── PHASE 4 TELEMETRY (2026-09-14) ─────────────────────────────
        // The EXACT parameters the encoder was started with, so the
        // recorder's [RecEncode] line and [RecSaved]'s measured bitrate
        // can be attributed to a concrete configuration.
        public string EncodePreset { get; private set; } = "";
        public int EncodeCrfUsed { get; private set; }
        public int NominalFps { get; private set; }
        public int OutputWidth { get; private set; }
        public int OutputHeight { get; private set; }

        // ── CRISP-DEMO PASS TELEMETRY ─────────────────────────────────────────────────────
        // The PHYSICAL capture geometry, deliberately kept separate from the
        // OUTPUT geometry above. They differ whenever the recorder upscales,
        // and exposing only the output size is precisely what let an upscaled
        // 1080p window be reported as a native 4K capture (Phase 10).
        public int CaptureWidth { get; private set; }
        public int CaptureHeight { get; private set; }

        /// <summary>Output codec handed to ffmpeg (Phase 4).</summary>
        public string CodecName { get; private set; } = "H.264";

        /// <summary>Keyframe interval in frames actually passed as -g (Phase 8).</summary>
        public int Gop { get; private set; }

        /// <summary>Pixel format actually encoded (Phase 7).</summary>
        public string PixelFormat { get; private set; } = "yuv420p";

        /// <summary>
        /// Non-null when a requested setting had to be reduced — currently the
        /// 4:4:4 → 4:2:0 chroma fallback. The quality report must surface this
        /// so it can never overstate what was actually encoded.
        /// </summary>
        public string? ConfigurationFallback { get; private set; }

        /// <summary>The honest capture→output description (Phase 10).</summary>
        public string ResolutionProvenance =>
            VideoEncoder.ResolutionProvenance(CaptureWidth, CaptureHeight, OutputWidth, OutputHeight);

        /// <summary>True when the output was upscaled from a smaller capture.</summary>
        public bool WasUpscaled =>
            VideoEncoder.IsUpscaled(CaptureWidth, CaptureHeight, OutputWidth, OutputHeight);

        // ── PHASE 5 TELEMETRY (2026-09-14) ────────────────────────────
        // The concrete encoder this pipe actually uses (hardware or CPU)
        // and the live process handle for performance sampling.
        public string EncodeName { get; private set; } = "libx264";

        /// <summary>PHASE 6 — the literal ffmpeg command line this pipe was
        /// started with, kept so a later diagnostic can quote the exact
        /// invocation instead of rebuilding it from resolved parameters.</summary>
        public string CommandLine { get; private set; } = "";
        public bool HardwareEncoder { get; private set; }
        public Process? FfmpegProcess { get { try { return _process; } catch { return null; } } }

        // ── ENCODER HEALTH (Phase-2 diagnostics, 2026-09-14) ──────────
        // Observation only: process identity, liveness and exit state so a
        // mid-recording ffmpeg death shows up in the log instead of leaving
        // the HUD on "REC" over a dead encoder.
        private volatile bool _finishRequested;

        public int ProcessId
        {
            get { try { return _process?.Id ?? -1; } catch { return -1; } }
        }

        public bool ProcessAlive
        {
            get { try { return _process != null && !_process.HasExited; } catch { return false; } }
        }

        public int? ExitCode
        {
            get { try { var p = _process; return p != null && p.HasExited ? p.ExitCode : null; } catch { return null; } }
        }

        public DateTime ProcessStartedUtc { get; private set; }

        private FfmpegRawPipe(string videoPath) => VideoPath = videoPath;

        /// <summary>
        /// Starts ffmpeg reading raw BGRA frames from stdin.
        ///
        /// <paramref name="srcW"/>/<paramref name="srcH"/> are the CAPTURE
        /// dimensions and must match the buffer exactly — rawvideo has no
        /// header, so a mismatch shears the picture rather than failing
        /// loudly. <paramref name="outW"/>/<paramref name="outH"/> are the
        /// encoded output size.
        /// </summary>
        public static FfmpegRawPipe? TryStart(
            string videoPath, int fps,
            int srcW, int srcH,
            int outW, int outH,
            int crf,
            VideoEncoderSelection? encoder = null,
            ScreenRecordPixelFormat pixFmt = ScreenRecordPixelFormat.Yuv420)
        {
            string? ffmpeg = FfmpegMp4Composer.Find();
            if (string.IsNullOrWhiteSpace(ffmpeg)) return null;
            encoder ??= VideoEncoderSelection.Libx264;

            fps = Math.Clamp(fps, 1, 60);
            crf = Math.Clamp(crf, 10, 28);
            srcW = Math.Max(2, srcW) & ~1;
            srcH = Math.Max(2, srcH) & ~1;
            outW = Math.Max(2, outW) & ~1;
            outH = Math.Max(2, outH) & ~1;

            Directory.CreateDirectory(Path.GetDirectoryName(videoPath)!);

            // lanczos: the best general-purpose downscaler for text.
            // force_original_aspect_ratio + pad letterboxes instead of
            // stretching, so a window that is not 16:9 keeps its geometry.
            // Chroma (Phase 7): yuv420p is the compatible default. 4:4:4 keeps
            // coloured edges exact but needs a software encoder's High 4:4:4
            // profile and breaks many players, so an incompatible request falls
            // back to 420 rather than failing the encode — and the fallback is
            // recorded below so the quality report never overstates it.
            ScreenRecordPixelFormat effectivePixFmt = pixFmt;
            string? chromaFallback = null;
            if (pixFmt == ScreenRecordPixelFormat.Yuv444 && !VideoEncoder.IsChromaCompatible(pixFmt, encoder))
            {
                effectivePixFmt = ScreenRecordPixelFormat.Yuv420;
                chromaFallback = "4:4:4 requested but unsupported by " + encoder.Name + "; encoded yuv420p";
            }
            string outPixFmt = VideoEncoder.PixelFormatName(effectivePixFmt);

            // 2026-10-05 QUALITY PASS — unsharp added. This is the path that
            // produces the delivered file, and it was the ONLY one of the three
            // encode paths with no sharpening: the JPEG compose path in
            // FfmpegMp4Composer has carried "unsharp=5:5:0.8:5:5:0.0" since the
            // crisp-demo pass. That matters because the scale above is very
            // often an UPSCALE — capture is the window's client area in
            // physical pixels (GdiWindowCapture.Prepare -> GetClientRect), so a
            // maximised window on a 1080p screen yields ~1920x1040 and the
            // default 4K output stretches it 2x. Lanczos cannot invent detail,
            // so upscaled UI text arrives soft; a mild luma unsharp restores
            // perceived edge contrast. Chroma amount stays 0.0 to avoid colour
            // halos on coloured text, matching the compose path.
            string vf =
                $"scale={outW}:{outH}:flags=lanczos:force_original_aspect_ratio=decrease," +
                $"pad={outW}:{outH}:(ow-iw)/2:(oh-ih)/2:color=black," +
                "unsharp=5:5:0.8:5:5:0.0," +
                $"format={outPixFmt}";

            // ── RESOLUTION-AWARE PRESET (4K UHD support, 2026-09-10) ──
            // The x264 preset trades CPU per frame against compression.
            // Because each frame is now written on real capture (see
            // WindowScreenRecorder: one-frame-per-capture) and the file is
            // re-timed at save, the encoder's ONLY job is to keep up with
            // the capture rate — the faster it drains each 33 MB 4K frame,
            // the higher the sustained capture rate and the smoother the
            // result. So the bigger the output, the faster the preset:
            //
            //   ≤1080p : veryfast — plenty of headroom, best compression.
            //   1440p  : superfast.
            //   2160p  : superfast (was ultrafast — see VideoEncoder
            //            .DefaultPresetForOutput). The old comment claimed
            //            "quality is preserved by the low CRF". It is not: CRF
            //            sets how much error the encoder may introduce, while
            //            the preset decides WHICH TOOLS it has to spend that
            //            error budget with, and ultrafast turns off 8x8 DCT,
            //            CABAC, deblocking and aq-mode — the four that carry
            //            fine UI detail. It also left -tune stillimage inert,
            //            because psy-rd needs subme >= 6. Cost is covered by
            //            AdjustPreset below and the FPS guard at start time.
            long outPixels = (long)outW * outH;
            string preset = VideoEncoder.DefaultPresetForOutput(outPixels);

            // ── PHASE 5 (crisp-demo pass): MEASUREMENT-DRIVEN PRESET ──
            // The rung above is a starting point only. EncoderProbe has already
            // test-encoded with the selected encoder and timed it, so there is a
            // real number for what this machine sustains; EstimateEncodeFps
            // scales that measurement to THIS output size. AdjustPreset then
            // steps along the ladder: cheaper when the measurement says the
            // requested rate cannot be held, richer when there is demonstrable
            // spare capacity. Without a measurement the rung is left exactly as
            // it was — no preset here is chosen on theory, and "slow" is never
            // picked blindly.
            double? measuredFps = null;
            int measW = 0, measH = 0;
            string measEnc = "";
            try
            {
                var snap = EncoderProbe.GetSnapshot();
                measuredFps = snap.MeasuredFps;
                measW = snap.MeasuredWidth;
                measH = snap.MeasuredHeight;
                measEnc = snap.MeasuredEncoder;
            }
            catch { /* no probe available — keep the starting rung */ }

            string adjusted = preset;
            if (measuredFps.HasValue && measW > 0 && measH > 0)
            {
                double estAtOutput = VideoEncoder.EstimateEncodeFps(
                    measuredFps.Value, measW, measH, outW, outH);
                adjusted = VideoEncoder.AdjustPreset(preset, estAtOutput, fps);
            }
            if (adjusted != preset)
            {
                preset = adjusted;
                AppLogService.Info(
                    $"[RecPreset] {measEnc} measured {measuredFps:0.##}fps at {measW}x{measH} " +
                    $"→ {VideoEncoder.EstimateEncodeFps(measuredFps!.Value, measW, measH, outW, outH):0.##}fps " +
                    $"estimated at {outW}x{outH}; preset adjusted to {preset} (requested {fps}fps)",
                    new LogContext { Module = "Recording", Operation = "RawPipe" });
            }

            // ── PHASE 5 (2026-09-14): encoder-agnostic argument build ─
            // The quality mechanism differs per encoder (documented in
            // VideoEncoder.BuildQualityArgs): libx264 uses CRF, NVENC
            // uses VBR with a quality target (-cq) and no bitrate
            // ceiling, QSV uses constant CQP. All three are
            // QUALITY-BASED — Step 13 forbids solving starvation with
            // fixed bitrates. libx264-only extras (tune, threads, B-
            // frame count) never apply to the GPU encoders, which have
            // their own B-frame handling and do not need -threads.
            //
            // libx264 / libx265 (CPU path), updated by the crisp-demo pass:
            //   -tune stillimage: sharp static detail — correct for UI.
            //   -g {GopFor(fps)}: TWO-second keyframe interval (was one
            //     second). Fewer keyframes, no recurring bitrate spike on
            //     mostly-static UI, seeking still usable.
            //   -threads 0: every core; 4K needs all of them.
            //   -bf 3: emitted ONLY on presets that use B-frames. x264's
            //     ultrafast sets bframes=0 internally, so passing -bf there
            //     was argument text the encode never applied — VideoEncoder
            //     .PresetSupportsBFrames now suppresses it (Phase 8: remove a
            //     no-op option or document it accurately). Note the previous
            //     comment claimed superfast also disabled B-frames; it does
            //     not — only ultrafast does — so superfast keeps "-bf 3".
            // Plain if/else (not a switch expression with block arms):
            // the arm-block form with local declarations is rejected by
            // some compiler versions — this form is unambiguous.
            // GOP (Phase 8): was "-g {fps}", i.e. a 1-second keyframe interval.
            // Two seconds halves the keyframe count, which for mostly-static UI
            // removes a recurring bitrate spike and lets inter-frames carry the
            // unchanged regions — same quality, less size, steadier bitrate.
            // 30 fps → 60, 60 fps → 120. Seeking stays usable at 2s.
            int gop = VideoEncoder.GopFor(fps);

            string encoderArgs;
            string encoderPreset;
            if (!encoder.Hardware)
            {
                // Software x264/x265. "-bf 3" is only emitted on presets that
                // actually use B-frames: x264's ultrafast sets bframes=0
                // internally, so passing it there produced argument text that
                // the encode never applied. Phase 8 requires such options be
                // removed or documented accurately — this removes it.
                string q = VideoEncoder.BuildQualityArgs(encoder, crf).Args;
                string bfPart = VideoEncoder.BFramesArg(preset);
                if (bfPart.Length > 0) bfPart = " " + bfPart;

                // x264-ONLY extras. "-tune stillimage" is not a valid x265 tune
                // (x265 offers psnr/ssim/grain/fastdecode/zerolatency/animation),
                // so passing it to libx265 would abort the encode instead of
                // degrading it — and HEVC is an opt-in path that must not fail on
                // first use. "-threads 0" is kept on the x264 path for the same
                // reason; x265 sizes its own pools.
                string x264Extras = encoder == VideoEncoderSelection.Libx264
                    ? "-tune stillimage -threads 0 "
                    : "";

                encoderArgs = ($"-c:v {encoder.Name} -preset {preset} " + x264Extras + q +
                               $" -g {gop}" + bfPart).TrimEnd();
                encoderPreset = preset;
            }
            else
            {
                string q = VideoEncoder.BuildQualityArgs(encoder, crf).Args;
                encoderArgs = $"-c:v {encoder.Name} {q} -g {gop}";
                encoderPreset = encoder.Name.Contains("nvenc") ? "p5" : "default";
            }

            string args =
                $"-y -f rawvideo -pix_fmt bgra -s {srcW}x{srcH} -framerate {fps} " +
                $"-use_wallclock_as_timestamps 0 -i - " +
                $"-an -vf \"{vf}\" {encoderArgs} -pix_fmt {outPixFmt} " +
                // Explicit BT.709 tagging: screen capture IS sRGB (≈709).
                // Untagged files leave the primaries to guesswork, and a
                // 4K upscale interpreted as BT.2020 / flat-range
                // somewhere in the edit/upload chain is precisely the
                // "dull, washed" look users report. Tagging is free.
                $"-color_primaries bt709 -color_trc bt709 -colorspace bt709 " +
                $"-movflags +faststart " +
                $"\"{videoPath}\"";

            // ── PHASE 6 (2026-09-16): the exact command, verbatim ─────────
            // [RecEncode] logs the resolved PARAMETERS (encoder, preset, CRF,
            // in/out geometry). This logs the literal command line actually
            // handed to the child process, so diagnosing a recording never
            // depends on reconstructing arguments from several separate
            // fields - and so an option conflict is visible at a glance.
            // One line per recording, never periodic.
            AppLogService.Info(
                $"[RecFfmpegCmd] \"{ffmpeg}\" {args}",
                new LogContext { Module = "Recording", Operation = "FfmpegCommandLine" });

            // Phase 10 — the most important reporting fix in this pass. State
            // plainly whether the encoded resolution was CAPTURED or UPSCALED,
            // so no downstream summary can read a 4K output as a native 4K
            // capture. Upscaling cannot invent detail.
            AppLogService.Info(
                $"[RecResolution] {VideoEncoder.ResolutionProvenance(srcW, srcH, outW, outH)}",
                new LogContext { Module = "Recording", Operation = "ResolutionProvenance" });

            if (chromaFallback != null)
            {
                AppLogService.Warning(
                    $"[RecChroma] {chromaFallback}",
                    new LogContext { Module = "Recording", Operation = "PixelFormatFallback" });
            }

            try
            {
                var toolResult = ProcessStartGuard.TryStartTool(
                    ffmpeg!, args, CorrelationContext.NewId(), redirectStdIn: true);

                var p = toolResult.IsSuccess ? toolResult.Value : null;
                if (p == null)
                {
                    AppLogService.Warning(
                        "Raw video pipe could not start ffmpeg.",
                        new LogContext { Module = "Recording", Operation = "RawPipeStart" });
                    return null;
                }

                var qArgs = VideoEncoder.BuildQualityArgs(encoder, crf);
                var pipe = new FfmpegRawPipe(videoPath)
                {
                    _process = p,
                    _stdin = p.StandardInput.BaseStream,
                    IsOpen = true,
                    ProcessStartedUtc = DateTime.UtcNow,
                    // PHASE 4/5: record the ACTUAL values used (post-clamp,
                    // post-even-rounding, post-encoder-mapping), not the
                    // requested ones.
                    EncodeName = encoder.Name,
                    HardwareEncoder = encoder.Hardware,
                    EncodePreset = encoderPreset,
                    EncodeCrfUsed = qArgs.QualityValue,
                    CommandLine = args,
                    NominalFps = fps,
                    OutputWidth = outW,
                    OutputHeight = outH,
                    CaptureWidth = srcW,
                    CaptureHeight = srcH,
                    CodecName = encoder.Codec == ScreenRecordCodec.Hevc ? "HEVC" : "H.264",
                    Gop = gop,
                    PixelFormat = outPixFmt,
                    ConfigurationFallback = chromaFallback
                };

                // stderr MUST be drained. ffmpeg writes progress there and
                // a full pipe buffer deadlocks the child process — the
                // classic "encoder froze halfway" bug.
            _ = Task.Run(() =>
            {
                try
                {
                    string tail = p.StandardError.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(tail) && p.ExitCode != 0)
                        pipe.Error = Tail(tail);

                    // PHASE-2 DIAGNOSTIC: an early non-zero exit while the
                    // recording is still LIVE is the "silent encoder death"
                    // signal. An exit after finish was requested (normal
                    // stop, bounded-kill) is not logged here.
                    if (!pipe._finishRequested && p.ExitCode != 0)
                    {
                        AppLogService.Warning(
                            $"[RecEncoder] ffmpeg exited DURING RECORDING (pid={pipe.ProcessId}, code={p.ExitCode}): {Tail(tail)}",
                            new LogContext { Module = "Recording", Operation = "EncoderExit" });
                    }
                }
                catch (Exception ex)
                {
                    AppLogService.Debug($"[FfmpegRawPipe] stderr drain: {ex.Message}");
                }
            });

                return pipe;
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Raw video pipe failed to start: {ex.Message}",
                    new LogContext { Module = "Recording", Operation = "RawPipeStart" });
                return null;
            }
        }

        /// <summary>
        /// Writes one raw BGRA frame. Returns false once the encoder has
        /// died, which the recorder treats as a hard stop rather than
        /// silently producing a truncated file.
        /// </summary>
        /// <summary>
        /// Phase 5 — a write at or above this many ms is counted as BLOCKED,
        /// i.e. the caller stood waiting on x264/NVENC backpressure rather than
        /// doing capture work. 25 ms is just over one 40 fps frame interval, so
        /// anything at or above it has cost at least a whole frame.
        /// </summary>
        public const double WriteBlockThresholdMs = 25.0;

        /// <summary>Wall-clock ms of the most recent successful WriteFrame.</summary>
        public double LastWriteMs { get; private set; }

        /// <summary>Sum of all successful write durations, in ms.</summary>
        public double WriteMsTotal { get; private set; }

        /// <summary>Worst single write, in ms — the stall the user would feel.</summary>
        public double WriteMsMax { get; private set; }

        /// <summary>Writes that reached <see cref="WriteBlockThresholdMs"/>.</summary>
        public long WritesBlocked { get; private set; }

        public bool WriteFrame(byte[] buffer, int count)
        {
            if (!IsOpen || _stdin == null || buffer == null) return false;

            // Phase 5 (bottleneck attribution): this write is SYNCHRONOUS and
            // _stdin has no buffer of our own, so it blocks on encoder
            // backpressure — and until now nothing measured it. The adaptive
            // throttle only reads GdiWindowCapture.LastCaptureMs, so a starving
            // encoder was invisible to it: capture looked cheap while the loop
            // was actually waiting here. Timing uses raw timestamps rather than
            // a Stopwatch instance because this is a hot path at up to 60 fps.
            long t0 = Stopwatch.GetTimestamp();

            try
            {
                lock (_writeGate)
                {
                    if (!IsOpen || _stdin == null) return false;
                    _stdin.Write(buffer, 0, count);
                    FramesPiped++;
                    BytesPiped += count;
                }

                double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                LastWriteMs = ms;
                WriteMsTotal += ms;
                if (ms > WriteMsMax) WriteMsMax = ms;
                if (ms >= WriteBlockThresholdMs) WritesBlocked++;
                return true;
            }
            catch (Exception ex)
            {
                // Broken pipe = ffmpeg exited. Record it once and stop.
                IsOpen = false;
                Error ??= ex.Message;
                AppLogService.Warning(
                    $"Raw video pipe closed unexpectedly (pid={ProcessId}, exitCode={ExitCode?.ToString() ?? "n/a"}): {ex.Message}",
                    new LogContext { Module = "Recording", Operation = "RawPipeWrite" });
                return false;
            }
        }

        /// <summary>
        /// Closes stdin and waits for ffmpeg to flush and finalise the MP4.
        ///
        /// The wait is bounded: a hung encoder must not hold STOP open. If
        /// it overruns, the process is killed and whatever was written is
        /// still a playable file thanks to +faststart.
        /// </summary>
        public async Task<bool> FinishAsync(TimeSpan timeout)
        {
            if (_process == null) return false;

            // PHASE-2: mark deliberate end so the stderr drain does not
            // report a normal stop as an in-recording exit.
            _finishRequested = true;

            try
            {
                lock (_writeGate)
                {
                    IsOpen = false;
                    try { _stdin?.Flush(); } catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("lock suppressed");
            }
                    try { _stdin?.Dispose(); } catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("lock suppressed");
            }
                    _stdin = null;
                }

                var proc = _process;
                var waited = await Task.Run(() =>
                {
                    try { return proc.WaitForExit((int)timeout.TotalMilliseconds); }
                    catch { return false; }
                }).ConfigureAwait(false);

                if (!waited)
                {
                    AppLogService.Warning(
                        "Video encoder did not finish in time; terminating.",
                        new LogContext { Module = "Recording", Operation = "RawPipeFinish" });
                    try { proc.Kill(true); } catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("if suppressed");
            }
                    return File.Exists(VideoPath);
                }

                return proc.ExitCode == 0 && File.Exists(VideoPath);
            }
            catch (Exception ex)
            {
                Error ??= ex.Message;
                return File.Exists(VideoPath);
            }
        }

        private static string Tail(string s)
        {
            const int max = 400;
            s = s.Trim();
            return s.Length <= max ? s : s[^max..];
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _finishRequested = true;

            lock (_writeGate)
            {
                IsOpen = false;
                try { _stdin?.Dispose(); } catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("lock suppressed");
            }
                _stdin = null;
            }

            try
            {
                if (_process != null && !_process.HasExited)
                    _process.Kill(true);
            }
            catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("lock suppressed");
            }

            try { _process?.Dispose(); } catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("lock suppressed");
            }
            _process = null;
        }
    }
}
