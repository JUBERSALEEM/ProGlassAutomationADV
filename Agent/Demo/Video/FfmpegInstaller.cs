// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Demo/Video/FfmpegInstaller.cs
// PROJECT: ProGlassAutomation
// MODULE: Demo Center — one-click ffmpeg installer
//
// PROBLEM THIS SOLVES
//   Demo recording needs ffmpeg.exe + ffprobe.exe in the tools folder.
//   The old DOWNLOAD button only opened the zip URL in the system
//   browser, which failed on locked-down machines and still left the
//   user to unzip and copy files by hand. Users reported the button
//   "not working".
//
//   Now: click DOWNLOAD & INSTALL → the app downloads the exact
//   Windows essentials zip (the build ffmpeg.org itself lists),
//   verifies it, extracts it and places ffmpeg.exe + ffprobe.exe into
//   FfmpegMp4Composer.ToolsFolder. Click RECHECK (or just wait for the
//   auto-refresh) and the FFMPEG card flips to ready.
//
// SECURITY
//   • HTTPS + exact-host re-validation of the download URL (defense in
//     depth: even if the constant is ever edited, we refuse anything
//     that is not the gyan.dev host ffmpeg.org lists).
//   • Zip magic-byte check before trusting the payload.
//   • Hard size cap (600 MB) so a hijacked response cannot fill the disk.
//   • Zip-Slip guard: every entry's full path must stay inside the
//     extract folder — archive entries cannot write outside it.
//   • Temp artefacts removed in finally.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services.Logging;

namespace ProGlassAutomation.Agent.Demo.Video
{
    /// <summary>
    /// Downloads, verifies and installs ffmpeg into the tools folder.
    /// Never throws — every outcome is a Result.
    /// </summary>
    public static class FfmpegInstaller
    {
        /// <summary>Hard cap so a hijacked response cannot fill the disk.</summary>
        private const long MaxDownloadBytes = 600L * 1024L * 1024L;

        // House rule (Phase 2): one static HttpClient per provider,
        // never per-instance — avoids socket exhaustion.
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30),
        };

        /// <summary>
        /// Full one-click install. Reports human-readable stages through
        /// <paramref name="progress"/> and the download percentage 0–100
        /// through <paramref name="percent"/> (both may be invoked on a
        /// background thread — marshal before touching the UI).
        /// </summary>
        public static async Task<Result<string>> DownloadAndInstallAsync(
            Action<string>? progress = null,
            Action<double>? percent = null,
            CancellationToken ct = default)
        {
            string corrId = CorrelationContext.NewId();
            string tempZip = Path.Combine(Path.GetTempPath(),
                $"pro-glass-ffmpeg-{Guid.NewGuid():N}.zip");
            string tempExtract = Path.Combine(Path.GetTempPath(),
                $"pro-glass-ffmpeg-{Guid.NewGuid():N}");

            try
            {
                percent?.Invoke(0);

                // ── 1. Re-validate the URL, even though the constant is ours ──
                string url = FfmpegMp4Composer.WindowsEssentialsZipUrl;
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                    uri.Scheme != Uri.UriSchemeHttps ||
                    !string.Equals(uri.Host, "www.gyan.dev",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Result<string>.Fail(
                        "Refusing to download ffmpeg from an unexpected host: " + url,
                        corrId);
                }

                progress?.Invoke("Contacting gyan.dev for the Windows essentials zip…");

                // ── 2. Download with progress ────────────────────────────────
                using (var response = await _http.GetAsync(
                    url, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return Result<string>.Fail(
                            $"gyan.dev returned HTTP {(int)response.StatusCode}. " +
                            "Try again, or use COPY LINK for a manual download.",
                            corrId);
                    }

                    long total = response.Content.Headers.ContentLength ?? -1;
                    if (total > MaxDownloadBytes)
                    {
                        return Result<string>.Fail(
                            $"Download refused: {total / (1024 * 1024)} MB exceeds " +
                            $"the {MaxDownloadBytes / (1024 * 1024)} MB safety cap.",
                            corrId);
                    }

                    progress?.Invoke(total > 0
                        ? $"Downloading ffmpeg essentials zip ({total / (1024 * 1024)} MB)…"
                        : "Downloading ffmpeg essentials zip…");

                    using (var src = await response.Content
                        .ReadAsStreamAsync(ct).ConfigureAwait(false))
                    using (var dst = File.Create(tempZip))
                    {
                        var buffer = new byte[81920];
                        long copied = 0;
                        long lastPct = -1;
                        int read;
                        while ((read = await src.ReadAsync(buffer, ct)
                            .ConfigureAwait(false)) > 0)
                        {
                            await dst.WriteAsync(buffer.AsMemory(0, read), ct)
                                .ConfigureAwait(false);
                            copied += read;
                            if (copied > MaxDownloadBytes)
                            {
                                return Result<string>.Fail(
                                    "Download aborted: exceeded the safety cap.",
                                    corrId);
                            }
                            if (total > 0)
                            {
                                long pct = copied * 100 / total;
                                if (pct != lastPct)
                                {
                                    lastPct = pct;
                                    // Numeric percent drives the progress bar
                                    // in Settings; the text line updates every
                                    // 5% so it does not churn.
                                    percent?.Invoke(pct);
                                    if (pct % 5 == 0)
                                    {
                                        progress?.Invoke(
                                            $"Downloading ffmpeg essentials zip… {pct}%");
                                    }
                                }
                            }
                        }
                    }
                }

                percent?.Invoke(100);
                progress?.Invoke("Download complete — verifying the zip…");

                // ── 3. Verify it really is a zip before trusting it ──────────
                using (var probe = File.OpenRead(tempZip))
                {
                    var magic = new byte[4];
                    int got = await probe.ReadAsync(magic.AsMemory(0, 4), ct)
                        .ConfigureAwait(false);
                    if (got < 4 || magic[0] != (byte)'P' || magic[1] != (byte)'K')
                    {
                        return Result<string>.Fail(
                            "Downloaded file is not a valid zip — aborted before " +
                            "extracting anything.", corrId);
                    }
                }

                // ── 4. Extract with a Zip-Slip guard ─────────────────────────
                progress?.Invoke("Extracting the zip…");
                Directory.CreateDirectory(tempExtract);
                string extractRoot = Path.GetFullPath(tempExtract)
                    + Path.DirectorySeparatorChar;

                using (var archive = ZipFile.OpenRead(tempZip))
                {
                    foreach (var entry in archive.Entries)
                    {
                        // Directory entries have an empty Name.
                        if (string.IsNullOrEmpty(entry.Name)) continue;

                        string destPath = Path.GetFullPath(
                            Path.Combine(tempExtract, entry.FullName));
                        if (!destPath.StartsWith(extractRoot,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            return Result<string>.Fail(
                                "Zip rejected: an entry tries to write outside " +
                                "the extract folder.", corrId);
                        }

                        Directory.CreateDirectory(
                            Path.GetDirectoryName(destPath)!);
                        entry.ExtractToFile(destPath, overwrite: true);
                    }
                }

                // ── 5. Locate the binaries inside the versioned root folder ──
                string? ffmpeg = FindFile(tempExtract, "ffmpeg.exe");
                string? ffprobe = FindFile(tempExtract, "ffprobe.exe");
                if (ffmpeg == null || ffprobe == null)
                {
                    return Result<string>.Fail(
                        "The zip did not contain ffmpeg.exe and ffprobe.exe " +
                        "where expected. Use COPY LINK for a manual download.",
                        corrId);
                }

                // ── 6. Install into the folder Find() checks first ───────────
                progress?.Invoke("Installing ffmpeg into the tools folder…");
                Directory.CreateDirectory(FfmpegMp4Composer.ToolsFolder);
                File.Copy(ffmpeg,
                    Path.Combine(FfmpegMp4Composer.ToolsFolder, "ffmpeg.exe"),
                    overwrite: true);
                File.Copy(ffprobe,
                    Path.Combine(FfmpegMp4Composer.ToolsFolder, "ffprobe.exe"),
                    overwrite: true);

                FfmpegMp4Composer.InvalidateCache();

                AppLogService.Info(
                    $"ffmpeg one-click install completed: {FfmpegMp4Composer.ToolsFolder}",
                    new LogContext
                    {
                        Module = "Demo",
                        Class = nameof(FfmpegInstaller),
                        Operation = "InstallFfmpeg",
                        CorrelationId = corrId,
                    });

                progress?.Invoke(
                    "ffmpeg installed — RECHECK now shows it as ready.");
                return Result<string>.Ok(FfmpegMp4Composer.ToolsFolder, corrId);
            }
            catch (OperationCanceledException)
            {
                return Result<string>.Fail("ffmpeg download cancelled.", corrId);
            }
            catch (Exception ex)
            {
                var errId = AppLogService.Error(
                    "ffmpeg one-click install failed", ex,
                    new LogContext
                    {
                        Module = "Demo",
                        Class = nameof(FfmpegInstaller),
                        Operation = "InstallFfmpeg",
                        CorrelationId = corrId,
                    });
                return Result<string>.Fail(ex.Message, corrId, ex, errId);
            }
            finally
            {
                TryDeleteFile(tempZip);
                TryDeleteDir(tempExtract);
            }
        }

        // ── helpers (never throw) ────────────────────────────────────────────

        private static string? FindFile(string root, string name)
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(
                    root, name, SearchOption.AllDirectories))
                {
                    return path;
                }
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    "[FfmpegInstaller] FindFile failed: " + ex.Message);
            }
            return null;
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { AppLogService.Debug("[FfmpegInstaller] Temp zip cleanup failed: " + ex.Message); }
        }

        private static void TryDeleteDir(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
            catch (Exception ex) { AppLogService.Debug("[FfmpegInstaller] Temp extract cleanup failed: " + ex.Message); }
        }
    }
}
