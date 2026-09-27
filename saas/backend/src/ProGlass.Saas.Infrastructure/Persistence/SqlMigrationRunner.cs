using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ProGlass.Saas.Infrastructure.Persistence;

/// <summary>
/// SQL-first migration runner (ADR-008). The .sql files in
/// saas/database/migrations are the SOURCE OF TRUTH for the schema — the
/// same files the verification harness (saas/database/verify/apply_and_test.py)
/// applies and tests against live PostgreSQL. EF Core maps to that schema
/// but never creates or migrates it.
///
/// Semantics (identical to the Python harness — the two are kept in lockstep
/// deliberately so the harness proves exactly what production runs):
///   • migrations apply in filename order, each inside ONE transaction
///   • applied files are recorded in saas._saas_migrations with a SHA-256
///   • re-running is a no-op
///   • a checksum mismatch on an already-applied file is a HARD error —
///     never edit an applied migration; add a new one instead
///
/// Sources: an optional filesystem directory (dev — checked first) and the
/// embedded resources compiled into this assembly (deployment).
/// </summary>
public sealed class SqlMigrationRunner
{
    private const string ResourcePrefix = "ProGlass.Saas.Infrastructure.Migrations.";

    private readonly string _connectionString;
    private readonly string? _migrationsDirectory;
    private readonly ILogger<SqlMigrationRunner>? _logger;

    public SqlMigrationRunner(
        string connectionString,
        string? migrationsDirectory = null,
        ILogger<SqlMigrationRunner>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required.", nameof(connectionString));
        _connectionString = connectionString;
        _migrationsDirectory = migrationsDirectory;
        _logger = logger;
    }

    /// <summary>Applies every pending migration; returns the applied file names.</summary>
    public async Task<IReadOnlyList<string>> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var migrations = LoadMigrations();
        if (migrations.Count == 0)
            throw new InvalidOperationException(
                "No migration files found (neither on disk nor embedded). " +
                "Expected names like 0001_platform_identity.sql.");

        var applied = new List<string>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await BootstrapAsync(connection, cancellationToken).ConfigureAwait(false);

        var done = await LoadAppliedAsync(connection, cancellationToken).ConfigureAwait(false);

        foreach (var (name, sql, checksum) in migrations)
        {
            if (done.TryGetValue(name, out var recordedChecksum))
            {
                if (!string.Equals(recordedChecksum, checksum, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Checksum mismatch for already-applied migration '{name}'. " +
                        "Never edit an applied migration; add a new one instead.");
                continue;
            }

            await using var transaction =
                await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                await using (var record = connection.CreateCommand())
                {
                    record.Transaction = transaction;
                    record.CommandText =
                        "INSERT INTO saas._saas_migrations (name, checksum) VALUES ($1, $2)";
                    record.Parameters.AddWithValue(name);
                    record.Parameters.AddWithValue(checksum);
                    await record.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }

            applied.Add(name);
            _logger?.LogInformation("Applied migration {MigrationName}", name);
        }

        return applied;
    }

    /// <summary>
    /// Idempotent bootstrap: schema, runtime roles and the bookkeeping table.
    /// In production the connecting user is expected to own the schema; the
    /// role creation is best-effort (it needs CREATEROLE) and is skipped
    /// with a warning when unavailable — the DBA pre-creates the roles.
    /// </summary>
    private async Task BootstrapAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        await ExecuteAsync(connection, "CREATE SCHEMA IF NOT EXISTS saas", ct).ConfigureAwait(false);
        await ExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS saas._saas_migrations (
                name text PRIMARY KEY,
                checksum text NOT NULL,
                applied_at timestamptz NOT NULL DEFAULT now()
            )
            """, ct).ConfigureAwait(false);

        try
        {
            await ExecuteAsync(connection, """
                DO $$
                BEGIN
                  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'saas_owner') THEN
                    CREATE ROLE saas_owner NOLOGIN;
                  END IF;
                  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'saas_app') THEN
                    CREATE ROLE saas_app NOLOGIN;
                  END IF;
                END $$;
                """, ct).ConfigureAwait(false);
        }
        catch (PostgresException ex)
        {
            _logger?.LogWarning(ex,
                "Could not verify/create the saas_owner/saas_app roles " +
                "(no CREATEROLE privilege?). They must pre-exist for 0005_rls.sql.");
        }
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task<Dictionary<string, string>> LoadAppliedAsync(
        NpgsqlConnection connection, CancellationToken ct)
    {
        var done = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, checksum FROM saas._saas_migrations";
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            done[reader.GetString(0)] = reader.GetString(1);
        }
        return done;
    }

    /// <summary>Filesystem migrations override embedded ones with the same name.</summary>
    private List<(string Name, string Sql, string Checksum)> LoadMigrations()
    {
        var byName = new SortedDictionary<string, (string Sql, string Checksum)>(
            StringComparer.Ordinal);

        var assembly = typeof(SqlMigrationRunner).Assembly;
        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                continue;
            var name = resourceName.Substring(ResourcePrefix.Length);
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Missing resource {resourceName}");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var sql = reader.ReadToEnd();
            byName[name] = (sql, Sha256(sql));
        }

        if (_migrationsDirectory is not null)
        {
            foreach (var file in Directory.GetFiles(_migrationsDirectory, "*.sql")
                         .OrderBy(f => f, StringComparer.Ordinal))
            {
                var sql = File.ReadAllText(file);
                byName[Path.GetFileName(file)] = (sql, Sha256(sql));
            }
        }

        return byName
            .Select(kv => (kv.Key, kv.Value.Sql, kv.Value.Checksum))
            .ToList();
    }

    private static string Sha256(string sql) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
}
