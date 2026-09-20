// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/Cutting/StockSelectionService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 15 Stock Selection
//   Deterministic stock selection using existing Sheet Store.
//   No second inventory system, no hard-coded 3210×2250 as production rule.
//   Selection considers material compatibility, dimensions, available balance,
//   reserved qty, allocated qty, requested qty.
//   Deterministic ordering: exact material match, smallest valid stock,
//   lowest waste candidate, stable StockSheetId.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production.Cutting;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Optimization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production.Cutting
{
    public sealed class StockSelectionResult
    {
        public GlassSheet Sheet { get; set; } = null!;
        public StockSize StockSize { get; set; } = null!;
        public int AvailableBalance { get; set; }
        public int ReservedQty { get; set; }
        public int AllocatedQty { get; set; }
        // ReservedQty already sums reservation Quantity for Reserved+Allocated
        // (stock sheets). AllocatedQty is placed-piece counts — a different
        // unit. Subtracting both treated 15 pieces as 15 sheets.
        public int EffectiveAvailable => Math.Max(0, AvailableBalance - ReservedQty);
        public decimal WasteScore { get; set; } // Lower is better
    }

    public static class StockSelectionService
    {
        /// <summary>
        /// Get available sheets from Sheet Store for a material group.
        /// Deterministic ordering: exact material match, smallest valid stock,
        /// lowest waste candidate, stable StockSheetId.
        /// </summary>
        public static async Task<List<StockSelectionResult>> GetCompatibleSheetsAsync(
            MaterialGroup group,
            CancellationToken ct = default)
        {
            var results = new List<StockSelectionResult>();
            if (group == null || group.Requirements.Count == 0) return results;

            try
            {
                using var appDb = new AppDbContext();

                // Balance is [NotMapped] Stock − Used. EF cannot translate
                // g.Balance; the arithmetic is the same SQL as UseStockAsync.
                var allSheets = await appDb.GlassSheets.AsNoTracking()
                    .Where(g => g.Stock - g.Used > 0)
                    .ToListAsync(ct).ConfigureAwait(false);

                // Filter by material compatibility
                var compatible = allSheets.Where(s => IsSheetCompatibleWithGroup(s, group)).ToList();

                // Get reservations per sheet for this job/material
                var reservedMap = await GetReservedPerSheetAsync(group, ct).ConfigureAwait(false);
                var allocatedMap = await GetAllocatedPerSheetAsync(group, ct).ConfigureAwait(false);

                foreach (var sheet in compatible)
                {
                    ct.ThrowIfCancellationRequested();

                    reservedMap.TryGetValue(sheet.Id, out int reserved);
                    allocatedMap.TryGetValue(sheet.Id, out int allocated);

                    int available = sheet.Balance;
                    int effective = Math.Max(0, available - reserved - allocated);

                    // Only include if effective available >0 or unlimited fallback
                    if (effective <= 0 && sheet.Balance > 0) continue; // No effective stock

                    var stockSize = new StockSize
                    {
                        WidthMm = sheet.WidthMm,
                        HeightMm = sheet.HeightMm,
                        ThicknessMm = sheet.ThicknessMm,
                        Color = sheet.Color,
                        Label = $"{sheet.Category} {sheet.Color} {sheet.ThicknessMm}mm {sheet.SizeDisplay} (Bal {sheet.Balance})",
                        MaxSheets = effective
                    };

                    // Waste score: smallest valid stock that fits max piece
                    var maxPiece = group.Requirements.OrderByDescending(r => r.WidthMm * r.HeightMm).FirstOrDefault();
                    decimal wasteScore = 0;
                    if (maxPiece != null)
                    {
                        decimal sheetArea = (decimal)sheet.WidthMm * sheet.HeightMm;
                        decimal pieceArea = (decimal)maxPiece.WidthMm * maxPiece.HeightMm;
                        wasteScore = sheetArea - pieceArea; // Smaller waste = better
                    }

                    results.Add(new StockSelectionResult
                    {
                        Sheet = sheet,
                        StockSize = stockSize,
                        AvailableBalance = available,
                        ReservedQty = reserved,
                        AllocatedQty = allocated,
                        WasteScore = wasteScore
                    });
                }

                // Deterministic ordering:
                // 1. Exact material match (already filtered)
                // 2. Smallest valid stock (waste score ascending)
                // 3. Lowest waste candidate (same as 2)
                // 4. Stable StockSheetId ascending
                var ordered = results
                    .OrderBy(r => r.WasteScore)
                    .ThenBy(r => r.Sheet.WidthMm * r.Sheet.HeightMm)
                    .ThenBy(r => r.Sheet.Id)
                    .ToList();

                return ordered;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"[StockSelection] Failed for group {group?.GroupId}: {ex.Message}",
                    new LogContext { Module = "Production", Class = nameof(StockSelectionService), Operation = nameof(GetCompatibleSheetsAsync) });

                // A failed load is not a healthy rack of 100 sheets.
                return new List<StockSelectionResult>();
            }
        }

        private static bool IsSheetCompatibleWithGroup(GlassSheet sheet, MaterialGroup group)
        {
            if (sheet == null || group == null) return false;

            // Thickness must match exactly
            if (sheet.ThicknessMm != group.ThicknessMm) return false;

            // Color must match (case-insensitive)
            if (!string.Equals(sheet.Color?.Trim(), group.Color?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                // Allow Clear vs empty as compatible? For safety, require exact match
                // But also allow family-based fallback: if group family is Clear and sheet category Clear
                if (!string.Equals(sheet.Category?.Trim(), group.GlassFamily?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(sheet.Color?.Trim(), group.GlassFamily?.Trim(), StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Category/Family must match — Low-E vs Clear not allowed
            string sheetFamily = (sheet.Category ?? "").Trim();
            string groupFamily = (group.GlassFamily ?? "").Trim();

            // If both specify family, must match; if sheet category is generic, allow color match
            if (!string.IsNullOrWhiteSpace(sheetFamily) && !string.IsNullOrWhiteSpace(groupFamily))
            {
                // Exact or contains
                if (!sheetFamily.Equals(groupFamily, StringComparison.OrdinalIgnoreCase) &&
                    !sheetFamily.Contains(groupFamily, StringComparison.OrdinalIgnoreCase) &&
                    !groupFamily.Contains(sheetFamily, StringComparison.OrdinalIgnoreCase))
                {
                    // Special case: Low-E sheets have Category Low-E, group family Low-E
                    // Clear sheets Category Clear, group family Clear — must match
                    // If sheet category is Clear and group family is Low-E → not compatible
                    if (sheetFamily.Equals("Clear", StringComparison.OrdinalIgnoreCase) &&
                        groupFamily.Equals("Low-E", StringComparison.OrdinalIgnoreCase))
                        return false;
                    if (sheetFamily.Equals("Low-E", StringComparison.OrdinalIgnoreCase) &&
                        groupFamily.Equals("Clear", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }

            // Dimensions: sheet must be at least as large as largest piece in group
            var maxW = group.Requirements.Max(r => Math.Max(r.WidthMm, r.HeightMm));
            var maxH = group.Requirements.Max(r => Math.Min(r.WidthMm, r.HeightMm));

            // Sheet must fit max piece (considering rotation)
            bool fits = (sheet.WidthMm >= maxW && sheet.HeightMm >= maxH) ||
                        (sheet.WidthMm >= maxH && sheet.HeightMm >= maxW);

            return fits;
        }

        private static async Task<Dictionary<int, int>> GetReservedPerSheetAsync(MaterialGroup group, CancellationToken ct)
        {
            var map = new Dictionary<int, int>();
            try
            {
                ProductionRepository.EnsureInitialized();
                using var db = new ProductionDbContext();

                var reservations = await db.Reservations.AsNoTracking()
                    .Where(r => r.Status == (int)Models.Production.SavedProduction.ReservationStatus.Reserved ||
                                r.Status == (int)Models.Production.SavedProduction.ReservationStatus.Allocated)
                    .ToListAsync(ct).ConfigureAwait(false);

                foreach (var res in reservations)
                {
                    if (res.GlassSheetId <= 0) continue;
                    map.TryGetValue(res.GlassSheetId, out int current);
                    map[res.GlassSheetId] = current + res.Quantity;
                }
            }
            catch
            {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("foreach suppressed");
            }
            return map;
        }

        private static async Task<Dictionary<int, int>> GetAllocatedPerSheetAsync(MaterialGroup group, CancellationToken ct)
        {
            var map = new Dictionary<int, int>();
            try
            {
                ProductionRepository.EnsureInitialized();
                using var db = new ProductionDbContext();

                var allocations = await db.Allocations.AsNoTracking()
                    .Where(a => a.Status == (int)Models.Production.SavedProduction.AllocationStatus.Allocated)
                    .ToListAsync(ct).ConfigureAwait(false);

                // Allocations link to reservation, need to resolve GlassSheetId via reservation
                var reservationIds = allocations.Select(a => a.ReservationId).Distinct().ToList();
                var reservations = await db.Reservations.AsNoTracking()
                    .Where(r => reservationIds.Contains(r.ReservationId))
                    .ToDictionaryAsync(r => r.ReservationId, r => r.GlassSheetId, ct).ConfigureAwait(false);

                foreach (var alloc in allocations)
                {
                    if (!reservations.TryGetValue(alloc.ReservationId, out int sheetId)) continue;
                    if (sheetId <= 0) continue;
                    map.TryGetValue(sheetId, out int current);
                    map[sheetId] = current + alloc.Quantity;
                }
            }
            catch
            {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("foreach suppressed");
            }
            return map;
        }

        /// <summary>
        /// Deterministic stock selection: returns best sheets for requested quantity.
        /// </summary>
        public static async Task<List<StockSelectionResult>> SelectBestSheetsAsync(
            MaterialGroup group,
            int requestedSheetCount,
            CancellationToken ct = default)
        {
            var compatible = await GetCompatibleSheetsAsync(group, ct).ConfigureAwait(false);

            if (compatible.Count == 0) return compatible;

            // For cutting, we need enough sheets to cover total area
            // Estimate sheets needed: total area / (sheet area * avg utilization 80%)
            decimal totalArea = group.TotalAreaSqm;
            var bestSheet = compatible.First();
            decimal sheetArea = (decimal)bestSheet.Sheet.WidthMm * bestSheet.Sheet.HeightMm / 1_000_000m;
            decimal avgUtil = 0.8m;
            int estimatedSheets = sheetArea > 0 ? (int)Math.Ceiling(totalArea / (sheetArea * avgUtil)) : 1;
            estimatedSheets = Math.Max(1, Math.Max(estimatedSheets, requestedSheetCount));

            // Take enough sheets deterministically
            var selected = new List<StockSelectionResult>();
            int remaining = estimatedSheets;

            foreach (var cand in compatible)
            {
                if (remaining <= 0) break;
                int take = Math.Min(remaining, cand.EffectiveAvailable > 0 ? cand.EffectiveAvailable : remaining);
                if (take > 0)
                {
                    selected.Add(cand);
                    remaining -= take;
                }
            }

            // If not enough effective stock, include fallback unlimited
            if (remaining > 0 && compatible.Any(c => c.Sheet.Id == 0))
            {
                var fallback = compatible.First(c => c.Sheet.Id == 0);
                selected.Add(fallback);
            }

            return selected;
        }
    }
}
