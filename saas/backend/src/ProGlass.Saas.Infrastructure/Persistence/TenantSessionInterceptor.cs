using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProGlass.Saas.Domain.Tenancy;

namespace ProGlass.Saas.Infrastructure.Persistence;

/// <summary>
/// Sets the PostgreSQL session GUC <c>app.tenant_id</c> every time EF opens
/// a connection, which drives the Row-Level Security policies (migration
/// 0005_rls.sql). This is the database half of tenant isolation; the EF
/// global query filter (SaasDbContext) is the application half.
///
/// Pool safety: Npgsql issues DISCARD ALL when a connection returns to the
/// pool, clearing session GUCs — a leaked tenant id cannot survive on a
/// reused connection. Unset/empty GUC fails CLOSED (RLS NULLIF policy).
///
/// Phase 4's auth middleware is the only writer of the
/// <see cref="ITenantContext"/> this reads; the tenant id never comes from
/// client input.
/// </summary>
public sealed class TenantSessionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenant;

    public TenantSessionInterceptor(ITenantContext tenant)
    {
        _tenant = tenant ?? throw new ArgumentNullException(nameof(tenant));
    }

    public override void ConnectionOpened(
        DbConnection connection, ConnectionEndEventData eventData)
    {
        SetTenantContext(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        SetTenantContext(connection);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken)
            .ConfigureAwait(false);
    }

    private void SetTenantContext(DbConnection connection)
    {
        if (_tenant.TenantId == Guid.Empty)
            return; // nothing to set — RLS fails closed on the unset GUC

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.tenant_id', @tid, false)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tid";
        parameter.Value = _tenant.TenantId.ToString();
        command.Parameters.Add(parameter);
        command.ExecuteNonQuery();
    }
}
