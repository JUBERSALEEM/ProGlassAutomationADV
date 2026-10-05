// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/Control/ProductionDashboardService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 17 Factory Dashboard
//   Factory-oriented KPIs: Jobs in Production, Ready to Start, In Progress, On Hold, Blocked,
//   QC Pending, QC Failed, Rework, Completed Today, Ready for Delivery
//   Additional: Overdue Jobs, Due Soon, Material Shortages, Bottleneck Process
//   Do not overload dashboard with meaningless cards, use real production data.
//   Process Load: workload by process counts from actual WorkItems no duplicated counters.
//   Bottleneck Detection: deterministic largest ready queue longest waiting time most blocked overdue QC backlog rework backlog
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.Control;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production.Control
{
    public static class ProductionDashboardService
    {
        public static async Task<ProductionDashboardKpis> GetKpisAsync(CancellationToken ct = default)
        {
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();

            var workItems = await db.WorkItems.AsNoTracking().Where(w => !w.IsCancelled).ToListAsync(ct).ConfigureAwait(false);
            var holds = await db.ControlHolds.AsNoTracking().Where(h => !h.IsReleased).ToListAsync(ct).ConfigureAwait(false);
            var qc = await db.QcEntries.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
            var overrides = await db.ControlOverrides.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);

            // Use queue service for readiness — but for performance, calculate quickly here for KPIs
            var queue = await ProductionQueueService.GetQueueAsync(includeCompleted: true, includeCancelled: false, take: 1000, ct: ct).ConfigureAwait(false);

            var today = DateTime.Today;

            var kpis = new ProductionDashboardKpis
            {
                JobsInProduction = workItems.Select(w => w.JobRef).Distinct().Count(),
                ReadyToStart = queue.Count(q => q.Readiness == ProductionReadiness.Ready),
                InProgress = queue.Count(q => q.Readiness == ProductionReadiness.InProgress),
                OnHold = queue.Count(q => q.Readiness == ProductionReadiness.OnHold),
                Blocked = queue.Count(q => q.Readiness == ProductionReadiness.Blocked),
                QcPending = queue.Count(q => q.Readiness == ProductionReadiness.QcPending),
                QcFailed = queue.Count(q => q.Readiness == ProductionReadiness.QcFailed),
                Rework = queue.Count(q => q.Readiness == ProductionReadiness.Rework),
                CompletedToday = workItems.Count(w => w.CompletedAt.HasValue && w.CompletedAt.Value.Date == today),
                OverdueJobs = queue.Where(q => q.DueRisk == DueDateRisk.Overdue).Select(q => q.JobRef).Distinct().Count(),
                DueSoon = queue.Count(q => q.DueRisk == DueDateRisk.DueSoon || q.DueRisk == DueDateRisk.AtRisk),
                MaterialShortages = queue.Count(q => q.HasMaterialShortage)
            };

            // Ready for Delivery via gateway — count distinct jobs where dispatch ready
            try
            {
                var readiness = await ProductionRepository.GetUnitReadinessAsync().ConfigureAwait(false);
                kpis.ReadyForDelivery = readiness.ReadyUnits;
            }
            catch
            {
                kpis.ReadyForDelivery = queue.Count(q => q.NextLabel == "Ready" && q.Readiness == ProductionReadiness.Completed);
            }

            // Process Load — counts from actual WorkItems no duplicated counters
            var processLoad = queue.GroupBy(q => (int)q.Stage).ToDictionary(g => g.Key, g => g.Count());
            kpis.ProcessLoad = processLoad;

            // Bottleneck — largest ready queue
            var bottleneck = queue.Where(q => q.Readiness == ProductionReadiness.Ready)
                .GroupBy(q => q.Stage)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (bottleneck != null)
            {
                kpis.BottleneckProcess = ProductionDistributionEngine.StageLabel(bottleneck.Key);
                kpis.BottleneckCount = bottleneck.Count();
            }

            return kpis;
        }

        public static async Task<List<ProductionQueuePressure>> GetQueuePressureAsync(CancellationToken ct = default)
        {
            var queue = await ProductionQueueService.GetQueueAsync(includeCompleted: false, includeCancelled: false, take: 1000, ct: ct).ConfigureAwait(false);

            var pressures = queue.GroupBy(q => q.Stage).Select(g =>
            {
                var ready = g.Count(x => x.Readiness == ProductionReadiness.Ready);
                var blocked = g.Count(x => x.Readiness == ProductionReadiness.Blocked);
                var overdue = g.Count(x => x.DueRisk == DueDateRisk.Overdue);
                var qcBacklog = g.Count(x => x.Readiness == ProductionReadiness.QcPending);
                var reworkBacklog = g.Count(x => x.Readiness == ProductionReadiness.Rework);
                var avgWait = g.Where(x => x.QueueWaitTime.HasValue).Select(x => x.QueueWaitTime!.Value.TotalHours).DefaultIfEmpty(0).Average();

                return new ProductionQueuePressure
                {
                    ProcessType = (int)g.Key,
                    ProcessName = ProductionDistributionEngine.StageLabel(g.Key),
                    ReadyQueue = ready,
                    BlockedQueue = blocked,
                    OverdueQueue = overdue,
                    QcBacklog = qcBacklog,
                    ReworkBacklog = reworkBacklog,
                    AvgWaitHours = Math.Round(avgWait, 1)
                };
            })
            .OrderByDescending(p => p.Total)
            .ToList();

            return pressures;
        }

        public static async Task<List<WorkstationLoad>> GetWorkstationLoadAsync(CancellationToken ct = default)
        {
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();

            var workstations = await db.Workstations.AsNoTracking().Where(w => w.IsActive).ToListAsync(ct).ConfigureAwait(false);
            var assignments = await db.ControlAssignments.AsNoTracking().Where(a => a.IsActive).ToListAsync(ct).ConfigureAwait(false);

            var queue = await ProductionQueueService.GetQueueAsync(includeCompleted: false, includeCancelled: false, take: 1000, ct: ct).ConfigureAwait(false);

            var loads = workstations.Select(ws =>
            {
                var assigned = assignments.Where(a => a.WorkstationId == ws.WorkstationId).ToList();
                var assignedWorkItemIds = assigned.Select(a => a.WorkItemId).ToHashSet();
                var assignedQueue = queue.Where(q => assignedWorkItemIds.Contains(q.WorkItemId)).ToList();

                return new WorkstationLoad
                {
                    WorkstationId = ws.WorkstationId,
                    Code = ws.Code,
                    Name = ws.Name,
                    ProcessType = ws.ProcessType,
                    ProcessName = ws.ProcessName,
                    AssignedCount = assigned.Count,
                    ReadyCount = assignedQueue.Count(q => q.Readiness == ProductionReadiness.Ready),
                    InProgressCount = assignedQueue.Count(q => q.Readiness == ProductionReadiness.InProgress),
                    BlockedCount = assignedQueue.Count(q => q.Readiness == ProductionReadiness.Blocked)
                };
            }).OrderBy(l => l.ProcessType).ThenBy(l => l.Code).ToList();

            return loads;
        }
    }
}
