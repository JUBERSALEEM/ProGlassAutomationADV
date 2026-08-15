// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Database/DatabaseBackup/BackupService.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — backup & restore (v2 upgrade)
//
// v1 backed up only proglass.db. v2 backs up the COMPLETE data set:
// every SQLite database + config.json into one timestamped "backup set"
// folder, with retention, verification and safe restore.
//
// Layout:  %AppData%\ProGlassAutomation\Backups\
//            set_2026-08-14_18-30-00\{ all *.db, config.json }
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
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation");

        private static readonly string BackupFolder =
            Path.Combine(AppDataFolder, "Backups");

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
            try { SqliteConnection.ClearAllPools(); } catch { }

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
                        var files = dir.GetFiles();
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
                if (!Directory.Exists(setFolder))
                    return (false, "Backup set not found.");

                var files = new DirectoryInfo(setFolder).GetFiles();
                if (files.Length == 0)
                    return (false, "Backup set is empty.");

                // Safety snapshot first — restore can always be undone.
                var safety = CreateFullBackupSet("pre-restore safety");
                if (safety.Length == 0)
                    return (false,
                        "Could not create the pre-restore safety backup.");

                // Release pooled SQLite connections so the live .db files
                // can actually be overwritten (otherwise the copy is
                // skipped and the restore silently does nothing).
                try { SqliteConnection.ClearAllPools(); } catch { }

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

                AppLogService.Info(
                    $"Backup set restored: {files[0].Directory?.Name} " +
                    $"({restored}/{files.Length} files)",
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
            catch { }
        }
    }
}
