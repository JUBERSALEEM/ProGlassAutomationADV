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
    /// Phase 10.6 : Added attachment CRUD.
    /// Phase 10.6b: Added GetAttachmentCountAsync for per-sibling counts.
    /// Phase 10.7 : Added GetPreviouslyDeliveredMapAsync for self-healing
    ///              PREV column.
    /// Phase 10.8b: DeliveryHistoryEntry carries cumulative timeline.
    /// Ship 8c-2  : Added GetSqmSummariesByJobOrderAsync.
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
        /// DN identified by excludeDnId.
        /// Key format: $"{specGroupNumber}|{serialNumber}"
        /// </summary>
        Task<Dictionary<string, int>> GetPreviouslyDeliveredMapAsync(
            string jobOrderId, string excludeDnId);

        // ── Ship 8c-2 — SQM aggregation ─────────────────────────────────

        /// <summary>
        /// Batch-computes SQM aggregates per Job Order.
        /// Uses SqmCalculator (0.5 m² minimum per panel).
        /// Returns dictionary keyed by JobOrderId.
        /// </summary>
        Task<Dictionary<string, JobOrderSqmSummary>>
            GetSqmSummariesByJobOrderAsync(IEnumerable<string> jobOrderIds);

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

        Task<List<DeliveryNoteAttachment>> GetAttachmentsAsync(
            string deliveryNoteId);
        Task<DeliveryNoteAttachment> AddAttachmentAsync(
            DeliveryNoteAttachment entity);
        Task DeleteAttachmentAsync(int attachmentId);

        // ── Attachment counts (Phase 10.6b) ──────────────────────────────

        Task<int> GetAttachmentCountAsync(string deliveryNoteId);

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
        public System.Collections.Generic.List<DeliveryHistoryEntry> Entries
        { get; set; } = new();
    }

    /// <summary>
    /// One row in the DN detail history feed.
    ///
    /// LEGACY FIELDS (kept for backward compat):
    ///   DeliveredQty — pcs delivered by THIS DN
    ///   BalanceQty   — per-DN snapshot (display only)
    ///
    /// PHASE 10.8b — CUMULATIVE TIMELINE FIELDS:
    ///   OrderedQty           — total JO ordered (same across all rows)
    ///   CumulativePrevQty    — sum of DeliveredQty from all prior DNs
    ///   CumulativeBalanceQty — balance remaining AFTER this DN
    ///   SequenceNumber       — this DN's position in delivery chain
    /// </summary>
    public class DeliveryHistoryEntry
    {
        public string DeliveryNoteId { get; set; } = "";
        public string DeliveryType { get; set; } = "";
        public string Status { get; set; } = "";
        public int DeliveredQty { get; set; }
        public int BalanceQty { get; set; }
        public System.DateTime DeliveryDate { get; set; }
        public string DriverName { get; set; } = "";

        // Phase 10.8b — cumulative timeline
        public int OrderedQty { get; set; }
        public int CumulativePrevQty { get; set; }
        public int CumulativeBalanceQty { get; set; }
        public int SequenceNumber { get; set; }
    }

    // ── Ship 8c-2 — Aggregated SQM per Job Order ────────────────────────

    /// <summary>
    /// Returned by GetSqmSummariesByJobOrderAsync.
    /// All values in m², rounded to 3 decimal places.
    /// Panel SQM uses SqmCalculator with 0.5 m² minimum floor.
    /// </summary>
    public class JobOrderSqmSummary
    {
        public decimal OrderedSqm { get; set; }
        public decimal DeliveredSqm { get; set; }
        public decimal BalanceSqm { get; set; }
    }
}