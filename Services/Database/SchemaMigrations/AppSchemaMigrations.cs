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
        public static IReadOnlyList<SchemaMigration> Inquiries { get; }
            = new List<SchemaMigration>();

        // proformainvoices.db
        public static IReadOnlyList<SchemaMigration> ProformaInvoices { get; }
            = new List<SchemaMigration>
            {
                new(1, "Add optional stable Inquiry relationship", connection =>
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
    }
}
