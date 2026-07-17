using ProGlassAutomation.Models.SheetStore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.SheetStore
{
    /// <summary>
    /// Contract for Sheet Store data access.
    /// ViewModels depend on this interface, never on the concrete repository.
    /// Allows swapping to mock/test implementations easily.
    /// </summary>
    public interface ISheetStoreRepository
    {
        // ============ CRUD ============
        Task<List<GlassSheet>> GetAllAsync();
        Task<GlassSheet?> GetByIdAsync(int id);
        Task<GlassSheet> AddAsync(GlassSheet sheet);
        Task UpdateAsync(GlassSheet sheet);
        Task DeleteAsync(int id);

        // ============ BUSINESS OPERATIONS ============
        Task BuyStockAsync(int sheetId, int quantity, decimal? newPurchasePrice = null);
        Task UseStockAsync(int sheetId, int quantity);

        // ============ LOOKUPS (for filter dropdowns) ============
        Task<List<string>> GetCategoriesAsync();
        Task<List<string>> GetColorsAsync();
        Task<List<string>> GetSuppliersAsync();
        Task<List<int>> GetThicknessesAsync();
    }
}