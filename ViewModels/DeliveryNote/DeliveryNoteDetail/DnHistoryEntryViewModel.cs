// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/DeliveryNote/DeliveryNoteDetail/DnHistoryEntryViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Delivery Note — detail
//
// History-sidebar row view-model. Extracted from the large
// DeliveryNoteDetailViewModel file (Phase 11/12 God-VM split) — behaviour
// unchanged, relocated for organisation only.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.DeliveryNote.Common.DeliveryNoteStatus;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.ViewModels;
using System;

namespace ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail
{
    public class DnHistoryEntryViewModel : ViewModelBase
    {
        public string DeliveryNoteId { get; }
        public string DeliveryType { get; }
        public string Status { get; }
        public int DeliveredQty { get; }
        public int BalanceQty { get; }
        public string DeliveryDate { get; }
        public string DriverName { get; }
        public string TypeIcon { get; }
        public string StatusBrushKey { get; }
        public int OrderedQty { get; }
        public int CumulativePrevQty { get; }
        public int CumulativeBalanceQty { get; }
        public int SequenceNumber { get; }

        // ── CHAT 19 INVOICED ─────────────────────────────────────────────

        /// <summary>
        /// True when this DN has been converted to a Tax Invoice.
        /// Populated from DeliveryHistoryEntry.HasTaxInvoice which is
        /// set by DeliveryNoteRepository.GetDeliveryHistoryAsync().
        /// Drives the invoiced badge in the history sidebar row.
        /// </summary>
        public bool HasTaxInvoice { get; }

        /// <summary>
        /// The Tax Invoice reference linked to this DN.
        /// e.g. "TI-2026-001" — empty when not yet invoiced.
        /// </summary>
        public string TaxInvoiceId { get; }

        /// <summary>Convenience alias — use in XAML where IsInvoiced reads naturally.</summary>
        public bool IsInvoiced => HasTaxInvoice;

        /// <summary>
        /// Badge text for history sidebar row:
        ///   "✅ TI-2026-001" — when invoiced
        ///   ""               — when not yet invoiced
        /// Bind to TextBlock.Text + Visibility.
        /// </summary>
        public string InvoicedBadgeText =>
            HasTaxInvoice && !string.IsNullOrWhiteSpace(TaxInvoiceId)
                ? $"✅ {TaxInvoiceId}"
                : "";

        /// <summary>
        /// True when this DN can be invoiced:
        ///   • Not already invoiced (HasTaxInvoice = false)
        ///   • Status is Dispatched/Delivered/Signed/Received
        /// Used to control "🧾 Tax Invoice" button enabled state in sidebar.
        /// </summary>
        public bool CanInvoice =>
            !HasTaxInvoice && (
                string.Equals(Status, "Dispatched",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Status, "Delivered",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Status, "Signed",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Status, "Received",
                    StringComparison.OrdinalIgnoreCase));

        // ── Standard properties ───────────────────────────────────────────

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        private bool _isCurrentDn;
        public bool IsCurrentDn
        {
            get => _isCurrentDn;
            set
            {
                if (_isCurrentDn == value) return;
                _isCurrentDn = value;
                OnPropertyChanged();
            }
        }

        private int _attachmentCount;
        public int AttachmentCount
        {
            get => _attachmentCount;
            set
            {
                if (_attachmentCount == value) return;
                _attachmentCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAttachments));
            }
        }

        public bool HasAttachments => _attachmentCount > 0;
        public bool HasBalance => BalanceQty > 0;

        public DnHistoryEntryViewModel(DeliveryHistoryEntry e)
        {
            DeliveryNoteId = e.DeliveryNoteId;
            DeliveryType = e.DeliveryType;
            Status = e.Status;
            DeliveredQty = e.DeliveredQty;
            BalanceQty = e.BalanceQty;
            DeliveryDate = e.DeliveryDate.ToString("dd MMM yyyy");
            DriverName = e.DriverName;
            OrderedQty = e.OrderedQty;
            CumulativePrevQty = e.CumulativePrevQty;
            CumulativeBalanceQty = e.CumulativeBalanceQty;
            SequenceNumber = e.SequenceNumber;

            // CHAT 19 INVOICED: map invoice state from history entry
            // Set by DeliveryNoteRepository.GetDeliveryHistoryAsync()
            // which reads HasTaxInvoice + TaxInvoiceId from DN record
            HasTaxInvoice = e.HasTaxInvoice;
            TaxInvoiceId = e.TaxInvoiceId ?? "";

            TypeIcon = e.DeliveryType switch
            {
                "Rework" => "🔧",
                "Return" => "↩️",
                _ => "📦"
            };

            var statusInfo = DeliveryNoteStatusMeta.Get(e.Status);
            StatusBrushKey = statusInfo.BrushKey;
        }
    }
}
