using System;
using System.Collections.Generic;
using System.IO;
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
        private const int MaxPendingEncodes = 8;

        private readonly List<RecordedFrame> _frames = new();
        private readonly object _framesGate = new();
        private readonly WasapiLoopbackRecorder _audio = new();
        private readonly WasapiMicRecorder _mic = new();
        private CancellationTokenSource? _cts;
        private Task? _loop;
        private DateTime _started;
        private int _pendingEncodes;
        private int _captureBusy;
        private ScreenRecordSettings _settings = new();

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
        public ScreenRecordSettings Settings => _settings;

        public void Start(string framesFolder, int fps) =>
            Start(framesFolder, new ScreenRecordSettings { Fps = fps });

        public void Start(string framesFolder, ScreenRecordSettings settings)
        {
            try { _cts?.Cancel(); } catch { }
            _settings = (settings ?? new ScreenRecordSettings()).Clone().Clamp();
            Directory.CreateDirectory(framesFolder);
            lock (_framesGate) _frames.Clear();
            DroppedFrames = 0;
            EncodedFrames = 0;
            Duration = TimeSpan.Zero;
            _started = DateTime.UtcNow;
            _cts = new CancellationTokenSource();

            LiveAudioStarted = false;
            MicAudioStarted = false;
            string parent = Directory.GetParent(framesFolder)?.FullName ?? framesFolder;
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
                catch (OperationCanceledException) { }
            }
            _loop = null;

            var until = DateTime.UtcNow.AddSeconds(8);
            while (Volatile.Read(ref _pendingEncodes) > 0 && DateTime.UtcNow < until)
                await Task.Delay(40).ConfigureAwait(false);

            Duration = DateTime.UtcNow - _started;
            try { await _audio.StopAsync().ConfigureAwait(false); }
            catch { /* loopback is optional */ }
            try { await _mic.StopAsync().ConfigureAwait(false); }
            catch { /* mic is optional */ }
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
            var next = DateTime.UtcNow;
            int i = 0;

            while (!ct.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                if (now < next)
                {
                    try { await Task.Delay(next - now, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                    now = DateTime.UtcNow;
                }
                else if (now > next + interval + interval)
                {
                    int missed = (int)((now - next).TotalMilliseconds / interval.TotalMilliseconds) - 1;
                    if (missed > 0) DroppedFrames += missed;
                    next = now;
                }

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

                next += interval;
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

        private Task<PixelShot?> CapturePixelsAsync(CancellationToken ct)
        {
            var disp = Application.Current?.Dispatcher;
            if (disp == null) return Task.FromResult<PixelShot?>(null);
            Interlocked.Exchange(ref _captureBusy, 1);
            try
            {
                return disp.InvokeAsync(() =>
                {
                    try
                    {
                        ct.ThrowIfCancellationRequested();
                        return CaptureOnUi();
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _captureBusy, 0);
                    }
                }, DispatcherPriority.Background, ct).Task
                    .ContinueWith(t =>
                    {
                        Interlocked.Exchange(ref _captureBusy, 0);
                        if (t.IsCanceled || t.IsFaulted) return null;
                        return t.Result;
                    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            catch
            {
                Interlocked.Exchange(ref _captureBusy, 0);
                return Task.FromResult<PixelShot?>(null);
            }
        }

        private PixelShot? CaptureOnUi()
        {
            var window = Application.Current?.MainWindow;
            if (window == null || window.ActualWidth < 2 || window.ActualHeight < 2)
                return null;

            var (targetW, targetH) = _settings.CaptureSize;
            double dipW = Math.Max(1, window.ActualWidth);
            double dipH = Math.Max(1, window.ActualHeight);
            var dpiInfo = VisualTreeHelper.GetDpi(window);
            double nativeW = dipW * dpiInfo.DpiScaleX;
            double nativeH = dipH * dpiInfo.DpiScaleY;
            double scale = Math.Max(
                Math.Min(targetW / Math.Max(1, nativeW), targetH / Math.Max(1, nativeH)),
                1.0);

            int capW = Math.Max(2, ((int)Math.Round(nativeW * scale)) & ~1);
            int capH = Math.Max(2, ((int)Math.Round(nativeH * scale)) & ~1);
            capW = Math.Min(capW, targetW);
            capH = Math.Min(capH, targetH);
            int dpi = Math.Max(96, (int)Math.Round(96 * dpiInfo.DpiScaleX * scale));

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
                var dpiInfo = VisualTreeHelper.GetDpi(window);
                double pxW = dipW * dpiInfo.DpiScaleX;
                double pxH = dipH * dpiInfo.DpiScaleY;
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
                var enc = new JpegBitmapEncoder { QualityLevel = _settings.JpegQualityFor };
                enc.Frames.Add(BitmapFrame.Create(src));
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
                using (var fs = File.Create(path))
                    enc.Save(fs);

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
            try { Stop(); } catch { }
            try { _cts?.Dispose(); } catch { }
            _audio.Dispose();
        }
    }
}
