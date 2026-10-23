// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Database/SchemaMigrations/SchemaMigration.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — versioned schema migration model
//
// One unit of schema change for a single SQLite database file.
//
// This is the SINGLE migration primitive used across every database
// (proglass / joborders / deliverynotes / taxinvoices / proformainvoices /
// specifications / audit / masterdata / statements / aimemory). It replaces
// the previous "EnsureCreated only" approach for existing installs: fresh
// databases are still created by EnsureCreated at the CURRENT schema, and
// this framework upgrades an EXISTING database forward without destroying
// data.
//
// DESIGN RULES (see SchemaMigrationRunner):
//   • Migrations are IDEMPOTENT. Existing data is not silently rewritten;
//     legacy columns can be renamed/dropped only after explicit value-copy
//     verification or a proof that the removed column is empty.
//   • Version is an integer tracked per database file via
//     PRAGMA user_version.
//   • The database file is backed up BEFORE any migration applies.
//   • Each migration runs inside a transaction (SQLite DDL is
//     transactional) so a failure leaves the schema untouched.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Data.Sqlite;

namespace ProGlassAutomation.Services.Database.SchemaMigrations
{
    /// <summary>
    /// One versioned schema migration for a single database file.
    /// </summary>
    public sealed class SchemaMigration
    {
        /// <summary>
        /// The schema version this migration advances the database to.
        /// Must be strictly greater than the previous migration's Version.
        /// </summary>
        public int Version { get; }

        /// <summary>Human-readable description (used in logs).</summary>
        public string Name { get; }

        /// <summary>
        /// Applies the schema change on an already-open connection (inside
        /// the runner's transaction). Use the idempotent helpers in
        /// <see cref="SchemaMigrationSql"/> so a re-run is always safe.
        /// </summary>
        public System.Action<SqliteConnection> Apply { get; }

        /// <summary>
        /// Optional post-apply assertions for critical indexes and migrated
        /// data. Runs inside the same transaction; throwing aborts the whole
        /// migration and leaves user_version unchanged.
        /// </summary>
        public System.Action<SqliteConnection>? Verify { get; }

        public SchemaMigration(
            int version,
            string name,
            System.Action<SqliteConnection> apply,
            System.Action<SqliteConnection>? verify = null)
        {
            if (version <= 0)
                throw new System.ArgumentOutOfRangeException(
                    nameof(version), "Migration versions must be >= 1.");
            if (string.IsNullOrWhiteSpace(name))
                throw new System.ArgumentNullException(nameof(name));
            Version = version;
            Name = name;
            Apply = apply ?? throw new System.ArgumentNullException(nameof(apply));
            Verify = verify;
        }
    }
}
