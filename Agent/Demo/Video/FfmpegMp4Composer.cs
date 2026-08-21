using System;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Video
{
    public static class FfmpegMp4Composer
    {
        /// <summary>Official FFmpeg download index.</summary>
        public const string OfficialDownloadPageUrl = "https://ffmpeg.org/download.html";

        /// <summary>
        /// Direct Windows essentials zip (ffmpeg + ffprobe) from the
        /// builds FFmpeg.org itself lists for Windows.
        /// </summary>
        public const string WindowsEssentialsZipUrl =
            "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

        public static string ToolsFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation", "tools");

        private static string? _cached;
        private static bool _looked;

        /// <summary>Forget the last lookup so Recheck hits disk/PATH again.</summary>
        public static void InvalidateCache()
        {
            _looked = false;
            _cached = null;
        }

        public static string? Find()
        {
            if (_looked) return _cached;
            _cached = FindCore();
            _looked = true;
            return _cached;
        }

        private static string? FindCore()
        {
            string local = Path.Combine(ToolsFolder, "ffmpeg.exe");
            if (File.Exists(local)) return local;

            foreach (var name in new[] { "ffmpeg", "ffmpeg.exe" })
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = name,
                        Arguments = "-version",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                                    var toolResult = ProcessStartGuard.TryStartTool(psi.FileName, psi.Arguments, CorrelationContext.NewId());
                using var p = toolResult.IsSuccess ? toolResult.Value : null;
                    if (p == null) continue;
                    if (!p.WaitForExit(1500)) { try { p.Kill(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); } continue; }
                    if (p.ExitCode == 0) return name;
                }
                catch { /* try next */ }
            }

            return null;
        }

        public static string? FindProbe()
        {
            foreach (var name in new[] { "ffprobe", "ffprobe.exe" })
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = name,
                        Arguments = "-version",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                                    var toolResult = ProcessStartGuard.TryStartTool(psi.FileName, psi.Arguments, CorrelationContext.NewId());
                using var p = toolResult.IsSuccess ? toolResult.Value : null;
                    if (p == null) continue;
                    if (!p.WaitForExit(4000)) { try { p.Kill(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); } continue; }
                    if (p.ExitCode == 0) return name;
                }
                catch { /* try next */ }
            }

            string local = Path.Combine(ToolsFolder, "ffprobe.exe");
            return File.Exists(local) ? local : null;
        }

        public static async Task<string?> ComposeAviWavAsync(
            string aviPath, string? wavPath, string mp4Path, CancellationToken ct)
        {
            string? ffmpeg = Find();
            if (ffmpeg == null) return null;
            if (!File.Exists(aviPath)) return null;

            Directory.CreateDirectory(Path.GetDirectoryName(mp4Path)!);
            string args = wavPath != null && File.Exists(wavPath)
                ? $"-y -i \"{aviPath}\" -i \"{wavPath}\" " +
                  $"-c:v libx264 -pix_fmt yuv420p -c:a aac -shortest \"{mp4Path}\""
                : $"-y -i \"{aviPath}\" -c:v libx264 -pix_fmt yuv420p \"{mp4Path}\"";

            return await RunComposeAsync(ffmpeg, args, mp4Path, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// JPEG sequence + optional WAV → H.264/AAC MP4 at the requested
        /// size and frame rate. Even dimensions are forced for yuv420p.
        /// </summary>
        public static async Task<string?> ComposeFramesAudioAsync(
            string framesFolder, string pattern, string? wavPath, string mp4Path,
            int fps, int width, int height, CancellationToken ct,
            int crf = 16)
        {
            string? ffmpeg = Find();
            if (ffmpeg == null) return null;
            if (string.IsNullOrWhiteSpace(framesFolder) || !Directory.Exists(framesFolder))
                return null;

            fps = Math.Clamp(fps, 1, 60);
            crf = Math.Clamp(crf, 12, 23);
            int w = Math.Max(2, width) & ~1;
            int h = Math.Max(2, height) & ~1;
            string input = Path.Combine(framesFolder, pattern);
            string vf =
                $"scale={w}:{h}:flags=lanczos:force_original_aspect_ratio=decrease," +
                $"pad={w}:{h}:(ow-iw)/2:(oh-ih)/2," +
                "unsharp=5:5:0.6:5:5:0.0,format=yuv420p";

            Directory.CreateDirectory(Path.GetDirectoryName(mp4Path)!);
            string args = wavPath != null && File.Exists(wavPath)
                ? $"-y -framerate {fps} -i \"{input}\" -i \"{wavPath}\" " +
                  $"-vf \"{vf}\" -c:v libx264 -preset medium -crf {crf} -profile:v high " +
                  $"-c:a aac -b:a 256k -ar 48000 -shortest -movflags +faststart \"{mp4Path}\""
                : $"-y -framerate {fps} -i \"{input}\" " +
                  $"-vf \"{vf}\" -c:v libx264 -preset medium -crf {crf} -profile:v high " +
                  $"-movflags +faststart \"{mp4Path}\"";

            return await RunComposeAsync(ffmpeg, args, mp4Path, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Mix loopback + microphone into one WAV. Returns the mixed path
        /// or the single surviving input. Null if neither file is usable.
        /// </summary>
        public static async Task<string?> MixAudioAsync(
            string? loopbackWav, string? micWav, string outputWav, CancellationToken ct)
        {
            bool loop = !string.IsNullOrEmpty(loopbackWav) && File.Exists(loopbackWav);
            bool mic = !string.IsNullOrEmpty(micWav) && File.Exists(micWav);
            if (loop && !mic) return loopbackWav;
            if (mic && !loop) return micWav;
            if (!loop && !mic) return null;

            string? ffmpeg = Find();
            if (ffmpeg == null) return loopbackWav ?? micWav;

            Directory.CreateDirectory(Path.GetDirectoryName(outputWav)!);
            string args =
                $"-y -i \"{loopbackWav}\" -i \"{micWav}\" " +
                "-filter_complex \"[0:a][1:a]amix=inputs=2:duration=longest:dropout_transition=0,dynaudnorm\" " +
                $"-c:a pcm_s16le \"{outputWav}\"";

            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            try
            {
                                var toolResult = ProcessStartGuard.TryStartTool(psi.FileName, psi.Arguments, CorrelationContext.NewId());
                using var p = toolResult.IsSuccess ? toolResult.Value : null;
                if (p == null) return loopbackWav ?? micWav;

                // P-1: drain both redirected pipes before waiting, or the
                // mix deadlocks as soon as ffmpeg fills the stderr buffer.
                var mixOut = p.StandardOutput.ReadToEndAsync();
                var mixErr = p.StandardError.ReadToEndAsync();

                await p.WaitForExitAsync(ct).ConfigureAwait(false);
                await Task.WhenAll(mixOut, mixErr).ConfigureAwait(false);

                // Phase 3: No .Result — use await (tasks already completed after WhenAll)
                var mixErrText = await mixErr.ConfigureAwait(false);

                if (p.ExitCode == 0 && File.Exists(outputWav) && new FileInfo(outputWav).Length > 44)
                    return outputWav;

                AppLogService.Debug(
                    $"[ffmpeg mix] exit {p.ExitCode}: {Tail(mixErrText)}");
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[ffmpeg mix] " + ex.Message);
            }
            return loopbackWav ?? micWav;
        }

        /// <summary>
        /// Copy an already-encoded video and mux a WAV. Seconds, not a
        /// second full encode — this is the instant-save path.
        /// </summary>
        public static async Task<string?> MuxCopyVideoAudioAsync(
            string videoPath, string? wavPath, string mp4Path, CancellationToken ct)
        {
            string? ffmpeg = Find();
            if (ffmpeg == null || !File.Exists(videoPath)) return null;
            Directory.CreateDirectory(Path.GetDirectoryName(mp4Path)!);
            string args = wavPath != null && File.Exists(wavPath)
                ? $"-y -i \"{videoPath}\" -i \"{wavPath}\" " +
                  $"-c:v copy -c:a aac -b:a 256k -ar 48000 -shortest " +
                  $"-movflags +faststart \"{mp4Path}\""
                : $"-y -i \"{videoPath}\" -c:v copy -movflags +faststart \"{mp4Path}\"";
            return await RunComposeAsync(ffmpeg, args, mp4Path, ct).ConfigureAwait(false);
        }

        public static async Task<string?> ComposeAsync(
            string framesGlobOrConcat, string? wavPath, string mp4Path,
            int fps, CancellationToken ct)
        {
            string? ffmpeg = Find();
            if (ffmpeg == null) return null;

            Directory.CreateDirectory(Path.GetDirectoryName(mp4Path)!);
            string args = wavPath != null && File.Exists(wavPath)
                ? $"-y -framerate {fps} -i \"{framesGlobOrConcat}\" -i \"{wavPath}\" " +
                  $"-c:v libx264 -pix_fmt yuv420p -c:a aac -shortest \"{mp4Path}\""
                : $"-y -framerate {fps} -i \"{framesGlobOrConcat}\" " +
                  $"-c:v libx264 -pix_fmt yuv420p \"{mp4Path}\"";

            return await RunComposeAsync(ffmpeg, args, mp4Path, ct).ConfigureAwait(false);
        }

        // ══════════════════════════════════════════════════════════════
        //  AUDIT P-1 — PIPE DEADLOCK ON ENCODE
        //
        //  WHAT WAS WRONG
        //  Both stdout and stderr were redirected and NEITHER was read
        //  before WaitForExitAsync. A redirected pipe has a fixed OS
        //  buffer (~4 KB on Windows). When it fills, the child process
        //  BLOCKS on its next write, forever, because nobody is draining
        //  it. ffmpeg is extremely chatty on stderr — it prints its banner,
        //  every input's stream metadata, then a progress line per frame —
        //  so it overruns that buffer within the first second of any real
        //  encode.
        //
        //  The result is a deadlock, not a slow path: ffmpeg waits for the
        //  app to read, the app waits for ffmpeg to exit. The awaiting
        //  task never completes, the temporary frames are never cleaned
        //  up, and the ffmpeg process survives the app.
        //
        //  It appeared to "work" only when the output happened to stay
        //  under one buffer — a very short clip — which is exactly the
        //  case a manual test uses.
        //
        //  THE FIX
        //  Start draining both pipes BEFORE awaiting exit. ReadToEndAsync
        //  returns as soon as the stream closes at process exit, so the
        //  three awaits complete together. stderr is kept and logged on
        //  failure: previously an ffmpeg error produced a bare null with
        //  its explanation discarded.
        // ══════════════════════════════════════════════════════════════
        private static async Task<string?> RunComposeAsync(
            string ffmpeg, string args, string mp4Path, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            try
            {
                                var toolResult = ProcessStartGuard.TryStartTool(psi.FileName, psi.Arguments, CorrelationContext.NewId());
                using var p = toolResult.IsSuccess ? toolResult.Value : null;
                if (p == null) return null;

                // Drain first, then wait — never the other way round.
                var stdoutTask = p.StandardOutput.ReadToEndAsync();
                var stderrTask = p.StandardError.ReadToEndAsync();

                try
                {
                    await p.WaitForExitAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // A cancelled encode must not leave ffmpeg running and
                    // holding the output file open.
                    try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
                    catch { /* already gone */ }
                    throw;
                }

                await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

                // Phase 3: No .Result — await completed tasks
                var stderrText = await stderrTask.ConfigureAwait(false);

                if (p.ExitCode == 0 && IsValidMp4(mp4Path))
                    return mp4Path;

                AppLogService.Debug(
                    $"[ffmpeg] exit {p.ExitCode}: {Tail(stderrText)}");
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[ffmpeg] " + ex.Message);
                return null;
            }
        }

        /// <summary>Last few lines of ffmpeg stderr — the part that says why.</summary>
        private static string Tail(string? text, int max = 400)
        {
            if (string.IsNullOrWhiteSpace(text)) return "(no output)";
            text = text.Trim();
            return text.Length <= max ? text : "…" + text[^max..];
        }

        /// <summary>
        /// ISO-BMFF: size(4) + 'ftyp' at offset 4. Does not treat AVI as MP4.
        /// </summary>
        public static bool IsValidMp4(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            var info = new FileInfo(path);
            if (info.Length < 32) return false;
            using var fs = File.OpenRead(path);
            var buf = new byte[12];
            if (fs.Read(buf, 0, 12) < 12) return false;
            return buf[4] == (byte)'f' && buf[5] == (byte)'t'
                && buf[6] == (byte)'y' && buf[7] == (byte)'p';
        }

        /// <summary>
        /// Duration from ffprobe when present. Null means it was not measured.
        /// Never invents a duration.
        /// </summary>
        public static double? TryReadDurationSeconds(string path)
        {
            string? probe = FindProbe();
            if (probe == null || !File.Exists(path)) return null;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = probe,
                    Arguments = "-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"" + path + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                                var toolResult = ProcessStartGuard.TryStartTool(psi.FileName, psi.Arguments, CorrelationContext.NewId());
                using var p = toolResult.IsSuccess ? toolResult.Value : null;
                if (p == null) return null;

                // P-1: stderr is redirected too. Reading only stdout leaves
                // the error pipe to fill and block ffprobe on exit, so the
                // 5s WaitForExit below would time out on any file that
                // produces warnings. Drain it concurrently.
                var errTask = p.StandardError.ReadToEndAsync();
                string text = p.StandardOutput.ReadToEnd();

                if (!p.WaitForExit(5000)) { try { p.Kill(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); } return null; }
                try { errTask.Wait(500); } catch { /* drained or gone */ }
                text = (text ?? "").Trim();
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double sec)
                    && sec > 0)
                    return sec;
            }
            catch { /* unmeasured */ }
            return null;
        }
    }
}
