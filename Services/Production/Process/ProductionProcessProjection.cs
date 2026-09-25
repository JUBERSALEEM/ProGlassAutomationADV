// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/Process/ProductionProcessProjection.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — release → process execution projection
//   A released job writes ProductionWorkItems. Process Work Center reads
//   ProcessExecutions. This projects one Pending execution per live work
//   item so the station list shows the job before Start.
//   Idempotent: an existing execution for that work item is kept, including
//   one already started. Re-release does not reset floor status.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.Process;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Audit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production.Process
{
    public static class ProductionProcessProjection
    {
        /// <summary>
        /// Same identity Start uses, plus position so Outer and Inner of one
        /// serial do not collide on the unique ExecutionId index.
        /// An empty position keeps the legacy id.
        /// </summary>
        public static string BuildProjectedExecutionId(
            string jobRef, int specGroup, int serial, ProductionProcessType processType, string? position)
        {
            string id = ProductionProcessRoutingService.BuildExecutionId(jobRef, specGroup, serial, processType, 0);
            string pos = (position ?? string.Empty).Trim().Replace("|", "/");
            return pos.Length == 0 ? id : id + "|" + pos;
        }

        /// <summary>
        /// Inserts missing Pending executions for one job. Does not SaveChanges.
        /// Returns how many new rows were added.
        /// </summary>
        public static async Task<int> ProjectJobAsync(
            ProductionDbContext db, string jobRef, CancellationToken ct = default)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            jobRef = (jobRef ?? string.Empty).Trim();
            if (jobRef.Length == 0) return 0;

            var items = await db.WorkItems
                .Where(w => w.JobRef == jobRef && !w.IsCancelled && w.Status != (int)WorkItemStatus.Cancelled)
                .ToListAsync(ct).ConfigureAwait(false);

            var existing = await db.ProcessExecutions
                .Where(e => e.JobRef == jobRef)
                .ToListAsync(ct).ConfigureAwait(false);

            var byWorkItem = new Dictionary<int, SavedProcessExecution>();
            var byId = new Dictionary<string, SavedProcessExecution>(StringComparer.Ordinal);
            foreach (var exec in existing)
            {
                if (!string.IsNullOrEmpty(exec.ExecutionId) && !byId.ContainsKey(exec.ExecutionId))
                    byId[exec.ExecutionId] = exec;
                if (exec.OutputWorkItemId > 0 && exec.ReworkSequence == 0 && !byWorkItem.ContainsKey(exec.OutputWorkItemId))
                    byWorkItem[exec.OutputWorkItemId] = exec;
            }

            var units = items
                .GroupBy(w => (w.SpecGroupNumber, w.SerialNumber))
                .ToDictionary(g => g.Key, g => (IReadOnlyList<SavedProductionWorkItem>)g.ToList());

            int added = 0;
            foreach (var wi in items)
            {
                if (wi.Id <= 0 || wi.Stage < 1 || wi.Stage > 8) continue;
                var process = (ProductionProcessType)wi.Stage;
                string projectedId = BuildProjectedExecutionId(wi.JobRef, wi.SpecGroupNumber, wi.SerialNumber, process, wi.Position);
                string legacyId = ProductionProcessRoutingService.BuildExecutionId(wi.JobRef, wi.SpecGroupNumber, wi.SerialNumber, process, 0);

                SavedProcessExecution? match = null;
                if (!byWorkItem.TryGetValue(wi.Id, out match))
                    byId.TryGetValue(projectedId, out match);
                if (match == null)
                {
                    match = existing.FirstOrDefault(e =>
                        e.ReworkSequence == 0
                        && string.IsNullOrEmpty(e.ParentExecutionId)
                        && e.ProcessType == wi.Stage
                        && e.SpecGroupNumber == wi.SpecGroupNumber
                        && e.SerialNumber == wi.SerialNumber
                        && string.Equals(e.Position ?? string.Empty, wi.Position ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                        && (e.ExecutionId == legacyId || e.ExecutionId == projectedId));
                }

                if (match != null)
                {
                    if (match.OutputWorkItemId == 0)
                        match.OutputWorkItemId = wi.Id;
                    if (match.Status == (int)WorkItemStatus.Pending && match.StartedAt == null && match.QtyDone == 0 && match.QtyRequired != wi.Qty)
                    {
                        match.QtyRequired = wi.Qty;
                        match.UpdatedAt = DateTime.Now;
                    }
                    byWorkItem[wi.Id] = match;
                    continue;
                }

                if (!units.TryGetValue((wi.SpecGroupNumber, wi.SerialNumber), out var unit))
                    unit = new List<SavedProductionWorkItem> { wi };

                var route = ProductionProcessRoutingService.BuildRouteForWorkItem(wi, unit);
                string prev = route.PreviousRequiredBefore(process)?.ToString() ?? wi.PredecessorStage ?? string.Empty;
                string next = route.NextRequiredAfter(process)?.ToString() ?? string.Empty;
                string pieceId = string.IsNullOrWhiteSpace(wi.UniqueKey)
                    ? wi.JobRef + "|" + wi.SpecGroupNumber + "|" + wi.SerialNumber + "|" + wi.Position
                    : wi.UniqueKey;

                var row = new SavedProcessExecution
                {
                    ExecutionId = projectedId,
                    JobRef = wi.JobRef,
                    SourceProformaId = wi.SourceProformaId ?? string.Empty,
                    JobOrderId = wi.JobRef,
                    SpecGroupNumber = wi.SpecGroupNumber,
                    SpecName = wi.SpecName ?? string.Empty,
                    SerialNumber = wi.SerialNumber,
                    Position = wi.Position ?? string.Empty,
                    PieceId = pieceId,
                    InputPieceId = pieceId,
                    OutputPieceId = pieceId,
                    ProcessType = wi.Stage,
                    Sequence = 0,
                    InputWorkItemId = 0,
                    OutputWorkItemId = wi.Id,
                    Status = (int)WorkItemStatus.Pending,
                    QtyRequired = wi.Qty,
                    QtyDone = 0,
                    EntryType = 0,
                    Notes = "Released — pending start",
                    QcStatus = 0,
                    IsFoc = wi.IsFoc,
                    PreviousStage = prev,
                    NextStage = next,
                    ReworkSequence = 0,
                    CreatedBy = AuditService.CurrentUsername,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                db.ProcessExecutions.Add(row);
                byId[projectedId] = row;
                byWorkItem[wi.Id] = row;
                existing.Add(row);
                added++;
            }

            var liveIds = items.Select(i => i.Id).ToHashSet();
            foreach (var exec in existing)
            {
                if (exec.Status != (int)WorkItemStatus.Pending) continue;
                if (exec.StartedAt != null || exec.QtyDone > 0) continue;
                if (exec.ReworkSequence != 0 || !string.IsNullOrEmpty(exec.ParentExecutionId)) continue;
                if (exec.OutputWorkItemId <= 0 || liveIds.Contains(exec.OutputWorkItemId)) continue;
                db.ProcessExecutions.Remove(exec);
            }

            return added;
        }

        /// <summary>
        /// Backfill for jobs released before projection existed. Does not raise
        /// <see cref="ProductionRepository.WorkItemsChanged"/> — the caller is already loading.
        /// </summary>
        public static async Task EnsurePendingExecutionsAsync(string? jobRef, CancellationToken ct = default)
        {
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();
            if (!string.IsNullOrWhiteSpace(jobRef))
            {
                await ProjectJobAsync(db, jobRef, ct).ConfigureAwait(false);
            }
            else
            {
                var jobs = await db.WorkItems.AsNoTracking()
                    .Select(w => w.JobRef)
                    .Distinct()
                    .ToListAsync(ct).ConfigureAwait(false);
                foreach (var job in jobs)
                {
                    if (string.IsNullOrWhiteSpace(job)) continue;
                    await ProjectJobAsync(db, job, ct).ConfigureAwait(false);
                }
            }

            if (db.ChangeTracker.HasChanges())
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
