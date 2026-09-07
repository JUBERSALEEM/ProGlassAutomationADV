using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Logging;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

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
        private const string ManifestFileName = "proglass_shared_root.json";
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

                int dbCount = Directory.GetFiles(normalized, "*.db", SearchOption.TopDirectoryOnly).Length;
                bool hasManifest = File.Exists(Path.Combine(normalized, ManifestFileName));
                string dataHint = dbCount > 0
                    ? $" {dbCount} database file(s) found."
                    : " No database files found yet; Prepare Folder can seed it.";
                string manifestHint = hasManifest
                    ? " Shared-root manifest found."
                    : " Shared-root manifest not found yet.";

                return new SharedDataRootProbeResult
                {
                    Ok = true,
                    NormalizedPath = normalized,
                    LooksLikeNetworkPath = LooksNetworkBased(normalized),
                    Message = (LooksNetworkBased(normalized)
                        ? "✓ Shared data folder is reachable and writable on the network."
                        : "✓ Folder is writable. It is not a UNC path; confirm this mapped/local drive is shared to all workstations before using it company-wide.")
                        + dataHint + manifestHint + " Restart ProGlass on each workstation after enabling."
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

                WriteManifest(probe.NormalizedPath);

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


        private static void WriteManifest(string root)
        {
            try
            {
                var manifest = new
                {
                    Product = "ProGlass Automation",
                    Purpose = "Shared LAN/Wi-Fi company database root",
                    CreatedOrUpdatedAtUtc = DateTime.UtcNow,
                    Workstation = Environment.MachineName,
                    AppVersion = typeof(SharedDataRootService).Assembly.GetName().Version?.ToString() ?? "",
                    Warning = "Do not edit database files manually. Do not use cloud-sync folders for live database files."
                };
                File.WriteAllText(Path.Combine(root, ManifestFileName),
                    JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                AppLogService.Warning("Shared-root manifest could not be written: " + ex.Message,
                    new LogContext { Module = "Database", Operation = "WriteSharedRootManifest" });
            }
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
                copied += CopySqliteFileSet(file, targetRoot);
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


        private static int CopySqliteFileSet(string dbPath, string targetRoot)
        {
            int copied = 0;
            foreach (var path in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
            {
                if (!File.Exists(path)) continue;
                var target = Path.Combine(targetRoot, Path.GetFileName(path));
                if (File.Exists(target)) continue;
                File.Copy(path, target, overwrite: false);
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
