// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/Control/ProductionControlService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 17 Production Control Service
//   Controlled state transitions: Ready → Start → InProgress → Pause → Paused → Resume → InProgress → Complete
//   Also Ready → Hold → On Hold → Release Hold → Ready
//   Reuse existing WorkItemStatus architecture, Paused=3 preserved.
//   Hold system records Hold reason HeldBy HeldAt ReleasedBy ReleasedAt Notes, do not delete history when released.
//   Operator/Workstation assignment where architecture permits, preserve AssignedBy AssignedAt AssignedUser.
//   Priority Normal High Urgent deterministic, influences queue ordering, must NOT bypass safety dependencies.
//   Idempotency deterministic identities, no GUID alone, no duplicate assignments/holds/status transitions.
//   Concurrency tolerate two users same WorkItem second safely fail or no-op no duplicate execution no silent overwrite.
//   Audit every control action via existing audit system, no second audit DB.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.Control;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production.Control
{
    public static class ProductionControlService
    {
        // ── Deterministic IDs ────────────────────────────────────────────
        public static string BuildHoldId(string jobRef, int specGroup, int serial, int stage, int seq) => $"{(jobRef ?? "").Trim()}|{specGroup}|{serial}|{stage}|HOLD:{seq:000}";
        public static string BuildAssignmentId(string jobRef, int specGroup, int serial, int stage, int seq) => $"{(jobRef ?? "").Trim()}|{specGroup}|{serial}|{stage}|ASSIGN:{seq:000}";
        public static string BuildControlId(string jobRef, int specGroup, int serial, int stage) => $"{(jobRef ?? "").Trim()}|{specGroup}|{serial}|{stage}|CTRL";

        // ── Start ────────────────────────────────────────────────────────
        public static async Task<(bool Ok, string Message)> StartAsync(int workItemId, string? operatorName = null, CancellationToken ct = default)
        {
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();

            var wi = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == workItemId, ct).ConfigureAwait(false);
            if (wi == null) return (false, "WorkItem not found");
            if (wi.IsCancelled) return (false, $"WorkItem cancelled: {wi.CancelReason}");
            if (wi.Status == (int)WorkItemStatus.InProgress) return (true, "Already in progress — idempotent no-op");
            if (wi.Status == (int)WorkItemStatus.Completed) return (false, "WorkItem already completed");
            if (wi.Status == (int)WorkItemStatus.Paused) return (false, "WorkItem is paused — use Resume");
            if (wi.Status == (int)WorkItemStatus.Rejected) return (false, "Rejected work cannot start");
            if (wi.Status == (int)WorkItemStatus.QcFailed) return (false, "QC failed — cannot start");

            // Check hold
            var activeHold = await db.ControlHolds.FirstOrDefaultAsync(h => h.WorkItemId == workItemId && !h.IsReleased, ct).ConfigureAwait(false);
            if (activeHold != null) return (false, $"On Hold: {activeHold.HoldReason} by {activeHold.HeldBy}");

            // Readiness check — must be READY
            var allForJob = await db.WorkItems.AsNoTracking().Where(w => w.JobRef == wi.JobRef).ToListAsync(ct).ConfigureAwait(false);
            var allForUnit = allForJob.Where(w => w.SpecGroupNumber == wi.SpecGroupNumber && w.SerialNumber == wi.SerialNumber).ToList();
            var qc = await db.QcEntries.AsNoTracking().Where(q => q.JobRef == wi.JobRef).ToListAsync(ct).ConfigureAwait(false);
            var qcDict = qc.GroupBy(q => (q.SpecGroupNumber, q.SerialNumber, q.Position, q.Stage)).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.QcSequence).First().Result);
            var pairings = await db.ProcessPairings.AsNoTracking().Where(p => p.JobRef == wi.JobRef).ToListAsync(ct).ConfigureAwait(false);
            var assemblies = await db.ProcessAssemblies.AsNoTracking().Where(a => a.JobRef == wi.JobRef).ToListAsync(ct).ConfigureAwait(false);
            var pairingComplete = allForJob
                .Where(w => w.Stage == (int)ProductionStage.Pairing && !w.IsCancelled)
                .GroupBy(w => (w.SpecGroupNumber, w.SerialNumber))
                .ToDictionary(g => g.Key, g => g.Any(x => x.QtyDone >= x.Qty));
            var assemblyComplete = allForJob
                .Where(w => w.Stage == (int)ProductionStage.DoublingLamination && !w.IsCancelled)
                .GroupBy(w => (w.SpecGroupNumber, w.SerialNumber))
                .ToDictionary(g => g.Key, g => g.Any(x => x.QtyDone >= x.Qty));
            var reservations = await db.Reservations.AsNoTracking().Where(r => r.JobRef == wi.JobRef).ToListAsync(ct).ConfigureAwait(false);
            var allocations = await db.Allocations.AsNoTracking().Where(a => a.JobRef == wi.JobRef).ToListAsync(ct).ConfigureAwait(false);
            bool hasRes = wi.Stage == (int)ProductionStage.Cutting
                ? reservations.Any()
                : reservations.Any(r => r.SpecGroupNumber == wi.SpecGroupNumber && r.SerialNumber == wi.SerialNumber);
            bool hasAlloc = wi.Stage == (int)ProductionStage.Cutting
                ? allocations.Any()
                : allocations.Any(a => a.SpecGroupNumber == wi.SpecGroupNumber && a.SerialNumber == wi.SerialNumber);

            var (readiness, blockReason) = ProductionReadinessEngine.Calculate(wi, allForJob, allForUnit, qcDict, pairingComplete, assemblyComplete, hasRes, hasAlloc, false, "");

            if (readiness != ProductionReadiness.Ready)
                return (false, $"Not ready: {blockReason} — {readiness}");

            // Concurrency: use transaction, check again before update
            await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
            using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
            try
            {
                // Re-fetch with lock
                var fresh = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == workItemId, ct).ConfigureAwait(false);
                if (fresh == null) return (false, "WorkItem not found");
                if (fresh.Status == (int)WorkItemStatus.InProgress) { await tx.CommitAsync(ct).ConfigureAwait(false); return (true, "Already in progress"); }

                fresh.Status = (int)WorkItemStatus.InProgress;
                fresh.UpdatedAt = DateTime.Now;

                db.Entries.Add(new SavedProductionEntry
                {
                    WorkItemId = fresh.Id,
                    JobRef = fresh.JobRef,
                    Stage = fresh.Stage,
                    SpecGroupNumber = fresh.SpecGroupNumber,
                    SerialNumber = fresh.SerialNumber,
                    Position = fresh.Position,
                    SpecName = fresh.SpecName,
                    GlassRef = fresh.GlassRef,
                    Qty = fresh.QtyDone,
                    DoneBy = AuditService.CurrentUsername,
                    DoneAt = DateTime.Now,
                    EntryType = 0,
                    Reason = $"Start by {operatorName ?? AuditService.CurrentUsername}",
                    Notes = $"Production Control Start — Operator {operatorName ?? AuditService.CurrentUsername}"
                });

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await tx.CommitAsync(ct).ConfigureAwait(false);

                AppLogService.Info($"[ProductionControl] Start WorkItem {workItemId} Job {wi.JobRef} by {AuditService.CurrentUsername}", new LogContext { Module = "Production", Operation = "Start" });
                return (true, "Started");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                AppLogService.Error($"[ProductionControl] Start failed {workItemId}: {ex.Message}", ex);
                return (false, $"Start failed: {ex.Message}");
            }
        }

        // ── Pause ────────────────────────────────────────────────────────
        public static async Task<(bool Ok, string Message)> PauseAsync(int workItemId, string reason, CancellationToken ct = default)
        {
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            var wi = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == workItemId, ct).ConfigureAwait(false);
            if (wi == null) return (false, "WorkItem not found");
            if (wi.Status == (int)WorkItemStatus.Paused) return (true, "Already paused — idempotent");
            if (wi.Status != (int)WorkItemStatus.InProgress) return (false, "Only InProgress can be paused");

            wi.Status = (int)WorkItemStatus.Paused;
            wi.UpdatedAt = DateTime.Now;

            db.Entries.Add(new SavedProductionEntry
            {
                WorkItemId = wi.Id,
                JobRef = wi.JobRef,
                Stage = wi.Stage,
                SpecGroupNumber = wi.SpecGroupNumber,
                SerialNumber = wi.SerialNumber,
                Position = wi.Position,
                SpecName = wi.SpecName,
                GlassRef = wi.GlassRef,
                Qty = wi.QtyDone,
                DoneBy = AuditService.CurrentUsername,
                DoneAt = DateTime.Now,
                EntryType = 0,
                Reason = reason ?? "Pause",
                Notes = $"Production Control Pause — {reason}"
            });

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            AppLogService.Info($"[ProductionControl] Pause {workItemId} Reason {reason} by {AuditService.CurrentUsername}");
            return (true, "Paused");
        }

        // ── Resume ───────────────────────────────────────────────────────
        public static async Task<(bool Ok, string Message)> ResumeAsync(int workItemId, CancellationToken ct = default)
        {
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            var wi = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == workItemId, ct).ConfigureAwait(false);
            if (wi == null) return (false, "WorkItem not found");
            if (wi.Status == (int)WorkItemStatus.InProgress) return (true, "Already in progress — idempotent");
            if (wi.Status != (int)WorkItemStatus.Paused) return (false, "Only Paused can be resumed");

            wi.Status = (int)WorkItemStatus.InProgress;
            wi.UpdatedAt = DateTime.Now;

            db.Entries.Add(new SavedProductionEntry
            {
                WorkItemId = wi.Id,
                JobRef = wi.JobRef,
                Stage = wi.Stage,
                SpecGroupNumber = wi.SpecGroupNumber,
                SerialNumber = wi.SerialNumber,
                Position = wi.Position,
                SpecName = wi.SpecName,
                GlassRef = wi.GlassRef,
                Qty = wi.QtyDone,
                DoneBy = AuditService.CurrentUsername,
                DoneAt = DateTime.Now,
                EntryType = 0,
                Reason = "Resume",
                Notes = $"Production Control Resume by {AuditService.CurrentUsername}"
            });

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return (true, "Resumed");
        }

        // ── Hold ─────────────────────────────────────────────────────────
        public static async Task<(bool Ok, string Message, string HoldId)> HoldAsync(int workItemId, string holdReason, string notes, CancellationToken ct = default)
        {
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            var wi = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == workItemId, ct).ConfigureAwait(false);
            if (wi == null) return (false, "WorkItem not found", "");
            if (wi.IsCancelled) return (false, "Cancelled work cannot be held", "");

            var existingHold = await db.ControlHolds.FirstOrDefaultAsync(h => h.WorkItemId == workItemId && !h.IsReleased, ct).ConfigureAwait(false);
            if (existingHold != null) return (true, $"Already on hold: {existingHold.HoldReason} — idempotent", existingHold.HoldId);

            var seq = await db.ControlHolds.CountAsync(h => h.WorkItemId == workItemId, ct).ConfigureAwait(false);
            var holdId = BuildHoldId(wi.JobRef, wi.SpecGroupNumber, wi.SerialNumber, wi.Stage, seq);

            var hold = new SavedProductionControlHold
            {
                HoldId = holdId,
                WorkItemId = wi.Id,
                JobRef = wi.JobRef,
                SpecGroupNumber = wi.SpecGroupNumber,
                SerialNumber = wi.SerialNumber,
                Position = wi.Position,
                Stage = wi.Stage,
                HoldReason = holdReason ?? "Supervisor hold",
                HeldBy = AuditService.CurrentUsername,
                HeldAt = DateTime.Now,
                IsReleased = false,
                Notes = notes ?? "",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            db.ControlHolds.Add(hold);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            db.Entries.Add(new SavedProductionEntry
            {
                WorkItemId = wi.Id,
                JobRef = wi.JobRef,
                Stage = wi.Stage,
                SpecGroupNumber = wi.SpecGroupNumber,
                SerialNumber = wi.SerialNumber,
                Position = wi.Position,
                SpecName = wi.SpecName,
                GlassRef = wi.GlassRef,
                Qty = wi.QtyDone,
                DoneBy = AuditService.CurrentUsername,
                DoneAt = DateTime.Now,
                EntryType = 0,
                Reason = $"Hold: {holdReason}",
                Notes = notes ?? ""
            });
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            AppLogService.Info($"[ProductionControl] Hold {workItemId} Reason {holdReason} by {AuditService.CurrentUsername}");
            return (true, "On Hold", holdId);
        }

        public static async Task<(bool Ok, string Message)> ReleaseHoldAsync(string holdId, string notes, CancellationToken ct = default)
        {
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            var hold = await db.ControlHolds.FirstOrDefaultAsync(h => h.HoldId == holdId, ct).ConfigureAwait(false);
            if (hold == null) return (false, "Hold not found");
            if (hold.IsReleased) return (true, "Already released — idempotent");

            hold.IsReleased = true;
            hold.ReleasedBy = AuditService.CurrentUsername;
            hold.ReleasedAt = DateTime.Now;
            hold.UpdatedAt = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(notes)) hold.Notes += $" | Release: {notes}";

            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            var wi = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == hold.WorkItemId, ct).ConfigureAwait(false);
            if (wi != null)
            {
                db.Entries.Add(new SavedProductionEntry
                {
                    WorkItemId = wi.Id,
                    JobRef = wi.JobRef,
                    Stage = wi.Stage,
                    SpecGroupNumber = wi.SpecGroupNumber,
                    SerialNumber = wi.SerialNumber,
                    Position = wi.Position,
                    SpecName = wi.SpecName,
                    GlassRef = wi.GlassRef,
                    Qty = wi.QtyDone,
                    DoneBy = AuditService.CurrentUsername,
                    DoneAt = DateTime.Now,
                    EntryType = 0,
                    Reason = $"Release Hold: {hold.HoldReason}",
                    Notes = notes ?? ""
                });
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            return (true, "Hold Released");
        }

        // ── Assignment ───────────────────────────────────────────────────
        public static async Task<(bool Ok, string Message, string AssignmentId)> AssignAsync(int workItemId, string workstationId, string operatorName, string notes, CancellationToken ct = default)
        {
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            var wi = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == workItemId, ct).ConfigureAwait(false);
            if (wi == null) return (false, "WorkItem not found", "");

            // Idempotency: if already assigned same workstation+operator active, return existing
            var existing = await db.ControlAssignments.FirstOrDefaultAsync(a => a.WorkItemId == workItemId && a.IsActive && a.WorkstationId == (workstationId ?? "") && a.Operator == (operatorName ?? ""), ct).ConfigureAwait(false);
            if (existing != null) return (true, "Already assigned — idempotent", existing.AssignmentId);

            // Deactivate previous assignments for same work item (preserve history, not delete)
            var prevActives = await db.ControlAssignments.Where(a => a.WorkItemId == workItemId && a.IsActive).ToListAsync(ct).ConfigureAwait(false);
            foreach (var prev in prevActives)
            {
                prev.IsActive = false;
                prev.UnassignedBy = AuditService.CurrentUsername;
                prev.UnassignedAt = DateTime.Now;
                prev.UpdatedAt = DateTime.Now;
            }

            var seq = await db.ControlAssignments.CountAsync(a => a.WorkItemId == workItemId, ct).ConfigureAwait(false);
            var assignmentId = BuildAssignmentId(wi.JobRef, wi.SpecGroupNumber, wi.SerialNumber, wi.Stage, seq);

            var assignment = new SavedProductionControlAssignment
            {
                AssignmentId = assignmentId,
                WorkItemId = wi.Id,
                JobRef = wi.JobRef,
                SpecGroupNumber = wi.SpecGroupNumber,
                SerialNumber = wi.SerialNumber,
                Position = wi.Position,
                Stage = wi.Stage,
                WorkstationId = workstationId ?? "",
                Operator = operatorName ?? "",
                AssignedBy = AuditService.CurrentUsername,
                AssignedAt = DateTime.Now,
                IsActive = true,
                Notes = notes ?? "",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            db.ControlAssignments.Add(assignment);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            AppLogService.Info($"[ProductionControl] Assign {workItemId} WS {workstationId} Operator {operatorName} by {AuditService.CurrentUsername}");
            return (true, "Assigned", assignmentId);
        }

        public static async Task<(bool Ok, string Message)> UnassignAsync(int workItemId, CancellationToken ct = default)
        {
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            var actives = await db.ControlAssignments.Where(a => a.WorkItemId == workItemId && a.IsActive).ToListAsync(ct).ConfigureAwait(false);
            if (actives.Count == 0) return (true, "No active assignment — idempotent");

            foreach (var a in actives)
            {
                a.IsActive = false;
                a.UnassignedBy = AuditService.CurrentUsername;
                a.UnassignedAt = DateTime.Now;
                a.UpdatedAt = DateTime.Now;
            }
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return (true, "Unassigned");
        }

        // ── Priority ─────────────────────────────────────────────────────
        public static async Task<(bool Ok, string Message)> SetPriorityAsync(int workItemId, ProductionPriority priority, string notes, CancellationToken ct = default)
        {
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            var wi = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == workItemId, ct).ConfigureAwait(false);
            if (wi == null) return (false, "WorkItem not found");

            var controlId = BuildControlId(wi.JobRef, wi.SpecGroupNumber, wi.SerialNumber, wi.Stage);
            var existing = await db.ControlOverrides.FirstOrDefaultAsync(c => c.ControlId == controlId, ct).ConfigureAwait(false);

            if (existing == null)
            {
                existing = new SavedProductionControlOverride
                {
                    ControlId = controlId,
                    WorkItemId = wi.Id,
                    JobRef = wi.JobRef,
                    SpecGroupNumber = wi.SpecGroupNumber,
                    SerialNumber = wi.SerialNumber,
                    Position = wi.Position,
                    Stage = wi.Stage,
                    Priority = (int)priority,
                    PrioritySetBy = AuditService.CurrentUsername,
                    PrioritySetAt = DateTime.Now,
                    Notes = notes ?? "",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                db.ControlOverrides.Add(existing);
            }
            else
            {
                // Idempotent if same priority
                if (existing.Priority == (int)priority) return (true, "Priority already set — idempotent");
                existing.Priority = (int)priority;
                existing.PrioritySetBy = AuditService.CurrentUsername;
                existing.PrioritySetAt = DateTime.Now;
                existing.UpdatedAt = DateTime.Now;
                if (!string.IsNullOrWhiteSpace(notes)) existing.Notes = notes;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            AppLogService.Info($"[ProductionControl] Priority {workItemId} -> {priority} by {AuditService.CurrentUsername}");
            return (true, $"Priority set to {priority}");
        }

        // ── Due Date ─────────────────────────────────────────────────────
        public static async Task<(bool Ok, string Message)> SetDueDateAsync(int workItemId, DateTime? dueDate, string notes, CancellationToken ct = default)
        {
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            var wi = await db.WorkItems.FirstOrDefaultAsync(w => w.Id == workItemId, ct).ConfigureAwait(false);
            if (wi == null) return (false, "WorkItem not found");

            var controlId = BuildControlId(wi.JobRef, wi.SpecGroupNumber, wi.SerialNumber, wi.Stage);
            var existing = await db.ControlOverrides.FirstOrDefaultAsync(c => c.ControlId == controlId, ct).ConfigureAwait(false);

            if (existing == null)
            {
                existing = new SavedProductionControlOverride
                {
                    ControlId = controlId,
                    WorkItemId = wi.Id,
                    JobRef = wi.JobRef,
                    SpecGroupNumber = wi.SpecGroupNumber,
                    SerialNumber = wi.SerialNumber,
                    Position = wi.Position,
                    Stage = wi.Stage,
                    Priority = 0,
                    DueDate = dueDate,
                    PrioritySetBy = AuditService.CurrentUsername,
                    PrioritySetAt = DateTime.Now,
                    Notes = notes ?? "",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                db.ControlOverrides.Add(existing);
            }
            else
            {
                existing.DueDate = dueDate;
                existing.UpdatedAt = DateTime.Now;
                existing.PrioritySetBy = AuditService.CurrentUsername;
                existing.PrioritySetAt = DateTime.Now;
                if (!string.IsNullOrWhiteSpace(notes)) existing.Notes = notes;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return (true, dueDate.HasValue ? $"Due date set to {dueDate:yyyy-MM-dd}" : "Due date cleared");
        }

        // ── Complete (reuse existing piece-flow) ─────────────────────────
        public static async Task<(bool Ok, int AppliedQty, string Message)> CompleteAsync(int workItemId, int qty, CancellationToken ct = default)
        {
            // Reuse ProductionRepository.SetDoneQtyAsync which already has clamping and audit
            var result = await ProductionRepository.SetDoneQtyAsync(workItemId, qty).ConfigureAwait(false);
            return result;
        }
    }
}
