// ═══════════════════════════════════════════════════════════════════════
// GENERATED FILE — DO NOT EDIT BY HAND
// Source of truth: saas/database/migrations/*.sql (verified by
// saas/database/verify/apply_and_test.py against live PostgreSQL).
// Regenerate with: python3 saas/devbox/generate_ef_model.py
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Tenancy;

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.tenants.</summary>
    public sealed class Tenant
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string Timezone { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.plans.</summary>
    public sealed class Plan
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public decimal Price { get; set; }
        public string BillingPeriod { get; set; } = null!;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.features.</summary>
    public sealed class Feature
    {
        public Guid Id { get; set; }
        public string Key { get; set; } = null!;
        public string Description { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.plan_features.</summary>
    public sealed class PlanFeature
    {
        public Guid PlanId { get; set; }
        public Guid FeatureId { get; set; }
        public int? LimitValue { get; set; }
        public Plan Plan { get; set; } = null!;
        public Feature Feature { get; set; } = null!;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.subscriptions.</summary>
    public sealed class Subscription : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid PlanId { get; set; }
        public string Status { get; set; } = null!;
        public DateOnly PeriodStart { get; set; }
        public DateOnly PeriodEnd { get; set; }
        public DateTime? TrialEndsAt { get; set; }
        public string? CancelReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public Plan Plan { get; set; } = null!;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.tenant_features.</summary>
    public sealed class TenantFeature : ITenantOwned
    {
        public Guid TenantId { get; set; }
        public Guid FeatureId { get; set; }
        public bool IsEnabled { get; set; }
        public int? LimitValue { get; set; }
        public Feature Feature { get; set; } = null!;
    }
}
