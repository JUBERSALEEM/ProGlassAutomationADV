// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Database/DatabaseBackup/BackupService.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — backup & restore (v2 upgrade)
//
// v1 backed up only proglass.db. The app backup set copies every top-level
// SQLite database + existing WAL/SHM sidecars, config.json when present,
// and the whitelisted Inquiry/Proforma/Delivery attachment trees. It is a
// managed-file snapshot, NOT a byte-for-byte copy of the complete data root:
// arbitrary root files and files absent from a selected set are not removed
// by RestoreBackupSet. Whole-root manual restore is documented separately.
// Tax Invoice attachments are blobs inside taxinvoices.db.
//
// Layout:  <ActiveRoot>\Backups\set_<timestamp>\{ top-level *.db and
//           sidecars, config.json when present, Inquiries\,
//           ProformaInvoices\, DeliveryNotes\ }
//
// SAFETY RULES:
//   • Restore first tries to create a complete CURRENT-state safety set.
//   • Restore requires caller-side user confirmation (Settings asks).
//   • Partial backup sets are rejected; partial restores are reported as
//     failures and include the safety-set path. Restore is still an overlay.
//   • Retention pruning is logged.
//
// The original single-file API (CreateBackup / RestoreFromBackup /
// GetAvailableBackups) is preserved for backward compatibility.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Data.Sqlite;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace ProGlassAutomation.Services.Database.DatabaseBackup
{
    /// <summary>Result of creating a managed backup set.</summary>
    public readonly record struct BackupSetCreateResult(
        bool Success, string FolderPath, int FileCount, string Message);

    /// <summary>One backup set on disk.</summary>
    public class BackupSetInfo
    {
        public string FolderPath { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public long TotalBytes { get; set; }
        public int FileCount { get; set; }
        public bool IsRestorable { get; set; }
        public bool HasInventoryManifest { get; set; }
        public string RestoreValidationMessage { get; set; } = "";

        public string RestoreStatusDisplay => IsRestorable
            ? string.IsNullOrWhiteSpace(RestoreValidationMessage)
                ? "Restorable"
                : "Restorable — " + RestoreValidationMessage
            : "Not restorable — " + RestoreValidationMessage;

        public string SizeDisplay =>
            TotalBytes < 1024 * 1024
                ? $"{TotalBytes / 1024.0:0.#} KB"
                : $"{TotalBytes / (1024.0 * 1024.0):0.##} MB";
    }

    /// <summary>
    /// Managed-file backup & overlay restore. Static facade, never throws —
    /// backup problems are logged and returned as explicit failures.
    /// </summary>
    public static class BackupService
    {
        private static string AppDataFolder => AppDataRoot.CurrentFolder;

        private static string BackupFolder =>
            Path.Combine(AppDataFolder, "Backups");

        /// <summary>
        /// Folder names (direct children of the AppData root) whose FILE
        /// contents are business data living outside the *.db files:
        ///   Inquiries\{id}\Attachments\        — inquiry attachments
        ///   ProformaInvoices\Attachments\      — PI attachments + manifests
        ///   DeliveryNotes\Attachments\         — DN attachments (Images/PDFs)
        /// Tax Invoice attachments are blobs inside taxinvoices.db and ride
        /// the *.db copy; they do not appear here.
        ///
        /// AUDIT FIX (2026-09-05, P0): v2 copied only *.db + config.json, so
        /// every PI/DN/Inquiry attachment file was silently lost on a
        /// restore. The whitelist is DELIBERATE and static — it is never
        /// derived from a directory listing, because Backups\ itself is a
        /// child of AppDataFolder and "copy all subfolders" would recurse
        /// backups into backups forever.
        /// </summary>
        internal static readonly string[] ExtraDataSubfolders =
            { "Inquiries", "ProformaInvoices", "DeliveryNotes" };

        /// <summary>Root folder where backup sets are stored (for the restore drill).</summary>
        public static string GetBackupsFolder() => BackupFolder;

        private const int MaxBackupSets = 10;
        private const int LegacyMaxBackups = 7;
        private const string InventoryManifestFileName = "backup-manifest.json";
        private const int InventoryManifestVersion = 1;

        private sealed class BackupInventoryManifest
        {
            public int FormatVersion { get; set; }
            public DateTime CreatedAtUtc { get; set; }
            public List<BackupInventoryEntry> Files { get; set; } = new();
        }

        private sealed class BackupInventoryEntry
        {
            public string RelativePath { get; set; } = "";
            public long Length { get; set; }
            public string Sha256 { get; set; } = "";
        }

        // PROFESSIONAL backup policy:
        //   • Automatic (on exit) only when the newest backup set on
        //     disk is older than AutoBackupInterval — closing and
        //     reopening the app all day creates at most one set/hour,
        //     never one per exit.
        //   • Manual creation warns (in the UI layer) when a backup
        //     was made within ManualWarnWindow.
        private static readonly TimeSpan AutoBackupInterval =
            TimeSpan.FromHours(1);

        /// <summary>Manual backups newer than this should prompt first.</summary>
        public static readonly TimeSpan ManualWarnWindow =
            TimeSpan.FromMinutes(10);

        // ══════════════════════════════════════════════════════════════
        //  CREATE
        // ══════════════════════════════════════════════════════════════

        /// <summary>How long since the newest backup set (null = none).</summary>
        public static TimeSpan? TimeSinceLastBackup()
        {
            var sets = GetBackupSets();
            if (sets.Length == 0) return null;
            return DateTime.Now - sets[0].CreatedAt;
        }

        /// <summary>
        /// Automatic backup (App.OnExit). Throttled against the newest
        /// set ON DISK so frequent app restarts never flood the folder.
        /// </summary>
        public static void CreateBackup()
        {
            try
            {
                var sinceLast = TimeSinceLastBackup();
                if (sinceLast.HasValue && sinceLast.Value < AutoBackupInterval)
                    return;

                CreateFullBackupSet("auto");
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Auto backup failed: {ex.Message}",
                    new LogContext
                    {
                        Module = "Backup",
                        Class = nameof(BackupService),
                        Operation = "CreateBackup"
                    });
            }
        }

        /// <summary>
        /// Creates one complete managed-file backup set. Any copy error
        /// rejects and removes the partial set. Returns its folder, or an
        /// empty string on failure.
        /// </summary>
        public static string CreateFullBackupSet(
            string reason = "manual",
            string? preserveSetFolder = null) =>
            CreateFullBackupSetDetailed(reason, preserveSetFolder).FolderPath;

        /// <summary>
        /// Creates a complete managed-file backup set and returns the actual
        /// outcome for the UI. Any missing required DB or copy failure rejects
        /// and removes the partial set.
        /// </summary>
        public static BackupSetCreateResult CreateFullBackupSetDetailed(
            string reason = "manual",
            string? preserveSetFolder = null)
        {
            string setFolder = "";
            try
            {
                AppDataRoot.EnsureCurrentFolder();
                Directory.CreateDirectory(BackupFolder);

                // Milliseconds avoid two manual/exit backups colliding in the
                // same second and accidentally mixing their contents.
                var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff") +
                    "_" + Guid.NewGuid().ToString("N")[..6];
                setFolder = Path.Combine(BackupFolder, $"set_{stamp}");
                Directory.CreateDirectory(setFolder);

                var failures = new System.Collections.Generic.List<string>();
                int copied = CopyDataFilesInto(setFolder, failures);
                if (copied == 0 || failures.Count > 0)
                {
                    TryDeleteIncompleteSet(setFolder);
                    string detail = failures.Count == 0
                        ? "No files were copied."
                        : string.Join("; ", failures.Take(5));
                    string message =
                        $"Backup failed: copied {copied} data file(s); {failures.Count} copy failure(s). {detail}";
                    AppLogService.Warning(message,
                        new LogContext { Module = "Backup", Operation = "Create" });
                    return new BackupSetCreateResult(false, "", copied, message);
                }

                try
                {
                    WriteInventoryManifest(setFolder);
                }
                catch (Exception ex)
                {
                    TryDeleteIncompleteSet(setFolder);
                    string message =
                        $"Backup failed: could not create the file inventory manifest. {ex.Message}";
                    AppLogService.Error(message, ex,
                        new LogContext { Module = "Backup", Operation = "CreateManifest" });
                    return new BackupSetCreateResult(false, "", copied, message);
                }

                CleanupOldSets(preserveSetFolder);

                int totalFiles = copied + 1; // include backup-manifest.json
                string success = $"Backup set created ({reason}): {copied} data file(s) plus a verified inventory manifest at {setFolder}.";
                AppLogService.Info(success,
                    new LogContext { Module = "Backup", Operation = "Create" });
                return new BackupSetCreateResult(true, setFolder, totalFiles, success);
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(setFolder))
                    TryDeleteIncompleteSet(setFolder);
                string failure = $"Backup set creation failed: {ex.Message}";
                AppLogService.Error(failure, ex,
                    new LogContext { Module = "Backup", Operation = "Create" });
                return new BackupSetCreateResult(false, "", 0, failure);
            }
        }

        private static void WriteInventoryManifest(string setFolder)
        {
            var entries = new List<BackupInventoryEntry>();
            foreach (string file in Directory.GetFiles(
                         setFolder, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(setFolder, file)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/');
                var info = new FileInfo(file);
                entries.Add(new BackupInventoryEntry
                {
                    RelativePath = relative,
                    Length = info.Length,
                    Sha256 = ComputeSha256(file)
                });
            }

            if (entries.Count == 0)
                throw new InvalidOperationException(
                    "The backup has no data files to inventory.");

            var manifest = new BackupInventoryManifest
            {
                FormatVersion = InventoryManifestVersion,
                CreatedAtUtc = DateTime.UtcNow,
                Files = entries.OrderBy(e => e.RelativePath,
                    StringComparer.OrdinalIgnoreCase).ToList()
            };

            string manifestPath = Path.Combine(setFolder, InventoryManifestFileName);
            string temporaryPath = manifestPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(manifest));
            File.Move(temporaryPath, manifestPath, overwrite: false);
        }

        private static string ComputeSha256(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        private static void TryDeleteIncompleteSet(string setFolder)
        {
            try
            {
                if (Directory.Exists(setFolder))
                    Directory.Delete(setFolder, recursive: true);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Could not remove incomplete backup set '{setFolder}': {ex.Message}",
                    new LogContext { Module = "Backup", Operation = "CleanupIncompleteSet" });
            }
        }

        private static int CopyDataFilesInto(
            string setFolder,
            System.Collections.Generic.List<string> failures)
        {
            // Release pooled SQLite connections so the live .db files are
            // not locked (or mid-write) while we copy them. This affects this
            // process only; shared-root operators must close every client.
            try { SqliteConnection.ClearAllPools(); }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Could not clear local SQLite pools before backup: {ex.Message}",
                    new LogContext { Module = "Backup", Operation = "Copy" });
            }

            int copied = 0;
            var source = new DirectoryInfo(AppDataFolder);
            if (!source.Exists)
                throw new DirectoryNotFoundException(
                    $"Active data root does not exist: {source.FullName}");

            foreach (var db in source.GetFiles("*.db"))
                copied += CopySqliteFileSet(db.FullName, setFolder, failures);

            var config = Path.Combine(AppDataFolder, "config.json");
            if (File.Exists(config))
            {
                try
                {
                    File.Copy(config, Path.Combine(setFolder, "config.json"),
                        overwrite: true);
                    copied++;
                }
                catch (Exception ex)
                {
                    failures.Add($"config.json: {ex.Message}");
                    AppLogService.Warning(
                        $"Could not back up config.json: {ex.Message}",
                        new LogContext { Module = "Backup", Operation = "Copy" });
                }
            }

            // Attachment trees are part of the backup contract. Any skipped
            // file invalidates the set; never report a partial set as full.
            foreach (var sub in ExtraDataSubfolders)
            {
                var sourceDir = Path.Combine(AppDataFolder, sub);
                if (!Directory.Exists(sourceDir)) continue;

                try
                {
                    copied += CopyDirectoryTree(
                        sourceDir, Path.Combine(setFolder, sub), failures);
                }
                catch (Exception ex)
                {
                    failures.Add($"{sub}: {ex.Message}");
                    AppLogService.Warning(
                        $"Attachment folder '{sub}' could not be fully backed up: {ex.Message}",
                        new LogContext { Module = "Backup", Operation = "Copy" });
                }
            }

            if (!source.GetFiles("*.db").Any())
                failures.Add("No top-level SQLite .db files were found.");

            return copied;
        }


        /// <summary>
        /// Takes a SQLite-supported online backup into one standalone
        /// database file. Unlike copying .db, -wal and -shm independently,
        /// BackupDatabase obtains a consistent SQLite snapshot while readers
        /// and writers may be active. The output is verified before it is
        /// counted as a successful backup file.
        /// </summary>
        private static int CopySqliteFileSet(
            string dbPath,
            string targetFolder,
            System.Collections.Generic.List<string> failures)
        {
            string targetPath = Path.Combine(targetFolder, Path.GetFileName(dbPath));
            try
            {
                var sourceBuilder = new SqliteConnectionStringBuilder
                {
                    DataSource = dbPath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false
                };
                var targetBuilder = new SqliteConnectionStringBuilder
                {
                    DataSource = targetPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Pooling = false
                };

                using (var source = new SqliteConnection(sourceBuilder.ToString()))
                using (var target = new SqliteConnection(targetBuilder.ToString()))
                {
                    source.Open();
                    target.Open();
                    source.BackupDatabase(target);

                    using var check = target.CreateCommand();
                    check.CommandText = "PRAGMA integrity_check;";
                    var integrity = Convert.ToString(check.ExecuteScalar());
                    if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException(
                            $"SQLite integrity_check failed for {Path.GetFileName(dbPath)}: {integrity}");
                }

                return 1;
            }
            catch (Exception ex)
            {
                try
                {
                    if (File.Exists(targetPath)) File.Delete(targetPath);
                    if (File.Exists(targetPath + "-wal")) File.Delete(targetPath + "-wal");
                    if (File.Exists(targetPath + "-shm")) File.Delete(targetPath + "-shm");
                }
                catch (Exception cleanupEx)
                {
                    AppLogService.Warning(
                        $"Could not remove incomplete SQLite backup '{targetPath}': {cleanupEx.Message}",
                        new LogContext { Module = "Backup", Operation = "CleanupIncompleteDatabase" });
                }

                failures.Add($"{Path.GetFileName(dbPath)}: {ex.Message}");
                AppLogService.Error(
                    $"SQLite online backup failed for {Path.GetFileName(dbPath)}: {ex.Message}",
                    ex,
                    new LogContext { Module = "Backup", Class = nameof(BackupService), Operation = "CopySqliteDatabase" });
                return 0;
            }
        }

        /// <summary>
        /// Copies @src into @dst recursively, creating directories as
        /// needed. Returns the number of files copied and records every
        /// per-file/directory error for the backup or restore caller.
        /// </summary>
        private static int CopyDirectoryTree(
            string sourceDir,
            string targetDir,
            System.Collections.Generic.List<string> failures)
        {
            Directory.CreateDirectory(targetDir);
            int copied = 0;

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                try
                {
                    File.Copy(file, Path.Combine(
                        targetDir, Path.GetFileName(file)), overwrite: true);
                    copied++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{file}: {ex.Message}");
                    AppLogService.Warning(
                        $"Copy skipped file {file}: {ex.Message}",
                        new LogContext { Module = "Backup", Operation = "Copy" });
                }
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                try
                {
                    copied += CopyDirectoryTree(subDir,
                        Path.Combine(targetDir, Path.GetFileName(subDir)), failures);
                }
                catch (Exception ex)
                {
                    failures.Add($"{subDir}: {ex.Message}");
                    AppLogService.Warning(
                        $"Copy skipped directory {subDir}: {ex.Message}",
                        new LogContext { Module = "Backup", Operation = "Copy" });
                }
            }

            return copied;
        }

        // ══════════════════════════════════════════════════════════════
        //  LIST
        // ══════════════════════════════════════════════════════════════

        /// <summary>All backup sets, newest first. Use TryGetBackupSets for truthful UI status.</summary>
        public static BackupSetInfo[] GetBackupSets() =>
            TryGetBackupSets(out var sets, out _) ? sets : Array.Empty<BackupSetInfo>();

        /// <summary>
        /// Lists sets without conflating an inaccessible root with an empty
        /// folder. Each set is validated for at least one readable SQLite DB.
        /// </summary>
        public static bool TryGetBackupSets(out BackupSetInfo[] sets, out string message)
        {
            sets = Array.Empty<BackupSetInfo>();
            message = "";
            try
            {
                AppDataRoot.EnsureCurrentFolder();
                if (!Directory.Exists(AppDataRoot.CurrentFolder))
                {
                    message = $"Backup list unavailable: active data root does not exist: {AppDataRoot.CurrentFolder}";
                    return false;
                }

                DirectoryInfo[] directories;
                try
                {
                    directories = new DirectoryInfo(BackupFolder).GetDirectories("set_*");
                }
                catch (DirectoryNotFoundException)
                {
                    message = "No backup sets found in the active data root.";
                    return true;
                }

                sets = directories
                    .Select(dir =>
                    {
                        // AllDirectories includes attachment trees in the
                        // visible size/file count. Validation considers only
                        // the required top-level database files.
                        var files = dir.GetFiles("*", SearchOption.AllDirectories);
                        string validation = ValidateBackupSetFiles(dir.GetFiles());
                        string manifestValidation = ValidateInventoryManifest(
                            dir.FullName, verifyHashes: false,
                            out bool hasManifest, out string manifestStatus);
                        if (string.IsNullOrEmpty(validation)
                            && !string.IsNullOrEmpty(manifestValidation))
                            validation = manifestValidation;
                        return new BackupSetInfo
                        {
                            FolderPath = dir.FullName,
                            Name = dir.Name,
                            CreatedAt = dir.CreationTime,
                            TotalBytes = files.Sum(f => f.Length),
                            FileCount = files.Length,
                            IsRestorable = string.IsNullOrEmpty(validation),
                            HasInventoryManifest = hasManifest,
                            RestoreValidationMessage = string.IsNullOrEmpty(validation)
                                ? manifestStatus
                                : validation
                        };
                    })
                    .OrderByDescending(s => s.CreatedAt)
                    .ToArray();

                message = sets.Length == 0
                    ? "No backup sets found in the active data root."
                    : $"Found {sets.Length} backup set(s) in {BackupFolder}.";
                return true;
            }
            catch (Exception ex)
            {
                sets = Array.Empty<BackupSetInfo>();
                message = $"Backup list unavailable: {ex.Message}";
                AppLogService.Error(message, ex,
                    new LogContext { Module = "Backup", Operation = "List" });
                return false;
            }
        }

        private static string ValidateBackupSetFiles(FileInfo[] files)
        {
            var unsupported = files.FirstOrDefault(file =>
                !IsSupportedRootBackupFileName(file.Name));
            if (unsupported != null)
                return $"unsupported top-level file '{unsupported.Name}'";

            var databases = files
                .Where(file => string.Equals(Path.GetExtension(file.Name), ".db",
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (databases.Length == 0)
                return "no top-level SQLite database file";

            foreach (var database in databases)
            {
                try
                {
                    using var stream = database.OpenRead();
                    byte[] header = new byte[16];
                    int read = stream.Read(header, 0, header.Length);
                    if (read != header.Length
                        || !header.SequenceEqual(new byte[]
                        {
                            0x53, 0x51, 0x4C, 0x69, 0x74, 0x65, 0x20, 0x66,
                            0x6F, 0x72, 0x6D, 0x61, 0x74, 0x20, 0x33, 0x00
                        }))
                        return $"database file '{database.Name}' has an invalid SQLite header";
                }
                catch (Exception ex)
                {
                    return $"database file '{database.Name}' cannot be read: {ex.Message}";
                }
            }

            return "";
        }

        private static bool IsSupportedRootBackupFileName(string fileName)
        {
            if (string.Equals(fileName, InventoryManifestFileName,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "config.json",
                    StringComparison.OrdinalIgnoreCase))
                return true;

            return fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".db-wal", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(".db-shm", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks a backup inventory against the files actually present.
        /// Listing checks path/length completeness cheaply; restore additionally
        /// hashes every file before creating the safety set or changing data.
        /// Older sets without a manifest remain restorable, but are explicitly
        /// labelled as completeness-unverified.
        /// </summary>
        private static string ValidateInventoryManifest(
            string setFolder,
            bool verifyHashes,
            out bool hasManifest,
            out string status)
        {
            string manifestPath = Path.Combine(setFolder, InventoryManifestFileName);
            hasManifest = File.Exists(manifestPath);
            status = hasManifest
                ? "Inventory present; hashes are checked before restore."
                : "Legacy set has no inventory manifest; file completeness is unverified.";
            if (!hasManifest) return "";

            try
            {
                var manifest = JsonSerializer.Deserialize<BackupInventoryManifest>(
                    File.ReadAllText(manifestPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (manifest == null
                    || manifest.FormatVersion != InventoryManifestVersion
                    || manifest.Files == null
                    || manifest.Files.Count == 0)
                    return "inventory manifest is empty or uses an unsupported format";

                string root = Path.GetFullPath(setFolder);
                string rootPrefix = root.EndsWith(Path.DirectorySeparatorChar.ToString(),
                    StringComparison.Ordinal)
                    ? root
                    : root + Path.DirectorySeparatorChar;
                var expected = new Dictionary<string, BackupInventoryEntry>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var entry in manifest.Files)
                {
                    if (entry == null
                        || !TryNormalizeInventoryPath(entry.RelativePath, out string relative)
                        || !IsSupportedInventoryPath(relative)
                        || entry.Length < 0
                        || string.IsNullOrWhiteSpace(entry.Sha256)
                        || !expected.TryAdd(relative, entry))
                        return "inventory manifest contains an invalid or duplicate file path";

                    string fullPath = Path.GetFullPath(Path.Combine(
                        root, relative.Replace('/', Path.DirectorySeparatorChar)));
                    if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                        return "inventory manifest contains a path outside the backup set";
                    if (!File.Exists(fullPath))
                        return $"inventory file '{relative}' is missing";

                    var info = new FileInfo(fullPath);
                    if (info.Length != entry.Length)
                        return $"inventory file '{relative}' has a different size";
                    if (verifyHashes && !string.Equals(
                            ComputeSha256(fullPath), entry.Sha256,
                            StringComparison.OrdinalIgnoreCase))
                        return $"inventory file '{relative}' failed SHA-256 verification";
                }

                foreach (string actualPath in Directory.GetFiles(
                             root, "*", SearchOption.AllDirectories))
                {
                    if (string.Equals(Path.GetFullPath(actualPath),
                            Path.GetFullPath(manifestPath),
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    string relative = Path.GetRelativePath(root, actualPath)
                        .Replace(Path.DirectorySeparatorChar, '/')
                        .Replace(Path.AltDirectorySeparatorChar, '/');
                    if (!expected.ContainsKey(relative))
                        return $"unlisted file '{relative}' is present in the backup set";
                }

                status = verifyHashes
                    ? "Inventory and SHA-256 hashes verified."
                    : "Inventory complete; SHA-256 hashes will be checked before restore.";
                return "";
            }
            catch (Exception ex)
            {
                return "inventory manifest could not be verified: " + ex.Message;
            }
        }

        private static bool TryNormalizeInventoryPath(
            string? path, out string normalized)
        {
            normalized = "";
            if (string.IsNullOrWhiteSpace(path)) return false;
            string slashPath = path.Replace('\\', '/');
            if (slashPath.StartsWith("/", StringComparison.Ordinal)
                || Path.IsPathRooted(path))
                return false;

            var segments = slashPath.Split('/');
            if (segments.Length == 0
                || segments.Any(segment => string.IsNullOrWhiteSpace(segment)
                    || segment == "." || segment == ".."))
                return false;

            normalized = string.Join("/", segments);
            return true;
        }

        private static bool IsSupportedInventoryPath(string relativePath)
        {
            if (string.Equals(relativePath, InventoryManifestFileName,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            if (ExtraDataSubfolders.Any(sub => relativePath.StartsWith(
                    sub + "/", StringComparison.OrdinalIgnoreCase)))
                return true;

            return !relativePath.Contains('/')
                && IsSupportedRootBackupFileName(relativePath);
        }

        // ══════════════════════════════════════════════════════════════
        //  RESTORE
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Overlays the files present in a backup set onto the active root.
        /// A safety snapshot of the CURRENT state is taken first. This is not
        /// a clean whole-folder replacement: files absent from the set remain
        /// in place. The caller must confirm and explain this distinction.
        /// </summary>
        public static (bool Success, string Message) RestoreBackupSet(
            string setFolder)
        {
            try
            {
                if (ProGlassAutomation.Services.Database.AppDataRoot.IsDemo)
                    return (false,
                        "Restore is blocked in Demo Mode. Exit Demo Mode first — " +
                        "restore writes the configured production data root.");

                AppDataRoot.EnsureCurrentFolder();
                if (!Directory.Exists(setFolder))
                    return (false, "Backup set not found.");

                var setDir = new DirectoryInfo(setFolder);
                var files = setDir.GetFiles();
                bool hasAttachmentTrees = ExtraDataSubfolders.Any(
                    sub => Directory.Exists(Path.Combine(setFolder, sub)));
                if (files.Length == 0 && !hasAttachmentTrees)
                    return (false, "Backup set is empty.");

                string validation = ValidateBackupSetFiles(files);
                string manifestValidation = ValidateInventoryManifest(
                    setDir.FullName, verifyHashes: true,
                    out bool hasManifest, out string manifestStatus);
                if (string.IsNullOrWhiteSpace(validation))
                    validation = manifestValidation;
                if (!string.IsNullOrWhiteSpace(validation))
                    return (false,
                        $"Backup set is not restorable: {validation}. No files were changed.");

                string verificationNote = hasManifest
                    ? manifestStatus + " "
                    : "Warning: this legacy backup has no inventory manifest, so only its SQLite headers could be checked; file completeness is unverified. ";

                // Safety snapshot first. If a later copy fails, the active
                // root may be mixed; the path is returned for recovery.
                var safetyResult = CreateFullBackupSetDetailed(
                    "pre-restore safety", preserveSetFolder: setDir.FullName);
                if (!safetyResult.Success)
                    return (false,
                        "Could not create a complete pre-restore safety backup. " +
                        safetyResult.Message + " No selected files were changed.");
                string safety = safetyResult.FolderPath;

                try { SqliteConnection.ClearAllPools(); }
                catch (Exception ex)
                {
                    AppLogService.Warning(
                        $"Could not clear local SQLite pools before restore: {ex.Message}",
                        new LogContext { Module = "Backup", Operation = "Restore" });
                }

                var failures = new System.Collections.Generic.List<string>();
                int restored = 0;
                int expected = files.Count(file => !string.Equals(
                    file.Name, InventoryManifestFileName,
                    StringComparison.OrdinalIgnoreCase));
                foreach (var sub in ExtraDataSubfolders)
                {
                    var sourceDir = Path.Combine(setFolder, sub);
                    if (Directory.Exists(sourceDir))
                        expected += Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories).Length;
                }

                foreach (var file in files)
                {
                    if (string.Equals(file.Name, InventoryManifestFileName,
                            StringComparison.OrdinalIgnoreCase))
                        continue; // inventory is metadata, never live-root data
                    try
                    {
                        file.CopyTo(Path.Combine(AppDataFolder, file.Name),
                            overwrite: true);
                        restored++;
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{file.Name}: {ex.Message}");
                        AppLogService.Warning(
                            $"Restore skipped {file.Name}: {ex.Message}",
                            new LogContext
                            {
                                Module = "Backup",
                                Operation = "Restore"
                            });
                    }
                }

                // Attachment folders are overlaid, not cleaned. The full-root
                // replacement tool is the supported way to remove files that
                // do not exist in the selected snapshot.
                foreach (var sub in ExtraDataSubfolders)
                {
                    var sourceDir = Path.Combine(setFolder, sub);
                    if (!Directory.Exists(sourceDir)) continue;

                    try
                    {
                        restored += CopyDirectoryTree(
                            sourceDir, Path.Combine(AppDataFolder, sub), failures);
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{sub}: {ex.Message}");
                        AppLogService.Warning(
                            $"Attachment folder '{sub}' could not be fully restored: {ex.Message}",
                            new LogContext
                            {
                                Module = "Backup",
                                Operation = "Restore"
                            });
                    }
                }

                if (failures.Count > 0 || restored != expected)
                {
                    string detail = string.Join("; ", failures.Take(5));
                    var message =
                        $"Restore INCOMPLETE: copied {restored} of {expected} files. " +
                        "The active root may now contain a mixture of old and restored files. " +
                        $"Keep ProGlass closed and recover from the safety snapshot at '{safety}'.";
                    if (!string.IsNullOrWhiteSpace(detail))
                        message += " Copy errors: " + detail;

                    AppLogService.Error(
                        message, null,
                        new LogContext { Module = "Backup", Operation = "Restore" });
                    return (false, message);
                }

                AppLogService.Info(
                    $"Backup set overlaid: {setDir.Name} " +
                    $"({restored} files copied; other files were left in place)",
                    new LogContext { Module = "Backup", Operation = "Restore" });

                return (true,
                    $"Copied {restored} file(s) from {setDir.Name} into the active data root. " +
                    "Files not present in this backup set were not removed. Restart ProGlass " +
                    "to load the restored files. For a clean full-root restore, close every " +
                    "app instance and replace the whole data-root folder using the Windows procedure. " +
                    verificationNote);
            }
            catch (Exception ex)
            {
                AppLogService.Error("Restore failed", ex,
                    new LogContext { Module = "Backup", Operation = "Restore" });
                return (false, $"Restore failed: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  LEGACY v1 API (preserved)
        // ══════════════════════════════════════════════════════════════

        /// <summary>Legacy single-file restore (proglass.db backups).</summary>
        public static bool RestoreFromBackup(string backupFilePath)
        {
            try
            {
                if (ProGlassAutomation.Services.Database.AppDataRoot.IsDemo)
                    return false;

                var dbPath = AppDbContext.DatabaseFilePath;
                if (!File.Exists(backupFilePath)) return false;

                var safetyBackup = Path.Combine(
                    BackupFolder, "pre_restore_safety.db");

                if (File.Exists(dbPath))
                    File.Copy(dbPath, safetyBackup, overwrite: true);

                File.Copy(backupFilePath, dbPath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                AppLogService.Error("Legacy restore failed", ex,
                    new LogContext { Module = "Backup", Operation = "RestoreLegacy" });
                return false;
            }
        }

        /// <summary>Legacy v1 single-file backups (proglass_*.db).</summary>
        public static FileInfo[] GetAvailableBackups()
        {
            if (!Directory.Exists(BackupFolder))
                return Array.Empty<FileInfo>();

            return new DirectoryInfo(BackupFolder)
                .GetFiles("proglass_*.db")
                .OrderByDescending(f => f.CreationTime)
                .ToArray();
        }

        // ══════════════════════════════════════════════════════════════
        //  RETENTION
        // ══════════════════════════════════════════════════════════════

        private static void CleanupOldSets(string? preserveSetFolder = null)
        {
            try
            {
                string? preserve = string.IsNullOrWhiteSpace(preserveSetFolder)
                    ? null
                    : Path.GetFullPath(preserveSetFolder);
                var sets = GetBackupSets();
                foreach (var old in sets.Skip(MaxBackupSets))
                {
                    if (preserve != null && string.Equals(
                        Path.GetFullPath(old.FolderPath), preserve,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        // A restore may be selecting an older set. The
                        // safety snapshot must not prune the very set being
                        // restored; temporarily exceeding retention is safer.
                        continue;
                    }

                    Directory.Delete(old.FolderPath, recursive: true);
                    AppLogService.Info(
                        $"Old backup set removed: {old.Name}",
                        new LogContext { Module = "Backup", Operation = "Prune" });
                }
            }
            catch (Exception ex)
            {
                // S-5: was a silent catch. A swallowed failure here
                // meant old backup sets accumulated forever and filled the disk unnoticed.
                AppLogService.Warning(
                    $"PruneOldBackups failed: {ex.Message}",
                    new LogContext
                    {
                        Module = "Backup",
                        Operation = "PruneOldBackups"
                    });
            }
        }
    }
}
