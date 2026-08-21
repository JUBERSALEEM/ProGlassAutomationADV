using System;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Common;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo
{
    public sealed class DemoStepResult
    {
        public string Title { get; init; } = "";
        public string Narration { get; init; } = "";
        public string FramePath { get; init; } = "";
        public bool Navigated { get; init; }
        public string Log { get; init; } = "";
    }

    /// <summary>
    /// Writes a demo package: frames, narration, SRT/VTT, HTML player, log.
    /// MP4 is produced only when ffmpeg is actually on PATH — never faked.
    /// </summary>
    public static class DemoPackageComposer
    {
        public static string RootFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation", "DemoVideos");

        public static string NewJobFolder(string module)
        {
            string safe = string.Concat((module ?? "Demo").Where(ch =>
                char.IsLetterOrDigit(ch) || ch is '-' or '_'));
            if (safe.Length == 0) safe = "Demo";
            string dir = Path.Combine(RootFolder, safe, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(dir);
            Directory.CreateDirectory(Path.Combine(dir, "frames"));
            return dir;
        }

        public static void WritePackage(string folder, string title, IReadOnlyList<DemoStepResult> steps)
        {
            var narration = new StringBuilder();
            var srt = new StringBuilder();
            var vtt = new StringBuilder();
            vtt.AppendLine("WEBVTT");
            vtt.AppendLine();

            int t = 0;
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                narration.AppendLine($"[{i + 1}] {step.Title}");
                narration.AppendLine(step.Narration);
                narration.AppendLine();

                int start = t;
                int dur = Math.Max(4, (step.Narration ?? "").Length / 18);
                int end = start + dur;
                srt.AppendLine((i + 1).ToString());
                srt.AppendLine($"{Fmt(start)} --> {Fmt(end)}");
                srt.AppendLine(step.Narration);
                srt.AppendLine();
                vtt.AppendLine($"{Fmt(start)} --> {Fmt(end)}");
                vtt.AppendLine(step.Narration);
                vtt.AppendLine();
                t = end;
            }

            File.WriteAllText(Path.Combine(folder, "narration.txt"), narration.ToString());
            File.WriteAllText(Path.Combine(folder, "captions.srt"), srt.ToString());
            File.WriteAllText(Path.Combine(folder, "captions.vtt"), vtt.ToString());
            File.WriteAllText(Path.Combine(folder, "log.json"),
                System.Text.Json.JsonSerializer.Serialize(steps.Select(s => new
                {
                    s.Title, s.Narration, s.FramePath, s.Navigated, s.Log
                })));
            File.WriteAllText(Path.Combine(folder, "index.html"), BuildHtml(title, steps));
            File.WriteAllText(Path.Combine(folder, "README.txt"),
                "ProGlass demo package\n\n" +
                "This folder is the demo output. Frames are PNG screenshots of " +
                "the live application. captions.srt / .vtt match the narration beats.\n\n" +
                "MP4 is written only if ffmpeg is installed on this machine. " +
                "If video.mp4 is missing, open index.html in a browser — that is the " +
                "validated player, not a placeholder claim of a movie file.\n");
        }

        public static async Task<string?> TryFfmpegAsync(string folder, CancellationToken ct)
        {
            string? ffmpeg = FindFfmpeg();
            if (ffmpeg == null) return null;

            string list = Path.Combine(folder, "frames.txt");
            var frames = Directory.GetFiles(Path.Combine(folder, "frames"), "*.png")
                .OrderBy(f => f)
                .ToList();
            if (frames.Count == 0) return null;

            var sb = new StringBuilder();
            foreach (var f in frames)
                sb.AppendLine($"file '{f.Replace("'", "'\\''")}'\nduration 4");
            sb.AppendLine($"file '{frames[^1].Replace("'", "'\\''")}'");
            File.WriteAllText(list, sb.ToString());

            string mp4 = Path.Combine(folder, "video.mp4");
            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = $"-y -f concat -safe 0 -i \"{list}\" -pix_fmt yuv420p \"{mp4}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            try
            {
                using var proc = Process.Start(psi);
                if (proc == null) return null;

                // P-1: stderr is redirected, so it MUST be drained before
                // waiting. ffmpeg writes a progress line per frame; once
                // the ~4 KB pipe buffer fills it blocks on write and this
                // await never returns — the demo export hangs and leaves an
                // orphaned ffmpeg process behind. Reading concurrently is
                // what lets the process actually reach exit.
                var errTask = proc.StandardError.ReadToEndAsync();

                await proc.WaitForExitAsync(ct).ConfigureAwait(false);
                string err = await errTask.ConfigureAwait(false);

                if (proc.ExitCode == 0 && File.Exists(mp4)) return mp4;

                AppLogService.Debug(
                    $"[Demo ffmpeg] exit {proc.ExitCode}: " +
                    (err.Length > 300 ? err[^300..] : err));
                return null;
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[Demo ffmpeg] {ex.Message}");
                return null;
            }
        }

        public static string? FindFfmpeg()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = "-version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                if (p == null) return null;
                p.WaitForExit(3000);
                return p.ExitCode == 0 ? "ffmpeg" : null;
            }
            catch { return null; }
        }

        private static string Fmt(int seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00},000";
        }

        private static string BuildHtml(string title, IReadOnlyList<DemoStepResult> steps)
        {
            var cards = new StringBuilder();
            foreach (var s in steps)
            {
                string rel = string.IsNullOrEmpty(s.FramePath)
                    ? ""
                    : "frames/" + Path.GetFileName(s.FramePath);
                cards.AppendLine("<article class='beat'>");
                if (rel.Length > 0)
                    cards.AppendLine($"<img src='{rel}' alt='{Esc(s.Title)}'/>");
                cards.AppendLine($"<h2>{Esc(s.Title)}</h2>");
                cards.AppendLine($"<p>{Esc(s.Narration)}</p>");
                cards.AppendLine("</article>");
            }

            return $@"<!doctype html>
<html lang='en'><head><meta charset='utf-8'/>
<title>{Esc(title)}</title>
<style>
body{{margin:0;font:15px/1.5 Segoe UI,sans-serif;background:#0B1F3A;color:#F8FAFC}}
header{{padding:28px 36px;border-bottom:3px solid #C9A227}}
h1{{margin:0;font-size:22px;letter-spacing:.04em}}
main{{padding:28px 36px;display:grid;gap:22px}}
.beat{{background:#10263f;border-radius:12px;overflow:hidden}}
.beat img{{width:100%;display:block;background:#061326}}
.beat h2{{margin:14px 18px 4px;font-size:15px;color:#C9A227}}
.beat p{{margin:0 18px 16px;color:#E8EEF5}}
</style></head>
<body><header><h1>{Esc(title)}</h1>
<p>Verified workflow storyboard — ProGlass Automation</p></header>
<main>{cards}</main></body></html>";
        }

        private static string Esc(string? s) =>
            (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
