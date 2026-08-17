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
using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Services.Audit
{
    /// <summary>Idempotent schema upgrades for audit.db.</summary>
    public static class AuditDbMigrations
    {
        /// <summary>Applies all pending migrations. Never throws.</summary>
        public static void Apply()
        {
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
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────

        internal static SqliteConnection Open()
        {
            var connection = new SqliteConnection(
                $"Data Source={AuditDbContext.DatabaseFilePath}");
            connection.Open();
            return connection;
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
