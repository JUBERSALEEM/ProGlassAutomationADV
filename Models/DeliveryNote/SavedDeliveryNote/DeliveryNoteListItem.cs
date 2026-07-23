using ProGlassAutomation.ViewModels;
using System;

namespace ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote
{
    /// <summary>
    /// Row model for the Delivery Note list UI. Only surfaces indexed
    /// columns (fast to load, no JSON deserialization for browsing).
    /// Full snapshot loaded only when user opens detail view.
    ///
    /// Phase 10.7: Added TotalPreviouslyDeliveredQty (Improvement 7).
    ///             Added TaxInvoiceId + HasTaxInvoice (Improvement 6 — Phase 19 prep).
    /// </summary>
    public class DeliveryNoteListItem : ViewModelBase
    {
        public int Id { get; set; }

        private string _deliveryNoteId = "";
        public string DeliveryNoteId
        {
            get => _deliveryNoteId;
            set { if (_deliveryNoteId == value) return; _deliveryNoteId = value; OnPropertyChanged(); }
        }

        private string _jobOrderId = "";
        public string JobOrderId
        {
            get => _jobOrderId;
            set { if (_jobOrderId == value) return; _jobOrderId = value; OnPropertyChanged(); }
        }

        private string _proformaId = "";
        public string ProformaId
        {
            get => _proformaId;
            set { if (_proformaId == value) return; _proformaId = value; OnPropertyChanged(); }
        }

        private string _customerName = "";
        public string CustomerName
        {
            get => _customerName;
            set { if (_customerName == value) return; _customerName = value; OnPropertyChanged(); }
        }

        private string _projectName = "";
        public string ProjectName
        {
            get => _projectName;
            set { if (_projectName == value) return; _projectName = value; OnPropertyChanged(); }
        }

        private string _status = "Draft";
        public string Status
        {
            get => _status;
            set { if (_status == value) return; _status = value; OnPropertyChanged(); }
        }

        private string _deliveryType = "Original";
        public string DeliveryType
        {
            get => _deliveryType;
            set { if (_deliveryType == value) return; _deliveryType = value; OnPropertyChanged(); }
        }

        private bool _isFoc;
        public bool IsFoc
        {
            get => _isFoc;
            set { if (_isFoc == value) return; _isFoc = value; OnPropertyChanged(); }
        }

        private string _driverName = "";
        public string DriverName
        {
            get => _driverName;
            set { if (_driverName == value) return; _driverName = value; OnPropertyChanged(); }
        }

        private string _vehicleNumber = "";
        public string VehicleNumber
        {
            get => _vehicleNumber;
            set { if (_vehicleNumber == value) return; _vehicleNumber = value; OnPropertyChanged(); }
        }

        private int _totalOrderedQty;
        public int TotalOrderedQty
        {
            get => _totalOrderedQty;
            set { if (_totalOrderedQty == value) return; _totalOrderedQty = value; OnPropertyChanged(); }
        }

        private int _totalDeliveredQty;
        public int TotalDeliveredQty
        {
            get => _totalDeliveredQty;
            set { if (_totalDeliveredQty == value) return; _totalDeliveredQty = value; OnPropertyChanged(); }
        }

        private int _totalBalanceQty;
        public int TotalBalanceQty
        {
            get => _totalBalanceQty;
            set { if (_totalBalanceQty == value) return; _totalBalanceQty = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Phase 10.7 (Improvement 7) — mirrors SavedDeliveryNote.TotalPreviouslyDeliveredQty.
        /// Populated by ToListItem() in the repository.
        /// </summary>
        private int _totalPreviouslyDeliveredQty;
        public int TotalPreviouslyDeliveredQty
        {
            get => _totalPreviouslyDeliveredQty;
            set { if (_totalPreviouslyDeliveredQty == value) return; _totalPreviouslyDeliveredQty = value; OnPropertyChanged(); }
        }

        private int _sequenceNumber;
        public int SequenceNumber
        {
            get => _sequenceNumber;
            set { if (_sequenceNumber == value) return; _sequenceNumber = value; OnPropertyChanged(); }
        }

        private DateTime _deliveryDate;
        public DateTime DeliveryDate
        {
            get => _deliveryDate;
            set { if (_deliveryDate == value) return; _deliveryDate = value; OnPropertyChanged(); }
        }

        private string _deliveryTime = "";
        public string DeliveryTime
        {
            get => _deliveryTime;
            set { if (_deliveryTime == value) return; _deliveryTime = value; OnPropertyChanged(); }
        }

        private DateTime _createdAt;
        public DateTime CreatedAt
        {
            get => _createdAt;
            set { if (_createdAt == value) return; _createdAt = value; OnPropertyChanged(); }
        }

        private DateTime _updatedAt;
        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set { if (_updatedAt == value) return; _updatedAt = value; OnPropertyChanged(); }
        }

        // ── Tax Invoice link (Phase 19 prep — Improvement 6) ─────────────

        /// <summary>
        /// Phase 10.7 placeholder — wired up in Phase 19.
        /// Empty string = no Tax Invoice generated yet.
        /// </summary>
        private string _taxInvoiceId = "";
        public string TaxInvoiceId
        {
            get => _taxInvoiceId;
            set { if (_taxInvoiceId == value) return; _taxInvoiceId = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// True when a Tax Invoice has been generated from this DN.
        /// Phase 19 will set this when converting DN → Tax Invoice.
        /// </summary>
        private bool _hasTaxInvoice;
        public bool HasTaxInvoice
        {
            get => _hasTaxInvoice;
            set { if (_hasTaxInvoice == value) return; _hasTaxInvoice = value; OnPropertyChanged(); }
        }
    }
}