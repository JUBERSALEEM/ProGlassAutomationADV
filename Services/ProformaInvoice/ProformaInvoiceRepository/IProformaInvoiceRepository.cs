using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository
{
    /// <summary>
    /// Repository contract for saved proforma invoices (proformainvoices.db).
    ///
    /// BUG-07: Return types now nullable — GetByIdAsync and
    ///         GetByProformaIdAsync can legitimately return null when
    ///         the requested record does not exist.
    /// BUG-12: CancellationToken added on long-running queries.
    ///         Default value keeps backward compatibility with callers.
    /// </summary>
    public interface IProformaInvoiceRepository
    {
        /// <summary>Ensure database file + tables exist. Called once at app startup.</summary>
        Task InitializeAsync();

        /// <summary>Returns true if an invoice with the given ProformaId already exists.</summary>
        Task<bool> ExistsByProformaIdAsync(string proformaId);

        /// <summary>
        /// Suggests the next available ProformaId based on a duplicate.
        /// E.g. "PI-2026-001" → "PI-2026-002" (auto-increments the numeric suffix).
        /// </summary>
        Task<string> SuggestNextIdAsync(string currentId);

        /// <summary>Persist a new invoice. Throws if ProformaId already exists.</summary>
        Task<int> InsertAsync(SavedProformaInvoice invoice);

        /// <summary>Update the invoice matching ProformaId. Creates if missing.</summary>
        Task UpdateAsync(SavedProformaInvoice invoice);

        /// <summary>
        /// Fetches by numeric primary key.
        /// BUG-07: Returns null if not found — callers must handle nulls.
        /// </summary>
        Task<SavedProformaInvoice?> GetByIdAsync(int id);

        /// <summary>
        /// Fetches by user-facing ProformaId.
        /// BUG-07: Returns null if not found — callers must handle nulls.
        /// </summary>
        Task<SavedProformaInvoice?> GetByProformaIdAsync(string proformaId);

        /// <summary>
        /// Returns all invoices ordered by CreatedAt DESC.
        /// BUG-12: Optional cancellation token for long-running loads.
        /// </summary>
        Task<List<SavedProformaInvoice>> GetAllAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// PERF-2: analytics-only projection for dashboards.
        ///
        /// GetAllAsync materialises every column of every row, including
        /// FullSnapshotJson — a complete invoice snapshot blob — and the
        /// dashboards were reading all of it just to count rows and sum
        /// totals. This reads only the 5 scalar columns they actually use,
        /// with AsNoTracking, so the blob column is never touched.
        ///
        /// Deliberately a NEW method: GetAllAsync has 23 callers and some
        /// of them genuinely need the snapshot, so changing it in place
        /// would have been unsafe.
        /// </summary>
        Task<List<ProformaAnalyticsRow>> GetAnalyticsRowsAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Bounded, DB-side search for the global-search box. Filters the
        /// indexed columns with a case-insensitive contains and returns at
        /// most <paramref name="take"/> rows, projecting only the columns
        /// search displays. Avoids loading every row (and its
        /// FullSnapshotJson blob) just to find top matches.
        /// </summary>
        Task<List<ProformaSearchRow>> SearchAsync(
            string term, int take);

        Task DeleteAsync(int id);
    }
}