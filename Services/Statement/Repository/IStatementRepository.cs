// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Statement/Repository/IStatementRepository.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 16)
//
// FIX vs PHASE 2 VERSION:
//   StatementListItem: Added FilterDateFrom + FilterDateTo properties.
//   These are needed by:
//     • StatementRepository.ToListItem() — maps from entity
//     • StatementGeneratorViewModel.PrepareEdit() — restores filter
//     • StatementListRowViewModel — exposes to XAML if needed
//   All other interface methods UNCHANGED.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Statement;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Statement.Repository
{
    public interface IStatementRepository
    {
        // ── Lifecycle ─────────────────────────────────────────────────────

        /// <summary>Ensure DB file + tables exist. Called once at app startup.</summary>
        Task InitializeAsync();

        // ── Single record retrieval ───────────────────────────────────────

        /// <summary>
        /// Returns full SavedStatement with LineItems by primary key.
        /// Returns null if not found.
        /// </summary>
        Task<SavedStatement?> GetByIdAsync(int id);

        /// <summary>
        /// Returns full SavedStatement with LineItems by statement number.
        /// Returns null if not found.
        /// </summary>
        Task<SavedStatement?> GetByStatementNumberAsync(string statementNumber);

        // ── List retrieval ────────────────────────────────────────────────

        /// <summary>
        /// Returns all statements as lightweight list items,
        /// ordered by CreatedAt DESC.
        /// </summary>
        Task<List<StatementListItem>> GetAllAsync(
            CancellationToken cancellationToken = default);

        /// <summary>Returns statements filtered by customer name (contains).</summary>
        Task<List<StatementListItem>> GetByCustomerAsync(string customerName);

        /// <summary>Returns statements filtered by company name (contains).</summary>
        Task<List<StatementListItem>> GetByCompanyAsync(string companyName);

        /// <summary>Returns statements filtered by project name (contains).</summary>
        Task<List<StatementListItem>> GetByProjectAsync(string projectName);

        // ── Numbering ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns the next available statement number.
        /// Format: STMT-YYYY-NNN (e.g. STMT-2026-001).
        /// Year resets sequence to 001 on January 1.
        /// </summary>
        Task<string> GetNextStatementNumberAsync();

        // ── CRUD ──────────────────────────────────────────────────────────

        /// <summary>
        /// Inserts a new statement with its line items.
        /// Sets CreatedAt + UpdatedAt to UtcNow.
        /// Returns the saved entity with Id populated.
        /// LineItems are cascade-inserted via navigation property.
        /// </summary>
        Task<SavedStatement> InsertAsync(SavedStatement entity);

        /// <summary>
        /// Updates an existing statement (header + line items) using the
        /// RowVersion captured when the statement was loaded. Replaces all
        /// existing line items (delete-then-reinsert).
        /// </summary>
        Task UpdateAsync(SavedStatement entity, int expectedRowVersion);

        /// <summary>Deletes a statement only if the loaded version is current.</summary>
        Task DeleteAsync(int id, int expectedRowVersion);

        // ── Existence checks ──────────────────────────────────────────────

        Task<bool> ExistsByStatementNumberAsync(string statementNumber);

        // ── PDF path management ───────────────────────────────────────────

        /// <summary>
        /// Updates only PdfPath + HasAttachments fields.
        /// Called after PDF generation to record output path.
        /// Avoids re-saving full snapshot just for path update.
        /// </summary>
        Task UpdatePdfPathAsync(
            int statementId,
            string pdfPath,
            bool hasAttachments,
            int expectedRowVersion);

        /// <summary>
        /// Lightweight analytics projection: TotalInvoiced, TotalOutstanding,
        /// OverdueAmount only. Excludes FullSnapshotJson / FilterJson. Used by
        /// the AI context builder and dashboards to avoid loading every
        /// snapshot blob for simple sums.
        /// </summary>
        Task<List<StatementAnalyticsRow>> GetAnalyticsRowsAsync();
    }

    // ════════════════════════════════════════════════════════════════════
    //  SUPPORTING DTOs
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lightweight statement list item.
    /// Populated from indexed columns only — no JSON parse.
    /// Used by StatementListViewModel for the saved statements list.
    ///
    /// FIX: Added FilterDateFrom + FilterDateTo (needed by PrepareEdit).
    /// </summary>
    public class StatementListItem
    {
        public int Id { get; set; }
        public string StatementNumber { get; set; } = "";
        public string Title { get; set; } = "";
        public string FilterCustomer { get; set; } = "";
        public string FilterCompany { get; set; } = "";
        public string FilterProject { get; set; } = "";
        public string FilterSalesman { get; set; } = "";

        // ── FIX: date filter fields for PrepareEdit restore ───────────────
        public DateTime? FilterDateFrom { get; set; }
        public DateTime? FilterDateTo { get; set; }

        public int InvoiceCount { get; set; }
        public decimal TotalInvoiced { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal OverdueAmount { get; set; }
        public string PdfPath { get; set; } = "";
        public bool HasAttachments { get; set; }
        public DateTime CreatedAt { get; set; }
        public int RowVersion { get; set; }

        // ── Display helpers ───────────────────────────────────────────────

        public string CreatedAtDisplay =>
            CreatedAt.ToString("dd MMM yyyy  HH:mm");

        public string TotalInvoicedDisplay =>
            $"AED {TotalInvoiced:N2}";

        public string TotalOutstandingDisplay =>
            $"AED {TotalOutstanding:N2}";

        public string OverdueAmountDisplay =>
            OverdueAmount > 0 ? $"AED {OverdueAmount:N2}" : "—";

        public bool HasPdf =>
            !string.IsNullOrWhiteSpace(PdfPath) &&
            System.IO.File.Exists(PdfPath);

        /// <summary>
        /// Short subtitle for the list card.
        /// Shows the most specific filter applied.
        /// </summary>
        public string SubTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(FilterCustomer))
                    return FilterCustomer;
                if (!string.IsNullOrWhiteSpace(FilterProject))
                    return FilterProject;
                if (!string.IsNullOrWhiteSpace(FilterCompany))
                    return FilterCompany;
                if (!string.IsNullOrWhiteSpace(FilterSalesman))
                    return FilterSalesman;
                return "All Records";
            }
        }
    }
}