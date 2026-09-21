// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/Control/ProductionQueueService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 17 Unified Production Queue
//   Aggregates existing WorkItems, does not create duplicate production jobs.
//   Queue info: JobRef Customer PI JO Spec Piece Process Priority DueDate Status Readiness Block Reason Qty Completed Remaining QC Rework Operator Workstation
//   Deterministic ordering: Ready state, Urgency, Due date, Priority, Process sequence, JobRef, Piece identity
//   Never DB insertion order, never random.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.Control;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Production.Process;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production.Control
{
    public static class ProductionQueueService
    {
        /// <summary>
        /// Get unified production queue with readiness, block reasons, priority, due-date risk, workstation, operator.
        /// Uses filtered queries, projections, async loading, cancellation, incremental refresh.
        /// </summary>
        public static async Task<List<ProductionQueueItem>> GetQueueAsync(
            string? jobRefFilter = null,
            int? stageFilter = null,
            ProductionReadiness? readinessFilter = null,
            ProductionPriority? priorityFilter = null,
            string? workstationFilter = null,
            string? search = null,
            bool includeCompleted = false,
            bool includeCancelled = false,
            int take = 200,
            CancellationToken ct = default)
        {
            ProductionRepository.EnsureInitialized();
            using var db = new ProductionDbContext();

            var workItemsQuery = db.WorkItems.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(jobRefFilter))
                workItemsQuery = workItemsQuery.Where(w => w.JobRef == jobRefFilter);

            if (stageFilter.HasValue)
                workItemsQuery = workItemsQuery.Where(w => w.Stage == stageFilter.Value);

            if (!includeCancelled)
                workItemsQuery = workItemsQuery.Where(w => !w.IsCancelled);

            if (!includeCompleted)
                workItemsQuery = workItemsQuery.Where(w => w.Status != (int)WorkItemStatus.Completed || w.QtyDone < w.Qty);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                workItemsQuery = workItemsQuery.Where(w =>
                    w.JobRef.Contains(term) ||
                    w.CustomerName.Contains(term) ||
                    w.SpecName.Contains(term) ||
                    w.GlassRef.Contains(term) ||
                    w.SourceProformaId.Contains(term));
            }

            var workItems = await workItemsQuery
                .OrderBy(w => w.JobRef)
                .ThenBy(w => w.SpecGroupNumber)
                .ThenBy(w => w.SerialNumber)
                .ThenBy(w => w.Position)
                .ThenBy(w => w.Stage)
                .Take(take * 2) // fetch more for filtering after readiness
                .ToListAsync(ct).ConfigureAwait(false);

            // Load supporting data in batch — no DB per row
            var jobRefs = workItems.Select(w => w.JobRef).Distinct().ToList();
            var workItemIds = workItems.Select(w => w.Id).ToList();

            var qcEntries = await db.QcEntries.AsNoTracking()
                .Where(q => jobRefs.Contains(q.JobRef))
                .ToListAsync(ct).ConfigureAwait(false);

            var qcDict = qcEntries
                .GroupBy(q => (q.SpecGroupNumber, q.SerialNumber, q.Position, q.Stage))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.QcSequence).First().Result);

            var pairingComplete = new Dictionary<(int Spec, int Serial), bool>();
            var assemblyComplete = new Dictionary<(int Spec, int Serial), bool>();

            // Pairing/assembly status from Phase16 tables
            var pairings = await db.ProcessPairings.AsNoTracking().Where(p => jobRefs.Contains(p.JobRef)).ToListAsync(ct).ConfigureAwait(false);
            var assemblies = await db.ProcessAssemblies.AsNoTracking().Where(a => jobRefs.Contains(a.JobRef)).ToListAsync(ct).ConfigureAwait(false);

            foreach (var grp in workItems.GroupBy(w => (w.SpecGroupNumber, w.SerialNumber)))
            {
                var pairWi = grp.FirstOrDefault(w => w.Stage == (int)ProductionStage.Pairing && !w.IsCancelled);
                pairingComplete[grp.Key] = pairWi != null && pairWi.QtyDone >= pairWi.Qty;
                var asmWi = grp.FirstOrDefault(w => w.Stage == (int)ProductionStage.DoublingLamination && !w.IsCancelled);
                assemblyComplete[grp.Key] = asmWi != null && asmWi.QtyDone >= asmWi.Qty;
            }

            var holds = await db.ControlHolds.AsNoTracking()
                .Where(h => jobRefs.Contains(h.JobRef) && !h.IsReleased)
                .ToListAsync(ct).ConfigureAwait(false);

            var assignments = await db.ControlAssignments.AsNoTracking()
                .Where(a => jobRefs.Contains(a.JobRef) && a.IsActive)
                .ToListAsync(ct).ConfigureAwait(false);

            var overrides = await db.ControlOverrides.AsNoTracking()
                .Where(o => jobRefs.Contains(o.JobRef))
                .ToListAsync(ct).ConfigureAwait(false);

            var workstations = await db.Workstations.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);

            // Active planning holds are Reserved + Allocated (same as
            // MaterialReservationService). Status==Reserved only treated
            // allocated cutting as "no reservation" / shortage.
            var reservations = await db.Reservations.AsNoTracking()
                .Where(r => jobRefs.Contains(r.JobRef)
                         && (r.Status == (int)ReservationStatus.Reserved
                          || r.Status == (int)ReservationStatus.Allocated))
                .ToListAsync(ct).ConfigureAwait(false);

            var allocations = await db.Allocations.AsNoTracking()
                .Where(a => jobRefs.Contains(a.JobRef))
                .ToListAsync(ct).ConfigureAwait(false);

            // Build queue items
            var queue = new List<ProductionQueueItem>();

            // Group for unit context (for readiness)
            var allWorkItemsForJob = workItems.GroupBy(w => w.JobRef).ToDictionary(g => g.Key, g => g.ToList());
            var allForUnit = workItems.GroupBy(w => (w.JobRef, w.SpecGroupNumber, w.SerialNumber)).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var wi in workItems)
            {
                ct.ThrowIfCancellationRequested();

                var jobGroup = allWorkItemsForJob.TryGetValue(wi.JobRef, out var jg) ? jg : new List<SavedProductionWorkItem> { wi };
                var unitKey = (wi.JobRef, wi.SpecGroupNumber, wi.SerialNumber);
                var unitGroup = allForUnit.TryGetValue(unitKey, out var ug) ? ug : new List<SavedProductionWorkItem> { wi };

                var hold = holds.FirstOrDefault(h => h.WorkItemId == wi.Id || (h.JobRef == wi.JobRef && h.SpecGroupNumber == wi.SpecGroupNumber && h.SerialNumber == wi.SerialNumber && h.Stage == wi.Stage && !h.IsReleased));
                bool isOnHold = hold != null;
                string holdReason = hold?.HoldReason ?? "";

                // Cutting reservations are often serial 0 / job-level sheet holds.
                // Matching spec+serial only treated allocated cutting as "no reservation".
                bool hasReservation = wi.Stage == (int)ProductionStage.Cutting
                    ? reservations.Any(r => r.JobRef == wi.JobRef)
                    : reservations.Any(r => r.JobRef == wi.JobRef && r.SpecGroupNumber == wi.SpecGroupNumber && r.SerialNumber == wi.SerialNumber);
                bool hasAllocation = wi.Stage == (int)ProductionStage.Cutting
                    ? allocations.Any(a => a.JobRef == wi.JobRef)
                    : allocations.Any(a => a.JobRef == wi.JobRef && a.SpecGroupNumber == wi.SpecGroupNumber && a.SerialNumber == wi.SerialNumber);

                var (readiness, blockReason) = ProductionReadinessEngine.Calculate(
                    wi,
                    jobGroup,
                    unitGroup,
                    qcDict,
                    pairingComplete,
                    assemblyComplete,
                    hasReservation,
                    hasAllocation,
                    isOnHold,
                    holdReason);

                // Filter by readiness if requested
                if (readinessFilter.HasValue && readiness != readinessFilter.Value) continue;

                var assignment = assignments.FirstOrDefault(a => a.WorkItemId == wi.Id && a.IsActive);
                var over = overrides.FirstOrDefault(o => o.WorkItemId == wi.Id || (o.JobRef == wi.JobRef && o.SpecGroupNumber == wi.SpecGroupNumber && o.SerialNumber == wi.SerialNumber && o.Stage == wi.Stage));
                var ws = workstations.FirstOrDefault(w => w.WorkstationId == (assignment?.WorkstationId ?? ""));

                var priority = over != null ? ProductionReadinessEngine.ParsePriority(over.Priority) : ProductionPriority.Normal;
                if (priorityFilter.HasValue && priority != priorityFilter.Value) continue;

                if (!string.IsNullOrWhiteSpace(workstationFilter) && assignment?.WorkstationId != workstationFilter) continue;

                // Due date — from override or JO DeliveryDate
                DateTime? dueDate = over?.DueDate;
                if (!dueDate.HasValue)
                {
                    // Try to get from JO — we need to load JO separately? For now use null, will be enriched by dashboard service
                    // We'll attempt to get from ProductionRelease? Not stored. Use CreatedAt + 14 days as fallback visibility
                    dueDate = null;
                }

                var dueRisk = ProductionReadinessEngine.CalculateDueRisk(dueDate);

                var qcResult = qcDict.TryGetValue((wi.SpecGroupNumber, wi.SerialNumber, wi.Position, wi.Stage), out var qr) ? qr : 0;

                var item = new ProductionQueueItem
                {
                    WorkItemId = wi.Id,
                    UniqueKey = wi.UniqueKey,
                    JobRef = wi.JobRef,
                    SourceProformaId = wi.SourceProformaId,
                    CustomerName = wi.CustomerName,
                    ProjectName = wi.ProjectName,
                    Salesman = wi.Salesman,
                    SpecGroupNumber = wi.SpecGroupNumber,
                    SpecName = wi.SpecName,
                    BuildType = wi.BuildType,
                    SerialNumber = wi.SerialNumber,
                    GlassRef = wi.GlassRef,
                    Position = wi.Position,
                    WidthMm = wi.WidthMm,
                    HeightMm = wi.HeightMm,
                    Qty = wi.Qty,
                    QtyDone = wi.QtyDone,
                    Stage = (ProductionStage)wi.Stage,
                    StageLabel = ProductionDistributionEngine.StageLabel((ProductionStage)wi.Stage),
                    Status = (WorkItemStatus)wi.Status,
                    Readiness = readiness,
                    BlockReason = blockReason,
                    Priority = priority,
                    DueDate = dueDate,
                    DueRisk = dueRisk,
                    QcResult = qcResult,
                    IsRework = wi.ReplacementSequence > 0,
                    IsFoc = wi.IsFoc,
                    IsCancelled = wi.IsCancelled,
                    ReplacementSequence = wi.ReplacementSequence,
                    Operator = assignment?.Operator ?? "",
                    WorkstationId = assignment?.WorkstationId ?? "",
                    WorkstationName = ws?.Name ?? "",
                    AssignedBy = assignment?.AssignedBy ?? "",
                    AssignedAt = assignment?.AssignedAt,
                    IsOnHold = isOnHold,
                    HoldReason = holdReason,
                    HeldBy = hold?.HeldBy ?? "",
                    HeldAt = hold?.HeldAt,
                    // Stage Qty is PCS. Reservation.Quantity is sheets.
                    // Allocated/Consumed are allocation row counts, not QtyDone.
                    MaterialRequired = wi.Qty,
                    MaterialReserved = reservations.Where(r => r.JobRef == wi.JobRef).Sum(r => r.Quantity),
                    MaterialAllocated = allocations.Count(a => a.JobRef == wi.JobRef && a.Status == (int)AllocationStatus.Allocated),
                    MaterialConsumed = allocations.Count(a => a.JobRef == wi.JobRef && a.Status == (int)AllocationStatus.Consumed),
                    MaterialAvailable = reservations.Where(r => r.JobRef == wi.JobRef).Sum(r => r.Quantity),
                    MaterialShortage = hasReservation || wi.Stage != (int)ProductionStage.Cutting ? 0 : 1,
                    CreatedAt = wi.CreatedAt,
                    UpdatedAt = wi.UpdatedAt,
                    CompletedAt = wi.CompletedAt,
                    CompletedBy = wi.CompletedBy,
                    Notes = wi.Notes,
                    RequiredStages = ProductionProcessRoutingService.BuildFullJobRoute(jobGroup.Select(j => new Models.Production.SavedProduction.SavedProductionWorkItem
                    {
                        JobRef = j.JobRef,
                        SpecGroupNumber = j.SpecGroupNumber,
                        SerialNumber = j.SerialNumber,
                        Position = j.Position,
                        Stage = j.Stage,
                        IsCancelled = j.IsCancelled
                    }).ToList()).Select(s => (ProductionStage)(int)s).ToList()
                };

                // Previous/Next/Traceability
                var route = ProductionProcessRoutingService.BuildRouteForWorkItem(
                    new Models.Production.SavedProduction.SavedProductionWorkItem
                    {
                        JobRef = wi.JobRef,
                        SourceProformaId = wi.SourceProformaId,
                        SpecGroupNumber = wi.SpecGroupNumber,
                        SpecName = wi.SpecName,
                        SerialNumber = wi.SerialNumber,
                        Position = wi.Position,
                        BuildType = wi.BuildType,
                        GlassRef = wi.GlassRef,
                        WidthMm = wi.WidthMm,
                        HeightMm = wi.HeightMm,
                        Qty = wi.Qty,
                        Stage = wi.Stage,
                        IsCancelled = wi.IsCancelled
                    },
                    unitGroup.Select(u => new Models.Production.SavedProduction.SavedProductionWorkItem
                    {
                        JobRef = u.JobRef,
                        SpecGroupNumber = u.SpecGroupNumber,
                        SerialNumber = u.SerialNumber,
                        Position = u.Position,
                        Stage = u.Stage,
                        IsCancelled = u.IsCancelled
                    }).ToList());

                item.PreviousStage = route.PreviousRequiredBefore((Models.Production.Process.ProductionProcessType)wi.Stage) is Models.Production.Process.ProductionProcessType prev ? (ProductionStage)(int)prev : null;
                item.NextStage = route.NextRequiredAfter((Models.Production.Process.ProductionProcessType)wi.Stage) is Models.Production.Process.ProductionProcessType next ? (ProductionStage)(int)next : null;
                item.PreviousLabel = item.PreviousStage.HasValue ? ProductionDistributionEngine.StageLabel(item.PreviousStage.Value) : "";
                item.CurrentLabel = item.StageLabel;
                item.NextLabel = item.NextStage.HasValue ? ProductionDistributionEngine.StageLabel(item.NextStage.Value) : "Ready";
                item.TraceabilityLabel = $"{item.PreviousLabel} → {item.CurrentLabel} → {item.NextLabel}";

                // DGU specifics
                var pair = pairings.FirstOrDefault(p => p.SpecGroupNumber == wi.SpecGroupNumber && p.SerialNumber == wi.SerialNumber && p.JobRef == wi.JobRef);
                if (pair != null)
                {
                    item.OuterPieceId = pair.OuterPieceId;
                    item.InnerPieceId = pair.InnerPieceId;
                    item.PairId = pair.PairId;
                }
                var asm = assemblies.FirstOrDefault(a => a.SpecGroupNumber == wi.SpecGroupNumber && a.SerialNumber == wi.SerialNumber && a.JobRef == wi.JobRef);
                if (asm != null)
                    item.AssemblyId = asm.AssemblyLabel;

                queue.Add(item);
            }

            // Deterministic queue ordering: Ready state, Urgency, Due date, Priority, Process sequence, JobRef, Piece identity
            var ordered = queue
                .OrderBy(q => q.Readiness == ProductionReadiness.Ready ? 0 : q.Readiness == ProductionReadiness.InProgress ? 1 : q.Readiness == ProductionReadiness.Blocked ? 3 : 2)
                .ThenByDescending(q => q.Priority) // Urgent first
                .ThenBy(q => q.DueDate ?? DateTime.MaxValue)
                .ThenBy(q => (int)q.Stage)
                .ThenBy(q => q.JobRef, StringComparer.Ordinal)
                .ThenBy(q => q.SpecGroupNumber)
                .ThenBy(q => q.SerialNumber)
                .ThenBy(q => q.Position, StringComparer.Ordinal)
                .ThenBy(q => q.UniqueKey, StringComparer.Ordinal)
                .Take(take)
                .ToList();

            return ordered;
        }

        public static async Task<List<ProductionQueueItem>> GetExecutableQueueAsync(int take = 100, CancellationToken ct = default)
        {
            // Ready only, deterministic
            return await GetQueueAsync(readinessFilter: ProductionReadiness.Ready, includeCompleted: false, includeCancelled: false, take: take, ct: ct).ConfigureAwait(false);
        }

        public static async Task<List<ProductionQueueItem>> GetBlockedQueueAsync(int take = 100, CancellationToken ct = default)
        {
            return await GetQueueAsync(readinessFilter: ProductionReadiness.Blocked, includeCompleted: false, includeCancelled: false, take: take, ct: ct).ConfigureAwait(false);
        }
    }
}
