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

        // ═══════════════════════════════════════════════════════════
        //  CONSUMPTION TRACEABILITY (2026-09-02)
        //  Answers "which salesman's customer's project consumed how
        //  many sheets" — impossible today because a Use recorded only
        //  a quantity and a free-text reference.
        //
        //  Denormalised on purpose: a history row is an immutable
        //  AUDIT record. If the job order is later edited or deleted,
        //  the sheet was still consumed for that customer on that day,
        //  and the record must not silently change or vanish.
        // ═══════════════════════════════════════════════════════════

        // NULLABLE ON PURPOSE — this was a real crash ("The data is NULL at
        // ordinal 2") and the fix is the TYPE, not the data.
        //
        // Migration 1001 adds these as "TEXT NULL", so every row that
        // existed BEFORE the migration has SQL NULL in them. A non-nullable
        // CLR `string` tells EF the column can never be null, so EF emits a
        // GetString() with no null check and throws on the first legacy row
        // — which is why the history list failed to load entirely.
        //
        // Backfilling to '' would fix today's rows but leave the same trap
        // for any row inserted by an older build or by hand, so the model
        // now states the truth: these columns are optional.
        //
        // Readers should use the *Display helpers below rather than the raw
        // values, so a null never reaches the UI.

        /// <summary>PI or JO number this consumption was booked against.</summary>
        public string? DocumentReference { get; set; }

        public string? CustomerName { get; set; }
        public string? ProjectName { get; set; }
        public string? Salesman { get; set; }

        /// <summary>"Outer" / "Inner" — which pane of the spec consumed it.</summary>
        public string? PanePosition { get; set; }

        // ── Null-safe display accessors ──────────────────────────────
        public string DocumentReferenceDisplay => Text(DocumentReference);
        public string CustomerNameDisplay => Text(CustomerName);
        public string ProjectNameDisplay => Text(ProjectName);
        public string SalesmanDisplay => Text(Salesman);
        public string PanePositionDisplay => Text(PanePosition);

        private static string Text(string? v) =>
            string.IsNullOrWhiteSpace(v) ? "—" : v!.Trim();

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