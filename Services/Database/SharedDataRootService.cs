using Microsoft.Data.Sqlite;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Logging;
using System;
using System.Diagnostics;
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
    ///      path when validation failed, leaving the next startup without
    ///      a usable shared target. That invalid configuration is still
    ///      rejected; a valid share that later goes offline now selects a
    ///      visible local fallback instead of terminating the application.
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
                    Message = "Enter a shared folder different from the local ProGlass AppData folder. Recommended: \\\\OFFICE-PC\\ProGlassData (replace OFFICE-PC with the host PC name) or a mapped drive on the same LAN/Wi-Fi.",
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

        /// <summary>
        /// A folder this PC can create and share. Never the local AppData
        /// root — that path is rejected by <see cref="AppDataRoot.TryNormalizeSharedRoot"/>.
        /// </summary>
        public static string SuggestShareableFolder()
        {
            string? root = Path.GetPathRoot(Environment.SystemDirectory);
            if (string.IsNullOrWhiteSpace(root)) root = @"C:\";
            string candidate = Path.Combine(root, "ProGlassData");
            try
            {
                string full = Path.GetFullPath(candidate);
                string local = Path.GetFullPath(AppDataRoot.ProductionFolder);
                string demo = Path.GetFullPath(AppDataRoot.DemoFolder);
                if (full.Equals(local, StringComparison.OrdinalIgnoreCase)
                    || full.Equals(demo, StringComparison.OrdinalIgnoreCase)
                    || full.StartsWith(local + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || full.StartsWith(demo + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    candidate = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
                        "ProGlassData");
                }
            }
            catch
            {
                candidate = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
                    "ProGlassData");
            }
            return candidate;
        }

        /// <summary>
        /// Creates the company folder and, on a local path, publishes a
        /// Windows file share named ProGlassData. Creating a folder does
        /// not copy databases and does not require a safety backup — the
        /// old Prepare button cancelled before any folder existed when the
        /// backup failed, which is why operators could not create a share.
        /// </summary>
        public static (bool Ok, string Message, string Path) CreateSharedFolder(
            string? path, bool publishWindowsShare = true)
        {
            bool usedSuggestion = string.IsNullOrWhiteSpace(path);
            string requested = usedSuggestion ? SuggestShareableFolder() : path!.Trim();
            if (!AppDataRoot.TryNormalizeSharedRoot(requested, out string normalized))
            {
                return (false,
                    "✗ That path cannot be the shared folder. Use a new folder such as C:\\ProGlassData, not the local ProGlass AppData folder.",
                    requested);
            }

            try
            {
                Directory.CreateDirectory(normalized);
                string probeFile = Path.Combine(normalized, ".proglass-share-probe");
                File.WriteAllText(probeFile, "ok");
                File.Delete(probeFile);
                WriteManifest(normalized);
            }
            catch (Exception ex)
            {
                string extra = requested.StartsWith(@"\\", StringComparison.Ordinal)
                    ? " A network path can only be created when the share already exists. Leave the path empty and press Create Folder on the computer that will host the files."
                    : "";
                return (false,
                    "✗ Could not create the shared folder: " + AppDataRoot.DescribeShareFailure(ex) + extra,
                    normalized);
            }

            string shareNote = publishWindowsShare
                ? TryPublishWindowsShare(normalized)
                : " Share this folder from File Explorer → Properties → Sharing so other workstations can open it.";
            string created = usedSuggestion
                ? "✓ Created " + normalized + " (the path was empty, so ProGlass chose a folder outside AppData)."
                : "✓ Created shared folder: " + normalized + ".";
            return (true, created + shareNote, normalized);
        }

        /// <summary>
        /// Best-effort SMB publish. Failure does not undo the folder —
        /// Windows share creation needs administrator rights, and a folder
        /// the operator can share by hand is still a successful create.
        /// </summary>
        private static string TryPublishWindowsShare(string folder)
        {
            if (folder.StartsWith(@"\\", StringComparison.Ordinal))
                return " This path is already on a network share. Other workstations can use it as typed.";
            if (!OperatingSystem.IsWindows())
                return " Share this folder from the host operating system so other workstations can open it.";
            if (folder.IndexOf('"') >= 0)
                return " The folder exists. Its path contains a quote, so ProGlass did not publish a Windows share.";

            const string shareName = "ProGlassData";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "net",
                    Arguments = "share " + shareName + "=\"" + folder + "\" /GRANT:Everyone,CHANGE /REMARK:\"ProGlass company database\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                if (process == null)
                    return " The folder exists. Windows did not start the share command.";
                if (!process.WaitForExit(8000))
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (Exception killEx)
                    {
                        AppLogService.Warning(
                            "Windows share command did not exit and could not be stopped: " + killEx.Message,
                            new LogContext { Module = "Database", Operation = "PublishWindowsShare" });
                    }
                    return " The folder exists. Windows did not finish publishing the share. Share it from File Explorer → Properties → Sharing.";
                }
                string output = (process.StandardOutput.ReadToEnd() + " " + process.StandardError.ReadToEnd()).Trim();
                if (process.ExitCode == 0)
                    return " Windows share \\\\" + Environment.MachineName + "\\" + shareName + " is published. Other workstations use that path. Tighten permissions in Windows if this network is not private.";
                if (output.Contains("already been shared", StringComparison.OrdinalIgnoreCase)
                    || output.Contains("2118", StringComparison.Ordinal))
                    return " Windows share " + shareName + " already exists. Other workstations use \\\\" + Environment.MachineName + "\\" + shareName + ".";
                return " The folder was created, but Windows did not publish the share"
                    + (string.IsNullOrWhiteSpace(output) ? "." : ": " + output)
                    + " Share it from File Explorer → Properties → Sharing, or run ProGlass as administrator once.";
            }
            catch (Exception ex)
            {
                return " The folder was created. Windows share was not published: " + ex.Message;
            }
        }

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
                    "(for example \\\\OFFICE-PC\\ProGlassData) — shared mode is not saved with a missing path.");
            }

            config.DataConnectivity.UseSharedDataRoot = enabled;
            config.DataConnectivity.SharedDataRootPath = enabled ? normalized : "";
            config.DataConnectivity.CopyLocalDataOnEnable = copyLocalData;
            config.DataConnectivity.SharedRootOfflineFallbackNeedsReview = false;
            config.DataConnectivity.SharedRootLocalModeNoticePending = false;
            config.DataConnectivity.SharedRootReconnectNoticeShown = false;
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
                    using var conn = new SqliteConnection(
                        SqliteResilience.BuildConnectionString(db));
                    conn.Open();
                    SqliteResilience.ApplyConnectionPolicy(conn);
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
                    using var conn = new SqliteConnection(
                        SqliteResilience.BuildSharedRootConnectionString(db));
                    conn.Open();
                    string mode = SqliteResilience.ApplyConnectionPolicy(
                        conn, sharedRootOverride: true);
                    if (string.Equals(mode, "delete", StringComparison.OrdinalIgnoreCase))
                        converted++;
                }
                catch (Exception ex)
                {
                    // Do not report the root prepared when even one database
                    // remains in WAL mode. Applying shared settings is gated
                    // on every file passing the central mode verification.
                    AppLogService.Error(
                        $"Could not set rollback journal on shared {Path.GetFileName(db)}.",
                        ex,
                        new LogContext { Module = "Database", Operation = "ConvertSharedDatabases" });
                    throw new InvalidOperationException(
                        $"Could not convert {Path.GetFileName(db)} to the required DELETE journal mode. " +
                        "Close all ProGlass workstations and retry shared-folder preparation.", ex);
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
