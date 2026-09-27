using System.Globalization;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Application.Stock;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 8 service — WPF Services/SheetStore/SheetStoreRepository +
//  InventoryItemRepository, ported to the SaaS result/audit conventions.
//
//  Behaviour notes (mapping §2.1):
//  • The WPF's silent no-ops became typed 4xx errors: a REST write that
//    did nothing must not answer 200 (documented difference; the WPF's
//    own InventoryItemRepository throws for the same cases).
//  • The WPF's atomic SQL increments (BuyStock / UseStock) are kept —
//    the store does the guarded UPDATE + ledger row in one transaction.
//  • Every movement writes an immutable stock_transactions row (SaaS
//    0002 schema — the WPF had no ledger; documented improvement).
// ═══════════════════════════════════════════════════════════════════════

/// <summary>The Sheet Store: glass sheets + consumables.</summary>
public sealed class StockService(IStockStore store, IAuditWriter audit)
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    private static readonly string[] ItemCategories =
        { "spacer", "sealant", "desiccant", "gas", "hardware" };

    // ═════════════════════════ GLASS SHEETS ═════════════════════════

    public async Task<MasterData.PagedResult<GlassSheetDto>> SearchSheetsAsync(
        Guid tenantId, string? category, string? color, string? supplier,
        int? thicknessMm, string? search, string? sortKey,
        int? page, int? pageSize, CancellationToken ct)
    {
        var (p, size) = NormalizePaging(page, pageSize);
        var result = await store.SearchSheetsAsync(
            tenantId, category, color, supplier, thicknessMm, search,
            (sortKey ?? "").Trim().ToLowerInvariant(), p, size, ct);
        return new MasterData.PagedResult<GlassSheetDto>(
            result.Items.Select(ToDto).ToList(),
            result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<StockResult<GlassSheetDto>> GetSheetAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var sheet = await store.FindSheetAsync(tenantId, id, ct);
        return sheet is null
            ? StockResult<GlassSheetDto>.Fail(
                StockErrorCode.NotFound, $"Glass sheet {id} was not found.")
            : StockResult<GlassSheetDto>.Ok(ToDto(sheet));
    }

    public async Task<StockResult<GlassSheetDto>> CreateSheetAsync(
        Guid tenantId, Guid actorId, GlassSheetRequest request,
        string? ip, CancellationToken ct)
    {
        var error = ValidateSheet(request);
        if (error is not null)
        {
            return StockResult<GlassSheetDto>.Fail(
                StockErrorCode.ValidationFailed, error);
        }

        var now = DateTime.UtcNow;
        var sheet = new GlassSheet
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Category = request.Category!.Trim(),
            ThicknessMm = request.ThicknessMm!.Value,
            Color = string.IsNullOrWhiteSpace(request.Color) ? "Clear" : request.Color.Trim(),
            WidthMm = request.WidthMm!.Value,
            HeightMm = request.HeightMm!.Value,
            Supplier = (request.Supplier ?? "").Trim(),
            PurchasePricePerSqm = request.PurchasePricePerSqm ?? 0m,
            SellPricePerSqm = request.SellPricePerSqm ?? 0m,
            Stock = request.Stock ?? 0,
            Used = request.Used ?? 0,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId,
            // WPF AddAsync: an opening stock counts as the first purchase.
            LastPurchaseDate = (request.Stock ?? 0) > 0
                ? DateOnly.FromDateTime(now)
                : null
        };

        var (ok, addError) = await store.TryAddSheetAsync(sheet, ct);
        if (!ok)
        {
            return StockResult<GlassSheetDto>.Fail(
                StockErrorCode.DuplicateSheet, addError ?? "A sheet with the same identity already exists.");
        }

        await AuditAsync(tenantId, actorId, "stock.sheet_created",
            "glass_sheets", sheet.Id, null, ToDto(sheet), ip, ct);
        return StockResult<GlassSheetDto>.Ok(ToDto(sheet));
    }

    public async Task<StockResult<GlassSheetDto>> UpdateSheetAsync(
        Guid tenantId, Guid id, Guid actorId, GlassSheetRequest request,
        string? ip, CancellationToken ct)
    {
        var error = ValidateSheet(request);
        if (error is not null)
        {
            return StockResult<GlassSheetDto>.Fail(
                StockErrorCode.ValidationFailed, error);
        }

        var existing = await store.FindSheetAsync(tenantId, id, ct);
        if (existing is null)
        {
            return StockResult<GlassSheetDto>.Fail(
                StockErrorCode.NotFound, $"Glass sheet {id} was not found.");
        }

        var before = ToDto(existing);
        existing.Category = request.Category!.Trim();
        existing.ThicknessMm = request.ThicknessMm!.Value;
        existing.Color = string.IsNullOrWhiteSpace(request.Color) ? "Clear" : request.Color.Trim();
        existing.WidthMm = request.WidthMm!.Value;
        existing.HeightMm = request.HeightMm!.Value;
        existing.Supplier = (request.Supplier ?? "").Trim();
        existing.PurchasePricePerSqm = request.PurchasePricePerSqm ?? 0m;
        existing.SellPricePerSqm = request.SellPricePerSqm ?? 0m;
        existing.Stock = request.Stock ?? 0;
        existing.Used = request.Used ?? 0;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = actorId;

        var (ok, updateError) = await store.TryUpdateSheetAsync(existing, ct);
        if (!ok)
        {
            return StockResult<GlassSheetDto>.Fail(
                StockErrorCode.DuplicateSheet, updateError ?? "A sheet with the same identity already exists.");
        }

        await AuditAsync(tenantId, actorId, "stock.sheet_updated",
            "glass_sheets", existing.Id, before, ToDto(existing), ip, ct);
        return StockResult<GlassSheetDto>.Ok(ToDto(existing));
    }

    public async Task<StockResult<object>> DeleteSheetAsync(
        Guid tenantId, Guid id, Guid actorId, string? ip, CancellationToken ct)
    {
        var existing = await store.FindSheetAsync(tenantId, id, ct);
        if (existing is null)
        {
            return StockResult<object>.Fail(
                StockErrorCode.NotFound, $"Glass sheet {id} was not found.");
        }

        if (!await store.DeleteSheetAsync(tenantId, id, ct))
        {
            return StockResult<object>.Fail(
                StockErrorCode.NotFound, $"Glass sheet {id} was not found.");
        }

        await AuditAsync(tenantId, actorId, "stock.sheet_deleted",
            "glass_sheets", id, ToDto(existing), null, ip, ct);
        return StockResult<object>.Ok(new { });
    }

    /// <summary>
    /// POST /{id}/buy — the WPF BuyStockAsync: an atomic purchase with an
    /// optional purchase-price overwrite (only when &gt; 0) and the
    /// last-purchase stamp, plus the ledger row.
    /// </summary>
    public async Task<StockResult<GlassSheetDto>> BuySheetStockAsync(
        Guid tenantId, Guid id, Guid actorId, StockMovementRequest request,
        string? ip, CancellationToken ct)
    {
        if (request.Quantity <= 0)
        {
            return StockResult<GlassSheetDto>.Fail(StockErrorCode.ValidationFailed,
                "Quantity must be greater than zero.");
        }

        var movement = await store.BuySheetStockAsync(
            tenantId, id, (int)request.Quantity, request.NewUnitPrice,
            actorId, request.Notes, ct);
        if (movement.Outcome == StockMovementOutcome.NotFound)
        {
            return StockResult<GlassSheetDto>.Fail(
                StockErrorCode.NotFound, $"Glass sheet {id} was not found.");
        }

        var sheet = (await store.FindSheetAsync(tenantId, id, ct))!;
        await AuditAsync(tenantId, actorId, "stock.sheet_purchase",
            "glass_sheets", id, null, new
            {
                quantity = (int)request.Quantity,
                newPurchasePrice = request.NewUnitPrice,
                balanceAfter = movement.BalanceAfter
            }, ip, ct);
        return StockResult<GlassSheetDto>.Ok(ToDto(sheet));
    }

    /// <summary>
    /// POST /{id}/use — the WPF UseStockAsync: a guarded atomic issue.
    /// The balance guard is part of the UPDATE's WHERE clause (no race
    /// window); insufficient stock keeps the WPF's message verbatim.
    /// </summary>
    public async Task<StockResult<GlassSheetDto>> UseSheetStockAsync(
        Guid tenantId, Guid id, Guid actorId, StockMovementRequest request,
        string? ip, CancellationToken ct)
    {
        if (request.Quantity <= 0)
        {
            return StockResult<GlassSheetDto>.Fail(StockErrorCode.ValidationFailed,
                "Quantity must be greater than zero.");
        }

        var movement = await store.UseSheetStockAsync(
            tenantId, id, (int)request.Quantity, actorId, request.Notes, ct);
        if (movement.Outcome == StockMovementOutcome.NotFound)
        {
            return StockResult<GlassSheetDto>.Fail(
                StockErrorCode.NotFound, $"Glass sheet {id} was not found.");
        }
        if (movement.Outcome == StockMovementOutcome.InsufficientStock)
        {
            return StockResult<GlassSheetDto>.Fail(StockErrorCode.InsufficientStock,
                $"Not enough stock. Available: {movement.BalanceAfter}, " +
                $"Requested: {(int)request.Quantity}");
        }

        var sheet = (await store.FindSheetAsync(tenantId, id, ct))!;
        await AuditAsync(tenantId, actorId, "stock.sheet_issue",
            "glass_sheets", id, null, new
            {
                quantity = (int)request.Quantity,
                balanceAfter = movement.BalanceAfter
            }, ip, ct);
        return StockResult<GlassSheetDto>.Ok(ToDto(sheet));
    }

    public async Task<IReadOnlyList<GlassSheetDto>> GetLowStockSheetsAsync(
        Guid tenantId, CancellationToken ct) =>
        (await store.GetLowStockSheetsAsync(tenantId, ct)).Select(ToDto).ToList();

    public async Task<SheetFiltersDto> GetSheetFiltersAsync(
        Guid tenantId, CancellationToken ct) =>
        await store.GetSheetFiltersAsync(tenantId, ct);

    public async Task<MasterData.PagedResult<StockTransactionDto>> GetSheetHistoryAsync(
        Guid tenantId, Guid id, int? page, int? pageSize, CancellationToken ct)
    {
        var (p, size) = NormalizePaging(page, pageSize);
        var result = await store.GetSheetHistoryAsync(tenantId, id, p, size, ct);
        return new MasterData.PagedResult<StockTransactionDto>(
            result.Items.Select(ToDto).ToList(),
            result.Page, result.PageSize, result.TotalCount);
    }

    // ═══════════════════════ INVENTORY ITEMS ═══════════════════════

    public async Task<MasterData.PagedResult<InventoryItemDto>> SearchItemsAsync(
        Guid tenantId, string? category, string? supplier, string? search,
        int? page, int? pageSize, CancellationToken ct)
    {
        var (p, size) = NormalizePaging(page, pageSize);
        var result = await store.SearchItemsAsync(
            tenantId, category, supplier, search, p, size, ct);
        return new MasterData.PagedResult<InventoryItemDto>(
            result.Items.Select(ToDto).ToList(),
            result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<StockResult<InventoryItemDto>> GetItemAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var item = await store.FindItemAsync(tenantId, id, ct);
        return item is null
            ? StockResult<InventoryItemDto>.Fail(
                StockErrorCode.NotFound, $"Inventory item {id} was not found.")
            : StockResult<InventoryItemDto>.Ok(ToDto(item));
    }

    public async Task<StockResult<InventoryItemDto>> CreateItemAsync(
        Guid tenantId, Guid actorId, InventoryItemRequest request,
        string? ip, CancellationToken ct)
    {
        var error = ValidateItem(request);
        if (error is not null)
        {
            return StockResult<InventoryItemDto>.Fail(
                StockErrorCode.ValidationFailed, error);
        }

        var now = DateTime.UtcNow;
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Category = request.Category!.Trim().ToLowerInvariant(),
            Name = request.Name!.Trim(),
            Supplier = (request.Supplier ?? "").Trim(),
            Stock = request.Stock ?? 0m,
            Used = request.Used ?? 0m,
            ReorderLevel = request.ReorderLevel ?? 0m,
            PurchasePricePerUnit = request.PurchasePricePerUnit ?? 0m,
            SellPricePerUnit = request.SellPricePerUnit ?? 0m,
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? "LM" : request.Unit!.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId
        };

        var (ok, addError) = await store.TryAddItemAsync(item, ct);
        if (!ok)
        {
            return StockResult<InventoryItemDto>.Fail(
                StockErrorCode.ValidationFailed, addError ?? "The item could not be saved.");
        }

        await AuditAsync(tenantId, actorId, "stock.item_created",
            "inventory_items", item.Id, null, ToDto(item), ip, ct);
        return StockResult<InventoryItemDto>.Ok(ToDto(item));
    }

    public async Task<StockResult<InventoryItemDto>> UpdateItemAsync(
        Guid tenantId, Guid id, Guid actorId, InventoryItemRequest request,
        string? ip, CancellationToken ct)
    {
        var error = ValidateItem(request);
        if (error is not null)
        {
            return StockResult<InventoryItemDto>.Fail(
                StockErrorCode.ValidationFailed, error);
        }

        var existing = await store.FindItemAsync(tenantId, id, ct);
        if (existing is null)
        {
            return StockResult<InventoryItemDto>.Fail(
                StockErrorCode.NotFound, $"Inventory item {id} was not found.");
        }

        var before = ToDto(existing);
        existing.Category = request.Category!.Trim().ToLowerInvariant();
        existing.Name = request.Name!.Trim();
        existing.Supplier = (request.Supplier ?? "").Trim();
        existing.Stock = request.Stock ?? 0m;
        existing.Used = request.Used ?? 0m;
        existing.ReorderLevel = request.ReorderLevel ?? 0m;
        existing.PurchasePricePerUnit = request.PurchasePricePerUnit ?? 0m;
        existing.SellPricePerUnit = request.SellPricePerUnit ?? 0m;
        existing.Unit = string.IsNullOrWhiteSpace(request.Unit) ? "LM" : request.Unit!.Trim();
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = actorId;

        var (ok, updateError) = await store.TryUpdateItemAsync(existing, ct);
        if (!ok)
        {
            return StockResult<InventoryItemDto>.Fail(
                StockErrorCode.ValidationFailed, updateError ?? "The item could not be saved.");
        }

        await AuditAsync(tenantId, actorId, "stock.item_updated",
            "inventory_items", existing.Id, before, ToDto(existing), ip, ct);
        return StockResult<InventoryItemDto>.Ok(ToDto(existing));
    }

    public async Task<StockResult<object>> DeleteItemAsync(
        Guid tenantId, Guid id, Guid actorId, string? ip, CancellationToken ct)
    {
        var existing = await store.FindItemAsync(tenantId, id, ct);
        if (existing is null)
        {
            return StockResult<object>.Fail(
                StockErrorCode.NotFound, $"Inventory item {id} was not found.");
        }

        if (!await store.DeleteItemAsync(tenantId, id, ct))
        {
            return StockResult<object>.Fail(
                StockErrorCode.NotFound, $"Inventory item {id} was not found.");
        }

        await AuditAsync(tenantId, actorId, "stock.item_deleted",
            "inventory_items", id, ToDto(existing), null, ip, ct);
        return StockResult<object>.Ok(new { });
    }

    /// <summary>
    /// POST /{id}/receive — the WPF InventoryItemRepository.ReceiveAsync
    /// (quantity ≤ 0 refused with its exact message; price overwrite only
    /// when &gt; 0) plus the ledger row.
    /// </summary>
    public async Task<StockResult<InventoryItemDto>> ReceiveItemStockAsync(
        Guid tenantId, Guid id, Guid actorId, StockMovementRequest request,
        string? ip, CancellationToken ct)
    {
        if (request.Quantity <= 0)
        {
            return StockResult<InventoryItemDto>.Fail(StockErrorCode.ValidationFailed,
                "Received quantity must be greater than zero.");
        }

        var movement = await store.ReceiveItemStockAsync(
            tenantId, id, request.Quantity, request.NewUnitPrice,
            actorId, request.Notes, ct);
        if (movement.Outcome == StockMovementOutcome.NotFound)
        {
            return StockResult<InventoryItemDto>.Fail(
                StockErrorCode.NotFound, $"Inventory item {id} was not found.");
        }

        var item = (await store.FindItemAsync(tenantId, id, ct))!;
        await AuditAsync(tenantId, actorId, "stock.item_receive",
            "inventory_items", id, null, new
            {
                quantity = request.Quantity,
                newUnitPrice = request.NewUnitPrice,
                balanceAfter = movement.QuantityBalance
            }, ip, ct);
        return StockResult<InventoryItemDto>.Ok(ToDto(item));
    }

    /// <summary>
    /// POST /{id}/consume — the WPF ConsumeAsync (guarded balance with its
    /// exact message, unit-suffixed) plus the ledger row.
    /// </summary>
    public async Task<StockResult<InventoryItemDto>> ConsumeItemStockAsync(
        Guid tenantId, Guid id, Guid actorId, StockMovementRequest request,
        string? ip, CancellationToken ct)
    {
        if (request.Quantity <= 0)
        {
            return StockResult<InventoryItemDto>.Fail(StockErrorCode.ValidationFailed,
                "Consumed quantity must be greater than zero.");
        }

        var movement = await store.ConsumeItemStockAsync(
            tenantId, id, request.Quantity, actorId, request.Notes, ct);
        if (movement.Outcome == StockMovementOutcome.NotFound)
        {
            return StockResult<InventoryItemDto>.Fail(
                StockErrorCode.NotFound, $"Inventory item {id} was not found.");
        }
        if (movement.Outcome == StockMovementOutcome.InsufficientStock)
        {
            var item = await store.FindItemAsync(tenantId, id, ct);
            var unit = item?.Unit ?? "";
            var balance = movement.QuantityBalance ?? 0m;
            return StockResult<InventoryItemDto>.Fail(StockErrorCode.InsufficientStock,
                $"Not enough stock. Available: {balance.ToString("N2", CultureInfo.InvariantCulture)} {unit}, " +
                $"requested: {request.Quantity.ToString("N2", CultureInfo.InvariantCulture)} {unit}.");
        }

        var after = (await store.FindItemAsync(tenantId, id, ct))!;
        await AuditAsync(tenantId, actorId, "stock.item_consume",
            "inventory_items", id, null, new
            {
                quantity = request.Quantity,
                balanceAfter = movement.QuantityBalance
            }, ip, ct);
        return StockResult<InventoryItemDto>.Ok(ToDto(after));
    }

    public async Task<IReadOnlyList<InventoryItemDto>> GetLowStockItemsAsync(
        Guid tenantId, CancellationToken ct) =>
        (await store.GetLowStockItemsAsync(tenantId, ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<string>> GetItemSuppliersAsync(
        Guid tenantId, CancellationToken ct) =>
        await store.GetItemSuppliersAsync(tenantId, ct);

    public async Task<MasterData.PagedResult<StockTransactionDto>> GetItemHistoryAsync(
        Guid tenantId, Guid id, int? page, int? pageSize, CancellationToken ct)
    {
        var (p, size) = NormalizePaging(page, pageSize);
        var result = await store.GetItemHistoryAsync(tenantId, id, p, size, ct);
        return new MasterData.PagedResult<StockTransactionDto>(
            result.Items.Select(ToDto).ToList(),
            result.Page, result.PageSize, result.TotalCount);
    }

    // ═════════════════════════ HELPERS ═════════════════════════════

    private static (int Page, int Size) NormalizePaging(int? page, int? pageSize)
    {
        var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        return (Math.Max(page ?? 1, 1), size);
    }

    private static string? ValidateSheet(GlassSheetRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Category))
            return "Category is required.";
        if (r.ThicknessMm is not > 0)
            return "Thickness (mm) must be greater than zero.";
        if (r.WidthMm is not > 0)
            return "Width (mm) must be greater than zero.";
        if (r.HeightMm is not > 0)
            return "Height (mm) must be greater than zero.";
        if (r.PurchasePricePerSqm is < 0)
            return "Purchase price per SQM cannot be negative.";
        if (r.SellPricePerSqm is < 0)
            return "Sell price per SQM cannot be negative.";
        if (r.Stock is < 0)
            return "Stock cannot be negative.";
        if (r.Used is < 0)
            return "Used cannot be negative.";
        return null;
    }

    private static string? ValidateItem(InventoryItemRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name))
            return "Name is required.";
        var category = (r.Category ?? "").Trim().ToLowerInvariant();
        if (!ItemCategories.Contains(category))
            return $"Category must be one of: {string.Join(", ", ItemCategories)}.";
        if (r.Stock is < 0)
            return "Stock cannot be negative.";
        if (r.Used is < 0)
            return "Used cannot be negative.";
        if (r.ReorderLevel is < 0)
            return "Reorder level cannot be negative.";
        if (r.PurchasePricePerUnit is < 0)
            return "Purchase price per unit cannot be negative.";
        if (r.SellPricePerUnit is < 0)
            return "Sell price per unit cannot be negative.";
        return null;
    }

    /// <summary>WPF SqmCalculator.CalculateSheetArea — mm → SQM at 4dp
    /// (3210 × 2250 = 7.2225).</summary>
    private static decimal SqmPerSheet(GlassSheet s) =>
        Math.Round(s.WidthMm * (decimal)s.HeightMm / 1_000_000m, 4);

    internal static GlassSheetDto ToDto(GlassSheet s)
    {
        var sqm = SqmPerSheet(s);
        var balance = s.Stock - s.Used;
        return new GlassSheetDto(
            s.Id, s.Category, s.ThicknessMm, s.Color, s.WidthMm, s.HeightMm,
            s.Supplier, s.PurchasePricePerSqm, s.SellPricePerSqm,
            s.Stock, s.Used, balance, sqm,
            sqm * s.Stock, sqm * s.Used, sqm * balance,
            sqm * s.Stock * s.PurchasePricePerSqm,
            sqm * s.Stock * s.SellPricePerSqm,
            sqm * balance * s.SellPricePerSqm,
            sqm * s.Stock * (s.SellPricePerSqm - s.PurchasePricePerSqm),
            sqm * s.Used * s.PurchasePricePerSqm,
            sqm * s.Used * s.SellPricePerSqm,
            sqm * s.Used * (s.SellPricePerSqm - s.PurchasePricePerSqm),
            sqm * balance * s.PurchasePricePerSqm,
            s.SellPricePerSqm - s.PurchasePricePerSqm,
            s.LastPurchaseDate, s.CreatedAt, s.UpdatedAt);
    }

    internal static InventoryItemDto ToDto(InventoryItem i)
    {
        var balance = i.Stock - i.Used;
        return new InventoryItemDto(
            i.Id, i.Category, i.Name, i.Supplier, i.Stock, i.Used, balance,
            i.ReorderLevel,
            i.ReorderLevel > 0 && balance <= i.ReorderLevel,
            balance <= 0,
            i.PurchasePricePerUnit, i.SellPricePerUnit, i.Unit,
            i.Stock * i.PurchasePricePerUnit,
            balance * i.PurchasePricePerUnit,
            i.Used * i.PurchasePricePerUnit,
            i.CreatedAt, i.UpdatedAt);
    }

    private static StockTransactionDto ToDto(StockTransaction t) =>
        new(t.Id, t.ItemType, t.GlassSheetId, t.InventoryItemId,
            t.TransactionType, t.QuantityDelta, t.BalanceAfter, t.UnitCost,
            t.ReferenceType, t.ReferenceId, t.Notes, t.PerformedBy, t.CreatedAt);

    private async Task AuditAsync(
        Guid tenantId, Guid? actorUserId, string action, string entityType,
        Guid entityId, object? before, object? after, string? ip, CancellationToken ct)
        => await audit.WriteAsync(tenantId, actorUserId, action, entityType,
            entityId, ip, null,
            before is null ? null : System.Text.Json.JsonSerializer.Serialize(before),
            after is null ? null : System.Text.Json.JsonSerializer.Serialize(after),
            ct).ConfigureAwait(false);
}
