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
    public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
    {
        public void Configure(EntityTypeBuilder<Company> builder)
        {
            builder.ToTable("companies", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.Name).HasColumnName("name").HasColumnType("text");
            builder.Property(x => x.Trn).HasColumnName("trn").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Location).HasColumnName("location").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Representative).HasColumnName("representative").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.RepresentativeMobile).HasColumnName("representative_mobile").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Email).HasColumnName("email").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.AccountNumber).HasColumnName("account_number").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.BankName).HasColumnName("bank_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.LogoStorageKey).HasColumnName("logo_storage_key").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.IsDefault).HasColumnName("is_default").HasColumnType("boolean").HasDefaultValue(false);
            builder.Property(x => x.UsageCount).HasColumnName("usage_count").HasColumnType("integer").HasDefaultValue(1);
            builder.Property(x => x.LastUsedAt).HasColumnName("last_used_at").HasColumnType("timestamptz");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.Property(x => x.LogoLayout).HasColumnName("logo_layout").HasColumnType("text").HasDefaultValue("None");
            builder.Property(x => x.LogoWidthMm).HasColumnName("logo_width_mm").HasColumnType("numeric(6,2)").HasDefaultValue(22m);
            builder.Property(x => x.LogoHeightMm).HasColumnName("logo_height_mm").HasColumnType("numeric(6,2)").HasDefaultValue(26m);
            builder.HasIndex(x => new { x.TenantId, x.Name })
                .HasDatabaseName("ix_companies_tenant_name");
        }
    }

    public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
    {
        public void Configure(EntityTypeBuilder<Client> builder)
        {
            builder.ToTable("clients", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.CustomerName).HasColumnName("customer_name").HasColumnType("text");
            builder.Property(x => x.CustomerTrn).HasColumnName("customer_trn").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.ReferenceCode).HasColumnName("reference_code").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.SalesRepresentative).HasColumnName("sales_representative").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.BillingAddress).HasColumnName("billing_address").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.AttentionExecutiveName).HasColumnName("attention_executive_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Phone).HasColumnName("phone").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.UsageCount).HasColumnName("usage_count").HasColumnType("integer").HasDefaultValue(1);
            builder.Property(x => x.LastUsedAt).HasColumnName("last_used_at").HasColumnType("timestamptz");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.Property(x => x.DocumentPreparedBy).HasColumnName("document_prepared_by").HasColumnType("text").HasDefaultValue("");
            builder.HasIndex(x => new { x.TenantId, x.CustomerName })
                .HasDatabaseName("ix_clients_tenant_name");
        }
    }

    public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
    {
        public void Configure(EntityTypeBuilder<Project> builder)
        {
            builder.ToTable("projects", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.IdentifierName).HasColumnName("identifier_name").HasColumnType("text");
            builder.Property(x => x.ContractNo).HasColumnName("contract_no").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Reference).HasColumnName("reference").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.SiteDeliveryLocation).HasColumnName("site_delivery_location").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.LpoNumber).HasColumnName("lpo_number").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.CustomerId).HasColumnName("customer_id").HasColumnType("uuid");
            builder.Property(x => x.UsageCount).HasColumnName("usage_count").HasColumnType("integer").HasDefaultValue(1);
            builder.Property(x => x.LastUsedAt).HasColumnName("last_used_at").HasColumnType("timestamptz");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.HasIndex(x => new { x.TenantId, x.IdentifierName })
                .HasDatabaseName("ix_projects_tenant_name");
            builder.HasIndex(x => new { x.TenantId, x.CustomerId })
                .HasDatabaseName("ix_projects_tenant_customer");
            builder.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }

    public sealed class SalesmanConfiguration : IEntityTypeConfiguration<Salesman>
    {
        public void Configure(EntityTypeBuilder<Salesman> builder)
        {
            builder.ToTable("salesmen", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.Name).HasColumnName("name").HasColumnType("text");
            builder.Property(x => x.Department).HasColumnName("department").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.UsageCount).HasColumnName("usage_count").HasColumnType("integer").HasDefaultValue(1);
            builder.Property(x => x.LastUsedAt).HasColumnName("last_used_at").HasColumnType("timestamptz");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.HasIndex(x => new { x.TenantId, x.Name })
                .HasDatabaseName("ix_salesmen_tenant_name");
        }
    }

}