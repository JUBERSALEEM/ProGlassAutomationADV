namespace ProGlass.Saas.Application.Stock;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 8 — Sheet Store (WPF Services/SheetStore/*).
//
//  glass sheets: CRUD + filtered/sorted/paged search + atomic buy/use
//  (the WPF's SQL-side increments), low-stock threshold 5
//  (Models/SheetStore/GlassSheetLowStock.cs — the ONE declaration).
//  inventory items: CRUD + receive/consume with the per-item reorder
//  level. Every movement writes an immutable stock_transactions ledger
//  row (the SaaS 0002 schema — the WPF SQLite had no ledger; documented
//  improvement).
// ═══════════════════════════════════════════════════════════════════════

/// <summary>Stock failure codes (API §0 error contract).</summary>
public enum StockErrorCode
{
    None = 0,
    ValidationFailed,
    NotFound,
    InsufficientStock,
    DuplicateSheet,
}

/// <summary>Typed result: a value or a code + operator-readable message.</summary>
public sealed record StockResult<T>(
    bool Success, T? Value, StockErrorCode ErrorCode, string Message)
{
    public static StockResult<T> Ok(T value) =>
        new(true, value, StockErrorCode.None, "");

    public static StockResult<T> Fail(StockErrorCode code, string message) =>
        new(false, default, code, message);
}

/// <summary>
/// POST/PUT body for a glass sheet (WPF GlassSheet editable fields). The
/// computed values (SqmPerSheet, Balance, value totals) are derived —
/// never accepted from the client.
/// </summary>
public sealed record GlassSheetRequest(
    string? Category,
    int? ThicknessMm,
    string? Color,
    int? WidthMm,
    int? HeightMm,
    string? Supplier,
    decimal? PurchasePricePerSqm,
    decimal? SellPricePerSqm,
    int? Stock,
    int? Used);

/// <summary>
/// A glass sheet with the WPF GlassSheet computed read model (SqmPerSheet,
/// Balance, the SQM/value totals, the consumed-value block).
/// </summary>
public sealed record GlassSheetDto(
    Guid Id,
    string Category,
    int ThicknessMm,
    string Color,
    int WidthMm,
    int HeightMm,
    string Supplier,
    decimal PurchasePricePerSqm,
    decimal SellPricePerSqm,
    int Stock,
    int Used,
    int Balance,
    decimal SqmPerSheet,
    decimal TotalSqmInStock,
    decimal TotalSqmUsed,
    decimal TotalSqmBalance,
    decimal TotalPurchaseValue,
    decimal TotalSellValue,
    decimal BalanceValue,
    decimal ProfitValue,
    decimal UsedCostValue,
    decimal UsedSellValue,
    decimal UsedProfitValue,
    decimal BalanceCostValue,
    decimal ProfitPerSqm,
    DateOnly? LastPurchaseDate,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>e.g. "3210 × 2250".</summary>
    public string SizeDisplay => $"{WidthMm} × {HeightMm}";

    /// <summary>e.g. "6mm".</summary>
    public string ThicknessDisplay => $"{ThicknessMm}mm";
}

/// <summary>POST/PUT body for a consumable inventory item.</summary>
public sealed record InventoryItemRequest(
    string? Category,
    string? Name,
    string? Supplier,
    decimal? Stock,
    decimal? Used,
    decimal? ReorderLevel,
    decimal? PurchasePricePerUnit,
    decimal? SellPricePerUnit,
    string? Unit);

/// <summary>
/// A consumable item with the WPF InventoryItem computed read model
/// (Balance, IsLowStock — a zero reorder level means "not tracked" and
/// never triggers — and the value totals).
/// </summary>
public sealed record InventoryItemDto(
    Guid Id,
    string Category,
    string Name,
    string Supplier,
    decimal Stock,
    decimal Used,
    decimal Balance,
    decimal ReorderLevel,
    bool IsLowStock,
    bool IsOutOfStock,
    decimal PurchasePricePerUnit,
    decimal SellPricePerUnit,
    string Unit,
    decimal TotalPurchaseValue,
    decimal BalanceCostValue,
    decimal UsedCostValue,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Body of a stock movement (buy / use / receive / consume).</summary>
public sealed record StockMovementRequest(
    decimal Quantity,
    decimal? NewUnitPrice = null,
    string? Notes = null);

/// <summary>The filter dropdown data for the sheet grid.</summary>
public sealed record SheetFiltersDto(
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Colors,
    IReadOnlyList<string> Suppliers,
    IReadOnlyList<int> ThicknessesMm);

/// <summary>One immutable ledger row (0002 schema).</summary>
public sealed record StockTransactionDto(
    Guid Id,
    string ItemType,
    Guid? GlassSheetId,
    Guid? InventoryItemId,
    string TransactionType,
    decimal QuantityDelta,
    decimal BalanceAfter,
    decimal UnitCost,
    string ReferenceType,
    Guid? ReferenceId,
    string Notes,
    Guid? PerformedBy,
    DateTime CreatedAt);
