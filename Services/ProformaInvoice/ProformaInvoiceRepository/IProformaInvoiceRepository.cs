using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository
{
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

        /// <summary>Fetches by numeric primary key.</summary>
        Task<SavedProformaInvoice> GetByIdAsync(int id);

        /// <summary>Fetches by user-facing ProformaId.</summary>
        Task<SavedProformaInvoice> GetByProformaIdAsync(string proformaId);

        /// <summary>Returns all invoices ordered by CreatedAt DESC (for future Load List UI).</summary>
        Task<List<SavedProformaInvoice>> GetAllAsync();

        Task DeleteAsync(int id);
    }
}