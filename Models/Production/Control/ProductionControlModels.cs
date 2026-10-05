// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Production/Control/ProductionControlModels.cs
// PROJECT: ProGlassAutomation
// MODULE: Production — Phase 17 Production Control & Factory Readiness
//   Conceptual model: Production Release → Production Route → Production Work Items
//   → Readiness Engine → Production Queue → Operator/Workstation → Execution → QC → Next Queue → Ready
//   Control layer observes and coordinates existing work, does not duplicate execution records.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.Production;
using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Models.Production.Control
{
    /// <summary>Deterministic readiness — not simply Status==Pending.</summary>
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

    /// <summary>Unified production queue item — aggregates existing WorkItems, no duplicate jobs.</summary>
    public sealed class ProductionQueueItem
    {
        // Identity
        public int WorkItemId { get; set; }
        public string UniqueKey { get; set; } = string.Empty;
        public string JobRef { get; set; } = string.Empty;
        public string SourceProformaId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string Salesman { get; set; } = string.Empty;
        public int SpecGroupNumber { get; set; }
        public string SpecName { get; set; } = string.Empty;
        public string BuildType { get; set; } = string.Empty;
        public int SerialNumber { get; set; }
        public string GlassRef { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public int WidthMm { get; set; }
        public int HeightMm { get; set; }
        public int Qty { get; set; }
        public int QtyDone { get; set; }
        public int Remaining => Math.Max(0, Qty - QtyDone);

        // Process
        public ProductionStage Stage { get; set; }
        public int StageInt => (int)Stage;
        public string StageLabel { get; set; } = string.Empty;
        public ProductionStage? PreviousStage { get; set; }
        public ProductionStage? NextStage { get; set; }
        public List<ProductionStage> RequiredStages { get; set; } = new();

        // Status
        public WorkItemStatus Status { get; set; }
        public ProductionReadiness Readiness { get; set; }
        public string BlockReason { get; set; } = string.Empty;
        public bool IsBlocked => Readiness == ProductionReadiness.Blocked;
        public bool IsReady => Readiness == ProductionReadiness.Ready;

        // Priority / DueDate
        public ProductionPriority Priority { get; set; } = ProductionPriority.Normal;
        public DateTime? DueDate { get; set; }
        public DueDateRisk DueRisk { get; set; } = DueDateRisk.NoDate;
        public int PriorityWeight => (int)Priority;

        // QC / Rework / Breakage / FOC
        public int QcResult { get; set; } // 0 Pending 1 Passed 2 Failed
        public string QcId { get; set; } = string.Empty;
        public bool IsRework { get; set; }
        public bool IsFoc { get; set; }
        public bool IsCancelled { get; set; }
        public int ReplacementSequence { get; set; }

        // Operator / Workstation
        public string Operator { get; set; } = string.Empty;
        public string WorkstationId { get; set; } = string.Empty;
        public string WorkstationName { get; set; } = string.Empty;
        public string AssignedBy { get; set; } = string.Empty;
        public DateTime? AssignedAt { get; set; }

        // Hold
        public bool IsOnHold { get; set; }
        public string HoldReason { get; set; } = string.Empty;
        public string HeldBy { get; set; } = string.Empty;
        public DateTime? HeldAt { get; set; }

        // Material
        public int MaterialRequired { get; set; }
        public int MaterialReserved { get; set; }
        public int MaterialAllocated { get; set; }
        public int MaterialConsumed { get; set; }
        public int MaterialAvailable { get; set; }
        public int MaterialShortage { get; set; }
        public bool HasMaterialShortage => MaterialShortage > 0;

        // Timing
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string CompletedBy { get; set; } = string.Empty;
        public TimeSpan? QueueWaitTime { get; set; }
        public TimeSpan? ProcessingDuration { get; set; }

        // Traceability
        public string TraceabilityLabel { get; set; } = string.Empty;
        public string PreviousLabel { get; set; } = string.Empty;
        public string CurrentLabel { get; set; } = string.Empty;
        public string NextLabel { get; set; } = string.Empty;

        // DGU specific
        public string OuterPieceId { get; set; } = string.Empty;
        public string InnerPieceId { get; set; } = string.Empty;
        public string PairId { get; set; } = string.Empty;
        public string AssemblyId { get; set; } = string.Empty;

        // Notes
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class ProductionProgress
    {
        public string JobRef { get; set; } = string.Empty;
        public string SpecName { get; set; } = string.Empty;
        public string BuildType { get; set; } = string.Empty;
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

        public double ProgressPct => Required == 0 ? 0 : Math.Round((double)Completed / Required * 100, 1);

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

        public string BottleneckProcess { get; set; } = string.Empty;
        public int BottleneckCount { get; set; }

        public Dictionary<int, int> ProcessLoad { get; set; } = new();
    }

    public sealed class WorkstationLoad
    {
        public string WorkstationId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int ProcessType { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public int AssignedCount { get; set; }
        public int ReadyCount { get; set; }
        public int InProgressCount { get; set; }
        public int BlockedCount { get; set; }
    }

    public sealed class ProductionQueuePressure
    {
        public string ProcessName { get; set; } = string.Empty;
        public int ProcessType { get; set; }
        public int ReadyQueue { get; set; }
        public int BlockedQueue { get; set; }
        public int OverdueQueue { get; set; }
        public int QcBacklog { get; set; }
        public int ReworkBacklog { get; set; }
        public double AvgWaitHours { get; set; }
        public int Total => ReadyQueue + BlockedQueue;

        public string PressureLabel => Total > 15 ? "High Pressure" : Total > 8 ? "Medium Pressure" : Total > 0 ? "Low Pressure" : "Idle";
    }
}
