using System.Reflection;

namespace ProGlassAutomation.Services.Updates
{
    /// <summary>
    /// Single source of truth for application identity. About, Updates
    /// and diagnostics all read from here — no duplicated version strings.
    /// </summary>
    public static class AppInfo
    {
        public const string Name = "ProGlass Automation";
        public const string Version = "2.0.0";
        public const string Developer = "Juber Saleem";
        public const string Copyright = "© 2026 Juber Saleem. All rights reserved.";

        /// <summary>GitHub repository backing the OTA update check.</summary>
        public const string GitHubOwner = "JUBERSALEEM";
        public const string GitHubRepo = "ProGlassAutomationADV";

        public static string AssemblyVersion =>
            Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
            ?? Version;
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
    }
}
