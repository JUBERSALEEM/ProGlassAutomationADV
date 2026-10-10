using ProGlassAutomation.Models.CreditNote.SavedCreditNote;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.CreditNote.Repository
{
    /// <summary>
    /// Persistence contract for Credit Notes (ERP hardening — Phase 4).
    /// Mirrors ITaxInvoiceRepository so the module behaves like the rest of
    /// the fiscal documents.
    /// </summary>
    public interface ICreditNoteRepository
    {
        /// <summary>Create the database / apply schema migrations.</summary>
        Task InitializeAsync();

        // ── Retrieval ───────────────────────────────────────────────────
        Task<SavedCreditNote?> GetByIdAsync(int id);
        Task<SavedCreditNote?> GetByNumberAsync(string creditNoteNumber);
        Task<List<SavedCreditNote>> GetAllAsync(bool includeVoided = false);

        /// <summary>All credit notes raised against one tax invoice.</summary>
        Task<List<SavedCreditNote>> GetByTaxInvoiceAsync(string taxInvoiceReference);

        /// <summary>All credit notes in a proforma family.</summary>
        Task<List<SavedCreditNote>> GetByProformaAsync(string proformaId);

        // ── Numbering ────────────────────────────────────────────────────

        /// <summary>Next ordinal within the CN-YYYY series for a given year.</summary>
        Task<int> GetNextSequenceNumberAsync(int year);

        /// <summary>Builds the immutable "CN-YYYY-NNN" number for a year+sequence.</summary>
        string BuildCreditNoteNumber(int year, int sequenceNumber);

        Task<bool> ExistsByNumberAsync(string creditNoteNumber);

        // ── CRUD ─────────────────────────────────────────────────────────
        Task<SavedCreditNote> InsertAsync(SavedCreditNote entity);
        /// <summary>Update using the required RowVersion captured by the caller.</summary>
        Task UpdateAsync(SavedCreditNote entity, int expectedRowVersion);

        /// <summary>Physically delete — only ever a childless Draft.</summary>
        Task DeleteAsync(int id);

        /// <summary>Soft-close (void) an issued credit note; retained for audit.</summary>
        Task VoidAsync(int id, string reason);
    }
}
