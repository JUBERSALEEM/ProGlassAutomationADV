using Microsoft.EntityFrameworkCore;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Infrastructure.Persistence;

/// <summary>
/// IRefreshTokenStore implementation over short-lived tenant-bound
/// DbContexts (SaasDbContextFactory). Stores rows exactly as told — the
/// ROTATION, REUSE-DETECTION and ABSOLUTE-LIFETIME policy lives in
/// AuthService (single tested place). Column semantics map 1:1 to
/// migration 0001/0006: token_hash is the SHA-256 hex of the token's
/// secret, replaced_by_token_hash links a rotated token to its successor
/// (empty string is normalized to NULL — a terminal revocation has no
/// successor).
/// </summary>
public sealed class RefreshTokenStore : IRefreshTokenStore
{
    private readonly ISaasDbContextFactory _factory;

    public RefreshTokenStore(ISaasDbContextFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task CreateAsync(
        Guid tenantId, Guid userId, string tokenHash,
        DateTime expiresAtUtc, DateTime absoluteExpiresAtUtc, string? createdIp,
        CancellationToken ct)
    {
        using var context = _factory.CreateForTenant(tenantId);
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAtUtc,
            AbsoluteExpiresAtUtc = absoluteExpiresAtUtc,
            CreatedIp = createdIp,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<RefreshTokenRecord?> FindActiveByHashAsync(
        Guid tenantId, string tokenHash, CancellationToken ct)
    {
        using var context = _factory.CreateForTenant(tenantId);
        var token = await context.RefreshTokens
            .Where(r => r.TokenHash == tokenHash && r.RevokedAt == null
                        && r.ExpiresAt > DateTime.UtcNow)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return token is null ? null : ToRecord(token);
    }

    public async Task<RefreshTokenRecord?> FindAnyByHashAsync(
        Guid tenantId, string tokenHash, CancellationToken ct)
    {
        using var context = _factory.CreateForTenant(tenantId);
        var token = await context.RefreshTokens
            .Where(r => r.TokenHash == tokenHash)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return token is null ? null : ToRecord(token);
    }

    public async Task RevokeAsync(
        Guid tenantId, Guid id, string replacedByTokenHash, CancellationToken ct)
    {
        using var context = _factory.CreateForTenant(tenantId);
        await context.RefreshTokens
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.RevokedAt, DateTime.UtcNow)
                .SetProperty(r => r.ReplacedByTokenHash,
                    string.IsNullOrEmpty(replacedByTokenHash) ? null : replacedByTokenHash),
                ct)
            .ConfigureAwait(false);
    }

    public async Task<int> RevokeAllForUserAsync(
        Guid tenantId, Guid userId, CancellationToken ct)
    {
        using var context = _factory.CreateForTenant(tenantId);
        return await context.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.RevokedAt, DateTime.UtcNow), ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RefreshTokenRecord>> ListForUserAsync(
        Guid tenantId, Guid userId, CancellationToken ct)
    {
        using var context = _factory.CreateForTenant(tenantId);
        var rows = await context.RefreshTokens
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return rows.Select(ToRecord).ToList();
    }

    private static RefreshTokenRecord ToRecord(RefreshToken r) => new(
        r.Id, r.TenantId, r.UserId, r.TokenHash,
        r.CreatedAt, r.ExpiresAt, r.AbsoluteExpiresAtUtc,
        r.RevokedAt, r.ReplacedByTokenHash, r.CreatedIp);
}
