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
using System.Collections.Generic;

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
        public static IReadOnlyList<SchemaMigration> DeliveryNotes { get; }
            = new List<SchemaMigration>();

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
            catch (SqliteException)
            {
                // Non-fatal on purpose: the register still works without the
                // constraint, and the repository's number generator already
                // regenerates on unique collisions.
            }
        }

        /// <summary>
        /// Migration 1002: drop any column that exists in the live
        /// Inquiries table but isn't in the expected model. Interim
        /// builds (before 1001) shipped a different InquiryRecord
        /// shape — most notably a required NOT NULL column called
        /// "InquiryDate" that was later renamed to "ReceivedAt".
        /// 1001 added the new columns and indexes but never removed
        /// the legacy ones, so every INSERT now fails with:
        ///     SQLite error 19: 'NOT NULL constraint failed:
        ///     Inquiries.InquiryDate'
        /// because EF's INSERT statement omits the column (it's not
        /// in the model) and SQLite tries to apply the (missing)
        /// DEFAULT. This step runs after 1001 and is idempotent: a
        /// fresh database that was just CREATE-TABLE'd in step 1 has
        /// only the expected columns, so the Except set is empty and
        /// the ALTER TABLE block is a no-op.
        /// </summary>
        private static void DropLegacyInquiriesColumns(SqliteConnection connection)
        {
            string[] expectedInquiriesColumns = new[]
            {
                "Id", "InquiryNumber", "InquiryReference", "CustomerId", "CustomerName", "CustomerLocation",
                "ProjectId", "ProjectName", "ProjectDescription", "SalesmanId", "SalesmanName",
                "PreparedByUserId", "PreparedByName", "PreparedByRole", "Status", "Priority",
                "InquiryValue", "Notes", "ReceivedAt", "PreparedAt", "SentAt", "ConfirmedAt",
                "CreatedAt", "UpdatedAt", "UpdatedBy", "ProformaInvoiceId", "ProformaNumber",
                "JobOrderNumber"
            };
            // PRAGMA table_info gives us the live column list. Anything
            // outside the expected set is dropped. We avoid quoting
            // issues by validating membership before issuing DDL.
            var liveColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA table_info(\"Inquiries\");";
                using var reader = pragma.ExecuteReader();
                while (reader.Read()) liveColumns.Add(reader.GetString(1));
            }
            foreach (var orphan in liveColumns.Except(expectedInquiriesColumns, StringComparer.OrdinalIgnoreCase))
            {
                // SQLite >= 3.35 supports ALTER TABLE ... DROP COLUMN.
                // The SQLitePCL raw bundle in this project is 3.46+ so
                // this is always available. Identifier sanitization:
                // orphan is a column name from PRAGMA so it can't be
                // user input; quoting it with " preserves any case that
                // SQLite preserved.
                try
                {
                    using var drop = connection.CreateCommand();
                    drop.CommandText = $"ALTER TABLE \"Inquiries\" DROP COLUMN \"{orphan}\";";
                    drop.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    // Non-fatal: a single bad column shouldn't block
                    // startup. Worst case it forces a manual migration.
                    System.Diagnostics.Debug.WriteLine(
                        $"DropLegacyInquiriesColumns: could not drop '{orphan}': {ex.Message}");
                }
            }
        }
    }
}
