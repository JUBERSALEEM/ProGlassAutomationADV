using System;
using System.IO;
using System.Reflection;

namespace ProGlassAutomation.Services.Updates
{
    /// <summary>
    /// Single source of truth for application identity. About, Updates
    /// and diagnostics all read from here — no duplicated version strings.
    ///
    /// v3 OTA (spec §5): the authoritative version now comes from the
    /// ASSEMBLY identity, which is set ONCE in the .csproj (&lt;Version&gt;).
    /// The const below is only a fallback for unusual host scenarios.
    /// </summary>
    public static class AppInfo
    {
        public const string Name = "ProGlass Automation";
        public const string Developer = "Juber Saleem";
        public const string Copyright = "© 2026 Juber Saleem. Made in India. All rights reserved.";

        /// <summary>GitHub repository backing the OTA update check.</summary>
        public const string GitHubOwner = "JUBERSALEEM";
        public const string GitHubRepo = "ProGlassAutomationADV";

        /// <summary>Fallback only — real value comes from the assembly.</summary>
        private const string FallbackVersion = "2.0.0";

        /// <summary>
        /// Current application version (MAJOR.MINOR.PATCH), resolved once
        /// from the entry assembly identity set by the .csproj.
        /// </summary>
        public static string Version { get; } = ResolveVersion();

        /// <summary>
        /// Build label for About/Settings (e.g. "2026.08.14") derived
        /// from the build artifact timestamp. Never hard-coded.
        /// </summary>
        public static string BuildLabel { get; } = ResolveBuildLabel();

        public static string AssemblyVersion =>
            Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
            ?? FallbackVersion;

        private static string ResolveVersion()
        {
            try
            {
                var v = Assembly.GetEntryAssembly()?.GetName().Version;

                // Ignore the default 1.0.0 identity (unversioned dev
                // build) so we never report an OLDER version than the
                // product actually is.
                if (v != null &&
                    (v.Major > 1 || v.Minor > 0 || v.Build > 0))
                {
                    return v.ToString(3);
                }
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[AppInfo] ResolveVersion: fall through to the fallback: " + ex.Message); }

            return FallbackVersion;
        }

        private static string ResolveBuildLabel()
        {
            try
            {
                var path = Assembly.GetEntryAssembly()?.Location;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return File.GetLastWriteTime(path).ToString("yyyy.MM.dd");
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[AppInfo] ResolveBuildLabel: fall through: " + ex.Message); }

            return DateTime.Now.ToString("yyyy.MM.dd");
        }
    }

    /// <summary>Result of one OTA update check.</summary>
    public class UpdateCheckResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
        public bool UpdateAvailable { get; set; }
        public string LatestVersion { get; set; } = "";
        public string ReleaseName { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public string ReleasePageUrl { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string DownloadFileName { get; set; } = "";

        /// <summary>When the release was published on GitHub (UTC).</summary>
        public DateTime? PublishedAtUtc { get; set; }
    }
}
