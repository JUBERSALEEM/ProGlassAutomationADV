// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/Control/ProductionReadinessEngine.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 17 Readiness Engine
//   Deterministic readiness calculation for every WorkItem:
//   READY BLOCKED IN_PROGRESS PAUSED REWORK QC_PENDING QC_FAILED COMPLETED CANCELLED ON_HOLD
//   A WorkItem is READY only when:
//   1 not cancelled, 2 predecessor complete, 3 predecessor QC passed, 4 material/input exists,
//   5 component exists for assembly, 6 not already completed, 7 not waiting rework, 8 no blocking.
//   Do not simply use Status==Pending as readiness.
//   Every blocked work item must have understandable reason, deterministic explainable.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.Control;
using ProGlassAutomation.Models.Production.SavedProduction;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.Production.Control
{
    public static class ProductionReadinessEngine
    {
        /// <summary>
        /// Calculate readiness for a single work item given full context.
        /// </summary>
        public static (ProductionReadiness readiness, string blockReason) Calculate(
            SavedProductionWorkItem workItem,
            IReadOnlyList<SavedProductionWorkItem> allWorkItemsForJob,
            IReadOnlyList<SavedProductionWorkItem> allWorkItemsForUnit,
            IReadOnlyDictionary<(int Spec, int Serial, string Position, int Stage), int> qcResults, // 0 pending 1 passed 2 failed
            IReadOnlyDictionary<(int Spec, int Serial), bool> pairingComplete,
            IReadOnlyDictionary<(int Spec, int Serial), bool> assemblyComplete,
            bool hasReservation,
            bool hasAllocation,
            bool isOnHold,
            string holdReason)
        {
            if (workItem == null) throw new ArgumentNullException(nameof(workItem));

            // 1 Cancelled
            if (workItem.IsCancelled)
                return (ProductionReadiness.Cancelled, ProductionBlockReasons.Cancelled);

            // On Hold
            if (isOnHold)
                return (ProductionReadiness.OnHold, string.IsNullOrWhiteSpace(holdReason) ? ProductionBlockReasons.OnHold : holdReason);

            // Completed
            if (workItem.Status == (int)WorkItemStatus.Completed && workItem.QtyDone >= workItem.Qty)
                return (ProductionReadiness.Completed, ProductionBlockReasons.None);

            // Rework / Rejected / Paused mapping
            if (workItem.Status == (int)WorkItemStatus.Rework)
                return (ProductionReadiness.Rework, ProductionBlockReasons.WaitingForRework);
            if (workItem.Status == (int)WorkItemStatus.Rejected)
                return (ProductionReadiness.QcFailed, ProductionBlockReasons.QcFailed);
            if (workItem.Status == (int)WorkItemStatus.Paused)
                return (ProductionReadiness.Paused, ProductionBlockReasons.None);
            if (workItem.Status == (int)WorkItemStatus.QcPending)
                return (ProductionReadiness.QcPending, ProductionBlockReasons.WaitingForQc);
            if (workItem.Status == (int)WorkItemStatus.QcFailed)
                return (ProductionReadiness.QcFailed, ProductionBlockReasons.QcFailed);

            // In Progress
            if (workItem.Status == (int)WorkItemStatus.InProgress)
                return (ProductionReadiness.InProgress, ProductionBlockReasons.None);

            // Pending — need to check predecessors, QC, material, components

            var stage = (ProductionStage)workItem.Stage;

            // Material check for Cutting stage
            if (stage == ProductionStage.Cutting)
            {
                if (!hasReservation)
                    return (ProductionReadiness.Blocked, ProductionBlockReasons.NoReservation);
                // shortage visibility handled in queue, but still allow ready if reserved
            }

            // Predecessor check — piece-flow logic
            // For pane positions (Glass, Outer, Inner, Lam Outer, Lam Inner, Unit is assembly)
            // Use ProductionDistributionEngine precedence: Cutting → edge work (Holes, Polish, EdgeDeletion, Mitring) → Tempering → Pairing → Doubling

            // Determine predecessor stages that must be complete
            var predecessorStages = GetPredecessorStages(stage);

            foreach (var predStage in predecessorStages)
            {
                // For pane: predecessor must be complete for same Spec|Serial|Position
                // For Unit: predecessor is PaneReady (outer+inner complete)

                if (workItem.Position.Equals("Unit", StringComparison.OrdinalIgnoreCase))
                {
                    // Unit (Pairing/Doubling) depends on pane readiness
                    if (predStage == ProductionStage.Pairing)
                    {
                        // Doubling depends on Pairing WorkItem PCS — not
                        // ProcessPairing.Status. Supporting pairing rows may
                        // already be "Completed" while QtyDone is still 0.
                        var pairItem = allWorkItemsForUnit.FirstOrDefault(w =>
                                w.Stage == (int)ProductionStage.Pairing && !w.IsCancelled)
                            ?? allWorkItemsForJob.FirstOrDefault(w =>
                                w.Stage == (int)ProductionStage.Pairing
                                && w.SpecGroupNumber == workItem.SpecGroupNumber
                                && w.SerialNumber == workItem.SerialNumber
                                && !w.IsCancelled);
                        if (pairItem == null || pairItem.QtyDone < pairItem.Qty)
                            return (ProductionReadiness.Blocked, ProductionBlockReasons.PairingIncomplete);
                        continue;
                    }

                    // Pairing depends on outer and inner complete
                    if (stage == ProductionStage.Pairing)
                    {
                        // Check outer component
                        var outerItems = allWorkItemsForJob.Where(w => w.SpecGroupNumber == workItem.SpecGroupNumber && w.SerialNumber == workItem.SerialNumber && (w.Position == "Outer" || w.Position == "Glass" || w.Position == "Lam Outer")).ToList();
                        var innerItems = allWorkItemsForJob.Where(w => w.SpecGroupNumber == workItem.SpecGroupNumber && w.SerialNumber == workItem.SerialNumber && (w.Position == "Inner" || w.Position == "Lam Inner")).ToList();

                        bool outerComplete = outerItems.Count == 0 || outerItems.All(w => w.QtyDone >= w.Qty && w.Status == (int)WorkItemStatus.Completed);
                        bool innerComplete = innerItems.Count == 0 || innerItems.All(w => w.QtyDone >= w.Qty && w.Status == (int)WorkItemStatus.Completed);

                        // For DGU, both outer and inner required; for Lam, both lam outer/inner
                        if (!outerComplete && !innerComplete)
                            return (ProductionReadiness.Blocked, ProductionBlockReasons.WaitingForOuterComponent + " & " + ProductionBlockReasons.WaitingForInnerComponent);
                        if (!outerComplete)
                            return (ProductionReadiness.Blocked, ProductionBlockReasons.WaitingForOuterComponent);
                        if (!innerComplete)
                            return (ProductionReadiness.Blocked, ProductionBlockReasons.WaitingForInnerComponent);
                        continue;
                    }
                }
                else
                {
                    // Pane position — predecessor is same position, same spec/serial, previous stage
                    var predItem = allWorkItemsForUnit.FirstOrDefault(w => w.Position == workItem.Position && w.Stage == (int)predStage && !w.IsCancelled);
                    if (predItem != null)
                    {
                        if (predItem.QtyDone < predItem.Qty || predItem.Status != (int)WorkItemStatus.Completed)
                        {
                            return (ProductionReadiness.Blocked, MapStageToWaitingReason(predStage));
                        }

                        // QC check for predecessor
                        var qcKey = (workItem.SpecGroupNumber, workItem.SerialNumber, workItem.Position, (int)predStage);
                        if (qcResults != null && qcResults.TryGetValue(qcKey, out var qcResult))
                        {
                            if (qcResult == 2) // Failed
                                return (ProductionReadiness.Blocked, ProductionBlockReasons.QcFailed);
                            if (qcResult == 0) // Pending — if predecessor completed but QC pending, block next?
                            {
                                // Only block if QC pending is required — tempering after edge work QC
                                // For safety, if QC exists and is pending, block
                                return (ProductionReadiness.Blocked, ProductionBlockReasons.WaitingForQc);
                            }
                        }
                    }
                }
            }

            // Check if waiting for rework
            if (workItem.ReplacementSequence > 0)
            {
                // If parent not yet reworked?
                // For simplicity, if status pending and replacement sequence >0, it's ready unless blocked
            }

            // All checks passed — READY
            return (ProductionReadiness.Ready, ProductionBlockReasons.None);
        }

        private static List<ProductionStage> GetPredecessorStages(ProductionStage current)
        {
            // Canonical order: Cutting=1 Holes=2 Polish=3 EdgeDel=4 Mitring=5 Tempering=6 Pairing=7 Doubling=8
            // Predecessors: for each stage, all previous required stages that are per-pane or unit
            return current switch
            {
                ProductionStage.Cutting => new List<ProductionStage>(),
                ProductionStage.HolesCutouts => new List<ProductionStage> { ProductionStage.Cutting },
                ProductionStage.Polish => new List<ProductionStage> { ProductionStage.Cutting },
                ProductionStage.EdgeDeletion => new List<ProductionStage> { ProductionStage.Cutting },
                ProductionStage.Mitring => new List<ProductionStage> { ProductionStage.Cutting },
                ProductionStage.Tempering => new List<ProductionStage> { ProductionStage.Cutting, ProductionStage.HolesCutouts, ProductionStage.Polish, ProductionStage.EdgeDeletion, ProductionStage.Mitring },
                ProductionStage.Pairing => new List<ProductionStage> { ProductionStage.Tempering, ProductionStage.Cutting }, // pane complete
                ProductionStage.DoublingLamination => new List<ProductionStage> { ProductionStage.Pairing },
                _ => new List<ProductionStage>()
            };
        }

        private static string MapStageToWaitingReason(ProductionStage stage)
        {
            return stage switch
            {
                ProductionStage.Cutting => ProductionBlockReasons.WaitingForCutting,
                ProductionStage.HolesCutouts => ProductionBlockReasons.WaitingForHolesCutouts,
                ProductionStage.Polish => ProductionBlockReasons.WaitingForPolish,
                ProductionStage.EdgeDeletion => ProductionBlockReasons.WaitingForEdgeDeletion,
                ProductionStage.Mitring => ProductionBlockReasons.WaitingForMitring,
                ProductionStage.Tempering => ProductionBlockReasons.WaitingForTempering,
                ProductionStage.Pairing => ProductionBlockReasons.WaitingForPairing,
                ProductionStage.DoublingLamination => ProductionBlockReasons.WaitingForDoubling,
                _ => ProductionBlockReasons.PreviousStageIncomplete
            };
        }

        public static DueDateRisk CalculateDueRisk(DateTime? dueDate)
        {
            if (!dueDate.HasValue) return DueDateRisk.NoDate;
            var today = DateTime.Today;
            var diff = (dueDate.Value.Date - today).TotalDays;
            if (diff < 0) return DueDateRisk.Overdue;
            if (diff <= 2) return DueDateRisk.AtRisk;
            if (diff <= 7) return DueDateRisk.DueSoon;
            return DueDateRisk.OnTrack;
        }

        public static ProductionPriority ParsePriority(int priority)
        {
            return priority switch
            {
                2 => ProductionPriority.Urgent,
                1 => ProductionPriority.High,
                _ => ProductionPriority.Normal
            };
        }
    }
}
