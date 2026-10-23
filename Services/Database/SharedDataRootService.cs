using ProGlassAutomation.Services.AI.AIConfig;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Database
{
    public sealed class SharedDataRootProbeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string NormalizedPath { get; init; } = "";
        public bool LooksLikeNetworkPath { get; init; }
        public int DatabaseFileCount { get; init; }
    }

    /// <summary>
    /// Validates and saves the shared data-folder location selected by the
    /// operator. Windows sharing and permissions are configured with Windows
    /// tools; ProGlass does not publish shares, change security settings, or
    /// discover other computers.
    /// </summary>
    public static class SharedDataRootService
    {
        private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(4);

        /// <summary>
        /// Tests the selected, existing folder without creating it or changing
        /// its contents. A temporary file verifies read/write/delete access;
        /// existing ProGlass database files are counted for operator review.
        /// </summary>
        public static SharedDataRootProbeResult Probe(string? folder)
        {
            if (!AppDataRoot.TryNormalizeSharedRoot(folder, out var normalized))
            {
                return new SharedDataRootProbeResult
                {
                    Ok = false,
                    Message = "Enter an existing data folder other than the local ProGlass AppData folder. Use the same UNC path (for example \\\\OFFICE-PC\\ProGlassData) or mapped network drive on every workstation.",
                    NormalizedPath = folder ?? ""
                };
            }

            bool network = LooksNetworkBased(normalized);
            var probe = Task.Run(() => ProbeFolder(normalized, network));
            probe.ContinueWith(
                task => { _ = task.Exception; },
                TaskContinuationOptions.OnlyOnFaulted);

            try
            {
                if (!probe.Wait(ProbeTimeout))
                {
                    return new SharedDataRootProbeResult
                    {
                        Ok = false,
                        NormalizedPath = normalized,
                        LooksLikeNetworkPath = network,
                        Message = "✗ The folder did not respond within 4 seconds. Check the host, Windows share access, and network connection. The active data root was not changed."
                    };
                }

                return probe.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                return new SharedDataRootProbeResult
                {
                    Ok = false,
                    NormalizedPath = normalized,
                    LooksLikeNetworkPath = network,
                    Message = "✗ Folder test failed: " + AppDataRoot.DescribeShareFailure(ex.GetBaseException())
                };
            }
        }

        private static SharedDataRootProbeResult ProbeFolder(string normalized, bool network)
        {
            var attributes = File.GetAttributes(normalized);
            if ((attributes & FileAttributes.Directory) == 0)
                throw new IOException("The selected path is not a folder.");

            string testFile = Path.Combine(normalized,
                ".proglass_folder_test_" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(testFile, "ProGlass folder access test.");
                _ = File.ReadAllText(testFile);
                File.Delete(testFile);
            }
            catch
            {
                try { if (File.Exists(testFile)) File.Delete(testFile); }
                catch { /* Preserve the original read/write/delete failure. */ }
                throw;
            }

            int databaseCount = Directory.GetFiles(
                normalized, "*.db", SearchOption.TopDirectoryOnly).Length;
            string dataHint = databaseCount == 0
                ? " No top-level ProGlass database files were found; confirm this is the intended company folder before applying."
                : $" Found {databaseCount} top-level ProGlass database file(s). No data will be copied or merged.";
            string message = network
                ? "✓ This network folder is reachable from this workstation and supports read/write/delete."
                : "✓ This folder is reachable, but it is a local path, not a UNC or mapped network location. Use the same UNC path or mapped drive on every workstation.";

            return new SharedDataRootProbeResult
            {
                Ok = true,
                NormalizedPath = normalized,
                LooksLikeNetworkPath = network,
                DatabaseFileCount = databaseCount,
                Message = message + dataHint
            };
        }

        /// <summary>
        /// A UNC path or a mapped Windows drive is a network location. A
        /// local drive path must not be saved as a shared company root because
        /// other workstations would otherwise open different datasets.
        /// </summary>
        public static bool LooksNetworkBased(string path) =>
            !string.IsNullOrWhiteSpace(path)
            && (path.Trim().StartsWith(@"\\", StringComparison.Ordinal)
                || !string.IsNullOrEmpty(AppDataRoot.GetMappedNetworkPath(path)));

        /// <summary>
        /// Persists only a validated network path. It never copies, merges,
        /// creates, or overwrites data. Root activation occurs after the
        /// operator confirms the tested path and restarts ProGlass.
        /// </summary>
        public static (bool Ok, string Message) ApplyToConfig(
            AppConfig config, bool enabled, string path)
        {
            config.DataConnectivity ??= new DataConnectivitySettings();
            string normalized = "";
            if (enabled)
            {
                if (!AppDataRoot.TryNormalizeSharedRoot(path, out normalized))
                {
                    return (false, "✗ Enter a valid shared folder path before applying.");
                }
                if (!LooksNetworkBased(normalized))
                {
                    return (false,
                        "✗ The selected path is local. Use the shared UNC path or mapped network drive that every workstation can access.");
                }
            }

            config.DataConnectivity.UseSharedDataRoot = enabled;
            config.DataConnectivity.SharedDataRootPath = enabled ? normalized : "";
            config.DataConnectivity.SharedRootOfflineFallbackNeedsReview = false;
            config.DataConnectivity.SharedRootLocalModeNoticePending = false;
            config.DataConnectivity.SharedRootReconnectNoticeShown = false;
            if (string.IsNullOrWhiteSpace(config.DataConnectivity.WorkstationName))
                config.DataConnectivity.WorkstationName = Environment.MachineName;

            return (true, enabled
                ? "✓ Shared folder settings saved. Restart ProGlass on each workstation and select the same network path. No data was copied or merged."
                : "✓ Workstation will use its local AppData database root after restart.");
        }
    }
}
