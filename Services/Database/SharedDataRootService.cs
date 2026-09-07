using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Logging;
using System;
using System.IO;
using System.Linq;

namespace ProGlassAutomation.Services.Database
{
    public sealed class SharedDataRootProbeResult
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public string NormalizedPath { get; init; } = "";
        public bool LooksLikeNetworkPath { get; init; }
    }

    /// <summary>
    /// Professional LAN/Wi-Fi data-root helper. This does not turn SQLite
    /// into a database server; it safely points all company workstations at
    /// the same shared folder and verifies read/write/delete access first.
    /// </summary>
    public static class SharedDataRootService
    {
        private static readonly string[] ExtraDataSubfolders =
            { "Inquiries", "ProformaInvoices", "DeliveryNotes" };

        public static SharedDataRootProbeResult Probe(string? folder)
        {
            if (!AppDataRoot.TryNormalizeSharedRoot(folder, out var normalized))
            {
                return new SharedDataRootProbeResult
                {
                    Ok = false,
                    Message = "Enter a shared folder different from the local ProGlass AppData folder. Recommended: \\SERVER\\ProGlassData or a mapped drive on the same LAN/Wi-Fi.",
                    NormalizedPath = folder ?? ""
                };
            }

            try
            {
                Directory.CreateDirectory(normalized);
                var testFile = Path.Combine(normalized,
                    ".proglass_network_write_test_" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(testFile,
                    "ProGlass shared database write test " + DateTime.UtcNow.ToString("o"));
                _ = File.ReadAllText(testFile);
                File.Delete(testFile);

                return new SharedDataRootProbeResult
                {
                    Ok = true,
                    NormalizedPath = normalized,
                    LooksLikeNetworkPath = LooksNetworkBased(normalized),
                    Message = LooksNetworkBased(normalized)
                        ? "✓ Shared data folder is reachable and writable on the network. Restart ProGlass on each workstation after enabling."
                        : "✓ Folder is writable. It is not a UNC path; confirm this mapped/local drive is shared to all workstations before using it company-wide."
                };
            }
            catch (Exception ex)
            {
                return new SharedDataRootProbeResult
                {
                    Ok = false,
                    NormalizedPath = normalized,
                    LooksLikeNetworkPath = LooksNetworkBased(normalized),
                    Message = "✗ Shared folder is not ready: " + ex.Message
                };
            }
        }

        public static bool LooksNetworkBased(string path) =>
            !string.IsNullOrWhiteSpace(path)
            && path.Trim().StartsWith(@"\\", StringComparison.Ordinal);

        public static (bool Ok, string Message) PrepareSharedFolder(
            string sharedFolder,
            bool copyLocalData)
        {
            var probe = Probe(sharedFolder);
            if (!probe.Ok) return (false, probe.Message);

            try
            {
                Directory.CreateDirectory(probe.NormalizedPath);
                int copied = 0;

                if (copyLocalData && IsFolderEmptyOfProGlassData(probe.NormalizedPath))
                    copied = CopyLocalDatasetTo(probe.NormalizedPath);

                return (true, copied > 0
                    ? $"✓ Shared folder prepared and {copied} local data item(s) copied. Save settings and restart all workstations."
                    : "✓ Shared folder prepared. Save settings and restart all workstations.");
            }
            catch (Exception ex)
            {
                AppLogService.Error("Shared data folder preparation failed", ex,
                    new LogContext { Module = "Database", Operation = "PrepareSharedFolder" });
                return (false, "✗ Could not prepare shared folder: " + ex.Message);
            }
        }

        public static void ApplyToConfig(AppConfig config, bool enabled, string path, bool copyLocalData)
        {
            config.DataConnectivity ??= new DataConnectivitySettings();
            config.DataConnectivity.UseSharedDataRoot = enabled;
            config.DataConnectivity.SharedDataRootPath = enabled && AppDataRoot.TryNormalizeSharedRoot(path, out var normalized)
                ? normalized
                : "";
            config.DataConnectivity.CopyLocalDataOnEnable = copyLocalData;
            if (string.IsNullOrWhiteSpace(config.DataConnectivity.WorkstationName))
                config.DataConnectivity.WorkstationName = Environment.MachineName;
        }

        private static bool IsFolderEmptyOfProGlassData(string folder)
        {
            if (!Directory.Exists(folder)) return true;
            return !Directory.EnumerateFiles(folder, "*.db", SearchOption.TopDirectoryOnly).Any()
                && ExtraDataSubfolders.All(s => !Directory.Exists(Path.Combine(folder, s)));
        }

        private static int CopyLocalDatasetTo(string targetRoot)
        {
            int copied = 0;
            var sourceRoot = AppDataRoot.ProductionFolder;
            if (!Directory.Exists(sourceRoot)) return 0;

            foreach (var file in Directory.GetFiles(sourceRoot, "*.db", SearchOption.TopDirectoryOnly))
            {
                File.Copy(file, Path.Combine(targetRoot, Path.GetFileName(file)), overwrite: false);
                copied++;
            }

            foreach (var sub in ExtraDataSubfolders)
            {
                var source = Path.Combine(sourceRoot, sub);
                var target = Path.Combine(targetRoot, sub);
                if (!Directory.Exists(source) || Directory.Exists(target)) continue;
                CopyDirectory(source, target);
                copied++;
            }

            return copied;
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: false);
            foreach (var dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }
    }
}
