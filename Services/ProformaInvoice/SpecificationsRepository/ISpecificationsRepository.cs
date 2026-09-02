using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.ProformaInvoice.SpecificationsRepository
{
    /// <summary>
    /// Contract for spec library CRUD operations.
    /// Enables future testing/mocking + swappable persistence.
    /// </summary>
    public interface ISpecificationsRepository
    {
        /// <summary>
        /// Load all specs, optionally filtered by glass type.
        /// Sorted by: UsageCount DESC, then LastUsedAt DESC (most useful first).
        /// </summary>
        Task<List<SavedSpecification>> GetAllAsync(string glassTypeFilter = null);

        /// <summary>
        /// Save or update a spec.
        /// If a spec with same GlassType + SpecName exists, updates its rate
        /// and increments usage count. Otherwise creates new record.
        /// </summary>
        Task<SavedSpecification> SaveOrUpdateAsync(string glassType, string specName, decimal pricePerSqm);

        /// <summary>
        /// Specifications Center overload. Same save, plus the catalogue
        /// columns: category/colour/thickness parsed from the spec, the
        /// matched Sheet Store size, and the originating proforma.
        ///
        /// The sheet values are recorded for DISPLAY and traceability only
        /// ("which sheet was this quoted against"). Pricing still comes from
        /// pricePerSqm, i.e. from Step 4 -- a later Sheet Store price edit
        /// must never silently re-cost an existing specification.
        ///
        /// Any argument may be null/0; those simply leave the column blank,
        /// so callers that do not know a value are not forced to invent one.
        /// </summary>
        Task<SavedSpecification> SaveOrUpdateAsync(
            string glassType,
            string specName,
            decimal pricePerSqm,
            string? category,
            string? color,
            int thicknessMm,
            int sheetWidthMm,
            int sheetHeightMm,
            int sheetId,
            string? proformaId);

        /// <summary>
        /// Increment usage count and update LastUsedAt when a spec is picked
        /// in Step 5. Called when spec is selected from dropdown.
        /// </summary>
        Task MarkAsUsedAsync(int specId);

        /// <summary>
        /// Delete a saved spec (user-initiated cleanup).
        /// </summary>
        Task DeleteAsync(int specId);

        /// <summary>
        /// Ensure database file and tables exist.
        /// Called at app startup.
        /// </summary>
        Task InitializeAsync();
    }
}