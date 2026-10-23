using System;
using System.ComponentModel.DataAnnotations;

namespace ProGlassAutomation.Models.SheetStore
{
    /// <summary>
    /// Immutable movement record for non-glass consumable inventory.
    /// Snapshot fields intentionally survive deletion or renaming of the
    /// inventory item, and this table has no cascading foreign key.
    /// </summary>
    public class InventoryItemMovement
    {
        [Key]
        public int Id { get; set; }

        public int InventoryItemId { get; set; }

        [Required, MaxLength(300)]
        public string ItemDescription { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Action { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string Details { get; set; } = string.Empty;

        /// <summary>Receipt/issue quantity; adjustments may be signed.</summary>
        public decimal QuantityChanged { get; set; }

        public decimal? ValueChanged { get; set; }

        [Required, MaxLength(100)]
        public string UserName { get; set; } = Environment.UserName;

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
