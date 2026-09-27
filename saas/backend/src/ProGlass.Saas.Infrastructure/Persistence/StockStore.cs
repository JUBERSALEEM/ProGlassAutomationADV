using Microsoft.EntityFrameworkCore;
using ProGlass.Saas.Application.Stock;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Infrastructure.Persistence;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 8 storage — the WPF SheetStoreRepository / InventoryItemRepository
//  ports over PostgreSQL (RLS-scoped contexts).
//
//  The stock increments stay SQL-side exactly like the WPF's fixed
//  BuyStock/UseStock (ExecuteUpdate under one transaction) — no
//  read-modify-write window. Every movement also appends its immutable
//  stock_transactions row in the SAME transaction, so the ledger can
//  never disagree with the balance.
// ═══════════════════════════════════════════════════════════════════════

public sealed class StockStore(SaasDbContextFactory factory) : IStockStore
{
    // ── Glass sheets ────────────────────────────────────────────────

    public async Task<MasterData.PagedResult<GlassSheet>> SearchSheetsAsync(
        Guid tenantId, string? category, string? color, string? supplier,
        int? thicknessMm, string? search, string? sortKey,
        int page, int pageSize, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var query = db.GlassSheets.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(g => g.Category == category);
        if (!string.IsNullOrWhiteSpace(color))
            query = query.Where(g => g.Color == color);
        if (!string.IsNullOrWhiteSpace(supplier))
            query = query.Where(g => g.Supplier == supplier);
        if (thicknessMm is > 0)
            query = query.Where(g => g.ThicknessMm == thicknessMm.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            // WPF note kept: thickness is a computed display on the WPF
            // side — searchable via the exact thicknessMm filter only.
            query = query.Where(g =>
                g.Category.ToLower().Contains(s)
                || g.Color.ToLower().Contains(s)
                || g.Supplier.ToLower().Contains(s));
        }

        // WPF sort keys verbatim; value sorts stay client-side there
        // (computed properties) and are not SQL sorts here either.
        query = sortKey switch
        {
            "newest" => query.OrderByDescending(g => g.CreatedAt),
            "oldest" => query.OrderBy(g => g.CreatedAt),
            "stockasc" => query.OrderBy(g => g.Stock),
            "stockdesc" => query.OrderByDescending(g => g.Stock),
            _ => query.OrderBy(g => g.Category)
                      .ThenBy(g => g.Color)
                      .ThenBy(g => g.ThicknessMm)
        };

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct).ConfigureAwait(false);

        return new MasterData.PagedResult<GlassSheet>(items, page, pageSize, total);
    }

    public async Task<GlassSheet?> FindSheetAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.GlassSheets.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<(bool Ok, string? Error)> TryAddSheetAsync(
        GlassSheet sheet, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(sheet.TenantId);
        db.GlassSheets.Add(sheet);
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return (true, null);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return (false, "A glass sheet with the same category, thickness, " +
                           "colour, size and supplier already exists.");
        }
    }

    public async Task<(bool Ok, string? Error)> TryUpdateSheetAsync(
        GlassSheet sheet, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(sheet.TenantId);
        db.GlassSheets.Update(sheet);
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return (true, null);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return (false, "Another glass sheet with the same category, " +
                           "thickness, colour, size and supplier already exists.");
        }
    }

    public async Task<bool> DeleteSheetAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var sheet = await db.GlassSheets
            .FirstOrDefaultAsync(g => g.Id == id, ct).ConfigureAwait(false);
        if (sheet is null)
        {
            return false;
        }

        db.GlassSheets.Remove(sheet);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<StockMovementResult> BuySheetStockAsync(
        Guid tenantId, Guid sheetId, int quantity, decimal? newPurchasePrice,
        Guid actorId, string? notes, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        await using var tx = await db.Database
            .BeginTransactionAsync(ct).ConfigureAwait(false);

        var applyPrice = newPurchasePrice is > 0;
        var updated = applyPrice
            ? await db.GlassSheets
                .Where(g => g.Id == sheetId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(g => g.Stock, g => g.Stock + quantity)
                    .SetProperty(g => g.PurchasePricePerSqm, _ => newPurchasePrice!.Value)
                    .SetProperty(g => g.LastPurchaseDate,
                        _ => DateOnly.FromDateTime(DateTime.UtcNow))
                    .SetProperty(g => g.UpdatedAt, _ => DateTime.UtcNow)
                    .SetProperty(g => g.UpdatedBy, _ => actorId), ct)
            : await db.GlassSheets
                .Where(g => g.Id == sheetId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(g => g.Stock, g => g.Stock + quantity)
                    .SetProperty(g => g.LastPurchaseDate,
                        _ => DateOnly.FromDateTime(DateTime.UtcNow))
                    .SetProperty(g => g.UpdatedAt, _ => DateTime.UtcNow)
                    .SetProperty(g => g.UpdatedBy, _ => actorId), ct);

        if (updated == 0)
        {
            return StockMovementResult.NotFound;
        }

        var sheet = await db.GlassSheets.AsNoTracking()
            .Where(g => g.Id == sheetId)
            .Select(g => new { g.Stock, g.Used, g.PurchasePricePerSqm })
            .FirstAsync(ct).ConfigureAwait(false);
        var balance = sheet.Stock - sheet.Used;

        db.StockTransactions.Add(new StockTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ItemType = "sheet",
            GlassSheetId = sheetId,
            TransactionType = "purchase",
            QuantityDelta = quantity,
            BalanceAfter = balance,
            UnitCost = sheet.PurchasePricePerSqm,
            PerformedBy = actorId,
            Notes = notes ?? ""
        });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return StockMovementResult.Ok(balance);
    }

    public async Task<StockMovementResult> UseSheetStockAsync(
        Guid tenantId, Guid sheetId, int quantity,
        Guid actorId, string? notes, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        await using var tx = await db.Database
            .BeginTransactionAsync(ct).ConfigureAwait(false);

        // The guard is IN the WHERE clause — the WPF's race fix verbatim.
        var updated = await db.GlassSheets
            .Where(g => g.Id == sheetId && g.Stock - g.Used >= quantity)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(g => g.Used, g => g.Used + quantity)
                .SetProperty(g => g.UpdatedAt, _ => DateTime.UtcNow)
                .SetProperty(g => g.UpdatedBy, _ => actorId), ct);

        if (updated == 0)
        {
            // Not found vs short — re-read to tell them apart (only to
            // choose the error; the write decision was already atomic).
            var sheet = await db.GlassSheets.AsNoTracking()
                .Where(g => g.Id == sheetId)
                .Select(g => (int?)g.Stock - g.Used)
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (sheet is null)
            {
                return StockMovementResult.NotFound;
            }

            return new StockMovementResult(
                StockMovementOutcome.InsufficientStock, sheet.Value, null);
        }

        var after = await db.GlassSheets.AsNoTracking()
            .Where(g => g.Id == sheetId)
            .Select(g => new { Balance = g.Stock - g.Used, g.SellPricePerSqm })
            .FirstAsync(ct).ConfigureAwait(false);

        db.StockTransactions.Add(new StockTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ItemType = "sheet",
            GlassSheetId = sheetId,
            TransactionType = "issue",
            QuantityDelta = -quantity,
            BalanceAfter = after.Balance,
            UnitCost = after.SellPricePerSqm,
            PerformedBy = actorId,
            Notes = notes ?? ""
        });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return StockMovementResult.Ok(after.Balance);
    }

    public async Task<IReadOnlyList<GlassSheet>> GetLowStockSheetsAsync(
        Guid tenantId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        // GlassSheetLowStock.Threshold = 5 — the ONE declaration
        // (balanced ≤ 5 needs reordering).
        return await db.GlassSheets.AsNoTracking()
            .Where(g => g.Stock - g.Used <= 5)
            .OrderBy(g => g.Stock - g.Used)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<SheetFiltersDto> GetSheetFiltersAsync(
        Guid tenantId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var sheets = db.GlassSheets.AsNoTracking();
        return new SheetFiltersDto(
            await sheets.Select(g => g.Category).Distinct().OrderBy(c => c)
                .ToListAsync(ct).ConfigureAwait(false),
            await sheets.Select(g => g.Color).Distinct().OrderBy(c => c)
                .ToListAsync(ct).ConfigureAwait(false),
            await sheets.Select(g => g.Supplier).Where(s => s != "").Distinct()
                .OrderBy(s => s).ToListAsync(ct).ConfigureAwait(false),
            await sheets.Select(g => g.ThicknessMm).Distinct().OrderBy(t => t)
                .ToListAsync(ct).ConfigureAwait(false));
    }

    public async Task<MasterData.PagedResult<StockTransaction>> GetSheetHistoryAsync(
        Guid tenantId, Guid sheetId, int page, int pageSize, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var query = db.StockTransactions.AsNoTracking()
            .Where(t => t.GlassSheetId == sheetId);
        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct).ConfigureAwait(false);
        return new MasterData.PagedResult<StockTransaction>(items, page, pageSize, total);
    }

    // ── Inventory items ─────────────────────────────────────────────

    public async Task<MasterData.PagedResult<InventoryItem>> SearchItemsAsync(
        Guid tenantId, string? category, string? supplier, string? search,
        int page, int pageSize, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var query = db.InventoryItems.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(i => i.Category == category);
        if (!string.IsNullOrWhiteSpace(supplier))
            query = query.Where(i => i.Supplier == supplier);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(i =>
                i.Name.ToLower().Contains(s)
                || i.Supplier.ToLower().Contains(s)
                || i.Category.ToLower().Contains(s));
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .OrderBy(i => i.Category).ThenBy(i => i.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct).ConfigureAwait(false);

        return new MasterData.PagedResult<InventoryItem>(items, page, pageSize, total);
    }

    public async Task<InventoryItem?> FindItemAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.InventoryItems.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<(bool Ok, string? Error)> TryAddItemAsync(
        InventoryItem item, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(item.TenantId);
        db.InventoryItems.Add(item);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> TryUpdateItemAsync(
        InventoryItem item, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(item.TenantId);
        db.InventoryItems.Update(item);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return (true, null);
    }

    public async Task<bool> DeleteItemAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var item = await db.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == id, ct).ConfigureAwait(false);
        if (item is null)
        {
            return false;
        }

        db.InventoryItems.Remove(item);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<StockMovementResult> ReceiveItemStockAsync(
        Guid tenantId, Guid itemId, decimal quantity, decimal? newUnitPrice,
        Guid actorId, string? notes, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        await using var tx = await db.Database
            .BeginTransactionAsync(ct).ConfigureAwait(false);

        var applyPrice = newUnitPrice is > 0;
        var updated = applyPrice
            ? await db.InventoryItems
                .Where(i => i.Id == itemId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.Stock, i => i.Stock + quantity)
                    .SetProperty(i => i.PurchasePricePerUnit, _ => newUnitPrice!.Value)
                    .SetProperty(i => i.UpdatedAt, _ => DateTime.UtcNow)
                    .SetProperty(i => i.UpdatedBy, _ => actorId), ct)
            : await db.InventoryItems
                .Where(i => i.Id == itemId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.Stock, i => i.Stock + quantity)
                    .SetProperty(i => i.UpdatedAt, _ => DateTime.UtcNow)
                    .SetProperty(i => i.UpdatedBy, _ => actorId), ct);

        if (updated == 0)
        {
            return StockMovementResult.NotFound;
        }

        var item = await db.InventoryItems.AsNoTracking()
            .Where(i => i.Id == itemId)
            .Select(i => new { i.Stock, i.Used, i.PurchasePricePerUnit })
            .FirstAsync(ct).ConfigureAwait(false);
        var balance = item.Stock - item.Used;

        db.StockTransactions.Add(new StockTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ItemType = "inventory",
            InventoryItemId = itemId,
            TransactionType = "purchase",
            QuantityDelta = quantity,
            BalanceAfter = balance,
            UnitCost = item.PurchasePricePerUnit,
            PerformedBy = actorId,
            Notes = notes ?? ""
        });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return StockMovementResult.Ok(balance);
    }

    public async Task<StockMovementResult> ConsumeItemStockAsync(
        Guid tenantId, Guid itemId, decimal quantity,
        Guid actorId, string? notes, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        await using var tx = await db.Database
            .BeginTransactionAsync(ct).ConfigureAwait(false);

        var updated = await db.InventoryItems
            .Where(i => i.Id == itemId && i.Stock - i.Used >= quantity)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(i => i.Used, i => i.Used + quantity)
                .SetProperty(i => i.UpdatedAt, _ => DateTime.UtcNow)
                .SetProperty(i => i.UpdatedBy, _ => actorId), ct);

        if (updated == 0)
        {
            var balance = await db.InventoryItems.AsNoTracking()
                .Where(i => i.Id == itemId)
                .Select(i => (decimal?)i.Stock - i.Used)
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (balance is null)
            {
                return StockMovementResult.NotFound;
            }

            return new StockMovementResult(
                StockMovementOutcome.InsufficientStock, null, balance.Value);
        }

        var after = await db.InventoryItems.AsNoTracking()
            .Where(i => i.Id == itemId)
            .Select(i => new { Balance = i.Stock - i.Used, i.SellPricePerUnit })
            .FirstAsync(ct).ConfigureAwait(false);

        db.StockTransactions.Add(new StockTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ItemType = "inventory",
            InventoryItemId = itemId,
            TransactionType = "consume",
            QuantityDelta = -quantity,
            BalanceAfter = after.Balance,
            UnitCost = after.SellPricePerUnit,
            PerformedBy = actorId,
            Notes = notes ?? ""
        });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return StockMovementResult.Ok(after.Balance);
    }

    public async Task<IReadOnlyList<InventoryItem>> GetLowStockItemsAsync(
        Guid tenantId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        // WPF IsLowStock: a zero reorder level means "not tracked".
        return await db.InventoryItems.AsNoTracking()
            .Where(i => i.ReorderLevel > 0 && i.Stock - i.Used <= i.ReorderLevel)
            .OrderBy(i => i.Stock - i.Used)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetItemSuppliersAsync(
        Guid tenantId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.InventoryItems.AsNoTracking()
            .Select(i => i.Supplier)
            .Where(s => s != "")
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<MasterData.PagedResult<StockTransaction>> GetItemHistoryAsync(
        Guid tenantId, Guid itemId, int page, int pageSize, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var query = db.StockTransactions.AsNoTracking()
            .Where(t => t.InventoryItemId == itemId);
        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct).ConfigureAwait(false);
        return new MasterData.PagedResult<StockTransaction>(items, page, pageSize, total);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true
        || ex.InnerException?.Message.Contains(
            "duplicate key value violates unique constraint",
            StringComparison.OrdinalIgnoreCase) == true;
}
