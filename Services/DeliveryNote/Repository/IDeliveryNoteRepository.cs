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
    /// Phase 10.6 : Added attachment CRUD (independent of parent DN save).
    /// Phase 10.6b: Added GetAttachmentCountAsync for per-sibling counts.
    /// Phase 10.7 : Added GetPreviouslyDeliveredMapAsync for self-healing
    ///              PREV column — always fresh from confirmed sibling DNs.
    /// </summary>
    public interface IDeliveryNoteRepository
    {
        Task InitializeAsync();

        // ── Single DN retrieval ──────────────────────────────────────────

        Task<SavedDeliveryNote?> GetByIdAsync(int id);
        Task<SavedDeliveryNote?> GetByDeliveryNoteIdAsync(string deliveryNoteId);

        // ── List retrieval ───────────────────────────────────────────────

        Task<List<DeliveryNoteListItem>> GetAllAsync();
        Task<List<DeliveryNoteListItem>> GetByJobOrderIdAsync(string jobOrderId);
        Task<List<DeliveryNoteListItem>> GetPreparedAsync();
        Task<List<DeliveryNoteListItem>> GetDeliveredAsync();
        Task<List<PendingQtySummary>> GetPendingQtyJobsAsync();
        Task<List<DeliveryNoteListItem>> GetReworkAsync();
        Task<List<DeliveryNoteListItem>> GetReturnedAsync();

        // ── Delivery history ─────────────────────────────────────────────

        Task<DeliveryHistorySummary> GetDeliveryHistoryAsync(string jobOrderId);

        Task<int> GetPreviouslyDeliveredQtyAsync(
            string jobOrderId, int specGroupNumber, int serialNumber);

        // ── Phase 10.7 — Self-healing PREV map ──────────────────────────
        /// <summary>
        /// Returns a dictionary keyed by "specGroupNumber|serialNumber"
        /// whose value is the total DeliveredQty across all CONFIRMED
        /// (non-Draft) sibling DNs for this job order, EXCLUDING the
        /// DN identified by <paramref name="excludeDnId"/>.
        ///
        /// Used to self-heal PreviouslyDeliveredQty on load and after save,
        /// so stale DB snapshots don't cause wrong PREV/Balance values.
        ///
        /// Key format: $"{specGroupNumber}|{serialNumber}"
        /// </summary>
        Task<Dictionary<string, int>> GetPreviouslyDeliveredMapAsync(
            string jobOrderId, string excludeDnId);

        // ── Numbering ────────────────────────────────────────────────────

        Task<int> GetNextSequenceAsync(string jobOrderId);
        Task<string> BuildDeliveryNoteIdAsync(string jobOrderId, int sequence);

        // ── CRUD ─────────────────────────────────────────────────────────

        Task<SavedDeliveryNote> InsertAsync(SavedDeliveryNote entity);
        Task UpdateAsync(SavedDeliveryNote entity);
        Task DeleteAsync(int id);

        // ── Existence checks ─────────────────────────────────────────────

        Task<bool> ExistsByDeliveryNoteIdAsync(string deliveryNoteId);
        Task<bool> HasAnyDeliveryAsync(string jobOrderId);

        // ── Attachments (Phase 10.6) ─────────────────────────────────────

        Task<List<DeliveryNoteAttachment>> GetAttachmentsAsync(string deliveryNoteId);
        Task<DeliveryNoteAttachment> AddAttachmentAsync(DeliveryNoteAttachment entity);
        Task DeleteAttachmentAsync(int attachmentId);

        // ── Attachment counts (Phase 10.6b) ──────────────────────────────

        /// <summary>Count of attachments for a specific DN.</summary>
        Task<int> GetAttachmentCountAsync(string deliveryNoteId);

        /// <summary>
        /// Batch: attachment counts for a list of DN IDs.
        /// Returns dictionary keyed by DeliveryNoteId. IDs with zero
        /// attachments still present in the dictionary (value = 0).
        /// </summary>
        Task<Dictionary<string, int>> GetAttachmentCountsAsync(
            IEnumerable<string> deliveryNoteIds);
    }

    // ── Supporting DTOs ──────────────────────────────────────────────────

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