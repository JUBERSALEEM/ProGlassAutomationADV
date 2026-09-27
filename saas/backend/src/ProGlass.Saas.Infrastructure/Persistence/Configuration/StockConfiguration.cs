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
    public sealed class GlassSheetConfiguration : IEntityTypeConfiguration<GlassSheet>
    {
        public void Configure(EntityTypeBuilder<GlassSheet> builder)
        {
            builder.ToTable("glass_sheets", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.Category).HasColumnName("category").HasColumnType("text");
            builder.Property(x => x.ThicknessMm).HasColumnName("thickness_mm").HasColumnType("integer");
            builder.Property(x => x.Color).HasColumnName("color").HasColumnType("text").HasDefaultValue("Clear");
            builder.Property(x => x.WidthMm).HasColumnName("width_mm").HasColumnType("integer");
            builder.Property(x => x.HeightMm).HasColumnName("height_mm").HasColumnType("integer");
            builder.Property(x => x.Supplier).HasColumnName("supplier").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.PurchasePricePerSqm).HasColumnName("purchase_price_per_sqm").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.SellPricePerSqm).HasColumnName("sell_price_per_sqm").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.Stock).HasColumnName("stock").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.Used).HasColumnName("used").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.LastPurchaseDate).HasColumnName("last_purchase_date").HasColumnType("date");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.HasIndex(x => new { x.TenantId, x.Category, x.ThicknessMm, x.Color, x.WidthMm, x.HeightMm, x.Supplier })
                .IsUnique().HasDatabaseName("uq_glass_sheets_identity");
            builder.HasIndex(x => new { x.TenantId, x.Color, x.ThicknessMm })
                .HasDatabaseName("ix_glass_sheets_tenant_color");
            builder.HasIndex(x => new { x.TenantId, x.Category })
                .HasDatabaseName("ix_glass_sheets_tenant_category");
        }
    }

    public sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
    {
        public void Configure(EntityTypeBuilder<InventoryItem> builder)
        {
            builder.ToTable("inventory_items", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.Category).HasColumnName("category").HasColumnType("text");
            builder.Property(x => x.Name).HasColumnName("name").HasColumnType("text");
            builder.Property(x => x.Supplier).HasColumnName("supplier").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Stock).HasColumnName("stock").HasColumnType("numeric(14,2)").HasDefaultValue(0m);
            builder.Property(x => x.Used).HasColumnName("used").HasColumnType("numeric(14,2)").HasDefaultValue(0m);
            builder.Property(x => x.ReorderLevel).HasColumnName("reorder_level").HasColumnType("numeric(14,2)").HasDefaultValue(0m);
            builder.Property(x => x.PurchasePricePerUnit).HasColumnName("purchase_price_per_unit").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.SellPricePerUnit).HasColumnName("sell_price_per_unit").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.Unit).HasColumnName("unit").HasColumnType("text").HasDefaultValue("LM");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.HasIndex(x => new { x.TenantId, x.Category })
                .HasDatabaseName("ix_inventory_items_tenant_category");
            builder.HasIndex(x => new { x.TenantId, x.Supplier })
                .HasDatabaseName("ix_inventory_items_tenant_supplier");
        }
    }

    public sealed class StockTransactionConfiguration : IEntityTypeConfiguration<StockTransaction>
    {
        public void Configure(EntityTypeBuilder<StockTransaction> builder)
        {
            builder.ToTable("stock_transactions", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.ItemType).HasColumnName("item_type").HasColumnType("text");
            builder.Property(x => x.GlassSheetId).HasColumnName("glass_sheet_id").HasColumnType("uuid");
            builder.Property(x => x.InventoryItemId).HasColumnName("inventory_item_id").HasColumnType("uuid");
            builder.Property(x => x.TransactionType).HasColumnName("transaction_type").HasColumnType("text");
            builder.Property(x => x.QuantityDelta).HasColumnName("quantity_delta").HasColumnType("numeric(14,2)");
            builder.Property(x => x.BalanceAfter).HasColumnName("balance_after").HasColumnType("numeric(14,2)");
            builder.Property(x => x.UnitCost).HasColumnName("unit_cost").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.ReferenceType).HasColumnName("reference_type").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.ReferenceId).HasColumnName("reference_id").HasColumnType("uuid");
            builder.Property(x => x.Notes).HasColumnName("notes").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.PerformedBy).HasColumnName("performed_by").HasColumnType("uuid");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.HasIndex(x => new { x.TenantId, x.GlassSheetId, x.CreatedAt })
                .HasDatabaseName("ix_stock_transactions_tenant_sheet");
            builder.HasIndex(x => new { x.TenantId, x.InventoryItemId, x.CreatedAt })
                .HasDatabaseName("ix_stock_transactions_tenant_inventory");
            builder.HasIndex(x => new { x.TenantId, x.CreatedAt })
                .HasDatabaseName("ix_stock_transactions_tenant_created");
            builder.HasOne(x => x.GlassSheet)
                .WithMany()
                .HasForeignKey(x => x.GlassSheetId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.InventoryItem)
                .WithMany()
                .HasForeignKey(x => x.InventoryItemId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.PerformedByUser)
                .WithMany()
                .HasForeignKey(x => x.PerformedBy)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }

}