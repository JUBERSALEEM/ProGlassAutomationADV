// ═══════════════════════════════════════════════════════════════════════
// GENERATED FILE — DO NOT EDIT BY HAND
// Source of truth: saas/database/migrations/*.sql (verified by
// saas/database/verify/apply_and_test.py against live PostgreSQL).
// Regenerate with: python3 saas/devbox/generate_ef_model.py
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Tenancy;

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.users.</summary>
    public sealed class User : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Username { get; set; } = null!;
        public string DisplayName { get; set; } = null!;
        public string? Email { get; set; }
        public string Mobile { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string Role { get; set; } = null!;
        public bool IsActive { get; set; }
        public int FailedAttemptCount { get; set; }
        public DateTime? LockedUntilUtc { get; set; }
        public string? MfaSecretEncrypted { get; set; }
        public DateTime? EmailConfirmedAt { get; set; }
        public bool MustResetPassword { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.refresh_tokens.</summary>
    public sealed class RefreshToken : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public string TokenHash { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? ReplacedByTokenHash { get; set; }
        public string? CreatedIp { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime AbsoluteExpiresAtUtc { get; set; }
        public User User { get; set; } = null!;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.audit_logs.</summary>
    public sealed class AuditLog : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid? UserId { get; set; }
        public string Action { get; set; } = null!;
        public string EntityType { get; set; } = null!;
        public Guid? EntityId { get; set; }
        public string? BeforeJson { get; set; }
        public string? AfterJson { get; set; }
        public string? Ip { get; set; }
        public string? CorrelationId { get; set; }
        public DateTime CreatedAt { get; set; }
        public User? User { get; set; } = null;
    }
}
