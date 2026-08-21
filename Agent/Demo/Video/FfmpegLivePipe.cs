using System;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Common;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Video
{
    /// <summary>
    /// Live H.264 encode: JPEG frames are piped into ffmpeg while the
    /// user records. Stop then only remuxes audio — no second full encode.
    /// </summary>
    public sealed class FfmpegLivePipe : IDisposable
    {
        private Process? _process;
        private Stream? _stdin;
        private readonly object _gate = new();
        private bool _closed;

        public string VideoPath { get; }
        public bool IsOpen { get; private set; }
        public int FramesPiped { get; private set; }
        public long BytesPiped { get; private set; }
        public string? Error { get; private set; }

        private FfmpegLivePipe(string videoPath)
        {
            VideoPath = videoPath;
        }

        public static FfmpegLivePipe? TryStart(
            string videoPath, int fps, int width, int height, int crf)
        {
            string? ffmpeg = FfmpegMp4Composer.Find();
            if (ffmpeg == null) return null;

            fps = Math.Clamp(fps, 1, 60);
            crf = Math.Clamp(crf, 14, 23);
            int w = Math.Max(2, width) & ~1;
            int h = Math.Max(2, height) & ~1;
            Directory.CreateDirectory(Path.GetDirectoryName(videoPath)!);

            string vf =
                $"scale={w}:{h}:flags=lanczos:force_original_aspect_ratio=decrease," +
                $"pad={w}:{h}:(ow-iw)/2:(oh-ih)/2,format=yuv420p";
            string args =
                $"-y -f image2pipe -vcodec mjpeg -framerate {fps} -i - " +
                $"-vf \"{vf}\" -c:v libx264 -preset ultrafast -tune zerolatency " +
                $"-crf {crf} -pix_fmt yuv420p -movflags +faststart \"{videoPath}\"";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpeg,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                var toolResult = ProcessStartGuard.TryStartTool(psi.FileName, psi.Arguments, CorrelationContext.NewId());
                var p = toolResult.IsSuccess ? toolResult.Value : null;
                if (p == null) return null;
                var pipe = new FfmpegLivePipe(videoPath)
                {
                    _process = p,
                    _stdin = p.StandardInput.BaseStream,
                    IsOpen = true
                };
                _ = Task.Run(() =>
                {
                    try { p.StandardError.ReadToEnd(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
                });
                return pipe;
            }
            catch (Exception ex2)
            {
                AppLogService.Debug("[FfmpegLivePipe] start failed: " + ex2.Message);
                return null;
            }
        }

        public bool WriteJpeg(byte[] jpeg)
        {
            if (jpeg == null || jpeg.Length == 0) return false;
            lock (_gate)
            {
                if (!IsOpen || _stdin == null || _closed) return false;
                try
                {
                    _stdin.Write(jpeg, 0, jpeg.Length);
                    FramesPiped++;
                    BytesPiped += jpeg.Length;
                    return true;
                }
                catch (Exception ex)
                {
                    Error = ex.Message;
                    IsOpen = false;
                    return false;
                }
            }
        }

        public async Task<bool> FinishAsync(CancellationToken ct)
        {
            Process? p;
            lock (_gate)
            {
                _closed = true;
                IsOpen = false;
                p = _process;
                try { _stdin?.Flush(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
                try { _stdin?.Close(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
                _stdin = null;
            }

            if (p == null) return File.Exists(VideoPath) && new FileInfo(VideoPath).Length > 32;
            try
            {
                // Timeout guard: a wedged ffmpeg must never hang STOP.
                // image2pipe normally exits on stdin EOF; 8s covers the
                // final H.264 flush even on a slow laptop.
                var waitTask = p.WaitForExitAsync(ct);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(8), ct);
                var exited = await Task.WhenAny(waitTask, timeoutTask).ConfigureAwait(false);

                if (exited == waitTask)
                {
                    return p.ExitCode == 0 && FfmpegMp4Composer.IsValidMp4(VideoPath);
                }

                // Timed out — kill so the caller can fall back to JPEG.
                Error = "ffmpeg did not exit in time.";
                try { if (!p.HasExited) p.Kill(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
                return false;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                try { if (!p.HasExited) p.Kill(); } catch (Exception ex2) { AppLogService.Warning($"Suppressed exception: {ex2.Message}"); }
                return false;
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _closed = true;
                IsOpen = false;
                try { _stdin?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
                _stdin = null;
            }
            try
            {
                if (_process != null && !_process.HasExited)
                    _process.Kill();
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            try { _process?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            _process = null;
        }
    }
}
