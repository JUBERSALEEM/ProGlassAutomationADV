using Microsoft.EntityFrameworkCore;
using ProGlass.Saas.Application.Optimization;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Optimization;

namespace ProGlass.Saas.Infrastructure.Persistence;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 11 storage — the WPF OptimizationSessionRepository port:
//  sessions/sheets/placements/cuts/leftovers persisted in ONE
//  transaction per group; RLS-scoped contexts per tenant.
// ═══════════════════════════════════════════════════════════════════════

public sealed class OptimizationStore(SaasDbContextFactory factory) : IOptimizationStore
{
    public async Task<JobOrder?> FindJobAsync(
        Guid tenantId, Guid jobOrderId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.JobOrders.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobOrderId, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GlassSheet>> ListSheetsAsync(
        Guid tenantId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.GlassSheets.AsNoTracking()
            .OrderBy(g => g.ThicknessMm).ThenBy(g => g.WidthMm)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> CountSessionsSinceAsync(
        Guid tenantId, DateOnly since, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var from = since.ToDateTime(TimeOnly.MinValue);
        return await db.OptimizationSessions.AsNoTracking()
            .Where(s => s.CreatedAt >= from)
            .CountAsync(ct).ConfigureAwait(false);
    }

    public async Task<OptimizationSession> PersistAsync(
        Guid tenantId, Guid actorId,
        string documentType, Guid? documentId, string compositionKey,
        OptimizationResult plan, string paramsJson, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var now = DateTime.UtcNow;
        var largestUsable = plan.Sheets.Count == 0 ? 0m
            : plan.Sheets.Max(s =>
                Math.Round((decimal)s.UsableWidth * s.UsableHeight / 1_000_000m,
                    4, MidpointRounding.AwayFromZero));

        var session = new OptimizationSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentType = documentType,
            DocumentId = documentId,
            CompositionKey = compositionKey,
            ParamsJson = paramsJson,
            Status = "completed",
            Strategy = plan.Strategy,
            SheetsUsed = plan.Sheets.Count,
            UnplacedCount = plan.Unplaced.Count,
            TotalUsedSqm = plan.TotalUsedSqm,
            TotalStockSqm = plan.TotalStockSqm,
            UtilizationPct = plan.UtilizationPct,
            ConservationVerified = plan.ConservationVerified,
            GeometryVerified = plan.GeometryVerified,
            ValidationProblems = System.Text.Json.JsonSerializer.Serialize(
                plan.ValidationProblems),
            ScaleNotes = System.Text.Json.JsonSerializer.Serialize(plan.ScaleNotes),
            SheetsLowerBound = plan.SheetsLowerBound(largestUsable),
            CreatedBy = actorId,
            StartedAt = now,
            CompletedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            UpdatedBy = actorId
        };
        db.OptimizationSessions.Add(session);

        foreach (var sheet in plan.Sheets)
        {
            var sheetRow = new OptimizationSheet
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SessionId = session.Id,
                SheetNo = sheet.SheetNo,
                StockWMm = sheet.Stock.WidthMm,
                StockHMm = sheet.Stock.HeightMm,
                ThicknessMm = sheet.Stock.ThicknessMm,
                Color = sheet.Stock.Color,
                MaterialKey = sheet.Stock.MaterialKey,
                UsableX = sheet.UsableX,
                UsableY = sheet.UsableY,
                UsableW = sheet.UsableWidth,
                UsableH = sheet.UsableHeight,
                UsedSqm = sheet.UsedSqm,
                StockSqm = sheet.StockSqm,
                UtilizationPct = sheet.UtilizationPct,
                TrimCutCount = sheet.TrimCutCount,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId
            };
            db.OptimizationSheets.Add(sheetRow);

            foreach (var p in sheet.Pieces)
            {
                db.OptimizationPlacements.Add(new OptimizationPlacement
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SheetId = sheetRow.Id,
                    PieceRef = p.Piece.PieceLabel,
                    JobRef = p.Piece.JobRef,
                    GlassRef = p.Piece.GlassRef,
                    SpecLabel = p.Piece.SpecLabel,
                    Position = p.Piece.Position,
                    Color = p.Piece.Color,
                    ThicknessMm = p.Piece.ThicknessMm,
                    XMm = p.X,
                    YMm = p.Y,
                    WMm = p.WidthMm,
                    HMm = p.HeightMm,
                    Rotated = p.Rotated,
                    Qty = 1,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = actorId,
                    UpdatedBy = actorId
                });
            }

            foreach (var c in sheet.Cuts)
            {
                db.OptimizationCuts.Add(new OptimizationCut
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SheetId = sheetRow.Id,
                    Seq = c.Seq,
                    Axis = c.Axis == CutAxis.Horizontal ? "H" : "V",
                    AtMm = c.AtMm,
                    FromX = c.FromX,
                    FromY = c.FromY,
                    FromW = c.FromW,
                    FromH = c.FromH,
                    Stage = c.Stage,
                    IsTrim = c.IsTrim,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = actorId,
                    UpdatedBy = actorId
                });
            }

            foreach (var l in sheet.Leftovers)
            {
                db.OptimizationLeftovers.Add(new OptimizationLeftover
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SheetId = sheetRow.Id,
                    XMm = (int)Math.Round(l.X),
                    YMm = (int)Math.Round(l.Y),
                    WMm = (int)Math.Round(l.WidthMm),
                    HMm = (int)Math.Round(l.HeightMm),
                    IsReusable = l.IsUsable,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = actorId,
                    UpdatedBy = actorId
                });
            }
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return session;
    }

    public async Task<OptimizationSession?> FindSessionAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.OptimizationSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OptimizationSession>> ListSessionsAsync(
        Guid tenantId, Guid? jobOrderId, int page, int pageSize,
        CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var query = db.OptimizationSessions.AsNoTracking().AsQueryable();
        if (jobOrderId.HasValue)
        {
            query = query.Where(s => s.DocumentId == jobOrderId.Value);
        }

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OptimizationSheet>> ListSheetsForSessionAsync(
        Guid tenantId, Guid sessionId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.OptimizationSheets.AsNoTracking()
            .Where(s => s.SessionId == sessionId)
            .OrderBy(s => s.SheetNo)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OptimizationPlacement>> ListPlacementsAsync(
        Guid tenantId, IReadOnlyList<Guid> sheetIds, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.OptimizationPlacements.AsNoTracking()
            .Where(p => sheetIds.Contains(p.SheetId))
            .OrderBy(p => p.SheetId).ThenBy(p => p.YMm).ThenBy(p => p.XMm)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OptimizationCut>> ListCutsAsync(
        Guid tenantId, IReadOnlyList<Guid> sheetIds, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.OptimizationCuts.AsNoTracking()
            .Where(c => sheetIds.Contains(c.SheetId))
            .OrderBy(c => c.SheetId).ThenBy(c => c.Seq)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OptimizationLeftover>> ListLeftoversAsync(
        Guid tenantId, IReadOnlyList<Guid> sheetIds, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.OptimizationLeftovers.AsNoTracking()
            .Where(l => sheetIds.Contains(l.SheetId))
            .OrderBy(l => l.SheetId).ThenBy(l => l.YMm).ThenBy(l => l.XMm)
            .ToListAsync(ct).ConfigureAwait(false);
    }
}
