// ═══════════════════════════════════════════════════════════════════════
// GENERATED FILE — DO NOT EDIT BY HAND
// Source of truth: saas/database/migrations/*.sql (verified by
// saas/database/verify/apply_and_test.py against live PostgreSQL).
// Regenerate with: python3 saas/devbox/generate_ef_model.py
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Infrastructure.Persistence.Configuration
{
    public sealed class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("users", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.Username).HasColumnName("username").HasColumnType("text");
            builder.Property(x => x.DisplayName).HasColumnName("display_name").HasColumnType("text");
            builder.Property(x => x.Email).HasColumnName("email").HasColumnType("text");
            builder.Property(x => x.Mobile).HasColumnName("mobile").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.PasswordHash).HasColumnName("password_hash").HasColumnType("text");
            builder.Property(x => x.Role).HasColumnName("role").HasColumnType("text").HasDefaultValue("Viewer");
            builder.Property(x => x.IsActive).HasColumnName("is_active").HasColumnType("boolean").HasDefaultValue(true);
            builder.Property(x => x.FailedAttemptCount).HasColumnName("failed_attempt_count").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.LockedUntilUtc).HasColumnName("locked_until_utc").HasColumnType("timestamptz");
            builder.Property(x => x.MfaSecretEncrypted).HasColumnName("mfa_secret_encrypted").HasColumnType("text");
            builder.Property(x => x.EmailConfirmedAt).HasColumnName("email_confirmed_at").HasColumnType("timestamptz");
            builder.Property(x => x.MustResetPassword).HasColumnName("must_reset_password").HasColumnType("boolean").HasDefaultValue(false);
            builder.Property(x => x.LastLoginAt).HasColumnName("last_login_at").HasColumnType("timestamptz");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.HasIndex(x => new { x.TenantId, x.Username })
                .IsUnique().HasDatabaseName("uq_users_tenant_username");
            builder.HasIndex(x => new { x.TenantId, x.Email })
                .IsUnique().HasDatabaseName("uq_users_tenant_email");
            builder.HasIndex(x => x.TenantId)
                .HasDatabaseName("ix_users_tenant");
        }
    }

    public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.ToTable("refresh_tokens", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("uuid");
            builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasColumnType("text");
            builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamptz");
            builder.Property(x => x.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamptz");
            builder.Property(x => x.ReplacedByTokenHash).HasColumnName("replaced_by_token_hash").HasColumnType("text");
            builder.Property(x => x.CreatedIp).HasColumnName("created_ip").HasColumnType("text");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.AbsoluteExpiresAtUtc).HasColumnName("absolute_expires_at_utc").HasColumnType("timestamptz");
            builder.HasIndex(x => x.UserId)
                .HasDatabaseName("ix_refresh_tokens_user");
            builder.HasIndex(x => x.TenantId)
                .HasDatabaseName("ix_refresh_tokens_tenant");
            builder.HasIndex(x => x.TokenHash)
                .HasDatabaseName("ix_refresh_tokens_token_hash");
            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.ToTable("audit_logs", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("uuid");
            builder.Property(x => x.Action).HasColumnName("action").HasColumnType("text");
            builder.Property(x => x.EntityType).HasColumnName("entity_type").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.EntityId).HasColumnName("entity_id").HasColumnType("uuid");
            builder.Property(x => x.BeforeJson).HasColumnName("before_json").HasColumnType("jsonb");
            builder.Property(x => x.AfterJson).HasColumnName("after_json").HasColumnType("jsonb");
            builder.Property(x => x.Ip).HasColumnName("ip").HasColumnType("text");
            builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasColumnType("text");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.HasIndex(x => new { x.TenantId, x.CreatedAt })
                .HasDatabaseName("ix_audit_logs_tenant_created");
            builder.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId })
                .HasDatabaseName("ix_audit_logs_tenant_entity");
            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }

}