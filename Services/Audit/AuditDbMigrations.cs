// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Audit/AuditDbMigrations.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — additive audit.db migrations
//
// audit.db already exists on user machines, and EF's EnsureCreated does
// NOT add new tables/columns to an existing database. This migrator
// applies schema changes with hand-written, idempotent SQL:
//
//   • PRAGMA check → ALTER TABLE ADD COLUMN only when missing
//   • CREATE TABLE IF NOT EXISTS for new tables
//
// Every migration is additive: existing rows are never touched or lost.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Data.Sqlite;
using ProGlassAutomation.Services.Database.SchemaMigrations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.Audit
{
    /// <summary>Idempotent schema upgrades for audit.db.</summary>
    public static class AuditDbMigrations
    {
        public static bool IsReady { get; private set; }

        /// <summary>Applies all pending migrations. Errors fail closed and are returned.</summary>
        public static bool Apply()
        {
            IsReady = false;
            try
            {
                using var connection = Open();

                AddColumnIfMissing(connection,
                    "ActivityLog", "SessionId", "TEXT NOT NULL DEFAULT ''");

                AddColumnIfMissing(connection,
                    "ActivityLog", "ChangesJson", "TEXT NOT NULL DEFAULT ''");

                // PHASE D — optional WHY note on every audit entry.
                AddColumnIfMissing(connection,
                    "ActivityLog", "Reason", "TEXT NOT NULL DEFAULT ''");

                // Professional audit context — who/where/version/operation.
                AddColumnIfMissing(connection,
                    "ActivityLog", "UserDisplayName", "TEXT NOT NULL DEFAULT ''");
                AddColumnIfMissing(connection,
                    "ActivityLog", "UserRole", "TEXT NOT NULL DEFAULT ''");
                AddColumnIfMissing(connection,
                    "ActivityLog", "MachineName", "TEXT NOT NULL DEFAULT ''");
                AddColumnIfMissing(connection,
                    "ActivityLog", "AppVersion", "TEXT NOT NULL DEFAULT ''");
                AddColumnIfMissing(connection,
                    "ActivityLog", "LocalTimeZone", "TEXT NOT NULL DEFAULT ''");
                AddColumnIfMissing(connection,
                    "ActivityLog", "OperationId", "TEXT NOT NULL DEFAULT ''");

                Execute(connection, @"
                    CREATE INDEX IF NOT EXISTS IX_ActivityLog_Entity
                    ON ActivityLog (EntityType, EntityId);");

                Execute(connection, @"
                    CREATE INDEX IF NOT EXISTS IX_ActivityLog_OperationId
                    ON ActivityLog (OperationId);");

                // PHASE 13 — persisted login lockout state on users.
                // These columns are additive so existing installs keep their
                // users; a fresh DB gets them from the EF model on EnsureCreated.
                AddColumnIfMissing(connection,
                    "AppUsers", "FailedAttemptCount", "INTEGER NOT NULL DEFAULT 0");

                AddColumnIfMissing(connection,
                    "AppUsers", "LockedUntilUtc", "TEXT NULL");

                // PHASE 17 — Sharing Center FROM identity. Optional contact
                // fields on users; existing installs keep their users with
                // blank values, and the composer simply shows name + role.
                AddColumnIfMissing(connection,
                    "AppUsers", "Mobile", "TEXT NOT NULL DEFAULT ''");

                AddColumnIfMissing(connection,
                    "AppUsers", "Email", "TEXT NOT NULL DEFAULT ''");

                // AUTH-001 — materialized canonical username key. Backfill in
                // .NET so normalization has exactly the same Unicode and case
                // semantics as login. A collision aborts readiness rather than
                // choosing an account or allowing ambiguous sign-in.
                AddColumnIfMissing(connection,
                    "AppUsers", "UsernameNormalized", "TEXT NOT NULL DEFAULT ''");
                BackfillNormalizedUsernames(connection);
                SchemaMigrationSql.RequireNoDuplicateKeyValues(
                    connection, "AppUsers", "UsernameNormalized");
                Execute(connection, @"
                    CREATE UNIQUE INDEX IF NOT EXISTS IX_AppUsers_UsernameNormalized
                    ON AppUsers (UsernameNormalized);");

                // Promote the legacy spelling in stored rows. RolePermissions
                // still accepts "Admin" during recovery from older backups.
                Execute(connection, @"
                    UPDATE AppUsers
                    SET Role = 'Administrator'
                    WHERE lower(trim(Role)) = 'admin';");

                Execute(connection, @"
                    CREATE TABLE IF NOT EXISTS RecordMetadata (
                        EntityType     TEXT NOT NULL,
                        EntityId       TEXT NOT NULL,
                        CreatedAtUtc   TEXT NOT NULL,
                        CreatedByUser  TEXT NOT NULL,
                        CreatedSession TEXT NOT NULL,
                        UpdatedAtUtc   TEXT NOT NULL,
                        UpdatedByUser  TEXT NOT NULL,
                        UpdatedSession TEXT NOT NULL,
                        UpdateCount    INTEGER NOT NULL DEFAULT 0,
                        PRIMARY KEY (EntityType, EntityId)
                    );");

                Execute(connection, @"
                    CREATE TABLE IF NOT EXISTS RecordVersions (
                        Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                        EntityType   TEXT NOT NULL,
                        EntityId     TEXT NOT NULL,
                        VersionNo    INTEGER NOT NULL,
                        TimestampUtc TEXT NOT NULL,
                        Username     TEXT NOT NULL,
                        SessionId    TEXT NOT NULL,
                        Summary      TEXT NOT NULL DEFAULT '',
                        SnapshotJson TEXT NOT NULL DEFAULT ''
                    );");

                Execute(connection, @"
                    CREATE INDEX IF NOT EXISTS IX_RecordVersions_Lookup
                    ON RecordVersions (EntityType, EntityId, VersionNo);");

                VerifyRequiredSchema(connection);
                IsReady = true;
                return true;
            }
            catch (Exception ex)
            {
                // R-1 RECURRENCE: Debug.WriteLine is [Conditional("DEBUG")]
                // and is stripped in Release, so a failed schema upgrade was
                // invisible in shipped builds. That matters here more than
                // most places: if these additive migrations do not apply,
                // audit writes that reference the new columns/tables fail
                // later, and the audit trail silently stops recording -
                // exactly the evidence needed to investigate an incident.
                Logging.AppLogService.Error(
                    "audit.db schema upgrade failed. Change history and "
                    + "record versioning may not be recorded.",
                    ex,
                    new Logging.LogContext
                    {
                        Module = "Audit",
                        Class = nameof(AuditDbMigrations),
                        Operation = "Apply"
                    });
                return false;
            }
        }

        private static void VerifyRequiredSchema(SqliteConnection connection)
        {
            foreach (string column in new[]
            {
                "SessionId", "ChangesJson", "Reason", "UserDisplayName", "UserRole",
                "MachineName", "AppVersion", "LocalTimeZone", "OperationId"
            })
                SchemaMigrationSql.RequireColumn(connection, "ActivityLog", column);

            foreach (string column in new[]
            {
                "Username", "UsernameNormalized", "Role", "IsActive",
                "FailedAttemptCount", "LockedUntilUtc", "Mobile", "Email"
            })
                SchemaMigrationSql.RequireColumn(connection, "AppUsers", column);

            foreach (string column in new[]
            {
                "EntityType", "EntityId", "VersionNo", "TimestampUtc",
                "Username", "SessionId", "Summary", "SnapshotJson"
            })
                SchemaMigrationSql.RequireColumn(connection, "RecordVersions", column);

            foreach (string column in new[]
            {
                "EntityType", "EntityId", "CreatedAtUtc", "CreatedByUser",
                "CreatedSession", "UpdatedAtUtc", "UpdatedByUser",
                "UpdatedSession", "UpdateCount"
            })
                SchemaMigrationSql.RequireColumn(connection, "RecordMetadata", column);

            SchemaMigrationSql.RequireIndex(connection, "IX_ActivityLog_Entity",
                "ActivityLog", unique: false,
                expectedColumns: new[] { "EntityType", "EntityId" });
            SchemaMigrationSql.RequireIndex(connection, "IX_ActivityLog_OperationId",
                "ActivityLog", unique: false,
                expectedColumns: new[] { "OperationId" });
            SchemaMigrationSql.RequireIndex(connection, "IX_AppUsers_UsernameNormalized",
                "AppUsers", unique: true,
                expectedColumns: new[] { "UsernameNormalized" });
            SchemaMigrationSql.RequireIndex(connection, "IX_RecordVersions_Lookup",
                "RecordVersions", unique: false,
                expectedColumns: new[] { "EntityType", "EntityId", "VersionNo" });

            SchemaMigrationSql.RequireNoNullValues(
                connection, "AppUsers", "UsernameNormalized");
            SchemaMigrationSql.RequireNoEmptyValues(
                connection, "AppUsers", "UsernameNormalized");
            SchemaMigrationSql.RequireNoDuplicateKeyValues(
                connection, "AppUsers", "UsernameNormalized");
            SchemaMigrationSql.RequireNoNullValues(
                connection, "AppUsers", "FailedAttemptCount");
            SchemaMigrationSql.RequireNoNullValues(
                connection, "RecordVersions", "VersionNo");
        }

        private static void BackfillNormalizedUsernames(SqliteConnection connection)
        {
            var rows = new List<(int Id, string Username, string Normalized)>();
            using (var select = connection.CreateCommand())
            {
                select.CommandText = "SELECT Id, Username FROM AppUsers ORDER BY Id;";
                using var reader = select.ExecuteReader();
                while (reader.Read())
                {
                    string username = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    rows.Add((reader.GetInt32(0), username,
                        UsernameNormalizer.Normalize(username)));
                }
            }

            var duplicate = rows
                .GroupBy(row => row.Normalized, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
            {
                string conflicting = string.Join(", ", duplicate.Select(row =>
                    $"'{row.Username}' (user #{row.Id})"));
                throw new InvalidOperationException(
                    "Username normalization found a collision: " + conflicting +
                    ". Resolve the duplicate accounts before sign-in; no account was selected.");
            }

            foreach (var row in rows)
            {
                using var update = connection.CreateCommand();
                update.CommandText =
                    "UPDATE AppUsers SET UsernameNormalized = $normalized WHERE Id = $id;";
                update.Parameters.AddWithValue("$normalized", row.Normalized);
                update.Parameters.AddWithValue("$id", row.Id);
                update.ExecuteNonQuery();
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────

        internal static SqliteConnection Open()
        {
            var connection = new SqliteConnection(
                ProGlassAutomation.Services.Database.SqliteResilience
                    .BuildConnectionString(AuditDbContext.DatabaseFilePath));
            try
            {
                connection.Open();
                ProGlassAutomation.Services.Database.SqliteResilience
                    .ApplyConnectionPolicy(connection);
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        private static void AddColumnIfMissing(
            SqliteConnection connection,
            string table, string column, string definition)
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = $"PRAGMA table_info({table});";
                using var reader = pragma.ExecuteReader();
                while (reader.Read())
                    existing.Add(reader.GetString(1));
            }

            if (existing.Count > 0 && !existing.Contains(column))
            {
                Execute(connection,
                    $"ALTER TABLE {table} ADD COLUMN {column} {definition};");
            }
        }

        internal static void Execute(SqliteConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }
}
