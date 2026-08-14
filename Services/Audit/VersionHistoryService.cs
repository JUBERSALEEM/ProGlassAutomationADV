// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Audit/VersionHistoryService.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — entity version history (§24)
//
// Append-only version chain per record (EntityType, EntityId). Each
// save stores the document snapshot + who/when/session, so previous
// versions can be listed and restored.
//
// Restoring is itself audited — history is never silently erased.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Audit
{
    /// <summary>One stored version of a record.</summary>
    public class RecordVersion
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = "";
        public string EntityId { get; set; } = "";
        public int VersionNo { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string Username { get; set; } = "";
        public string SessionId { get; set; } = "";
        public string Summary { get; set; } = "";
        public string SnapshotJson { get; set; } = "";
    }

    /// <summary>Append-only version store (audit.db → RecordVersions).</summary>
    public static class VersionHistoryService
    {
        /// <summary>Stores a new version; version number auto-increments.</summary>
        public static async Task<int> AddVersionAsync(
            string entityType, string entityId,
            string summary, string snapshotJson)
        {
            try
            {
                using var connection = AuditDbMigrations.Open();

                int next = 1;
                using (var max = connection.CreateCommand())
                {
                    max.CommandText = @"
                        SELECT COALESCE(MAX(VersionNo), 0)
                        FROM RecordVersions
                        WHERE EntityType = $type AND EntityId = $id;";
                    max.Parameters.AddWithValue("$type", entityType);
                    max.Parameters.AddWithValue("$id", entityId);

                    var result = await max.ExecuteScalarAsync();
                    next = Convert.ToInt32(result) + 1;
                }

                using var insert = connection.CreateCommand();
                insert.CommandText = @"
                    INSERT INTO RecordVersions
                        (EntityType, EntityId, VersionNo, TimestampUtc,
                         Username, SessionId, Summary, SnapshotJson)
                    VALUES
                        ($type, $id, $ver, $now, $user, $session,
                         $summary, $snap);";

                insert.Parameters.AddWithValue("$type", entityType);
                insert.Parameters.AddWithValue("$id", entityId);
                insert.Parameters.AddWithValue("$ver", next);
                insert.Parameters.AddWithValue(
                    "$now", DateTime.UtcNow.ToString("o"));
                insert.Parameters.AddWithValue(
                    "$user", AuditService.CurrentUsername);
                insert.Parameters.AddWithValue(
                    "$session", AuditService.SessionId);
                insert.Parameters.AddWithValue("$summary", summary ?? "");
                insert.Parameters.AddWithValue(
                    "$snap", snapshotJson ?? "");

                await insert.ExecuteNonQueryAsync();
                return next;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[VersionHistory] Add failed: {ex.Message}");
                return 0;
            }
        }

        /// <summary>All versions of a record, newest first.</summary>
        public static async Task<List<RecordVersion>> GetVersionsAsync(
            string entityType, string entityId,
            bool includeSnapshots = false)
        {
            var list = new List<RecordVersion>();

            try
            {
                using var connection = AuditDbMigrations.Open();
                using var command = connection.CreateCommand();

                command.CommandText = includeSnapshots
                    ? @"SELECT Id, EntityType, EntityId, VersionNo,
                               TimestampUtc, Username, SessionId,
                               Summary, SnapshotJson
                        FROM RecordVersions
                        WHERE EntityType = $type AND EntityId = $id
                        ORDER BY VersionNo DESC;"
                    : @"SELECT Id, EntityType, EntityId, VersionNo,
                               TimestampUtc, Username, SessionId,
                               Summary, ''
                        FROM RecordVersions
                        WHERE EntityType = $type AND EntityId = $id
                        ORDER BY VersionNo DESC;";

                command.Parameters.AddWithValue("$type", entityType);
                command.Parameters.AddWithValue("$id", entityId);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new RecordVersion
                    {
                        Id = reader.GetInt32(0),
                        EntityType = reader.GetString(1),
                        EntityId = reader.GetString(2),
                        VersionNo = reader.GetInt32(3),
                        TimestampUtc = DateTime.Parse(reader.GetString(4)),
                        Username = reader.GetString(5),
                        SessionId = reader.GetString(6),
                        Summary = reader.GetString(7),
                        SnapshotJson = reader.GetString(8)
                    });
                }
            }
            catch { }

            return list;
        }

        /// <summary>Loads one version including its snapshot.</summary>
        public static async Task<RecordVersion?> GetByIdAsync(int versionId)
        {
            try
            {
                using var connection = AuditDbMigrations.Open();
                using var command = connection.CreateCommand();

                command.CommandText = @"
                    SELECT Id, EntityType, EntityId, VersionNo,
                           TimestampUtc, Username, SessionId,
                           Summary, SnapshotJson
                    FROM RecordVersions WHERE Id = $id;";
                command.Parameters.AddWithValue("$id", versionId);

                using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return null;

                return new RecordVersion
                {
                    Id = reader.GetInt32(0),
                    EntityType = reader.GetString(1),
                    EntityId = reader.GetString(2),
                    VersionNo = reader.GetInt32(3),
                    TimestampUtc = DateTime.Parse(reader.GetString(4)),
                    Username = reader.GetString(5),
                    SessionId = reader.GetString(6),
                    Summary = reader.GetString(7),
                    SnapshotJson = reader.GetString(8)
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
