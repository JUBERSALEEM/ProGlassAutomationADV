using ProGlassAutomation.Agent.Demo.Video;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

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
        // Absorbs bursts: JPEG encode runs on background threads, so a short
        // UI stall should queue frames instead of dropping them. 4 was too
        // tight and turned a busy dispatcher into visible frame drops (lag).
        // 2026-08-25: raised 8 -> 16. When the JPEG encoders fell behind
        // (busy module transitions), captures were skipped and the
        // recording visibly "jumped" over workspace activity. A deeper
        // queue absorbs the burst; the concat-demuxer compose in
        // FfmpegMp4Composer handles whatever still drops.
        private const int MaxPendingEncodes = 24;

        /// <summary>
        /// Cap on concurrent JPEG encoders.
        ///
        /// Task.Run per frame let the pool spawn as many encoders as frames
        /// in flight. On a busy machine the pool injects threads roughly one
        /// per second, so a burst produced a thread stampede that competed
        /// with the UI for cores -- one of the reasons the app stuttered
        /// WHILE recording. Bounded parallelism keeps throughput without the
        /// stampede; the 24-deep queue still absorbs bursts.
        /// </summary>
        private readonly SemaphoreSlim _encodeSlots =
            new(Math.Max(2, Environment.ProcessorCount / 2));

        private readonly List<RecordedFrame> _frames = new();
        private readonly object _framesGate = new();
        private readonly WasapiLoopbackRecorder _audio = new();
        private readonly WasapiMicRecorder _mic = new();
        private CancellationTokenSource? _cts;
        private Task? _loop;
        private DateTime _started;
        private DateTime _pauseStarted;
        private TimeSpan _pausedAccumulated;
        private int _pendingEncodes;
        private int _captureBusy;
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

        /// <summary>Final MP4 written directly by the raw pipeline, if any.</summary>
        public string? RawVideoPath => _rawActive ? _rawVideoPath : null;

        public IReadOnlyList<RecordedFrame> Frames
        {
            get { lock (_framesGate) return _frames.ToArray(); }
        }

        public bool IsRecording => _loop != null && !_loop.IsCompleted;
        public DateTime StartedUtc => _started;
        public TimeSpan Duration { get; private set; }

        // Incremented from BOTH the capture loop and thread-pool encode
        // tasks. Plain ++ on an int is a read-modify-write and loses counts
        // under contention, which made the HUD's dropped/encoded figures
        // quietly wrong. Interlocked + Volatile.Read is the correct pair.
        private int _droppedFrames;
        private int _encodedFrames;

        public int DroppedFrames => Volatile.Read(ref _droppedFrames);
        public int EncodedFrames => Volatile.Read(ref _encodedFrames);
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
                // No HWND (headless/test): keep the legacy path so
                // recording degrades instead of disappearing.
                _loop = Task.Run(() => LoopAsync(framesFolder, ct));
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

            // Short, bounded settle so the LAST few frames land. 1.5 s is
            // enough for a 24-deep queue at any supported resolution, and it
            // is short enough to feel instant.
            var until = DateTime.UtcNow.AddMilliseconds(1500);
            while (Volatile.Read(ref _pendingEncodes) > 0 && DateTime.UtcNow < until)
                await Task.Delay(25).ConfigureAwait(false);

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

            try { await _audio.StopAsync().ConfigureAwait(false); }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WindowScreenRecorder] StopAsync: loopback is optional: " + ex.Message); }
            try { await _mic.StopAsync().ConfigureAwait(false); }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WindowScreenRecorder] StopAsync: mic is optional: " + ex.Message); }

            if (_pipe != null)
            {
                EncodeStage = "Closing live encoder…";
                EncodePercent = 40;
                try { await _pipe.FinishAsync(CancellationToken.None).ConfigureAwait(false); }
                catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WindowScreenRecorder] StopAsync: fallback JPEG encode still available: " + ex.Message); }
            }
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

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out NativePoint pos);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint { public int X; public int Y; }

        /// <summary>
        /// Capture loop.
        ///
        /// TIMING IS FRAME-INDEXED, NOT WALL-CLOCK. The old loop slept for
        /// "interval minus however long this iteration took", which lets
        /// error accumulate: every late frame pushes all later frames later
        /// still, so a 60 s recording drifts and plays back at the wrong
        /// speed.
        ///
        /// Each slot now has an absolute deadline computed from the START
        /// time and the slot INDEX, so drift cannot accumulate — a late
        /// frame is late once and the next slot is still on the grid.
        ///
        /// A DROPPED SLOT KEEPS ITS PLACE. Previously a drop just vanished,
        /// which silently shortened the video and desynchronised audio.
        /// The slot index still advances, so the composer sees a gap at the
        /// right moment instead of a shorter timeline.
        /// </summary>
        private async Task LoopAsync(string folder, CancellationToken ct)
        {
            int fps = Math.Max(1, _settings.Fps);
            var interval = TimeSpan.FromMilliseconds(1000.0 / fps);

            var epoch = DateTime.UtcNow;
            long slot = 0;      // absolute frame slot since epoch
            int saved = 0;      // frames actually written

            while (!ct.IsCancellationRequested)
            {
                if (_paused)
                {
                    try { await Task.Delay(60, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }

                    // Re-base the grid so paused time is not counted as
                    // dropped frames when recording resumes.
                    epoch = DateTime.UtcNow - TimeSpan.FromTicks(interval.Ticks * slot);
                    continue;
                }

                bool busy = Volatile.Read(ref _pendingEncodes) >= MaxPendingEncodes
                            || Volatile.Read(ref _captureBusy) != 0;

                if (busy)
                {
                    Interlocked.Increment(ref _droppedFrames);
                }
                else
                {
                    var shot = await CapturePixelsAsync(ct, TimeSpan.FromTicks(interval.Ticks * slot))
                        .ConfigureAwait(false);

                    if (shot != null)
                    {
                        string path = System.IO.Path.Combine(folder, $"f{saved:00000}.jpg");
                        saved++;
                        Interlocked.Increment(ref _pendingEncodes);
                        var copy = shot;
                        _ = Task.Run(async () =>
                        {
                            await _encodeSlots.WaitAsync().ConfigureAwait(false);
                            try { EncodeJpeg(copy, path); }
                            finally { _encodeSlots.Release(); }
                        }, CancellationToken.None);
                    }
                    else
                    {
                        Interlocked.Increment(ref _droppedFrames);
                    }
                }

                slot++;

                // Absolute deadline for the NEXT slot — no accumulated drift.
                var nextDue = epoch + TimeSpan.FromTicks(interval.Ticks * slot);
                var wait = nextDue - DateTime.UtcNow;

                if (wait > TimeSpan.Zero)
                {
                    try { await Task.Delay(wait, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                }
                else if (-wait > interval)
                {
                    // We are behind by whole slots. Skip forward onto the
                    // grid rather than firing a burst of catch-up captures,
                    // which would only deepen the backlog.
                    long behind = (long)(-wait.Ticks / interval.Ticks);
                    slot += behind;
                    Interlocked.Add(ref _droppedFrames, (int)Math.Min(behind, int.MaxValue));
                }
            }
        }

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
        /// Timing is frame-indexed against an absolute grid so it cannot
        /// drift, and a late frame is repeated rather than dropped: a
        /// constant-frame-rate stream keeps audio in sync, whereas a
        /// missing frame silently shortens the video.
        /// </summary>
        private async Task RawLoopAsync(string framesFolder, CancellationToken ct)
        {
            int fps = Math.Max(1, _settings.Fps);
            var interval = TimeSpan.FromMilliseconds(1000.0 / fps);

            using var capture = new GdiWindowCapture();

            if (!capture.Prepare(_targetHwnd))
            {
                AppLogService.Warning(
                    "Window capture could not initialise; using legacy path.",
                    new Services.Logging.LogContext { Module = "Recording", Operation = "RawLoop" });
                await LoopAsync(framesFolder, ct).ConfigureAwait(false);
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
                AppLogService.Warning(
                    "Video encoder unavailable; using legacy path.",
                    new Services.Logging.LogContext { Module = "Recording", Operation = "RawLoop" });
                await LoopAsync(framesFolder, ct).ConfigureAwait(false);
                return;
            }

            _rawActive = true;
            int srcW = capture.Width, srcH = capture.Height;
            var buffer = new byte[srcW * 4 * srcH];
            int frameBytes = buffer.Length;

            var epoch = DateTime.UtcNow;
            long slot = 0;
            bool haveFrame = false;
            int blankStreak = 0;

            while (!ct.IsCancellationRequested)
            {
                if (_paused)
                {
                    try { await Task.Delay(50, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                    epoch = DateTime.UtcNow - TimeSpan.FromTicks(interval.Ticks * slot);
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
                        blankStreak = capture.LastFrameWasBlank ? blankStreak + 1 : 0;
                    }
                }

                if (haveFrame)
                {
                    // Repeat the previous frame when a capture fails, so
                    // the stream stays constant-frame-rate.
                    if (!_rawPipe.WriteFrame(buffer, frameBytes))
                        break;   // encoder died

                    Interlocked.Increment(ref _encodedFrames);
                }
                else
                {
                    Interlocked.Increment(ref _droppedFrames);
                }

                // Persistent black output means the driver ignored
                // PW_RENDERFULLCONTENT. Bail to the legacy path rather
                // than hand the user a black video.
                if (blankStreak > fps * 2)
                {
                    AppLogService.Warning(
                        "Window capture returned blank frames; using legacy path.",
                        new Services.Logging.LogContext { Module = "Recording", Operation = "RawLoop" });

                    _rawActive = false;
                    try { await _rawPipe.FinishAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
                    catch (Exception ex) { AppLogService.Debug($"[RawLoop] finish: {ex.Message}"); }

                    await LoopAsync(framesFolder, ct).ConfigureAwait(false);
                    return;
                }

                slot++;
                var wait = epoch + TimeSpan.FromTicks(interval.Ticks * slot) - DateTime.UtcNow;

                if (wait > TimeSpan.Zero)
                {
                    try { await Task.Delay(wait, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                }
                else if (-wait > interval)
                {
                    // Behind by whole slots: step onto the grid instead of
                    // firing a catch-up burst that would deepen the backlog.
                    long behind = -wait.Ticks / interval.Ticks;
                    slot += behind;
                    Interlocked.Add(ref _droppedFrames, (int)Math.Min(behind, int.MaxValue));
                }
            }
        }

        private sealed class PixelShot
        {
            public required byte[] Pixels { get; init; }
            public int Width { get; init; }
            public int Height { get; init; }
            public int CursorX { get; init; }
            public int CursorY { get; init; }
            public TimeSpan Timestamp { get; init; }
        }

        // Phase 3: No ContinueWith + .Result — use async/await, single _captureBusy handling
        private async Task<PixelShot?> CapturePixelsAsync(
            CancellationToken ct, TimeSpan slotTimestamp)
        {
            var disp = Application.Current?.Dispatcher;
            if (disp == null) return null;
            if (Interlocked.Exchange(ref _captureBusy, 1) != 0) return null;

            try
            {
                // 2026-08-25 senior fix — capture ran at Background
                // priority, which queues every frame behind ALL normal
                // UI work. While the demo workflow drives navigation at
                // normal priority, captures waited indefinitely — the
                // recorded video lagged behind the actions on screen
                // ("not recording live as per input").
                //
                // Loaded sits above Background/DataBind but BELOW Input,
                // so user input and demo interactions still win, yet
                // frames are captured promptly instead of whenever the
                // UI goes idle. RenderTargetBitmap.Render still forces
                // layout, so we never raise it to Input/Normal.
                var shot = await disp.InvokeAsync(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    return CaptureOnUi(slotTimestamp);
                }, DispatcherPriority.Loaded, ct).Task.ConfigureAwait(false);

                return shot;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                Services.Logging.AppLogService.Warning(
                    $"CapturePixels failed: {ex.Message}",
                    new Services.Logging.LogContext { Module = "Recording", Operation = "CapturePixels" });
                return null;
            }
            finally
            {
                Interlocked.Exchange(ref _captureBusy, 0);
            }
        }

        private PixelShot? CaptureOnUi(TimeSpan slotTimestamp)
        {
            var window = Application.Current?.MainWindow;
            if (window == null || window.ActualWidth < 2 || window.ActualHeight < 2)
                return null;

            // Never waste a capture on a minimized / hidden window — it
            // would render an empty frame and drop the next real one.
            if (!window.IsVisible || window.WindowState == WindowState.Minimized)
                return null;

            var (targetW, targetH) = _settings.CaptureSize;
            double dipW = Math.Max(1, window.ActualWidth);
            double dipH = Math.Max(1, window.ActualHeight);
            var dpiInfo = VisualTreeHelper.GetDpi(window);
            // Defensive: some systems/drivers report 0 DPI scale; default to 1.0
            // to avoid zero-size captures (would produce empty 2x2 frames).
            double scaleX = dpiInfo.DpiScaleX > 0 ? dpiInfo.DpiScaleX : 1.0;
            double scaleY = dpiInfo.DpiScaleY > 0 ? dpiInfo.DpiScaleY : 1.0;
            double nativeW = dipW * scaleX;
            double nativeH = dipH * scaleY;
            double scale = Math.Max(
                Math.Min(targetW / Math.Max(1, nativeW), targetH / Math.Max(1, nativeH)),
                1.0);

            int capW = Math.Max(2, ((int)Math.Round(nativeW * scale)) & ~1);
            int capH = Math.Max(2, ((int)Math.Round(nativeH * scale)) & ~1);
            capW = Math.Min(capW, targetW);
            capH = Math.Min(capH, targetH);
            int dpi = Math.Max(96, (int)Math.Round(96 * scaleX * scale));

            BitmapSource? bmp;
            try
            {
                // Do not SetBitmapScalingMode on the live window every
                // frame — that dirties the visual tree and is a leading
                // cause of laggy capture. NearestNeighbor is enough for
                // a screen recording of already-crisp UI chrome.
                var rtb = new RenderTargetBitmap(capW, capH, dpi, dpi, PixelFormats.Pbgra32);
                rtb.Render(window);
                rtb.Freeze();
                bmp = rtb;
            }
            catch
            {
                return null;
            }

            int stride = capW * 4;
            var pixels = new byte[stride * capH];
            bmp.CopyPixels(pixels, stride, 0);

            GetCursorPos(out var pos);
            int cx = 0, cy = 0;
            try
            {
                var origin = window.PointToScreen(new Point(0, 0));
                double pxW = dipW * scaleX;
                double pxH = dipH * scaleY;
                if (pxW > 0 && pxH > 0)
                {
                    cx = (int)Math.Round((pos.X - origin.X) / pxW * capW);
                    cy = (int)Math.Round((pos.Y - origin.Y) / pxH * capH);
                }
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[WindowScreenRecorder] CaptureOnUi: cursor overlay is optional: " + ex.Message); }

            if (_settings.DrawCursor)
                DrawPointer(pixels, capW, capH, cx, cy);

            return new PixelShot
            {
                Pixels = pixels,
                Width = capW,
                Height = capH,
                CursorX = cx,
                CursorY = cy,
                // Grid time, not clock time — see LoopAsync. Using the
                // wall clock here reintroduced the drift the slot grid
                // exists to prevent.
                Timestamp = slotTimestamp
            };
        }

        private void EncodeJpeg(PixelShot shot, string path)
        {
            try
            {
                var src = BitmapSource.Create(
                    shot.Width, shot.Height, 96, 96,
                    PixelFormats.Pbgra32, null, shot.Pixels, shot.Width * 4);
                src.Freeze();

                byte[] bytes;
                using (var ms = new MemoryStream())
                {
                    var enc = new JpegBitmapEncoder { QualityLevel = _settings.JpegQualityFor };
                    enc.Frames.Add(BitmapFrame.Create(src));
                    enc.Save(ms);
                    bytes = ms.ToArray();
                }

                // Keep the on-disk frame (fallback encode path) AND feed the
                // live H.264 pipe. Without this the live encoder was started
                // but starved — STOP always fell back to the slow JPEG
                // re-encode (or failed when frames were dropped).
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, bytes);
                var pipe = _pipe;
                if (pipe != null && !pipe.WriteJpeg(bytes))
                {
                    // ffmpeg died — the HUD switches to "JPEG fallback" and
                    // the stop path uses the on-disk frames instead.
                }

                if (File.Exists(path))
                {
                    lock (_framesGate)
                    {
                        _frames.Add(new RecordedFrame
                        {
                            Path = path,
                            Timestamp = shot.Timestamp,
                            CursorX = shot.CursorX,
                            CursorY = shot.CursorY
                        });
                    }
                    Interlocked.Increment(ref _encodedFrames);
                }
            }
            catch
            {
                Interlocked.Increment(ref _droppedFrames);
            }
            finally
            {
                Interlocked.Decrement(ref _pendingEncodes);
            }
        }

        private static void DrawPointer(byte[] px, int w, int h, int cx, int cy)
        {
            if ((uint)cx >= (uint)w || (uint)cy >= (uint)h) return;
            const int r = 8;
            for (int y = cy - r - 1; y <= cy + r + 1; y++)
            {
                if ((uint)y >= (uint)h) continue;
                for (int x = cx - r - 1; x <= cx + r + 1; x++)
                {
                    if ((uint)x >= (uint)w) continue;
                    int dx = x - cx, dy = y - cy;
                    int d2 = dx * dx + dy * dy;
                    int i = (y * w + x) * 4;
                    if (d2 <= 16)
                    {
                        px[i] = 220; px[i + 1] = 96; px[i + 2] = 37; px[i + 3] = 255;
                    }
                    else if (d2 <= r * r)
                    {
                        px[i] = 255; px[i + 1] = 255; px[i + 2] = 255; px[i + 3] = 255;
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
            try { _encodeSlots.Dispose(); }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
        }
    }
}
