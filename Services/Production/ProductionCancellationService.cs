// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/ProductionCancellationService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 14 Cancellation (soft delete, preserve audit)
//   Cancellation is never physical delete — preserves who/when/reason,
//   affected work/reservation/allocation, history.
//   Material is released only when safe (not consumed).
//   Idempotent, auditable.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production
{
    public static class ProductionCancellationService
    {
        public static async Task<(bool Ok, string Message)> CancelWorkItemAsync(
            int workItemId, string reason, CancellationToken ct = default)
        {
            if (workItemId <= 0) return (false, "WorkItemId required.");
            if (string.IsNullOrWhiteSpace(reason)) reason = "Cancelled";

            ProductionRepository.EnsureInitialized();

            try
            {
                using var db = new ProductionDbContext();
                await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
                using var tx = await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, ct).ConfigureAwait(false);
                var wi = await db.WorkItems.FindAsync(new object[] { workItemId }, ct).ConfigureAwait(false);
                if (wi == null) return (false, "Work item not found.");

                if (wi.IsCancelled)
                    return (true, "Already cancelled (idempotent).");

                if (wi.Status == (int)WorkItemStatus.Completed && wi.QtyDone >= wi.Qty)
                {
                    // Allow cancellation but warn — completed work history preserved
                    AppLogService.Warning($"[Cancellation] Cancelling completed work item {workItemId}",
                        new LogContext { Module = "Production", Class = nameof(ProductionCancellationService), Operation = nameof(CancelWorkItemAsync) });
                }

                wi.IsCancelled = true;
                wi.CancelledBy = AuditService.CurrentUsername;
                wi.CancelledAt = DateTime.Now;
                wi.CancelReason = reason;
                wi.Status = (int)WorkItemStatus.Cancelled;
                wi.UpdatedAt = DateTime.Now;

                // Create audit entry
                var entry = new SavedProductionEntry
                {
                    WorkItemId = workItemId,
                    JobRef = wi.JobRef,
                    Stage = wi.Stage,
                    SpecGroupNumber = wi.SpecGroupNumber,
                    SerialNumber = wi.SerialNumber,
                    Position = wi.Position,
                    SpecName = wi.SpecName,
                    GlassRef = wi.GlassRef,
                    Qty = wi.Qty,
                    DoneBy = AuditService.CurrentUsername,
                    DoneAt = DateTime.Now,
                    EntryType = (int)ProductionEntryType.Cancelled,
                    Reason = reason,
                    Notes = $"Cancelled: {reason}"
                };
                db.Entries.Add(entry);

                // Release related reservations only when safe (not consumed)
                var reservations = await db.Reservations
                    .Where(r => r.JobRef == wi.JobRef &&
                                r.SpecGroupNumber == wi.SpecGroupNumber &&
                                r.SerialNumber == wi.SerialNumber)
                    .ToListAsync(ct).ConfigureAwait(false);

                int releasedCount = 0;
                foreach (var res in reservations)
                {
                    if (res.Status == (int)ReservationStatus.Consumed) continue;
                    if (res.Status == (int)ReservationStatus.Cancelled ||
                        res.Status == (int)ReservationStatus.Released) continue;

                    res.Status = (int)ReservationStatus.Released;
                    res.ReleasedAt = DateTime.Now;
                    res.ReleaseReason = $"Released via work item cancellation {workItemId}: {reason}";
                    releasedCount++;
                }

                // Cancel related allocations only when safe
                var allocs = await db.Allocations
                    .Where(a => a.JobRef == wi.JobRef &&
                                a.SpecGroupNumber == wi.SpecGroupNumber &&
                                a.SerialNumber == wi.SerialNumber &&
                                a.Status != (int)AllocationStatus.Consumed &&
                                a.Status != (int)AllocationStatus.Cancelled)
                    .ToListAsync(ct).ConfigureAwait(false);

                foreach (var a in allocs)
                {
                    a.Status = (int)AllocationStatus.Cancelled;
                    a.Notes = $"Cancelled via work item {workItemId}: {reason}";
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                AppLogService.Info($"[Cancellation] WorkItem {workItemId} cancelled, {releasedCount} reservations released, {allocs.Count} allocations cancelled",
                    new LogContext { Module = "Production", Class = nameof(ProductionCancellationService), Operation = nameof(CancelWorkItemAsync) });

                return (true, $"Cancelled work item + released {releasedCount} reservations + cancelled {allocs.Count} allocations (soft, audit preserved).");
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[Cancellation] Cancel failed for WorkItem {workItemId}",
                    ex, new LogContext { Module = "Production", Class = nameof(ProductionCancellationService), Operation = nameof(CancelWorkItemAsync) });
                return (false, $"Cancel failed: {ex.Message}");
            }
        }

        public static async Task<(bool Ok, string Message)> CancelJobAsync(
            string jobRef, string reason, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobRef)) return (false, "JobRef required.");
            ProductionRepository.EnsureInitialized();

            try
            {
                using var db = new ProductionDbContext();
                var workItems = await db.WorkItems
                    .Where(w => w.JobRef == jobRef && !w.IsCancelled)
                    .ToListAsync(ct).ConfigureAwait(false);

                if (workItems.Count == 0) return (false, $"No active work items for job {jobRef}.");

                int cancelled = 0;
                foreach (var wi in workItems)
                {
                    ct.ThrowIfCancellationRequested();
                    var (ok, _) = await CancelWorkItemAsync(wi.Id, reason, ct).ConfigureAwait(false);
                    if (ok) cancelled++;
                }

                return (true, $"Cancelled {cancelled}/{workItems.Count} work items for job {jobRef} (soft, audit preserved).");
            }
            catch (OperationCanceledException)
            {
                return (false, "Job cancellation cancelled.");
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[Cancellation] Cancel job failed for {jobRef}",
                    ex, new LogContext { Module = "Production", Class = nameof(ProductionCancellationService), Operation = nameof(CancelJobAsync) });
                return (false, $"Cancel job failed: {ex.Message}");
            }
        }
    }
}
