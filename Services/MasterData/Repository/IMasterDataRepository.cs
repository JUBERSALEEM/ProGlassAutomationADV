using ProGlassAutomation.Models.MasterData;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.MasterData.Repository
{
    /// <summary>
    /// Repository contract for the Master Data database (masterdata.db).
    ///
    /// Unified interface for all 4 master data entities:
    ///   Companies, Clients, Projects, Salesmen
    ///
    /// Why one interface instead of 4 separate?
    ///   - All 4 entities are tightly coupled (saved from same invoice)
    ///   - All share the same upsert + search + list pattern
    ///   - One DB context → one repository → less file sprawl
    ///   - Easier to test and wire in App.xaml.cs
    ///
    /// Upsert pattern:
    ///   SaveOrUpdate methods match by uniqueness key (case-insensitive).
    ///   If match found → update fields + increment UsageCount + set LastUsedAt.
    ///   If no match → insert new record with UsageCount=1.
    ///
    /// List pattern:
    ///   GetAll methods return ordered by UsageCount DESC then LastUsedAt DESC.
    ///   Most-used records appear first in card lists and dropdowns.
    ///
    /// Search pattern:
    ///   Search methods accept a query string and match against Name
    ///   fields using case-insensitive LIKE. Used for ComboBox
    ///   autocomplete and card filtering.
    ///
    /// Auto-save flow:
    ///   ProformaInvoiceMapper.BuildEntityFromWizard() calls
    ///   AutoSaveFromInvoiceAsync() after building the entity.
    ///   This extracts company/client/project/salesman from the
    ///   wizard VMs and upserts them into masterdata.db.
    ///   No UI interaction required — completely invisible to user.
    /// </summary>
    public interface IMasterDataRepository
    {
        /// <summary>Ensure database file + all 4 tables exist.</summary>
        Task InitializeAsync();

        // ══════════════════════════════════════════════════════════════════
        //  COMPANIES
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Upsert a company profile. Matches by CompanyName + CompanyTrn
        /// (case-insensitive). Updates existing or inserts new.
        /// Returns the saved/updated entity.
        /// </summary>
        Task<SavedCompany> SaveOrUpdateCompanyAsync(SavedCompany company);

        /// <summary>
        /// Returns all companies ordered by UsageCount DESC, LastUsedAt DESC.
        /// Used for Step 1 card list.
        /// </summary>
        Task<List<SavedCompany>> GetAllCompaniesAsync();

        /// <summary>
        /// Search companies by name (case-insensitive LIKE).
        /// Used for autocomplete / filtering.
        /// </summary>
        Task<List<SavedCompany>> SearchCompaniesAsync(string query);

        /// <summary>Delete a company profile by ID.</summary>
        Task DeleteCompanyAsync(int id);

        // ══════════════════════════════════════════════════════════════════
        //  CLIENTS
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Upsert a client profile. Matches by CustomerName + CustomerTrn
        /// (case-insensitive). Updates existing or inserts new.
        /// </summary>
        Task<SavedClient> SaveOrUpdateClientAsync(SavedClient client);

        /// <summary>
        /// Returns all clients ordered by UsageCount DESC, LastUsedAt DESC.
        /// Used for Step 2 (future) card list.
        /// </summary>
        Task<List<SavedClient>> GetAllClientsAsync();

        /// <summary>
        /// Search clients by name (case-insensitive LIKE).
        /// </summary>
        Task<List<SavedClient>> SearchClientsAsync(string query);

        /// <summary>Delete a client profile by ID.</summary>
        Task DeleteClientAsync(int id);

        // ══════════════════════════════════════════════════════════════════
        //  PROJECTS
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Upsert a project profile. Matches by ProjectIdentifierName +
        /// CustomerName (case-insensitive). Updates existing or inserts new.
        /// </summary>
        Task<SavedProject> SaveOrUpdateProjectAsync(SavedProject project);

        /// <summary>
        /// Returns all projects ordered by UsageCount DESC, LastUsedAt DESC.
        /// Used for Step 3 (future) card list.
        /// </summary>
        Task<List<SavedProject>> GetAllProjectsAsync();

        /// <summary>
        /// Search projects by name (case-insensitive LIKE).
        /// </summary>
        Task<List<SavedProject>> SearchProjectsAsync(string query);

        /// <summary>
        /// Returns all projects for a specific customer.
        /// Used when user selects a client card — shows only their projects.
        /// </summary>
        Task<List<SavedProject>> GetProjectsByCustomerAsync(string customerName);

        /// <summary>Delete a project profile by ID.</summary>
        Task DeleteProjectAsync(int id);

        // ══════════════════════════════════════════════════════════════════
        //  SALESMEN
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Upsert a salesman. Matches by Name (case-insensitive).
        /// Updates existing or inserts new.
        /// </summary>
        Task<SavedSalesman> SaveOrUpdateSalesmanAsync(SavedSalesman salesman);

        /// <summary>
        /// Returns all salesmen ordered by UsageCount DESC, LastUsedAt DESC.
        /// Used for salesman ComboBox dropdown.
        /// </summary>
        Task<List<SavedSalesman>> GetAllSalesmenAsync();

        /// <summary>
        /// Search salesmen by name (case-insensitive LIKE).
        /// Used for ComboBox autocomplete.
        /// </summary>
        Task<List<SavedSalesman>> SearchSalesmenAsync(string query);

        /// <summary>Delete a salesman by ID.</summary>
        Task DeleteSalesmanAsync(int id);

        // ══════════════════════════════════════════════════════════════════
        //  BULK AUTO-SAVE (called from ProformaInvoiceMapper)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Convenience method — extracts company, client, project, and
        /// salesman from raw field values and upserts all 4 in one call.
        /// Skips any entity where the primary name field is empty.
        ///
        /// Called by ProformaInvoiceMapper.BuildEntityFromWizard() after
        /// building the invoice entity. Completely invisible to the user.
        ///
        /// Parameters mirror the wizard VM field values directly:
        ///   companyName, companyTrn, companyLocation
        ///   customerName, customerTrn, customerRef, salesRep,
        ///   preparedBy, billingAddress, attentionName, contactPhone
        ///   projectName, contractNo, siteDelivery, lpoNumber
        /// </summary>
        Task AutoSaveFromInvoiceAsync(
            // Company fields
            string companyName,
            string companyTrn,
            string companyLocation,

            // Client fields
            string customerName,
            string customerTrn,
            string customerReferenceCode,
            string salesRepresentative,
            string documentPreparedBy,
            string billingAddress,
            string attentionExecutiveName,
            string clientContactPhone,

            // Project fields
            string projectIdentifierName,
            string projectContractNo,
            string siteDeliveryLocation,
            string lpoNumber);
    }
}