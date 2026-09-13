// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/MaterialAllocationService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 14 Material Allocation (allocation foundation)
//   Distinguishes Available/Reserved/Allocated/Consumed/Released.
//   Allocation is traceable: Stock Sheet → Allocation → Optimization Layout
//   → Cut Piece → Production → Breakage/Rework → Consumption → Actual Cost
//   Idempotent via AllocationId unique constraint.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production
{
    public static class MaterialAllocationService
    {
        public static string BuildAllocationId(
            string reservationId, int sheetNo, string placedPieceLabel)
        {
            reservationId = (reservationId ?? "").Trim();
            placedPieceLabel = (placedPieceLabel ?? "").Trim();
            return $"{reservationId}|S{sheetNo}|{placedPieceLabel}";
        }

        public static async Task<(bool Ok, SavedMaterialAllocation? Allocation, string Message)>
            AllocateAsync(
                string reservationId,
                string jobRef,
                int specGroupNumber,
                int serialNumber,
                string optimizationSessionId,
                int sheetNo,
                string placedPieceLabel,
                string glassRef,
                int widthMm, int heightMm,
                int quantity,
                decimal utilizationPct,
                decimal wastagePct,
                string? notes = null,
                CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(reservationId)) return (false, null, "ReservationId required.");
            if (quantity <= 0) quantity = 1;

            ProductionRepository.EnsureInitialized();
            string allocationId = BuildAllocationId(reservationId, sheetNo, placedPieceLabel);

            try
            {
                using var db = new ProductionDbContext();

                var existing = await db.Allocations
                    .FirstOrDefaultAsync(a => a.AllocationId == allocationId, ct)
                    .ConfigureAwait(false);

                if (existing != null)
                    return (true, existing, "Allocation already exists (idempotent).");

                // Ensure reservation exists and is in valid state
                var reservation = await db.Reservations
                    .FirstOrDefaultAsync(r => r.ReservationId == reservationId, ct)
                    .ConfigureAwait(false);

                if (reservation == null)
                    return (false, null, $"Reservation {reservationId} not found.");

                if (reservation.Status == (int)ReservationStatus.Cancelled ||
                    reservation.Status == (int)ReservationStatus.Released)
                    return (false, null, $"Reservation {reservationId} is {((ReservationStatus)reservation.Status)} — cannot allocate.");

                var allocation = new SavedMaterialAllocation
                {
                    AllocationId = allocationId,
                    ReservationId = reservationId,
                    JobRef = jobRef ?? reservation.JobRef,
                    SpecGroupNumber = specGroupNumber,
                    SerialNumber = serialNumber,
                    OptimizationSessionId = optimizationSessionId ?? reservation.OptimizationSessionId,
                    SheetNo = sheetNo,
                    PlacedPieceLabel = placedPieceLabel ?? string.Empty,
                    GlassRef = glassRef ?? string.Empty,
                    WidthMm = widthMm,
                    HeightMm = heightMm,
                    Quantity = quantity,
                    UtilizationPct = utilizationPct,
                    WastagePct = wastagePct,
                    Status = (int)AllocationStatus.Allocated,
                    CreatedBy = AuditService.CurrentUsername,
                    CreatedAt = DateTime.Now,
                    Notes = notes ?? string.Empty
                };

                db.Allocations.Add(allocation);

                // Move reservation to Allocated if it was Reserved
                if (reservation.Status == (int)ReservationStatus.Reserved)
                    reservation.Status = (int)ReservationStatus.Allocated;

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                return (true, allocation, "Allocated.");
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("UNIQUE") == true || ex.Message.Contains("UNIQUE"))
            {
                try
                {
                    using var db = new ProductionDbContext();
                    var raced = await db.Allocations.FirstOrDefaultAsync(a => a.AllocationId == allocationId, ct).ConfigureAwait(false);
                    if (raced != null) return (true, raced, "Allocation already exists (race idempotent).");
                }
                catch { }
                return (false, null, "Allocation conflict (unique).");
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[MaterialAllocation] Allocate failed for {allocationId}",
                    ex, new LogContext { Module = "Production", Class = nameof(MaterialAllocationService), Operation = nameof(AllocateAsync) });
                return (false, null, $"Allocate failed: {ex.Message}");
            }
        }

        public static async Task<(bool Ok, string Message)> ConsumeAsync(
            string allocationId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(allocationId)) return (false, "AllocationId required.");
            ProductionRepository.EnsureInitialized();

            try
            {
                using var db = new ProductionDbContext();
                var alloc = await db.Allocations.FirstOrDefaultAsync(a => a.AllocationId == allocationId, ct).ConfigureAwait(false);
                if (alloc == null) return (false, "Allocation not found.");

                if (alloc.Status == (int)AllocationStatus.Consumed)
                    return (true, "Already consumed (idempotent).");

                if (alloc.Status == (int)AllocationStatus.Cancelled ||
                    alloc.Status == (int)AllocationStatus.Released)
                    return (false, $"Cannot consume {((AllocationStatus)alloc.Status)} allocation.");

                alloc.Status = (int)AllocationStatus.Consumed;
                alloc.ConsumedAt = DateTime.Now;

                // If all allocations for reservation consumed, mark reservation consumed
                var reservationId = alloc.ReservationId;
                var allAllocs = await db.Allocations
                    .Where(a => a.ReservationId == reservationId)
                    .ToListAsync(ct).ConfigureAwait(false);

                bool allConsumed = allAllocs.All(a => a.AllocationId == allocationId || a.Status == (int)AllocationStatus.Consumed);
                if (allConsumed)
                {
                    var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.ReservationId == reservationId, ct).ConfigureAwait(false);
                    if (reservation != null && reservation.Status != (int)ReservationStatus.Consumed)
                    {
                        reservation.Status = (int)ReservationStatus.Consumed;
                    }
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                return (true, "Consumed.");
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[MaterialAllocation] Consume failed for {allocationId}",
                    ex, new LogContext { Module = "Production", Class = nameof(MaterialAllocationService), Operation = nameof(ConsumeAsync) });
                return (false, $"Consume failed: {ex.Message}");
            }
        }

        public static async Task<List<SavedMaterialAllocation>> GetByJobAsync(string jobRef, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobRef)) return new List<SavedMaterialAllocation>();
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            return await db.Allocations.AsNoTracking()
                .Where(a => a.JobRef == jobRef)
                .OrderBy(a => a.SheetNo)
                .ThenBy(a => a.PlacedPieceLabel)
                .ToListAsync(ct).ConfigureAwait(false);
        }

        public static async Task<List<SavedMaterialAllocation>> GetByReservationAsync(string reservationId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(reservationId)) return new List<SavedMaterialAllocation>();
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            return await db.Allocations.AsNoTracking()
                .Where(a => a.ReservationId == reservationId)
                .ToListAsync(ct).ConfigureAwait(false);
        }
    }
}
