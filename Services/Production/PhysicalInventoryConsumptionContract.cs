// Architecture documentation ONLY. Not a wired business event.
// No caller may persist this type or call UseStock from it.
// Product must still name the physical leave-inventory event.

using System;

namespace ProGlassAutomation.Services.Production
{
    /// <summary>
    /// Proposed shape of a future PhysicalInventoryConsumption record.
    /// EXISTING identifiers can be copied from current rows.
    /// MISSING identifiers would require a product/architecture decision.
    /// This type is not written to either database.
    /// </summary>
    public sealed class PhysicalInventoryConsumptionContract
    {
        // ── EXISTING / SAFE TO REUSE ─────────────────────────────────
        public int GlassSheetId { get; init; }          // GlassSheet.Id, Reservation.GlassSheetId
        public int Quantity { get; init; }              // reservation sheets OR UseStock qty (unit differs by source)
        public string JobOrderId { get; init; } = "";   // Reservation.JobRef
        public string SourceProformaId { get; init; } = "";
        public string ReservationId { get; init; } = "";
        public string AllocationId { get; init; } = "";
        public int WorkItemId { get; init; }            // CuttingPlacedPiece.SourceWorkItemId
        public string CuttingSessionId { get; init; } = "";
        public string OptimizationSessionId { get; init; } = "";
        public string CreatedBy { get; init; } = "";
        public DateTime CreatedAt { get; init; }
        public string Reason { get; init; } = "";
        public string Source { get; init; } = "";       // "Sheet Store (manual)" / "Step 6" / "Agent"

        // ── MISSING — WOULD REQUIRE A DECISION ───────────────────────
        // EventId / IdempotencyKey: no physical-event identity exists.
        // UseStock CAS cannot tell retry from a new legitimate use.
        // Status / Error / reconciliation state: no cross-DB event row.
        // EventType (planning vs physical): Consumed today is planning-only.
        public string? EventId { get; init; }
        public string? IdempotencyKey { get; init; }
        public string? EventType { get; init; }
        public string? ReconciliationStatus { get; init; }
    }

    public static class PhysicalInventoryConsumptionIdentity
    {
        public const string ExistingGlassSheetId = "GlassSheet.Id / Reservation.GlassSheetId";
        public const string ExistingReservationId = "JobRef|Spec|Serial|GlassSheetId|OptSession|SheetNo";
        public const string ExistingAllocationId = "ReservationId|S{sheetNo}|{placedPieceLabel}";
        public const string ExistingHistoryId = "SheetHistory.Id (proglass.db, autoincrement — not an event key)";
        public const string MissingPhysicalEventId = "No PhysicalInventoryConsumption row / EventId exists";
        public const string MissingIdempotencyKey = "UseStockAsync has no per-event key; only Stock-Used CAS";
    }
}
