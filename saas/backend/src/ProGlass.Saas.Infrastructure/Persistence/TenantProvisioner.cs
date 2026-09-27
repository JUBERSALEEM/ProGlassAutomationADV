using Microsoft.Extensions.Logging;
using Npgsql;
using ProGlass.Saas.Application.Auth;

namespace ProGlass.Saas.Infrastructure.Persistence;

/// <summary>
/// Creates a tenant and its Owner (first user of role <see cref="ProGlass.Saas.Domain.Authorization.AppRoles.Administrator"/>
/// user) on the ELEVATED platform connection — the runtime role saas_app
/// cannot INSERT into saas.tenants by design (0005_rls.sql), so
/// registration cannot run over the application connection (ADR-011).
///
/// Everything runs in ONE transaction; the RLS session GUC is set with
/// set_config(..., true) — transaction-scoped — before the users INSERT,
/// so the insert satisfies the FORCE ROW LEVEL SECURITY tenant_isolation
/// policy both as superuser (bypass) and as a non-superuser owner/migration
/// role (policy match). The GUC reverts at COMMIT/ROLLBACK.
///
/// Registration is PUBLIC: slugs are normalized (lowercase, trimmed) by
/// AuthService before this runs; the uq_tenants_slug constraint makes a
/// concurrent duplicate registration fail the transaction ( AuthService
/// pre-checks IsSlugTakenAsync for a friendly error, the constraint is the
/// race-safe backstop).
/// </summary>
public sealed class TenantProvisioner : ITenantProvisioner
{
    /// <summary>First user of a new tenant — the WPF role catalog has no
    /// "owner" role; Administrator is the full-access set (WPF verbatim).</summary>
    public const string OwnerRole = "Administrator";

    /// <summary>WPF LicenseService.AutoTrialDays = 30 — the automatic
    /// evaluation window every new tenant starts with (the
    /// AutoTrialManager port, Phase 13).</summary>
    public const int TrialDays = 30;

    private readonly string _platformConnectionString;
    private readonly ILogger<TenantProvisioner> _logger;

    public TenantProvisioner(string platformConnectionString,
        ILogger<TenantProvisioner> logger)
    {
        if (string.IsNullOrWhiteSpace(platformConnectionString))
            throw new ArgumentException(
                "Platform connection string (SAAS_PLATFORM_DB) is required — the " +
                "runtime role cannot insert into saas.tenants.",
                nameof(platformConnectionString));
        _platformConnectionString = platformConnectionString;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProvisionedTenant> ProvisionAsync(
        string tenantName, string tenantSlug,
        string ownerUsername, string ownerDisplayName,
        string ownerEmail, string ownerPasswordHash,
        string? ip, CancellationToken ct)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var connection = new NpgsqlConnection(_platformConnectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct)
            .ConfigureAwait(false);

        try
        {
            // Transaction-scoped tenant context for the INSERTs below
            // (reverts on COMMIT/ROLLBACK — no leakage to the pool).
            await ExecuteAsync(connection, transaction, ct,
                "SELECT set_config('app.tenant_id', @tid, true)",
                ("tid", tenantId.ToString())).ConfigureAwait(false);

            await ExecuteAsync(connection, transaction, ct,
                """
                INSERT INTO saas.tenants (id, name, slug, status, created_at)
                VALUES (@id, @name, @slug, 'active', @now)
                """,
                ("id", tenantId), ("name", tenantName),
                ("slug", tenantSlug), ("now", now)).ConfigureAwait(false);

            await ExecuteAsync(connection, transaction, ct, """
                INSERT INTO saas.users
                    (id, tenant_id, username, display_name, email, role,
                     password_hash, is_active, must_reset_password,
                     failed_attempt_count, created_at)
                VALUES (@id, @tid, @username, @displayName, @email, @role,
                        @passwordHash, true, false, 0, @now)
                """,
                ("id", userId), ("tid", tenantId),
                ("username", ownerUsername), ("displayName", ownerDisplayName),
                ("email", ownerEmail), ("role", OwnerRole),
                ("passwordHash", ownerPasswordHash), ("now", now)).ConfigureAwait(false);

            // Phase 13 — the AutoTrialManager port: every new tenant gets
            // a 30-day evaluation trial (WPF LicenseService.AutoTrialDays
            // = 30) on the 'trial' plan, ALL features on (the WPF trial
            // edition: "30-day, single-activation, all features"). Seeded
            // by migration 0017; granted here in the SAME transaction — a
            // tenant without its trial row would read as hard-blocked.
            await ExecuteAsync(connection, transaction, ct,
                """
                INSERT INTO saas.subscriptions
                  (id, tenant_id, plan_id, status, period_start, period_end,
                   trial_ends_at, created_at, updated_at)
                SELECT @id, @tid, p.id, 'trialing', CURRENT_DATE,
                       CURRENT_DATE + @trialDays::int, @trialEnd, @now, @now
                FROM saas.plans p
                WHERE p.code = 'trial'
                """,
                ("id", Guid.NewGuid()), ("tid", tenantId),
                ("trialDays", TrialDays),
                ("trialEnd", now.AddDays(TrialDays)), ("now", now))
                .ConfigureAwait(false);

            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }

        _logger.LogInformation(
            "Provisioned tenant {TenantId} (slug {Slug}) with owner {UserId} from {Ip}",
            tenantId, tenantSlug, userId, ip ?? "unknown");
        return new ProvisionedTenant(tenantId, userId);
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken ct, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
