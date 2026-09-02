using ProGlassAutomation.Models.SheetStore.SheetStoreHistory;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.SheetStore.SheetStoreHistory
{
    public interface ISheetHistoryRepository
    {
        /// <summary>Get all history entries, newest first.</summary>
        Task<List<SheetHistory>> GetAllAsync();

        /// <summary>Get history for a specific sheet.</summary>
        Task<List<SheetHistory>> GetBySheetIdAsync(int sheetId);

        /// <summary>Record a new history entry.</summary>
        /// <summary>
        /// Records one sheet movement.
        ///
        /// The traceability arguments are OPTIONAL so every existing caller
        /// compiles unchanged, and so a manual Add/Edit — which has no
        /// document context — is not forced to invent one.
        /// </summary>
        Task LogAsync(int sheetId, string sheetDescription, string action, string details,
                      int quantityChanged = 0, decimal? valueChanged = null,
                      string? documentReference = null,
                      string? customerName = null,
                      string? projectName = null,
                      string? salesman = null,
                      string? panePosition = null);

        /// <summary>Get most recent timestamp (for "Last Updated" display).</summary>
        Task<System.DateTime?> GetLastUpdatedAsync();

        /// <summary>Total count of history entries.</summary>
        Task<int> GetTotalCountAsync();
    }
}