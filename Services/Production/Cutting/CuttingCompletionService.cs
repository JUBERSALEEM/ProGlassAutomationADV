// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/Cutting/CuttingCompletionService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 15 Cutting Completion
//   Implements actual completion of cutting work items, partial production,
//   breakage during cutting, FOC during cutting, QC integration.
//   Uses existing WorkItemStatus, QtyDone, ProductionHistory, allocation status.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production.Cutting
{
    public static class CuttingCompletionService
    {
        /// <summary>
        /// Complete cutting for a placed piece — updates WorkItem QtyDone, piece status, allocation status.
        /// Supports partial completion: required 10, completed 6, remaining 4 preserved.
        /// </summary>
        public static async Task<(bool Ok, string Message)> CompletePieceAsync(
            string placedPieceId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(placedPieceId)) return (false, "PlacedPieceId required.");

            ProductionRepository.EnsureInitialized();

            try
            {
                using var db = new ProductionDbContext();
                // Same SQLite BEGIN IMMEDIATE as reservation/allocation writes.
                // Without it, Complete can overwrite a concurrent Release/Cancel
                // (Released → Consumed) because this path used to SaveChanges
                // outside a hold lock.
                await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
                using var tx = await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, ct).ConfigureAwait(false);

                var placed = await db.CuttingPlacedPieces
                    .FirstOrDefaultAsync(p => p.PlacedPieceId == placedPieceId, ct).ConfigureAwait(false);

                if (placed == null) return (false, "Placed piece not found.");

                if (placed.Status == (int)CuttingPieceStatus.Completed)
                    return (true, "Already completed (idempotent).");

                if (placed.Status == (int)CuttingPieceStatus.Cancelled)
                    return (false, "Cannot complete cancelled piece.");

                // Update placed piece status
                placed.Status = (int)CuttingPieceStatus.Completed;

                // Update source work item QtyDone — partial production preserved
                if (placed.SourceWorkItemId > 0)
                {
                    var workItem = await db.WorkItems.FindAsync(new object[] { placed.SourceWorkItemId }, ct).ConfigureAwait(false);
                    if (workItem != null && !workItem.IsCancelled)
                    {
                        // QtyDone piece-flow: increment by 1 for this piece
                        // Do NOT mark entire job complete when only one piece complete
                        int newDone = Math.Min(workItem.Qty, workItem.QtyDone + 1);
                        var apply = ProductionRepository.ApplyDoneQtyOnTrackedItem(
                            db, workItem, newDone, inflow: workItem.Qty,
                            notes: $"Cutting completed piece {placed.PieceLabel} at {placed.X},{placed.Y} {(placed.IsRotated ? "90°" : "0°")}");
                        if (!apply.Ok)
                            return (false, apply.Message);
                    }
                }

                // Planning consume only — does not call Sheet Store UseStock.
                // Consumed here means the production hold is finished, not
                // that a sheet left Sheet Store.
                if (!string.IsNullOrWhiteSpace(placed.AllocationId))
                {
                    var allocation = await db.Allocations
                        .FirstOrDefaultAsync(a => a.AllocationId == placed.AllocationId, ct).ConfigureAwait(false);
                    if (allocation != null)
                    {
                        if (allocation.Status == (int)AllocationStatus.Cancelled ||
                            allocation.Status == (int)AllocationStatus.Released)
                            return (false, $"Cannot consume {((AllocationStatus)allocation.Status)} allocation.");

                        if (allocation.Status != (int)AllocationStatus.Consumed)
                        {
                            allocation.Status = (int)AllocationStatus.Consumed;
                            allocation.ConsumedAt = DateTime.Now;
                            allocation.Notes = $"Consumed via cutting completion {placedPieceId}";
                        }

                        // If all allocations for reservation consumed, mark reservation consumed
                        var reservationId = allocation.ReservationId;
                        var allAllocs = await db.Allocations
                            .Where(a => a.ReservationId == reservationId)
                            .ToListAsync(ct).ConfigureAwait(false);

                        bool allConsumed = allAllocs.All(a =>
                            a.AllocationId == placed.AllocationId
                            || a.Status == (int)AllocationStatus.Consumed);
                        if (allConsumed)
                        {
                            var reservation = await db.Reservations
                                .FirstOrDefaultAsync(r => r.ReservationId == reservationId, ct).ConfigureAwait(false);
                            // Match ConsumeAsync: never overwrite Released/Cancelled.
                            if (reservation != null
                                && reservation.Status != (int)ReservationStatus.Consumed
                                && reservation.Status != (int)ReservationStatus.Released
                                && reservation.Status != (int)ReservationStatus.Cancelled)
                            {
                                reservation.Status = (int)ReservationStatus.Consumed;
                            }
                        }
                    }
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);

                AppLogService.Info($"[CuttingCompletion] Completed piece {placedPieceId} {placed.PieceLabel}",
                    new LogContext { Module = "Production", Class = nameof(CuttingCompletionService), Operation = nameof(CompletePieceAsync) });

                return (true, $"Completed {placed.PieceLabel}");
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[CuttingCompletion] Complete failed for {placedPieceId}",
                    ex, new LogContext { Module = "Production", Class = nameof(CuttingCompletionService), Operation = nameof(CompletePieceAsync) });
                return (false, $"Complete failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Record breakage during cutting — integrates Phase14 breakage service.
        /// Example: 10 required, 8 cut successfully, 1 breaks, 1 remaining → record breakage, create replacement.
        /// Do NOT silently increase original quantity.
        /// </summary>
        public static async Task<(bool Ok, string Message)> RecordBreakageDuringCuttingAsync(
            string placedPieceId,
            string reason,
            bool isFoc = false,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(placedPieceId)) return (false, "PlacedPieceId required.");

            ProductionRepository.EnsureInitialized();

            try
            {
                using var db = new ProductionDbContext();
                var placed = await db.CuttingPlacedPieces
                    .FirstOrDefaultAsync(p => p.PlacedPieceId == placedPieceId, ct).ConfigureAwait(false);

                if (placed == null) return (false, "Placed piece not found.");

                // Update placed piece status to Breakage
                placed.Status = isFoc ? (int)CuttingPieceStatus.Foc : (int)CuttingPieceStatus.Breakage;
                placed.Notes = $"Breakage during cutting: {reason} IsFoc={isFoc}";
                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                // Use Phase14 breakage service to create replacement work item
                if (placed.SourceWorkItemId > 0)
                {
                    var (ok, breakageEntry, replacement, message) = await ProductionBreakageService
                        .RecordBreakageAsync(placed.SourceWorkItemId, 1, reason, notes: $"Breakage during cutting {placedPieceId}", isFoc: isFoc, ct: ct)
                        .ConfigureAwait(false);

                    if (ok)
                    {
                        // Create new cutting requirement for replacement? For Phase15, we preserve traceability:
                        // Original Piece → Breakage → Replacement → Cutting → QC
                        // The replacement work item will be picked up in next cutting workflow run
                        return (true, $"Breakage recorded: {message} — replacement traceable Original {placed.PieceId} → Replacement {replacement?.ReplacementId}");
                    }
                    else
                    {
                        return (false, $"Breakage record failed: {message}");
                    }
                }

                return (true, $"Breakage recorded for {placed.PieceLabel} — no source work item, manual replacement needed");
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[CuttingCompletion] Breakage failed for {placedPieceId}",
                    ex, new LogContext { Module = "Production", Class = nameof(CuttingCompletionService), Operation = nameof(RecordBreakageDuringCuttingAsync) });
                return (false, $"Breakage failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get cutting progress for a job — required/completed/remaining preserved, no destructive overwrite.
        /// </summary>
        public static async Task<(int Required, int Completed, int Remaining, int Breakage, int Foc, int Rework, int QcPending)> GetProgressByJobAsync(
            string jobRef, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobRef)) return (0, 0, 0, 0, 0, 0, 0);

            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();

            var placed = await db.CuttingPlacedPieces.AsNoTracking()
                .Where(p => p.JobRef == jobRef)
                .ToListAsync(ct).ConfigureAwait(false);

            int required = placed.Count;
            int completed = placed.Count(p => p.Status == (int)CuttingPieceStatus.Completed);
            int breakage = placed.Count(p => p.Status == (int)CuttingPieceStatus.Breakage);
            int foc = placed.Count(p => p.Status == (int)CuttingPieceStatus.Foc);
            int rework = placed.Count(p => p.Status == (int)CuttingPieceStatus.Rework);
            int qcPending = placed.Count(p => p.Status == (int)CuttingPieceStatus.QcPending);
            int remaining = required - completed - breakage - foc;

            return (required, completed, Math.Max(0, remaining), breakage, foc, rework, qcPending);
        }

        /// <summary>
        /// Complete all pieces for a cutting session — batch, supports partial.
        /// </summary>
        public static async Task<(bool Ok, string Message)> CompleteSessionAsync(
            string cuttingSessionId,
            IProgress<string>? progress = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(cuttingSessionId)) return (false, "SessionId required.");

            ProductionRepository.EnsureInitialized();

            try
            {
                using var db = new ProductionDbContext();
                var session = await db.CuttingSessions
                    .FirstOrDefaultAsync(c => c.CuttingSessionId == cuttingSessionId, ct).ConfigureAwait(false);

                if (session == null) return (false, "Cutting session not found.");

                var placed = await db.CuttingPlacedPieces
                    .Where(p => p.CuttingSessionId == cuttingSessionId && p.Status == (int)CuttingPieceStatus.Allocated)
                    .ToListAsync(ct).ConfigureAwait(false);

                int completed = 0;
                foreach (var p in placed)
                {
                    ct.ThrowIfCancellationRequested();
                    var (ok, msg) = await CompletePieceAsync(p.PlacedPieceId, ct).ConfigureAwait(false);
                    if (ok) completed++;
                    progress?.Report($"Completed {completed}/{placed.Count}: {p.PieceLabel}");
                }

                session.Status = (int)CuttingSessionStatus.Completed;
                session.CompletedAt = DateTime.Now;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                return (true, $"Completed {completed}/{placed.Count} pieces for session {cuttingSessionId}");
            }
            catch (OperationCanceledException)
            {
                return (false, "Completion cancelled.");
            }
            catch (Exception ex)
            {
                return (false, $"Completion failed: {ex.Message}");
            }
        }
    }
}
