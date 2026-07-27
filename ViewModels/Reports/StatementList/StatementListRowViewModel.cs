// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementList/StatementListRowViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 16)
//
// PURPOSE:
//   Wraps StatementListItem for display in the saved statements list.
//   Exposes all display-ready properties needed by StatementListView.xaml.
//   Mirrors TaxInvoiceRowViewModel pattern (Chat 14).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Statement.Repository;

namespace ProGlassAutomation.ViewModels.Reports.StatementList
{
    public class StatementListRowViewModel : ViewModelBase
    {
        private readonly StatementListItem _item;

        public StatementListRowViewModel(StatementListItem item)
        {
            _item = item;
        }

        // ── Identity ──────────────────────────────────────────────────────
        public int Id => _item.Id;
        public string StatementNumber => _item.StatementNumber;
        public string Title => _item.Title;

        // ── Filter summary ────────────────────────────────────────────────
        public string SubTitle => _item.SubTitle;
        public string FilterCustomer => _item.FilterCustomer;
        public string FilterCompany => _item.FilterCompany;
        public string FilterProject => _item.FilterProject;
        public string FilterSalesman => _item.FilterSalesman;

        // ── Financial ─────────────────────────────────────────────────────
        public string TotalInvoicedDisplay => _item.TotalInvoicedDisplay;
        public string TotalOutstandingDisplay => _item.TotalOutstandingDisplay;
        public string OverdueAmountDisplay => _item.OverdueAmountDisplay;
        public decimal OverdueAmount => _item.OverdueAmount;
        public decimal TotalOutstanding => _item.TotalOutstanding;
        public int InvoiceCount => _item.InvoiceCount;

        // ── Status flags ──────────────────────────────────────────────────
        public bool HasPdf => _item.HasPdf;
        public bool HasAttachments => _item.HasAttachments;
        public bool HasOverdue => _item.OverdueAmount > 0;

        // ── PDF path (for open command) ───────────────────────────────────
        public string PdfPath => _item.PdfPath;

        // ── Timestamps ────────────────────────────────────────────────────
        public string CreatedAtDisplay => _item.CreatedAtDisplay;

        // ── Source item (for edit navigation) ────────────────────────────
        public StatementListItem Source => _item;

        // ── Status color key (for DynamicResource brush lookup) ───────────

        /// <summary>
        /// Returns a theme-key suffix for the outstanding amount color.
        /// "Red" if overdue amount > 0, "Amber" if outstanding > 0, else "Green".
        /// Used by converter in XAML to select TintRedBrush / TintAmberBrush /
        /// TintGreenBrush dynamically.
        /// </summary>
        public string OutstandingColorKey
        {
            get
            {
                if (_item.OverdueAmount > 0) return "Red";
                if (_item.TotalOutstanding > 0) return "Amber";
                return "Green";
            }
        }

        /// <summary>
        /// Badge label for attachment indicator.
        /// "📎 With Attachments" or empty string.
        /// </summary>
        public string AttachmentBadge =>
            _item.HasAttachments ? "📎 With Attachments" : "";
    }
}