using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.DeliveryNote.Repository
{
    /// <summary>
    /// Repository contract for delivery note persistence (deliverynotes.db).
    ///
    /// All methods are async. Items and Attachments are cascade-loaded
    /// with their parent DN via GetByIdAsync and GetByJobOrderIdAsync.
    /// List methods return lightweight DeliveryNoteListItem (no JSON parse).
    ///
    /// Phase 10.5c: Added GetReturnedAsync() for the Returned tab.
    /// </summary>
    public interface IDeliveryNoteRepository
    {
        Task InitializeAsync();

        // ── Single DN retrieval ──────────────────────────────────────────

        /// <summary>
        /// Returns full entity with Items + Attachments navigation loaded.
        /// Returns null if not found.
        /// </summary>
        Task<SavedDeliveryNote?> GetByIdAsync(int id);

        /// <summary>
        /// Returns full entity by DN string ID (e.g. "DN-2026-001-01").
        /// Returns null if not found.
        /// </summary>
        Task<SavedDeliveryNote?> GetByDeliveryNoteIdAsync(string deliveryNoteId);

        // ── List retrieval ───────────────────────────────────────────────

        /// <summary>All DNs as lightweight list items (newest first).</summary>
        Task<List<DeliveryNoteListItem>> GetAllAsync();

        /// <summary>All DNs for a specific Job Order (ordered by sequence).</summary>
        Task<List<DeliveryNoteListItem>> GetByJobOrderIdAsync(string jobOrderId);

        /// <summary>All DNs with Status = Draft (Prepared tab).</summary>
        Task<List<DeliveryNoteListItem>> GetPreparedAsync();

        /// <summary>
        /// All DNs with Status in Delivered|Signed|Received (Delivered tab).
        /// </summary>
        Task<List<DeliveryNoteListItem>> GetDeliveredAsync();

        /// <summary>
        /// Job Orders that still have balance qty > 0.
        /// Returns distinct JO references with pending qty summary.
        /// </summary>
        Task<List<PendingQtySummary>> GetPendingQtyJobsAsync();

        /// <summary>All DNs with DeliveryType = Rework (Rework tab).</summary>
        Task<List<DeliveryNoteListItem>> GetReworkAsync();

        /// <summary>All DNs with DeliveryType = Return (Returned tab).</summary>
        Task<List<DeliveryNoteListItem>> GetReturnedAsync();

        // ── Delivery history ─────────────────────────────────────────────

        /// <summary>
        /// Per-JO delivery summary: total ordered, delivered, balance,
        /// rework, returned. Computed from DeliveryNoteItems table.
        /// </summary>
        Task<DeliveryHistorySummary> GetDeliveryHistoryAsync(string jobOrderId);

        /// <summary>
        /// Previously delivered qty for a specific item row across all
        /// confirmed DNs for this JO. Used when creating new DN from balance.
        /// Only counts DNs with Status != Draft (excludes dummy DNs).
        /// </summary>
        Task<int> GetPreviouslyDeliveredQtyAsync(
            string jobOrderId,
            int specGroupNumber,
            int serialNumber);

        // ── Numbering ────────────────────────────────────────────────────

        /// <summary>
        /// Returns next sequence number for a JO's deliveries.
        /// e.g. JOB-2026-001 has DN-2026-001-01 → returns 2
        /// </summary>
        Task<int> GetNextSequenceAsync(string jobOrderId);

        /// <summary>
        /// Builds the full DN ID string from JO ID + sequence.
        /// e.g. ("JOB-2026-001", 2) → "DN-2026-001-02"
        /// </summary>
        Task<string> BuildDeliveryNoteIdAsync(string jobOrderId, int sequence);

        // ── CRUD ─────────────────────────────────────────────────────────

        Task<SavedDeliveryNote> InsertAsync(SavedDeliveryNote entity);
        Task UpdateAsync(SavedDeliveryNote entity);
        Task DeleteAsync(int id);

        // ── Existence checks ─────────────────────────────────────────────

        Task<bool> ExistsByDeliveryNoteIdAsync(string deliveryNoteId);
        Task<bool> HasAnyDeliveryAsync(string jobOrderId);
    }

    // ── Supporting DTOs ──────────────────────────────────────────────────

    /// <summary>
    /// Summary of pending (undelivered) qty for one Job Order.
    /// Used in the Pending Qty tab.
    /// </summary>
    public class PendingQtySummary
    {
        public string JobOrderId { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public int TotalOrdered { get; set; }
        public int TotalDelivered { get; set; }
        public int TotalBalance { get; set; }
        public string LastDnId { get; set; } = "";
    }

    /// <summary>
    /// Full delivery history for one Job Order across all DNs.
    /// </summary>
    public class DeliveryHistorySummary
    {
        public string JobOrderId { get; set; } = "";
        public int TotalOrdered { get; set; }
        public int TotalDelivered { get; set; }
        public int TotalBalance { get; set; }
        public int TotalRework { get; set; }
        public int TotalReturned { get; set; }
        public System.Collections.Generic.List<DeliveryHistoryEntry> Entries { get; set; } = new();
    }

    /// <summary>One DN's contribution to the delivery history timeline.</summary>
    public class DeliveryHistoryEntry
    {
        public string DeliveryNoteId { get; set; } = "";
        public string DeliveryType { get; set; } = "";
        public string Status { get; set; } = "";
        public int DeliveredQty { get; set; }
        public int BalanceQty { get; set; }
        public System.DateTime DeliveryDate { get; set; }
        public string DriverName { get; set; } = "";
    }
}