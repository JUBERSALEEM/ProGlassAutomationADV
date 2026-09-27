using Npgsql;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// SqlMigrationRunner semantics — the .NET twin of the Python harness's
/// migration checks: idempotent re-runs and tamper detection.
/// </summary>
[Collection("database")]
public class MigrationRunnerTests
{
    private readonly DatabaseFixture _fixture;

    public MigrationRunnerTests(DatabaseFixture fixture) => _fixture = fixture;

    [DatabaseFact]
    public async Task Second_run_applies_nothing()
    {
        var runner = new SqlMigrationRunner(_fixture.OwnerDb);
        var applied = await runner.ApplyAsync();
        Assert.Empty(applied);
    }

    [DatabaseFact]
    public async Task Tampered_checksum_is_a_hard_error()
    {
        const string migration = "0001_platform_identity.sql";
        string? original = null;

        await using (var connection = new NpgsqlConnection(_fixture.OwnerDb))
        {
            await connection.OpenAsync();
            using (var read = connection.CreateCommand())
            {
                read.CommandText = "SELECT checksum FROM saas._saas_migrations WHERE name = $1";
                read.Parameters.AddWithValue(migration);
                original = (string?)await read.ExecuteScalarAsync();
            }
            Assert.NotNull(original);

            using (var tamper = connection.CreateCommand())
            {
                tamper.CommandText = "UPDATE saas._saas_migrations SET checksum = 'deadbeef' WHERE name = $1";
                tamper.Parameters.AddWithValue(migration);
                await tamper.ExecuteNonQueryAsync();
            }
        }

        try
        {
            var runner = new SqlMigrationRunner(_fixture.OwnerDb);
            await Assert.ThrowsAsync<InvalidOperationException>(() => runner.ApplyAsync());
        }
        finally
        {
            await using var connection = new NpgsqlConnection(_fixture.OwnerDb);
            await connection.OpenAsync();
            using var restore = connection.CreateCommand();
            restore.CommandText = "UPDATE saas._saas_migrations SET checksum = $1 WHERE name = $2";
            restore.Parameters.AddWithValue(original!);
            restore.Parameters.AddWithValue(migration);
            await restore.ExecuteNonQueryAsync();
        }
    }
}
