// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Updates/UpdateService.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — OTA update check (GitHub Releases)
//
// Checks the latest GitHub release, compares versions, and can download
// the release asset to the user's Downloads folder with progress.
//
// SAFETY: the service NEVER replaces or runs application files. The
// user decides what to do with the downloaded installer (spec §34).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Updates
{
    /// <summary>OTA update check + download (no auto-install).</summary>
    public static class UpdateService
    {
        private static readonly HttpClient _http = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            // GitHub API rejects requests without a User-Agent.
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                $"{AppInfo.GitHubRepo}-desktop/{AppInfo.Version}");
            client.DefaultRequestHeaders.Accept.ParseAdd(
                "application/vnd.github+json");
            return client;
        }

        /// <summary>Checks GitHub for a newer release. Never throws.</summary>
        public static async Task<UpdateCheckResult> CheckForUpdateAsync()
        {
            var url =
                $"https://api.github.com/repos/{AppInfo.GitHubOwner}/" +
                $"{AppInfo.GitHubRepo}/releases/latest";

            try
            {
                using var response = await _http.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return Fail(response.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? "No releases published yet."
                        : $"GitHub returned {(int)response.StatusCode}.");
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tag = (root.TryGetProperty("tag_name", out var t)
                    ? t.GetString() : "") ?? "";

                string latest = NormalizeVersion(tag);
                bool available = IsNewer(latest, AppInfo.Version);

                string downloadUrl = "";
                string fileName = "";

                if (root.TryGetProperty("assets", out var assets) &&
                    assets.ValueKind == JsonValueKind.Array &&
                    assets.GetArrayLength() > 0)
                {
                    var asset = assets.EnumerateArray().First();
                    downloadUrl = asset.TryGetProperty("browser_download_url", out var d)
                        ? d.GetString() ?? "" : "";
                    fileName = asset.TryGetProperty("name", out var n)
                        ? n.GetString() ?? "" : "";
                }

                // Parse the publish date (ISO-8601 from GitHub).
                DateTime? published = null;
                if (root.TryGetProperty("published_at", out var pa) &&
                    DateTime.TryParse(
                        pa.GetString(), null,
                        System.Globalization.DateTimeStyles.RoundtripKind,
                        out var parsedDate))
                {
                    published = parsedDate;
                }

                return new UpdateCheckResult
                {
                    Success = true,
                    UpdateAvailable = available,
                    LatestVersion = latest,
                    ReleaseName = root.TryGetProperty("name", out var nm)
                        ? nm.GetString() ?? "" : "",
                    ReleaseNotes = root.TryGetProperty("body", out var b)
                        ? b.GetString() ?? "" : "",
                    ReleasePageUrl = root.TryGetProperty("html_url", out var h)
                        ? h.GetString() ?? "" : "",
                    DownloadUrl = downloadUrl,
                    DownloadFileName = fileName,
                    PublishedAtUtc = published
                };
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Update check failed: {ex.Message}",
                    new LogContext
                    {
                        Module = "Updates",
                        Class = nameof(UpdateService),
                        Operation = "CheckForUpdate"
                    });

                return Fail(ex.Message);
            }
        }

        /// <summary>
        /// Downloads the release asset to the Downloads folder.
        /// Reports 0–100 progress. Never executes the file.
        /// </summary>
        public static async Task<(bool Success, string Path, string Error)>
            DownloadUpdateAsync(
                UpdateCheckResult update,
                Action<int>? progress = null,
                CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(update.DownloadUrl))
                    return (false, "", "This release has no downloadable asset.");

                var downloads = Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile);
                var targetDir = Path.Combine(downloads, "Downloads");
                if (!Directory.Exists(targetDir))
                    targetDir = downloads;

                var fileName = string.IsNullOrWhiteSpace(update.DownloadFileName)
                    ? $"ProGlassAutomation-{update.LatestVersion}-release.zip"
                    : update.DownloadFileName;

                var targetPath = Path.Combine(targetDir, fileName);

                using var response = await _http.GetAsync(
                    update.DownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                long? total = response.Content.Headers.ContentLength;

                await using var source =
                    await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var file = new FileStream(
                    targetPath, FileMode.Create, FileAccess.Write,
                    FileShare.None, 81920, useAsync: true);

                var buffer = new byte[81920];
                long copied = 0;
                int read;

                while ((read = await source.ReadAsync(
                    buffer, cancellationToken)) > 0)
                {
                    await file.WriteAsync(
                        buffer.AsMemory(0, read), cancellationToken);
                    copied += read;

                    if (total is > 0)
                        progress?.Invoke((int)(copied * 100 / total.Value));
                }

                progress?.Invoke(100);

                AppLogService.Info(
                    $"Update downloaded to {targetPath}",
                    new LogContext { Module = "Updates", Operation = "Download" });

                return (true, targetPath, "");
            }
            catch (Exception ex)
            {
                return (false, "", ex.Message);
            }
        }

        // ── Version helpers ─────────────────────────────────────────────

        private static string NormalizeVersion(string tag)
        {
            var v = (tag ?? "").Trim().TrimStart('v', 'V');
            return Version.TryParse(v, out _) ? v : v + ".0";
        }

        private static bool IsNewer(string candidate, string current)
        {
            if (Version.TryParse(candidate, out var a) &&
                Version.TryParse(current, out var b))
                return a > b;

            return !string.Equals(candidate, current,
                StringComparison.OrdinalIgnoreCase);
        }

        private static UpdateCheckResult Fail(string message) => new()
        {
            Success = false,
            ErrorMessage = message
        };
    }
}
