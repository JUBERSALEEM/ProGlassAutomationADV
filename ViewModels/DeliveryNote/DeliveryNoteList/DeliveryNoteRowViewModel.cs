// ═══════════════════════════════════════════════════════════════════════
// FILE: DeliveryNoteRowViewModel.cs.cs
// PROJECT: ProGlassAutomation
// MODULE: Delivery Note — list rows
//
// DeliveryNoteRowViewModel.cs split out (Phase 11/12 God-VM split) — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.DeliveryNote.Common.DeliveryNoteStatus;
using ProGlassAutomation.Models.DeliveryNote.Common.DeliveryType;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteList
{
    public class DeliveryNoteRowViewModel : ViewModelBase
    {
        public int Id { get; }
        public string DeliveryNoteId { get; }
        public string JobOrderId { get; }
        public string ProformaId { get; }
        public string CustomerName { get; }
        public string ProjectName { get; }
        public string Status { get; }
        public string DeliveryType { get; }
        public bool IsFoc { get; }
        public string DriverName { get; }
        public string VehicleNumber { get; }
        public int TotalOrderedQty { get; }
        public int TotalDeliveredQty { get; }
        public int TotalBalanceQty { get; }
        public int TotalPreviouslyDeliveredQty { get; }
        public int SequenceNumber { get; }
        public DateTime DeliveryDate { get; }
        public string DeliveryTime { get; }
        public DateTime CreatedAt { get; }

        // ── CHAT 19 — Master DN ───────────────────────────────────────────

        public bool IsMasterDn { get; }
        public double DeliveryProgressPercent { get; }
        public bool IsFullyDelivered { get; }

        // ── CHAT 19 INVOICED ─────────────────────────────────────────────

        /// <summary>
        /// True when this DN has been converted to a Tax Invoice.
        /// Set by TaxInvoiceDetailViewModel.SaveAsync() via
        /// DeliveryNoteRepository.MarkAsInvoicedAsync().
        /// Drives invoiced badge in DN list and history sidebar.
        /// </summary>
        public bool HasTaxInvoice { get; }

        /// <summary>
        /// The Tax Invoice reference number linked to this DN.
        /// e.g. "TI-2026-001" or "TI-2026-001-A".
        /// Empty string = not yet invoiced.
        /// </summary>
        public string TaxInvoiceId { get; }

        /// <summary>
        /// Convenience alias for HasTaxInvoice.
        /// Use in XAML bindings where "IsInvoiced" reads more naturally.
        /// </summary>
        public bool IsInvoiced => HasTaxInvoice;

        /// <summary>
        /// Display text for the invoiced badge in the list row.
        /// Returns "✅ {TaxInvoiceId}" when invoiced.
        /// Returns "" when not yet invoiced.
        /// Used in XAML badge Visibility + Text binding.
        /// </summary>
        public string InvoicedBadgeText =>
            HasTaxInvoice && !string.IsNullOrWhiteSpace(TaxInvoiceId)
                ? $"✅ {TaxInvoiceId}"
                : "";

        // ── Computed display properties ───────────────────────────────────

        /// <summary>
        /// List-level display status per the business requirement:
        ///   "Invoiced"   — the DN has been converted to a Tax Invoice.
        ///   "Dn created" — otherwise (created, dispatched, delivered, etc).
        /// The stored Status string is kept intact for the detail/production
        /// flows; this is the user-facing list status.
        /// </summary>
        public string DisplayStatus => IsInvoiced ? "Invoiced" : "Dn created";

        public string StatusLabel { get; }
        public string StatusBrushKey { get; }
        public string TypeLabel { get; }
        public string TypeBrushKey { get; }
        public string DeliveryDateDisplay { get; }
        public string QtySummary { get; }
        public bool HasBalance { get; }

        /// <summary>Backward compat — use IsMasterDn for new code.</summary>
        public bool IsDummy { get; }

        /// <summary>
        /// CHAT 14: Dispatched/Delivered/Signed/Received = invoice-eligible.
        /// CHAT 19: Always false when IsMasterDn = true.
        /// CHAT 19 INVOICED: Also false when already invoiced
        ///   (cannot invoice same DN twice).
        /// </summary>
        public bool CanGenerateInvoice { get; }

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

        public DeliveryNoteRowViewModel(DeliveryNoteListItem e)
        {
            Id = e.Id;
            DeliveryNoteId = e.DeliveryNoteId;
            JobOrderId = e.JobOrderId;
            ProformaId = e.ProformaId;
            CustomerName = string.IsNullOrWhiteSpace(e.CustomerName)
                ? "—" : e.CustomerName;
            ProjectName = string.IsNullOrWhiteSpace(e.ProjectName)
                ? "—" : e.ProjectName;
            Status = e.Status;
            DeliveryType = e.DeliveryType;
            IsFoc = e.IsFoc;
            DriverName = e.DriverName;
            VehicleNumber = e.VehicleNumber;
            TotalOrderedQty = e.TotalOrderedQty;
            TotalDeliveredQty = e.TotalDeliveredQty;
            TotalBalanceQty = e.TotalBalanceQty;
            TotalPreviouslyDeliveredQty = e.TotalPreviouslyDeliveredQty;
            SequenceNumber = e.SequenceNumber;
            DeliveryDate = e.DeliveryDate;
            DeliveryTime = e.DeliveryTime;
            CreatedAt = e.CreatedAt;

            // CHAT 19: Master DN
            IsMasterDn = e.IsMasterDn;

            DeliveryProgressPercent = e.TotalOrderedQty > 0
                ? Math.Min(100.0,
                    (double)e.TotalPreviouslyDeliveredQty
                    / e.TotalOrderedQty * 100.0)
                : 0.0;

            IsFullyDelivered = e.TotalOrderedQty > 0
                && e.TotalBalanceQty == 0;

            // CHAT 19 INVOICED: map from list item
            HasTaxInvoice = e.HasTaxInvoice;
            TaxInvoiceId = e.TaxInvoiceId ?? "";

            var statusInfo = DeliveryNoteStatusMeta.Get(Status);
            StatusLabel = $"{statusInfo.Icon} {statusInfo.DisplayName}";
            StatusBrushKey = statusInfo.BrushKey;

            var typeInfo = DeliveryTypeMeta.Get(DeliveryType);
            TypeLabel = $"{typeInfo.Icon} {typeInfo.DisplayName}";
            TypeBrushKey = typeInfo.BrushKey;

            DeliveryDateDisplay = DeliveryDate.ToString("dd MMM yyyy");
            QtySummary =
                $"{TotalDeliveredQty} delivered  •  {TotalBalanceQty} balance";
            HasBalance = TotalBalanceQty > 0;

            IsDummy = string.Equals(
                DeliveryType, "Dummy",
                StringComparison.OrdinalIgnoreCase);

            // CHAT 19 INVOICED: already invoiced = cannot invoice again
            bool isInvoiceEligibleStatus =
                string.Equals(Status, "Dispatched",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Status, "Delivered",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Status, "Signed",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Status, "Received",
                    StringComparison.OrdinalIgnoreCase);

            CanGenerateInvoice =
                !IsMasterDn &&
                !HasTaxInvoice &&        // CHAT 19 INVOICED: block re-invoicing
                isInvoiceEligibleStatus;
        }
    }
}
