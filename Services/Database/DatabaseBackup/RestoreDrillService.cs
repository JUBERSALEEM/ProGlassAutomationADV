// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Database/DatabaseBackup/RestoreDrillService.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — backup restore drill (Phase 10)
// VERSION: Phase 10 — 2026-08-21
//
// PURPOSE (backup drill):
//   A backup you have never restored is a backup you do not have. The
//   drill PROVES a backup set can actually be read back — without ever
//   touching the live data:
//
//     1. Create a full backup set (all SQLite DBs + config.json).
//     2. Copy it into an isolated scratch folder (the "restore").
//     3. Verify every copied file is byte-identical (size + SHA-256).
//     4. Open every copied SQLite file read-only and run
//        PRAGMA integrity_check — a corrupt backup fails here, loudly,
//        BEFORE you ever need it.
//     5. Clean up the scratch folder and return a report.
//
//   Because the drill runs against the scratch folder only, it is safe
//   to run on any size database (the "1 GB DB" acceptance case included)
//   and can be scheduled or run manually from Settings.
//
// USAGE:
//   var result = RestoreDrillService.RunDrill();
//   if (!result.IsSuccess) → show result.Error with correlation
//   else → show result.Value (files verified, DBs integrity-checked)
//
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Data.Sqlite;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace ProGlassAutomation.Services.Database.DatabaseBackup
{
    /// <summary>Result of one SQLite integrity check on a backup file.</summary>
    public sealed class DbIntegrityResult
    {
        public string FileName { get; set; } = "";
        public bool IsOk { get; set; }
        public string Detail { get; set; } = "";
    }

    /// <summary>Summary of one restore drill run.</summary>
    public sealed class RestoreDrillReport
    {
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string BackupSetPath { get; set; } = "";
        public string DrillFolder { get; set; } = "";
        public int FilesCopied { get; set; }
        public int FilesVerified { get; set; }
        public int FilesFailed { get; set; }
        public int DatabasesChecked { get; set; }
        public int DatabasesOk { get; set; }
        public List<DbIntegrityResult> IntegrityResults { get; set; } = new();

        /// <summary>True when every copied file matched and every DB passed.</summary>
        public bool IsClean =>
            FilesCopied > 0 &&
            FilesFailed == 0 &&
            DatabasesChecked > 0 &&
            DatabasesOk == DatabasesChecked;
    }

    /// <summary>
    /// Runs the backup restore drill. Never throws, never touches live
    /// data — failures return a failed <see cref="Result{T}"/> with a
    /// correlation id.
    /// </summary>
    public static class RestoreDrillService
    {
        /// <summary>
        /// Runs a full drill: backup → isolated restore → verify → integrity
        /// check → cleanup. Safe on live systems (writes only to a scratch
        /// folder plus one new backup set).
        /// </summary>
        public static Result<RestoreDrillReport> RunDrill(
            string? correlationId = null)
        {
            var corrId = correlationId ?? CorrelationContext.NewId();
            using var scope = CorrelationContext.BeginScope(corrId);

            var report = new RestoreDrillReport();

            AppLogService.Info(
                "Restore drill started",
                new LogContext
                {
                    Module = "Backup",
                    Operation = "RestoreDrill",
                    CorrelationId = corrId
                });

            // 1. Create the backup set we are going to prove we can restore.
            var setFolder = BackupService.CreateFullBackupSet("restore drill");
            if (string.IsNullOrEmpty(setFolder))
            {
                var err = "Restore drill aborted: could not create a backup set.";
                AppLogService.Warning(err,
                    new LogContext
                    {
                        Module = "Backup",
                        Operation = "RestoreDrill",
                        CorrelationId = corrId
                    });
                return Result<RestoreDrillReport>.Fail(err, corrId);
            }

            report.BackupSetPath = setFolder;
            var files = new DirectoryInfo(setFolder).GetFiles();
            if (files.Length == 0)
            {
                var err = "Restore drill aborted: the backup set is empty.";
                AppLogService.Warning(err,
                    new LogContext
                    {
                        Module = "Backup",
                        Operation = "RestoreDrill",
                        CorrelationId = corrId
                    });
                return Result<RestoreDrillReport>.Fail(err, corrId);
            }

            // 2. Restore into an isolated scratch folder.
            var scratch = Path.Combine(
                BackupService.GetBackupsFolder(),
                "_drill_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" +
                Guid.NewGuid().ToString("N")[..6]);
            report.DrillFolder = scratch;

            try
            {
                Directory.CreateDirectory(scratch);

                foreach (var file in files)
                {
                    var copyPath = Path.Combine(scratch, file.Name);
                    file.CopyTo(copyPath, overwrite: true);
                    report.FilesCopied++;

                    // 3. Verify byte-identity of the "restored" copy.
                    if (AreFilesIdentical(file.FullName, copyPath))
                        report.FilesVerified++;
                    else
                        report.FilesFailed++;
                }

                // 4. SQLite integrity check on every restored database.
                var integrity = VerifyBackupSetIntegrity(scratch);
                report.IntegrityResults = integrity;
                report.DatabasesChecked = integrity.Count;
                report.DatabasesOk = integrity.FindAll(i => i.IsOk).Count;

                AppLogService.Info(
                    $"Restore drill finished: {report.FilesVerified}/{report.FilesCopied} " +
                    $"files verified, {report.DatabasesOk}/{report.DatabasesChecked} DBs OK",
                    new LogContext
                    {
                        Module = "Backup",
                        Operation = "RestoreDrill",
                        CorrelationId = corrId,
                        Extra = $"Set={report.BackupSetPath}"
                    });

                return Result<RestoreDrillReport>.Ok(report, corrId);
            }
            catch (Exception ex)
            {
                var errId = AppLogService.Error(
                    "Restore drill failed", ex,
                    new LogContext
                    {
                        Module = "Backup",
                        Operation = "RestoreDrill",
                        CorrelationId = corrId
                    });
                return Result<RestoreDrillReport>.Fail(
                    $"Restore drill failed: {ex.Message}", corrId, ex, errId);
            }
            finally
            {
                // 5. Cleanup — the scratch folder is throwaway by design.
                try
                {
                    if (Directory.Exists(scratch))
                        Directory.Delete(scratch, recursive: true);
                }
                catch (Exception ex)
                {
                    AppLogService.Warning(
                        $"Could not remove drill scratch folder: {ex.Message}",
                        new LogContext
                        {
                            Module = "Backup",
                            Operation = "RestoreDrill",
                            CorrelationId = corrId
                        });
                }
            }
        }

        /// <summary>
        /// Opens every .db file in a folder read-only and runs
        /// PRAGMA integrity_check. Returns one result per database.
        /// Pure with respect to the live data — reads the given folder only.
        /// </summary>
        public static List<DbIntegrityResult> VerifyBackupSetIntegrity(
            string folder)
        {
            var results = new List<DbIntegrityResult>();

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return results;

            foreach (var db in new DirectoryInfo(folder).GetFiles("*.db"))
            {
                results.Add(CheckDatabaseIntegrity(db.FullName));
            }

            return results;
        }

        /// <summary>Runs PRAGMA integrity_check on one SQLite file.</summary>
        public static DbIntegrityResult CheckDatabaseIntegrity(string dbPath)
        {
            var result = new DbIntegrityResult
            {
                FileName = Path.GetFileName(dbPath)
            };

            try
            {
                var builder = new SqliteConnectionStringBuilder
                {
                    DataSource = dbPath,
                    Mode = SqliteOpenMode.ReadOnly
                };

                using var connection = new SqliteConnection(builder.ToString());
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA integrity_check;";
                var raw = command.ExecuteScalar() as string;

                result.IsOk = string.Equals(
                    raw?.Trim(), "ok", StringComparison.OrdinalIgnoreCase);
                result.Detail = raw ?? "";
            }
            catch (Exception ex)
            {
                result.IsOk = false;
                result.Detail = ex.Message;
            }

            return result;
        }

        /// <summary>SHA-256 of a file as an uppercase hex string.</summary>
        public static string ComputeFileHash(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream));
        }

        /// <summary>
        /// True when two files are the same length and have the same hash.
        /// </summary>
        public static bool AreFilesIdentical(string source, string copy)
        {
            if (!File.Exists(source) || !File.Exists(copy))
                return false;

            var sourceInfo = new FileInfo(source);
            var copyInfo = new FileInfo(copy);

            if (sourceInfo.Length != copyInfo.Length)
                return false;

            return string.Equals(
                ComputeFileHash(source),
                ComputeFileHash(copy),
                StringComparison.Ordinal);
        }
    }
}
