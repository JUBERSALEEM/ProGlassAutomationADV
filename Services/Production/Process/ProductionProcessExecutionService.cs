// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/Process/ProductionProcessExecutionService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 16 Shared Process Execution Service
//   One reusable framework, not eight separate architectures.
//   Preserves ExecutionId JobRef PI JO SpecNo Serial PieceId ProcessType Sequence
//   InputWorkItemId OutputWorkItemId Status StartedBy StartedAt CompletedBy
//   CompletedAt QtyRequired QtyDone EntryType Reason Notes QcStatus ParentExecutionId ReworkSequence
//   Use existing production identity conventions, deterministic ids.
//   Reuse existing WorkItemStatus 0 Pending 1 InProgress 2 Completed + additive 3 Paused 4 Rework 5 Rejected 6 Cancelled 7 QcPending 8 QcFailed.
//   Input/Output traceability Previous→Current→Next, preserve original piece identity,
//   deterministic child/output identity when transformation creates new entity.
//   Quantity/piece-flow semantics Required 10 Cutting 10 completed Tempering 8 completed 1 failed 1 pending → 10 total 8 passed 1 failed 1 remaining no destructive overwrite.
//   QC integration reuse Phase14 ProductionQcService, Breakage/Rework reuse ProductionBreakageService, FOC supported.
//   Performance async/await CancellationToken IProgress.
//   Idempotency deterministic identities JobRef|Spec|Serial|Stage|ExecutionSequence.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.Process;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production.Process
{
    public static class ProductionProcessExecutionService
    {
        public static string BuildExecutionId(string jobRef, int specGroup, int serial, ProductionProcessType processType, int sequence)
            => ProductionProcessRoutingService.BuildExecutionId(jobRef, specGroup, serial, processType, sequence);

        /// <summary>
        /// Start process execution — gating check predecessor must be complete, idempotent deterministic.
        /// </summary>
        public static async Task<(bool Ok, string Message, SavedProcessExecution? Execution)> StartAsync(
            string jobRef,
            int specGroupNumber,
            int serialNumber,
            string position,
            ProductionProcessType processType,
            int qtyRequired,
            string pieceId,
            int inputWorkItemId,
            int outputWorkItemId,
            string processDetailJson = "",
            bool isFoc = false,
            string notes = "",
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobRef)) return (false, "JobRef required.", null);
            ProductionRepository.EnsureInitialized();

            try
            {
                using var db = new ProductionDbContext();

                // Gating: check predecessor complete
                var allWorkItems = await db.WorkItems.AsNoTracking()
                    .Where(w => w.JobRef == jobRef && w.SpecGroupNumber == specGroupNumber && w.SerialNumber == serialNumber && !w.IsCancelled)
                    .ToListAsync(ct).ConfigureAwait(false);

                var route = ProductionProcessRoutingService.BuildRouteForWorkItem(
                    allWorkItems.FirstOrDefault(w => w.SpecGroupNumber == specGroupNumber && w.SerialNumber == serialNumber) ??
                    new SavedProductionWorkItem { JobRef = jobRef, SpecGroupNumber = specGroupNumber, SerialNumber = serialNumber, Position = position, Stage = (int)processType },
                    allWorkItems);

                // Build stage statuses from WorkItems QtyDone
                var stageStatuses = new Dictionary<ProductionProcessType, WorkItemStatus>();
                foreach (var wi in allWorkItems)
                {
                    var pt = (ProductionProcessType)wi.Stage;
                    var status = wi.QtyDone >= wi.Qty ? WorkItemStatus.Completed : wi.QtyDone > 0 ? WorkItemStatus.InProgress : WorkItemStatus.Pending;
                    if (wi.IsCancelled) status = WorkItemStatus.Cancelled;
                    stageStatuses[pt] = status;
                }

                if (!ProductionProcessRoutingService.CanBegin(processType, route, stageStatuses))
                {
                    var prev = route.PreviousRequiredBefore(processType);
                    return (false, $"Process {processType} cannot begin — predecessor {prev} not complete (gating).", null);
                }

                // Idempotency: check existing execution with same deterministic id sequence 0
                string executionId = BuildExecutionId(jobRef, specGroupNumber, serialNumber, processType, sequence: 0);

                var existing = await db.ProcessExecutions.FirstOrDefaultAsync(e => e.ExecutionId == executionId, ct).ConfigureAwait(false);
                if (existing != null)
                {
                    if (existing.Status == (int)WorkItemStatus.Completed)
                        return (true, "Already completed (idempotent).", existing);
                    if (existing.Status == (int)WorkItemStatus.InProgress)
                        return (true, "Already in progress (idempotent).", existing);

                    // Re-activate if paused/cancelled
                    existing.Status = (int)WorkItemStatus.InProgress;
                    existing.StartedBy = AuditService.CurrentUsername;
                    existing.StartedAt = DateTime.Now;
                    existing.UpdatedAt = DateTime.Now;
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    return (true, $"Resumed {processType} for {pieceId}", existing);
                }

                // Determine input/output piece ids — preserve original, deterministic child when transformation creates new entity
                string inputPieceId = pieceId;
                string outputPieceId = pieceId; // safe default — same PieceId preserved

                // For transformations that genuinely create new entity (e.g., Tempering creates tempered version, Pairing creates Pair-001)
                // Use deterministic child identity only when needed — for now preserve same, except Pairing/Doubling handled separately
                if (processType == ProductionProcessType.Tempering)
                {
                    // Tempering: input P001 → output P001-T01 if we want child, but spec says if safe preserve same PieceId
                    // We preserve same for traceability, but record PreviousStage/NextStage
                    outputPieceId = $"{pieceId}-T01"; // deterministic child — preserves original via InputPieceId
                }

                var prevStage = route.PreviousRequiredBefore(processType)?.ToString() ?? "";
                var nextStage = route.NextRequiredAfter(processType)?.ToString() ?? "";

                var execution = new SavedProcessExecution
                {
                    ExecutionId = executionId,
                    JobRef = jobRef,
                    SourceProformaId = allWorkItems.FirstOrDefault()?.SourceProformaId ?? "",
                    JobOrderId = jobRef,
                    SpecGroupNumber = specGroupNumber,
                    SpecName = allWorkItems.FirstOrDefault()?.SpecName ?? "",
                    SerialNumber = serialNumber,
                    Position = position,
                    PieceId = pieceId,
                    InputPieceId = inputPieceId,
                    OutputPieceId = outputPieceId,
                    ProcessType = (int)processType,
                    Sequence = 0,
                    InputWorkItemId = inputWorkItemId,
                    OutputWorkItemId = outputWorkItemId,
                    Status = (int)WorkItemStatus.InProgress,
                    StartedBy = AuditService.CurrentUsername,
                    StartedAt = DateTime.Now,
                    QtyRequired = qtyRequired,
                    QtyDone = 0,
                    EntryType = 0, // Normal
                    Notes = notes,
                    QcStatus = 0, // Pending
                    IsFoc = isFoc,
                    PreviousStage = prevStage,
                    NextStage = nextStage,
                    ProcessDetailJson = processDetailJson,
                    CreatedBy = AuditService.CurrentUsername,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                db.ProcessExecutions.Add(execution);

                // Update WorkItem status to InProgress if not already
                if (outputWorkItemId > 0)
                {
                    var wi = await db.WorkItems.FindAsync(new object[] { outputWorkItemId }, ct).ConfigureAwait(false);
                    if (wi != null && !wi.IsCancelled && wi.Status == (int)WorkItemStatus.Pending)
                    {
                        wi.Status = (int)WorkItemStatus.InProgress;
                        wi.UpdatedAt = DateTime.Now;
                    }
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                AppLogService.Info($"[ProcessExecution] Started {processType} {executionId} {pieceId}",
                    new LogContext { Module = "Production", Class = nameof(ProductionProcessExecutionService), Operation = nameof(StartAsync) });

                return (true, $"Started {processType} for {pieceId}", execution);
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[ProcessExecution] Start failed {jobRef} {specGroupNumber}/{serialNumber} {processType}",
                    ex, new LogContext { Module = "Production", Class = nameof(ProductionProcessExecutionService), Operation = nameof(StartAsync) });
                return (false, $"Start failed: {ex.Message}", null);
            }
        }

        public static async Task<(bool Ok, string Message)> PauseAsync(string executionId, string reason = "", CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(executionId)) return (false, "ExecutionId required.");
            ProductionRepository.EnsureInitialized();
            try
            {
                using var db = new ProductionDbContext();
                var exec = await db.ProcessExecutions.FirstOrDefaultAsync(e => e.ExecutionId == executionId, ct).ConfigureAwait(false);
                if (exec == null) return (false, "Execution not found.");
                if (exec.Status == (int)WorkItemStatus.Completed) return (false, "Already completed, cannot pause.");
                exec.Status = (int)WorkItemStatus.Paused;
                exec.Reason = reason;
                exec.UpdatedAt = DateTime.Now;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                return (true, $"Paused {exec.ProcessType} {executionId}");
            }
            catch (Exception ex)
            {
                return (false, $"Pause failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Complete process execution — piece-flow semantics Required 10 Cutting 10 completed Tempering 8 completed 1 failed 1 pending → 10 total 8 passed 1 failed 1 remaining no destructive overwrite.
        /// </summary>
        public static async Task<(bool Ok, string Message)> CompleteAsync(
            string executionId,
            int qtyDone,
            string notes = "",
            string processDetailJson = "",
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(executionId)) return (false, "ExecutionId required.");
            ProductionRepository.EnsureInitialized();

            try
            {
                using var db = new ProductionDbContext();
                var exec = await db.ProcessExecutions.FirstOrDefaultAsync(e => e.ExecutionId == executionId, ct).ConfigureAwait(false);
                if (exec == null) return (false, "Execution not found.");
                if (exec.Status == (int)WorkItemStatus.Completed) return (true, "Already completed (idempotent).");
                if (exec.Status == (int)WorkItemStatus.Cancelled) return (false, "Cancelled, cannot complete.");

                if (exec.OutputWorkItemId > 0)
                {
                    var linked = await db.WorkItems.FindAsync(new object[] { exec.OutputWorkItemId }, ct).ConfigureAwait(false);
                    if (linked != null)
                    {
                        if (linked.IsCancelled || linked.Status == (int)WorkItemStatus.Cancelled)
                            return (false, "Linked work item cancelled — cannot complete execution.");
                        if (linked.Status == (int)WorkItemStatus.Rejected)
                            return (false, "Linked work item rejected — cannot complete execution.");
                        if (linked.Status == (int)WorkItemStatus.QcFailed)
                            return (false, "Linked work item QC failed — cannot complete execution.");
                    }
                }

                // Piece-flow: QtyDone incremental, no destructive overwrite, clamp to QtyRequired
                int newDone = Math.Min(exec.QtyRequired, exec.QtyDone + qtyDone);
                exec.QtyDone = newDone;
                exec.Notes = string.IsNullOrWhiteSpace(notes) ? exec.Notes : notes;
                if (!string.IsNullOrWhiteSpace(processDetailJson))
                    exec.ProcessDetailJson = processDetailJson;

                exec.Status = newDone >= exec.QtyRequired ? (int)WorkItemStatus.Completed : (int)WorkItemStatus.InProgress;
                exec.UpdatedAt = DateTime.Now;

                if (exec.Status == (int)WorkItemStatus.Completed)
                {
                    exec.CompletedBy = AuditService.CurrentUsername;
                    exec.CompletedAt = DateTime.Now;
                }

                // ProcessExecution is SUPPORTING metadata. WorkItem.QtyDone is
                // authoritative and is written only by SetDoneQtyAsync /
                // ApplyDoneQtyOnTrackedItem. Completing an execution must not
                // independently increment the floor quantity.
                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                AppLogService.Info($"[ProcessExecution] Completed {exec.ProcessType} {executionId} QtyDone {newDone}/{exec.QtyRequired}",
                    new LogContext { Module = "Production", Class = nameof(ProductionProcessExecutionService), Operation = nameof(CompleteAsync) });

                return (true, $"Completed {((ProductionProcessType)exec.ProcessType)} {exec.PieceId} {newDone}/{exec.QtyRequired}");
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[ProcessExecution] Complete failed {executionId}",
                    ex, new LogContext { Module = "Production", Class = nameof(ProductionProcessExecutionService), Operation = nameof(CompleteAsync) });
                return (false, $"Complete failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Record breakage/rework during any process — reuse Phase14 ProductionBreakageService.
        /// Original → failure → Breakage/Rework → Replacement → required process again, do not reset original as if nothing happened.
        /// </summary>
        public static async Task<(bool Ok, string Message)> RecordBreakageAsync(
            string executionId,
            string reason,
            int entryType, // 1 Breakage, 2 Rework
            bool isFoc = false,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(executionId)) return (false, "ExecutionId required.");
            ProductionRepository.EnsureInitialized();

            try
            {
                using var db = new ProductionDbContext();
                var exec = await db.ProcessExecutions.FirstOrDefaultAsync(e => e.ExecutionId == executionId, ct).ConfigureAwait(false);
                if (exec == null) return (false, "Execution not found.");

                exec.Status = entryType == 1 ? (int)WorkItemStatus.Rejected : (int)WorkItemStatus.Rework;
                exec.EntryType = entryType;
                exec.Reason = reason;
                exec.IsFoc = isFoc;
                exec.UpdatedAt = DateTime.Now;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                // Reuse Phase14 breakage service for WorkItem replacement
                if (exec.OutputWorkItemId > 0)
                {
                    var (ok, breakageEntry, replacement, msg) = await ProductionBreakageService.RecordBreakageAsync(
                        exec.OutputWorkItemId, 1, reason, notes: $"Breakage during {((ProductionProcessType)exec.ProcessType)} {executionId}", isFoc: isFoc, ct: ct).ConfigureAwait(false);

                    if (ok)
                    {
                        // Create new execution for replacement — deterministic child
                        string newExecId = BuildExecutionId(exec.JobRef, exec.SpecGroupNumber, exec.SerialNumber, (ProductionProcessType)exec.ProcessType, exec.ReworkSequence + 1);
                        var newExec = new SavedProcessExecution
                        {
                            ExecutionId = newExecId,
                            JobRef = exec.JobRef,
                            SourceProformaId = exec.SourceProformaId,
                            JobOrderId = exec.JobOrderId,
                            SpecGroupNumber = exec.SpecGroupNumber,
                            SpecName = exec.SpecName,
                            SerialNumber = exec.SerialNumber,
                            Position = exec.Position,
                            PieceId = exec.PieceId, // preserve original
                            InputPieceId = exec.PieceId,
                            OutputPieceId = $"{exec.PieceId}-R{exec.ReworkSequence + 1:00}",
                            ProcessType = exec.ProcessType,
                            Sequence = exec.ReworkSequence + 1,
                            InputWorkItemId = exec.InputWorkItemId,
                            OutputWorkItemId = replacement?.ReplacementWorkItemId ?? 0,
                            Status = (int)WorkItemStatus.Pending,
                            QtyRequired = 1,
                            QtyDone = 0,
                            EntryType = 0,
                            ParentExecutionId = executionId,
                            ReworkSequence = exec.ReworkSequence + 1,
                            IsFoc = isFoc,
                            PreviousStage = exec.PreviousStage,
                            NextStage = exec.NextStage,
                            CreatedBy = AuditService.CurrentUsername,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now,
                            Notes = $"Replacement for breakage {executionId} → {replacement?.ReplacementId}"
                        };
                        db.ProcessExecutions.Add(newExec);
                        await db.SaveChangesAsync(ct).ConfigureAwait(false);

                        return (true, $"Breakage recorded: {msg} — replacement execution {newExecId} traceable Original {exec.PieceId} → Breakage {executionId} → Replacement {replacement?.ReplacementId}");
                    }
                    else
                    {
                        return (false, $"Breakage record failed: {msg}");
                    }
                }

                return (true, $"Breakage recorded for {exec.PieceId} — no source work item, manual replacement needed");
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[ProcessExecution] Breakage failed {executionId}",
                    ex, new LogContext { Module = "Production", Class = nameof(ProductionProcessExecutionService), Operation = nameof(RecordBreakageAsync) });
                return (false, $"Breakage failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get executions by job — for work center, reporting, gating.
        /// </summary>
        public static async Task<List<SavedProcessExecution>> GetByJobAsync(string jobRef, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobRef)) return new List<SavedProcessExecution>();
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            return await db.ProcessExecutions.AsNoTracking()
                .Where(e => e.JobRef == jobRef)
                .OrderBy(e => e.SpecGroupNumber)
                .ThenBy(e => e.SerialNumber)
                .ThenBy(e => e.ProcessType)
                .ThenBy(e => e.Sequence)
                .ToListAsync(ct).ConfigureAwait(false);
        }

        public static async Task<List<SavedProcessExecution>> GetByProcessAsync(ProductionProcessType processType, CancellationToken ct = default)
        {
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            return await db.ProcessExecutions.AsNoTracking()
                .Where(e => e.ProcessType == (int)processType)
                .OrderByDescending(e => e.CreatedAt)
                .Take(200)
                .ToListAsync(ct).ConfigureAwait(false);
        }

        public static async Task<(int Required, int Completed, int Remaining, int Failed, int Rework, int QcPending)> GetProgressByJobAsync(string jobRef, CancellationToken ct = default)
        {
            var execs = await GetByJobAsync(jobRef, ct).ConfigureAwait(false);
            int required = execs.Sum(e => e.QtyRequired);
            int completed = execs.Where(e => e.Status == (int)WorkItemStatus.Completed).Sum(e => e.QtyDone);
            int failed = execs.Where(e => e.Status == (int)WorkItemStatus.Rejected || e.Status == (int)WorkItemStatus.Cancelled).Sum(e => e.QtyRequired - e.QtyDone);
            int rework = execs.Where(e => e.Status == (int)WorkItemStatus.Rework).Count();
            int qcPending = execs.Where(e => e.QcStatus == 0 && e.Status == (int)WorkItemStatus.Completed).Count();
            int remaining = Math.Max(0, required - completed - failed);
            return (required, completed, remaining, failed, rework, qcPending);
        }
    }
}
