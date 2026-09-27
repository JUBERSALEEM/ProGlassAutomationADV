// ═══════════════════════════════════════════════════════════════════════
// GENERATED FILE — DO NOT EDIT BY HAND
// Source of truth: saas/database/migrations/*.sql (verified by
// saas/database/verify/apply_and_test.py against live PostgreSQL).
// Regenerate with: python3 saas/devbox/generate_ef_model.py
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Tenancy;

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.companies.</summary>
    public sealed class Company : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = null!;
        public string Trn { get; set; } = null!;
        public string Location { get; set; } = null!;
        public string Representative { get; set; } = null!;
        public string RepresentativeMobile { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string AccountNumber { get; set; } = null!;
        public string BankName { get; set; } = null!;
        public string LogoStorageKey { get; set; } = null!;
        public bool IsDefault { get; set; }
        public int UsageCount { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.clients.</summary>
    public sealed class Client : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string CustomerName { get; set; } = null!;
        public string CustomerTrn { get; set; } = null!;
        public string ReferenceCode { get; set; } = null!;
        public string SalesRepresentative { get; set; } = null!;
        public string BillingAddress { get; set; } = null!;
        public string AttentionExecutiveName { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public int UsageCount { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.projects.</summary>
    public sealed class Project : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string IdentifierName { get; set; } = null!;
        public string ContractNo { get; set; } = null!;
        public string Reference { get; set; } = null!;
        public string SiteDeliveryLocation { get; set; } = null!;
        public string LpoNumber { get; set; } = null!;
        public Guid? CustomerId { get; set; }
        public int UsageCount { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public Client? Customer { get; set; } = null;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.salesmen.</summary>
    public sealed class Salesman : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = null!;
        public string Department { get; set; } = null!;
        public int UsageCount { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
    }
}
