using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.Stock;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// The Sheet Store (Phase 8 — WPF SheetStoreRepository +
/// InventoryItemRepository) against live PostgreSQL: CRUD, filtered
/// search, the ATOMIC buy/use movements with the immutable ledger, the
/// low-stock rules and tenant isolation.
/// </summary>
[Collection("database")]
public sealed class StockFlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly StockService _sut;

    public StockFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var factory = new SaasDbContextFactory(fixture.AppDb);
        var audit = new AuditWriter(factory, NullLogger<AuditWriter>.Instance);
        _sut = new StockService(new StockStore(factory), audit);
    }

    private static GlassSheetRequest Sheet(
        string category = "Clear Glass", int thickness = 6,
        string color = "Clear", int width = 3210, int height = 2250,
        string supplier = "Gulf Glass", decimal purchase = 46m,
        decimal sell = 1044m, int stock = 10) =>
        new(category, thickness, color, width, height, supplier,
            purchase, sell, stock, 0);

    [Fact]
    public async Task Sheet_crud_and_identity_duplicate()
    {
        var tenant = _fixture.CreateTenant("StkCrud");

        var created = await _sut.CreateSheetAsync(
            tenant, Guid.NewGuid(), Sheet(), null, CancellationToken.None);
        Assert.True(created.Success, created.Message);
        Assert.Equal(10, created.Value!.Stock);
        Assert.Equal(3210 * 2250 / 1_000_000m, created.Value.SqmPerSheet);
        Assert.Equal(10, created.Value.Balance);
        Assert.NotNull(created.Value.LastPurchaseDate); // opening stock

        // Same identity twice → the schema's UNIQUE constraint surfaces
        // as a typed conflict (the SaaS schema is stricter than the WPF
        // SQLite, which had no such constraint — documented).
        var dup = await _sut.CreateSheetAsync(
            tenant, Guid.NewGuid(), Sheet(), null, CancellationToken.None);
        Assert.False(dup.Success);
        Assert.Equal(StockErrorCode.DuplicateSheet, dup.ErrorCode);

        var updated = await _sut.UpdateSheetAsync(
            tenant, created.Value.Id, Guid.NewGuid(),
            Sheet(sell: 1100m, stock: 12), null, CancellationToken.None);
        Assert.True(updated.Success, updated.Message);
        Assert.Equal(1100m, updated.Value!.SellPricePerSqm);
        Assert.Equal(12, updated.Value.Stock);
        // A zero opening stock does not stamp a purchase date on edit.
        Assert.Equal(created.Value.LastPurchaseDate, updated.Value.LastPurchaseDate);

        var deleted = await _sut.DeleteSheetAsync(
            tenant, created.Value.Id, Guid.NewGuid(), null, CancellationToken.None);
        Assert.True(deleted.Success);
        Assert.Equal(StockErrorCode.NotFound,
            (await _sut.GetSheetAsync(tenant, created.Value.Id, CancellationToken.None))
                .ErrorCode);
    }

    [Fact]
    public async Task Sheet_search_filters_sort_and_page()
    {
        var tenant = _fixture.CreateTenant("StkSearch");
        await CreateAsync(tenant, Sheet(category: "Clear Glass", color: "Clear", thickness: 6, stock: 3));
        await CreateAsync(tenant, Sheet(category: "Tinted Glass", color: "Bronze", thickness: 8, supplier: "Emirates"));
        await CreateAsync(tenant, Sheet(category: "Tinted Glass", color: "Grey", thickness: 8, stock: 30));

        var tinted = await _sut.SearchSheetsAsync(
            tenant, "Tinted Glass", null, null, null, null, null, null, null,
            CancellationToken.None);
        Assert.Equal(2, tinted.TotalCount);

        var thick = await _sut.SearchSheetsAsync(
            tenant, null, null, null, 8, null, null, null, null, CancellationToken.None);
        Assert.Equal(2, thick.TotalCount);
        Assert.All(thick.Items, s => Assert.Equal(8, s.ThicknessMm));

        var text = await _sut.SearchSheetsAsync(
            tenant, null, null, null, null, "bronze", null, null, null,
            CancellationToken.None);
        Assert.Single(text.Items);

        // stockdesc → the 30-sheet Grey row first; page 1 of size 2.
        var sorted = await _sut.SearchSheetsAsync(
            tenant, null, null, null, null, null, "stockdesc", 1, 2,
            CancellationToken.None);
        Assert.Equal(30, sorted.Items[0].Stock);
        Assert.Equal(2, sorted.Items.Count);
        Assert.Equal(3, sorted.TotalCount);

        var filters = await _sut.GetSheetFiltersAsync(tenant, CancellationToken.None);
        Assert.Equal(new[] { "Clear Glass", "Tinted Glass" }, filters.Categories);
        Assert.Equal(new[] { 6, 8 }, filters.ThicknessesMm);
        Assert.Equal(new[] { "Emirates", "Gulf Glass" }, filters.Suppliers);
    }

    [Fact]
    public async Task Buy_increments_price_and_writes_the_ledger()
    {
        var tenant = _fixture.CreateTenant("StkBuy");
        var sheet = await CreateAsync(tenant, Sheet(stock: 5, purchase: 40m));

        var bought = await _sut.BuySheetStockAsync(
            tenant, sheet.Id, Guid.NewGuid(),
            new StockMovementRequest(10, 46m, "restock"), null, CancellationToken.None);
        Assert.True(bought.Success, bought.Message);
        Assert.Equal(15, bought.Value!.Stock);
        Assert.Equal(15, bought.Value.Balance);
        Assert.Equal(46m, bought.Value.PurchasePricePerSqm);  // overwritten
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), bought.Value.LastPurchaseDate);

        var history = await _sut.GetSheetHistoryAsync(
            tenant, sheet.Id, null, null, CancellationToken.None);
        var row = Assert.Single(history.Items);
        Assert.Equal("purchase", row.TransactionType);
        Assert.Equal(10, row.QuantityDelta);
        Assert.Equal(15, row.BalanceAfter);
        Assert.Equal(46m, row.UnitCost);
        Assert.Equal("restock", row.Notes);

        // A non-positive price never overwrites the purchase price.
        var again = await _sut.BuySheetStockAsync(
            tenant, sheet.Id, Guid.NewGuid(),
            new StockMovementRequest(5, 0m, null), null, CancellationToken.None);
        Assert.True(again.Success);
        Assert.Equal(20, again.Value!.Stock);
        Assert.Equal(46m, again.Value.PurchasePricePerSqm);

        // Both purchases landed in the ledger.
        var history2 = await _sut.GetSheetHistoryAsync(
            tenant, sheet.Id, null, null, CancellationToken.None);
        Assert.Equal(2, history2.TotalCount);
        Assert.Equal(20, history2.Items[0].BalanceAfter);   // newest first
    }

    [Fact]
    public async Task Use_is_guarded_and_reports_the_wpf_message()
    {
        var tenant = _fixture.CreateTenant("StkUse");
        var sheet = await CreateAsync(tenant, Sheet(stock: 10));

        var used = await _sut.UseSheetStockAsync(
            tenant, sheet.Id, Guid.NewGuid(),
            new StockMovementRequest(4), null, CancellationToken.None);
        Assert.True(used.Success, used.Message);
        Assert.Equal(4, used.Value!.Used);
        Assert.Equal(6, used.Value.Balance);

        var shortUse = await _sut.UseSheetStockAsync(
            tenant, sheet.Id, Guid.NewGuid(),
            new StockMovementRequest(7), null, CancellationToken.None);
        Assert.False(shortUse.Success);
        Assert.Equal(StockErrorCode.InsufficientStock, shortUse.ErrorCode);
        Assert.Equal("Not enough stock. Available: 6, Requested: 7", shortUse.Message);

        // The failed issue must not have moved anything.
        var after = await _sut.GetSheetAsync(tenant, sheet.Id, CancellationToken.None);
        Assert.Equal(4, after.Value!.Used);

        var history = await _sut.GetSheetHistoryAsync(
            tenant, sheet.Id, null, null, CancellationToken.None);
        var row = Assert.Single(history.Items);
        Assert.Equal("issue", row.TransactionType);
        Assert.Equal(-4, row.QuantityDelta);
        Assert.Equal(6, row.BalanceAfter);

        // The WPF silently ignored quantity ≤ 0; an API must not answer
        // success for a write that did nothing (documented difference).
        var zero = await _sut.UseSheetStockAsync(
            tenant, sheet.Id, Guid.NewGuid(),
            new StockMovementRequest(0), null, CancellationToken.None);
        Assert.Equal(StockErrorCode.ValidationFailed, zero.ErrorCode);
    }

    [Fact]
    public async Task Low_stock_sheets_are_balances_at_or_below_five()
    {
        var tenant = _fixture.CreateTenant("StkLow");
        await CreateAsync(tenant, Sheet(color: "A", stock: 6));   // 6 > 5 → fine
        await CreateAsync(tenant, Sheet(color: "B", stock: 5));   // 5 ≤ 5 → low
        var usedUp = await CreateAsync(tenant, Sheet(color: "C", stock: 8));
        await _sut.UseSheetStockAsync(
            tenant, usedUp.Id, Guid.NewGuid(), new StockMovementRequest(5),
            null, CancellationToken.None);                        // balance 3 → low

        var low = await _sut.GetLowStockSheetsAsync(tenant, CancellationToken.None);

        Assert.Equal(2, low.Count);
        Assert.Equal(3, low[0].Balance);   // ordered by balance
        Assert.Equal(5, low[1].Balance);
    }

    [Fact]
    public async Task Inventory_items_receive_consume_and_reorder_level()
    {
        var tenant = _fixture.CreateTenant("StkInv");

        var created = await _sut.CreateItemAsync(
            tenant, Guid.NewGuid(),
            new InventoryItemRequest("spacer", "Warm-edge 16mm black", "AluPro",
                100m, 0m, 20m, 8.5m, 12m, "LM"),
            null, CancellationToken.None);
        Assert.True(created.Success, created.Message);
        Assert.Equal(100m, created.Value!.Stock);
        Assert.False(created.Value.IsLowStock);

        var badCategory = await _sut.CreateItemAsync(
            tenant, Guid.NewGuid(),
            new InventoryItemRequest("glass", "Not a consumable", null,
                null, null, null, null, null, null),
            null, CancellationToken.None);
        Assert.Equal(StockErrorCode.ValidationFailed, badCategory.ErrorCode);
        Assert.Contains("spacer", badCategory.Message);

        var received = await _sut.ReceiveItemStockAsync(
            tenant, created.Value.Id, Guid.NewGuid(),
            new StockMovementRequest(45.55m, 9m, "delivery"), null, CancellationToken.None);
        Assert.True(received.Success, received.Message);
        Assert.Equal(145.55m, received.Value!.Stock);
        Assert.Equal(9m, received.Value.PurchasePricePerUnit);

        // The WPF's exact refusal for a zero receipt.
        var zeroReceipt = await _sut.ReceiveItemStockAsync(
            tenant, created.Value.Id, Guid.NewGuid(),
            new StockMovementRequest(0), null, CancellationToken.None);
        Assert.Equal("Received quantity must be greater than zero.", zeroReceipt.Message);

        var consumed = await _sut.ConsumeItemStockAsync(
            tenant, created.Value.Id, Guid.NewGuid(),
            new StockMovementRequest(130m), null, CancellationToken.None);
        Assert.True(consumed.Success, consumed.Message);
        Assert.Equal(15.55m, consumed.Value!.Balance);
        Assert.True(consumed.Value.IsLowStock);   // ≤ reorder 20

        var shortConsume = await _sut.ConsumeItemStockAsync(
            tenant, created.Value.Id, Guid.NewGuid(),
            new StockMovementRequest(50m), null, CancellationToken.None);
        Assert.Equal(StockErrorCode.InsufficientStock, shortConsume.ErrorCode);
        Assert.Equal(
            "Not enough stock. Available: 15.55 LM, requested: 50.00 LM.",
            shortConsume.Message);

        var low = await _sut.GetLowStockItemsAsync(tenant, CancellationToken.None);
        Assert.Single(low);
        Assert.Equal(15.55m, low[0].Balance);

        var history = await _sut.GetItemHistoryAsync(
            tenant, created.Value.Id, null, null, CancellationToken.None);
        Assert.Equal(2, history.TotalCount);   // purchase + consume
    }

    [Fact]
    public async Tenants_are_isolated()
    {
        var a = _fixture.CreateTenant("StkA");
        var b = _fixture.CreateTenant("StkB");
        var sheet = await CreateAsync(a, Sheet());

        var theirs = await _sut.GetSheetAsync(b, sheet.Id, CancellationToken.None);
        Assert.Equal(StockErrorCode.NotFound, theirs.ErrorCode);

        var moveTheirs = await _sut.BuySheetStockAsync(
            b, sheet.Id, Guid.NewGuid(), new StockMovementRequest(1),
            null, CancellationToken.None);
        Assert.Equal(StockErrorCode.NotFound, moveTheirs.ErrorCode);

        var listTheirs = await _sut.SearchSheetsAsync(
            b, null, null, null, null, null, null, null, null, CancellationToken.None);
        Assert.Empty(listTheirs.Items);
    }

    private async Task<GlassSheetDto> CreateAsync(Guid tenant, GlassSheetRequest request)
    {
        var result = await _sut.CreateSheetAsync(
            tenant, Guid.NewGuid(), request, null, CancellationToken.None);
        Assert.True(result.Success, result.Message);
        return result.Value!;
    }
}
