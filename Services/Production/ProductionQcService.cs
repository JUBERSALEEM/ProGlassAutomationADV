// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/ProductionQcService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 14 QC foundation
//   Minimal but production-grade QC state flow:
//   Production → QC Pending → QC Passed → QC Failed → Rework → Ready
//   Preserves Work Item, Piece, Stage, Result, Reason, Inspector, Timestamp, Notes
//   QC failure must not destroy completed production history.
//   Idempotent via QcId deterministic.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production;
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
    public static class ProductionQcService
    {
        public static string BuildQcId(
            string jobRef, int specGroup, int serial, int stage, int sequence)
        {
            jobRef = (jobRef ?? "").Trim();
            return $"{jobRef}|{specGroup}|{serial}|{stage}|QC:{sequence}";
        }

        public static async Task<(bool Ok, SavedProductionQcEntry? QcEntry, string Message)>
            RecordQcAsync(
                int workItemId,
                QcResult result,
                string reason,
                string? notes = null,
                string? parentQcId = null,
                bool isRework = false,
                CancellationToken ct = default)
        {
            if (workItemId <= 0) return (false, null, "WorkItemId required.");

            ProductionRepository.EnsureInitialized();

            try
            {
                using var db = new ProductionDbContext();
                var workItem = await db.WorkItems.FindAsync(new object[] { workItemId }, ct).ConfigureAwait(false);
                if (workItem == null) return (false, null, "Work item not found.");

                // Determine next sequence for this work item
                var existing = await db.QcEntries
                    .Where(q => q.WorkItemId == workItemId)
                    .OrderByDescending(q => q.QcSequence)
                    .ToListAsync(ct).ConfigureAwait(false);

                int nextSeq = existing.Count == 0 ? 1 : existing.Max(q => q.QcSequence) + 1;
                string qcId = BuildQcId(workItem.JobRef, workItem.SpecGroupNumber, workItem.SerialNumber, workItem.Stage, nextSeq);

                // Idempotency: check deterministic QcId
                var existingQc = await db.QcEntries.FirstOrDefaultAsync(q => q.QcId == qcId, ct).ConfigureAwait(false);
                if (existingQc != null)
                    return (true, existingQc, "QC already recorded (idempotent).");

                var qcEntry = new SavedProductionQcEntry
                {
                    QcId = qcId,
                    WorkItemId = workItemId,
                    JobRef = workItem.JobRef,
                    SourceProformaId = workItem.SourceProformaId,
                    SpecGroupNumber = workItem.SpecGroupNumber,
                    SerialNumber = workItem.SerialNumber,
                    GlassRef = workItem.GlassRef,
                    SpecName = workItem.SpecName,
                    Position = workItem.Position,
                    Stage = workItem.Stage,
                    QcSequence = nextSeq,
                    Result = (int)result,
                    Reason = reason ?? string.Empty,
                    Inspector = AuditService.CurrentUsername,
                    InspectedAt = DateTime.Now,
                    Notes = notes ?? string.Empty,
                    ParentQcId = parentQcId ?? string.Empty,
                    IsRework = isRework
                };

                db.QcEntries.Add(qcEntry);

                // Create audit entry in ProductionEntries as well (EntryType QcPassed/QcFailed)
                var auditEntry = new SavedProductionEntry
                {
                    WorkItemId = workItemId,
                    JobRef = workItem.JobRef,
                    Stage = workItem.Stage,
                    SpecGroupNumber = workItem.SpecGroupNumber,
                    SerialNumber = workItem.SerialNumber,
                    Position = workItem.Position,
                    SpecName = workItem.SpecName,
                    GlassRef = workItem.GlassRef,
                    Qty = workItem.QtyDone,
                    DoneBy = AuditService.CurrentUsername,
                    DoneAt = DateTime.Now,
                    EntryType = result == QcResult.Passed ? (int)ProductionEntryType.QcPassed : (int)ProductionEntryType.QcFailed,
                    Reason = reason ?? string.Empty,
                    RelatedQcId = qcId,
                    Notes = notes ?? string.Empty
                };
                db.Entries.Add(auditEntry);

                // Update work item status based on QC result — but do NOT destroy history
                // QcFailed does not revert QtyDone, it just marks status for delivery cap logic
                if (result == QcResult.Failed)
                {
                    workItem.Status = (int)WorkItemStatus.QcFailed;
                }
                else if (result == QcResult.Passed)
                {
                    // If previously QcFailed, and now passed, keep Completed if QtyDone==Qty
                    if (workItem.QtyDone >= workItem.Qty)
                        workItem.Status = (int)WorkItemStatus.Completed;
                }
                else if (result == QcResult.Rework)
                {
                    workItem.Status = (int)WorkItemStatus.Rework;
                }
                workItem.UpdatedAt = DateTime.Now;

                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                AppLogService.Info($"[QC] {workItem.JobRef} Spec {workItem.SpecGroupNumber} Serial {workItem.SerialNumber} Stage {workItem.Stage} QC {result} reason {reason}",
                    new LogContext { Module = "Production", Class = nameof(ProductionQcService), Operation = nameof(RecordQcAsync) });

                return (true, qcEntry, $"QC {result} recorded.");
            }
            catch (Exception ex)
            {
                AppLogService.Error($"[QC] Record failed for WorkItem {workItemId}",
                    ex, new LogContext { Module = "Production", Class = nameof(ProductionQcService), Operation = nameof(RecordQcAsync) });
                return (false, null, $"QC failed: {ex.Message}");
            }
        }

        public static async Task<List<SavedProductionQcEntry>> GetByWorkItemAsync(int workItemId, CancellationToken ct = default)
        {
            if (workItemId <= 0) return new List<SavedProductionQcEntry>();
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            return await db.QcEntries.AsNoTracking()
                .Where(q => q.WorkItemId == workItemId)
                .OrderByDescending(q => q.InspectedAt)
                .ToListAsync(ct).ConfigureAwait(false);
        }

        public static async Task<List<SavedProductionQcEntry>> GetByJobAsync(string jobRef, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobRef)) return new List<SavedProductionQcEntry>();
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            return await db.QcEntries.AsNoTracking()
                .Where(q => q.JobRef == jobRef)
                .OrderByDescending(q => q.InspectedAt)
                .Take(200)
                .ToListAsync(ct).ConfigureAwait(false);
        }

        public static async Task<(int Total, int Passed, int Failed, int Pending, int Rework)> GetSummaryByJobAsync(string jobRef, CancellationToken ct = default)
        {
            var list = await GetByJobAsync(jobRef, ct).ConfigureAwait(false);
            return (
                list.Count,
                list.Count(q => q.Result == (int)QcResult.Passed),
                list.Count(q => q.Result == (int)QcResult.Failed),
                list.Count(q => q.Result == (int)QcResult.Pending),
                list.Count(q => q.Result == (int)QcResult.Rework || q.IsRework)
            );
        }
    }
}
