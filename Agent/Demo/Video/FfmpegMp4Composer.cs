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
        public static string? Find()
        {
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
                    if (!p.WaitForExit(4000)) { try { p.Kill(); } catch { } continue; }
                    if (p.ExitCode == 0) return name;
                }
                catch { /* try next */ }
            }

            string local = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ProGlassAutomation", "tools", "ffmpeg.exe");
            return File.Exists(local) ? local : null;
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

            string local = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ProGlassAutomation", "tools", "ffprobe.exe");
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
