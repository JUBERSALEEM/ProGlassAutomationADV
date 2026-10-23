using Microsoft.Data.Sqlite;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Database.DatabaseBackup;
using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Database
{
    [Collection("HermeticDb")]
    public sealed class BackupManifestTests : IDisposable
    {
        private readonly string _tempFolder;
        private readonly string? _savedFolderOverride;

        public BackupManifestTests()
        {
            _tempFolder = Path.Combine(
                Path.GetTempPath(), "pga_backup_manifest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);
            _savedFolderOverride = AppDataRoot.TestFolderOverride;
            AppDataRoot.TestFolderOverride = _tempFolder;
        }

        public void Dispose()
        {
            AppDataRoot.TestFolderOverride = _savedFolderOverride;
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(_tempFolder, recursive: true); }
            catch { /* best-effort temp cleanup */ }
        }

        [Fact]
        public void Missing_inventoried_attachment_is_not_restorable_and_restore_changes_nothing()
        {
            string database = Path.Combine(_tempFolder, "proglass.db");
            using (var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = database }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText =
                    "CREATE TABLE Marker (Value TEXT NOT NULL);" +
                    "INSERT INTO Marker (Value) VALUES ('live-state');";
                command.ExecuteNonQuery();
            }

            string attachment = Path.Combine(
                _tempFolder, "Inquiries", "1", "Attachments", "quote.pdf");
            Directory.CreateDirectory(Path.GetDirectoryName(attachment)!);
            File.WriteAllBytes(attachment, new byte[] { 1, 2, 3, 4, 5 });
            string sentinel = Path.Combine(_tempFolder, "operator-note.txt");
            File.WriteAllText(sentinel, "leave me alone");

            var created = BackupService.CreateFullBackupSetDetailed("manifest test");
            Assert.True(created.Success, created.Message);
            Assert.True(File.Exists(Path.Combine(created.FolderPath, "backup-manifest.json")));

            Assert.True(BackupService.TryGetBackupSets(out var sets, out _));
            var set = Assert.Single(sets);
            Assert.True(set.IsRestorable);
            Assert.True(set.HasInventoryManifest);
            Assert.Contains("Inventory complete", set.RestoreValidationMessage);

            string backedUpAttachment = Path.Combine(
                created.FolderPath, "Inquiries", "1", "Attachments", "quote.pdf");
            File.Delete(backedUpAttachment);

            Assert.True(BackupService.TryGetBackupSets(out sets, out _));
            set = Assert.Single(sets);
            Assert.False(set.IsRestorable);
            Assert.Contains("missing", set.RestoreValidationMessage,
                StringComparison.OrdinalIgnoreCase);

            var restore = BackupService.RestoreBackupSet(created.FolderPath);
            Assert.False(restore.Success);
            Assert.Contains("No files were changed", restore.Message);
            Assert.Equal("leave me alone", File.ReadAllText(sentinel));
            Assert.True(File.Exists(database));
        }
    }
}
