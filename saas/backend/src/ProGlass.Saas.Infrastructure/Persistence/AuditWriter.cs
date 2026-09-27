using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Infrastructure.Persistence;

/// <summary>
/// IAuditWriter implementation. Events WITH a tenant go to
/// saas.audit_logs (tenant-bound context → RLS WITH CHECK passes; the
/// runtime role holds INSERT-only on that table, 0005_rls.sql — the audit
/// trail cannot be rewritten by the application).
///
/// Events WITHOUT a tenant (e.g. a login attempt against an unknown
/// company code) cannot pass RLS — there is no tenant context that would
/// accept the row — so they go to the APPLICATION LOG instead. Never
/// silently dropped (§ audit parity with WPF ActivityLog).
/// </summary>
public sealed class AuditWriter : IAuditWriter
{
    private readonly ISaasDbContextFactory _factory;
    private readonly ILogger<AuditWriter> _logger;

    public AuditWriter(ISaasDbContextFactory factory, ILogger<AuditWriter> logger)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task WriteAsync(
        Guid? tenantId, Guid? userId, string action,
        string entityType, Guid? entityId, string? ip, string? correlationId,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(action))
            throw new ArgumentException("Audit action is required.", nameof(action));

        if (tenantId is null || tenantId == Guid.Empty)
        {
            _logger.LogWarning(
                "Auth audit event without tenant context (action {Action}, user {UserId}, " +
                "ip {Ip}, entity {EntityType}:{EntityId}) — recorded here, not in audit_logs " +
                "(row-level security scopes audit_logs to a tenant).",
                action, userId, ip ?? "unknown", entityType, entityId);
            return;
        }

        using var context = _factory.CreateForTenant(tenantId.Value);
        context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Ip = ip,
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
