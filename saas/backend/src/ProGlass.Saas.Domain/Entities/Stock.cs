// ═══════════════════════════════════════════════════════════════════════
// GENERATED FILE — DO NOT EDIT BY HAND
// Source of truth: saas/database/migrations/*.sql (verified by
// saas/database/verify/apply_and_test.py against live PostgreSQL).
// Regenerate with: python3 saas/devbox/generate_ef_model.py
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Tenancy;

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.glass_sheets.</summary>
    public sealed class GlassSheet : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Category { get; set; } = null!;
        public int ThicknessMm { get; set; }
        public string Color { get; set; } = null!;
        public int WidthMm { get; set; }
        public int HeightMm { get; set; }
        public string Supplier { get; set; } = null!;
        public decimal PurchasePricePerSqm { get; set; }
        public decimal SellPricePerSqm { get; set; }
        public int Stock { get; set; }
        public int Used { get; set; }
        public DateOnly? LastPurchaseDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.inventory_items.</summary>
    public sealed class InventoryItem : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Category { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Supplier { get; set; } = null!;
        public decimal Stock { get; set; }
        public decimal Used { get; set; }
        public decimal ReorderLevel { get; set; }
        public decimal PurchasePricePerUnit { get; set; }
        public decimal SellPricePerUnit { get; set; }
        public string Unit { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.stock_transactions.</summary>
    public sealed class StockTransaction : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string ItemType { get; set; } = null!;
        public Guid? GlassSheetId { get; set; }
        public Guid? InventoryItemId { get; set; }
        public string TransactionType { get; set; } = null!;
        public decimal QuantityDelta { get; set; }
        public decimal BalanceAfter { get; set; }
        public decimal UnitCost { get; set; }
        public string ReferenceType { get; set; } = null!;
        public Guid? ReferenceId { get; set; }
        public string Notes { get; set; } = null!;
        public Guid? PerformedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public GlassSheet? GlassSheet { get; set; } = null;
        public InventoryItem? InventoryItem { get; set; } = null;
        public User? PerformedByUser { get; set; } = null;
    }
}
