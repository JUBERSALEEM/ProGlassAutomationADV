using System;

namespace ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote
{
    /// <summary>
    /// Row model for the Delivery Note list UI. Only surfaces indexed
    /// columns (fast to load, no JSON deserialization for browsing).
    /// Full snapshot loaded only when user opens detail view.
    ///
    /// BUG-06: Removed ViewModelBase inheritance — this is a pure POCO
    ///         data transfer object. No UI binds directly to this class.
    ///         It is always wrapped in DeliveryNoteRowViewModel which
    ///         handles all INotifyPropertyChanged concerns.
    ///
    /// Phase 10.7: Added TotalPreviouslyDeliveredQty (Improvement 7).
    ///             Added TaxInvoiceId + HasTaxInvoice (Improvement 6).
    /// </summary>
    public class DeliveryNoteListItem
    {
        public int Id { get; set; }
        public string DeliveryNoteId { get; set; } = "";
        public string JobOrderId { get; set; } = "";
        public string ProformaId { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string ProjectName { get; set; } = "";

        /// <summary>Draft | Dispatched | Delivered | Signed | Received</summary>
        public string Status { get; set; } = "Draft";

        /// <summary>Original | Dummy | Rework | Return</summary>
        public string DeliveryType { get; set; } = "Original";

        public bool IsFoc { get; set; }
        public string DriverName { get; set; } = "";
        public string VehicleNumber { get; set; } = "";

        // ── Quantity summary ─────────────────────────────────────────────

        public int TotalOrderedQty { get; set; }
        public int TotalDeliveredQty { get; set; }
        public int TotalBalanceQty { get; set; }

        /// <summary>
        /// Phase 10.7 (Improvement 7) — mirrors SavedDeliveryNote.TotalPreviouslyDeliveredQty.
        /// Populated by ToListItem() in the repository.
        /// </summary>
        public int TotalPreviouslyDeliveredQty { get; set; }

        public int SequenceNumber { get; set; }
        public DateTime DeliveryDate { get; set; }
        public string DeliveryTime { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // ── Tax Invoice link (Phase 19 prep — Improvement 6) ─────────────

        /// <summary>
        /// Phase 10.7 placeholder — wired in Phase 19.
        /// Empty string = no Tax Invoice generated yet.
        /// </summary>
        public string TaxInvoiceId { get; set; } = "";

        /// <summary>
        /// True when a Tax Invoice has been generated from this DN.
        /// Phase 19 will set this when converting DN → Tax Invoice.
        /// </summary>
        public bool HasTaxInvoice { get; set; }
    }
}