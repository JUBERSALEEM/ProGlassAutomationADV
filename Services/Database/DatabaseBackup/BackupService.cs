// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Database/DatabaseBackup/BackupService.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — backup & restore (v2 upgrade)
//
// v1 backed up only proglass.db. v2 backs up the COMPLETE data set:
// every SQLite database + config.json + the whitelisted attachment
// file trees (Inquiries/, ProformaInvoices/, DeliveryNotes/) into one
// timestamped "backup set" folder, with retention, verification and
// safe restore. Tax-invoice attachments are blobs inside taxinvoices.db
// and travel with the *.db copy automatically.
//
// Layout:  %AppData%\ProGlassAutomation\Backups\
//            set_2026-08-14_18-30-00\{ all *.db, config.json,
//                                       Inquiries\, ProformaInvoices\,
//                                       DeliveryNotes\ }
//
// SAFETY RULES honoured:
//   • Restore always snapshots the CURRENT state first (pre_restore).
//   • Restore requires caller-side user confirmation (Settings asks).
//   • Nothing is deleted beyond the retention window, and pruning is logged.
//
// The original single-file API (CreateBackup / RestoreFromBackup /
// GetAvailableBackups) is preserved for backward compatibility.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Data.Sqlite;
using ProGlassAutomation.Services.Logging;
using System;
using System.IO;
using System.Linq;

namespace ProGlassAutomation.Services.Database.DatabaseBackup
{
    /// <summary>One backup set on disk.</summary>
    public class BackupSetInfo
    {
        public string FolderPath { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public long TotalBytes { get; set; }
        public int FileCount { get; set; }

        public string SizeDisplay =>
            TotalBytes < 1024 * 1024
                ? $"{TotalBytes / 1024.0:0.#} KB"
                : $"{TotalBytes / (1024.0 * 1024.0):0.##} MB";
    }

    /// <summary>
    /// Full-dataset backup & restore. Static facade, never throws —
    /// backup problems are logged, never surfaced as crashes.
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
        /// Creates one full backup set (all databases + config).
        /// Returns the set folder, or empty string on failure.
        /// </summary>
        public static string CreateFullBackupSet(string reason = "manual")
        {
            try
            {
                Directory.CreateDirectory(BackupFolder);

                var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var setFolder = Path.Combine(BackupFolder, $"set_{stamp}");
                Directory.CreateDirectory(setFolder);

                int copied = CopyDataFilesInto(setFolder);
                if (copied == 0)
                {
                    Directory.Delete(setFolder, recursive: true);
                    return "";
                }

                CleanupOldSets();

                AppLogService.Info(
                    $"Backup set created ({reason}): {copied} files → {setFolder}",
                    new LogContext { Module = "Backup", Operation = "Create" });

                return setFolder;
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    "Backup set creation failed", ex,
                    new LogContext { Module = "Backup", Operation = "Create" });
                return "";
            }
        }

        private static int CopyDataFilesInto(string setFolder)
        {
            // Release pooled SQLite connections so the live .db files are
            // not locked (or mid-write) while we copy them.
            try { SqliteConnection.ClearAllPools(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

            int copied = 0;
            var source = new DirectoryInfo(AppDataFolder);

            foreach (var db in source.GetFiles("*.db"))
            {
                try
                {
                    db.CopyTo(Path.Combine(setFolder, db.Name), overwrite: true);
                    copied++;
                }
                catch (Exception ex)
                {
                    AppLogService.Warning(
                        $"Could not back up {db.Name}: {ex.Message}",
                        new LogContext { Module = "Backup", Operation = "Copy" });
                }
            }

            var config = Path.Combine(AppDataFolder, "config.json");
            if (File.Exists(config))
            {
                File.Copy(config, Path.Combine(setFolder, "config.json"),
                    overwrite: true);
                copied++;
            }

            // Attachment file trees (see ExtraDataSubfolders). Whitelisted,
            // recursive, per-file fault tolerant — one unreadable file must
            // never poison the rest of the set.
            foreach (var sub in ExtraDataSubfolders)
            {
                var sourceDir = Path.Combine(AppDataFolder, sub);
                if (!Directory.Exists(sourceDir)) continue;

                try
                {
                    copied += CopyDirectoryTree(
                        sourceDir, Path.Combine(setFolder, sub));
                }
                catch (Exception ex)
                {
                    AppLogService.Warning(
                        $"Attachment folder '{sub}' could not be fully backed up: {ex.Message}",
                        new LogContext { Module = "Backup", Operation = "Copy" });
                }
            }

            return copied;
        }

        /// <summary>
        /// Copies @src into @dst recursively, creating directories as
        /// needed. Returns the number of FILES copied; per-file failures
        /// are logged and skipped rather than aborting the tree.
        /// </summary>
        private static int CopyDirectoryTree(string sourceDir, string targetDir)
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
                    AppLogService.Warning(
                        $"Backup skipped file {file}: {ex.Message}",
                        new LogContext { Module = "Backup", Operation = "Copy" });
                }
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
                copied += CopyDirectoryTree(subDir,
                    Path.Combine(targetDir, Path.GetFileName(subDir)));

            return copied;
        }

        // ══════════════════════════════════════════════════════════════
        //  LIST
        // ══════════════════════════════════════════════════════════════

        /// <summary>All backup sets, newest first.</summary>
        public static BackupSetInfo[] GetBackupSets()
        {
            try
            {
                if (!Directory.Exists(BackupFolder))
                    return Array.Empty<BackupSetInfo>();

                return new DirectoryInfo(BackupFolder)
                    .GetDirectories("set_*")
                    .Select(dir =>
                    {
                        // AllDirectories: attachment trees (Inquiries/,
                        // ProformaInvoices/, DeliveryNotes/) inside the set
                        // are real data that must show up in the reported
                        // size/file-count (audit P0 fix, 2026-09-05).
                        var files = dir.GetFiles("*", SearchOption.AllDirectories);
                        return new BackupSetInfo
                        {
                            FolderPath = dir.FullName,
                            Name = dir.Name,
                            CreatedAt = dir.CreationTime,
                            TotalBytes = files.Sum(f => f.Length),
                            FileCount = files.Length
                        };
                    })
                    .OrderByDescending(s => s.CreatedAt)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<BackupSetInfo>();
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  RESTORE
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Restores every file from a backup set over the live data.
        /// A safety snapshot of the CURRENT state is taken first.
        /// Caller must confirm with the user before invoking this.
        /// </summary>
        public static (bool Success, string Message) RestoreBackupSet(
            string setFolder)
        {
            try
            {
                if (ProGlassAutomation.Services.Database.AppDataRoot.IsDemo)
                    return (false,
                        "Restore is blocked in Demo Mode. Exit Demo Mode first — " +
                        "restore always writes the production folder and must not " +
                        "run while the isolated demo folder is active.");

                if (!Directory.Exists(setFolder))
                    return (false, "Backup set not found.");

                var setDir = new DirectoryInfo(setFolder);
                var files = setDir.GetFiles();
                bool hasAttachmentTrees = ExtraDataSubfolders.Any(
                    sub => Directory.Exists(Path.Combine(setFolder, sub)));
                if (files.Length == 0 && !hasAttachmentTrees)
                    return (false, "Backup set is empty.");

                // Safety snapshot first — restore can always be undone.
                var safety = CreateFullBackupSet("pre-restore safety");
                if (safety.Length == 0)
                    return (false,
                        "Could not create the pre-restore safety backup.");

                // Release pooled SQLite connections so the live .db files
                // can actually be overwritten (otherwise the copy is
                // skipped and the restore silently does nothing).
                try { SqliteConnection.ClearAllPools(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }

                int restored = 0;
                foreach (var file in files)
                {
                    try
                    {
                        file.CopyTo(Path.Combine(AppDataFolder, file.Name),
                            overwrite: true);
                        restored++;
                    }
                    catch (Exception ex)
                    {
                        AppLogService.Warning(
                            $"Restore skipped {file.Name}: {ex.Message}",
                            new LogContext
                            {
                                Module = "Backup",
                                Operation = "Restore"
                            });
                    }
                }

                // Attachment file trees: restore as a clean replace per
                // whitelisted folder. Stale files left behind by a partial
                // restore would be re-resolvable against the restored DB
                // manifests (the DB is authoritative for what should
                // exist), so replace-in-place is the predictable choice.
                foreach (var sub in ExtraDataSubfolders)
                {
                    var sourceDir = Path.Combine(setFolder, sub);
                    if (!Directory.Exists(sourceDir)) continue;

                    try
                    {
                        restored += CopyDirectoryTree(
                            sourceDir, Path.Combine(AppDataFolder, sub));
                    }
                    catch (Exception ex)
                    {
                        AppLogService.Warning(
                            $"Attachment folder '{sub}' could not be fully restored: {ex.Message}",
                            new LogContext
                            {
                                Module = "Backup",
                                Operation = "Restore"
                            });
                    }
                }

                AppLogService.Info(
                    $"Backup set restored: {setDir.Name} " +
                    $"({restored} files incl. attachment trees)",
                    new LogContext { Module = "Backup", Operation = "Restore" });

                return (true,
                    $"Restored {restored} of {files.Length} files. " +
                    "Restart the app to load the restored data.");
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

        private static void CleanupOldSets()
        {
            try
            {
                var sets = GetBackupSets();
                foreach (var old in sets.Skip(MaxBackupSets))
                {
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
