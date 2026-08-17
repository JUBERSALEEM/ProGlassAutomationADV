using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.JobOrder.Repository
{
    /// <summary>
    /// Contract for Job Order data access.
    /// ViewModels depend on this interface, never on the concrete
    /// repository. Allows swapping to mock/test implementations easily.
    /// </summary>
    public interface IJobOrderRepository
    {
        /// <summary>Ensure database + tables exist. Call once at startup.</summary>
        Task InitializeAsync();

        // ── CRUD ──────────────────────────────────────────────────────────

        Task<List<SavedJobOrder>> GetAllAsync();
        Task<SavedJobOrder?> GetByIdAsync(int id);
        Task<SavedJobOrder?> GetByJobOrderIdAsync(string jobOrderId);
        Task<SavedJobOrder> InsertAsync(SavedJobOrder jobOrder);
        Task UpdateAsync(SavedJobOrder jobOrder);
        Task DeleteAsync(int id);

        // ── DUPLICATE DETECTION ───────────────────────────────────────────

        /// <summary>True if a job order with this ID already exists.</summary>
        Task<bool> ExistsByJobOrderIdAsync(string jobOrderId);

        /// <summary>
        /// Suggests the next available ID given a base ID.
        /// e.g. base "JOB-2026-001" → returns "JOB-2026-002" if 001 taken.
        /// </summary>
        Task<string> SuggestNextIdAsync(string baseId);

        // ── LOOKUP FROM PI ────────────────────────────────────────────────

        /// <summary>
        /// Get all job orders sourced from a specific Proforma Invoice.
        /// Since our model is 1 PI = 1 JOB, this typically returns 0 or 1
        /// item, but the method returns a list for future flexibility.
        /// </summary>
        Task<List<SavedJobOrder>> GetBySourceProformaIdAsync(string proformaId);

        /// <summary>
        /// Lightweight analytics projection: JobOrderId, Status, DeliveryDate
        /// only. Excludes FullSnapshotJson. Used by the AI context builder
        /// and dashboards to avoid loading every snapshot blob for simple
        /// counts and sums.
        /// </summary>
        Task<List<JobOrderAnalyticsRow>> GetAnalyticsRowsAsync();
    }
}