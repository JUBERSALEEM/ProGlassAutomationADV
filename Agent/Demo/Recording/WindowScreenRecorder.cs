using ProGlassAutomation.Agent.Demo.Video;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ProGlassAutomation.Agent.Demo.Recording
{
    public sealed class RecordedFrame
    {
        public string Path { get; init; } = "";
        public TimeSpan Timestamp { get; init; }
        public int CursorX { get; init; }
        public int CursorY { get; init; }
    }

    /// <summary>
    /// Captures only the running main window (not the desktop) at a
    /// chosen quality and frame rate. Encode and disk I/O run off the
    /// UI thread. A full dispatcher is never pumped — that was freezing
    /// the demo walk.
    /// </summary>
    public sealed class WindowScreenRecorder : IDisposable
    {
        private readonly List<RecordedFrame> _frames = new();
        private readonly object _framesGate = new();
        private readonly WasapiLoopbackRecorder _audio = new();
        private readonly WasapiMicRecorder _mic = new();
        private CancellationTokenSource? _cts;
        private Task? _loop;
        private DateTime _started;
        private DateTime _pauseStarted;
        private TimeSpan _pausedAccumulated;
        private ScreenRecordSettings _settings = new();
        private FfmpegLivePipe? _pipe;
        private string? _liveVideoPath;
        private volatile bool _paused;

        // ── OFF-UI-THREAD CAPTURE (2026-09-02) ───────────────────────
        // GDI PrintWindow against the app HWND, driven from a background
        // thread, encoded once as raw BGRA. Replaces the UI-thread
        // RenderTargetBitmap path that caused the stutter and could not
        // see popups. See GdiWindowCapture for the full rationale.
        private FfmpegRawPipe? _rawPipe;
        private string? _rawVideoPath;
        private IntPtr _targetHwnd = IntPtr.Zero;
        private volatile bool _rawActive;

        /// <summary>True when the fast off-thread pipeline produced the video.</summary>
        public bool RawCaptureActive => _rawActive;

        /// <summary>
        /// Non-null when capture could not start at all. The service reports
        /// this instead of pretending a recording is running — a HUD stuck
        /// on "REC" with no encoder behind it was part of why STOP appeared
        /// dead.
        /// </summary>
        public string? FatalStartError { get; private set; }

        /// <summary>Final MP4 written directly by the raw pipeline, if any.</summary>
        public string? RawVideoPath => _rawActive ? _rawVideoPath : null;

        public IReadOnlyList<RecordedFrame> Frames
        {
            get { lock (_framesGate) return _frames.ToArray(); }
        }

        public bool IsRecording => _loop != null && !_loop.IsCompleted;
        public DateTime StartedUtc => _started;
        public TimeSpan Duration { get; private set; }

        /// <summary>
        /// Nominal frame rate the raw H.264 pipe was started with (the
        /// value baked into ffmpeg's <c>-framerate</c>). The encoded file
        /// therefore CLAIMS a duration of <see cref="EncodedFrames"/> /
        /// this value. Because captures happen at the real (throttled)
        /// wall-clock rate — not this nominal rate — that claimed duration
        /// is almost always shorter than reality, which is exactly why a
        /// 3-minute recording used to play back as ~1 minute. The save
        /// path uses this together with <see cref="EncodedFrames"/> and
        /// <see cref="Duration"/> to re-time the file to real duration.
        /// Zero until the pipe has started.
        /// </summary>
        public int CaptureNominalFps { get; private set; }

        /// <summary>
        /// Timestamp-rescale factor that stretches the raw capture's
        /// claimed duration (<see cref="EncodedFrames"/> / <see
        /// cref="CaptureNominalFps"/>) onto the real elapsed wall-clock
        /// time (<see cref="Duration"/>). Fed to ffmpeg's <c>-itsscale</c>
        /// during the stream-copy save, so the final MP4's duration equals
        /// the real recording length no matter how many frames the machine
        /// actually managed to encode. Returns 1.0 (no change) when the
        /// inputs are not yet meaningful. Clamped to a sane range so a
        /// pathological frame count can never produce an absurd duration.
        /// </summary>
        public double RetimeScale =>
            ComputeRetimeScale(EncodedFrames, CaptureNominalFps, Duration.TotalSeconds);

        /// <summary>
        /// Pure, side-effect-free re-time factor: how much the raw
        /// capture's claimed duration (<paramref name="encodedFrames"/> /
        /// <paramref name="nominalFps"/>) must be stretched to match the
        /// real elapsed <paramref name="realSeconds"/>. Returns 1.0 (no
        /// change) whenever the inputs are not yet meaningful, and clamps
        /// the result to [0.02, 50] so a pathological frame count can never
        /// yield an absurd duration. Extracted static so the timing
        /// contract behind the "3 minutes plays as 1 minute" fix is unit
        /// testable without a live capture.
        /// </summary>
        public static double ComputeRetimeScale(
            int encodedFrames, int nominalFps, double realSeconds)
        {
            if (encodedFrames <= 0 || nominalFps <= 0 || realSeconds <= 0.05)
                return 1.0;

            double claimed = encodedFrames / (double)nominalFps;
            if (claimed <= 0.001) return 1.0;

            return Math.Clamp(realSeconds / claimed, 0.02, 50.0);
        }

        // Incremented from BOTH the capture loop and thread-pool encode
        // tasks. Plain ++ on an int is a read-modify-write and loses counts
        // under contention, which made the HUD's dropped/encoded figures
        // quietly wrong. Interlocked + Volatile.Read is the correct pair.
        private int _droppedFrames;
        private int _encodedFrames;

        public int DroppedFrames => Volatile.Read(ref _droppedFrames);
        public int EncodedFrames => Volatile.Read(ref _encodedFrames);

        // ── PHASE-2 DIAGNOSTICS (2026-09-14) ──────────────────────────
        // Captured-vs-written split, the effective fps the loop ended at,
        // and the audio stream durations — so the stop summary can say
        // exactly how many frames the machine actually produced and
        // whether the audio could have truncated the final file.
        private int _capturedFrames;

        public int CapturedFrames => Volatile.Read(ref _capturedFrames);
        public int FinalEffectiveFps { get; private set; }
        public TimeSpan? LoopbackAudioSeconds { get; private set; }
        public TimeSpan? MicAudioSeconds { get; private set; }
        public string? LiveAudioPath => _audio.Path;
        public string? MicAudioPath => _mic.Path;
        public string? AudioError =>
            string.Join(" · ", new[] { _audio.Error, _mic.Error }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        public bool LiveAudioStarted { get; private set; }
        public bool MicAudioStarted { get; private set; }
        public bool IsPaused => _paused;
        public bool LiveEncodeActive => _pipe != null && _pipe.IsOpen;
        public int LiveFramesPiped => _pipe?.FramesPiped ?? 0;
        public long LiveBytesPiped => _pipe?.BytesPiped ?? 0;
        public string? LiveVideoPath => _liveVideoPath;
        public string? LiveEncodeError => _pipe?.Error;
        public int EncodePercent { get; set; }
        public string EncodeStage { get; set; } = "";
        public ScreenRecordSettings Settings => _settings;

        public TimeSpan Elapsed
        {
            get
            {
                var now = DateTime.UtcNow;
                var paused = _pausedAccumulated;
                if (_paused && _pauseStarted != default)
                    paused += now - _pauseStarted;
                var raw = now - _started - paused;
                return raw < TimeSpan.Zero ? TimeSpan.Zero : raw;
            }
        }

        public void Start(string framesFolder, int fps) =>
            Start(framesFolder, (new ScreenRecordSettings { Fps = fps }).Clamp());

        public void Start(string framesFolder, ScreenRecordSettings settings)
        {
            try { _cts?.Cancel(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            _settings = (settings ?? new ScreenRecordSettings()).Clone().Clamp();
            Directory.CreateDirectory(framesFolder);
            lock (_framesGate) _frames.Clear();
            Interlocked.Exchange(ref _droppedFrames, 0);
            Interlocked.Exchange(ref _encodedFrames, 0);
            Interlocked.Exchange(ref _capturedFrames, 0);
            FinalEffectiveFps = Math.Max(1, _settings.Fps);
            LoopbackAudioSeconds = null;
            MicAudioSeconds = null;
            Duration = TimeSpan.Zero;
            EncodePercent = 0;
            EncodeStage = "Recording";
            _paused = false;
            _pausedAccumulated = TimeSpan.Zero;
            _pauseStarted = default;
            _started = DateTime.UtcNow;
            _cts = new CancellationTokenSource();

            LiveAudioStarted = false;
            MicAudioStarted = false;
            string parent = Directory.GetParent(framesFolder)?.FullName ?? framesFolder;

            // ── Live H.264 preview pipe (lag fix) ─────────────────────
            // The final MP4 is always composed from the saved JPEG frames
            // (correct timing), so a concurrent live encoder is pure CPU
            // overhead — it re-encodes every frame in real time and its
            // output is discarded on stop. It now defaults OFF
            // (LivePreviewEncode). Turning it off removes the biggest
            // background CPU consumer and directly reduces recording lag.
            try { _pipe?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            _pipe = null;
            _liveVideoPath = null;

            if (_settings.LivePreviewEncode)
            {
                _liveVideoPath = Path.Combine(parent, "live.h264.mp4");
                var (outW, outH) = _settings.OutputSize;
                _pipe = FfmpegLivePipe.TryStart(
                    _liveVideoPath, _settings.Fps, outW, outH, _settings.EncodeCrf);
            }

            if (_settings.CaptureLiveAudio)
                LiveAudioStarted = _audio.Start(System.IO.Path.Combine(parent, "live.wav"));
            if (_settings.CaptureMicrophone)
                MicAudioStarted = _mic.Start(System.IO.Path.Combine(parent, "mic.wav"));

            // Resolve the target HWND ONCE, on the UI thread. After this
            // the capture loop never touches the dispatcher again — that
            // is the whole point of the new pipeline.
            _targetHwnd = ResolveMainWindowHandle();
            FatalStartError = null;
            _rawActive = false;
            _rawVideoPath = null;
            try { _rawPipe?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            _rawPipe = null;

            var ct = _cts.Token;

            if (_targetHwnd != IntPtr.Zero)
            {
                _rawVideoPath = Path.Combine(parent, "capture.mp4");
                _loop = Task.Run(() => RawLoopAsync(framesFolder, ct));
            }
            else
            {
                FatalStartError =
                    "The application window is not available for capture.";
                AppLogService.Warning(
                    FatalStartError,
                    new Services.Logging.LogContext { Module = "Recording", Operation = "Start" });
            }
        }

        /// <summary>
        /// Stops capture and returns AS SOON AS the loop is cancelled.
        ///
        /// The old implementation blocked the caller for up to EIGHT SECONDS
        /// draining the encode queue:
        ///
        ///     var until = DateTime.UtcNow.AddSeconds(8);
        ///     while (pendingEncodes > 0 &amp;&amp; Now &lt; until) await Task.Delay(40);
        ///
        /// STOP is a user gesture. Making it wait on background disk I/O is
        /// why stopping felt frozen. Outstanding encodes are frames already
        /// captured, so they can finish on their own; the composer reads
        /// whatever landed. The drain is now bounded and off the caller's
        /// path.
        /// </summary>
        public async Task StopAsync()
        {
            _cts?.Cancel();

            // Bounded join on the capture loop only. It checks the token
            // every iteration, so this returns almost immediately.
            if (_loop != null)
            {
                try
                {
                    var loop = _loop;
                    await Task.WhenAny(loop, Task.Delay(750)).ConfigureAwait(false);
                }
                catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            }
            _loop = null;

            Duration = Elapsed;

            // Finalise the raw encoder FIRST: it holds the actual video.
            // Bounded so a wedged ffmpeg cannot hold STOP open; +faststart
            // means even a killed process leaves a playable file.
            if (_rawPipe != null)
            {
                EncodeStage = "Finalising video…";
                EncodePercent = 45;
                try
                {
                    bool ok = await _rawPipe
                        .FinishAsync(TimeSpan.FromSeconds(10))
                        .ConfigureAwait(false);

                    if (!ok) _rawActive = false;
                }
                catch (Exception ex)
                {
                    _rawActive = false;
                    AppLogService.Warning(
                        $"Video finalise failed: {ex.Message}",
                        new Services.Logging.LogContext { Module = "Recording", Operation = "StopAsync" });
                }
            }

            // PHASE-2: capture the audio stream durations (WavDuration) —
            // a short live.wav relative to wall time is the -shortest
            // truncation signal.
            try { LoopbackAudioSeconds = await _audio.StopAsync().ConfigureAwait(false); }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WindowScreenRecorder] StopAsync: loopback is optional: " + ex.Message); }
            try { MicAudioSeconds = await _mic.StopAsync().ConfigureAwait(false); }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WindowScreenRecorder] StopAsync: mic is optional: " + ex.Message); }

            if (_pipe != null)
            {
                EncodeStage = "Closing live encoder…";
                EncodePercent = 40;
                try { await _pipe.FinishAsync(CancellationToken.None).ConfigureAwait(false); }
                catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WindowScreenRecorder] StopAsync: fallback JPEG encode still available: " + ex.Message); }
            }

            // ── PHASE-2 DIAGNOSTIC: one grep-able stop summary ─────────
            // For the exact recording that just ended: wall time,
            // captured/written/dropped/expected frames, encoder liveness +
            // exit code, what the raw file CLAIMS vs what ffprobe reports
            // about its stream, how long the audio actually ran, and the
            // retime factor the save path is about to apply.
            try
            {
                double? rawClaimed = null;
                string? rawProbe = null;
                if (_rawActive && !string.IsNullOrWhiteSpace(_rawVideoPath))
                {
                    try { rawClaimed = FfmpegMp4Composer.TryReadDurationSeconds(_rawVideoPath); }
                    catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WindowScreenRecorder] StopAsync: raw duration probe optional: " + ex.Message); }
                    try { rawProbe = FfmpegMp4Composer.TryProbeVideoSummary(_rawVideoPath); }
                    catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WindowScreenRecorder] StopAsync: raw stream probe optional: " + ex.Message); }
                }

                string? pipeError = _rawPipe?.Error;
                if (!string.IsNullOrWhiteSpace(pipeError) && pipeError.Length > 200)
                    pipeError = "…" + pipeError[^200..];

                AppLogService.Info(
                    $"[RecSummary] wall={Duration.TotalSeconds:F1}s " +
                    $"nominal={_settings.Fps}fps effective={FinalEffectiveFps}fps " +
                    $"captured={Volatile.Read(ref _capturedFrames)} " +
                    $"written={Volatile.Read(ref _encodedFrames)} " +
                    $"dropped={Volatile.Read(ref _droppedFrames)} " +
                    $"expected={(int)Math.Round(Duration.TotalSeconds * _settings.Fps)} " +
                    $"ffmpeg pid={_rawPipe?.ProcessId ?? -1} " +
                    $"alive={(_rawPipe?.ProcessAlive ?? false)} " +
                    $"exitCode={_rawPipe?.ExitCode?.ToString() ?? "n/a"} " +
                    $"started={_rawPipe?.ProcessStartedUtc.ToString("HH:mm:ss") ?? "n/a"} " +
                    $"stderr={(string.IsNullOrWhiteSpace(pipeError) ? "clean" : pipeError!)} " +
                    $"rawClaimed={(rawClaimed?.ToString("0.###") ?? "n/a")}s " +
                    $"rawProbe={rawProbe ?? "n/a"} " +
                    $"audio loopback={(LoopbackAudioSeconds?.TotalSeconds.ToString("0.##") ?? "off")}s " +
                    $"mic={(MicAudioSeconds?.TotalSeconds.ToString("0.##") ?? "off")}s " +
                    $"audioError={(string.IsNullOrWhiteSpace(AudioError) ? "none" : AudioError)} " +
                    $"retime={RetimeScale:0.0000} " +
                    $"rawActive={_rawActive}",
                    new Services.Logging.LogContext { Module = "Recording", Operation = "StopSummary" });
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WindowScreenRecorder] StopAsync: summary log optional: " + ex.Message); }

            EncodePercent = 55;
        }

        public void SetPaused(bool paused)
        {
            if (_paused == paused) return;
            if (paused)
            {
                _paused = true;
                _pauseStarted = DateTime.UtcNow;
            }
            else
            {
                if (_pauseStarted != default)
                    _pausedAccumulated += DateTime.UtcNow - _pauseStarted;
                _pauseStarted = default;
                _paused = false;
            }
            _audio.IsPaused = paused;
            _mic.IsPaused = paused;
        }

        public void Stop() => _cts?.Cancel();


        /// <summary>
        /// Resolves MainWindow's HWND. The ONLY dispatcher touch in the new
        /// pipeline, done once at start.
        /// </summary>
        private static IntPtr ResolveMainWindowHandle()
        {
            try
            {
                var disp = Application.Current?.Dispatcher;
                if (disp == null) return IntPtr.Zero;

                return disp.Invoke(() =>
                {
                    var w = Application.Current?.MainWindow;
                    if (w == null) return IntPtr.Zero;
                    return new System.Windows.Interop.WindowInteropHelper(w).Handle;
                });
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Could not resolve window handle for capture: {ex.Message}",
                    new Services.Logging.LogContext { Module = "Recording", Operation = "ResolveHwnd" });
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// THE REAL CAPTURE LOOP.
        ///
        /// Runs entirely on a background thread: GDI PrintWindow reads the
        /// window's DWM-composited surface and the bytes go straight into
        /// x264. The UI thread is never involved, which is what removes the
        /// stutter, and DWM's surface includes owned popups, dropdowns and
        /// dialogs — content the old visual-tree render could not see.
        ///
        /// One frame is written per real capture (no CFR dup-fill); the
        /// file therefore claims nominal-fps timing but is rescaled to the
        /// real elapsed duration at save via -itsscale (see RetimeScale /
        /// FfmpegMp4Composer.RetimeCopyAsync), which is what keeps a
        /// 3-minute recording 3 minutes long even when the encoder cannot
        /// sustain the nominal frame rate (e.g. 4K/60 on a CPU encoder).
        /// </summary>
        private async Task RawLoopAsync(string framesFolder, CancellationToken ct)
        {
            int fps = Math.Max(1, _settings.Fps);
            var interval = TimeSpan.FromMilliseconds(1000.0 / fps);

            using var capture = new GdiWindowCapture();

            if (!capture.Prepare(_targetHwnd))
            {
                // DO NOT fall back to the UI-thread capture. That path is
                // what froze the app: it posts a full software re-render to
                // the dispatcher 30x a second, so module switches, dialogs
                // and even the STOP button stop responding. A recording
                // that fails loudly is strictly better than one that
                // hangs the application.
                FatalStartError =
                    "Window capture could not start. Recording was not begun.";
                AppLogService.Warning(
                    FatalStartError,
                    new Services.Logging.LogContext { Module = "Recording", Operation = "RawLoop" });
                return;
            }

            var (outW, outH) = _settings.OutputSize;

            _rawPipe = FfmpegRawPipe.TryStart(
                _rawVideoPath!, fps,
                capture.Width, capture.Height,
                outW, outH,
                _settings.EncodeCrf);

            if (_rawPipe == null)
            {
                FatalStartError =
                    "Video encoder could not start (ffmpeg). Recording was not begun.";
                AppLogService.Warning(
                    FatalStartError,
                    new Services.Logging.LogContext { Module = "Recording", Operation = "RawLoop" });
                return;
            }

            _rawActive = true;
            CaptureNominalFps = fps;
            int srcW = capture.Width, srcH = capture.Height;
            var buffer = new byte[srcW * 4 * srcH];
            int frameBytes = buffer.Length;

            var epoch = DateTime.UtcNow;
            long slot = 0;
            bool haveFrame = false;
            int blankStreak = 0;
            bool warnedFallback = false;

            // ── ONE-FRAME-PER-CAPTURE + RE-TIME-AT-SAVE (2026-09-10) ─
            //
            // ROOT CAUSE OF "3 minutes records as 1 minute".
            // The ffmpeg raw pipe stamps every written frame at exactly
            // 1/nominal-fps, so the encoded file CLAIMS a duration of
            // framesWritten / nominal-fps. The previous loop tried to keep
            // that honest by DUPLICATING the latest frame into every
            // elapsed nominal slot ("wall-clock fill"). Two things made
            // that collapse the duration on any real machine, and worst of
            // all at the shipped 4K/60 default:
            //
            //   1. A 4K BGRA frame is ~33 MB — far larger than the OS pipe
            //      buffer — so WriteFrame BLOCKS on x264 backpressure.
            //      x264 cannot swallow 60 fps of 4K, so the loop can only
            //      write frames as fast as the encoder drains them (say
            //      ~20 equivalent fps). Over 180 real seconds it writes
            //      ~3600 frames -> 3600/60 = 60 s of claimed video. The
            //      exact reported 3:1 shrink.
            //   2. The dup-fill was then CLAMPED (`pending > fps`) and the
            //      overflow *discarded* as "dropped" — so whenever the
            //      encoder fell behind for even one second, that real wall
            //      time was thrown away and the video got shorter still.
            //
            // THE FIX. Stop pretending the stream is a fixed-rate CFR
            // stream at capture time. Write EXACTLY ONE frame per real
            // capture — no dup-fill, no clamp, nothing discarded — which
            // also removes the biggest source of encoder load so 4K is
            // actually feasible and smooth. The file still nominally
            // claims nominal-fps timing, but the SAVE path rescales its
            // timestamps (ffmpeg -itsscale, a stream-copy, no re-encode)
            // so the final MP4 duration equals the REAL elapsed wall-clock
            // time (see RetimeScale + FfmpegMp4Composer.RetimeCopyAsync).
            // Duration is then correct at any resolution, any fps, on any
            // machine, however far the encoder fell behind.

            // ── ADAPTIVE THROTTLE (Phase 1 freeze fix) ───────────────
            //
            // PrintWindow BLOCKS on the window's owning thread — our UI
            // thread (see GdiWindowCapture's header). Every capture is
            // therefore a UI stall, and issuing them at a fixed 30fps
            // regardless of cost is exactly what made the app
            // unresponsive: on a heavy screen each capture can take
            // longer than the frame interval, so the UI thread never gets
            // a gap in which to service input, and STOP cannot be
            // clicked.
            //
            // The loop now measures what it costs the UI thread and gives
            // time back when it is expensive. Recording at a lower frame
            // rate is a minor quality compromise; an unusable application
            // is not acceptable at any frame rate.
            int effectiveFps = fps;
            var effInterval = interval;
            double slowStreak = 0;
            const double UiBudgetFraction = 0.5;   // never eat >50% of a frame
            const int MinFps = 8;

            // PHASE-2 DIAGNOSTIC: periodic health line — every 100 written
            // frames or 10 s, whichever comes first. Never per-frame, so it
            // cannot itself throttle recording.
            int healthLogWritten = 0;
            var healthLogAt = DateTime.UtcNow;

            while (!ct.IsCancellationRequested)
            {
                if (_paused)
                {
                    try { await Task.Delay(50, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                    // Rebase the pacing clock on resume so the wait math
                    // does not try to "catch up" the paused interval with
                    // a burst of back-to-back captures. Duration excludes
                    // paused time via Elapsed, and the save-time rescale
                    // maps frames onto that real (un-paused) duration, so
                    // there is no separate write clock to rebase anymore.
                    epoch = DateTime.UtcNow - TimeSpan.FromTicks(effInterval.Ticks * slot);
                    continue;
                }

                // Re-prepare only reallocates when the window actually
                // resized. A size change mid-recording would break the
                // rawvideo contract (fixed -s), so the stream keeps the
                // original geometry and the new content is letterboxed by
                // the scale/pad filter instead of shearing.
                if (capture.Prepare(_targetHwnd)
                    && capture.Width == srcW && capture.Height == srcH)
                {
                    if (capture.TryCapture(buffer))
                    {
                        haveFrame = true;
                        Interlocked.Increment(ref _capturedFrames);
                        blankStreak = capture.LastFrameWasBlank ? blankStreak + 1 : 0;

                        // The screen blit costs nothing on the UI thread;
                        // the PrintWindow fallback does. Say so once, so a
                        // machine silently on the slow path is diagnosable
                        // instead of just "feeling laggy".
                        if (capture.UsedFallbackPath && !warnedFallback)
                        {
                            warnedFallback = true;
                            AppLogService.Warning(
                                "Screen capture fell back to PrintWindow, which " +
                                "blocks the UI thread. Recording will throttle to " +
                                "stay responsive.",
                                new Services.Logging.LogContext
                                { Module = "Recording", Operation = "RawLoop" });
                        }

                        // How much of this frame's budget did we take from
                        // the UI thread?
                        double budgetMs = effInterval.TotalMilliseconds * UiBudgetFraction;
                        if (capture.LastCaptureMs > budgetMs)
                        {
                            slowStreak++;

                            // Sustained overrun: halve the rate so the UI
                            // thread gets real gaps back. Recovery is
                            // deliberately slower than back-off, because
                            // oscillating between rates looks worse to the
                            // user than simply running at the lower one.
                            if (slowStreak >= 5 && effectiveFps > MinFps)
                            {
                                effectiveFps = Math.Max(MinFps, effectiveFps / 2);
                                effInterval = TimeSpan.FromMilliseconds(1000.0 / effectiveFps);
                                FinalEffectiveFps = effectiveFps;   // PHASE-2: record the throttle
                                slowStreak = 0;

                                AppLogService.Debug(
                                    $"[Recording] capture {capture.LastCaptureMs:F0}ms " +
                                    $"exceeded budget; frame rate -> {effectiveFps}fps " +
                                    "to keep the UI responsive.");
                            }
                        }
                        else
                        {
                            slowStreak = Math.Max(0, slowStreak - 0.25);
                        }
                    }
                }

                if (haveFrame)
                {
                    // ── Write exactly ONE frame per real capture ─────
                    // No dup-fill, no clamp: every captured frame is a
                    // real moment in time, and the save-time -itsscale
                    // rescale (see RetimeScale) stretches the stream back
                    // onto real wall-clock duration. This is what keeps a
                    // 3-minute recording 3 minutes long even when the
                    // encoder can only sustain a fraction of nominal fps.
                    if (!_rawPipe.WriteFrame(buffer, frameBytes))
                    {
                        haveFrame = false;   // encoder died -> break outer loop
                    }
                    else
                    {
                        Interlocked.Increment(ref _encodedFrames);
                    }

                    if (!_rawPipe.IsOpen)
                        break;   // encoder died

                    // A written frame consumes the "have a fresh frame"
                    // flag so a stalled capture does not re-pipe the same
                    // stale buffer indefinitely; the next successful
                    // TryCapture re-arms it.
                    haveFrame = false;
                }
                else
                {
                    Interlocked.Increment(ref _droppedFrames);
                }

                // ── PHASE-2 DIAGNOSTIC: periodic encoder health ───────
                // Answers "is the encoder alive right now, and is anything
                // actually being written?" — the exact question the silent
                // HUD cannot answer.
                int writtenNow = Volatile.Read(ref _encodedFrames);
                if (writtenNow - healthLogWritten >= 100 || DateTime.UtcNow - healthLogAt >= TimeSpan.FromSeconds(10))
                {
                    healthLogWritten = writtenNow;
                    healthLogAt = DateTime.UtcNow;
                    var rp = _rawPipe;
                    AppLogService.Debug(
                        $"[RecHealth] t={(DateTime.UtcNow - _started).TotalSeconds:F0}s " +
                        $"captured={Volatile.Read(ref _capturedFrames)} " +
                        $"written={writtenNow} " +
                        $"dropped={Volatile.Read(ref _droppedFrames)} " +
                        $"effFps={effectiveFps} " +
                        $"ffmpeg={(rp == null ? "n/a" : rp.ProcessAlive ? "alive" : "DEAD (code=" + (rp.ExitCode?.ToString() ?? "?") + ")")} " +
                        $"pid={rp?.ProcessId ?? -1}",
                        new Services.Logging.LogContext { Module = "Recording", Operation = "Health" });
                }

                // Persistent black output means the driver ignored
                // PW_RENDERFULLCONTENT. Bail to the legacy path rather
                // than hand the user a black video.
                if (blankStreak > fps * 2)
                {
                    AppLogService.Warning(
                        "Window capture returned blank frames; using legacy path.",
                        new Services.Logging.LogContext { Module = "Recording", Operation = "RawLoop" });

                    // Blank output means the driver ignored
                    // PW_RENDERFULLCONTENT. Stop cleanly instead of
                    // switching to the UI-thread path and freezing the app.
                    _rawActive = false;
                    FatalStartError =
                        "The display driver returned blank frames for this window. "
                        + "Recording stopped.";

                    try { await _rawPipe.FinishAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
                    catch (Exception ex) { AppLogService.Debug($"[RawLoop] finish: {ex.Message}"); }
                    return;
                }

                slot++;

                // Pace against the ADAPTIVE interval. Using the nominal one
                // here would defeat the throttle entirely: the loop would
                // keep firing at the original rate no matter how expensive
                // each capture had become.
                var wait = epoch + TimeSpan.FromTicks(effInterval.Ticks * slot) - DateTime.UtcNow;

                if (wait > TimeSpan.Zero)
                {
                    try { await Task.Delay(wait, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                }
                else
                {
                    // We are already late. YIELD ANYWAY — an unconditional
                    // pause is what guarantees the UI thread a gap to
                    // service input. Without it a slow machine spins
                    // capture-after-capture and STOP can never be clicked,
                    // which is precisely the reported "have to force-close"
                    // behaviour.
                    try { await Task.Delay(15, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }

                    if (-wait > effInterval)
                    {
                        long behind = -wait.Ticks / effInterval.Ticks;
                        slot += behind;
                        Interlocked.Add(ref _droppedFrames, (int)Math.Min(behind, int.MaxValue));
                    }
                }
            }
        }






        public void Dispose()
        {
            try { Stop(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            try { _cts?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            try { _pipe?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            try { _rawPipe?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            _audio.Dispose();
            _mic.Dispose();
        }
    }
}
