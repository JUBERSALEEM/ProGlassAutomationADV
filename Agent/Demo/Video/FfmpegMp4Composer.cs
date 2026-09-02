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
                catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[FfmpegMp4Composer] FindCore: try next: " + ex.Message); }
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
                catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[FfmpegMp4Composer] FindProbe: try next: " + ex.Message); }
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
            bool hasAudio = wavPath != null && File.Exists(wavPath);
            string vf =
                $"scale={w}:{h}:flags=lanczos:force_original_aspect_ratio=decrease," +
                $"pad={w}:{h}:(ow-iw)/2:(oh-ih)/2," +
                // Screen content benefits from stronger luma sharpening than
                // camera footage. Keep chroma untouched to avoid colour halos.
                "unsharp=5:5:0.8:5:5:0.0,format=yuv420p" +
                // If live loopback is unavailable, narration is synthesized
                // after capture and can outlast the frames. Clone the final
                // frame so -shortest follows audio instead of cutting speech.
                (hasAudio ? ",tpad=stop_mode=clone:stop_duration=600" : "");

            Directory.CreateDirectory(Path.GetDirectoryName(mp4Path)!);

            // Encode beside the destination and publish with one atomic move.
            // A 4K encode can run for minutes; writing directly to the final
            // name made Explorer show a half-built "video.mp4" next to raw
            // frames, which looked like an unpacked recording and was unsafe
            // to open/copy. Only a completely validated ISO-BMFF is exposed.
            string packingPath = mp4Path + ".packing.mp4";
            try { if (File.Exists(packingPath)) File.Delete(packingPath); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[FfmpegMp4Composer] Stale packing-file delete failed: " + ex.Message); }
            string args = wavPath != null && File.Exists(wavPath)
                ? $"-y -framerate {fps} -i \"{input}\" -i \"{wavPath}\" " +
                  $"-vf \"{vf}\" -c:v libx264 -preset slow -tune animation -crf {crf} -profile:v high -level:v 5.2 " +
                  $"-c:a aac -b:a 320k -ar 48000 -shortest -movflags +faststart \"{packingPath}\""
                : $"-y -framerate {fps} -i \"{input}\" " +
                  $"-vf \"{vf}\" -c:v libx264 -preset slow -tune animation -crf {crf} -profile:v high -level:v 5.2 " +
                  $"-movflags +faststart \"{packingPath}\"";

            string? packed = await RunComposeAsync(ffmpeg, args, packingPath, ct).ConfigureAwait(false);
            if (packed == null) return null;
            try
            {
                File.Move(packingPath, mp4Path, overwrite: true);
                return IsValidMp4(mp4Path) ? mp4Path : null;
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[ffmpeg publish] " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// JPEG list + optional WAV → H.264/AAC MP4, using the CONCAT
        /// demuxer instead of the f%05d.jpg image2 pattern.
        /// <para>
        /// 2026-08-25 FIX — "ffmpeg ran but did not leave a valid MP4":
        /// dropped captures leave holes in the f00000.jpg, f00001.jpg…
        /// sequence, and the image2 demuxer STOPS at the first missing
        /// number (or errors out entirely when frame zero is absent).
        /// The result was a truncated or empty encode that failed the
        /// ftyp validation. The concat demuxer reads an explicit list of
        /// the files that actually exist, so dropped frames are simply
        /// skipped and every captured frame reaches the video.
        /// </para>
        /// </summary>
        /// <summary>
        /// Muxes an ALREADY-ENCODED H.264 MP4 with an optional WAV track.
        ///
        /// The video is STREAM-COPIED (-c:v copy): the off-thread capture
        /// pipeline already produced final-quality H.264, so re-encoding it
        /// would add generation loss and several seconds of CPU for nothing.
        /// Only the audio is encoded.
        ///
        /// With no audio this is a plain file move, which is as fast as the
        /// save can possibly be.
        /// </summary>
        public static async Task<string?> MuxVideoWithAudioAsync(
            string videoPath, string? wavPath, string mp4Path, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
                return null;

            bool hasAudio = !string.IsNullOrWhiteSpace(wavPath) && File.Exists(wavPath);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(mp4Path)!);

                // No audio: nothing to mux. Move the finished file into
                // place rather than paying for a pointless remux.
                if (!hasAudio)
                {
                    if (!string.Equals(
                            Path.GetFullPath(videoPath),
                            Path.GetFullPath(mp4Path),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        File.Move(videoPath, mp4Path, overwrite: true);
                    }
                    return IsValidMp4(mp4Path) ? mp4Path : null;
                }

                string? ffmpeg = Find();
                if (ffmpeg == null) return null;

                string packingPath = mp4Path + ".packing.mp4";
                try { if (File.Exists(packingPath)) File.Delete(packingPath); }
                catch (Exception ex) { AppLogService.Debug("[Mux] stale packing delete: " + ex.Message); }

                // -shortest: the WAV can outrun the video by a few frames
                // because audio capture stops fractionally later.
                string args =
                    $"-y -i \"{videoPath}\" -i \"{wavPath}\" " +
                    $"-c:v copy -c:a aac -b:a 320k -ar 48000 " +
                    $"-shortest -movflags +faststart \"{packingPath}\"";

                string? packed = await RunComposeAsync(ffmpeg, args, packingPath, ct)
                    .ConfigureAwait(false);
                if (packed == null) return null;

                File.Move(packingPath, mp4Path, overwrite: true);
                return IsValidMp4(mp4Path) ? mp4Path : null;
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Mux of captured video failed: {ex.Message}",
                    new LogContext { Module = "Recording", Operation = "MuxVideoWithAudio" });
                return null;
            }
        }

        public static async Task<string?> ComposeFrameListAudioAsync(
            System.Collections.Generic.IReadOnlyList<string> frameFiles,
            string? wavPath, string mp4Path,
            int fps, int width, int height, CancellationToken ct,
            int crf = 16)
        {
            string? ffmpeg = Find();
            if (ffmpeg == null || frameFiles == null || frameFiles.Count == 0)
                return null;

            fps = Math.Clamp(fps, 1, 60);
            crf = Math.Clamp(crf, 12, 23);
            int w = Math.Max(2, width) & ~1;
            int h = Math.Max(2, height) & ~1;
            bool hasAudio = wavPath != null && File.Exists(wavPath);

            // ── Build the concat list from the frames that exist ──────
            string listPath;
            try
            {
                string? dir = Path.GetDirectoryName(Path.GetFullPath(frameFiles[0] ?? ""));
                if (string.IsNullOrEmpty(dir)) return null;
                listPath = Path.Combine(dir, "concat-" + Guid.NewGuid().ToString("N") + ".txt");

                double dur = 1.0 / fps;
                var sb = new System.Text.StringBuilder();
                string? lastEntry = null;
                foreach (var f in frameFiles)
                {
                    if (string.IsNullOrEmpty(f) || !File.Exists(f)) continue;
                    string entry = "file '" + f.Replace("'", "'\\''") + "'";
                    sb.AppendLine(entry);
                    sb.AppendLine("duration " + dur.ToString("0.00000", CultureInfo.InvariantCulture));
                    lastEntry = entry;
                }
                // Repeat the final frame so its duration is honoured —
                // concat drops the duration of the very last entry.
                if (lastEntry != null) sb.AppendLine(lastEntry);

                File.WriteAllText(listPath, sb.ToString());
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[ffmpeg concat-list] " + ex.Message);
                return null;
            }

            try
            {
                string vf =
                    $"scale={w}:{h}:flags=lanczos:force_original_aspect_ratio=decrease," +
                    $"pad={w}:{h}:(ow-iw)/2:(oh-ih)/2," +
                    "unsharp=5:5:0.8:5:5:0.0,format=yuv420p" +
                    (hasAudio ? ",tpad=stop_mode=clone:stop_duration=600" : "");

                Directory.CreateDirectory(Path.GetDirectoryName(mp4Path)!);

                string packingPath = mp4Path + ".packing.mp4";
                try { if (File.Exists(packingPath)) File.Delete(packingPath); }
                catch (Exception ex) { AppLogService.Debug("[FfmpegMp4Composer] Stale packing-file delete failed: " + ex.Message); }

                string args = hasAudio
                    ? $"-y -f concat -safe 0 -i \"{listPath}\" -i \"{wavPath}\" " +
                      $"-vf \"{vf}\" -c:v libx264 -preset slow -tune animation -crf {crf} -profile:v high -level:v 5.2 " +
                      $"-c:a aac -b:a 320k -ar 48000 -shortest -movflags +faststart \"{packingPath}\""
                    : $"-y -f concat -safe 0 -i \"{listPath}\" " +
                      $"-vf \"{vf}\" -c:v libx264 -preset slow -tune animation -crf {crf} -profile:v high -level:v 5.2 " +
                      $"-movflags +faststart \"{packingPath}\"";

                string? packed = await RunComposeAsync(ffmpeg, args, packingPath, ct)
                    .ConfigureAwait(false);
                if (packed == null) return null;

                File.Move(packingPath, mp4Path, overwrite: true);
                return IsValidMp4(mp4Path) ? mp4Path : null;
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[ffmpeg concat publish] " + ex.Message);
                return null;
            }
            finally
            {
                try { if (File.Exists(listPath)) File.Delete(listPath); }
                catch (Exception ex) { AppLogService.Debug("[ffmpeg concat-list cleanup] " + ex.Message); }
            }
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
                  $"-c:v copy -c:a aac -b:a 320k -ar 48000 -shortest " +
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
                    catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[FfmpegMp4Composer] RunComposeAsync: already gone: " + ex.Message); }
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
                try { errTask.Wait(500); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[FfmpegMp4Composer] TryReadDurationSeconds: drained or gone: " + ex.Message); }
                text = (text ?? "").Trim();
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double sec)
                    && sec > 0)
                    return sec;
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[FfmpegMp4Composer] TryReadDurationSeconds: unmeasured: " + ex.Message); }
            return null;
        }
    }
}
