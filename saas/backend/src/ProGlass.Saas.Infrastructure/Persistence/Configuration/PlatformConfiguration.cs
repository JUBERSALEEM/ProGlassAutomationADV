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
    public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
    {
        public void Configure(EntityTypeBuilder<Tenant> builder)
        {
            builder.ToTable("tenants", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.Name).HasColumnName("name").HasColumnType("text");
            builder.Property(x => x.Slug).HasColumnName("slug").HasColumnType("text");
            builder.Property(x => x.Status).HasColumnName("status").HasColumnType("text").HasDefaultValue("active");
            builder.Property(x => x.Timezone).HasColumnName("timezone").HasColumnType("text").HasDefaultValue("Asia/Dubai");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.Property(x => x.DeletedAt).HasColumnName("deleted_at").HasColumnType("timestamptz");
            builder.HasIndex(x => x.Slug)
                .IsUnique().HasDatabaseName("uq_tenants_slug");
        }
    }

    public sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
    {
        public void Configure(EntityTypeBuilder<Plan> builder)
        {
            builder.ToTable("plans", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.Code).HasColumnName("code").HasColumnType("text");
            builder.Property(x => x.Name).HasColumnName("name").HasColumnType("text");
            builder.Property(x => x.Price).HasColumnName("price").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.BillingPeriod).HasColumnName("billing_period").HasColumnType("text").HasDefaultValue("monthly");
            builder.Property(x => x.IsActive).HasColumnName("is_active").HasColumnType("boolean").HasDefaultValue(true);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.HasIndex(x => x.Code)
                .IsUnique().HasDatabaseName("uq_plans_code");
        }
    }

    public sealed class FeatureConfiguration : IEntityTypeConfiguration<Feature>
    {
        public void Configure(EntityTypeBuilder<Feature> builder)
        {
            builder.ToTable("features", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.Key).HasColumnName("key").HasColumnType("text");
            builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.HasIndex(x => x.Key)
                .IsUnique().HasDatabaseName("uq_features_key");
        }
    }

    public sealed class PlanFeatureConfiguration : IEntityTypeConfiguration<PlanFeature>
    {
        public void Configure(EntityTypeBuilder<PlanFeature> builder)
        {
            builder.ToTable("plan_features", "saas");
            builder.HasKey(x => new { x.PlanId, x.FeatureId });
            builder.Property(x => x.PlanId).HasColumnName("plan_id").HasColumnType("uuid");
            builder.Property(x => x.FeatureId).HasColumnName("feature_id").HasColumnType("uuid");
            builder.Property(x => x.LimitValue).HasColumnName("limit_value").HasColumnType("integer");
            builder.HasOne(x => x.Plan)
                .WithMany()
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.Feature)
                .WithMany()
                .HasForeignKey(x => x.FeatureId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
    {
        public void Configure(EntityTypeBuilder<Subscription> builder)
        {
            builder.ToTable("subscriptions", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.PlanId).HasColumnName("plan_id").HasColumnType("uuid");
            builder.Property(x => x.Status).HasColumnName("status").HasColumnType("text").HasDefaultValue("trialing");
            builder.Property(x => x.PeriodStart).HasColumnName("period_start").HasColumnType("date").HasDefaultValueSql("CURRENT_DATE");
            builder.Property(x => x.PeriodEnd).HasColumnName("period_end").HasColumnType("date");
            builder.Property(x => x.TrialEndsAt).HasColumnName("trial_ends_at").HasColumnType("timestamptz");
            builder.Property(x => x.CancelReason).HasColumnName("cancel_reason").HasColumnType("text");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.HasIndex(x => x.TenantId)
                .HasDatabaseName("ix_subscriptions_tenant");
            builder.HasOne(x => x.Plan)
                .WithMany()
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class TenantFeatureConfiguration : IEntityTypeConfiguration<TenantFeature>
    {
        public void Configure(EntityTypeBuilder<TenantFeature> builder)
        {
            builder.ToTable("tenant_features", "saas");
            builder.HasKey(x => new { x.TenantId, x.FeatureId });
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.FeatureId).HasColumnName("feature_id").HasColumnType("uuid");
            builder.Property(x => x.IsEnabled).HasColumnName("is_enabled").HasColumnType("boolean").HasDefaultValue(true);
            builder.Property(x => x.LimitValue).HasColumnName("limit_value").HasColumnType("integer");
            builder.HasOne(x => x.Feature)
                .WithMany()
                .HasForeignKey(x => x.FeatureId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

}