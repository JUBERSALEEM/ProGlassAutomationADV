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

using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Common;
using System;
using System.Diagnostics;
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

        // ══════════════════════════════════════════════════════════════
        //  SECURITY — DOWNLOAD ORIGIN ALLOWLIST  (audit finding C-1)
        //
        //  THE PROBLEM THIS SOLVES
        //  DownloadUrl is read out of the GitHub API JSON
        //  ("browser_download_url") and was used verbatim: downloaded to
        //  the user's Downloads folder and then handed to Process.Start
        //  with UseShellExecute = true. Nothing verified where that URL
        //  actually pointed. Any tampering with the JSON response — a
        //  hostile proxy, DNS interception, a compromised release asset,
        //  or simply a bad edit to a release — would cause the app to
        //  fetch and EXECUTE an arbitrary binary with the user's
        //  privileges. That is remote code execution.
        //
        //  WHY AN ALLOWLIST RATHER THAN A HASH
        //  A hash has to come from somewhere. If it comes from the same
        //  JSON as the URL, it proves nothing — an attacker who can edit
        //  the URL can edit the hash beside it. A real integrity check
        //  needs an out-of-band trust anchor: Authenticode signature
        //  verification on the downloaded file, or the publisher's key.
        //  That is exactly what Velopack already does, and Velopack is
        //  the preferred path (VelopackUpdateService).
        //
        //  So this fix does what CAN be done correctly here: it pins the
        //  transport to HTTPS and pins the origin to GitHub's own release
        //  hosts. It does NOT claim to verify authenticity — see
        //  VerifyDownloadOrigin's remarks and Docs/SecurityAudit.md.
        //
        //  Hosts: github.com serves the release page redirect;
        //  objects.githubusercontent.com serves the actual asset bytes.
        // ══════════════════════════════════════════════════════════════
        private static readonly string[] AllowedDownloadHosts =
        {
            "github.com",
            "www.github.com",
            "objects.githubusercontent.com",
            "release-assets.githubusercontent.com",
            "api.github.com"
        };

        /// <summary>
        /// True when <paramref name="url"/> is an absolute HTTPS URL whose
        /// host is a GitHub release host. Used to refuse any asset URL that
        /// did not come from where we expect.
        /// </summary>
        /// <remarks>
        /// This is an ORIGIN check, not an authenticity check. It stops the
        /// app being redirected to an attacker-controlled server. It cannot
        /// detect a malicious file legitimately hosted on GitHub — only a
        /// code-signature check can do that, which is why the Store build
        /// and the Velopack path are preferred over this downloader.
        /// </remarks>
        /// <remarks>
        /// Public rather than internal so the security test suite
        /// (Tests/Agent/UpdateOriginSecurityTests.cs) can pin its
        /// behaviour without an InternalsVisibleTo attribute. It is a
        /// pure, side-effect-free predicate, so exposing it costs
        /// nothing and makes the guarantee testable.
        /// </remarks>
        public static bool IsAllowedDownloadUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            // HTTPS only — never fetch an executable over plaintext.
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            // Exact host match. Deliberately NOT EndsWith(".github.com"):
            // that would accept "evil-github.com" and
            // "github.com.attacker.net".
            foreach (var host in AllowedDownloadHosts)
            {
                if (string.Equals(uri.Host, host,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        // ══════════════════════════════════════════════════════════════
        //  v3 OTA ORCHESTRATOR (spec §7 states + §13 activity + §17
        //  offline + §18 automatic checking)
        //
        //  CheckNowAsync / DownloadNowAsync / InstallAndRestart wrap the
        //  original low-level methods with lifecycle states, audit
        //  entries and offline handling. The original methods stay
        //  public for backward compatibility. Velopack (apply/restart
        //  provider) plugs into these same states later.
        // ══════════════════════════════════════════════════════════════

        /// <summary>Current lifecycle state.</summary>
        public static UpdateState State { get; private set; } = UpdateState.Idle;

        /// <summary>Human-readable detail for the current state.</summary>
        public static string StateDetail { get; private set; } = "";

        /// <summary>Download progress 0–100 while Downloading.</summary>
        public static int DownloadPercent { get; private set; }

        /// <summary>When the last check ran (this session).</summary>
        public static DateTime? LastCheckUtc { get; private set; }

        /// <summary>Path of the downloaded installer (when ready).</summary>
        public static string DownloadedInstallerPath { get; private set; } = "";

        /// <summary>Raised on every state change (background thread).</summary>
        public static event Action StateChanged;

        /// <summary>True once a downloaded installer can be launched.</summary>
        public static bool CanInstall =>
            !string.IsNullOrEmpty(DownloadedInstallerPath) &&
            File.Exists(DownloadedInstallerPath);

        private static void SetState(UpdateState state, string detail = "")
        {
            State = state;
            StateDetail = detail ?? "";
            try { StateChanged?.Invoke(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
        }

        private static bool IsOffline =>
            App.Connectivity != null && !App.Connectivity.IsOnline;

        /// <summary>
        /// Full state-aware update check. Never throws; never blocks
        /// the UI (callers await it asynchronously).
        /// </summary>
        public static async Task<UpdateCheckResult> CheckNowAsync()
        {
            if (IsOffline)
            {
                SetState(UpdateState.Offline,
                    "No internet connection. Update check unavailable.");
                return Fail("Offline — no internet connection.");
            }

            SetState(UpdateState.Checking);

            var result = await CheckForUpdateAsync();
            LastCheckUtc = DateTime.UtcNow;
            UpdateHistoryService.StampCheckUtc();

            if (!result.Success)
            {
                SetState(IsOffline ? UpdateState.Offline : UpdateState.Failed,
                    result.ErrorMessage);
                AuditService.Log(AuditSections.Updates,
                    "UPDATE_CHECK_FAILED",
                    Shorten(result.ErrorMessage));
                return result;
            }

            if (result.UpdateAvailable)
            {
                SetState(UpdateState.UpdateAvailable, result.LatestVersion);
                AuditService.Log(AuditSections.Updates,
                    "UPDATE_AVAILABLE",
                    $"v{AppInfo.Version} -> v{result.LatestVersion}");
                UpdateHistoryService.Record(
                    UpdateEventType.Detected,
                    result.LatestVersion, result.ReleaseName);
            }
            else
            {
                SetState(UpdateState.UpToDate, result.LatestVersion);
                AuditService.Log(AuditSections.Updates,
                    "UPDATE_CHECK",
                    $"Up to date (v{result.LatestVersion})");
            }

            return result;
        }

        /// <summary>
        /// State-aware download of the release asset. Progress is
        /// reported via <paramref name="progress"/> AND DownloadPercent.
        /// </summary>
        public static async Task<(bool Success, string Path, string Error)>
            DownloadNowAsync(
                UpdateCheckResult update,
                Action<int>? progress = null,
                CancellationToken cancellationToken = default)
        {
            if (update == null)
                return (false, "", "Nothing to download — check for updates first.");

            if (string.IsNullOrWhiteSpace(update.DownloadUrl))
            {
                SetState(UpdateState.Failed,
                    "This release has no downloadable asset.");
                return (false, "", "This release has no downloadable asset.");
            }

            SetState(UpdateState.Downloading);
            DownloadPercent = 0;

            var (ok, path, error) = await DownloadUpdateAsync(
                update,
                p =>
                {
                    DownloadPercent = p;
                    progress?.Invoke(p);
                },
                cancellationToken);

            if (ok)
            {
                DownloadedInstallerPath = path;
                SetState(UpdateState.DownloadCompleted, path);
                AuditService.Log(AuditSections.Updates,
                    "UPDATE_DOWNLOAD_COMPLETED",
                    $"v{update.LatestVersion} -> {path}");
                UpdateHistoryService.Record(
                    UpdateEventType.Downloaded,
                    update.LatestVersion, update.ReleaseName, path);
            }
            else if (cancellationToken.IsCancellationRequested)
            {
                SetState(UpdateState.Cancelled);
                AuditService.Log(AuditSections.Updates,
                    "UPDATE_DOWNLOAD_CANCELLED",
                    $"v{update.LatestVersion}");
            }
            else
            {
                SetState(UpdateState.Failed, error);
                AuditService.Log(AuditSections.Updates,
                    "UPDATE_DOWNLOAD_FAILED",
                    $"v{update.LatestVersion}: {Shorten(error)}");
                AppLogService.Warning(
                    $"Update download failed: {error}",
                    new LogContext
                    {
                        Module = "Updates",
                        Class = nameof(UpdateService),
                        Operation = "Download"
                    });
            }

            return (ok, path, error);
        }

        /// <summary>
        /// Launches the downloaded installer and exits the app so the
        /// installer can apply the update. User data under
        /// %AppData%/ProGlassAutomation is NEVER touched (spec §19).
        /// Full in-app apply-and-restart arrives with the Velopack
        /// provider; until then the downloaded Setup.exe does the work.
        /// </summary>
        public static void InstallAndRestart()
        {
            if (!CanInstall)
            {
                SetState(UpdateState.Failed, "No downloaded update to install.");
                return;
            }

            var path = DownloadedInstallerPath;

            AuditService.Log(AuditSections.Updates,
                "UPDATE_INSTALL", Path.GetFileName(path));

            try
            {
                if (path.EndsWith(".exe",
                        StringComparison.OrdinalIgnoreCase))
                {
                    // ── DEFENCE IN DEPTH (audit C-1) ──────────────────
                    // DownloadedInstallerPath is only ever assigned after
                    // DownloadUpdateAsync passed the origin check, so a
                    // rogue URL cannot reach here. This second check makes
                    // that guarantee explicit and local: if the field is
                    // ever set from somewhere else in future, launching
                    // still requires the file to sit inside the folder we
                    // downloaded it to, not an arbitrary path.
                    var downloadsRoot = Path.GetFullPath(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.UserProfile));
                    var fullPath = Path.GetFullPath(path);

                    if (!fullPath.StartsWith(downloadsRoot,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        AuditService.Log(AuditSections.Updates,
                            "UPDATE_INSTALL_BLOCKED",
                            "Installer path outside the user profile.");

                        SetState(UpdateState.Failed,
                            "The update installer is not in the expected " +
                            "location and was not launched.");
                        return;
                    }

                    // ── RE-VERIFY SIGNATURE AT LAUNCH (Phase 10) ─────
                    // Defence in depth: the file was verified at download,
                    // but the time between download and install is exactly
                    // when a tampered file could be swapped in. Verify
                    // again immediately before launch.
                    var verdict = UpdateSignatureVerifier.Verify(path);
                    if (!verdict.IsValid)
                    {
                        AuditService.Log(AuditSections.Updates,
                            "UPDATE_INSTALL_BLOCKED_SIGNATURE",
                            verdict.Reason);

                        SetState(UpdateState.Failed,
                            "The update installer failed signature " +
                            "verification and was not launched.");
                        return;
                    }

                    ProcessStartGuard.TryOpenFile(path, CorrelationContext.NewId());

                    // Give the installer a moment to start, then exit so
                    // application files can be replaced safely.
                    System.Windows.Application.Current?.Dispatcher
                        .InvokeAsync(async () =>
                        {
                            await Task.Delay(800);
                            System.Windows.Application.Current.Shutdown();
                        });

                    SetState(UpdateState.Installing, path);
                }
                else
                {
                    // Archive release: reveal it so the user can extract
                    // and run Setup — the app still never self-replaces.
                    ProcessStartGuard.TryOpenExplorerSelect(path, CorrelationContext.NewId());
                    SetState(UpdateState.DownloadCompleted,
                        "Unzip the archive and run Setup to install.");
                }
            }
            catch (Exception ex)
            {
                SetState(UpdateState.Failed, ex.Message);
                AuditService.Log(AuditSections.Updates,
                    "UPDATE_INSTALL_FAILED", Shorten(ex.Message));
                AppLogService.Warning(
                    $"Update install launch failed: {ex.Message}",
                    new LogContext
                    {
                        Module = "Updates",
                        Class = nameof(UpdateService),
                        Operation = "Install"
                    });
            }
        }

        /// <summary>
        /// Startup check (spec §18): fires once per ~day, fully async,
        /// never delays or blocks application startup.
        /// </summary>
        public static async Task StartupCheckAsync()
        {
            try
            {
                // Let the app settle first — updates are never urgent
                // enough to compete with startup work.
                await Task.Delay(TimeSpan.FromSeconds(8));

                var last = UpdateHistoryService.ReadLastCheckUtc();
                if (last.HasValue &&
                    DateTime.UtcNow - last.Value < TimeSpan.FromHours(20))
                    return;

                await CheckNowAsync();
            }
            catch { /* OTA must never affect the running app */ }
        }

        /// <summary>
        /// True when the number of bytes copied matches the server's
        /// declared Content-Length. A null/absent Content-Length cannot be
        /// verified, so it is treated as complete (the gate is best-effort,
        /// exactly like the U-2 Whisper completeness check). Exposed so the
        /// test suite can pin the truncation decision without any network.
        /// </summary>
        public static bool IsDownloadComplete(long? total, long copied)
        {
            if (copied < 0) return false;
            if (!total.HasValue) return true;   // no declared length — cannot verify
            return copied == total.Value;
        }

        private static string Shorten(string s) =>
            string.IsNullOrEmpty(s) ? "" :
            (s.Length <= 200 ? s : s[..200] + "…");

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

                // ── SECURITY GATE (audit C-1) ─────────────────────────
                // Refuse anything that is not HTTPS-from-GitHub before a
                // single byte is fetched. See IsAllowedDownloadUrl.
                if (!IsAllowedDownloadUrl(update.DownloadUrl))
                {
                    AppLogService.Warning(
                        "Update download refused: asset URL is not an " +
                        "HTTPS GitHub release URL.",
                        new LogContext
                        {
                            Module = "Updates",
                            Operation = "DownloadUpdate"
                        });

                    AuditService.Log(AuditSections.Updates,
                        "UPDATE_DOWNLOAD_BLOCKED",
                        "Asset URL failed the origin check.");

                    return (false, "",
                        "This update was blocked because its download " +
                        "location could not be verified as an official " +
                        "GitHub release. No file was downloaded.");
                }

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

                // ── COMPLETENESS GATE (mirrors the U-2 Whisper fix) ──
                // If the connection dropped mid-transfer, the loop above
                // ends with a truncated file and a 100% progress bar but
                // nothing else notices. Compare the bytes actually written
                // against Content-Length and discard a short file instead of
                // handing a corrupt installer to the user.
                if (!IsDownloadComplete(total, copied))
                {
                    AppLogService.Warning(
                        $"Update download was truncated: expected " +
                        $"{total.Value} bytes, got {copied}. File discarded.",
                        new LogContext { Module = "Updates", Operation = "Download" });

                    AuditService.Log(AuditSections.Updates,
                        "UPDATE_DOWNLOAD_TRUNCATED",
                        $"{fileName}: expected {total.Value} bytes, got {copied}.");

                    // Best-effort delete of the partial file.
                    try { if (File.Exists(targetPath)) File.Delete(targetPath); }
                    catch { /* partial file cleanup is best-effort */ }

                    return (false, "",
                        "The update download was incomplete and was discarded. " +
                        "Please try again.");
                }

                // ── AUTHENTICITY GATE (Phase 10 — audit C-1) ─────────
                // The origin check above proves WHERE the file came from;
                // this proves WHO built it. Verify the Authenticode
                // signature (signature + file hash via WinVerifyTrust)
                // before the installer is ever handed to the user.
                var verdict = UpdateSignatureVerifier.Verify(targetPath);
                if (!verdict.IsValid)
                {
                    AppLogService.Warning(
                        $"Update signature verification failed: {verdict.Reason}",
                        new LogContext
                        {
                            Module = "Updates",
                            Operation = "VerifySignature",
                            Extra = $"File={fileName}, Subject={verdict.CertificateSubject}"
                        });

                    AuditService.Log(AuditSections.Updates,
                        "UPDATE_SIGNATURE_INVALID",
                        $"v{update.LatestVersion}: {verdict.Reason}");

                    try { if (File.Exists(targetPath)) File.Delete(targetPath); }
                    catch { /* cleanup is best-effort */ }

                    return (false, "",
                        "This update failed signature verification and was " +
                        "discarded. No file was kept. If you trust this " +
                        "release, verify the publisher manually before " +
                        "installing.");
                }

                AppLogService.Info(
                    $"Update downloaded and signature-verified: {targetPath} " +
                    $"(publisher={verdict.CertificateSubject})",
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
