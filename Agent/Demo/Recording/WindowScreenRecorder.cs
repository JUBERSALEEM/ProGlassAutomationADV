using ProGlassAutomation.Services.Export;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
    /// Captures the live main window at a stable frame rate while a workflow runs.
    /// </summary>
    public sealed class WindowScreenRecorder : IDisposable
    {
        private readonly List<RecordedFrame> _frames = new();
        private CancellationTokenSource? _cts;
        private Task? _loop;
        private DateTime _started;
        public IReadOnlyList<RecordedFrame> Frames => _frames;
        public bool IsRecording => _loop != null && !_loop.IsCompleted;

        public void Start(string framesFolder, int fps = 8)
        {
            try { _cts?.Cancel(); } catch { }
            Directory.CreateDirectory(framesFolder);
            _frames.Clear();
            _started = DateTime.UtcNow;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            int delay = Math.Max(40, 1000 / Math.Max(1, fps));
            _loop = Task.Run(() => LoopAsync(framesFolder, delay, ct));
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
        }

        public void Stop() => _cts?.Cancel();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point pos);

        [System.Runtime.InteropServices.StructLayout(
            System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct Point { public int X; public int Y; }

        private async Task LoopAsync(string folder, int delayMs, CancellationToken ct)
        {
            int i = 0;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    string path = System.IO.Path.Combine(folder, $"f{i:00000}.jpg");
                    var captured = await CaptureJpegAsync(path, ct).ConfigureAwait(false);
                    if (captured)
                    {
                    GetCursorPos(out var pos);
                    _frames.Add(new RecordedFrame
                    {
                        Path = path,
                        Timestamp = DateTime.UtcNow - _started,
                        CursorX = pos.X,
                        CursorY = pos.Y
                    });
                        i++;
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { /* keep recording */ }

                try { await Task.Delay(delayMs, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        private static Task<bool> CaptureJpegAsync(string path, CancellationToken ct)
        {
            var disp = Application.Current?.Dispatcher;
            if (disp == null) return Task.FromResult(false);
            return disp.InvokeAsync(() =>
            {
                ct.ThrowIfCancellationRequested();
                var bmp = ScreenshotService.CaptureMainWindow(ScreenshotQuality.HD);
                if (bmp == null) return false;
                var enc = new JpegBitmapEncoder { QualityLevel = 85 };
                enc.Frames.Add(BitmapFrame.Create(bmp));
                using var fs = File.Create(path);
                enc.Save(fs);
                return File.Exists(path);
            }, DispatcherPriority.Background, ct).Task;
        }

        public void Dispose()
        {
            try { Stop(); } catch { }
            _cts?.Dispose();
        }
    }
}
