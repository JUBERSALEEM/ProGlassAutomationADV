using ProGlassAutomation.Models.TaxInvoice;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.TaxInvoice.Repository
{
    /// <summary>
    /// Repository contract for Tax Invoice persistence (taxinvoices.db).
    /// </summary>
    public interface ITaxInvoiceRepository
    {
        Task InitializeAsync();

        Task<Models.TaxInvoice.TaxInvoice?> GetByIdAsync(int id);
        Task<Models.TaxInvoice.TaxInvoice?> GetByInvoiceNumberAsync(string invoiceNumber);
        Task<Models.TaxInvoice.TaxInvoice?> GetByDeliveryNoteIdAsync(string deliveryNoteId);

        Task<List<Models.TaxInvoice.TaxInvoice>> GetAllAsync();

        Task<int> GetNextSequenceAsync();
        Task<string> BuildInvoiceNumberAsync(int sequence);

        Task<Models.TaxInvoice.TaxInvoice> InsertAsync(Models.TaxInvoice.TaxInvoice entity);
        Task UpdateAsync(Models.TaxInvoice.TaxInvoice entity);
        Task DeleteAsync(int id);

        Task<bool> ExistsByInvoiceNumberAsync(string invoiceNumber);
    }
}