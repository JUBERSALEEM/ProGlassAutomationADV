using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Models.TaxInvoice.TaxInvoiceFamily;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceList
{
    // ═════════════════════════════════════════════════════════════════════
    //  INVOICE ROW VIEWMODEL
    //  One per Tax Invoice in the flat list (All / Draft / Issued tabs).
    //  Pure computed display properties — no WPF commands here.
    //  Commands live in TaxInvoiceListViewModel.
    //
    //  FIX: Added HasPurchaseOrder bool property.
    //       Was missing — bound in TaxInvoiceListView.xaml InvoiceRowTemplate
    //       for PO number visibility. EqualityToVisibilityConverter with "!"
    //       was the broken original approach — replaced with this bool.
    // ═════════════════════════════════════════════════════════════════════

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

        /// <summary>
        /// FIX: Added — was missing, bound in TaxInvoiceListView.xaml.
        /// Controls visibility of PO number row in InvoiceRowTemplate.
        /// True when PurchaseOrderNumber is non-empty.
        /// Replaces broken EqualityToVisibilityConverter with "!" param.
        /// </summary>
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

            // ── Status metadata ───────────────────────────────────────────
            (StatusLabel, StatusIcon, StatusBrushKey) = ResolveStatus(Status);

            // ── Invoice type ──────────────────────────────────────────────
            IsPartial = string.Equals(InvoiceType, "Partial",
                StringComparison.OrdinalIgnoreCase);
            InvoiceTypeLabel = IsPartial
                ? $"Partial ({SequenceLetter})"
                : "Full";
            InvoiceTypeBrushKey = IsPartial
                ? "TintAmberBrush"
                : "TintGreenBrush";

            // ── Date display ──────────────────────────────────────────────
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

            // ── Amounts ───────────────────────────────────────────────────
            GrandTotalDisplay = $"AED {GrandTotal:N2}";
            SubtotalDisplay = $"AED {Subtotal:N2}";
            VatDisplay = $"AED {VatAmount:N2}";
            HasDiscount = DiscountAmount > 0m;

            // ── Flags ─────────────────────────────────────────────────────
            IsDraft = string.Equals(Status, "Draft",
                StringComparison.OrdinalIgnoreCase);
            IsIssued = string.Equals(Status, "Issued",
                StringComparison.OrdinalIgnoreCase);
            IsPaid = string.Equals(Status, "Paid",
                StringComparison.OrdinalIgnoreCase);

            HasDnReference = !string.IsNullOrWhiteSpace(DeliveryNoteReference);

            // FIX: Added — controls PO number row visibility in list view
            HasPurchaseOrder = !string.IsNullOrWhiteSpace(PurchaseOrderNumber);
        }

        // ── Status resolver ───────────────────────────────────────────────

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

    // ═════════════════════════════════════════════════════════════════════
    //  INVOICE FAMILY ROW VIEWMODEL
    //  One per ProformaId family in the grouped "By Project" tab.
    //  Aggregates multiple invoices into one summary row.
    //  Mirrors JobOrderGroupRowViewModel pattern from DN module.
    // ═════════════════════════════════════════════════════════════════════

    public class InvoiceFamilyRowViewModel
    {
        // ── Identity ──────────────────────────────────────────────────────

        public string ProformaId { get; }
        public string CustomerName { get; }
        public string ProjectName { get; }
        public int InvoiceCount { get; }

        // ── Financial aggregates ──────────────────────────────────────────

        public decimal ProformaTotal { get; }
        public decimal InvoicedTotal { get; }
        public decimal BalanceTotal { get; }
        public decimal TotalVat { get; }
        public int TotalQty { get; }
        public decimal TotalSqm { get; }

        // ── Display strings ───────────────────────────────────────────────

        public string ProformaTotalDisplay { get; }
        public string InvoicedTotalDisplay { get; }
        public string BalanceTotalDisplay { get; }

        public string FamilyStatus { get; }
        public string FamilyStatusBrushKey { get; }

        public double ProgressPercent { get; }
        public string ProgressDisplay { get; }

        public bool IsFullyPaid { get; }
        public bool HasBalance { get; }
        public bool HasIssuedInvoices { get; }

        // ── Latest invoice ────────────────────────────────────────────────

        public string LatestInvoiceNumber { get; }
        public int LatestInvoiceId { get; }
        public string LatestIssueDateDisplay { get; }

        // ── Constructor ───────────────────────────────────────────────────

        public InvoiceFamilyRowViewModel(TaxInvoiceFamily family)
        {
            ProformaId = family.ProformaId;
            CustomerName = string.IsNullOrWhiteSpace(family.CustomerName)
                               ? "—" : family.CustomerName;
            ProjectName = string.IsNullOrWhiteSpace(family.ProjectName)
                               ? "—" : family.ProjectName;
            InvoiceCount = family.MemberCount;

            ProformaTotal = family.ProformaTotal;
            InvoicedTotal = family.InvoicedTotal;
            BalanceTotal = family.BalanceTotal;
            TotalVat = family.TotalVat;
            TotalQty = family.TotalQty;
            TotalSqm = family.TotalSqm;

            ProformaTotalDisplay = $"AED {ProformaTotal:N2}";
            InvoicedTotalDisplay = $"AED {InvoicedTotal:N2}";
            BalanceTotalDisplay = $"AED {BalanceTotal:N2}";

            FamilyStatus = family.FamilyStatus;
            FamilyStatusBrushKey = ResolveFamilyStatusBrush(FamilyStatus);

            IsFullyPaid = family.IsFullyPaid;
            HasBalance = family.HasBalance;
            HasIssuedInvoices = family.HasIssuedInvoices;

            ProgressPercent = ProformaTotal > 0m
                ? Math.Min(100.0,
                    Math.Round(
                        (double)(InvoicedTotal / ProformaTotal) * 100, 1))
                : 0.0;

            ProgressDisplay =
                $"AED {InvoicedTotal:N2} of AED {ProformaTotal:N2}";

            var latest = family.LatestInvoice;
            LatestInvoiceNumber = family.LatestInvoiceNumber;
            LatestInvoiceId = latest?.Id ?? 0;
            LatestIssueDateDisplay = family.LatestIssueDate == default
                ? "—"
                : family.LatestIssueDate.ToString("dd MMM yyyy");
        }

        // ── Factory ───────────────────────────────────────────────────────

        public static IReadOnlyList<InvoiceFamilyRowViewModel> FromFamilies(
            IEnumerable<TaxInvoiceFamily>? families)
        {
            if (families == null)
                return Array.Empty<InvoiceFamilyRowViewModel>();

            return families
                .Select(f => new InvoiceFamilyRowViewModel(f))
                .ToList();
        }

        // ── Status brush resolver ─────────────────────────────────────────

        private static string ResolveFamilyStatusBrush(string status) =>
            status switch
            {
                "Paid" => "TintGreenBrush",
                "Overdue" => "TintRedBrush",
                "HasCancelled" => "TintRedBrush",
                "PartiallyInvoiced" => "TintAmberBrush",
                "PartiallyPaid" => "TintAmberBrush",
                "Draft" => "TintIndigoBrush",
                _ => "TintIndigoBrush"
            };
    }
}