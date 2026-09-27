// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27), Phase 26a — the
// production process + control engine (the pure half):
//   Models/Production/Process/ProductionProcessExecution.cs (142 —
//     ProductionProcessType, ProductionProcessRoute with
//     NextRequiredAfter/PreviousRequiredBefore)
//   Models/Production/Control/ProductionControlModels.cs (247 —
//     ProductionReadiness, ProductionBlockReasons, ProductionPriority,
//     DueDateRisk, ProductionQueueItem, ProductionProgress,
//     ProductionDashboardKpis, WorkstationLoad, ProductionQueuePressure)
//   Models/Production/Process/ProcessDetails.cs (149 — the six
//     per-process detail payloads) + the summarize contract from
//     Services/Production/Process/ProcessExecutionFactory.cs
//   Services/Production/Process/ProductionProcessRoutingService.cs (213 —
//     BuildRouteForWorkItem/BuildFullJobRoute/CanBegin + the
//     deterministic identity builders)
//   Services/Production/Control/ProductionReadinessEngine.cs (228 — the
//     ten-state readiness calculation with explainable block reasons,
//     the canonical predecessor map, due-date risk)
//   Services/Production/ProductionPieceIdentity.cs (104 — the pane
//     instance identity contract JOB|SPEC|SERIAL|POSITION|WxH|P0001
//     ± |RPL:n, never a GUID, never DB order)
//   Services/Production/Control/ProductionControlService.cs id builders
//     (BuildHoldId/BuildAssignmentId/BuildControlId)
//   Services/Production/Control/ProductionTimingService.cs (51 — timing
//     from existing timestamps, never fabricated)
//
// SaaS adaptations (mechanical, documented): stages and statuses are the
// SaaS's existing TEXT vocabularies (ProductionStages names +
// WorkItemStatuses names from Phase 9b) instead of the WPF's int enums;
// the work-item input is the lightweight RouteWorkItem record so the
// engine stays PURE (no entity references). PURE AND DETERMINISTIC.
// ═══════════════════════════════════════════════════════════════════════

using System.Text.RegularExpressions;

namespace ProGlass.Saas.Domain.Proforma;

// ── the routing engine (WPF ProductionProcessRoutingService) ─────────

/// <summary>The lightweight work-item shape the pure engine consumes —
/// exactly the fields BuildRouteForWorkItem/Readiness read (WPF
/// SavedProductionWorkItem projection).</summary>
public sealed record RouteWorkItem(
    string JobRef,
    string SourceProformaId,
    int SpecGroupNumber,
    string SpecName,
    int SerialNumber,
    string Position,
    string BuildType,
    string GlassRef,
    string Stage,
    string Status,
    bool IsCancelled,
    int Qty,
    int QtyDone,
    string UniqueKey = "",
    int WidthMm = 0,
    int HeightMm = 0,
    int ReplacementSequence = 0);

/// <summary>Deterministic route for one spec row — which stages are
/// actually required, in canonical order (WPF ProductionProcessRoute).
/// Derived from the work items the distribution engine created, never
/// from UI selections: "do NOT make every job pass through every
/// stage."</summary>
public sealed class ProductionProcessRoute
{
    public string JobRef { get; init; } = "";
    public string SourceProformaId { get; init; } = "";
    public int SpecGroupNumber { get; init; }
    public string SpecName { get; init; } = "";
    public int SerialNumber { get; init; }
    public string Position { get; init; } = "";
    public string BuildType { get; init; } = "";
    public string GlassRef { get; init; } = "";
    public string PieceId { get; init; } = "";
    /// <summary>Required stage NAMES in canonical order (Cutting → … →
    /// DoublingLamination).</summary>
    public List<string> RequiredStages { get; set; } = new();

    public string RouteLabel => string.Join(" → ", RequiredStages);
    public bool RequiresStage(string stage) => RequiredStages.Contains(stage);

    public string? NextRequiredAfter(string current)
    {
        int idx = RequiredStages.IndexOf(current);
        if (idx < 0 || idx + 1 >= RequiredStages.Count) return null;
        return RequiredStages[idx + 1];
    }

    public string? PreviousRequiredBefore(string current)
    {
        int idx = RequiredStages.IndexOf(current);
        if (idx <= 0) return null;
        return RequiredStages[idx - 1];
    }
}

public static class ProcessRouting
{
    /// <summary>Build the deterministic route for one work item from the
    /// unit's live work items (WPF verbatim: Cutting always first, the
    /// optional stages only when the distribution engine created them,
    /// Pairing/Doubling only for the Unit position, canonical order).</summary>
    public static ProductionProcessRoute BuildRouteForWorkItem(
        RouteWorkItem workItem, IReadOnlyList<RouteWorkItem> allWorkItemsForUnit)
    {
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
            // Route is WorkItem-scoped; UniqueKey is the stable stage
            // identity (pane instance ids are minted by cutting expansion).
            PieceId = string.IsNullOrEmpty(workItem.UniqueKey) ? "" : workItem.UniqueKey
        };

        route.RequiredStages.Add(ProductionStages.CuttingName);

        bool HasStage(string stage) =>
            allWorkItemsForUnit.Any(w => w.Stage == stage && !w.IsCancelled);

        // Optional edge work in canonical order after Cutting.
        if (HasStage(ProductionStages.HolesCutouts))
            route.RequiredStages.Add(ProductionStages.HolesCutouts);
        if (HasStage(ProductionStages.Polish))
            route.RequiredStages.Add(ProductionStages.Polish);
        if (HasStage(ProductionStages.EdgeDeletion))
            route.RequiredStages.Add(ProductionStages.EdgeDeletion);
        if (HasStage(ProductionStages.Mitring))
            route.RequiredStages.Add(ProductionStages.Mitring);
        if (HasStage(ProductionStages.Tempering))
            route.RequiredStages.Add(ProductionStages.Tempering);

        // Assembly stages are Unit-scoped, not per-pane.
        if (string.Equals(workItem.Position, "Unit", StringComparison.OrdinalIgnoreCase))
        {
            if (HasStage(ProductionStages.Pairing))
                route.RequiredStages.Add(ProductionStages.Pairing);
            if (HasStage(ProductionStages.DoublingLamination))
                route.RequiredStages.Add(ProductionStages.DoublingLamination);
        }

        // Deduplicate in canonical order (Cutting → DoublingLamination).
        route.RequiredStages = route.RequiredStages
            .Distinct()
            .OrderBy(ProductionStages.Code)
            .ToList();
        return route;
    }

    /// <summary>All required stages for a job — used for delivery gating
    /// (WPF BuildFullJobRoute: Cutting always present).</summary>
    public static List<string> BuildFullJobRoute(IReadOnlyList<RouteWorkItem> allWorkItemsForJob)
    {
        if (allWorkItemsForJob.Count == 0)
            return new List<string> { ProductionStages.CuttingName };

        var stages = allWorkItemsForJob
            .Where(w => !w.IsCancelled)
            .Select(w => w.Stage)
            .Distinct()
            .OrderBy(ProductionStages.Code)
            .ToList();

        if (!stages.Contains(ProductionStages.CuttingName))
            stages.Insert(0, ProductionStages.CuttingName);

        return stages.Distinct().OrderBy(ProductionStages.Code).ToList();
    }

    /// <summary>Gating: a process can begin only when its route
    /// predecessor is Completed (WPF CanBegin; an unknown predecessor
    /// status is NOT complete — fail closed).</summary>
    public static bool CanBegin(
        string stage, ProductionProcessRoute route,
        IReadOnlyDictionary<string, string> stageStatuses)
    {
        var prev = route.PreviousRequiredBefore(stage);
        if (prev == null) return true;

        if (stageStatuses.TryGetValue(prev, out var status))
            return status == WorkItemStatuses.Completed;
        return false;
    }

    // ── deterministic identities (WPF verbatim formats) ─────────────

    /// <summary>JobRef|Spec|Serial|ProcessType|Sequence — the WPF
    /// ExecutionId convention (no GUID, no DateTime).</summary>
    public static string BuildExecutionId(string jobRef, int specGroup, int serial, int processType, int sequence)
    {
        jobRef = (jobRef ?? "").Trim();
        return $"{jobRef}|{specGroup}|{serial}|{processType}|{sequence}";
    }

    /// <summary>The projected identity Start uses, plus position so Outer
    /// and Inner of one serial do not collide on the unique index (WPF
    /// ProductionProcessProjection.BuildProjectedExecutionId). An empty
    /// position keeps the legacy id.</summary>
    public static string BuildProjectedExecutionId(
        string jobRef, int specGroup, int serial, int processType, string? position)
    {
        string id = BuildExecutionId(jobRef, specGroup, serial, processType, 0);
        string pos = (position ?? "").Trim().Replace("|", "/");
        return pos.Length == 0 ? id : id + "|" + pos;
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

    // ── control identities (WPF ProductionControlService) ───────────

    public static string BuildHoldId(string jobRef, int specGroup, int serial, int stageCode, int seq) =>
        $"{(jobRef ?? "").Trim()}|{specGroup}|{serial}|{stageCode}|HOLD:{seq:000}";

    public static string BuildAssignmentId(string jobRef, int specGroup, int serial, int stageCode, int seq) =>
        $"{(jobRef ?? "").Trim()}|{specGroup}|{serial}|{stageCode}|ASSIGN:{seq:000}";

    public static string BuildControlId(string jobRef, int specGroup, int serial, int stageCode) =>
        $"{(jobRef ?? "").Trim()}|{specGroup}|{serial}|{stageCode}|CTRL";
}

// ── piece identity (WPF ProductionPieceIdentity) ─────────────────────

public static class ProductionPieceIdentity
{
    private static readonly Regex PaneTail = new(
        @"\|P\d{4}(?:\|RPL:\d+)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Canonical pane-instance id — JOB|SPEC|SERIAL|POSITION|
    /// WxH|P0001, replacement generations append |RPL:n so they can
    /// never masquerade as the original (WPF CuttingRequirementService.
    /// BuildPieceId + ProductionPieceIdentity.BuildPaneInstanceId).</summary>
    public static string BuildPaneInstanceId(
        string jobRef, int specNo, int serial, string position,
        int widthMm, int heightMm, int pieceIndex1Based,
        int replacementSequence = 0)
    {
        jobRef = (jobRef ?? "").Trim();
        position = (position ?? "").Trim();
        string id = $"{jobRef}|{specNo}|{serial}|{position}|{widthMm}x{heightMm}|P{pieceIndex1Based:D4}";
        if (replacementSequence > 0)
            id += "|RPL:" + replacementSequence.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return id;
    }

    public static bool IsPaneInstanceId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        return PaneTail.IsMatch(id.Trim());
    }

    public static bool IsReplacementWorkItemKey(string? uniqueKey) =>
        !string.IsNullOrWhiteSpace(uniqueKey)
        && uniqueKey.IndexOf("|RPL:", StringComparison.Ordinal) >= 0;

    public static bool IsCuttingSessionId(string? id) =>
        !string.IsNullOrWhiteSpace(id)
        && id.StartsWith("CUT-", StringComparison.Ordinal);

    public static bool LooksLikeExecutionId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || IsPaneInstanceId(id)) return false;
        var parts = id.Split('|');
        // JobRef|Spec|Serial|ProcessType|Sequence — five tokens, the last
        // four numeric, process 1..8 (no position).
        return parts.Length == 5
            && int.TryParse(parts[1], out _)
            && int.TryParse(parts[2], out _)
            && int.TryParse(parts[3], out int process)
            && process is >= 1 and <= 8
            && int.TryParse(parts[4], out _);
    }

    public static string Classify(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "none";
        string s = id.Trim();
        if (IsPaneInstanceId(s))
            return s.IndexOf("|RPL:", StringComparison.Ordinal) >= 0
                ? "pane-instance-replacement"
                : "pane-instance";
        if (IsCuttingSessionId(s)) return "cutting-session";
        if (s.IndexOf("|Pair:", StringComparison.Ordinal) >= 0) return "pairing";
        if (s.IndexOf("|DGU:", StringComparison.Ordinal) >= 0
            || s.IndexOf("|LAM:", StringComparison.Ordinal) >= 0
            || s.IndexOf("|DGU+LAM:", StringComparison.Ordinal) >= 0)
            return "assembly";
        if (LooksLikeExecutionId(s)) return "process-execution";
        if (IsReplacementWorkItemKey(s)) return "workitem-replacement";
        return "workitem-or-other";
    }

    public static string FromWorkItem(RouteWorkItem wi, int pieceIndex1Based) =>
        BuildPaneInstanceId(
            wi.JobRef, wi.SpecGroupNumber, wi.SerialNumber, wi.Position,
            wi.WidthMm, wi.HeightMm, pieceIndex1Based, wi.ReplacementSequence);
}

// ── readiness (WPF ProductionReadinessEngine + models) ───────────────

/// <summary>Deterministic readiness — not simply Status == Pending
/// (WPF ProductionReadiness).</summary>
public enum ProductionReadiness
{
    Ready = 0,
    Blocked = 1,
    InProgress = 2,
    Paused = 3,
    Rework = 4,
    QcPending = 5,
    QcFailed = 6,
    Completed = 7,
    Cancelled = 8,
    OnHold = 9
}

public static class ProductionBlockReasons
{
    public const string None = "";
    public const string WaitingForCutting = "Waiting for Cutting";
    public const string WaitingForOuterComponent = "Waiting for Outer Component";
    public const string WaitingForInnerComponent = "Waiting for Inner Component";
    public const string WaitingForQc = "Waiting for QC";
    public const string WaitingForRework = "Waiting for Rework";
    public const string MaterialNotAvailable = "Material Not Available";
    public const string MaterialShortage = "Material Shortage";
    public const string PreviousStageIncomplete = "Previous Stage Incomplete";
    public const string PairingIncomplete = "Pairing Incomplete";
    public const string AssemblyIncomplete = "Assembly Incomplete";
    public const string Cancelled = "Cancelled";
    public const string OnHold = "On Hold";
    public const string WaitingForHolesCutouts = "Waiting for Holes/Cutouts";
    public const string WaitingForPolish = "Waiting for Polish";
    public const string WaitingForEdgeDeletion = "Waiting for Edge Deletion";
    public const string WaitingForMitring = "Waiting for Mitring";
    public const string WaitingForTempering = "Waiting for Tempering";
    public const string WaitingForPairing = "Waiting for Pairing";
    public const string WaitingForDoubling = "Waiting for Doubling/Lamination";
    public const string QcFailed = "QC Failed";
    public const string NoReservation = "No Reservation";
    public const string NoAllocation = "No Allocation";
}

public enum ProductionPriority
{
    Normal = 0,
    High = 1,
    Urgent = 2
}

public enum DueDateRisk
{
    OnTrack = 0,
    DueSoon = 1,
    AtRisk = 2,
    Overdue = 3,
    NoDate = 4
}

public static class ProductionReadinessEngine
{
    /// <summary>
    /// Calculate readiness for a single work item given full context
    /// (WPF Calculate, verbatim): a work item is READY only when it is
    /// not cancelled, its predecessors are complete and QC-passed,
    /// material is reserved (Cutting), components exist (assembly) and
    /// it is not completed/reworking/on hold. Every blocked item gets an
    /// explainable reason.
    /// </summary>
    /// <param name="qcResults">Latest QC result per (spec, serial,
    /// position, stage) — "Passed"/"Failed"/"Pending" (the SaaS derives
    /// it from the typed QC events; the WPF reads its QcEntries
    /// table).</param>
    /// <param name="hasReservation">An active material reservation
    /// exists (job-level for Cutting, unit-level otherwise).</param>
    /// <param name="hasAllocation">Allocation-ledger rows exist (the
    /// ledger itself is the 26b slice — the WPF engine accepts the flag
    /// and never blocks on it; same here).</param>
    public static (ProductionReadiness readiness, string blockReason) Calculate(
        RouteWorkItem workItem,
        IReadOnlyList<RouteWorkItem> allWorkItemsForJob,
        IReadOnlyList<RouteWorkItem> allWorkItemsForUnit,
        IReadOnlyDictionary<(int Spec, int Serial, string Position, string Stage), string> qcResults,
        IReadOnlyDictionary<(int Spec, int Serial), bool> pairingComplete,
        IReadOnlyDictionary<(int Spec, int Serial), bool> assemblyComplete,
        bool hasReservation,
        bool hasAllocation,
        bool isOnHold,
        string holdReason)
    {
        // 1 Cancelled
        if (workItem.IsCancelled || workItem.Status == WorkItemStatuses.Cancelled)
            return (ProductionReadiness.Cancelled, ProductionBlockReasons.Cancelled);

        // On Hold
        if (isOnHold)
            return (ProductionReadiness.OnHold,
                string.IsNullOrWhiteSpace(holdReason) ? ProductionBlockReasons.OnHold : holdReason);

        // Completed
        if (workItem.Status == WorkItemStatuses.Completed && workItem.QtyDone >= workItem.Qty)
            return (ProductionReadiness.Completed, ProductionBlockReasons.None);

        // Rework / Rejected / Paused / QC mapping
        if (workItem.Status == WorkItemStatuses.Rework)
            return (ProductionReadiness.Rework, ProductionBlockReasons.WaitingForRework);
        if (workItem.Status == WorkItemStatuses.Rejected)
            return (ProductionReadiness.QcFailed, ProductionBlockReasons.QcFailed);
        if (workItem.Status == WorkItemStatuses.Paused)
            return (ProductionReadiness.Paused, ProductionBlockReasons.None);
        if (workItem.Status == WorkItemStatuses.QcPending)
            return (ProductionReadiness.QcPending, ProductionBlockReasons.WaitingForQc);
        if (workItem.Status == WorkItemStatuses.QcFailed)
            return (ProductionReadiness.QcFailed, ProductionBlockReasons.QcFailed);

        // In Progress
        if (workItem.Status == WorkItemStatuses.InProgress)
            return (ProductionReadiness.InProgress, ProductionBlockReasons.None);

        // Pending — check predecessors, QC, material, components.
        var stage = workItem.Stage;

        // Material check for Cutting.
        if (stage == ProductionStages.CuttingName && !hasReservation)
            return (ProductionReadiness.Blocked, ProductionBlockReasons.NoReservation);

        foreach (var predStage in GetPredecessorStages(stage))
        {
            if (string.Equals(workItem.Position, "Unit", StringComparison.OrdinalIgnoreCase))
            {
                // Doubling depends on the Pairing work item PCS — not the
                // supporting ProcessPairing rows, which may read Completed
                // while QtyDone is still 0.
                if (predStage == ProductionStages.Pairing)
                {
                    var pairItem = allWorkItemsForUnit.FirstOrDefault(w =>
                            w.Stage == ProductionStages.Pairing && !w.IsCancelled)
                        ?? allWorkItemsForJob.FirstOrDefault(w =>
                            w.Stage == ProductionStages.Pairing
                            && w.SpecGroupNumber == workItem.SpecGroupNumber
                            && w.SerialNumber == workItem.SerialNumber
                            && !w.IsCancelled);
                    if (pairItem is null || pairItem.QtyDone < pairItem.Qty)
                        return (ProductionReadiness.Blocked, ProductionBlockReasons.PairingIncomplete);
                    continue;
                }

                // Pairing depends on outer + inner complete.
                if (stage == ProductionStages.Pairing)
                {
                    var outerItems = allWorkItemsForJob.Where(w =>
                        w.SpecGroupNumber == workItem.SpecGroupNumber
                        && w.SerialNumber == workItem.SerialNumber
                        && (w.Position == "Outer" || w.Position == "Glass" || w.Position == "Lam Outer")).ToList();
                    var innerItems = allWorkItemsForJob.Where(w =>
                        w.SpecGroupNumber == workItem.SpecGroupNumber
                        && w.SerialNumber == workItem.SerialNumber
                        && (w.Position == "Inner" || w.Position == "Lam Inner")).ToList();

                    bool outerComplete = outerItems.Count == 0
                        || outerItems.All(w => w.QtyDone >= w.Qty && w.Status == WorkItemStatuses.Completed);
                    bool innerComplete = innerItems.Count == 0
                        || innerItems.All(w => w.QtyDone >= w.Qty && w.Status == WorkItemStatuses.Completed);

                    if (!outerComplete && !innerComplete)
                        return (ProductionReadiness.Blocked,
                            ProductionBlockReasons.WaitingForOuterComponent + " & "
                            + ProductionBlockReasons.WaitingForInnerComponent);
                    if (!outerComplete)
                        return (ProductionReadiness.Blocked, ProductionBlockReasons.WaitingForOuterComponent);
                    if (!innerComplete)
                        return (ProductionReadiness.Blocked, ProductionBlockReasons.WaitingForInnerComponent);
                    continue;
                }
            }
            else
            {
                // Pane position — same position, same spec/serial, previous stage.
                var predItem = allWorkItemsForUnit.FirstOrDefault(w =>
                    w.Position == workItem.Position && w.Stage == predStage && !w.IsCancelled);
                if (predItem != null)
                {
                    if (predItem.QtyDone < predItem.Qty || predItem.Status != WorkItemStatuses.Completed)
                        return (ProductionReadiness.Blocked, MapStageToWaitingReason(predStage));

                    // QC on the predecessor: failed blocks, pending blocks.
                    var qcKey = (workItem.SpecGroupNumber, workItem.SerialNumber,
                        workItem.Position, predStage);
                    if (qcResults.TryGetValue(qcKey, out var qcResult))
                    {
                        if (qcResult == QcResults.Failed)
                            return (ProductionReadiness.Blocked, ProductionBlockReasons.QcFailed);
                        if (qcResult == QcResults.Pending)
                            return (ProductionReadiness.Blocked, ProductionBlockReasons.WaitingForQc);
                    }
                }
            }
        }

        // All checks passed.
        return (ProductionReadiness.Ready, ProductionBlockReasons.None);
    }

    /// <summary>The canonical predecessor map (WPF GetPredecessorStages):
    /// every stage's per-pane or unit prerequisites.</summary>
    public static IReadOnlyList<string> GetPredecessorStages(string current) => current switch
    {
        var s when s == ProductionStages.CuttingName => Array.Empty<string>(),
        var s when s == ProductionStages.HolesCutouts => new[] { ProductionStages.CuttingName },
        var s when s == ProductionStages.Polish => new[] { ProductionStages.CuttingName },
        var s when s == ProductionStages.EdgeDeletion => new[] { ProductionStages.CuttingName },
        var s when s == ProductionStages.Mitring => new[] { ProductionStages.CuttingName },
        var s when s == ProductionStages.Tempering => new[]
        {
            ProductionStages.CuttingName, ProductionStages.HolesCutouts,
            ProductionStages.Polish, ProductionStages.EdgeDeletion, ProductionStages.Mitring
        },
        // Pairing needs pane complete (Tempering when present, else Cutting).
        var s when s == ProductionStages.Pairing => new[]
        { ProductionStages.Tempering, ProductionStages.CuttingName },
        var s when s == ProductionStages.DoublingLamination => new[] { ProductionStages.Pairing },
        _ => Array.Empty<string>()
    };

    private static string MapStageToWaitingReason(string stage) => stage switch
    {
        var s when s == ProductionStages.CuttingName => ProductionBlockReasons.WaitingForCutting,
        var s when s == ProductionStages.HolesCutouts => ProductionBlockReasons.WaitingForHolesCutouts,
        var s when s == ProductionStages.Polish => ProductionBlockReasons.WaitingForPolish,
        var s when s == ProductionStages.EdgeDeletion => ProductionBlockReasons.WaitingForEdgeDeletion,
        var s when s == ProductionStages.Mitring => ProductionBlockReasons.WaitingForMitring,
        var s when s == ProductionStages.Tempering => ProductionBlockReasons.WaitingForTempering,
        var s when s == ProductionStages.Pairing => ProductionBlockReasons.WaitingForPairing,
        var s when s == ProductionStages.DoublingLamination => ProductionBlockReasons.WaitingForDoubling,
        _ => ProductionBlockReasons.PreviousStageIncomplete
    };

    public static DueDateRisk CalculateDueRisk(DateTime? dueDate, DateTime today)
    {
        if (!dueDate.HasValue) return DueDateRisk.NoDate;
        var diff = (dueDate.Value.Date - today.Date).TotalDays;
        if (diff < 0) return DueDateRisk.Overdue;
        if (diff <= 2) return DueDateRisk.AtRisk;
        if (diff <= 7) return DueDateRisk.DueSoon;
        return DueDateRisk.OnTrack;
    }

    public static ProductionPriority ParsePriority(int priority) => priority switch
    {
        2 => ProductionPriority.Urgent,
        1 => ProductionPriority.High,
        _ => ProductionPriority.Normal
    };
}

// ── the queue / progress read models (WPF models, verbatim shapes) ───

/// <summary>Unified production queue item — aggregates existing work
/// items, never duplicate jobs (WPF ProductionQueueItem; Remaining and
/// the readiness/shortage derivatives included).</summary>
public sealed class ProductionQueueItem
{
    public Guid WorkItemId { get; set; }
    public string UniqueKey { get; set; } = "";
    public string JobRef { get; set; } = "";
    public Guid? JobOrderId { get; set; }
    public string SourceProformaId { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string Salesman { get; set; } = "";
    public int SpecGroupNumber { get; set; }
    public string SpecName { get; set; } = "";
    public string BuildType { get; set; } = "";
    public int SerialNumber { get; set; }
    public string GlassRef { get; set; } = "";
    public string Position { get; set; } = "";
    public int WidthMm { get; set; }
    public int HeightMm { get; set; }
    public int Qty { get; set; }
    public int QtyDone { get; set; }
    public int Remaining => Math.Max(0, Qty - QtyDone);

    public string Stage { get; set; } = "";
    public string StageLabel => ProductionStages.StageLabel(Stage);
    public string? PreviousStage { get; set; }
    public string? NextStage { get; set; }
    public List<string> RequiredStages { get; set; } = new();

    public string Status { get; set; } = "";
    public ProductionReadiness Readiness { get; set; }
    public string BlockReason { get; set; } = "";
    public bool IsBlocked => Readiness == ProductionReadiness.Blocked;
    public bool IsReady => Readiness == ProductionReadiness.Ready;

    public ProductionPriority Priority { get; set; } = ProductionPriority.Normal;
    public DateTime? DueDate { get; set; }
    public DueDateRisk DueRisk { get; set; } = DueDateRisk.NoDate;
    public int PriorityWeight => (int)Priority;

    public string QcResult { get; set; } = "";
    public string QcId { get; set; } = "";
    public bool IsRework { get; set; }
    public bool IsFoc { get; set; }
    public bool IsCancelled { get; set; }
    public int ReplacementSequence { get; set; }

    public string Operator { get; set; } = "";
    public string WorkstationId { get; set; } = "";
    public string WorkstationName { get; set; } = "";
    public string AssignedBy { get; set; } = "";
    public DateTime? AssignedAt { get; set; }

    public bool IsOnHold { get; set; }
    public string HoldReason { get; set; } = "";
    public string HeldBy { get; set; } = "";
    public DateTime? HeldAt { get; set; }

    public int MaterialRequired { get; set; }
    public int MaterialReserved { get; set; }
    public int MaterialAllocated { get; set; }
    public int MaterialConsumed { get; set; }
    public int MaterialAvailable { get; set; }
    public int MaterialShortage { get; set; }
    public bool HasMaterialShortage => MaterialShortage > 0;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string CompletedBy { get; set; } = "";
    public TimeSpan? QueueWaitTime { get; set; }
    public TimeSpan? ProcessingDuration { get; set; }

    public string TraceabilityLabel { get; set; } = "";
    public string PreviousLabel { get; set; } = "";
    public string CurrentLabel { get; set; } = "";
    public string NextLabel { get; set; } = "";

    public string OuterPieceId { get; set; } = "";
    public string InnerPieceId { get; set; } = "";
    public string PairId { get; set; } = "";
    public string AssemblyId { get; set; } = "";

    public string Notes { get; set; } = "";
}

/// <summary>Deterministic queue ordering (WPF GetQueueAsync tail): Ready
/// first, then InProgress, then the rest before Blocked; priority
/// descends; due date, process sequence, JobRef, piece identity close
/// it out — never DB insertion order, never random.</summary>
public static class ProductionQueueOrder
{
    public static IEnumerable<ProductionQueueItem> Order(this IEnumerable<ProductionQueueItem> queue) =>
        queue
            .OrderBy(q => q.Readiness == ProductionReadiness.Ready ? 0
                : q.Readiness == ProductionReadiness.InProgress ? 1
                : q.Readiness == ProductionReadiness.Blocked ? 3 : 2)
            .ThenByDescending(q => q.Priority)
            .ThenBy(q => q.DueDate ?? DateTime.MaxValue)
            .ThenBy(q => ProductionStages.Code(q.Stage))
            .ThenBy(q => q.JobRef, StringComparer.Ordinal)
            .ThenBy(q => q.SpecGroupNumber)
            .ThenBy(q => q.SerialNumber)
            .ThenBy(q => q.Position, StringComparer.Ordinal)
            .ThenBy(q => q.UniqueKey, StringComparer.Ordinal);
}

/// <summary>Progress at job/process/overall level — physical pieces,
/// not double-counted DGU components (WPF ProductionProgress).</summary>
public sealed class ProductionProgress
{
    public string JobRef { get; set; } = "";
    public string SpecName { get; set; } = "";
    public string BuildType { get; set; } = "";
    public int SpecGroupNumber { get; set; }
    public int SerialNumber { get; set; }

    public int Required { get; set; }
    public int Completed { get; set; }
    public int InProgress { get; set; }
    public int Blocked { get; set; }
    public int Paused { get; set; }
    public int Rework { get; set; }
    public int QcPending { get; set; }
    public int QcFailed { get; set; }
    public int OnHold { get; set; }
    public int Cancelled { get; set; }
    public int Remaining => Math.Max(0, Required - Completed);

    public double ProgressPct =>
        Required == 0 ? 0 : Math.Round((double)Completed / Required * 100, 1);

    public string Label => $"{Completed}/{Required} ({ProgressPct}%)";
}

public sealed class ProductionDashboardKpis
{
    public int JobsInProduction { get; set; }
    public int ReadyToStart { get; set; }
    public int InProgress { get; set; }
    public int OnHold { get; set; }
    public int Blocked { get; set; }
    public int QcPending { get; set; }
    public int QcFailed { get; set; }
    public int Rework { get; set; }
    public int CompletedToday { get; set; }
    public int ReadyForDelivery { get; set; }

    public int OverdueJobs { get; set; }
    public int DueSoon { get; set; }
    public int MaterialShortages { get; set; }

    public string BottleneckProcess { get; set; } = "";
    public int BottleneckCount { get; set; }

    public Dictionary<int, int> ProcessLoad { get; set; } = new();
}

public sealed class WorkstationLoad
{
    public string WorkstationId { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int ProcessType { get; set; }
    public string ProcessName { get; set; } = "";
    public int AssignedCount { get; set; }
    public int ReadyCount { get; set; }
    public int InProgressCount { get; set; }
    public int BlockedCount { get; set; }
}

public sealed class ProductionQueuePressure
{
    public string ProcessName { get; set; } = "";
    public int ProcessType { get; set; }
    public int ReadyQueue { get; set; }
    public int BlockedQueue { get; set; }
    public int OverdueQueue { get; set; }
    public int QcBacklog { get; set; }
    public int ReworkBacklog { get; set; }
    public double AvgWaitHours { get; set; }
    public int Total => ReadyQueue + BlockedQueue;

    public string PressureLabel => Total > 15 ? "High Pressure"
        : Total > 8 ? "Medium Pressure"
        : Total > 0 ? "Low Pressure"
        : "Idle";
}

/// <summary>Timing from existing timestamps — never fabricated (WPF
/// ProductionTimingService.EnrichTimingAsync, synchronous pure form).</summary>
public static class ProductionTiming
{
    public static void EnrichTiming(List<ProductionQueueItem> queue, DateTime now)
    {
        foreach (var item in queue)
        {
            if (item.Status == WorkItemStatuses.Pending)
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
    }
}

// ── assembly lineage + process details (WPF ProcessDetails/Factory) ──

/// <summary>One component of an assembly — the lineage record that keeps
/// a finished DGU traceable to BOTH glass pieces (WPF
/// AssemblyComponent).</summary>
public sealed class AssemblyComponent
{
    public string PieceId { get; set; } = "";
    public string GlassRef { get; set; } = "";
    public string Position { get; set; } = "";
    public int WorkItemId { get; set; }
    public string ExecutionId { get; set; } = "";
    public string Role { get; set; } = "";
}

/// <summary>The one-line process summary the work center shows (WPF
/// ProcessExecutionFactory.SummarizeDetail): a short operator-readable
/// digest of the per-process detail JSON, or the raw payload when the
/// detail is not one of the six known shapes.</summary>
public static class ProcessDetailSummary
{
    public static string Summarize(string json, int processType) => processType switch
    {
        2 => SummarizeCounts(json, "holes", "cutouts"),
        3 => SummarizePolish(json),
        4 => SummarizeText(json, "edgeDeleted"),
        5 => SummarizeText(json, "edges"),
        6 => SummarizeTempering(json),
        _ => ""
    };

    private static string SummarizeCounts(string json, string holesKey, string cutoutsKey)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(
                string.IsNullOrWhiteSpace(json) ? "{}" : json);
            int holes = doc.RootElement.TryGetProperty(holesKey, out var h) && h.ValueKind == System.Text.Json.JsonValueKind.Number ? h.GetInt32() : 0;
            int cutouts = doc.RootElement.TryGetProperty(cutoutsKey, out var c) && c.ValueKind == System.Text.Json.JsonValueKind.Number ? c.GetInt32() : 0;
            return $"{holes} hole(s), {cutouts} cutout(s)";
        }
        catch (System.Text.Json.JsonException) { return ""; }
    }

    private static string SummarizePolish(string json) => SummarizeText(json, "polishType");

    private static string SummarizeText(string json, string property)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(
                string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.TryGetProperty(property, out var v)
                && v.ValueKind == System.Text.Json.JsonValueKind.String
                ? v.GetString() ?? "" : "";
        }
        catch (System.Text.Json.JsonException) { return ""; }
    }

    private static string SummarizeTempering(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(
                string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.TryGetProperty("temperature", out var t)
                && t.ValueKind == System.Text.Json.JsonValueKind.Number
                ? $"{t.GetInt32()}°C" : "";
        }
        catch (System.Text.Json.JsonException) { return ""; }
    }
}
