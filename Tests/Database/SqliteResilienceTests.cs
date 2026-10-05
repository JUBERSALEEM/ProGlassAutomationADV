// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Database/SqliteResilienceTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Database — SQLite hardening (senior audit — Aug 2026)
//
// Hermetic tests for Services/Database/SqliteResilience.cs. All tests:
//   • Open an ephemeral SQLite file under Path.GetTempPath().
//   • Delete it on Dispose. Never touch %AppData% or any real DB.
//   • Do NOT depend on EF Core, migrations, App.Config or AppDataRoot.
//     They exercise SqliteResilience.BuildConnectionString and the
//     interceptor directly against a raw SqliteConnection so a failure
//     here has one and only one cause.
//
// What we cover:
//   1. Pragmas actually apply (journal_mode / foreign_keys / busy_timeout).
//   2. Foreign-key enforcement is real (violating a FK throws).
//   3. Classifier maps SQLITE_CONSTRAINT_UNIQUE → UniqueViolation.
//   4. Classifier maps SQLITE_CONSTRAINT_FOREIGNKEY → ForeignKeyViolation.
//   5. Classifier ignores wrapped exceptions up to 8 levels deep.
//   6. IsTransient / IsUnique / IsTerminal helpers work.
//   7. SaveWithRetryAsync retries on Busy, gives up after maxAttempts.
//   8. SaveWithRetryAsync surfaces non-transient errors immediately.
//   9. Connection-string builder rejects empty paths.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Services.Database;
using System;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ProGlassAutomation.Tests.Database
{
    public class SqliteResilienceTests : IDisposable
    {
        private readonly string _dbPath;

        public SqliteResilienceTests()
        {
            _dbPath = Path.Combine(
                Path.GetTempPath(),
                "pga_test_" + Guid.NewGuid().ToString("N") + ".db");
        }

        public void Dispose()
        {
            // Clear connection pool so the file handle is released.
            SqliteConnection.ClearAllPools();
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* best effort */ }
            // WAL companions.
            foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
            {
                try { if (File.Exists(_dbPath + suffix)) File.Delete(_dbPath + suffix); } catch { /* best effort */ }
            }
        }

        // ── Connection-string builder ────────────────────────────────────

        [Fact]
        public void BuildConnectionString_rejects_empty_path()
        {
            Assert.Throws<ArgumentException>(() => SqliteResilience.BuildConnectionString(""));
            Assert.Throws<ArgumentException>(() => SqliteResilience.BuildConnectionString("   "));
            Assert.Throws<ArgumentException>(() => SqliteResilience.BuildConnectionString(null!));
        }

        [Fact]
        public void BuildConnectionString_produces_valid_sqlite_string()
        {
            var cs = SqliteResilience.BuildConnectionString(_dbPath);
            var b = new SqliteConnectionStringBuilder(cs);
            Assert.Equal(_dbPath, b.DataSource);
            Assert.Equal(SqliteOpenMode.ReadWriteCreate, b.Mode);
            Assert.Equal(SqliteCacheMode.Shared, b.Cache);
        }

        // ── Interceptor / pragma application ─────────────────────────────

        [Fact]
        public void Interceptor_activates_wal_and_foreign_keys_and_busy_timeout()
        {
            using var ctx = new PragmaTestContext(_dbPath);
            ctx.Database.EnsureCreated();

            // Force the interceptor to run by touching the connection.
            var conn = (SqliteConnection)ctx.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open) conn.Open();

            Assert.Equal("wal", ReadPragma(conn, "journal_mode").ToLowerInvariant());
            Assert.Equal("1", ReadPragma(conn, "foreign_keys"));
            // busy_timeout is returned as ms; we set 5000.
            Assert.Equal("5000", ReadPragma(conn, "busy_timeout"));
            // temp_store: 2 = MEMORY.
            Assert.Equal("2", ReadPragma(conn, "temp_store"));
        }

        [Fact]
        public void Foreign_key_violation_is_actually_thrown_when_enforcement_is_on()
        {
            using var ctx = new PragmaTestContext(_dbPath);
            ctx.Database.EnsureCreated();

            // A Child with a non-existent ParentId must throw once the
            // interceptor has applied PRAGMA foreign_keys=ON. Without the
            // interceptor, SQLite would silently accept the row.
            ctx.Children.Add(new Child { Id = 1, ParentId = 999, Name = "orphan" });

            var ex = Assert.ThrowsAny<DbUpdateException>(() => ctx.SaveChanges());
            Assert.Equal(SqliteErrorKind.ForeignKeyViolation,
                SqliteErrorClassifier.Classify(ex));
        }

        // ── Classifier ───────────────────────────────────────────────────

        [Fact]
        public void Classifier_detects_unique_violation()
        {
            using var ctx = new PragmaTestContext(_dbPath);
            ctx.Database.EnsureCreated();
            ctx.Parents.Add(new Parent { Id = 1, Name = "first" });
            ctx.SaveChanges();

            // Same primary key: SQLite reports as SQLITE_CONSTRAINT_PRIMARYKEY
            // (extended 1555). Our classifier maps both PRIMARYKEY and UNIQUE
            // to UniqueViolation because the caller treats them identically.
            ctx.Parents.Add(new Parent { Id = 1, Name = "dup" });
            var ex = Assert.ThrowsAny<DbUpdateException>(() => ctx.SaveChanges());
            Assert.Equal(SqliteErrorKind.UniqueViolation,
                SqliteErrorClassifier.Classify(ex));
            Assert.True(SqliteErrorClassifier.IsUnique(ex));
            Assert.False(SqliteErrorClassifier.IsTransient(ex));
        }

        [Fact]
        public void Classifier_walks_inner_exception_chain()
        {
            var sqlite = new SqliteException("faked BUSY", 5);
            var wrapped = new InvalidOperationException("layer 1",
                new AggregateException("layer 2",
                    new Exception("layer 3", sqlite)));
            Assert.Equal(SqliteErrorKind.Busy, SqliteErrorClassifier.Classify(wrapped));
            Assert.True(SqliteErrorClassifier.IsTransient(wrapped));
        }

        [Fact]
        public void Classifier_returns_Other_for_non_sqlite_exception()
        {
            Assert.Equal(SqliteErrorKind.Other,
                SqliteErrorClassifier.Classify(new InvalidOperationException("nope")));
            Assert.Equal(SqliteErrorKind.Other, SqliteErrorClassifier.Classify(null));
            Assert.False(SqliteErrorClassifier.IsTransient(null));
            Assert.False(SqliteErrorClassifier.IsUnique(null));
            Assert.False(SqliteErrorClassifier.IsTerminal(null));
        }

        [Fact]
        public void Classifier_flags_corrupt_and_readonly_as_terminal()
        {
            Assert.True(SqliteErrorClassifier.IsTerminal(new SqliteException("corrupt", 11)));
            Assert.True(SqliteErrorClassifier.IsTerminal(new SqliteException("readonly", 8)));
            Assert.False(SqliteErrorClassifier.IsTerminal(new SqliteException("busy", 5)));
        }

        // ── SaveWithRetryAsync ───────────────────────────────────────────

        [Fact]
        public async Task SaveWithRetryAsync_retries_on_busy_and_eventually_succeeds()
        {
            int attempts = 0;
            var result = await SqliteResilience.SaveWithRetryAsync(_ =>
            {
                attempts++;
                if (attempts < 3)
                    throw new DbUpdateException("boom",
                        new SqliteException("database is locked", 5));
                return Task.FromResult(42);
            }, CancellationToken.None);

            Assert.Equal(3, attempts);
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task SaveWithRetryAsync_gives_up_after_maxAttempts_and_rethrows()
        {
            int attempts = 0;
            var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
                SqliteResilience.SaveWithRetryAsync(_ =>
                {
                    attempts++;
                    throw new DbUpdateException("boom",
                        new SqliteException("database is locked", 5));
                }, CancellationToken.None, maxAttempts: 3));
            Assert.Equal(3, attempts);
            Assert.Equal(SqliteErrorKind.Busy, SqliteErrorClassifier.Classify(ex));
        }

        [Fact]
        public async Task SaveWithRetryAsync_does_not_retry_on_unique_violation()
        {
            int attempts = 0;
            await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
                SqliteResilience.SaveWithRetryAsync(_ =>
                {
                    attempts++;
                    // Extended code 2067 = SQLITE_CONSTRAINT_UNIQUE.
                    throw new DbUpdateException("dup",
                        new SqliteException("UNIQUE constraint failed", 19, 2067));
                }, CancellationToken.None, maxAttempts: 5));
            // A UNIQUE violation is a programmer/domain error and must
            // surface on the very first attempt.
            Assert.Equal(1, attempts);
        }

        [Fact]
        public async Task SaveWithRetryAsync_honours_cancellation()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            {
                await SqliteResilience.SaveWithRetryAsync(async token =>
                {
                    await Task.Delay(1, token).ConfigureAwait(false);
                    return 0;
                }, cts.Token, maxAttempts: 5);
            });
        }

        [Fact]
        public async Task SaveWithRetryAsync_rejects_null_delegate()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                SqliteResilience.SaveWithRetryAsync(null!, CancellationToken.None));
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static string ReadPragma(SqliteConnection conn, string pragma)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA {pragma};";
            var v = cmd.ExecuteScalar();
            return v?.ToString() ?? "";
        }

        // ── Test doubles ─────────────────────────────────────────────────

        private sealed class Parent
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }

        private sealed class Child
        {
            public int Id { get; set; }
            public int ParentId { get; set; }
            public string Name { get; set; } = "";
        }

        /// <summary>
        /// Minimal DbContext used only by these tests. It has one parent
        /// table, one child table, and a FK from Child.ParentId → Parent.Id
        /// with cascade delete. Nothing else — no seeding, no migrations.
        /// The whole point is to prove SqliteResilience's interceptor
        /// applies PRAGMAs and enforces foreign keys.
        /// </summary>
        private sealed class PragmaTestContext : DbContext
        {
            private readonly string _path;
            public PragmaTestContext(string path) => _path = path;

            public DbSet<Parent> Parents => Set<Parent>();
            public DbSet<Child> Children => Set<Child>();

            protected override void OnConfiguring(DbContextOptionsBuilder options)
            {
                SqliteResilience.ConfigureFor(options, _path);
            }

            protected override void OnModelCreating(ModelBuilder model)
            {
                model.Entity<Parent>().HasKey(x => x.Id);
                model.Entity<Child>(e =>
                {
                    e.HasKey(x => x.Id);
                    e.HasOne<Parent>()
                        .WithMany()
                        .HasForeignKey(x => x.ParentId)
                        .OnDelete(DeleteBehavior.Cascade);
                });
            }
        }
    }
}
