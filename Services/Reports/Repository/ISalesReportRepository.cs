using ProGlassAutomation.Models.Reports.Sales;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Reports.Repository
{
    public interface ISalesReportRepository
    {
        /// <summary>
        /// Fetches all sales reports from the database, optionally filtering them with the filter model.
        /// </summary>
        Task<List<SalesReport>> GetSalesReportAsync(SalesFilterModel? filter = null);

        /// <summary>
        /// Retrieves list of distinct customer names that have been invoiced.
        /// </summary>
        Task<List<string>> GetUniqueCustomersAsync();

        /// <summary>
        /// Retrieves list of distinct project names that have been invoiced.
        /// </summary>
        Task<List<string>> GetUniqueProjectsAsync();

        /// <summary>
        /// Retrieves list of distinct salesmen names that have been assigned to invoices.
        /// </summary>
        Task<List<string>> GetUniqueSalesmenAsync();

        /// <summary>
        /// Retrieves list of distinct company names that have issued invoices.
        /// </summary>
        Task<List<string>> GetUniqueCompaniesAsync();

        /// <summary>
        /// Returns the complete workflow document chain for a specific Tax Invoice number.
        /// Returns a tuple of strings displaying the IDs: (ProformaId, JobOrderId, DeliveryNoteId, TaxInvoiceNumber).
        /// </summary>
        Task<(string ProformaId, string JobOrderId, string DeliveryNoteId, string InvoiceNumber)?> GetDocumentChainAsync(string invoiceNumber);
    }
}