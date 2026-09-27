using Microsoft.EntityFrameworkCore;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Infrastructure.Persistence;

/// <summary>
/// IUserStore implementation over short-lived DbContexts from
/// <see cref="ISaasDbContextFactory"/>. Every tenant-owned read is
/// tenant-explicit: the context is built bound to the target tenant, so a
/// wrong tenant id simply finds nothing (query filter + RLS both fail
/// closed). Slug lookups read the platform table saas.tenants (NOT
/// row-secured, 0005_rls.sql) through a platform context.
///
/// Lockout POLICY lives in AuthService; this class only persists what it
/// is told (mechanical, per the port contract).
/// </summary>
public sealed class UserStore : IUserStore
{
    private readonly ISaasDbContextFactory _factory;

    public UserStore(ISaasDbContextFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<Guid?> FindTenantIdBySlugAsync(string tenantSlug, CancellationToken ct)
    {
        using var context = _factory.CreatePlatform();
        var normalized = tenantSlug.Trim().ToLowerInvariant();
        return await context.Tenants
            .Where(t => t.Slug == normalized && t.Status == "active" && t.DeletedAt == null)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<LoginUser?> FindLoginAsync(
        Guid tenantId, string usernameOrEmail, CancellationToken ct)
    {
        using var context = _factory.CreateForTenant(tenantId);
        var login = usernameOrEmail.Trim();
        var email = login.ToLowerInvariant();
        var user = await context.Users
            .Where(u => u.Username == login || u.Email == email)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return user is null ? null : ToLoginUser(user);
    }

    public async Task<LoginUser?> FindByIdAsync(
        Guid tenantId, Guid userId, CancellationToken ct)
    {
        using var context = _factory.CreateForTenant(tenantId);
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            .ConfigureAwait(false);
        return user is null ? null : ToLoginUser(user);
    }

    public async Task SaveLoginStateAsync(
        Guid tenantId, Guid userId,
        int failedAttemptCount, DateTime? lockedUntilUtc, DateTime? lastLoginAtUtc,
        CancellationToken ct)
    {
        using var context = _factory.CreateForTenant(tenantId);
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"User {userId} disappeared while saving login state.");

        user.FailedAttemptCount = failedAttemptCount;
        user.LockedUntilUtc = lockedUntilUtc;
        if (lastLoginAtUtc is not null)
            user.LastLoginAt = lastLoginAtUtc;
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdatePasswordHashAsync(
        Guid tenantId, Guid userId, string passwordHash,
        bool clearMustResetPassword, CancellationToken ct)
    {
        using var context = _factory.CreateForTenant(tenantId);
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"User {userId} disappeared while updating the password hash.");

        user.PasswordHash = passwordHash;
        if (clearMustResetPassword)
            user.MustResetPassword = false;
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> IsSlugTakenAsync(string tenantSlug, CancellationToken ct)
    {
        using var context = _factory.CreatePlatform();
        var normalized = tenantSlug.Trim().ToLowerInvariant();
        return await context.Tenants
            .AnyAsync(t => t.Slug == normalized, ct)
            .ConfigureAwait(false);
    }

    private static LoginUser ToLoginUser(User user) => new(
        user.TenantId, user.Id, user.Username, user.DisplayName,
        user.Email ?? "", user.Role, user.PasswordHash,
        user.IsActive, user.MustResetPassword,
        user.FailedAttemptCount, user.LockedUntilUtc, user.LastLoginAt);
}
