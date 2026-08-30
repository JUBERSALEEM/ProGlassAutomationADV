// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Database/SchemaMigrations/AppSchemaMigrations.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — migration registries for every SQLite database
//
// One registry per database file, consumed by SchemaMigrationRunner after
// each DbContext's EnsureCreated(). Fresh databases are created at the
// CURRENT schema by EnsureCreated, so these lists may begin empty; they
// exist so that future schema changes are added here as versioned,
// additive migrations instead of relying on EnsureCreated (which can never
// alter an existing database).
//
// RULE: every migration is additive/idempotent (add columns/tables/indexes
// only). Never drop or rewrite existing rows.
//
// NOTE (audit.db): audit.db already had its own hand-written additive
// migrator (AuditDbMigrations) before this framework existed. To avoid a
// behaviour change to a security-critical file in the same change that
// introduces the framework, audit.db keeps calling its existing migrator for
// now. Its operations are identical in spirit (idempotent ADD COLUMN /
// CREATE TABLE IF NOT EXISTS) and can be folded into this registry in a
// later, separately-verified phase.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Data.Sqlite;
using ProGlassAutomation.Services.Logging;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.Database.SchemaMigrations
{
    /// <summary>Central registry of schema migrations per database file.</summary>
    public static class AppSchemaMigrations
    {
        // proglass.db (Sheet Store)
        public static IReadOnlyList<SchemaMigration> ProGlass { get; }
            = new List<SchemaMigration>();

        // joborders.db
        public static IReadOnlyList<SchemaMigration> JobOrders { get; }
            = new List<SchemaMigration>();

        // deliverynotes.db
        public static IReadOnlyList<SchemaMigration> DeliveryNotes { get; } = new List<SchemaMigration>
        {
            new(1, "Add WeightPerPiece column for glass weight tracking", connection =>
            {
                SchemaMigrationSql.AddColumnIfMissing(connection, "DeliveryNoteItems", "WeightPerPiece", "REAL NOT NULL DEFAULT 0");
            })
        };

        // taxinvoices.db
        public static IReadOnlyList<SchemaMigration> TaxInvoices { get; }
            = new List<SchemaMigration>();

        // inquiries.db
        //
        // Interim builds shipped an earlier Inquiry domain model, so customer
        // databases exist whose Inquiries table predates the commercial
        // columns (InquiryValue / Priority / Notes / …) or even the child
        // tables. EnsureCreated never alters an existing database, and this
        // registry was empty until now — those installs crashed at first load
        // with SQLite error 1 "no such column i.InquiryValue".
        //
        // 1001 converges ANY legacy shape to the current model. It is
        // idempotent: CREATE TABLE IF NOT EXISTS, one ADD COLUMN per missing
        // column, duplicate numbers made unique, then the indexes. Reserved
        // high version for the same reason as ProformaInvoices above.
        public static IReadOnlyList<SchemaMigration> Inquiries { get; } = new List<SchemaMigration>
        {
            new(1001, "Converge legacy inquiry databases to the current model", connection =>
                ConvergeInquirySchema(connection)),
            // 1002 runs after 1001 so customers who already upgraded past
            // 1001 (and therefore still have the legacy InquiryDate column)
            // get the drop. We split it out as a separate migration
            // rather than mutating 1001, which is the documented baseline
            // for what EnsureCreated should look like after first run.
            new(1002, "Drop legacy Inquiry columns (InquiryDate, etc.) not in the model", connection =>
                DropLegacyInquiriesColumns(connection))
        };

        // proformainvoices.db
        public static IReadOnlyList<SchemaMigration> ProformaInvoices { get; }
            = new List<SchemaMigration>
            {
                // Use a reserved high version: customer databases may already
                // carry PRAGMA user_version values from older installers even
                // though this registry was previously empty. Version 1 was
                // therefore incorrectly skipped, leaving InquiryId absent.
                new(1001, "Add optional stable Inquiry relationship", connection =>
                {
                    SchemaMigrationSql.AddColumnIfMissing(connection, "ProformaInvoices", "InquiryId", "INTEGER NULL");
                    SchemaMigrationSql.CreateIndexIfNotExists(connection,
                        "CREATE INDEX IF NOT EXISTS IX_ProformaInvoices_InquiryId ON ProformaInvoices(InquiryId);");
                })
            };

        // specifications.db
        public static IReadOnlyList<SchemaMigration> Specifications { get; }
            = new List<SchemaMigration>();

        // masterdata.db
        public static IReadOnlyList<SchemaMigration> MasterData { get; }
            = new List<SchemaMigration>();

        // statements.db
        public static IReadOnlyList<SchemaMigration> Statements { get; }
            = new List<SchemaMigration>();

        // aimemory.db
        public static IReadOnlyList<SchemaMigration> AiMemory { get; }
            = new List<SchemaMigration>();

        // ── inquiries.db helpers (migration 1001) ───────────────────────

        /// <summary>
        /// Brings any legacy inquiries.db to the current InquiryRecord /
        /// InquiryAttachment / InquiryCommunication shape without touching
        /// existing rows. Every step is idempotent, so a database created by
        /// EnsureCreated at the current schema is a complete no-op.
        /// </summary>
        private static void ConvergeInquirySchema(SqliteConnection connection)
        {
            // 1) Tables missing entirely on the earliest builds.
            SchemaMigrationSql.CreateTableIfNotExists(connection, """
                CREATE TABLE IF NOT EXISTS "Inquiries" (
                    "Id"                 INTEGER NOT NULL CONSTRAINT "PK_Inquiries" PRIMARY KEY AUTOINCREMENT,
                    "InquiryNumber"      TEXT NOT NULL DEFAULT '',
                    "InquiryReference"   TEXT NOT NULL DEFAULT 'Customer Inquiry',
                    "CustomerId"         INTEGER NULL,
                    "CustomerName"       TEXT NOT NULL DEFAULT '',
                    "CustomerLocation"   TEXT NOT NULL DEFAULT '',
                    "ProjectId"          INTEGER NULL,
                    "ProjectName"        TEXT NOT NULL DEFAULT '',
                    "ProjectDescription" TEXT NOT NULL DEFAULT '',
                    "SalesmanId"         INTEGER NULL,
                    "SalesmanName"       TEXT NOT NULL DEFAULT '',
                    "PreparedByUserId"   INTEGER NULL,
                    "PreparedByName"     TEXT NOT NULL DEFAULT '',
                    "PreparedByRole"     TEXT NOT NULL DEFAULT '',
                    "Status"             TEXT NOT NULL DEFAULT 'Received',
                    "Priority"           TEXT NOT NULL DEFAULT 'Normal',
                    "InquiryValue"       TEXT NULL DEFAULT '0',
                    "Notes"              TEXT NOT NULL DEFAULT '',
                    "ReceivedAt"         TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                    "PreparedAt"         TEXT NULL,
                    "SentAt"             TEXT NULL,
                    "ConfirmedAt"        TEXT NULL,
                    "CreatedAt"          TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                    "UpdatedAt"          TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                    "UpdatedBy"          TEXT NOT NULL DEFAULT '',
                    "ProformaInvoiceId"  INTEGER NULL,
                    "ProformaNumber"     TEXT NOT NULL DEFAULT '',
                    "JobOrderNumber"     TEXT NOT NULL DEFAULT ''
                );
                """);
            SchemaMigrationSql.CreateTableIfNotExists(connection, """
                CREATE TABLE IF NOT EXISTS "InquiryAttachments" (
                    "Id"             INTEGER NOT NULL CONSTRAINT "PK_InquiryAttachments" PRIMARY KEY AUTOINCREMENT,
                    "InquiryId"      INTEGER NOT NULL,
                    "FileName"       TEXT NOT NULL DEFAULT '',
                    "Extension"      TEXT NOT NULL DEFAULT '',
                    "FileSize"       INTEGER NOT NULL DEFAULT 0,
                    "StoragePath"    TEXT NOT NULL DEFAULT '',
                    "AttachmentType" TEXT NOT NULL DEFAULT 'Document',
                    "UploadedBy"     TEXT NOT NULL DEFAULT '',
                    "UploadedAt"     TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                    CONSTRAINT "FK_InquiryAttachments_Inquiries_InquiryId"
                        FOREIGN KEY ("InquiryId") REFERENCES "Inquiries" ("Id") ON DELETE CASCADE
                );
                """);
            SchemaMigrationSql.CreateTableIfNotExists(connection, """
                CREATE TABLE IF NOT EXISTS "InquiryCommunications" (
                    "Id"          INTEGER NOT NULL CONSTRAINT "PK_InquiryCommunications" PRIMARY KEY AUTOINCREMENT,
                    "InquiryId"   INTEGER NOT NULL,
                    "EventType"   TEXT NOT NULL DEFAULT '',
                    "Title"       TEXT NOT NULL DEFAULT '',
                    "Description" TEXT NOT NULL DEFAULT '',
                    "OccurredAt"  TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                    "ActorUserId" INTEGER NULL,
                    "ActorName"   TEXT NOT NULL DEFAULT '',
                    CONSTRAINT "FK_InquiryCommunications_Inquiries_InquiryId"
                        FOREIGN KEY ("InquiryId") REFERENCES "Inquiries" ("Id") ON DELETE CASCADE
                );
                """);

            // 2) Columns missing on interim builds. ALTER TABLE ADD COLUMN
            //    fills existing rows with the DEFAULT, so EF can read them.
            (string Table, string Column, string Definition)[] columns =
            {
                ("Inquiries", "InquiryNumber",      "TEXT NOT NULL DEFAULT ''"),
                ("Inquiries", "InquiryReference",   "TEXT NOT NULL DEFAULT 'Customer Inquiry'"),
                ("Inquiries", "CustomerId",         "INTEGER NULL"),
                ("Inquiries", "CustomerName",       "TEXT NOT NULL DEFAULT ''"),
                ("Inquiries", "CustomerLocation",   "TEXT NOT NULL DEFAULT ''"),
                ("Inquiries", "ProjectId",          "INTEGER NULL"),
                ("Inquiries", "ProjectName",        "TEXT NOT NULL DEFAULT ''"),
                ("Inquiries", "ProjectDescription", "TEXT NOT NULL DEFAULT ''"),
                ("Inquiries", "SalesmanId",         "INTEGER NULL"),
                ("Inquiries", "SalesmanName",       "TEXT NOT NULL DEFAULT ''"),
                ("Inquiries", "PreparedByUserId",   "INTEGER NULL"),
                ("Inquiries", "PreparedByName",     "TEXT NOT NULL DEFAULT ''"),
                ("Inquiries", "PreparedByRole",     "TEXT NOT NULL DEFAULT ''"),
                ("Inquiries", "Status",             "TEXT NOT NULL DEFAULT 'Received'"),
                ("Inquiries", "Priority",           "TEXT NOT NULL DEFAULT 'Normal'"),
                ("Inquiries", "InquiryValue",       "TEXT NULL DEFAULT '0'"),
                ("Inquiries", "Notes",              "TEXT NOT NULL DEFAULT ''"),
                ("Inquiries", "ReceivedAt",         "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'"),
                ("Inquiries", "PreparedAt",         "TEXT NULL"),
                ("Inquiries", "SentAt",             "TEXT NULL"),
                ("Inquiries", "ConfirmedAt",        "TEXT NULL"),
                ("Inquiries", "CreatedAt",          "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'"),
                ("Inquiries", "UpdatedAt",          "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'"),
                ("Inquiries", "UpdatedBy",          "TEXT NOT NULL DEFAULT ''"),
                ("Inquiries", "ProformaInvoiceId",  "INTEGER NULL"),
                ("Inquiries", "ProformaNumber",     "TEXT NOT NULL DEFAULT ''"),
                ("Inquiries", "JobOrderNumber",     "TEXT NOT NULL DEFAULT ''"),
                ("InquiryAttachments", "InquiryId",      "INTEGER NOT NULL DEFAULT 0"),
                ("InquiryAttachments", "FileName",       "TEXT NOT NULL DEFAULT ''"),
                ("InquiryAttachments", "Extension",      "TEXT NOT NULL DEFAULT ''"),
                ("InquiryAttachments", "FileSize",       "INTEGER NOT NULL DEFAULT 0"),
                ("InquiryAttachments", "StoragePath",    "TEXT NOT NULL DEFAULT ''"),
                ("InquiryAttachments", "AttachmentType", "TEXT NOT NULL DEFAULT 'Document'"),
                ("InquiryAttachments", "UploadedBy",     "TEXT NOT NULL DEFAULT ''"),
                ("InquiryAttachments", "UploadedAt",     "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'"),
                ("InquiryCommunications", "InquiryId",   "INTEGER NOT NULL DEFAULT 0"),
                ("InquiryCommunications", "EventType",   "TEXT NOT NULL DEFAULT ''"),
                ("InquiryCommunications", "Title",       "TEXT NOT NULL DEFAULT ''"),
                ("InquiryCommunications", "Description", "TEXT NOT NULL DEFAULT ''"),
                ("InquiryCommunications", "OccurredAt",  "TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'"),
                ("InquiryCommunications", "ActorUserId", "INTEGER NULL"),
                ("InquiryCommunications", "ActorName",   "TEXT NOT NULL DEFAULT ''"),
            };
            foreach (var (table, column, definition) in columns)
                SchemaMigrationSql.AddColumnIfMissing(connection, table, column, definition);

            // 3) Rows with duplicate InquiryNumbers (possible from the era
            //    before the unique index) would make step 4 fail. Keep the
            //    oldest row's number, make later duplicates unique.
            SchemaMigrationSql.Execute(connection, """
                UPDATE "Inquiries"
                   SET "InquiryNumber" = "InquiryNumber" || '-' || "Id"
                 WHERE "Id" NOT IN (SELECT MIN("Id") FROM "Inquiries" GROUP BY "InquiryNumber")
                   AND "InquiryNumber" IN (SELECT "InquiryNumber" FROM "Inquiries"
                                            GROUP BY "InquiryNumber" HAVING COUNT(*) > 1);
                """);

            // 4) Indexes. The unique inquiry-number index is best-effort:
            //    column convergence above must never be blocked by data.
            foreach (string sql in new[]
            {
                "CREATE INDEX IF NOT EXISTS \"IX_Inquiries_ReceivedAt\" ON \"Inquiries\" (\"ReceivedAt\");",
                "CREATE INDEX IF NOT EXISTS \"IX_Inquiries_UpdatedAt\" ON \"Inquiries\" (\"UpdatedAt\");",
                "CREATE INDEX IF NOT EXISTS \"IX_Inquiries_Status\" ON \"Inquiries\" (\"Status\");",
                "CREATE INDEX IF NOT EXISTS \"IX_Inquiries_CustomerId\" ON \"Inquiries\" (\"CustomerId\");",
                "CREATE INDEX IF NOT EXISTS \"IX_Inquiries_SalesmanId\" ON \"Inquiries\" (\"SalesmanId\");",
                "CREATE INDEX IF NOT EXISTS \"IX_Inquiries_PreparedByUserId\" ON \"Inquiries\" (\"PreparedByUserId\");",
                "CREATE INDEX IF NOT EXISTS \"IX_InquiryAttachments_InquiryId\" ON \"InquiryAttachments\" (\"InquiryId\");",
                "CREATE INDEX IF NOT EXISTS \"IX_InquiryAttachments_UploadedAt\" ON \"InquiryAttachments\" (\"UploadedAt\");",
                "CREATE INDEX IF NOT EXISTS \"IX_InquiryCommunications_InquiryId_OccurredAt\" ON \"InquiryCommunications\" (\"InquiryId\", \"OccurredAt\");",
            })
            {
                SchemaMigrationSql.CreateIndexIfNotExists(connection, sql);
            }
            try
            {
                SchemaMigrationSql.CreateIndexIfNotExists(connection,
                    "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Inquiries_InquiryNumber\" ON \"Inquiries\" (\"InquiryNumber\");");
            }
            catch (SqliteException ex)
            {
                // Non-fatal on purpose: the register still works without the
                // constraint, and the repository's number generator already
                // regenerates on unique collisions.
                ProGlassAutomation.Services.Logging.AppLogService.Debug("[AppSchemaMigrations] Unique-index creation skipped: " + ex.Message);
            }
        }

        /// <summary>
        /// Migration 1002: drop any column that exists in the live
        /// Inquiries table but isn't in the expected model, AND
        /// rename known legacy columns to their current names so
        /// user data isn't lost. Interim builds (before 1001)
        /// shipped a different InquiryRecord shape — most notably a
        /// required NOT NULL column called "InquiryDate" that was
        /// later renamed to "ReceivedAt", and a "Salesman" column
        /// that was later renamed to "SalesmanName".
        ///
        /// 1001 added the new columns and indexes but never renamed
        /// or removed the legacy ones, so every INSERT fails with
        /// SQLite error 19 (NOT NULL constraint failed) on the
        /// leftover column because EF's INSERT statement omits it
        /// (it's not in the model) and SQLite tries to apply the
        /// (missing) DEFAULT. The user's first symptom is the
        /// "Attachment failed" dialog when the editor's auto-save
        /// runs before the first attachment on a brand-new inquiry.
        ///
        /// This step runs after 1001 and is idempotent: a fresh
        /// database that was just CREATE-TABLE'd in step 1 has
        /// only the expected columns, so the rename / drop
        /// passes are both no-ops.
        /// </summary>
        private static void DropLegacyInquiriesColumns(SqliteConnection connection)
        {
            // 0) Loud pre-migration snapshot. Logs the live column
            //    list so an operator can correlate "my DB has a
            //    Salesman column" with what the migration did. This
            //    is the first place to look when a user reports a
            //    NOT NULL constraint failure that the migration was
            //    supposed to fix.
            try
            {
                using var pre = connection.CreateCommand();
                pre.CommandText = "PRAGMA table_info(\"Inquiries\");";
                using var reader = pre.ExecuteReader();
                var before = new List<string>();
                while (reader.Read()) before.Add(reader.GetString(1));
                AppLogService.Info(
                    $"[Inquiries] pre-migration columns: {string.Join(", ", before)}",
                    new LogContext { Module = "Database", Class = "SchemaMigrations", Operation = "SnapshotInquiriesSchema" });
            }
            catch { /* snapshot logging is best-effort */ }

            // 1) Rename known legacy columns so user data is preserved.
            //    Maps "old name" → "new name". Each entry is run as
            //    "ALTER TABLE Inquiries RENAME COLUMN old TO new"
            //    which is supported on SQLite 3.25+. The runner
            //    ignores failures (the column may not exist on a
            //    particular install, OR the new name may already
            //    exist from migration 1001) and logs to AppLogService
            //    so ops can see what actually happened. The drop pass
            //    below still runs after the renames, so a renamed
            //    column with a duplicate new-name is cleaned up by
            //    the orphan drop.
            var renames = new (string OldName, string NewName)[]
            {
                ("InquiryDate", "ReceivedAt"),     // pre-2025 build
                ("Salesman",    "SalesmanName"),   // pre-2025 build (no "Name" suffix)
                ("Customer",    "CustomerName"),   // pre-2025 build (no "Name" suffix)
                ("Project",     "ProjectName"),    // pre-2025 build (no "Name" suffix)
                ("PreparedBy",  "PreparedByName"), // pre-2025 build (no "Name" suffix)
            };
            foreach (var (oldName, newName) in renames)
            {
                TryRenameColumn(connection, "Inquiries", oldName, newName);
            }

            // 2) Drop any column that exists but isn't in the
            //    expected model. Idempotent on a fresh DB: a
            //    CREATE-TABLE'd Inquiries only has the expected
            //    columns, so the Except set is empty.
            string[] expectedInquiriesColumns = new[]
            {
                "Id", "InquiryNumber", "InquiryReference", "CustomerId", "CustomerName", "CustomerLocation",
                "ProjectId", "ProjectName", "ProjectDescription", "SalesmanId", "SalesmanName",
                "PreparedByUserId", "PreparedByName", "PreparedByRole", "Status", "Priority",
                "InquiryValue", "Notes", "ReceivedAt", "PreparedAt", "SentAt", "ConfirmedAt",
                "CreatedAt", "UpdatedAt", "UpdatedBy", "ProformaInvoiceId", "ProformaNumber",
                "JobOrderNumber"
            };
            var liveColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA table_info(\"Inquiries\");";
                using var reader = pragma.ExecuteReader();
                while (reader.Read()) liveColumns.Add(reader.GetString(1));
            }
            var expectedSet = new HashSet<string>(expectedInquiriesColumns, StringComparer.OrdinalIgnoreCase);
            var orphans = liveColumns.Where(c => !expectedSet.Contains(c)).ToList();
            foreach (var orphan in orphans)
            {
                TryDropColumn(connection, "Inquiries", orphan);
            }

            // 3) Loud verification step. After the pass, log the
            //    final column list so an operator reading the log
            //    can confirm the table is in the expected shape.
            //    A lingering "Salesman" or "InquiryDate" in this
            //    list means a rename failed and the operator needs
            //    to look at the file by hand.
            try
            {
                using var check = connection.CreateCommand();
                check.CommandText = "PRAGMA table_info(\"Inquiries\");";
                using var reader = check.ExecuteReader();
                var final = new List<string>();
                while (reader.Read()) final.Add(reader.GetString(1));
                AppLogService.Info(
                    $"[Inquiries] post-migration columns: {string.Join(", ", final)}",
                    new LogContext { Module = "Database", Class = "SchemaMigrations", Operation = "VerifyInquiriesSchema" });
            }
            catch { /* verification logging is best-effort */ }
        }

        /// <summary>
        /// Renames an Inquiries column from <paramref name="oldName"/>
        /// to <paramref name="newName"/> if it exists. Idempotent:
        /// silently skips when the source column isn't present.
        /// Failures are logged to AppLogService so ops can see
        /// what actually happened — the previous implementation
        /// swallowed errors to Debug.WriteLine, which is invisible
        /// in production.
        /// </summary>
        private static void TryRenameColumn(SqliteConnection connection, string table, string oldName, string newName)
        {
            try
            {
                // Confirm the source column exists. PRAGMA table_info
                // is case-insensitive on the column-name match, but
                // we keep the case the user typed in their DDL for
                // the ALTER statement.
                using (var check = connection.CreateCommand())
                {
                    check.CommandText = $"PRAGMA table_info(\"{table}\");";
                    using var reader = check.ExecuteReader();
                    bool exists = false;
                    while (reader.Read())
                    {
                        if (string.Equals(reader.GetString(1), oldName, StringComparison.OrdinalIgnoreCase))
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists) return; // column not present, nothing to rename
                }

                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"ALTER TABLE \"{table}\" RENAME COLUMN \"{oldName}\" TO \"{newName}\";";
                cmd.ExecuteNonQuery();
                AppLogService.Info(
                    $"[Inquiries] renamed legacy column '{oldName}' → '{newName}' (user data preserved).",
                    new LogContext { Module = "Database", Class = "SchemaMigrations", Operation = "TryRenameColumn" });
            }
            catch (Exception ex)
            {
                // Non-fatal: another column with the same shape may
                // already exist (the new name is in the model), or
                // the rename is unsupported on this SQLite build.
                // Either way we log and continue so the drop pass
                // below still runs.
                AppLogService.Warning(
                    $"[Inquiries] could not rename column '{oldName}' → '{newName}': {ex.Message}",
                    new LogContext { Module = "Database", Class = "SchemaMigrations", Operation = "TryRenameColumn" });
            }
        }

        /// <summary>
        /// Drops a single orphan column from a table. Idempotent:
        /// silently skips when the column isn't present. Failures
        /// are logged to AppLogService (was previously swallowed
        /// to Debug.WriteLine, which is invisible in production).
        /// </summary>
        private static void TryDropColumn(SqliteConnection connection, string table, string column)
        {
            try
            {
                using var drop = connection.CreateCommand();
                drop.CommandText = $"ALTER TABLE \"{table}\" DROP COLUMN \"{column}\";";
                drop.ExecuteNonQuery();
                AppLogService.Info(
                    $"[{table}] dropped orphan column '{column}'.",
                    new LogContext { Module = "Database", Class = "SchemaMigrations", Operation = "TryDropColumn" });
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"[{table}] could not drop orphan column '{column}': {ex.Message}",
                    new LogContext { Module = "Database", Class = "SchemaMigrations", Operation = "TryDropColumn" });
            }
        }
    }
}
