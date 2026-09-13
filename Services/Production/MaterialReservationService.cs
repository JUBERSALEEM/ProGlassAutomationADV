// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/MaterialReservationService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 14 Material Reservation (reservation foundation)
//   Implements auditable reservations with idempotency, owner/source tracking,
//   Available/Reserved/Allocated/Consumed/Released states.
//   Lives in production.db, links logically to GlassSheet (proglass.db)
//   via GlassSheetId (cross-DB logical FK, no enforced FK).
//   Cross-DB failure: reservation in production.db is primary; actual
//   stock deduction in proglass.db is compensatable via release.
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
    public static class MaterialReservationService
    {
        /// <summary>
        /// Deterministic reservation id for idempotency:
        /// JobRef|Spec|Serial|GlassSheetId|OptSession|SheetNo
        /// </summary>
        public static string BuildReservationId(
            string jobRef, int specGroup, int serial,
            int glassSheetId, string optSessionId, int sheetNo = 0)
        {
            jobRef = (jobRef ?? "").Trim();
            optSessionId = (optSessionId ?? "").Trim();
            return $"{jobRef}|{specGroup}|{serial}|{glassSheetId}|{optSessionId}|{sheetNo}";
        }

        /// <summary>
        /// Reserve material — idempotent. Repeated calls with same deterministic
        /// ReservationId return existing reservation, no duplicate.
        /// </summary>
        public static async Task<(bool Ok, SavedMaterialReservation? Reservation, string Message)>
            ReserveAsync(
                string jobRef,
                string sourceProformaId,
                int specGroupNumber,
                int serialNumber,
                string glassRef,
                string specName,
                string optimizationSessionId,
                int glassSheetId,
                string stockSizeLabel,
                int widthMm, int heightMm, int thicknessMm,
                string color,
                int quantity,
                string? notes = null,
                CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobRef)) return (false, null, "JobRef required.");
            if (quantity <= 0) return (false, null, "Quantity must be > 0.");
            if (glassSheetId <= 0 && string.IsNullOrWhiteSpace(stockSizeLabel))
                return (false, null, "GlassSheetId or StockSizeLabel required.");

            ProductionRepository.EnsureInitialized();

            string reservationId = BuildReservationId(
                jobRef, specGroupNumber, serialNumber,
                glassSheetId, optimizationSessionId, sheetNo: 0);

            try
            {
                using var db = new ProductionDbContext();

                // Idempotency: return existing if already reserved
                var existing = await db.Reservations
                    .FirstOrDefaultAsync(r => r.ReservationId == reservationId, ct)
                    .ConfigureAwait(false);

                if (existing != null)
                {
                    // If already cancelled/released, re-activate as Reserved
                    if (existing.Status == (int)ReservationStatus.Cancelled ||
                        existing.Status == (int)ReservationStatus.Released)
                    {
                        existing.Status = (int)ReservationStatus.Reserved;
                        existing.ReleasedAt = null;
                        existing.ReleaseReason = string.Empty;
                        existing.Quantity = quantity;
                        existing.Notes = notes ?? existing.Notes;
                        await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    }
                    return (true, existing, "Reservation already exists (idempotent).");
                }

                var reservation = new SavedMaterialReservation
                {
                    ReservationId = reservationId,
                    JobRef = jobRef,
                    SourceProformaId = sourceProformaId ?? string.Empty,
                    SpecGroupNumber = specGroupNumber,
                    SerialNumber = serialNumber,
                    GlassRef = glassRef ?? string.Empty,
                    SpecName = specName ?? string.Empty,
                    OptimizationSessionId = optimizationSessionId ?? string.Empty,
                    GlassSheetId = glassSheetId,
                    StockSizeLabel = stockSizeLabel ?? string.Empty,
                    WidthMm = widthMm,
                    HeightMm = heightMm,
                    ThicknessMm = thicknessMm,
                    Color = color ?? string.Empty,
                    Quantity = quantity,
                    Status = (int)ReservationStatus.Reserved,
                    CreatedBy = AuditService.CurrentUsername,
                    CreatedAt = DateTime.Now,
                    Notes = notes ?? string.Empty
                };

                db.Reservations.Add(reservation);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                AppLogService.Info($"[MaterialReservation] Reserved {quantity} x {stockSizeLabel} for {jobRef} Spec {specGroupNumber} Serial {serialNumber}",
                    new LogContext { Module = "Production", Class = nameof(MaterialReservationService), Operation = nameof(ReserveAsync) });

                return (true, reservation, "Reserved.");
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("UNIQUE") == true ||
                                               ex.Message.Contains("UNIQUE"))
            {
                // Race: another thread inserted same ReservationId — fetch existing
                try
                {
                    using var db = new ProductionDbContext();
                    var raced = await db.Reservations
                        .FirstOrDefaultAsync(r => r.ReservationId == reservationId, ct)
                        .ConfigureAwait(false);
                    if (raced != null) return (true, raced, "Reservation already exists (race idempotent).");
                }
                catch { }
                AppLogService.Warning($"[MaterialReservation] UNIQUE race for {reservationId}: {ex.Message}",
                    new LogContext { Module = "Production", Class = nameof(MaterialReservationService), Operation = nameof(ReserveAsync) });
                return (false, null, "Reservation conflict (unique).");
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[MaterialReservation] Reserve failed for {jobRef}",
                    ex, new LogContext { Module = "Production", Class = nameof(MaterialReservationService), Operation = nameof(ReserveAsync) });
                return (false, null, $"Reserve failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Release reservation — makes material available again only when safe
        /// (not consumed). Consumed material is never released.
        /// </summary>
        public static async Task<(bool Ok, string Message)> ReleaseAsync(
            string reservationId, string releaseReason, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(reservationId)) return (false, "ReservationId required.");
            ProductionRepository.EnsureInitialized();

            try
            {
                using var db = new ProductionDbContext();
                var res = await db.Reservations
                    .FirstOrDefaultAsync(r => r.ReservationId == reservationId, ct)
                    .ConfigureAwait(false);

                if (res == null) return (false, "Reservation not found.");

                if (res.Status == (int)ReservationStatus.Consumed)
                    return (false, "Cannot release consumed material.");

                if (res.Status == (int)ReservationStatus.Released ||
                    res.Status == (int)ReservationStatus.Cancelled)
                    return (true, "Already released/cancelled (idempotent).");

                res.Status = (int)ReservationStatus.Released;
                res.ReleasedAt = DateTime.Now;
                res.ReleaseReason = releaseReason ?? string.Empty;

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                return (true, "Released.");
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[MaterialReservation] Release failed for {reservationId}",
                    ex, new LogContext { Module = "Production", Class = nameof(MaterialReservationService), Operation = nameof(ReleaseAsync) });
                return (false, $"Release failed: {ex.Message}");
            }
        }

        public static async Task<(bool Ok, string Message)> CancelAsync(
            string reservationId, string cancelReason, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(reservationId)) return (false, "ReservationId required.");
            ProductionRepository.EnsureInitialized();

            try
            {
                using var db = new ProductionDbContext();
                var res = await db.Reservations
                    .FirstOrDefaultAsync(r => r.ReservationId == reservationId, ct)
                    .ConfigureAwait(false);

                if (res == null) return (false, "Reservation not found.");

                if (res.Status == (int)ReservationStatus.Consumed)
                    return (false, "Cannot cancel consumed material.");

                if (res.Status == (int)ReservationStatus.Cancelled)
                    return (true, "Already cancelled (idempotent).");

                res.Status = (int)ReservationStatus.Cancelled;
                res.ReleasedAt = DateTime.Now;
                res.ReleaseReason = cancelReason ?? string.Empty;

                // Also cancel related allocations that are not consumed
                var allocs = await db.Allocations
                    .Where(a => a.ReservationId == reservationId &&
                                a.Status != (int)AllocationStatus.Consumed &&
                                a.Status != (int)AllocationStatus.Cancelled)
                    .ToListAsync(ct).ConfigureAwait(false);

                foreach (var a in allocs)
                {
                    a.Status = (int)AllocationStatus.Cancelled;
                    a.Notes = $"Cancelled via reservation {reservationId}: {cancelReason}";
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                return (true, $"Cancelled + {allocs.Count} allocations.");
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[MaterialReservation] Cancel failed for {reservationId}",
                    ex, new LogContext { Module = "Production", Class = nameof(MaterialReservationService), Operation = nameof(CancelAsync) });
                return (false, $"Cancel failed: {ex.Message}");
            }
        }

        public static async Task<List<SavedMaterialReservation>> GetByJobAsync(string jobRef, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobRef)) return new List<SavedMaterialReservation>();
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            return await db.Reservations
                .AsNoTracking()
                .Where(r => r.JobRef == jobRef)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(ct).ConfigureAwait(false);
        }

        public static async Task<Dictionary<string, int>> GetAvailableSummaryAsync(CancellationToken ct = default)
        {
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            var all = await db.Reservations.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);

            // Available = not Reserved/Allocated/Consumed? Actually per spec:
            // Stock 10, Reservation 3, Available 7, Allocated 3, Consumed 2, Released 1, Final Available 8 Consumed 2
            // So we summarize counts per status
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Available"] = 0, // calculated externally from SheetStore Balance - Reserved
                ["Reserved"] = all.Count(r => r.Status == (int)ReservationStatus.Reserved),
                ["Allocated"] = all.Count(r => r.Status == (int)ReservationStatus.Allocated),
                ["Consumed"] = all.Count(r => r.Status == (int)ReservationStatus.Consumed),
                ["Released"] = all.Count(r => r.Status == (int)ReservationStatus.Released),
                ["Cancelled"] = all.Count(r => r.Status == (int)ReservationStatus.Cancelled)
            };
            return dict;
        }
    }
}
