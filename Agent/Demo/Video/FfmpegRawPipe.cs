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
            int crf)
        {
            string? ffmpeg = FfmpegMp4Composer.Find();
            if (string.IsNullOrWhiteSpace(ffmpeg)) return null;

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
            string vf =
                $"scale={outW}:{outH}:flags=lanczos:force_original_aspect_ratio=decrease," +
                $"pad={outW}:{outH}:(ow-iw)/2:(oh-ih)/2:color=black,format=yuv420p";

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
            //   2160p  : ultrafast — 4K raw is ~33 MB/frame; only the
            //            cheapest preset sustains a usable rate on a CPU
            //            encoder. Quality is preserved by the low CRF, and
            //            YouTube re-encodes on upload anyway.
            long outPixels = (long)outW * outH;
            string preset =
                outPixels >= 3840L * 2160 ? "ultrafast" :
                outPixels >= 2560L * 1440 ? "superfast" :
                "veryfast";

            // -tune stillimage: x264 tuning aimed at sharp static detail
            //   rather than film grain — correct for UI capture.
            // -g: one keyframe per second, so seeking/scrubbing is usable.
            // -threads 0: let x264 use every core; 4K needs all of them.
            string args =
                $"-y -f rawvideo -pix_fmt bgra -s {srcW}x{srcH} -framerate {fps} " +
                $"-use_wallclock_as_timestamps 0 -i - " +
                $"-an -vf \"{vf}\" -c:v libx264 -preset {preset} -tune stillimage " +
                $"-threads 0 -crf {crf} -g {fps} -pix_fmt yuv420p " +
                // Explicit BT.709 tagging: screen capture IS sRGB (≈709).
                // Untagged files leave the primaries to guesswork, and a
                // 4K upscale interpreted as BT.2020 / flat-range
                // somewhere in the edit/upload chain is precisely the
                // "dull, washed" look users report. Tagging is free.
                $"-color_primaries bt709 -color_trc bt709 -colorspace bt709 " +
                $"-movflags +faststart " +
                $"\"{videoPath}\"";

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

                var pipe = new FfmpegRawPipe(videoPath)
                {
                    _process = p,
                    _stdin = p.StandardInput.BaseStream,
                    IsOpen = true,
                    ProcessStartedUtc = DateTime.UtcNow
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
        public bool WriteFrame(byte[] buffer, int count)
        {
            if (!IsOpen || _stdin == null || buffer == null) return false;

            try
            {
                lock (_writeGate)
                {
                    if (!IsOpen || _stdin == null) return false;
                    _stdin.Write(buffer, 0, count);
                    FramesPiped++;
                    BytesPiped += count;
                }
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
