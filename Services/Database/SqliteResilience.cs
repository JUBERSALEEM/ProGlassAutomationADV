// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Database/SqliteResilience.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — SQLite hardening (senior audit — Aug 2026)
//
// WHY THIS FILE EXISTS
// ────────────────────
// Every module's DbContext in this repo used to override OnConfiguring
// with a bare-minimum `options.UseSqlite("Data Source=…")` and nothing
// else. That produced three classes of production bug:
//
//   1. Transient SQLITE_BUSY / SQLITE_LOCKED errors when overlapping
//      application writes contend for SQLite's single-writer lock.
//      Local databases use WAL; shared databases use rollback-journal
//      DELETE mode with a longer busy timeout because WAL shared-memory
//      coordination is not safe across SMB/NAS.
//
//   2. Silent cascade-delete failures because SQLite foreign-key
//      enforcement is connection-local and OFF by default. The shared
//      interceptor enables and verifies it for every new EF connection.
//
//   3. UNIQUE-violation retry loops that could not actually retry
//      because they reused the same DbContext instance across attempts
//      — the change tracker still held the failed entity and the
//      SqliteConnection was in an ambiguous state.
//
// WHAT THIS FILE DOES
// ───────────────────
// One-stop connection/journal policy + retry helper used by every DbContext,
// raw migration connection, and path-aware read-only probe:
//
//     options.UseSqlite(SqliteResilience.BuildConnectionString(path))
//            .AddInterceptors(SqliteResilience.PragmaInterceptor);
//
// Local-root connections use WAL/NORMAL; shared-root connections use
// DELETE/FULL and a longer busy timeout, because WAL shared-memory
// coordination is not safe across SMB/NAS. Both policies enable FK checks.
// Connection-local cache/temp settings are tuned without mapping shared files.
//
// Retry helper: SaveChanges wrappers that catch transient SQLITE_BUSY /
// SQLITE_LOCKED (codes 5 / 6) and retry with exponential back-off.
// UNIQUE / NOT-NULL / CHECK / FOREIGN-KEY violations are NOT retried
// — those are programmer errors that should surface immediately.
//
// SAFETY / IDEMPOTENCY
// ────────────────────
// • Pragmas are re-applied on every new physical connection, not once
//   per app start. That is intentional: EF Core opens and closes
//   connections on demand, and each new connection to a SQLite file
//   starts with PRAGMA defaults regardless of what the previous
//   connection set.
//
// • journal_mode is persisted in the database header. Local paths request
//   WAL; shared paths request DELETE and fail closed if SQLite does not
//   report DELETE. Read-only probes verify the recorded mode without trying
//   to change it.
//
// • busy_timeout is a per-connection setting. It affects only future
//   statements on the same connection.
//
// • foreign_keys is enforced only for statements that follow the
//   PRAGMA. That means cascade behaviour changes the first time the
//   pragma is applied. This matches the DbContext's model expectations
//   (all HasMany / HasForeignKey declarations already assumed enforced
//   FKs). No repository code needs to change.
//
// • DO NOT wrap PRAGMA foreign_keys=ON inside a transaction — SQLite
//   silently ignores it in that case. The pragma interceptor runs at
//   ConnectionOpened, which is BEFORE any transaction can start.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Database
{
    /// <summary>
    /// Central SQLite hardening: path-aware journal pragmas, busy_timeout, foreign_keys,
    /// and a transient-error retry helper. See the file-header comment
    /// for the rationale.
    /// </summary>
    public static class SqliteResilience
    {
        // ── Central journal scripts for writable connections ────────────
        //
        // A single SQL batch is faster than N round-trips: SqliteCommand.
        // ExecuteNonQuery over a semicolon-separated batch runs all
        // pragmas in one call. Kept on separate lines here for
        // readability — the runtime concatenates them.
        internal const string PragmaScript =
            "PRAGMA journal_mode = WAL;" +
            "PRAGMA synchronous  = NORMAL;" +
            "PRAGMA busy_timeout = 5000;" +
            "PRAGMA foreign_keys = ON;" +
            "PRAGMA temp_store   = MEMORY;" +
            "PRAGMA mmap_size    = 67108864;" +   // 64 MB
            "PRAGMA cache_size   = -16000;";      // 16 MB (negative = kB)

        internal const string NetworkSharePragmaScript =
            "PRAGMA journal_mode = DELETE;" +     // WAL shared memory is unsafe on SMB/NAS
            "PRAGMA synchronous  = FULL;" +
            "PRAGMA busy_timeout = 15000;" +
            "PRAGMA foreign_keys = ON;" +
            "PRAGMA temp_store   = MEMORY;" +
            "PRAGMA cache_size   = -12000;";

        internal static string PragmaScriptFor(
            string? databaseFilePath, bool? sharedRootOverride = null) =>
            IsSharedRootDatabase(databaseFilePath, sharedRootOverride)
                ? NetworkSharePragmaScript
                : PragmaScript;

        private static string ReadOnlyPragmaScriptFor(bool shared) => shared
            ? "PRAGMA busy_timeout = 15000;PRAGMA foreign_keys = ON;"
            : "PRAGMA busy_timeout = 5000;PRAGMA foreign_keys = ON;";

        private static readonly ConcurrentDictionary<string, string> LoggedJournalModes =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// True when the database path is inside the configured company root,
        /// a UNC path, or a mapped network drive. This is deliberately based
        /// on the path itself/configured root, not on whether startup managed
        /// to reach the share; the policy remains non-WAL for unavailable or
        /// newly mounted shared paths too.
        /// </summary>
        internal static bool IsSharedRootDatabase(string? databaseFilePath) =>
            IsSharedRootDatabase(databaseFilePath, sharedRootOverride: null);

        private static bool IsSharedRootDatabase(
            string? databaseFilePath, bool? sharedRootOverride)
        {
            if (sharedRootOverride.HasValue) return sharedRootOverride.Value;
            if (string.IsNullOrWhiteSpace(databaseFilePath)) return false;
            try
            {
                string db = System.IO.Path.GetFullPath(databaseFilePath);
                string configuredRoot = AppDataRoot.ConfiguredSharedDataRoot;
                if (!string.IsNullOrWhiteSpace(configuredRoot))
                {
                    string root = System.IO.Path.GetFullPath(configuredRoot);
                    string rootPrefix = root.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                        || root.EndsWith(System.IO.Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                        ? root
                        : root + System.IO.Path.DirectorySeparatorChar;
                    if (db.Equals(root, StringComparison.OrdinalIgnoreCase)
                        || db.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                if (db.StartsWith(@"\\", StringComparison.Ordinal)
                    || db.StartsWith("//", StringComparison.Ordinal))
                    return true;

                return !string.IsNullOrWhiteSpace(AppDataRoot.GetMappedNetworkPath(db));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Applies the central path-aware SQLite policy and verifies the
        /// journal mode SQLite actually accepted. Runtime EF connections and
        /// raw migration/conversion connections call this exact routine.
        /// A caller can explicitly identify a known shared-root file with
        /// <paramref name="sharedRootOverride"/> when path detection alone is insufficient.
        /// </summary>
        internal static string ApplyConnectionPolicy(
            SqliteConnection connection, bool? sharedRootOverride = null)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            bool shared = IsSharedRootDatabase(connection.DataSource, sharedRootOverride);
            string requiredMode = shared ? "delete" : "wal";
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = PragmaScriptFor(connection.DataSource, sharedRootOverride);
                command.CommandType = CommandType.Text;
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    $"[SqliteResilience] Could not apply SQLite connection policy to '{connection.DataSource}'.",
                    ex,
                    new LogContext
                    {
                        Module = "Database",
                        Class = nameof(SqliteResilience),
                        Operation = "ApplyPragmas"
                    });
                if (shared)
                    throw new InvalidOperationException(
                        $"Could not apply the required non-WAL SQLite policy to shared database '{connection.DataSource}'. " +
                        "Database access is blocked to avoid unsafe shared-file writes.", ex);
            }

            return VerifyEffectiveJournalMode(connection, shared, requiredMode);
        }

        /// <summary>
        /// Applies non-mutating connection-local settings to a read-only
        /// connection, then verifies/logs its effective journal mode against
        /// the same path-aware policy. It never tries to change a database's
        /// persistent journal mode from a read-only handle.
        /// </summary>
        internal static string ApplyReadOnlyConnectionPolicy(SqliteConnection connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            bool shared = IsSharedRootDatabase(connection.DataSource);
            string requiredMode = shared ? "delete" : "wal";
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = ReadOnlyPragmaScriptFor(shared);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    $"[SqliteResilience] Could not apply read-only SQLite settings to '{connection.DataSource}'.",
                    ex,
                    new LogContext
                    {
                        Module = "Database",
                        Class = nameof(SqliteResilience),
                        Operation = "ApplyReadOnlyPragmas"
                    });
                if (shared)
                    throw new InvalidOperationException(
                        $"Could not apply the required read-only connection settings to shared database '{connection.DataSource}'. " +
                        "Database access is blocked.", ex);
            }

            return VerifyEffectiveJournalMode(connection, shared, requiredMode);
        }

        private static string VerifyEffectiveJournalMode(
            SqliteConnection connection, bool shared, string requiredMode)
        {
            string effectiveMode;
            try
            {
                using var check = connection.CreateCommand();
                check.CommandText = "PRAGMA journal_mode;";
                effectiveMode = Convert.ToString(check.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture)?.Trim().ToLowerInvariant() ?? "";
            }
            catch (Exception ex)
            {
                AppLogService.Error(
                    $"[SqliteResilience] Could not verify journal_mode for '{connection.DataSource}'.",
                    ex,
                    new LogContext
                    {
                        Module = "Database",
                        Class = nameof(SqliteResilience),
                        Operation = "VerifyJournalMode"
                    });
                if (shared)
                    throw new InvalidOperationException(
                        $"Could not verify the SQLite journal mode for shared database '{connection.DataSource}'. " +
                        "Database access is blocked.", ex);
                return "unknown";
            }

            string key = connection.DataSource ?? "";
            string modeLog = $"required={requiredMode}, effective={effectiveMode}";
            if (!LoggedJournalModes.TryGetValue(key, out var previous)
                || !string.Equals(previous, modeLog, StringComparison.OrdinalIgnoreCase))
            {
                LoggedJournalModes[key] = modeLog;
                AppLogService.Info(
                    $"SQLite journal policy for '{key}': {modeLog}.",
                    new LogContext
                    {
                        Module = "Database",
                        Class = nameof(SqliteResilience),
                        Operation = "VerifyJournalMode"
                    });
            }

            if (shared && !string.Equals(effectiveMode, "delete", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Shared database '{connection.DataSource}' is using journal_mode={effectiveMode}, " +
                    "but DELETE is required for a LAN/Wi-Fi data root. Database access is blocked. " +
                    "Close every ProGlass workstation, make a backup, and retry. If the journal mode " +
                    "still cannot be changed safely, contact ProGlass support before using this folder.");

            if (!shared && !string.Equals(effectiveMode, "wal", StringComparison.OrdinalIgnoreCase))
                AppLogService.Warning(
                    $"Local SQLite database '{connection.DataSource}' requested journal_mode=WAL but SQLite reports '{effectiveMode}'.",
                    new LogContext
                    {
                        Module = "Database",
                        Class = nameof(SqliteResilience),
                        Operation = "VerifyJournalMode"
                    });

            return effectiveMode;
        }

        // ── Public helpers ──────────────────────────────────────────────

        /// <summary>
        /// Preferred connection-string builder. Local paths use a shared page
        /// cache; configured/UNC/mapped shared roots use the default cache and
        /// the central non-WAL journal policy. The returned connection string
        /// never chooses a journal mode itself; ApplyConnectionPolicy owns and
        /// verifies that choice after open.
        /// </summary>
        public static string BuildConnectionString(string databaseFilePath) =>
            BuildConnectionStringCore(
                databaseFilePath, SqliteOpenMode.ReadWriteCreate, sharedRootOverride: null);

        /// <summary>
        /// Builds a connection for an explicitly identified shared-root file,
        /// even when path detection alone cannot classify the location.
        /// </summary>
        internal static string BuildSharedRootConnectionString(string databaseFilePath) =>
            BuildConnectionStringCore(
                databaseFilePath, SqliteOpenMode.ReadWriteCreate, sharedRootOverride: true);

        /// <summary>
        /// Builds a read-only connection string with the same cache choice,
        /// timeout, and path-aware shared-root classification as write paths.
        /// </summary>
        internal static string BuildReadOnlyConnectionString(string databaseFilePath) =>
            BuildConnectionStringCore(
                databaseFilePath, SqliteOpenMode.ReadOnly, sharedRootOverride: null);

        private static string BuildConnectionStringCore(
            string databaseFilePath,
            SqliteOpenMode mode,
            bool? sharedRootOverride)
        {
            if (string.IsNullOrWhiteSpace(databaseFilePath))
                throw new ArgumentException("Database file path is required.", nameof(databaseFilePath));

            bool shared = IsSharedRootDatabase(databaseFilePath, sharedRootOverride);
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = databaseFilePath,
                Mode = mode,
                Cache = shared ? SqliteCacheMode.Default : SqliteCacheMode.Shared,
                DefaultTimeout = 30,
                ForeignKeys = null
            };
            return builder.ToString();
        }

        /// <summary>
        /// The single EF Core interceptor every DbContext hooks in
        /// OnConfiguring. Applies the pragma script to each new physical
        /// connection immediately after open.
        /// </summary>
        public static readonly IInterceptor PragmaInterceptor = new SqlitePragmaInterceptor();

        /// <summary>
        /// Convenience: apply UseSqlite + PragmaInterceptor in one call.
        /// The one line every DbContext.OnConfiguring should call.
        /// </summary>
        public static DbContextOptionsBuilder ConfigureFor(
            DbContextOptionsBuilder options, string databaseFilePath)
        {
            options.UseSqlite(BuildConnectionString(databaseFilePath));
            options.AddInterceptors(PragmaInterceptor);
            return options;
        }

        /// <summary>
        /// Retry SaveChangesAsync on transient SQLite lock errors
        /// (SQLITE_BUSY / SQLITE_LOCKED). Non-transient failures
        /// (UNIQUE / NOT NULL / FOREIGN KEY / CHECK) surface
        /// immediately — the caller must handle them explicitly
        /// (typically by regenerating a key or reporting to the user).
        /// </summary>
        /// <param name="save">Delegate that performs the SaveChanges call.
        ///   Typically <c>ct => db.SaveChangesAsync(ct)</c>.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <param name="maxAttempts">Maximum total attempts (default 5).</param>
        /// <returns>The number of rows written on the successful attempt.</returns>
        public static async Task<int> SaveWithRetryAsync(
            Func<CancellationToken, Task<int>> save,
            CancellationToken ct = default,
            int maxAttempts = 5)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (maxAttempts < 1) maxAttempts = 1;

            int attempt = 0;
            while (true)
            {
                attempt++;
                try
                {
                    return await save(ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (attempt < maxAttempts && SqliteErrorClassifier.IsTransient(ex))
                {
                    // Exponential back-off with cap at 800 ms. The
                    // SQLite writer usually clears within a single tick;
                    // longer waits handle checkpoint stalls on WAL.
                    int delayMs = Math.Min(800, 40 * (1 << (attempt - 1)));
                    AppLogService.Debug(
                        $"[SqliteResilience] transient SQLite error on attempt {attempt}/{maxAttempts} " +
                        $"(code={SqliteErrorClassifier.Classify(ex)}): {ex.Message} — retrying in {delayMs} ms.",
                        new LogContext
                        {
                            Module = "Database",
                            Class = nameof(SqliteResilience),
                            Operation = nameof(SaveWithRetryAsync)
                        });
                    try { await Task.Delay(delayMs, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { throw; }
                }
            }
        }

        /// <summary>
        /// Runs a bulk write (<c>ExecuteUpdateAsync</c> /
        /// <c>ExecuteDeleteAsync</c>) under the same transient-error retry as
        /// <see cref="SaveWithRetryAsync"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This helper only retries transient SQLITE_BUSY / SQLITE_LOCKED
        /// failures. Callers still need an atomic SQL statement or a serializable
        /// transaction for correctness; retrying alone does not prevent lost
        /// updates from an application-level read-modify-write.
        /// </para>
        /// <para>
        /// The retry policy is shared rather than duplicated: a bulk write hits
        /// the same SQLITE_BUSY and WAL checkpoint stalls as a SaveChanges, and
        /// needs the same back-off. This is a thin alias so the loop lives in one
        /// place and both kinds of write age together.
        /// </para>
        /// <para>
        /// Note what a bulk write does NOT do: it bypasses change tracking and
        /// any SaveChanges interceptor, so computed properties, audit stamps and
        /// domain validation attached to those paths do not run. Every column
        /// the operation should touch must be named in the setter list. Verified
        /// when this was introduced — this codebase has no
        /// <c>SaveChanges</c> override and no SaveChanges interceptor, only the
        /// PRAGMA connection interceptor in <see cref="ConfigureFor"/>, so
        /// nothing was being skipped. Re-check that if either is ever added.
        /// </para>
        /// </remarks>
        /// <param name="execute">Delegate performing the bulk write. Typically
        ///   <c>ct =&gt; db.Entity.Where(...).ExecuteUpdateAsync(..., ct)</c>.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <param name="maxAttempts">Maximum total attempts (default 5).</param>
        /// <returns>The number of rows affected on the successful attempt. Zero
        ///   means the WHERE clause matched nothing — which for a guarded
        ///   increment is the guard refusing, not an error.</returns>
        public static Task<int> ExecuteWithRetryAsync(
            Func<CancellationToken, Task<int>> execute,
            CancellationToken ct = default,
            int maxAttempts = 5)
            => SaveWithRetryAsync(execute, ct, maxAttempts);

        // ── Interceptor implementation ──────────────────────────────────

        private sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
        {
            public override void ConnectionOpened(
                DbConnection connection, ConnectionEndEventData eventData)
            {
                ApplyPragmas(connection);
                base.ConnectionOpened(connection, eventData);
            }

            public override Task ConnectionOpenedAsync(
                DbConnection connection,
                ConnectionEndEventData eventData,
                CancellationToken cancellationToken = default)
            {
                ApplyPragmas(connection);
                return base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
            }

            private static void ApplyPragmas(DbConnection connection)
            {
                // This interceptor is shared by all EF contexts. Raw
                // migration connections call ApplyConnectionPolicy directly,
                // so runtime and migration policy cannot drift.
                if (connection is SqliteConnection sqlite)
                    ApplyConnectionPolicy(sqlite);
            }
        }
    }

    /// <summary>
    /// Classifies a SQLite error into one of a small set of categories
    /// the app cares about. Used by <see cref="SqliteResilience.SaveWithRetryAsync"/>
    /// to distinguish retry-able from terminal failures, and by
    /// repositories to raise clean user-visible messages.
    /// </summary>
    public enum SqliteErrorKind
    {
        /// <summary>Not a SQLite error, or an unrecognised category.</summary>
        Other = 0,
        /// <summary>SQLITE_BUSY (5) — another process/thread holds a lock. Retry-able.</summary>
        Busy = 5,
        /// <summary>SQLITE_LOCKED (6) — a lock inside the same connection. Retry-able.</summary>
        Locked = 6,
        /// <summary>SQLITE_CORRUPT (11) — the database file is damaged. Terminal.</summary>
        Corrupt = 11,
        /// <summary>SQLITE_READONLY (8) — write attempted on read-only media/mount.</summary>
        ReadOnly = 8,
        /// <summary>SQLITE_CONSTRAINT (19) generic — NOT NULL / CHECK / FOREIGN KEY / UNIQUE.</summary>
        Constraint = 19,
        /// <summary>SQLITE_CONSTRAINT_UNIQUE (2067) — UNIQUE index violation.</summary>
        UniqueViolation = 2067,
        /// <summary>SQLITE_CONSTRAINT_FOREIGNKEY (787) — cascade / restrict FK violation.</summary>
        ForeignKeyViolation = 787,
        /// <summary>SQLITE_CONSTRAINT_NOTNULL (1299) — NOT NULL violated.</summary>
        NotNullViolation = 1299,
        /// <summary>SQLITE_CONSTRAINT_CHECK (275) — CHECK violated.</summary>
        CheckViolation = 275,
    }

    /// <summary>
    /// Pure functions that inspect an exception (typically the InnerException
    /// of a <see cref="DbUpdateException"/>) and report which SQLite category
    /// it maps to.
    /// </summary>
    public static class SqliteErrorClassifier
    {
        /// <summary>Walks the exception chain looking for a <see cref="SqliteException"/>.
        /// Returns <see cref="SqliteErrorKind.Other"/> if none is found.</summary>
        public static SqliteErrorKind Classify(Exception? exception)
        {
            var sqlite = Find(exception);
            if (sqlite == null) return SqliteErrorKind.Other;

            // Prefer the extended code (SqliteExtendedErrorCode) — it
            // distinguishes SQLITE_CONSTRAINT_UNIQUE from the generic
            // SQLITE_CONSTRAINT. Fall back to the base code when the
            // extended one is not populated.
            int extended = sqlite.SqliteExtendedErrorCode;
            switch (extended)
            {
                case 2067: return SqliteErrorKind.UniqueViolation;
                case 1555: return SqliteErrorKind.UniqueViolation;   // SQLITE_CONSTRAINT_PRIMARYKEY
                case 787:  return SqliteErrorKind.ForeignKeyViolation;
                case 1299: return SqliteErrorKind.NotNullViolation;
                case 275:  return SqliteErrorKind.CheckViolation;
            }

            switch (sqlite.SqliteErrorCode)
            {
                case 5:  return SqliteErrorKind.Busy;
                case 6:  return SqliteErrorKind.Locked;
                case 8:  return SqliteErrorKind.ReadOnly;
                case 11: return SqliteErrorKind.Corrupt;
                case 19:
                    // Extended code was not populated (older driver) —
                    // sniff the message as a last resort. Message-based
                    // detection is inherently fragile, so we cover only
                    // the two we care about in retry loops.
                    string msg = sqlite.Message ?? "";
                    if (msg.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase))
                        return SqliteErrorKind.UniqueViolation;
                    if (msg.Contains("FOREIGN KEY constraint failed", StringComparison.OrdinalIgnoreCase))
                        return SqliteErrorKind.ForeignKeyViolation;
                    if (msg.Contains("NOT NULL constraint failed", StringComparison.OrdinalIgnoreCase))
                        return SqliteErrorKind.NotNullViolation;
                    if (msg.Contains("CHECK constraint failed", StringComparison.OrdinalIgnoreCase))
                        return SqliteErrorKind.CheckViolation;
                    return SqliteErrorKind.Constraint;
            }
            return SqliteErrorKind.Other;
        }

        /// <summary>Convenience: true iff <see cref="Classify"/> returns Busy or Locked.</summary>
        public static bool IsTransient(Exception? exception)
        {
            var kind = Classify(exception);
            return kind == SqliteErrorKind.Busy || kind == SqliteErrorKind.Locked;
        }

        /// <summary>Convenience: true iff <see cref="Classify"/> returns UniqueViolation.</summary>
        public static bool IsUnique(Exception? exception) =>
            Classify(exception) == SqliteErrorKind.UniqueViolation;

        /// <summary>Convenience: true iff <see cref="Classify"/> returns Corrupt or ReadOnly.</summary>
        public static bool IsTerminal(Exception? exception)
        {
            var kind = Classify(exception);
            return kind == SqliteErrorKind.Corrupt || kind == SqliteErrorKind.ReadOnly;
        }

        /// <summary>Walks the InnerException chain, up to a small depth, looking for a SqliteException.</summary>
        private static SqliteException? Find(Exception? exception)
        {
            for (int depth = 0; depth < 8 && exception != null; depth++)
            {
                if (exception is SqliteException s) return s;
                exception = exception.InnerException;
            }
            return null;
        }
    }
}
