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
    public sealed class InquiryConfiguration : IEntityTypeConfiguration<Inquiry>
    {
        public void Configure(EntityTypeBuilder<Inquiry> builder)
        {
            builder.ToTable("inquiries", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.InquiryNo).HasColumnName("inquiry_no").HasColumnType("text");
            builder.Property(x => x.Status).HasColumnName("status").HasColumnType("text").HasDefaultValue("Received");
            builder.Property(x => x.StatusText).HasColumnName("status_text").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Priority).HasColumnName("priority").HasColumnType("text").HasDefaultValue("normal");
            builder.Property(x => x.CustomerId).HasColumnName("customer_id").HasColumnType("uuid");
            builder.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("uuid");
            builder.Property(x => x.CustomerName).HasColumnName("customer_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.ProjectName).HasColumnName("project_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Salesman).HasColumnName("salesman").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Notes).HasColumnName("notes").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.ReceivedAt).HasColumnName("received_at").HasColumnType("timestamptz");
            builder.Property(x => x.BoqJson).HasColumnName("boq_json").HasColumnType("jsonb");
            builder.Property(x => x.ConvertedProformaId).HasColumnName("converted_proforma_id").HasColumnType("uuid");
            builder.Property(x => x.IsActive).HasColumnName("is_active").HasColumnType("boolean").HasDefaultValue(true);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.HasIndex(x => new { x.TenantId, x.InquiryNo })
                .HasDatabaseName("ix_inquiries_tenant_no");
            builder.HasIndex(x => new { x.TenantId, x.Status })
                .HasDatabaseName("ix_inquiries_tenant_status");
            builder.HasIndex(x => new { x.TenantId, x.CustomerName })
                .HasDatabaseName("ix_inquiries_tenant_customer");
            builder.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }

    public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
    {
        public void Configure(EntityTypeBuilder<Attachment> builder)
        {
            builder.ToTable("attachments", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.EntityType).HasColumnName("entity_type").HasColumnType("text");
            builder.Property(x => x.EntityId).HasColumnName("entity_id").HasColumnType("uuid");
            builder.Property(x => x.StorageKey).HasColumnName("storage_key").HasColumnType("text");
            builder.Property(x => x.OriginalName).HasColumnName("original_name").HasColumnType("text");
            builder.Property(x => x.MimeType).HasColumnName("mime_type").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.SizeBytes).HasColumnName("size_bytes").HasColumnType("bigint").HasDefaultValue(0);
            builder.Property(x => x.UploadedBy).HasColumnName("uploaded_by").HasColumnType("uuid");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId })
                .HasDatabaseName("ix_attachments_tenant_entity");
            builder.HasOne(x => x.UploadedByUser)
                .WithMany()
                .HasForeignKey(x => x.UploadedBy)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }

    public sealed class SpecificationConfiguration : IEntityTypeConfiguration<Specification>
    {
        public void Configure(EntityTypeBuilder<Specification> builder)
        {
            builder.ToTable("specifications", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.SpecName).HasColumnName("spec_name").HasColumnType("text");
            builder.Property(x => x.GlassType).HasColumnName("glass_type").HasColumnType("text");
            builder.Property(x => x.ParamsJson).HasColumnName("params_json").HasColumnType("jsonb");
            builder.Property(x => x.RatePerSqm).HasColumnName("rate_per_sqm").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.CostBasisPerSqm).HasColumnName("cost_basis_per_sqm").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.UsageCount).HasColumnName("usage_count").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.LastUsedAt).HasColumnName("last_used_at").HasColumnType("timestamptz");
            builder.Property(x => x.IsActive).HasColumnName("is_active").HasColumnType("boolean").HasDefaultValue(true);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.Property(x => x.Category).HasColumnName("category").HasColumnType("text");
            builder.Property(x => x.Color).HasColumnName("color").HasColumnType("text");
            builder.Property(x => x.ThicknessMm).HasColumnName("thickness_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.SheetWidthMm).HasColumnName("sheet_width_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.SheetHeightMm).HasColumnName("sheet_height_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.SheetId).HasColumnName("sheet_id").HasColumnType("uuid");
            builder.Property(x => x.PanesJson).HasColumnName("panes_json").HasColumnType("jsonb");
            builder.Property(x => x.LastProformaNo).HasColumnName("last_proforma_no").HasColumnType("text");
            builder.HasIndex(x => new { x.TenantId, x.SpecName })
                .HasDatabaseName("ix_specifications_tenant_name");
            builder.HasIndex(x => new { x.TenantId, x.GlassType })
                .HasDatabaseName("ix_specifications_tenant_type");
        }
    }

    public sealed class TenantPricingConfigConfiguration : IEntityTypeConfiguration<TenantPricingConfig>
    {
        public void Configure(EntityTypeBuilder<TenantPricingConfig> builder)
        {
            builder.ToTable("tenant_pricing_config", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.YieldFactorDefault).HasColumnName("yield_factor_default").HasColumnType("numeric(8,4)").HasDefaultValue(0.8500m);
            builder.Property(x => x.MarginDefault).HasColumnName("margin_default").HasColumnType("numeric(5,2)").HasDefaultValue(15m);
            builder.Property(x => x.ThicknessOptions).HasColumnName("thickness_options").HasColumnType("jsonb").HasDefaultValue("[]");
            builder.Property(x => x.TreatmentOptions).HasColumnName("treatment_options").HasColumnType("jsonb").HasDefaultValue("[]");
            builder.Property(x => x.AspWidths).HasColumnName("asp_widths").HasColumnType("jsonb").HasDefaultValue("[]");
            builder.Property(x => x.InterlayerTypes).HasColumnName("interlayer_types").HasColumnType("jsonb").HasDefaultValue("[]");
            builder.Property(x => x.InterlayerThicknesses).HasColumnName("interlayer_thicknesses").HasColumnType("jsonb").HasDefaultValue("[]");
            builder.Property(x => x.PolishOptions).HasColumnName("polish_options").HasColumnType("jsonb").HasDefaultValue("[]");
            builder.Property(x => x.PanelExtraOptions).HasColumnName("panel_extra_options").HasColumnType("jsonb").HasDefaultValue("[]");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.HasIndex(x => x.TenantId)
                .IsUnique().HasDatabaseName("uq_tenant_pricing_config_tenant");
        }
    }

    public sealed class TenantVatConfigConfiguration : IEntityTypeConfiguration<TenantVatConfig>
    {
        public void Configure(EntityTypeBuilder<TenantVatConfig> builder)
        {
            builder.ToTable("tenant_vat_config", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.VatRate).HasColumnName("vat_rate").HasColumnType("numeric(5,4)").HasDefaultValue(0.0500m);
            builder.Property(x => x.Currency).HasColumnName("currency").HasColumnType("char(3)").HasDefaultValue("AED");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.HasIndex(x => x.TenantId)
                .IsUnique().HasDatabaseName("uq_tenant_vat_config_tenant");
        }
    }

    public sealed class ProformaInvoiceConfiguration : IEntityTypeConfiguration<ProformaInvoice>
    {
        public void Configure(EntityTypeBuilder<ProformaInvoice> builder)
        {
            builder.ToTable("proforma_invoices", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.ProformaNo).HasColumnName("proforma_no").HasColumnType("text");
            builder.Property(x => x.RootProformaId).HasColumnName("root_proforma_id").HasColumnType("uuid");
            builder.Property(x => x.RevisionNumber).HasColumnName("revision_number").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.InquiryId).HasColumnName("inquiry_id").HasColumnType("uuid");
            builder.Property(x => x.CompanyId).HasColumnName("company_id").HasColumnType("uuid");
            builder.Property(x => x.ClientId).HasColumnName("client_id").HasColumnType("uuid");
            builder.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("uuid");
            builder.Property(x => x.SalesmanId).HasColumnName("salesman_id").HasColumnType("uuid");
            builder.Property(x => x.Status).HasColumnName("status").HasColumnType("text").HasDefaultValue("Draft");
            builder.Property(x => x.StatusText).HasColumnName("status_text").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.LifecycleState).HasColumnName("lifecycle_state").HasColumnType("text").HasDefaultValue("Draft");
            builder.Property(x => x.IssueDate).HasColumnName("issue_date").HasColumnType("date");
            builder.Property(x => x.ValidUntil).HasColumnName("valid_until").HasColumnType("date");
            builder.Property(x => x.GlassMaterialsSubtotal).HasColumnName("glass_materials_subtotal").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.OtherChargesSubtotal).HasColumnName("other_charges_subtotal").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.DiscountAmount).HasColumnName("discount_amount").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.VatAmount).HasColumnName("vat_amount").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.GrandTotalVatInclusive).HasColumnName("grand_total_vat_inclusive").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.IsVoid).HasColumnName("is_void").HasColumnType("boolean").HasDefaultValue(false);
            builder.Property(x => x.VoidedAt).HasColumnName("voided_at").HasColumnType("timestamptz");
            builder.Property(x => x.VoidedBy).HasColumnName("voided_by").HasColumnType("uuid");
            builder.Property(x => x.VoidReason).HasColumnName("void_reason").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.FurthestStep).HasColumnName("furthest_step").HasColumnType("integer").HasDefaultValue(1);
            builder.Property(x => x.SnapshotJson).HasColumnName("snapshot_json").HasColumnType("jsonb");
            builder.Property(x => x.RowVersion).HasColumnName("row_version").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.SentAt).HasColumnName("sent_at").HasColumnType("timestamptz");
            builder.Property(x => x.ApprovedAt).HasColumnName("approved_at").HasColumnType("timestamptz");
            builder.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at").HasColumnType("timestamptz");
            builder.Property(x => x.JobOrderId).HasColumnName("job_order_id").HasColumnType("uuid");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.Property(x => x.PnlWastagePercent).HasColumnName("pnl_wastage_percent").HasColumnType("numeric(5,2)").HasDefaultValue(15m);
            builder.Property(x => x.PnlCostAtSellingPrice).HasColumnName("pnl_cost_at_selling_price").HasColumnType("boolean").HasDefaultValue(true);
            builder.Property(x => x.PnlGrossVatInclusive).HasColumnName("pnl_gross_vat_inclusive").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.PnlNetExVat).HasColumnName("pnl_net_ex_vat").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.PnlTotalGlassCost).HasColumnName("pnl_total_glass_cost").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.PnlTotalProductionCost).HasColumnName("pnl_total_production_cost").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.PnlProfit).HasColumnName("pnl_profit").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.PnlMarginPercent).HasColumnName("pnl_margin_percent").HasColumnType("numeric(9,2)").HasDefaultValue(0m);
            builder.Property(x => x.PnlOverallMarginPercent).HasColumnName("pnl_overall_margin_percent").HasColumnType("numeric(9,2)").HasDefaultValue(0m);
            builder.Property(x => x.PnlNetMarginPercent).HasColumnName("pnl_net_margin_percent").HasColumnType("numeric(9,2)").HasDefaultValue(0m);
            builder.Property(x => x.PnlUInsertCost).HasColumnName("pnl_u_insert_cost").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.PnlBasisCost).HasColumnName("pnl_basis_cost").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.PnlCalculatedAt).HasColumnName("pnl_calculated_at").HasColumnType("timestamptz");
            builder.Property(x => x.DiscountType).HasColumnName("discount_type").HasColumnType("text").HasDefaultValue("Percentage");
            builder.Property(x => x.DiscountValue).HasColumnName("discount_value").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.IsVatApplicable).HasColumnName("is_vat_applicable").HasColumnType("boolean").HasDefaultValue(true);
            builder.Property(x => x.VatPercent).HasColumnName("vat_percent").HasColumnType("numeric(5,2)").HasDefaultValue(5.00m);
            builder.HasIndex(x => new { x.TenantId, x.ProformaNo })
                .IsUnique().HasDatabaseName("uq_proforma_invoices_tenant_no");
            builder.HasIndex(x => new { x.TenantId, x.Status })
                .HasDatabaseName("ix_proforma_invoices_tenant_status");
            builder.HasIndex(x => new { x.TenantId, x.IssueDate })
                .HasDatabaseName("ix_proforma_invoices_tenant_issue_date");
            builder.HasIndex(x => new { x.TenantId, x.ClientId })
                .HasDatabaseName("ix_proforma_invoices_tenant_client");
            builder.HasIndex(x => new { x.TenantId, x.RootProformaId })
                .HasDatabaseName("ix_proforma_invoices_tenant_root");
            builder.HasOne(x => x.RootProforma)
                .WithMany()
                .HasForeignKey(x => x.RootProformaId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(x => x.Inquiry)
                .WithMany()
                .HasForeignKey(x => x.InquiryId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(x => x.Company)
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(x => x.Salesman)
                .WithMany()
                .HasForeignKey(x => x.SalesmanId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }

    public sealed class SpecificationUsageConfiguration : IEntityTypeConfiguration<SpecificationUsage>
    {
        public void Configure(EntityTypeBuilder<SpecificationUsage> builder)
        {
            builder.ToTable("specification_usages", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.SpecId).HasColumnName("spec_id").HasColumnType("uuid");
            builder.Property(x => x.ProformaId).HasColumnName("proforma_id").HasColumnType("uuid");
            builder.Property(x => x.SpecName).HasColumnName("spec_name").HasColumnType("text");
            builder.Property(x => x.GlassType).HasColumnName("glass_type").HasColumnType("text");
            builder.Property(x => x.CompanyName).HasColumnName("company_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.CustomerName).HasColumnName("customer_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.ProjectName).HasColumnName("project_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.PricePerSqm).HasColumnName("price_per_sqm").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.SheetWidthMm).HasColumnName("sheet_width_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.SheetHeightMm).HasColumnName("sheet_height_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.UsedAt).HasColumnName("used_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.HasIndex(x => new { x.SpecId, x.ProformaId })
                .IsUnique().HasDatabaseName("uq_specification_usages_pair");
            builder.HasOne(x => x.Spec)
                .WithMany()
                .HasForeignKey(x => x.SpecId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.Proforma)
                .WithMany()
                .HasForeignKey(x => x.ProformaId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class SpecGroupConfiguration : IEntityTypeConfiguration<SpecGroup>
    {
        public void Configure(EntityTypeBuilder<SpecGroup> builder)
        {
            builder.ToTable("spec_groups", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.ProformaId).HasColumnName("proforma_id").HasColumnType("uuid");
            builder.Property(x => x.GroupNumber).HasColumnName("group_number").HasColumnType("integer");
            builder.Property(x => x.GlassType).HasColumnName("glass_type").HasColumnType("text");
            builder.Property(x => x.SpecName).HasColumnName("spec_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.SpecId).HasColumnName("spec_id").HasColumnType("uuid");
            builder.Property(x => x.RatePerSqm).HasColumnName("rate_per_sqm").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.PriceSource).HasColumnName("price_source").HasColumnType("text").HasDefaultValue("formula");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.Property(x => x.OuterGlassColor).HasColumnName("outer_glass_color").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.InnerGlassColor).HasColumnName("inner_glass_color").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.ThirdGlassColor).HasColumnName("third_glass_color").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.OuterSheetWidthMm).HasColumnName("outer_sheet_width_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.OuterSheetHeightMm).HasColumnName("outer_sheet_height_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.InnerSheetWidthMm).HasColumnName("inner_sheet_width_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.InnerSheetHeightMm).HasColumnName("inner_sheet_height_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.ThirdSheetWidthMm).HasColumnName("third_sheet_width_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.ThirdSheetHeightMm).HasColumnName("third_sheet_height_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.OuterSheetPricePerSqm).HasColumnName("outer_sheet_price_per_sqm").HasColumnType("numeric(18,4)").HasDefaultValue(0m);
            builder.Property(x => x.InnerSheetPricePerSqm).HasColumnName("inner_sheet_price_per_sqm").HasColumnType("numeric(18,4)").HasDefaultValue(0m);
            builder.Property(x => x.ThirdSheetPricePerSqm).HasColumnName("third_sheet_price_per_sqm").HasColumnType("numeric(18,4)").HasDefaultValue(0m);
            builder.Property(x => x.Step4SpacerPricePerSqm).HasColumnName("step4_spacer_price_per_sqm").HasColumnType("numeric(18,4)");
            builder.Property(x => x.Step4InterlayerCostPerSqm).HasColumnName("step4_interlayer_cost_per_sqm").HasColumnType("numeric(18,4)");
            builder.Property(x => x.Step4CuttingCostPerSqm).HasColumnName("step4_cutting_cost_per_sqm").HasColumnType("numeric(18,4)");
            builder.Property(x => x.Step4TemperingCostPerSqm).HasColumnName("step4_tempering_cost_per_sqm").HasColumnType("numeric(18,4)");
            builder.Property(x => x.UInsertPricePerSqm).HasColumnName("u_insert_price_per_sqm").HasColumnType("numeric(18,4)");
            builder.HasIndex(x => new { x.TenantId, x.ProformaId })
                .HasDatabaseName("ix_spec_groups_tenant_proforma");
            builder.HasOne(x => x.Proforma)
                .WithMany()
                .HasForeignKey(x => x.ProformaId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.Spec)
                .WithMany()
                .HasForeignKey(x => x.SpecId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }

    public sealed class SpecGroupItemConfiguration : IEntityTypeConfiguration<SpecGroupItem>
    {
        public void Configure(EntityTypeBuilder<SpecGroupItem> builder)
        {
            builder.ToTable("spec_group_items", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.SpecGroupId).HasColumnName("spec_group_id").HasColumnType("uuid");
            builder.Property(x => x.RowSerial).HasColumnName("row_serial").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.Position).HasColumnName("position").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.WidthMm).HasColumnName("width_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.HeightMm).HasColumnName("height_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.Qty).HasColumnName("qty").HasColumnType("integer").HasDefaultValue(1);
            builder.Property(x => x.W1).HasColumnName("w1").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.H1).HasColumnName("h1").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.W2).HasColumnName("w2").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.H2).HasColumnName("h2").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.Sqm).HasColumnName("sqm").HasColumnType("numeric(12,3)").HasDefaultValue(0m);
            builder.Property(x => x.TotalSqm).HasColumnName("total_sqm").HasColumnType("numeric(12,3)").HasDefaultValue(0m);
            builder.Property(x => x.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.Total).HasColumnName("total").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.Notes).HasColumnName("notes").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.Property(x => x.PerimeterFactor).HasColumnName("perimeter_factor").HasColumnType("integer").HasDefaultValue(2);
            builder.HasIndex(x => new { x.TenantId, x.SpecGroupId })
                .HasDatabaseName("ix_spec_group_items_tenant_group");
            builder.HasOne(x => x.SpecGroup)
                .WithMany()
                .HasForeignKey(x => x.SpecGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class OtherChargeConfiguration : IEntityTypeConfiguration<OtherCharge>
    {
        public void Configure(EntityTypeBuilder<OtherCharge> builder)
        {
            builder.ToTable("other_charges", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.ProformaId).HasColumnName("proforma_id").HasColumnType("uuid");
            builder.Property(x => x.ChargeName).HasColumnName("charge_name").HasColumnType("text");
            builder.Property(x => x.MetricKey).HasColumnName("metric_key").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.CheckedSpecGroupNumbers).HasColumnName("checked_spec_group_numbers").HasColumnType("jsonb").HasDefaultValue("[]");
            builder.Property(x => x.Rate).HasColumnName("rate").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.IsActive).HasColumnName("is_active").HasColumnType("boolean").HasDefaultValue(true);
            builder.Property(x => x.SortOrder).HasColumnName("sort_order").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.Property(x => x.TemplateKey).HasColumnName("template_key").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Unit).HasColumnName("unit").HasColumnType("text").HasDefaultValue("Qty");
            builder.Property(x => x.MetricQty).HasColumnName("metric_qty").HasColumnType("numeric(18,3)").HasDefaultValue(0m);
            builder.Property(x => x.PolishSubType).HasColumnName("polish_sub_type").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.CustomPolishDescription).HasColumnName("custom_polish_description").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.CustomPolishQty).HasColumnName("custom_polish_qty").HasColumnType("numeric(18,3)").HasDefaultValue(0m);
            builder.HasIndex(x => new { x.TenantId, x.ProformaId })
                .HasDatabaseName("ix_other_charges_tenant_proforma");
            builder.HasOne(x => x.Proforma)
                .WithMany()
                .HasForeignKey(x => x.ProformaId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class WastageRowConfiguration : IEntityTypeConfiguration<WastageRow>
    {
        public void Configure(EntityTypeBuilder<WastageRow> builder)
        {
            builder.ToTable("wastage_rows", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.ProformaId).HasColumnName("proforma_id").HasColumnType("uuid");
            builder.Property(x => x.ColorName).HasColumnName("color_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.OptimizationPercent).HasColumnName("optimization_percent").HasColumnType("numeric(6,3)").HasDefaultValue(0m);
            builder.Property(x => x.SheetQuantity).HasColumnName("sheet_quantity").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.FormulaPricePerSqm).HasColumnName("formula_price_per_sqm").HasColumnType("numeric(18,4)").HasDefaultValue(0m);
            builder.Property(x => x.Rate).HasColumnName("rate").HasColumnType("numeric(18,6)").HasDefaultValue(0m);
            builder.Property(x => x.ResultAmount).HasColumnName("result_amount").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.IsActive).HasColumnName("is_active").HasColumnType("boolean").HasDefaultValue(true);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.Property(x => x.TotalSqm).HasColumnName("total_sqm").HasColumnType("numeric(12,3)").HasDefaultValue(0m);
            builder.Property(x => x.IsManuallyAdded).HasColumnName("is_manually_added").HasColumnType("boolean").HasDefaultValue(false);
            builder.Property(x => x.UseOptimizationFormula).HasColumnName("use_optimization_formula").HasColumnType("boolean").HasDefaultValue(false);
            builder.Property(x => x.CheckedSourceKeys).HasColumnName("checked_source_keys").HasColumnType("jsonb").HasDefaultValue("[]");
            builder.Property(x => x.SortOrder).HasColumnName("sort_order").HasColumnType("integer").HasDefaultValue(0);
            builder.HasIndex(x => new { x.TenantId, x.ProformaId })
                .HasDatabaseName("ix_wastage_rows_tenant_proforma");
            builder.HasOne(x => x.Proforma)
                .WithMany()
                .HasForeignKey(x => x.ProformaId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class CustomChargeConfiguration : IEntityTypeConfiguration<CustomCharge>
    {
        public void Configure(EntityTypeBuilder<CustomCharge> builder)
        {
            builder.ToTable("custom_charges", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.ProformaId).HasColumnName("proforma_id").HasColumnType("uuid");
            builder.Property(x => x.ChargeName).HasColumnName("charge_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Qty).HasColumnName("qty").HasColumnType("numeric(18,3)").HasDefaultValue(1m);
            builder.Property(x => x.Unit).HasColumnName("unit").HasColumnType("text").HasDefaultValue("Qty");
            builder.Property(x => x.Rate).HasColumnName("rate").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.IsActive).HasColumnName("is_active").HasColumnType("boolean").HasDefaultValue(true);
            builder.Property(x => x.SortOrder).HasColumnName("sort_order").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.HasIndex(x => new { x.TenantId, x.ProformaId })
                .HasDatabaseName("ix_custom_charges_tenant_proforma");
            builder.HasOne(x => x.Proforma)
                .WithMany()
                .HasForeignKey(x => x.ProformaId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class PnlRowConfiguration : IEntityTypeConfiguration<PnlRow>
    {
        public void Configure(EntityTypeBuilder<PnlRow> builder)
        {
            builder.ToTable("pnl_rows", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.ProformaId).HasColumnName("proforma_id").HasColumnType("uuid");
            builder.Property(x => x.Kind).HasColumnName("kind").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.SpecNumber).HasColumnName("spec_number").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.SpecName).HasColumnName("spec_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.SheetWidthMm).HasColumnName("sheet_width_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.SheetHeightMm).HasColumnName("sheet_height_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.SheetSqm).HasColumnName("sheet_sqm").HasColumnType("numeric(12,4)").HasDefaultValue(0m);
            builder.Property(x => x.Quantity).HasColumnName("quantity").HasColumnType("numeric(18,3)").HasDefaultValue(0m);
            builder.Property(x => x.PricePerUnit).HasColumnName("price_per_unit").HasColumnType("numeric(18,4)").HasDefaultValue(0m);
            builder.Property(x => x.UnitLabel).HasColumnName("unit_label").HasColumnType("text").HasDefaultValue("SQM");
            builder.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.SheetSource).HasColumnName("sheet_source").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.SortOrder).HasColumnName("sort_order").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.HasIndex(x => new { x.TenantId, x.ProformaId })
                .HasDatabaseName("ix_pnl_rows_tenant_proforma");
            builder.HasOne(x => x.Proforma)
                .WithMany()
                .HasForeignKey(x => x.ProformaId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class JobOrderConfiguration : IEntityTypeConfiguration<JobOrder>
    {
        public void Configure(EntityTypeBuilder<JobOrder> builder)
        {
            builder.ToTable("job_orders", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.JobNo).HasColumnName("job_no").HasColumnType("text");
            builder.Property(x => x.ProformaId).HasColumnName("proforma_id").HasColumnType("uuid");
            builder.Property(x => x.Status).HasColumnName("status").HasColumnType("text").HasDefaultValue("Draft");
            builder.Property(x => x.CustomerId).HasColumnName("customer_id").HasColumnType("uuid");
            builder.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("uuid");
            builder.Property(x => x.SalesmanId).HasColumnName("salesman_id").HasColumnType("uuid");
            builder.Property(x => x.DueDate).HasColumnName("due_date").HasColumnType("date");
            builder.Property(x => x.Notes).HasColumnName("notes").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.RowVersion).HasColumnName("row_version").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.Property(x => x.CustomerName).HasColumnName("customer_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.ProjectName).HasColumnName("project_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.GrandTotalVatInclusive).HasColumnName("grand_total_vat_inclusive").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.Priority).HasColumnName("priority").HasColumnType("text").HasDefaultValue("Medium");
            builder.Property(x => x.SiteDeliveryContact).HasColumnName("site_delivery_contact").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.SiteDeliveryPhone).HasColumnName("site_delivery_phone").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.SiteDeliveryAddress).HasColumnName("site_delivery_address").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.AssignedTo).HasColumnName("assigned_to").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.CuttingInstructions).HasColumnName("cutting_instructions").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.DrawingReference).HasColumnName("drawing_reference").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.InternalNotes).HasColumnName("internal_notes").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.IsVoid).HasColumnName("is_void").HasColumnType("boolean").HasDefaultValue(false);
            builder.Property(x => x.VoidedAt).HasColumnName("voided_at").HasColumnType("timestamptz");
            builder.Property(x => x.VoidedBy).HasColumnName("voided_by").HasColumnType("uuid");
            builder.Property(x => x.VoidReason).HasColumnName("void_reason").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.SnapshotJson).HasColumnName("snapshot_json").HasColumnType("jsonb");
            builder.HasIndex(x => new { x.TenantId, x.JobNo })
                .IsUnique().HasDatabaseName("uq_job_orders_tenant_no");
            builder.HasIndex(x => new { x.TenantId, x.Status })
                .HasDatabaseName("ix_job_orders_tenant_status");
            builder.HasIndex(x => new { x.TenantId, x.ProformaId })
                .HasDatabaseName("ix_job_orders_tenant_proforma");
            builder.HasOne(x => x.Proforma)
                .WithMany()
                .HasForeignKey(x => x.ProformaId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(x => x.Salesman)
                .WithMany()
                .HasForeignKey(x => x.SalesmanId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }

    public sealed class ProductionPieceConfiguration : IEntityTypeConfiguration<ProductionPiece>
    {
        public void Configure(EntityTypeBuilder<ProductionPiece> builder)
        {
            builder.ToTable("production_pieces", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.JobOrderId).HasColumnName("job_order_id").HasColumnType("uuid");
            builder.Property(x => x.PieceLabel).HasColumnName("piece_label").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.SpecGroupItemId).HasColumnName("spec_group_item_id").HasColumnType("uuid");
            builder.Property(x => x.Stage).HasColumnName("stage").HasColumnType("text").HasDefaultValue("cutting");
            builder.Property(x => x.Status).HasColumnName("status").HasColumnType("text").HasDefaultValue("pending");
            builder.Property(x => x.WidthMm).HasColumnName("width_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.HeightMm).HasColumnName("height_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.Qty).HasColumnName("qty").HasColumnType("integer").HasDefaultValue(1);
            builder.Property(x => x.OptimizationSheetId).HasColumnName("optimization_sheet_id").HasColumnType("uuid");
            builder.Property(x => x.Notes).HasColumnName("notes").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.HasIndex(x => new { x.TenantId, x.JobOrderId })
                .HasDatabaseName("ix_production_pieces_tenant_job");
            builder.HasIndex(x => new { x.TenantId, x.Stage, x.Status })
                .HasDatabaseName("ix_production_pieces_tenant_stage");
            builder.HasOne(x => x.JobOrder)
                .WithMany()
                .HasForeignKey(x => x.JobOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.SpecGroupItem)
                .WithMany()
                .HasForeignKey(x => x.SpecGroupItemId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }

    public sealed class ProductionEventConfiguration : IEntityTypeConfiguration<ProductionEvent>
    {
        public void Configure(EntityTypeBuilder<ProductionEvent> builder)
        {
            builder.ToTable("production_events", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.ProductionPieceId).HasColumnName("production_piece_id").HasColumnType("uuid");
            builder.Property(x => x.EventType).HasColumnName("event_type").HasColumnType("text");
            builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.HasIndex(x => new { x.TenantId, x.ProductionPieceId, x.CreatedAt })
                .HasDatabaseName("ix_production_events_tenant_piece");
            builder.HasOne(x => x.ProductionPiece)
                .WithMany()
                .HasForeignKey(x => x.ProductionPieceId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class MaterialReservationConfiguration : IEntityTypeConfiguration<MaterialReservation>
    {
        public void Configure(EntityTypeBuilder<MaterialReservation> builder)
        {
            builder.ToTable("material_reservations", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.JobOrderId).HasColumnName("job_order_id").HasColumnType("uuid");
            builder.Property(x => x.GlassSheetId).HasColumnName("glass_sheet_id").HasColumnType("uuid");
            builder.Property(x => x.ReservedQty).HasColumnName("reserved_qty").HasColumnType("integer");
            builder.Property(x => x.Status).HasColumnName("status").HasColumnType("text").HasDefaultValue("reserved");
            builder.Property(x => x.ReservedBy).HasColumnName("reserved_by").HasColumnType("uuid");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.HasIndex(x => new { x.TenantId, x.JobOrderId })
                .HasDatabaseName("ix_material_reservations_tenant_job");
            builder.HasOne(x => x.JobOrder)
                .WithMany()
                .HasForeignKey(x => x.JobOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.GlassSheet)
                .WithMany()
                .HasForeignKey(x => x.GlassSheetId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.ReservedByUser)
                .WithMany()
                .HasForeignKey(x => x.ReservedBy)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }

    public sealed class DeliveryNoteConfiguration : IEntityTypeConfiguration<DeliveryNote>
    {
        public void Configure(EntityTypeBuilder<DeliveryNote> builder)
        {
            builder.ToTable("delivery_notes", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.DeliveryNo).HasColumnName("delivery_no").HasColumnType("text");
            builder.Property(x => x.JobOrderId).HasColumnName("job_order_id").HasColumnType("uuid");
            builder.Property(x => x.Status).HasColumnName("status").HasColumnType("text").HasDefaultValue("Draft");
            builder.Property(x => x.CustomerId).HasColumnName("customer_id").HasColumnType("uuid");
            builder.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("uuid");
            builder.Property(x => x.DeliveryDate).HasColumnName("delivery_date").HasColumnType("date");
            builder.Property(x => x.Driver).HasColumnName("driver").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Vehicle).HasColumnName("vehicle").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Notes).HasColumnName("notes").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.RowVersion).HasColumnName("row_version").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.Property(x => x.SequenceNumber).HasColumnName("sequence_number").HasColumnType("integer").HasDefaultValue(1);
            builder.Property(x => x.CustomerName).HasColumnName("customer_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.ProjectName).HasColumnName("project_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.DeliveryType).HasColumnName("delivery_type").HasColumnType("text").HasDefaultValue("Original");
            builder.Property(x => x.IsFoc).HasColumnName("is_foc").HasColumnType("boolean").HasDefaultValue(false);
            builder.Property(x => x.IsMasterDn).HasColumnName("is_master_dn").HasColumnType("boolean").HasDefaultValue(false);
            builder.Property(x => x.ReceiverName).HasColumnName("receiver_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.DeliveryTime).HasColumnName("delivery_time").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.TotalOrderedQty).HasColumnName("total_ordered_qty").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.TotalDeliveredQty).HasColumnName("total_delivered_qty").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.TotalBalanceQty).HasColumnName("total_balance_qty").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.IsVoid).HasColumnName("is_void").HasColumnType("boolean").HasDefaultValue(false);
            builder.Property(x => x.VoidedAt).HasColumnName("voided_at").HasColumnType("timestamptz");
            builder.Property(x => x.VoidedBy).HasColumnName("voided_by").HasColumnType("uuid");
            builder.Property(x => x.VoidReason).HasColumnName("void_reason").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.SnapshotJson).HasColumnName("snapshot_json").HasColumnType("jsonb");
            builder.HasIndex(x => new { x.TenantId, x.DeliveryNo })
                .IsUnique().HasDatabaseName("uq_delivery_notes_tenant_no");
            builder.HasIndex(x => new { x.TenantId, x.Status })
                .HasDatabaseName("ix_delivery_notes_tenant_status");
            builder.HasIndex(x => new { x.TenantId, x.JobOrderId })
                .HasDatabaseName("ix_delivery_notes_tenant_job");
            builder.HasOne(x => x.JobOrder)
                .WithMany()
                .HasForeignKey(x => x.JobOrderId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }

    public sealed class DeliveryNoteItemConfiguration : IEntityTypeConfiguration<DeliveryNoteItem>
    {
        public void Configure(EntityTypeBuilder<DeliveryNoteItem> builder)
        {
            builder.ToTable("delivery_note_items", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.DeliveryNoteId).HasColumnName("delivery_note_id").HasColumnType("uuid");
            builder.Property(x => x.SpecGroupItemId).HasColumnName("spec_group_item_id").HasColumnType("uuid");
            builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.WidthMm).HasColumnName("width_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.HeightMm).HasColumnName("height_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.Qty).HasColumnName("qty").HasColumnType("integer").HasDefaultValue(1);
            builder.Property(x => x.Sqm).HasColumnName("sqm").HasColumnType("numeric(12,3)").HasDefaultValue(0m);
            builder.Property(x => x.DeliveredQty).HasColumnName("delivered_qty").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.ReturnedQty).HasColumnName("returned_qty").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.Property(x => x.SpecGroupNumber).HasColumnName("spec_group_number").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.SerialNumber).HasColumnName("serial_number").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.SpecGroupName).HasColumnName("spec_group_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.GlassRef).HasColumnName("glass_ref").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.W2Mm).HasColumnName("w2_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.H2Mm).HasColumnName("h2_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.OrderedQty).HasColumnName("ordered_qty").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.PreviouslyDeliveredQty).HasColumnName("previously_delivered_qty").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.BalanceQty).HasColumnName("balance_qty").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.ReworkQty).HasColumnName("rework_qty").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.WeightPerPiece).HasColumnName("weight_per_piece").HasColumnType("numeric(12,3)").HasDefaultValue(0m);
            builder.Property(x => x.LineNotes).HasColumnName("line_notes").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.JobOrderId).HasColumnName("job_order_id").HasColumnType("uuid");
            builder.HasIndex(x => new { x.TenantId, x.DeliveryNoteId })
                .HasDatabaseName("ix_delivery_note_items_tenant_note");
            builder.HasOne(x => x.DeliveryNote)
                .WithMany()
                .HasForeignKey(x => x.DeliveryNoteId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.SpecGroupItem)
                .WithMany()
                .HasForeignKey(x => x.SpecGroupItemId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }

}