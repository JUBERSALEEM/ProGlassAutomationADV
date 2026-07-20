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