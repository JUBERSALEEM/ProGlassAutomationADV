using Microsoft.AspNetCore.Mvc;
using ProGlass.Saas.Application.Stock;
using ProGlass.Saas.Domain.Authorization;

namespace ProGlass.Saas.Api.Controllers;

/// <summary>
/// /api/v1/stock — the Sheet Store (WPF Services/SheetStore): glass
/// sheets + consumable inventory items with atomic buy/use movements and
/// the immutable movement ledger. Permissions map the WPF's
/// OperationGuard calls: create/edit/delete → SheetStore.Create/.Edit/
/// .Delete; movements are edits of stock (SheetStore.Edit — the WPF
/// gated them with the read-only gate only).
/// </summary>
[Route("api/v1/stock")]
public sealed class StockController(StockService stock)
    : TenantControllerBase
{
    // ── Glass sheets ────────────────────────────────────────────────

    [HttpGet("sheets")]
    public async Task<IActionResult> ListSheets(
        [FromQuery] string? category, [FromQuery] string? color,
        [FromQuery] string? supplier, [FromQuery] int? thicknessMm,
        [FromQuery] string? search, [FromQuery] string? sortKey,
        [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken ct)
        => Ok(await stock.SearchSheetsAsync(
            RequiredTenantId, category, color, supplier, thicknessMm,
            search, sortKey, page, pageSize, ct));

    [HttpGet("sheets/low-stock")]
    public async Task<IActionResult> LowStockSheets(CancellationToken ct)
        => Ok(await stock.GetLowStockSheetsAsync(RequiredTenantId, ct));

    [HttpGet("sheets/filters")]
    public async Task<IActionResult> SheetFilters(CancellationToken ct)
        => Ok(await stock.GetSheetFiltersAsync(RequiredTenantId, ct));

    [HttpGet("sheets/{id:guid}")]
    public async Task<IActionResult> GetSheet(Guid id, CancellationToken ct)
    {
        var result = await stock.GetSheetAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpPost("sheets")]
    [HasPermission(AppPermissions.SheetStoreCreate)]
    public async Task<IActionResult> CreateSheet(
        [FromBody] GlassSheetRequest request, CancellationToken ct)
    {
        var result = await stock.CreateSheetAsync(
            RequiredTenantId, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpPut("sheets/{id:guid}")]
    [HasPermission(AppPermissions.SheetStoreEdit)]
    public async Task<IActionResult> UpdateSheet(
        Guid id, [FromBody] GlassSheetRequest request, CancellationToken ct)
    {
        var result = await stock.UpdateSheetAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpDelete("sheets/{id:guid}")]
    [HasPermission(AppPermissions.SheetStoreDelete)]
    public async Task<IActionResult> DeleteSheet(Guid id, CancellationToken ct)
    {
        var result = await stock.DeleteSheetAsync(
            RequiredTenantId, id, RequiredUserId, ClientIp, ct);
        return result.Success ? NoContent() : Fail(result);
    }

    [HttpPost("sheets/{id:guid}/buy")]
    [HasPermission(AppPermissions.SheetStoreEdit)]
    public async Task<IActionResult> BuySheet(
        Guid id, [FromBody] StockMovementRequest request, CancellationToken ct)
    {
        var result = await stock.BuySheetStockAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpPost("sheets/{id:guid}/use")]
    [HasPermission(AppPermissions.SheetStoreEdit)]
    public async Task<IActionResult> UseSheet(
        Guid id, [FromBody] StockMovementRequest request, CancellationToken ct)
    {
        var result = await stock.UseSheetStockAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("sheets/{id:guid}/history")]
    public async Task<IActionResult> SheetHistory(
        Guid id, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken ct)
        => Ok(await stock.GetSheetHistoryAsync(
            RequiredTenantId, id, page, pageSize, ct));

    // ── Inventory items ─────────────────────────────────────────────

    [HttpGet("items")]
    public async Task<IActionResult> ListItems(
        [FromQuery] string? category, [FromQuery] string? supplier,
        [FromQuery] string? search, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken ct)
        => Ok(await stock.SearchItemsAsync(
            RequiredTenantId, category, supplier, search, page, pageSize, ct));

    [HttpGet("items/low-stock")]
    public async Task<IActionResult> LowStockItems(CancellationToken ct)
        => Ok(await stock.GetLowStockItemsAsync(RequiredTenantId, ct));

    [HttpGet("items/suppliers")]
    public async Task<IActionResult> ItemSuppliers(CancellationToken ct)
        => Ok(await stock.GetItemSuppliersAsync(RequiredTenantId, ct));

    [HttpGet("items/{id:guid}")]
    public async Task<IActionResult> GetItem(Guid id, CancellationToken ct)
    {
        var result = await stock.GetItemAsync(RequiredTenantId, id, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpPost("items")]
    [HasPermission(AppPermissions.SheetStoreCreate)]
    public async Task<IActionResult> CreateItem(
        [FromBody] InventoryItemRequest request, CancellationToken ct)
    {
        var result = await stock.CreateItemAsync(
            RequiredTenantId, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpPut("items/{id:guid}")]
    [HasPermission(AppPermissions.SheetStoreEdit)]
    public async Task<IActionResult> UpdateItem(
        Guid id, [FromBody] InventoryItemRequest request, CancellationToken ct)
    {
        var result = await stock.UpdateItemAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpDelete("items/{id:guid}")]
    [HasPermission(AppPermissions.SheetStoreDelete)]
    public async Task<IActionResult> DeleteItem(Guid id, CancellationToken ct)
    {
        var result = await stock.DeleteItemAsync(
            RequiredTenantId, id, RequiredUserId, ClientIp, ct);
        return result.Success ? NoContent() : Fail(result);
    }

    [HttpPost("items/{id:guid}/receive")]
    [HasPermission(AppPermissions.SheetStoreEdit)]
    public async Task<IActionResult> ReceiveItem(
        Guid id, [FromBody] StockMovementRequest request, CancellationToken ct)
    {
        var result = await stock.ReceiveItemStockAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpPost("items/{id:guid}/consume")]
    [HasPermission(AppPermissions.SheetStoreEdit)]
    public async Task<IActionResult> ConsumeItem(
        Guid id, [FromBody] StockMovementRequest request, CancellationToken ct)
    {
        var result = await stock.ConsumeItemStockAsync(
            RequiredTenantId, id, RequiredUserId, request, ClientIp, ct);
        return result.Success ? Ok(result.Value) : Fail(result);
    }

    [HttpGet("items/{id:guid}/history")]
    public async Task<IActionResult> ItemHistory(
        Guid id, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken ct)
        => Ok(await stock.GetItemHistoryAsync(
            RequiredTenantId, id, page, pageSize, ct));
}
