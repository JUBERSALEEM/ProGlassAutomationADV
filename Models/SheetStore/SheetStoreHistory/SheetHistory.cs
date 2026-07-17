using System;
using System.ComponentModel.DataAnnotations;

namespace ProGlassAutomation.Models.SheetStore.SheetStoreHistory
{
    /// <summary>
    /// Records every action performed on a glass sheet.
    /// Provides full audit trail: who did what and when.
    /// </summary>
    public class SheetHistory
    {
        [Key]
        public int Id { get; set; }

        // ============ REFERENCE ============
        public int SheetId { get; set; }

        // Snapshot of sheet info (in case sheet gets deleted later)
        [MaxLength(200)]
        public string SheetDescription { get; set; } = string.Empty;

        // ============ ACTION ============
        [MaxLength(20)]
        public string Action { get; set; } = string.Empty;   // Added, Edited, Deleted, Bought, Used

        [MaxLength(500)]
        public string Details { get; set; } = string.Empty;  // e.g., "Bought 20 sheets", "Stock: 50 → 70"

        // ============ VALUES ============
        public int QuantityChanged { get; set; }
        public decimal? ValueChanged { get; set; }

        // ============ WHO / WHEN ============
        [MaxLength(100)]
        public string UserName { get; set; } = Environment.UserName;

        public DateTime Timestamp { get; set; } = DateTime.Now;

        // ============ DISPLAY HELPERS ============
        public string TimestampDisplay => Timestamp.ToString("dd-MMM-yyyy HH:mm:ss");
        public string DateDisplay => Timestamp.ToString("dd-MMM-yyyy");
        public string TimeDisplay => Timestamp.ToString("HH:mm:ss");
        public string DayDisplay => Timestamp.ToString("dddd");

        /// <summary>Color code for the action (used in UI badge).</summary>
        public string ActionColor => Action switch
        {
            "Added" => "#10B981",     // green
            "Edited" => "#F59E0B",    // amber
            "Deleted" => "#EF4444",   // red
            "Bought" => "#3B82F6",    // blue
            "Used" => "#8B5CF6",      // purple
            _ => "#64748B"            // gray
        };
    }
}