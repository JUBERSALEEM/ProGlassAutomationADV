// ═══════════════════════════════════════════════════════════════════════
//  FILE: ProGlass.Saas.Infrastructure/Persistence/Configuration/
//        CuttingCostsConfiguration.cs
//  Phase 26b — EF mappings for the 0022 tables (material_allocations,
//  production_actual_costs, production_replacements, cutting_sessions,
//  cutting_sheets, cutting_placed_pieces).
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Infrastructure.Persistence.Configuration;

public sealed class MaterialAllocationConfiguration : IEntityTypeConfiguration<MaterialAllocation>
{
    public void Configure(EntityTypeBuilder<MaterialAllocation> builder)
    {
        builder.ToTable("material_allocations", "saas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        builder.Property(x => x.AllocationId).HasColumnName("allocation_id").HasColumnType("text");
        builder.Property(x => x.ReservationId).HasColumnName("reservation_id").HasColumnType("uuid");
        builder.Property(x => x.ReservationKey).HasColumnName("reservation_key").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.JobOrderId).HasColumnName("job_order_id").HasColumnType("uuid");
        builder.Property(x => x.JobRef).HasColumnName("job_ref").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.SpecGroupNumber).HasColumnName("spec_group_number").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.SerialNumber).HasColumnName("serial_number").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.OptimizationSessionId).HasColumnName("optimization_session_id").HasColumnType("uuid");
        builder.Property(x => x.SheetNo).HasColumnName("sheet_no").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.PlacedPieceLabel).HasColumnName("placed_piece_label").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.GlassRef).HasColumnName("glass_ref").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.WidthMm).HasColumnName("width_mm").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.HeightMm).HasColumnName("height_mm").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasColumnType("integer").HasDefaultValue(1);
        builder.Property(x => x.UtilizationPct).HasColumnName("utilization_pct").HasColumnType("numeric(18, 2)").HasDefaultValue(0m);
        builder.Property(x => x.WastagePct).HasColumnName("wastage_pct").HasColumnType("numeric(18, 2)").HasDefaultValue(0m);
        builder.Property(x => x.Status).HasColumnName("status").HasColumnType("text").HasDefaultValue("Allocated");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.ConsumedAt).HasColumnName("consumed_at").HasColumnType("timestamptz");
        builder.Property(x => x.Notes).HasColumnName("notes").HasColumnType("text").HasDefaultValue("");
        builder.HasOne(x => x.JobOrder)
            .WithMany()
            .HasForeignKey(x => x.JobOrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Reservation)
            .WithMany()
            .HasForeignKey(x => x.ReservationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class ActualCostConfiguration : IEntityTypeConfiguration<ActualCost>
{
    public void Configure(EntityTypeBuilder<ActualCost> builder)
    {
        builder.ToTable("production_actual_costs", "saas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        builder.Property(x => x.CostId).HasColumnName("cost_id").HasColumnType("text");
        builder.Property(x => x.JobOrderId).HasColumnName("job_order_id").HasColumnType("uuid");
        builder.Property(x => x.JobRef).HasColumnName("job_ref").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.SourceProformaId).HasColumnName("source_proforma_id").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.SpecGroupNumber).HasColumnName("spec_group_number").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.SerialNumber).HasColumnName("serial_number").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.GlassRef).HasColumnName("glass_ref").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.SpecName).HasColumnName("spec_name").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.CostType).HasColumnName("cost_type").HasColumnType("text").HasDefaultValue("ActualMaterial");
        builder.Property(x => x.StockSheetId).HasColumnName("stock_sheet_id").HasColumnType("uuid");
        builder.Property(x => x.MaterialAllocationId).HasColumnName("material_allocation_id").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasColumnType("numeric(18, 4)").HasDefaultValue(0m);
        builder.Property(x => x.UnitCost).HasColumnName("unit_cost").HasColumnType("numeric(18, 4)").HasDefaultValue(0m);
        builder.Property(x => x.TotalCost).HasColumnName("total_cost").HasColumnType("numeric(18, 4)").HasDefaultValue(0m);
        builder.Property(x => x.SellingValue).HasColumnName("selling_value").HasColumnType("numeric(18, 4)").HasDefaultValue(0m);
        builder.Property(x => x.IsFoc).HasColumnName("is_foc").HasColumnType("boolean").HasDefaultValue(false);
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.Notes).HasColumnName("notes").HasColumnType("text").HasDefaultValue("");
        builder.HasOne(x => x.JobOrder)
            .WithMany()
            .HasForeignKey(x => x.JobOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProductionReplacementConfiguration : IEntityTypeConfiguration<ProductionReplacement>
{
    public void Configure(EntityTypeBuilder<ProductionReplacement> builder)
    {
        builder.ToTable("production_replacements", "saas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        builder.Property(x => x.ReplacementId).HasColumnName("replacement_id").HasColumnType("text");
        builder.Property(x => x.JobOrderId).HasColumnName("job_order_id").HasColumnType("uuid");
        builder.Property(x => x.JobRef).HasColumnName("job_ref").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.SourceProformaId).HasColumnName("source_proforma_id").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.SpecGroupNumber).HasColumnName("spec_group_number").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.SerialNumber).HasColumnName("serial_number").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.GlassRef).HasColumnName("glass_ref").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.SpecName).HasColumnName("spec_name").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.Position).HasColumnName("position").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.Stage).HasColumnName("stage").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.OriginalPieceId).HasColumnName("original_piece_id").HasColumnType("uuid");
        builder.Property(x => x.OriginalEventId).HasColumnName("original_event_id").HasColumnType("uuid");
        builder.Property(x => x.ReplacementPieceId).HasColumnName("replacement_piece_id").HasColumnType("uuid");
        builder.Property(x => x.EntryType).HasColumnName("entry_type").HasColumnType("text").HasDefaultValue("Rework");
        builder.Property(x => x.Reason).HasColumnName("reason").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.Quantity).HasColumnName("quantity").HasColumnType("integer").HasDefaultValue(1);
        builder.Property(x => x.IsFoc).HasColumnName("is_foc").HasColumnType("boolean").HasDefaultValue(false);
        builder.Property(x => x.ReplacementSequence).HasColumnName("replacement_sequence").HasColumnType("integer").HasDefaultValue(1);
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.Status).HasColumnName("status").HasColumnType("text").HasDefaultValue("Pending");
        builder.HasOne(x => x.JobOrder)
            .WithMany()
            .HasForeignKey(x => x.JobOrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.OriginalPiece)
            .WithMany()
            .HasForeignKey(x => x.OriginalPieceId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.ReplacementPiece)
            .WithMany()
            .HasForeignKey(x => x.ReplacementPieceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class CuttingSessionConfiguration : IEntityTypeConfiguration<CuttingSession>
{
    public void Configure(EntityTypeBuilder<CuttingSession> builder)
    {
        builder.ToTable("cutting_sessions", "saas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        builder.Property(x => x.CuttingSessionId).HasColumnName("cutting_session_id").HasColumnType("text");
        builder.Property(x => x.JobOrderId).HasColumnName("job_order_id").HasColumnType("uuid");
        builder.Property(x => x.JobRef).HasColumnName("job_ref").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.SourceProformaId).HasColumnName("source_proforma_id").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.OptimizationSessionId).HasColumnName("optimization_session_id").HasColumnType("uuid");
        builder.Property(x => x.MaterialGroupId).HasColumnName("material_group_id").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.TotalPieces).HasColumnName("total_pieces").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.SheetsUsed).HasColumnName("sheets_used").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.UtilizationPct).HasColumnName("utilization_pct").HasColumnType("numeric(18, 2)").HasDefaultValue(0m);
        builder.Property(x => x.WastagePct).HasColumnName("wastage_pct").HasColumnType("numeric(18, 2)").HasDefaultValue(0m);
        builder.Property(x => x.Status).HasColumnName("status").HasColumnType("text").HasDefaultValue("Draft");
        builder.Property(x => x.RotationPolicy).HasColumnName("rotation_policy").HasColumnType("text").HasDefaultValue("Free90");
        builder.Property(x => x.SplitMode).HasColumnName("split_mode").HasColumnType("text").HasDefaultValue("HorizontalFirst");
        builder.Property(x => x.EffortLevel).HasColumnName("effort_level").HasColumnType("integer").HasDefaultValue(3);
        builder.Property(x => x.MarginProfileJson).HasColumnName("margin_profile_json").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.IsVerified).HasColumnName("is_verified").HasColumnType("boolean").HasDefaultValue(false);
        builder.Property(x => x.VerificationResult).HasColumnName("verification_result").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamptz");
        builder.Property(x => x.Notes).HasColumnName("notes").HasColumnType("text").HasDefaultValue("");
        builder.HasOne(x => x.JobOrder)
            .WithMany()
            .HasForeignKey(x => x.JobOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CuttingSheetConfiguration : IEntityTypeConfiguration<CuttingSheet>
{
    public void Configure(EntityTypeBuilder<CuttingSheet> builder)
    {
        builder.ToTable("cutting_sheets", "saas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        builder.Property(x => x.CuttingSheetId).HasColumnName("cutting_sheet_id").HasColumnType("text");
        builder.Property(x => x.CuttingSessionId).HasColumnName("cutting_session_id").HasColumnType("uuid");
        builder.Property(x => x.JobOrderId).HasColumnName("job_order_id").HasColumnType("uuid");
        builder.Property(x => x.JobRef).HasColumnName("job_ref").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.SheetNo).HasColumnName("sheet_no").HasColumnType("integer").HasDefaultValue(1);
        builder.Property(x => x.StockSheetId).HasColumnName("stock_sheet_id").HasColumnType("uuid");
        builder.Property(x => x.StockSizeLabel).HasColumnName("stock_size_label").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.StockWidthMm).HasColumnName("stock_width_mm").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.StockHeightMm).HasColumnName("stock_height_mm").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.ThicknessMm).HasColumnName("thickness_mm").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.Color).HasColumnName("color").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.UsableX).HasColumnName("usable_x").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.UsableY).HasColumnName("usable_y").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.UsableWidthMm).HasColumnName("usable_width_mm").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.UsableHeightMm).HasColumnName("usable_height_mm").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.UsedSqm).HasColumnName("used_sqm").HasColumnType("numeric(18, 4)").HasDefaultValue(0m);
        builder.Property(x => x.StockSqm).HasColumnName("stock_sqm").HasColumnType("numeric(18, 4)").HasDefaultValue(0m);
        builder.Property(x => x.UtilizationPct).HasColumnName("utilization_pct").HasColumnType("numeric(18, 2)").HasDefaultValue(0m);
        builder.Property(x => x.WastagePct).HasColumnName("wastage_pct").HasColumnType("numeric(18, 2)").HasDefaultValue(0m);
        builder.Property(x => x.IsVerified).HasColumnName("is_verified").HasColumnType("boolean").HasDefaultValue(false);
        builder.Property(x => x.VerificationResult).HasColumnName("verification_result").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.PlacedPieceCount).HasColumnName("placed_piece_count").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.HasOne(x => x.Session)
            .WithMany()
            .HasForeignKey(x => x.CuttingSessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.JobOrder)
            .WithMany()
            .HasForeignKey(x => x.JobOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CuttingPlacedPieceConfiguration : IEntityTypeConfiguration<CuttingPlacedPiece>
{
    public void Configure(EntityTypeBuilder<CuttingPlacedPiece> builder)
    {
        builder.ToTable("cutting_placed_pieces", "saas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        builder.Property(x => x.PlacedPieceId).HasColumnName("placed_piece_id").HasColumnType("text");
        builder.Property(x => x.CuttingSheetId).HasColumnName("cutting_sheet_id").HasColumnType("uuid");
        builder.Property(x => x.CuttingSessionId).HasColumnName("cutting_session_id").HasColumnType("uuid");
        builder.Property(x => x.JobOrderId).HasColumnName("job_order_id").HasColumnType("uuid");
        builder.Property(x => x.JobRef).HasColumnName("job_ref").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.SpecGroupNumber).HasColumnName("spec_group_number").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.SerialNumber).HasColumnName("serial_number").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.GlassRef).HasColumnName("glass_ref").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.SpecName).HasColumnName("spec_name").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.Position).HasColumnName("position").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.PieceId).HasColumnName("piece_id").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.PieceLabel).HasColumnName("piece_label").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.WidthMm).HasColumnName("width_mm").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.HeightMm).HasColumnName("height_mm").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.X).HasColumnName("x").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.Y).HasColumnName("y").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.IsRotated).HasColumnName("is_rotated").HasColumnType("boolean").HasDefaultValue(false);
        builder.Property(x => x.CutSequence).HasColumnName("cut_sequence").HasColumnType("integer").HasDefaultValue(0);
        builder.Property(x => x.Status).HasColumnName("status").HasColumnType("text").HasDefaultValue("Pending");
        builder.Property(x => x.SourcePieceId).HasColumnName("source_piece_id").HasColumnType("uuid");
        builder.Property(x => x.ReservationKey).HasColumnName("reservation_key").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.AllocationId).HasColumnName("allocation_id").HasColumnType("text").HasDefaultValue("");
        builder.Property(x => x.IsFoc).HasColumnName("is_foc").HasColumnType("boolean").HasDefaultValue(false);
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.Notes).HasColumnName("notes").HasColumnType("text").HasDefaultValue("");
        builder.HasOne(x => x.Sheet)
            .WithMany()
            .HasForeignKey(x => x.CuttingSheetId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.JobOrder)
            .WithMany()
            .HasForeignKey(x => x.JobOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
