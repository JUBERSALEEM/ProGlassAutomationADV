// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/Control/ProductionProgressService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 17 Progress Calculation
//   Calculate progress at Piece, Specification, JO, PI, Job, Customer, Process, Overall
//   Example JO-1024 Required 20 Completed 14 Rework 2 QC Pending 1 InProgress 2 Blocked 1 Progress 70%
//   Do not double-count DGU components, define clearly whether progress is physical pieces, work items, final assemblies
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.Control;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production.Control
{
    public static class ProductionProgressService
    {
        public static async Task<List<ProductionProgress>> GetJobProgressAsync(string? jobRef = null, CancellationToken ct = default)
        {
            var queue = await ProductionQueueService.GetQueueAsync(jobRefFilter: jobRef, includeCompleted: true, includeCancelled: true, take: 1000, ct: ct).ConfigureAwait(false);

            // Progress by JobRef — use final deliverable assemblies metric, not double-count DGU components
            // For DGU, progress should count Unit (assembly) not Outer+Inner separately
            // Strategy: if BuildType is Dgu/Lam/DguLam, count only Unit work items for final progress, but also track component progress

            var grouped = queue.GroupBy(q => q.JobRef);

            var result = new List<ProductionProgress>();

            foreach (var g in grouped)
            {
                var items = g.ToList();

                // For overall job progress, use distinct Spec|Serial as pieces, not work items
                // Required = sum of Qty for distinct units (SpecGroup+Serial)
                var units = items.GroupBy(i => (i.SpecGroupNumber, i.SerialNumber)).ToList();

                int required = units.Sum(u => u.First().Qty);
                int completed = 0;
                int inProgress = 0;
                int blocked = 0;
                int paused = 0;
                int rework = 0;
                int qcPending = 0;
                int qcFailed = 0;
                int onHold = 0;
                int cancelled = 0;

                foreach (var unit in units)
                {
                    var unitItems = unit.ToList();
                    // For unit progress, check if final stage completed
                    // If multi-pane, final is Doubling/Lamination Unit; if SGU, final is last pane stage
                    var finalItem = unitItems.OrderByDescending(i => (int)i.Stage).FirstOrDefault();
                    if (finalItem == null) continue;

                    if (finalItem.IsCancelled) cancelled++;
                    else if (finalItem.Readiness == ProductionReadiness.Completed) completed += finalItem.Qty;
                    else if (finalItem.Readiness == ProductionReadiness.InProgress) inProgress += finalItem.Remaining;
                    else if (finalItem.Readiness == ProductionReadiness.Blocked) blocked += finalItem.Remaining;
                    else if (finalItem.Readiness == ProductionReadiness.Paused) paused += finalItem.Remaining;
                    else if (finalItem.Readiness == ProductionReadiness.Rework) rework += finalItem.Remaining;
                    else if (finalItem.Readiness == ProductionReadiness.QcPending) qcPending += finalItem.Remaining;
                    else if (finalItem.Readiness == ProductionReadiness.QcFailed) qcFailed += finalItem.Remaining;
                    else if (finalItem.Readiness == ProductionReadiness.OnHold) onHold += finalItem.Remaining;
                    else if (finalItem.Readiness == ProductionReadiness.Ready) { /* ready counts as remaining */ }
                }

                // For job-level, completed is sum of completed pieces (not work items)
                var jobCompleted = items.Where(i => i.Readiness == ProductionReadiness.Completed).Sum(i => i.QtyDone);
                // Actually use unit-based completed above for physical pieces metric

                result.Add(new ProductionProgress
                {
                    JobRef = g.Key,
                    Required = required,
                    Completed = completed,
                    InProgress = inProgress,
                    Blocked = blocked,
                    Paused = paused,
                    Rework = rework,
                    QcPending = qcPending,
                    QcFailed = qcFailed,
                    OnHold = onHold,
                    Cancelled = cancelled
                });
            }

            return result;
        }

        public static async Task<ProductionProgress> GetOverallProgressAsync(CancellationToken ct = default)
        {
            var jobs = await GetJobProgressAsync(null, ct).ConfigureAwait(false);
            return new ProductionProgress
            {
                JobRef = "OVERALL",
                Required = jobs.Sum(j => j.Required),
                Completed = jobs.Sum(j => j.Completed),
                InProgress = jobs.Sum(j => j.InProgress),
                Blocked = jobs.Sum(j => j.Blocked),
                Paused = jobs.Sum(j => j.Paused),
                Rework = jobs.Sum(j => j.Rework),
                QcPending = jobs.Sum(j => j.QcPending),
                QcFailed = jobs.Sum(j => j.QcFailed),
                OnHold = jobs.Sum(j => j.OnHold),
                Cancelled = jobs.Sum(j => j.Cancelled)
            };
        }

        public static async Task<List<ProductionProgress>> GetProcessProgressAsync(CancellationToken ct = default)
        {
            var queue = await ProductionQueueService.GetQueueAsync(includeCompleted: true, includeCancelled: false, take: 1000, ct: ct).ConfigureAwait(false);

            var grouped = queue.GroupBy(q => q.Stage);

            var result = new List<ProductionProgress>();

            foreach (var g in grouped)
            {
                var items = g.ToList();
                result.Add(new ProductionProgress
                {
                    JobRef = g.Key.ToString(),
                    SpecName = ProductionDistributionEngine.StageLabel(g.Key),
                    Required = items.Sum(i => i.Qty),
                    Completed = items.Where(i => i.Readiness == ProductionReadiness.Completed).Sum(i => i.Qty),
                    InProgress = items.Where(i => i.Readiness == ProductionReadiness.InProgress).Sum(i => i.Remaining),
                    Blocked = items.Where(i => i.Readiness == ProductionReadiness.Blocked).Sum(i => i.Remaining),
                    Paused = items.Where(i => i.Readiness == ProductionReadiness.Paused).Sum(i => i.Remaining),
                    Rework = items.Where(i => i.Readiness == ProductionReadiness.Rework).Sum(i => i.Remaining),
                    QcPending = items.Where(i => i.Readiness == ProductionReadiness.QcPending).Sum(i => i.Remaining),
                    QcFailed = items.Where(i => i.Readiness == ProductionReadiness.QcFailed).Sum(i => i.Remaining),
                    OnHold = items.Where(i => i.Readiness == ProductionReadiness.OnHold).Sum(i => i.Remaining),
                    Cancelled = 0
                });
            }

            return result.OrderBy(p => p.JobRef).ToList();
        }
    }
}
