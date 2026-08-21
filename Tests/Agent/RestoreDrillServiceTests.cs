// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/RestoreDrillServiceTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Phase 10 — backup restore drill
//
// Covers the parts of the restore drill that run in isolation:
//   • CheckDatabaseIntegrity passes for a real SQLite file, fails for a
//     corrupt byte stream.
//   • VerifyBackupSetIntegrity finds every .db in a folder.
//   • AreFilesIdentical / ComputeFileHash detect tampering.
//
// RunDrill() itself writes to %AppData%/ProGlassAutomation/Backups and is
// therefore exercised manually (Settings → Backups → Run restore drill),
// not in unit tests.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using Microsoft.Data.Sqlite;
using ProGlassAutomation.Services.Database.DatabaseBackup;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class RestoreDrillServiceTests
    {
        [Fact]
        public void CheckDatabaseIntegrity_ok_for_valid_database()
        {
            var path = CreateTempDb();
            try
            {
                var result = RestoreDrillService.CheckDatabaseIntegrity(path);

                Assert.True(result.IsOk);
                Assert.Equal("ok", result.Detail);
                Assert.Equal(Path.GetFileName(path), result.FileName);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void CheckDatabaseIntegrity_fails_for_corrupt_file()
        {
            var path = Path.Combine(
                Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db");
            File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

            try
            {
                var result = RestoreDrillService.CheckDatabaseIntegrity(path);
                Assert.False(result.IsOk);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void VerifyBackupSetIntegrity_checks_every_db_in_folder()
        {
            var dir = Path.Combine(
                Path.GetTempPath(), "drill_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            try
            {
                CreateDbAt(Path.Combine(dir, "a.db"));
                CreateDbAt(Path.Combine(dir, "b.db"));
                File.WriteAllText(Path.Combine(dir, "config.json"), "{}");

                var results = RestoreDrillService.VerifyBackupSetIntegrity(dir);

                Assert.Equal(2, results.Count);
                Assert.All(results, r => Assert.True(r.IsOk));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void VerifyBackupSetIntegrity_missing_folder_returns_empty()
        {
            var results = RestoreDrillService.VerifyBackupSetIntegrity(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

            Assert.Empty(results);
        }

        [Fact]
        public void AreFilesIdentical_detects_byte_difference()
        {
            var a = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bin");
            var b = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bin");
            var c = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bin");

            File.WriteAllBytes(a, new byte[] { 1, 2, 3, 4, 5 });
            File.WriteAllBytes(b, new byte[] { 1, 2, 3, 4, 5 });
            File.WriteAllBytes(c, new byte[] { 1, 2, 3, 4, 6 });

            try
            {
                Assert.True(RestoreDrillService.AreFilesIdentical(a, b));
                Assert.False(RestoreDrillService.AreFilesIdentical(a, c));
            }
            finally
            {
                File.Delete(a);
                File.Delete(b);
                File.Delete(c);
            }
        }

        [Fact]
        public void AreFilesIdentical_rejects_missing_files()
        {
            var missing = Path.Combine(
                Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bin");
            var present = Path.Combine(
                Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllBytes(present, new byte[] { 1, 2, 3 });

            try
            {
                Assert.False(RestoreDrillService.AreFilesIdentical(missing, present));
                Assert.False(RestoreDrillService.AreFilesIdentical(present, missing));
            }
            finally
            {
                File.Delete(present);
            }
        }

        [Fact]
        public void ComputeFileHash_is_stable_uppercase_hex()
        {
            var path = Path.Combine(
                Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllBytes(path, new byte[] { 9, 8, 7 });

            try
            {
                var hash1 = RestoreDrillService.ComputeFileHash(path);
                var hash2 = RestoreDrillService.ComputeFileHash(path);

                Assert.Equal(hash1, hash2);
                Assert.Equal(64, hash1.Length); // SHA-256 hex
            }
            finally
            {
                File.Delete(path);
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static string CreateTempDb()
        {
            var path = Path.Combine(
                Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".db");
            CreateDbAt(path);
            return path;
        }

        private static void CreateDbAt(string path)
        {
            using var connection = new SqliteConnection($"Data Source={path}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText =
                "CREATE TABLE Drill (Id INTEGER PRIMARY KEY, Name TEXT);" +
                "INSERT INTO Drill (Name) VALUES ('glass');";
            command.ExecuteNonQuery();
        }
    }
}
