using System;
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
                    using var p = Process.Start(psi);
                    if (p == null) continue;
                    if (!p.WaitForExit(1500)) { try { p.Kill(); } catch { } continue; }
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
                    using var p = Process.Start(psi);
                    if (p == null) continue;
                    if (!p.WaitForExit(4000)) { try { p.Kill(); } catch { } continue; }
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
            int fps, int width, int height, CancellationToken ct)
        {
            string? ffmpeg = Find();
            if (ffmpeg == null) return null;
            if (string.IsNullOrWhiteSpace(framesFolder) || !Directory.Exists(framesFolder))
                return null;

            fps = Math.Clamp(fps, 1, 60);
            int w = Math.Max(2, width) & ~1;
            int h = Math.Max(2, height) & ~1;
            string input = Path.Combine(framesFolder, pattern);
            string vf =
                $"scale={w}:{h}:force_original_aspect_ratio=decrease," +
                $"pad={w}:{h}:(ow-iw)/2:(oh-ih)/2,format=yuv420p";

            Directory.CreateDirectory(Path.GetDirectoryName(mp4Path)!);
            string args = wavPath != null && File.Exists(wavPath)
                ? $"-y -framerate {fps} -i \"{input}\" -i \"{wavPath}\" " +
                  $"-vf \"{vf}\" -c:v libx264 -preset veryfast -crf 20 " +
                  $"-c:a aac -b:a 192k -shortest -movflags +faststart \"{mp4Path}\""
                : $"-y -framerate {fps} -i \"{input}\" " +
                  $"-vf \"{vf}\" -c:v libx264 -preset veryfast -crf 20 " +
                  $"-movflags +faststart \"{mp4Path}\"";

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
                using var p = Process.Start(psi);
                if (p == null) return null;
                await p.WaitForExitAsync(ct).ConfigureAwait(false);
                return p.ExitCode == 0 && IsValidMp4(mp4Path) ? mp4Path : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ffmpeg] " + ex.Message);
                return null;
            }
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
                using var p = Process.Start(psi);
                if (p == null) return null;
                string text = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(5000)) { try { p.Kill(); } catch { } return null; }
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
