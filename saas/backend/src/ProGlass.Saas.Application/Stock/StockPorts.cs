using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Application.Stock;

/// <summary>
/// The outcome of an atomic stock movement. The store never throws for
/// business outcomes — the service maps them to typed errors.
/// </summary>
public enum StockMovementOutcome
{
    Ok = 0,
    NotFound,
    InsufficientStock
}

/// <summary>Result of a buy/use/receive/consume movement.</summary>
public sealed record StockMovementResult(
    StockMovementOutcome Outcome,
    int? BalanceAfter,       // sheets
    decimal? QuantityBalance // inventory items
)
{
    public static readonly StockMovementResult NotFound = new(StockMovementOutcome.NotFound, null, null);

    public static StockMovementResult Ok(int balanceAfter) =>
        new(StockMovementOutcome.Ok, balanceAfter, null);

    public static StockMovementResult Ok(decimal balance) =>
        new(StockMovementOutcome.Ok, null, balance);
}

/// <summary>
/// Storage port for the Sheet Store (Phase 8). Implementations must keep
/// the stock increments ATOMIC — the WPF moved both BuyStock and UseStock
/// into SQL-side updates after losing-update races (see
/// SheetStoreRepository's method remarks); the SaaS does the same inside
/// one transaction, plus the ledger row.
/// </summary>
public interface IStockStore
{
    // ── Glass sheets ────────────────────────────────────────────────
    Task<MasterData.PagedResult<GlassSheet>> SearchSheetsAsync(
        Guid tenantId, string? category, string? color, string? supplier,
        int? thicknessMm, string? search, string? sortKey,
        int page, int pageSize, CancellationToken ct);

    Task<GlassSheet?> FindSheetAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// <summary>False + message on the identity UNIQUE violation (the
    /// SaaS schema's uq_glass_sheets_identity).</summary>
    Task<(bool Ok, string? Error)> TryAddSheetAsync(
        GlassSheet sheet, CancellationToken ct);

    /// <summary>False + message on the identity UNIQUE violation when the
    /// edit moved the sheet onto another sheet's identity.</summary>
    Task<(bool Ok, string? Error)> TryUpdateSheetAsync(
        GlassSheet sheet, CancellationToken ct);

    Task<bool> DeleteSheetAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// <summary>Atomic purchase: Stock += quantity in SQL, optional price
    /// overwrite (only when &gt; 0), LastPurchaseDate today, plus the
    /// ledger row — one transaction.</summary>
    Task<StockMovementResult> BuySheetStockAsync(
        Guid tenantId, Guid sheetId, int quantity, decimal? newPurchasePrice,
        Guid actorId, string? notes, CancellationToken ct);

    /// <summary>Atomic guarded issue: Used += quantity only when
    /// Stock − Used ≥ quantity (the guard is IN the UPDATE's WHERE, no
    /// window for a race), plus the ledger row.</summary>
    Task<StockMovementResult> UseSheetStockAsync(
        Guid tenantId, Guid sheetId, int quantity,
        Guid actorId, string? notes, CancellationToken ct);

    /// <summary>Sheets at or below the low-stock threshold (balance ≤ 5,
    /// GlassSheetLowStock — the one declaration), ordered by balance.</summary>
    Task<IReadOnlyList<GlassSheet>> GetLowStockSheetsAsync(
        Guid tenantId, CancellationToken ct);

    Task<SheetFiltersDto> GetSheetFiltersAsync(Guid tenantId, CancellationToken ct);

    /// <summary>The movement history for one sheet (newest first).</summary>
    Task<MasterData.PagedResult<StockTransaction>> GetSheetHistoryAsync(
        Guid tenantId, Guid sheetId, int page, int pageSize, CancellationToken ct);

    // ── Inventory items (consumables) ───────────────────────────────
    Task<MasterData.PagedResult<InventoryItem>> SearchItemsAsync(
        Guid tenantId, string? category, string? supplier, string? search,
        int page, int pageSize, CancellationToken ct);

    Task<InventoryItem?> FindItemAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<(bool Ok, string? Error)> TryAddItemAsync(
        InventoryItem item, CancellationToken ct);

    Task<(bool Ok, string? Error)> TryUpdateItemAsync(
        InventoryItem item, CancellationToken ct);

    Task<bool> DeleteItemAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// <summary>Atomic receive: Stock += quantity, optional unit-price
    /// overwrite (only when &gt; 0), plus the ledger row.</summary>
    Task<StockMovementResult> ReceiveItemStockAsync(
        Guid tenantId, Guid itemId, decimal quantity, decimal? newUnitPrice,
        Guid actorId, string? notes, CancellationToken ct);

    /// <summary>Atomic guarded consume: Used += quantity only when
    /// Stock − Used ≥ quantity, plus the ledger row.</summary>
    Task<StockMovementResult> ConsumeItemStockAsync(
        Guid tenantId, Guid itemId, decimal quantity,
        Guid actorId, string? notes, CancellationToken ct);

    /// <summary>Items at or below their reorder level (a zero level means
    /// "not tracked" and never triggers), ordered by balance.</summary>
    Task<IReadOnlyList<InventoryItem>> GetLowStockItemsAsync(
        Guid tenantId, CancellationToken ct);

    Task<IReadOnlyList<string>> GetItemSuppliersAsync(
        Guid tenantId, CancellationToken ct);

    /// <summary>The movement history for one item (newest first).</summary>
    Task<MasterData.PagedResult<StockTransaction>> GetItemHistoryAsync(
        Guid tenantId, Guid itemId, int page, int pageSize, CancellationToken ct);
}
