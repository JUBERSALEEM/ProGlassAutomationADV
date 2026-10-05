// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementList/StatementListRowViewModel.cs
// PROJECT: ProGlassAutomation
// CHAT 17 — CLEANED + FIXED
//
// CHANGES vs CHAT 16:
//   L7  — Removed AttachmentBadge (dead code, emoji string, unused by XAML)
//   L8  — Removed OutstandingColorKey (dead code, no converter contract)
//   Both removed cleanly — nothing in redesigned XAML references them.
//   All other properties unchanged.
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

        /// <summary>
        /// True when a PDF has been generated and exists on disk.
        /// Drives the "PDF" button IsEnabled state.
        /// </summary>
        public bool HasPdf => _item.HasPdf;

        /// <summary>
        /// True when the statement has associated attachments.
        /// Drives the "Attachments" badge visibility.
        /// </summary>
        public bool HasAttachments => _item.HasAttachments;

        /// <summary>
        /// True when any overdue amount exists.
        /// Drives: left accent bar color, status pill color/text,
        /// outstanding text color, overdue sub-line visibility.
        /// </summary>
        public bool HasOverdue => _item.OverdueAmount > 0;

        // ── PDF path (for OpenPdfCommand) ─────────────────────────────────
        public string PdfPath => _item.PdfPath;

        // ── Timestamps ────────────────────────────────────────────────────
        public string CreatedAtDisplay => _item.CreatedAtDisplay;

        // ── Source item (for edit navigation callback) ────────────────────
        public StatementListItem Source => _item;
    }
}