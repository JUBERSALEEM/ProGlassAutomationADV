using System;
using System.IO;
using System.Linq;

namespace ProGlassAutomation.Services.Database.DatabaseBackup
{
    /// <summary>
    /// Auto-backup service for the SQLite database.
    /// Copies proglass.db to ProgramData/ProGlassAutomation/Backups folder with timestamp.
    /// Keeps last 7 backups, auto-deletes older ones.
    /// </summary>
    public static class BackupService
    {
        private static readonly string BackupFolder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData),
            "ProGlassAutomation",
            "Backups");


        private const int MAX_BACKUPS = 7;


        /// <summary>
        /// Creates a timestamped backup of the database file.
        /// Called from App.OnExit().
        /// </summary>
        public static void CreateBackup()
        {
            try
            {
                var dbPath = AppDbContext.DatabaseFilePath;


                // Skip if DB doesn't exist yet
                if (!File.Exists(dbPath))
                    return;


                // Ensure backup folder exists
                if (!Directory.Exists(BackupFolder))
                    Directory.CreateDirectory(BackupFolder);


                // Create backup with timestamp
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

                var backupFileName = $"proglass_{timestamp}.db";

                var backupPath = Path.Combine(
                    BackupFolder,
                    backupFileName);


                // Copy the database file
                File.Copy(
                    dbPath,
                    backupPath,
                    overwrite: true);


                // Cleanup old backups (keep only last MAX_BACKUPS)
                CleanupOldBackups();


                System.Diagnostics.Debug.WriteLine(
                    $"[Backup] Created: {backupPath}");
            }
            catch (Exception ex)
            {
                // Silent fail — backup is optional, never crash the app
                System.Diagnostics.Debug.WriteLine(
                    $"[Backup] Error: {ex.Message}");
            }
        }


        /// <summary>
        /// Restores database from a specific backup file.
        /// Returns true if successful.
        /// </summary>
        public static bool RestoreFromBackup(string backupFilePath)
        {
            try
            {
                var dbPath = AppDbContext.DatabaseFilePath;


                if (!File.Exists(backupFilePath))
                    return false;


                // Create a backup of current DB before restoring (safety net)
                var safetyBackup = Path.Combine(
                    BackupFolder,
                    "pre_restore_safety.db");


                if (File.Exists(dbPath))
                {
                    File.Copy(
                        dbPath,
                        safetyBackup,
                        overwrite: true);
                }


                // Restore
                File.Copy(
                    backupFilePath,
                    dbPath,
                    overwrite: true);


                System.Diagnostics.Debug.WriteLine(
                    $"[Backup] Restored from: {backupFilePath}");


                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Backup] Restore error: {ex.Message}");

                return false;
            }
        }


        /// <summary>
        /// Gets list of available backup files, newest first.
        /// </summary>
        public static FileInfo[] GetAvailableBackups()
        {
            if (!Directory.Exists(BackupFolder))
                return Array.Empty<FileInfo>();


            return new DirectoryInfo(BackupFolder)
                .GetFiles("proglass_*.db")
                .OrderByDescending(f => f.CreationTime)
                .ToArray();
        }


        /// <summary>
        /// Deletes backups beyond MAX_BACKUPS count (oldest first).
        /// </summary>
        private static void CleanupOldBackups()
        {
            try
            {
                var backups = GetAvailableBackups();


                if (backups.Length <= MAX_BACKUPS)
                    return;


                // Delete oldest backups beyond the limit
                foreach (var old in backups.Skip(MAX_BACKUPS))
                {
                    old.Delete();

                    System.Diagnostics.Debug.WriteLine(
                        $"[Backup] Deleted old: {old.Name}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Backup] Cleanup error: {ex.Message}");
            }
        }
    }
}