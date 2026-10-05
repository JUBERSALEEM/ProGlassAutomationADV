// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Production/Process/ProductionProcessRoutingService.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 16 Routing Engine
//   Deterministic production route derived from specification, glass type,
//   processing requirements, component, production flags, existing spec models.
//   Do NOT infer production stages merely from UI selections, do NOT make every
//   job pass through every stage.
//   Canonical stages: Cutting=1 HolesCutouts=2 Polish=3 EdgeDeletion=4 Mitring=5
//   Tempering=6 Pairing=7 DoublingLamination=8 — existing identities preserved.
//   Example routes:
//   SGU: Cutting → optional Holes/Cutouts → optional Polish → optional EdgeDeletion
//        → optional Mitring → optional Tempering → QC → Ready
//   DGU: Cutting Outer + Cutting Inner → optional component processing → Pairing
//        → Doubling → QC → Ready
//   Lamination: Cutting components → optional processing → Pairing → Lamination → QC → Ready
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Production;
using ProGlassAutomation.Models.Production.Process;
using ProGlassAutomation.Models.Production.SavedProduction;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.Production.Process
{
    public static class ProductionProcessRoutingService
    {
        /// <summary>
        /// Build deterministic route for a single work item (spec row + position).
        /// Uses existing ProductionDistributionEngine routing + work item flags.
        /// </summary>
        public static ProductionProcessRoute BuildRouteForWorkItem(SavedProductionWorkItem workItem, IReadOnlyList<SavedProductionWorkItem> allWorkItemsForUnit)
        {
            if (workItem == null) throw new ArgumentNullException(nameof(workItem));

            var route = new ProductionProcessRoute
            {
                JobRef = workItem.JobRef,
                SourceProformaId = workItem.SourceProformaId,
                SpecGroupNumber = workItem.SpecGroupNumber,
                SpecName = workItem.SpecName,
                SerialNumber = workItem.SerialNumber,
                Position = workItem.Position,
                BuildType = workItem.BuildType,
                GlassRef = workItem.GlassRef,
                // Route is WorkItem-scoped. UniqueKey is the stable stage identity.
                // Pane-instance ids (P0001…) are minted only by cutting expansion.
                PieceId = workItem.UniqueKey ?? string.Empty
            };

            // Cutting always required — first stage
            route.RequiredStages.Add(ProductionProcessType.Cutting);

            // Determine optional stages from existing work items for this unit (Spec|Serial|Position)
            // The distribution engine already created work items only when required:
            // - HolesCutouts only when FAN_HOLES/HOLES/CUTOUT active
            // - Polish only when POLISH/POLISH_MATCHED_LAM/EDGE_GRINDING active
            // - Mitring only when MITRING active
            // - EdgeDeletion currently no charge trigger — placeholder, but we check if work item exists
            // - Tempering only when RequiresTempering FT/Heat Strengthened/Heat Soaked
            // - Pairing/Doubling only multi-pane

            // For single-pane piece (Glass/Unit/Lam Outer/Lam Inner/Outer/Inner)
            // Check if there are work items for optional stages with same SpecGroup+Serial+Position (or same Spec+Serial for Unit)

            bool HasStage(ProductionStage stage)
            {
                return allWorkItemsForUnit.Any(w => w.Stage == (int)stage && !w.IsCancelled);
            }

            // Optional edge work — must be in canonical order 2,3,4,5 after Cutting
            if (HasStage(ProductionStage.HolesCutouts))
                route.RequiredStages.Add(ProductionProcessType.HolesCutouts);

            if (HasStage(ProductionStage.Polish))
                route.RequiredStages.Add(ProductionProcessType.Polish);

            if (HasStage(ProductionStage.EdgeDeletion))
                route.RequiredStages.Add(ProductionProcessType.EdgeDeletion);

            if (HasStage(ProductionStage.Mitring))
                route.RequiredStages.Add(ProductionProcessType.Mitring);

            // Tempering after edge work
            if (HasStage(ProductionStage.Tempering))
                route.RequiredStages.Add(ProductionProcessType.Tempering);

            // For Unit position (pairing/doubling) — these are assembly stages, not per-pane
            // If workItem itself is Unit, its route includes Pairing and Doubling
            // If workItem is pane (Outer/Inner), its route ends before Pairing, but we note next stage is Pairing

            if (workItem.Position.Equals("Unit", StringComparison.OrdinalIgnoreCase))
            {
                if (HasStage(ProductionStage.Pairing))
                    route.RequiredStages.Add(ProductionProcessType.Pairing);
                if (HasStage(ProductionStage.DoublingLamination))
                    route.RequiredStages.Add(ProductionProcessType.DoublingLamination);
            }
            else
            {
                // Pane pieces — check if unit has pairing/doubling (means DGU/LAM)
                // For pane, we still want to show next stages as Pairing/Doubling in traceability,
                // but route for pane itself stops at Tempering (or last edge work). The pairing is separate.
                // However for simplicity, we include Pairing/Doubling in route for pane as next logical stages
                // if the job is multi-pane — this helps gating: tempering must complete before pairing.
                // But per spec: "Do NOT make every job pass through every stage" — so only include if multi-pane.

                // Check if job has any Unit work items for same Spec|Serial
                bool isMultiPane = allWorkItemsForUnit.Any(w => w.Position.Equals("Unit", StringComparison.OrdinalIgnoreCase));
                if (isMultiPane)
                {
                    // Pane route ends at last per-pane stage, but we add Pairing/Doubling as assembly continuation
                    // For traceability we keep them separate — pane route does NOT include Pairing/Doubling,
                    // but we store NextStage as Pairing for gating.
                    // To support gating, we will have a separate method GetFullJobRoute.
                }
            }

            // Deduplicate and ensure canonical order Cutting=1..Doubling=8
            route.RequiredStages = route.RequiredStages.Distinct().OrderBy(s => (int)s).ToList();

            return route;
        }

        /// <summary>
        /// Build full job route — all required stages for a job, deterministic, spec-driven.
        /// Used for gating: delivery cannot occur when required stages incomplete.
        /// </summary>
        public static List<ProductionProcessType> BuildFullJobRoute(IReadOnlyList<SavedProductionWorkItem> allWorkItemsForJob)
        {
            if (allWorkItemsForJob == null || allWorkItemsForJob.Count == 0) return new List<ProductionProcessType> { ProductionProcessType.Cutting };

            var stages = allWorkItemsForJob
                .Where(w => !w.IsCancelled)
                .Select(w => (ProductionStage)w.Stage)
                .Distinct()
                .OrderBy(s => (int)s)
                .Select(s => (ProductionProcessType)(int)s)
                .ToList();

            // Ensure Cutting first
            if (!stages.Contains(ProductionProcessType.Cutting))
                stages.Insert(0, ProductionProcessType.Cutting);

            // Ensure canonical order
            stages = stages.Distinct().OrderBy(s => (int)s).ToList();

            return stages;
        }

        /// <summary>
        /// Get route examples for documentation/testing — SGU, SGU+holes, Tempered, DGU, DGU+processing, etc.
        /// </summary>
        public static Dictionary<string, List<ProductionProcessType>> GetRouteExamples()
        {
            return new Dictionary<string, List<ProductionProcessType>>
            {
                ["SGU"] = new List<ProductionProcessType> { ProductionProcessType.Cutting },
                ["SGU with holes"] = new List<ProductionProcessType> { ProductionProcessType.Cutting, ProductionProcessType.HolesCutouts },
                ["Tempered SGU"] = new List<ProductionProcessType> { ProductionProcessType.Cutting, ProductionProcessType.Tempering },
                ["SGU with polish"] = new List<ProductionProcessType> { ProductionProcessType.Cutting, ProductionProcessType.Polish },
                ["SGU full processing"] = new List<ProductionProcessType> { ProductionProcessType.Cutting, ProductionProcessType.HolesCutouts, ProductionProcessType.Polish, ProductionProcessType.EdgeDeletion, ProductionProcessType.Mitring, ProductionProcessType.Tempering },
                ["DGU"] = new List<ProductionProcessType> { ProductionProcessType.Cutting, ProductionProcessType.Pairing, ProductionProcessType.DoublingLamination },
                ["DGU with processing"] = new List<ProductionProcessType> { ProductionProcessType.Cutting, ProductionProcessType.HolesCutouts, ProductionProcessType.Polish, ProductionProcessType.Tempering, ProductionProcessType.Pairing, ProductionProcessType.DoublingLamination },
                ["Lamination"] = new List<ProductionProcessType> { ProductionProcessType.Cutting, ProductionProcessType.Pairing, ProductionProcessType.DoublingLamination },
                ["DGU+Lamination"] = new List<ProductionProcessType> { ProductionProcessType.Cutting, ProductionProcessType.Pairing, ProductionProcessType.DoublingLamination },
            };
        }

        /// <summary>
        /// Check if process can begin — gating: predecessor must be complete.
        /// </summary>
        public static bool CanBegin(ProductionProcessType processType, ProductionProcessRoute route, IReadOnlyDictionary<ProductionProcessType, WorkItemStatus> stageStatuses)
        {
            var prev = route.PreviousRequiredBefore(processType);
            if (prev == null) return true; // first stage always can begin

            if (stageStatuses.TryGetValue(prev.Value, out var status))
            {
                return status == WorkItemStatus.Completed;
            }

            // If predecessor not in status map, assume not complete — cannot begin
            return false;
        }

        /// <summary>
        /// Deterministic ExecutionId — JobRef|Spec|Serial|Stage|ExecutionSequence
        /// Follows existing UniqueKey conventions, no GUID, no DateTime.
        /// </summary>
        public static string BuildExecutionId(string jobRef, int specGroup, int serial, ProductionProcessType processType, int sequence)
        {
            jobRef = (jobRef ?? "").Trim();
            return $"{jobRef}|{specGroup}|{serial}|{(int)processType}|{sequence}";
        }

        public static string BuildPairingId(string jobRef, int specGroup, int serial, int pairSeq)
        {
            jobRef = (jobRef ?? "").Trim();
            return $"{jobRef}|{specGroup}|{serial}|Pair:{pairSeq:000}";
        }

        public static string BuildAssemblyId(string jobRef, int specGroup, int serial, int assemblySeq, string type)
        {
            jobRef = (jobRef ?? "").Trim();
            type = (type ?? "DGU").Trim().ToUpperInvariant();
            return $"{jobRef}|{specGroup}|{serial}|{type}:{assemblySeq:000}";
        }
    }
}
