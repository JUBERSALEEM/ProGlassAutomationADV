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
        private const int MaxPendingEncodes = 8;

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

        public IReadOnlyList<RecordedFrame> Frames
        {
            get { lock (_framesGate) return _frames.ToArray(); }
        }

        public bool IsRecording => _loop != null && !_loop.IsCompleted;
        public DateTime StartedUtc => _started;
        public TimeSpan Duration { get; private set; }
        public int DroppedFrames { get; private set; }
        public int EncodedFrames { get; private set; }
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
            DroppedFrames = 0;
            EncodedFrames = 0;
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

            var ct = _cts.Token;
            _loop = Task.Run(() => LoopAsync(framesFolder, ct));
        }

        public async Task StopAsync()
        {
            _cts?.Cancel();
            if (_loop != null)
            {
                try { await _loop.ConfigureAwait(false); }
                catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            }
            _loop = null;

            var until = DateTime.UtcNow.AddSeconds(8);
            while (Volatile.Read(ref _pendingEncodes) > 0 && DateTime.UtcNow < until)
                await Task.Delay(40).ConfigureAwait(false);

            Duration = Elapsed;
            try { await _audio.StopAsync().ConfigureAwait(false); }
            catch { /* loopback is optional */ }
            try { await _mic.StopAsync().ConfigureAwait(false); }
            catch { /* mic is optional */ }

            if (_pipe != null)
            {
                EncodeStage = "Closing live encoder…";
                EncodePercent = 40;
                try { await _pipe.FinishAsync(CancellationToken.None).ConfigureAwait(false); }
                catch { /* fallback JPEG encode still available */ }
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

        private async Task LoopAsync(string folder, CancellationToken ct)
        {
            int fps = Math.Max(1, _settings.Fps);
            var interval = TimeSpan.FromMilliseconds(1000.0 / fps);
            int i = 0;

            while (!ct.IsCancellationRequested)
            {
                if (_paused)
                {
                    try { await Task.Delay(80, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                    continue;
                }

                var captureStart = DateTime.UtcNow;

                if (Volatile.Read(ref _pendingEncodes) >= MaxPendingEncodes
                    || Volatile.Read(ref _captureBusy) != 0)
                {
                    DroppedFrames++;
                }
                else
                {
                    string path = System.IO.Path.Combine(folder, $"f{i:00000}.jpg");
                    var shot = await CapturePixelsAsync(ct).ConfigureAwait(false);
                    if (shot != null)
                    {
                        i++;
                        Interlocked.Increment(ref _pendingEncodes);
                        var copy = shot;
                        _ = Task.Run(() => EncodeJpeg(copy, path), CancellationToken.None);
                    }
                }

                // Sleep for the remainder of the target interval, so captures
                // are evenly spaced regardless of how long the capture took.
                // If the capture took longer than the interval, we track it as
                // a drop and fire the next frame immediately (catch up).
                var elapsed = DateTime.UtcNow - captureStart;
                var remaining = interval - elapsed;
                if (remaining > TimeSpan.Zero)
                {
                    try { await Task.Delay(remaining, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                }
                else if (-remaining > interval)
                {
                    int missed = (int)(-remaining.TotalMilliseconds / interval.TotalMilliseconds) - 1;
                    if (missed > 0) DroppedFrames += missed;
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
        private async Task<PixelShot?> CapturePixelsAsync(CancellationToken ct)
        {
            var disp = Application.Current?.Dispatcher;
            if (disp == null) return null;
            if (Interlocked.Exchange(ref _captureBusy, 1) != 0) return null;

            try
            {
                // DispatcherPriority.Background ensures user input events
                // are always processed before frame capture. Normal priority
                // caused the app to freeze because RenderTargetBitmap.Render
                // forces a full re-layout on every capture.
                var shot = await disp.InvokeAsync(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    return CaptureOnUi();
                }, DispatcherPriority.Background, ct).Task.ConfigureAwait(false);

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

        private PixelShot? CaptureOnUi()
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
                RenderOptions.SetBitmapScalingMode(window, BitmapScalingMode.HighQuality);
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
            catch { /* cursor overlay is optional */ }

            DrawPointer(pixels, capW, capH, cx, cy);

            return new PixelShot
            {
                Pixels = pixels,
                Width = capW,
                Height = capH,
                CursorX = cx,
                CursorY = cy,
                Timestamp = DateTime.UtcNow - _started
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
                    EncodedFrames++;
                }
            }
            catch
            {
                DroppedFrames++;
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
            _audio.Dispose();
            _mic.Dispose();
        }
    }
}
