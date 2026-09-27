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
    public sealed class OptimizationSessionConfiguration : IEntityTypeConfiguration<OptimizationSession>
    {
        public void Configure(EntityTypeBuilder<OptimizationSession> builder)
        {
            builder.ToTable("optimization_sessions", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.DocumentType).HasColumnName("document_type").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.DocumentId).HasColumnName("document_id").HasColumnType("uuid");
            builder.Property(x => x.CompositionKey).HasColumnName("composition_key").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.ParamsJson).HasColumnName("params_json").HasColumnType("jsonb");
            builder.Property(x => x.Status).HasColumnName("status").HasColumnType("text").HasDefaultValue("queued");
            builder.Property(x => x.Strategy).HasColumnName("strategy").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.SheetsUsed).HasColumnName("sheets_used").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.UnplacedCount).HasColumnName("unplaced_count").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.TotalUsedSqm).HasColumnName("total_used_sqm").HasColumnType("numeric(14,4)").HasDefaultValue(0m);
            builder.Property(x => x.TotalStockSqm).HasColumnName("total_stock_sqm").HasColumnType("numeric(14,4)").HasDefaultValue(0m);
            builder.Property(x => x.UtilizationPct).HasColumnName("utilization_pct").HasColumnType("numeric(6,2)").HasDefaultValue(0m);
            builder.Property(x => x.ConservationVerified).HasColumnName("conservation_verified").HasColumnType("boolean").HasDefaultValue(false);
            builder.Property(x => x.GeometryVerified).HasColumnName("geometry_verified").HasColumnType("boolean").HasDefaultValue(false);
            builder.Property(x => x.ValidationProblems).HasColumnName("validation_problems").HasColumnType("jsonb").HasDefaultValue("[]");
            builder.Property(x => x.ScaleNotes).HasColumnName("scale_notes").HasColumnType("jsonb").HasDefaultValue("[]");
            builder.Property(x => x.SheetsLowerBound).HasColumnName("sheets_lower_bound").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.StartedAt).HasColumnName("started_at").HasColumnType("timestamptz");
            builder.Property(x => x.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamptz");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.HasIndex(x => new { x.TenantId, x.DocumentType, x.DocumentId })
                .HasDatabaseName("ix_optimization_sessions_tenant_document");
            builder.HasIndex(x => new { x.TenantId, x.Status })
                .HasDatabaseName("ix_optimization_sessions_tenant_status");
        }
    }

    public sealed class OptimizationSheetConfiguration : IEntityTypeConfiguration<OptimizationSheet>
    {
        public void Configure(EntityTypeBuilder<OptimizationSheet> builder)
        {
            builder.ToTable("optimization_sheets", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.SessionId).HasColumnName("session_id").HasColumnType("uuid");
            builder.Property(x => x.SheetNo).HasColumnName("sheet_no").HasColumnType("integer");
            builder.Property(x => x.StockWMm).HasColumnName("stock_w_mm").HasColumnType("integer");
            builder.Property(x => x.StockHMm).HasColumnName("stock_h_mm").HasColumnType("integer");
            builder.Property(x => x.ThicknessMm).HasColumnName("thickness_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.Color).HasColumnName("color").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.MaterialKey).HasColumnName("material_key").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.UsableX).HasColumnName("usable_x").HasColumnType("integer");
            builder.Property(x => x.UsableY).HasColumnName("usable_y").HasColumnType("integer");
            builder.Property(x => x.UsableW).HasColumnName("usable_w").HasColumnType("integer");
            builder.Property(x => x.UsableH).HasColumnName("usable_h").HasColumnType("integer");
            builder.Property(x => x.UsedSqm).HasColumnName("used_sqm").HasColumnType("numeric(12,4)").HasDefaultValue(0m);
            builder.Property(x => x.StockSqm).HasColumnName("stock_sqm").HasColumnType("numeric(12,4)").HasDefaultValue(0m);
            builder.Property(x => x.UtilizationPct).HasColumnName("utilization_pct").HasColumnType("numeric(6,2)").HasDefaultValue(0m);
            builder.Property(x => x.TrimCutCount).HasColumnName("trim_cut_count").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.HasIndex(x => new { x.TenantId, x.SessionId })
                .HasDatabaseName("ix_optimization_sheets_tenant_session");
            builder.HasOne(x => x.Session)
                .WithMany()
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class OptimizationPlacementConfiguration : IEntityTypeConfiguration<OptimizationPlacement>
    {
        public void Configure(EntityTypeBuilder<OptimizationPlacement> builder)
        {
            builder.ToTable("optimization_placements", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.SheetId).HasColumnName("sheet_id").HasColumnType("uuid");
            builder.Property(x => x.PieceRef).HasColumnName("piece_ref").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.JobRef).HasColumnName("job_ref").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.GlassRef).HasColumnName("glass_ref").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.SpecLabel).HasColumnName("spec_label").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Position).HasColumnName("position").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Color).HasColumnName("color").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.ThicknessMm).HasColumnName("thickness_mm").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.XMm).HasColumnName("x_mm").HasColumnType("integer");
            builder.Property(x => x.YMm).HasColumnName("y_mm").HasColumnType("integer");
            builder.Property(x => x.WMm).HasColumnName("w_mm").HasColumnType("integer");
            builder.Property(x => x.HMm).HasColumnName("h_mm").HasColumnType("integer");
            builder.Property(x => x.Rotated).HasColumnName("rotated").HasColumnType("boolean").HasDefaultValue(false);
            builder.Property(x => x.Qty).HasColumnName("qty").HasColumnType("integer").HasDefaultValue(1);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.HasIndex(x => new { x.TenantId, x.SheetId })
                .HasDatabaseName("ix_optimization_placements_tenant_sheet");
            builder.HasOne(x => x.Sheet)
                .WithMany()
                .HasForeignKey(x => x.SheetId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class OptimizationCutConfiguration : IEntityTypeConfiguration<OptimizationCut>
    {
        public void Configure(EntityTypeBuilder<OptimizationCut> builder)
        {
            builder.ToTable("optimization_cuts", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.SheetId).HasColumnName("sheet_id").HasColumnType("uuid");
            builder.Property(x => x.Seq).HasColumnName("seq").HasColumnType("integer");
            builder.Property(x => x.Axis).HasColumnName("axis").HasColumnType("text");
            builder.Property(x => x.AtMm).HasColumnName("at_mm").HasColumnType("integer");
            builder.Property(x => x.FromX).HasColumnName("from_x").HasColumnType("integer");
            builder.Property(x => x.FromY).HasColumnName("from_y").HasColumnType("integer");
            builder.Property(x => x.FromW).HasColumnName("from_w").HasColumnType("integer");
            builder.Property(x => x.FromH).HasColumnName("from_h").HasColumnType("integer");
            builder.Property(x => x.Stage).HasColumnName("stage").HasColumnType("integer").HasDefaultValue(1);
            builder.Property(x => x.IsTrim).HasColumnName("is_trim").HasColumnType("boolean").HasDefaultValue(false);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.HasIndex(x => new { x.TenantId, x.SheetId, x.Seq })
                .HasDatabaseName("ix_optimization_cuts_tenant_sheet");
            builder.HasOne(x => x.Sheet)
                .WithMany()
                .HasForeignKey(x => x.SheetId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class OptimizationLeftoverConfiguration : IEntityTypeConfiguration<OptimizationLeftover>
    {
        public void Configure(EntityTypeBuilder<OptimizationLeftover> builder)
        {
            builder.ToTable("optimization_leftovers", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.SheetId).HasColumnName("sheet_id").HasColumnType("uuid");
            builder.Property(x => x.XMm).HasColumnName("x_mm").HasColumnType("integer");
            builder.Property(x => x.YMm).HasColumnName("y_mm").HasColumnType("integer");
            builder.Property(x => x.WMm).HasColumnName("w_mm").HasColumnType("integer");
            builder.Property(x => x.HMm).HasColumnName("h_mm").HasColumnType("integer");
            builder.Property(x => x.IsReusable).HasColumnName("is_reusable").HasColumnType("boolean").HasDefaultValue(false);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.HasIndex(x => new { x.TenantId, x.SheetId })
                .HasDatabaseName("ix_optimization_leftovers_tenant_sheet");
            builder.HasOne(x => x.Sheet)
                .WithMany()
                .HasForeignKey(x => x.SheetId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

}