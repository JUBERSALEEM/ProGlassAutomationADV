// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Updates/VelopackUpdateService.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — OTA install/apply via Velopack (v2 OTA integration)
//
// Active ONLY when the app was installed through a Velopack package
// (VelopackApp.IsInstalled). In dev/portable mode the classic
// GitHub-Release check + manual installer flow remains in charge, so
// nothing regresses for non-installed builds.
//
// Flow: Check -> Download (progress) -> ApplyUpdatesAndRestart.
// GitHub Releases is the distribution source; no token required for
// public repos. Secrets never touch the client.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace ProGlassAutomation.Services.Updates
{
    /// <summary>Velopack-backed OTA path (installed builds only).</summary>
    public static class VelopackUpdateService
    {
        /// <summary>True when running from a Velopack installation.</summary>
        public static bool IsInstalled => VelopackApp.IsInstalled;

        private static UpdateManager? _manager;
        private static UpdateInfo? _pending;

        /// <summary>Pending update downloaded this session (if any).</summary>
        public static bool HasPendingUpdate => _pending != null;

        private static UpdateManager Manager =>
            _manager ??= new UpdateManager(new GithubSource(
                $"https://github.com/{AppInfo.GitHubOwner}/{AppInfo.GitHubRepo}",
                accessToken: null,
                prerelease: false));

        /// <summary>
        /// Checks GitHub Releases through Velopack. Returns the latest
        /// version string when an update is available, else null.
        /// Never throws.
        /// </summary>
        public static async Task<string?> CheckAsync()
        {
            try
            {
                if (!IsInstalled) return null;
                var info = await Manager.CheckForUpdatesAsync();
                _pending = info;
                return info?.TargetFullVersion;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Velopack] check failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Downloads the pending update with 0-100 progress.
        /// Returns true on success. Never throws.
        /// </summary>
        public static async Task<bool> DownloadAsync(Action<int>? progress = null)
        {
            try
            {
                if (!IsInstalled || _pending == null) return false;
                await Manager.DownloadUpdatesAsync(_pending, progress);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Velopack] download failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Applies the downloaded update and restarts the app.</summary>
        public static void ApplyAndRestart()
        {
            try
            {
                if (!IsInstalled || _pending == null) return;
                Manager.ApplyUpdatesAndRestart(_pending);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Velopack] apply failed: {ex.Message}");
            }
        }
    }
}
