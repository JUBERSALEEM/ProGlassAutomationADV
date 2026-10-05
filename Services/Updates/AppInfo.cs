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

        /// <summary>
        /// Fallback only — the real value comes from the assembly identity set
        /// by &lt;Version&gt; in the .csproj. Exposed so the licensing wrapper
        /// does not carry its own third copy of the number.
        /// <para>
        /// THIS IS A FLOOR, NOT A MIRROR. Do not "keep it in step" with the
        /// csproj by hand: that is a fourth copy of a number that will drift,
        /// and it drifted (this read 2.0.4 while the csproj said 2.0.11). It is
        /// used only when <see cref="ResolveVersion"/> cannot read the entry
        /// assembly, which means a build whose identity is unreadable reports
        /// an OLDER version than it really is. The consequence is noisy, not
        /// dangerous: <c>UpdateService.IsNewer</c> is a strict greater-than
        /// compare, so a too-low fallback can offer an update the machine
        /// already has — it can never offer a downgrade, and it cannot hide a
        /// newer release. The installed-build path is unaffected either way:
        /// <c>VelopackUpdateService</c> compares against the version Velopack
        /// read from the installed package, not against this constant.
        /// </para>
        /// <para>
        /// Durable fix (Phase 20 of docs/UPGRADE_PHASES_2026-10-05.md): inject
        /// the csproj value at build time (an <c>AssemblyMetadata</c> attribute
        /// or a generated constant) so there is exactly one copy to bump.
        /// Until then, a value below the current release is CORRECT and a
        /// value above it would be a bug — it would suppress real updates.
        /// </para>
        /// </summary>
        public const string FallbackVersion = "2.0.4";

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

        /// <summary>
        /// Full build identity for logs and diagnostics: version + the UTC
        /// timestamp of the built assembly, e.g.
        /// <c>2.0.4 (build 2026.09.20 14:03:11 UTC)</c>.
        /// <para>
        /// RC-014 (2026-09-20): &lt;Version&gt; sat at 2.0.1 for nine days
        /// across several behaviour-changing commits. Because
        /// <see cref="UpdateService.IsNewer"/> is a strict greater-than
        /// compare, OTA could therefore never offer those builds — and a log
        /// excerpt could never tell anyone WHICH build produced it. Every
        /// Optimization log line now stamps this, so "is the user running the
        /// fixed build?" is answerable from the log alone.
        /// </para>
        /// </summary>
        public static string BuildStamp =>
            Version + " (build " + BuildLabel + " " + BuildTimeUtc + " UTC)";

        /// <summary>
        /// Exact UTC build time of the entry assembly (falls back to now).
        /// <see cref="BuildLabel"/> is deliberately date-only for the About
        /// page; two builds on the same day are indistinguishable there.
        /// </summary>
        public static string BuildTimeUtc { get; } = ResolveBuildTimeUtc();

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

        private static string ResolveBuildTimeUtc()
        {
            try
            {
                var path = Assembly.GetEntryAssembly()?.Location;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return File.GetLastWriteTimeUtc(path).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[AppInfo] ResolveBuildTimeUtc: fall through: " + ex.Message); }

            return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
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
