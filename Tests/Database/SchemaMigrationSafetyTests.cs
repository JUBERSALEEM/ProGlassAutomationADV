using Microsoft.Data.Sqlite;
using ProGlassAutomation.Services.Database.SchemaMigrations;
using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Database
{
    public sealed class SchemaMigrationSafetyTests
    {
        [Fact]
        public void Dropped_legacy_data_is_copied_verified_and_present_in_pre_migration_backup()
        {
            string root = Path.Combine(
                Path.GetTempPath(), "pga_migration_safety_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string dbPath = Path.Combine(root, "preservation.db");

            try
            {
                using (var seed = new SqliteConnection(
                    new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString()))
                {
                    seed.Open();
                    SchemaMigrationSql.Execute(seed,
                        "CREATE TABLE Records (Id INTEGER PRIMARY KEY, LegacyValue TEXT NOT NULL);");
                    SchemaMigrationSql.Execute(seed,
                        "INSERT INTO Records (Id, LegacyValue) VALUES (1, 'customer-data');");
                    SchemaMigrationSql.Execute(seed, "PRAGMA user_version = 0;");
                }

                var migration = new SchemaMigration(
                    1,
                    "Copy legacy value before dropping old column",
                    connection =>
                    {
                        SchemaMigrationSql.AddColumnIfMissing(
                            connection, "Records", "CurrentValue", "TEXT NULL");
                        SchemaMigrationSql.Execute(connection,
                            "UPDATE Records SET CurrentValue = LegacyValue " +
                            "WHERE CurrentValue IS NULL OR TRIM(CurrentValue) = '';");
                        SchemaMigrationSql.RequireNoEmptyValues(
                            connection, "Records", "CurrentValue");
                        using (var compare = connection.CreateCommand())
                        {
                            compare.CommandText =
                                "SELECT COUNT(*) FROM Records " +
                                "WHERE CurrentValue <> LegacyValue;";
                            if (Convert.ToInt64(compare.ExecuteScalar()) != 0)
                                throw new InvalidOperationException(
                                    "LegacyValue was not preserved before drop.");
                        }
                        SchemaMigrationSql.DropColumnIfExists(
                            connection, "Records", "LegacyValue", dataPreservationVerified: true);
                        SchemaMigrationSql.CreateIndexIfNotExists(connection,
                            "CREATE UNIQUE INDEX IF NOT EXISTS IX_Records_Id ON Records(Id);");
                    },
                    connection =>
                    {
                        SchemaMigrationSql.RequireColumn(connection, "Records", "CurrentValue");
                        SchemaMigrationSql.RequireNoEmptyValues(
                            connection, "Records", "CurrentValue");
                        SchemaMigrationSql.RequireIndex(connection, "IX_Records_Id",
                            "Records", unique: true, expectedColumns: new[] { "Id" });
                    });

                Assert.True(SchemaMigrationRunner.Upgrade(
                    dbPath, new[] { migration }, "MigrationSafetyTest"));

                using (var migrated = new SqliteConnection(
                    new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString()))
                {
                    migrated.Open();
                    using var read = migrated.CreateCommand();
                    read.CommandText = "SELECT CurrentValue FROM Records WHERE Id = 1;";
                    Assert.Equal("customer-data", read.ExecuteScalar());
                    Assert.Equal(1, ReadUserVersion(migrated));
                }

                string backup = Assert.Single(Directory.GetFiles(
                    Path.Combine(root, "MigrationBackups"),
                    "preservation_pre_v1_*.db"));
                using var saved = new SqliteConnection(
                    new SqliteConnectionStringBuilder { DataSource = backup }.ToString());
                saved.Open();
                using var old = saved.CreateCommand();
                old.CommandText = "SELECT LegacyValue FROM Records WHERE Id = 1;";
                Assert.Equal("customer-data", old.ExecuteScalar());
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                try { Directory.Delete(root, recursive: true); }
                catch { /* best-effort temp cleanup */ }
            }
        }

        [Fact]
        public void Unique_index_duplicate_error_is_not_swallowed_or_data_rewritten()
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            SchemaMigrationSql.Execute(connection,
                "CREATE TABLE ExistingRows (Id INTEGER PRIMARY KEY, NaturalKey TEXT NOT NULL);");
            SchemaMigrationSql.Execute(connection,
                "INSERT INTO ExistingRows (NaturalKey) VALUES ('duplicate'), ('duplicate');");

            Assert.Throws<InvalidOperationException>(() =>
                SchemaMigrationSql.CreateIndexIfNotExists(connection,
                    "CREATE UNIQUE INDEX IF NOT EXISTS IX_ExistingRows_NaturalKey " +
                    "ON ExistingRows(NaturalKey);"));

            using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM ExistingRows;";
            Assert.Equal(2L, count.ExecuteScalar());
            using var index = connection.CreateCommand();
            index.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type='index' " +
                "AND name='IX_ExistingRows_NaturalKey';";
            Assert.Equal(0L, index.ExecuteScalar());
        }

        private static int ReadUserVersion(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }
}
