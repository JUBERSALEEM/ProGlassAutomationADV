// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/Control/ProductionTimingService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 17 Timing
//   Where existing execution timestamps exist, calculate Queue wait time, Processing duration,
//   Pause duration where available, QC waiting time, Rework duration, Total production elapsed time
//   Do not fabricate timestamps for historical records, do not alter existing historical records.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Production.Control;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production.Control
{
    public static class ProductionTimingService
    {
        public static async Task EnrichTimingAsync(List<ProductionQueueItem> queue, CancellationToken ct = default)
        {
            // Timing from existing timestamps — do not fabricate
            var now = DateTime.Now;

            foreach (var item in queue)
            {
                ct.ThrowIfCancellationRequested();

                // Queue wait time = now - CreatedAt if not started, or UpdatedAt - CreatedAt?
                // Use CreatedAt as queue entry time
                if (item.Status == Models.Production.WorkItemStatus.Pending)
                {
                    item.QueueWaitTime = now - item.CreatedAt;
                }
                else if (item.CompletedAt.HasValue)
                {
                    item.ProcessingDuration = item.CompletedAt.Value - item.CreatedAt;
                    item.QueueWaitTime = item.UpdatedAt - item.CreatedAt;
                }
                else
                {
                    item.QueueWaitTime = item.UpdatedAt - item.CreatedAt;
                    item.ProcessingDuration = now - item.UpdatedAt;
                }
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
