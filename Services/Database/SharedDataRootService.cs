using Microsoft.Data.Sqlite;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Logging;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

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
    ///
    /// SENIOR AUDIT (2026-09-24) — the shared folder preparation used to
    /// corrupt the company database in three quiet ways:
    ///   1. It copied the live -wal and -shm sidecars of the running app.
    ///      -shm is per-process shared-memory state (SQLite documentation:
    ///      never copy it), and WAL frames captured mid-transaction leave
    ///      the copied .db inconsistent.
    ///   2. It left the copied databases in WAL journal mode. WAL across
    ///      an SMB/NAS redirector is unsafe — the -shm coordination file
    ///      is not reliable over a network share, and two workstations can
    ///      diverge silently.
    ///   3. ApplyToConfig persisted UseSharedDataRoot=true with an empty
    ///      path when validation failed, so the next startup silently ran
    ///      on local AppData while Settings still claimed "shared".
    /// All three are fixed here: checkpoint the local databases first,
    /// copy only the .db files, convert every shared database to
    /// rollback-journal (DELETE) mode — persisted in the SQLite header —
    /// and refuse to save a half-configured shared mode.
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
                    Message = "Enter a shared folder different from the local ProGlass AppData folder. Recommended: \\\\SERVER\\\\ProGlassData or a mapped drive on the same LAN/Wi-Fi.",
                    NormalizedPath = folder ?? ""
                };
            }

            bool network = LooksNetworkBased(normalized);

            try
            {
                Directory.CreateDirectory(normalized);
                var testFile = Path.Combine(normalized,
                    ".proglass_network_write_test_" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(testFile,
                    "ProGlass shared database write test " + DateTime.UtcNow.ToString("o"));
                _ = File.ReadAllText(testFile);
                try { File.Delete(testFile); }
                catch { /* an orphaned .tmp test file is harmless */ }

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
                    LooksLikeNetworkPath = network,
                    Message = (network
                        ? "✓ Shared data folder is reachable and writable on the network."
                        : "✓ Folder is writable. It is a local path — make sure this exact folder is shared to all workstations before using it company-wide.")
                        + dataHint + manifestHint + " Restart ProGlass on each workstation after enabling."
                };
            }
            catch (Exception ex)
            {
                return new SharedDataRootProbeResult
                {
                    Ok = false,
                    NormalizedPath = normalized,
                    LooksLikeNetworkPath = network,
                    Message = "✗ Shared folder is not ready: " + AppDataRoot.DescribeShareFailure(ex)
                };
            }
        }

        /// <summary>
        /// A folder is "network based" when it is a UNC path or a local
        /// drive letter the Windows redirector reports as a network drive
        /// (a mapped LAN/Wi-Fi share).
        /// </summary>
        public static bool LooksNetworkBased(string path) =>
            !string.IsNullOrWhiteSpace(path)
            && (path.Trim().StartsWith(@"\\", StringComparison.Ordinal)
                || !string.IsNullOrEmpty(AppDataRoot.GetMappedNetworkPath(path)));

        public static (bool Ok, string Message) PrepareSharedFolder(
            string sharedFolder,
            bool copyLocalData)
        {
            var probe = Probe(sharedFolder);
            if (!probe.Ok) return (false, probe.Message);

            try
            {
                // Checkpoint every local WAL database BEFORE copying, so
                // the .db files contain everything and no sidecars are
                // needed (see class header).
                int checkpointed = CheckpointLocalDatabases();

                Directory.CreateDirectory(probe.NormalizedPath);
                int copied = 0;

                if (copyLocalData && IsFolderEmptyOfProGlassData(probe.NormalizedPath))
                    copied = CopyLocalDatasetTo(probe.NormalizedPath);

                // Convert every database now living on the share to
                // rollback-journal (DELETE) mode. The mode is persisted in
                // the SQLite file header, so from this moment every
                // workstation opens the shared databases SMB-safe.
                int converted = ConvertDatabasesToRollbackJournal(probe.NormalizedPath);

                WriteManifest(probe.NormalizedPath);

                return (true, "✓ Shared folder prepared"
                    + (copied > 0 ? $" and {copied} local data item(s) copied" : "")
                    + $", {checkpointed} local database(s) checkpointed, {converted} shared database(s) set to rollback-journal mode."
                    + " Save settings and restart all workstations.");
            }
            catch (Exception ex)
            {
                AppLogService.Error("Shared data folder preparation failed", ex,
                    new LogContext { Module = "Database", Operation = "PrepareSharedFolder" });
                return (false, "✗ Could not prepare shared folder: " + ex.Message);
            }
        }

        /// <summary>
        /// Writes the shared-mode decision into config. Returns (Ok,
        /// Message) instead of void: the caller must not report success —
        /// or persist anything — when the enabled state is invalid.
        /// </summary>
        public static (bool Ok, string Message) ApplyToConfig(
            AppConfig config, bool enabled, string path, bool copyLocalData)
        {
            config.DataConnectivity ??= new DataConnectivitySettings();

            // Declared before the guard: when enabled == false the
            // TryNormalizeSharedRoot call short-circuits and its out
            // parameter would be unassigned (CS0165) at the use below.
            string normalized = "";
            if (enabled && !AppDataRoot.TryNormalizeSharedRoot(path, out normalized))
            {
                return (false, "✗ Enter a valid shared folder path first " +
                    "(for example \\\\SERVER\\ProGlassData) — shared mode is not saved with a missing path.");
            }

            config.DataConnectivity.UseSharedDataRoot = enabled;
            config.DataConnectivity.SharedDataRootPath = enabled ? normalized : "";
            config.DataConnectivity.CopyLocalDataOnEnable = copyLocalData;
            if (string.IsNullOrWhiteSpace(config.DataConnectivity.WorkstationName))
                config.DataConnectivity.WorkstationName = Environment.MachineName;

            return (true, enabled
                ? "✓ Shared company database connectivity saved."
                : "✓ Workstation will use its local AppData database root after restart.");
        }

        // ── Preparation internals ────────────────────────────────────────

        /// <summary>
        /// Forces every local SQLite database to checkpoint its WAL into
        /// the main .db file. Best-effort: a database that refuses
        /// (writer busy) is logged and skipped — copying an already-
        /// consistent .db without its sidecars is still safe, it just
        /// misses the last in-flight transaction.
        /// </summary>
        private static int CheckpointLocalDatabases()
        {
            var sourceRoot = AppDataRoot.ProductionFolder;
            if (!Directory.Exists(sourceRoot)) return 0;

            int done = 0;
            foreach (var db in Directory.GetFiles(sourceRoot, "*.db", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    using var conn = new SqliteConnection("Data Source=" + db);
                    conn.Open();
                    for (int attempt = 0; attempt < 5; attempt++)
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                        using var reader = cmd.ExecuteReader();
                        if (reader.Read() && reader.GetInt32(0) == 0)
                        {
                            done++;
                            break;
                        }
                        Thread.Sleep(250);
                    }
                }
                catch (Exception ex)
                {
                    AppLogService.Warning(
                        $"WAL checkpoint skipped for {Path.GetFileName(db)}: {ex.Message}",
                        new LogContext { Module = "Database", Operation = "CheckpointLocalDatabases" });
                }
            }
            return done;
        }

        /// <summary>
        /// Sets rollback-journal mode on every .db now on the shared root.
        /// The mode is stored in the database header, so one conversion
        /// here protects every workstation from WAL-over-SMB for the life
        /// of the file.
        /// </summary>
        private static int ConvertDatabasesToRollbackJournal(string root)
        {
            if (!Directory.Exists(root)) return 0;

            int converted = 0;
            foreach (var db in Directory.GetFiles(root, "*.db", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    using var conn = new SqliteConnection("Data Source=" + db);
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "PRAGMA journal_mode = DELETE;";
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read()
                        && string.Equals(reader.GetString(0), "delete", StringComparison.OrdinalIgnoreCase))
                        converted++;
                }
                catch (Exception ex)
                {
                    // A conversion that needs exclusive access can lose to
                    // a workstation that is currently running. Logged here;
                    // SqliteResilience's journal-mode verification will fail
                    // any affected connection loudly instead of running WAL
                    // over the share.
                    AppLogService.Warning(
                        $"Could not set rollback journal on shared {Path.GetFileName(db)}: {ex.Message}",
                        new LogContext { Module = "Database", Operation = "ConvertSharedDatabases" });
                }
            }
            return converted;
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
                    JournalMode = "DELETE (rollback journal — WAL is unsafe over SMB/NAS)",
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
                // Only the .db file is copied. The WAL was absorbed into it
                // by CheckpointLocalDatabases, and -shm is per-process
                // shared-memory state that must never be copied.
                var target = Path.Combine(targetRoot, Path.GetFileName(file));
                if (File.Exists(target)) continue;
                File.Copy(file, target, overwrite: false);
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
