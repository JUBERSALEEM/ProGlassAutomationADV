// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/ProductionControlReportService.cs
// PROJECT: ProGlassAutomation
// MODULE: Reporting — Phase 17 Factory Control Reports
//   Reuse existing Report Generator, provide factory-control reports:
//   1 Production Queue, 2 Production Status, 3 Production Progress, 4 Blocked Work,
//   5 Rework Queue, 6 QC Queue, 7 Ready-for-Delivery, 8 Workstation Workload, 9 Production History
//   Do not create new reporting framework.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Production.Control;
using ProGlassAutomation.Services.Production.Control;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Reporting
{
    public static class ProductionControlReportService
    {
        public static async Task<List<ProductionQueueItem>> GenerateProductionQueueReportAsync(
            string? jobRefFilter = null,
            int? stageFilter = null,
            ProductionReadiness? readinessFilter = null,
            string? search = null,
            int take = 500,
            CancellationToken ct = default)
        {
            return await ProductionQueueService.GetQueueAsync(
                jobRefFilter: jobRefFilter,
                stageFilter: stageFilter,
                readinessFilter: readinessFilter,
                search: search,
                includeCompleted: false,
                includeCancelled: false,
                take: take,
                ct: ct).ConfigureAwait(false);
        }

        public static async Task<List<ProductionQueueItem>> GenerateBlockedWorkReportAsync(int take = 500, CancellationToken ct = default)
        {
            return await ProductionQueueService.GetBlockedQueueAsync(take, ct).ConfigureAwait(false);
        }

        public static async Task<List<ProductionQueueItem>> GenerateReworkQueueReportAsync(int take = 500, CancellationToken ct = default)
        {
            var queue = await ProductionQueueService.GetQueueAsync(includeCompleted: false, includeCancelled: false, take: 1000, ct: ct).ConfigureAwait(false);
            return queue.FindAll(q => q.Readiness == ProductionReadiness.Rework || q.IsRework);
        }

        public static async Task<List<ProductionQueueItem>> GenerateQcQueueReportAsync(int take = 500, CancellationToken ct = default)
        {
            var queue = await ProductionQueueService.GetQueueAsync(includeCompleted: true, includeCancelled: false, take: 1000, ct: ct).ConfigureAwait(false);
            return queue.FindAll(q => q.Readiness == ProductionReadiness.QcPending || q.Readiness == ProductionReadiness.QcFailed);
        }

        public static async Task<ProductionDashboardKpis> GenerateProductionStatusReportAsync(CancellationToken ct = default)
        {
            return await ProductionDashboardService.GetKpisAsync(ct).ConfigureAwait(false);
        }

        public static async Task<List<ProductionProgress>> GenerateProductionProgressReportAsync(string? jobRef = null, CancellationToken ct = default)
        {
            return await ProductionProgressService.GetJobProgressAsync(jobRef, ct).ConfigureAwait(false);
        }

        public static async Task<List<WorkstationLoad>> GenerateWorkstationWorkloadReportAsync(CancellationToken ct = default)
        {
            return await ProductionDashboardService.GetWorkstationLoadAsync(ct).ConfigureAwait(false);
        }

        public static async Task<List<ProductionQueuePressure>> GenerateQueuePressureReportAsync(CancellationToken ct = default)
        {
            return await ProductionDashboardService.GetQueuePressureAsync(ct).ConfigureAwait(false);
        }
    }
}
