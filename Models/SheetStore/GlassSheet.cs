using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProGlassAutomation.Models.SheetStore
{
    /// <summary>
    /// Represents one glass sheet type in the store inventory.
    /// Each row = unique combination of Category + Thickness + Color + Size + Supplier.
    /// </summary>
    public class GlassSheet
    {
        [Key]
        public int Id { get; set; }

        // ============ IDENTITY ============
        [Required, MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        [Required]
        public int ThicknessMm { get; set; }

        [Required, MaxLength(50)]
        public string Color { get; set; } = "Clear";

        [Required]
        public int WidthMm { get; set; }

        [Required]
        public int HeightMm { get; set; }

        [Required, MaxLength(100)]
        public string Supplier { get; set; } = string.Empty;

        // ============ PRICING (per SQM) ============
        public decimal PurchasePricePerSqm { get; set; }
        public decimal SellPricePerSqm { get; set; }

        // ============ STOCK ============
        public int Stock { get; set; }
        public int Used { get; set; }

        // ============ TIMESTAMPS ============
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public DateTime? LastPurchaseDate { get; set; }

        // ============ COMPUTED PROPERTIES (not stored in DB) ============
        [NotMapped]
        public decimal Sqm => (WidthMm * HeightMm) / 1_000_000m;

        [NotMapped]
        public int Balance => Stock - Used;

        [NotMapped]
        public decimal TotalPurchaseValue => Sqm * Stock * PurchasePricePerSqm;

        [NotMapped]
        public decimal TotalSellValue => Sqm * Stock * SellPricePerSqm;

        [NotMapped]
        public decimal BalanceValue => Sqm * Balance * SellPricePerSqm;

        [NotMapped]
        public decimal ProfitPerSqm => SellPricePerSqm - PurchasePricePerSqm;

        [NotMapped]
        public string SizeDisplay => $"{WidthMm} × {HeightMm}";

        [NotMapped]
        public string ThicknessDisplay => $"{ThicknessMm}mm";
    }
}