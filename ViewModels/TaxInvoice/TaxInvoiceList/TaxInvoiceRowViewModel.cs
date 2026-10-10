// ═══════════════════════════════════════════════════════════════════════
// FILE: TaxInvoiceRowViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Tax Invoice — list rows
//
// TaxInvoiceRowViewModel split out (Phase 11/12 God-VM split) — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Models.TaxInvoice.TaxInvoiceFamily;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceList
{
    public class TaxInvoiceRowViewModel : ViewModelBase
    {
        // ── Raw data ──────────────────────────────────────────────────────

        public int Id { get; }
        public string InvoiceNumber { get; }
        public string ProformaId { get; }
        public string DeliveryNoteReference { get; }
        public string JobOrderId { get; }
        public string PurchaseOrderNumber { get; }
        public string InvoiceType { get; }
        public string SequenceLetter { get; }
        public int SequenceNumber { get; }
        public string Status { get; }
        public string CustomerName { get; }
        public string ProjectName { get; }
        public string CompanyName { get; }
        public string PaymentTerms { get; }
        public DateTime IssueDate { get; }
        public DateTime DueDate { get; }
        public decimal Subtotal { get; }
        public decimal DiscountAmount { get; }
        public decimal AfterDiscount { get; }
        public decimal VatAmount { get; }
        public decimal GrandTotal { get; }
        public int TotalQty { get; }
        public decimal TotalSqm { get; }
        public DateTime CreatedAt { get; }
        public int RowVersion { get; }

        // ── Computed display ──────────────────────────────────────────────

        public string StatusLabel { get; }
        public string StatusBrushKey { get; }
        public string StatusIcon { get; }

        public string InvoiceTypeLabel { get; }
        public string InvoiceTypeBrushKey { get; }

        public string IssueDateDisplay { get; }
        public string DueDateDisplay { get; }

        public string GrandTotalDisplay { get; }
        public string SubtotalDisplay { get; }
        public string VatDisplay { get; }

        public bool IsPartial { get; }
        public bool IsOverdue { get; }
        public bool IsDraft { get; }
        public bool IsIssued { get; }
        public bool IsPaid { get; }
        public bool HasDiscount { get; }
        public bool HasDnReference { get; }
        public bool HasPurchaseOrder { get; }

        public string DueDateLabel { get; }

        // ── Selection ─────────────────────────────────────────────────────

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

        /// <summary>
        /// True when this is the CURRENTLY loaded invoice in detail view.
        /// Used for green highlight in family sidebar.
        /// Set by detail VM when family loads.
        /// </summary>
        private bool _isCurrentInvoice;
        public bool IsCurrentInvoice
        {
            get => _isCurrentInvoice;
            set
            {
                if (_isCurrentInvoice == value) return;
                _isCurrentInvoice = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Attachment count for this specific invoice.
        /// Populated by detail VM when family loads.
        /// </summary>
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

        // ── Constructor ───────────────────────────────────────────────────

        public TaxInvoiceRowViewModel(TaxInvoiceListItem e)
        {
            Id = e.Id;
            InvoiceNumber = e.InvoiceNumber;
            ProformaId = e.ProformaId;
            DeliveryNoteReference = e.DeliveryNoteReference;
            JobOrderId = e.JobOrderId;
            PurchaseOrderNumber = e.PurchaseOrderNumber;
            InvoiceType = e.InvoiceType;
            SequenceLetter = e.SequenceLetter;
            SequenceNumber = e.SequenceNumber;
            Status = e.Status;
            CustomerName = string.IsNullOrWhiteSpace(e.CustomerName)
                ? "—" : e.CustomerName;
            ProjectName = string.IsNullOrWhiteSpace(e.ProjectName)
                ? "—" : e.ProjectName;
            CompanyName = e.CompanyName;
            PaymentTerms = e.PaymentTerms;
            IssueDate = e.IssueDate;
            DueDate = e.DueDate;
            Subtotal = e.Subtotal;
            DiscountAmount = e.DiscountAmount;
            AfterDiscount = e.AfterDiscount;
            VatAmount = e.VatAmount;
            GrandTotal = e.GrandTotal;
            TotalQty = e.TotalQty;
            TotalSqm = e.TotalSqm;
            CreatedAt = e.CreatedAt;
            RowVersion = e.RowVersion;

            (StatusLabel, StatusIcon, StatusBrushKey) = ResolveStatus(Status);

            IsPartial = string.Equals(InvoiceType, "Partial",
                StringComparison.OrdinalIgnoreCase);
            InvoiceTypeLabel = IsPartial
                ? $"Partial ({SequenceLetter})"
                : "Full";
            InvoiceTypeBrushKey = IsPartial
                ? "TintAmberBrush"
                : "TintGreenBrush";

            IssueDateDisplay = IssueDate == default
                ? "—"
                : IssueDate.ToString("dd MMM yyyy");

            IsOverdue = DueDate != default
                && DueDate < DateTime.Today
                && !string.Equals(Status, "Paid",
                    StringComparison.OrdinalIgnoreCase)
                && !string.Equals(Status, "Cancelled",
                    StringComparison.OrdinalIgnoreCase)
                && !string.Equals(Status, "Void",
                    StringComparison.OrdinalIgnoreCase);

            DueDateDisplay = DueDate == default
                ? "—"
                : DueDate.ToString("dd MMM yyyy");

            DueDateLabel = IsOverdue
                ? $"⚠️ OVERDUE — {DueDateDisplay}"
                : DueDateDisplay;

            GrandTotalDisplay = $"AED {GrandTotal:N2}";
            SubtotalDisplay = $"AED {Subtotal:N2}";
            VatDisplay = $"AED {VatAmount:N2}";
            HasDiscount = DiscountAmount > 0m;

            IsDraft = string.Equals(Status, "Draft",
                StringComparison.OrdinalIgnoreCase);
            IsIssued = string.Equals(Status, "Issued",
                StringComparison.OrdinalIgnoreCase);
            IsPaid = string.Equals(Status, "Paid",
                StringComparison.OrdinalIgnoreCase);

            HasDnReference = !string.IsNullOrWhiteSpace(DeliveryNoteReference);
            HasPurchaseOrder = !string.IsNullOrWhiteSpace(PurchaseOrderNumber);
        }

        private static (string label, string icon, string brushKey)
            ResolveStatus(string status) => status switch
            {
                "Draft" => ("Draft", "📝", "TintIndigoBrush"),
                "Issued" => ("Issued", "📤", "TintAmberBrush"),
                "Paid" => ("Paid", "✅", "TintGreenBrush"),
                "PartiallyPaid" => ("Partially Paid", "💰", "TintAmberBrush"),
                "Overdue" => ("Overdue", "⚠️", "TintRedBrush"),
                "Cancelled" => ("Cancelled", "❌", "TintRedBrush"),
                "Void" => ("Void", "🚫", "TintRedBrush"),
                _ => (status, "📄", "TintIndigoBrush")
            };
    }
}
