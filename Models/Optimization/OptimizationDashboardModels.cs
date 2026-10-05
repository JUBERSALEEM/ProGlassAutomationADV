// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Optimization/OptimizationDashboardModels.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization — dashboard projections (Phase 28)
//
// Plain POCOs for the Optimization dashboard page. Same shape and same
// rules as the other dashboard models in Models/Dashboard: no
// INotifyPropertyChanged, because the owning ViewModel holds reactivity
// and rebuilds the whole collection on load.
//
// Every member here is a COLUMN THAT EXISTS on SavedOptimizationSession
// as a scalar. That is deliberate: SavedOptimizationSession also carries
// MarginsJson / StocksJson / PiecesJson / ResultJson, which are the full
// optimisation payload and can be megabytes for a large nest. A dashboard
// must never materialise them, so these models have nowhere to put them.
// See OptimizationDashboardService for the projection that enforces it.
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.Models.Optimization
{
    /// <summary>
    /// Whole-table aggregates for the Optimization dashboard, computed in
    /// SQL so row count never drives the cost of opening the page.
    /// </summary>
    public sealed class OptimizationDashboardKpis
    {
        /// <summary>Saved runs, excluding the reserved per-document latest-run rows.</summary>
        public int SavedRuns { get; set; }

        /// <summary>Total pieces across every saved run.</summary>
        public int TotalPieces { get; set; }

        /// <summary>Total sheets consumed across every saved run.</summary>
        public int TotalSheetsUsed { get; set; }

        /// <summary>Pieces the engine could not place. Non-zero is a real defect signal.</summary>
        public int TotalUnplaced { get; set; }

        /// <summary>Mean utilisation across saved runs, 0–100.</summary>
        public double AverageUtilizationPct { get; set; }

        /// <summary>Best utilisation achieved by any single run, 0–100.</summary>
        public double BestUtilizationPct { get; set; }

        /// <summary>Distinct engine stages used (QUICK / FULL / …).</summary>
        public int DistinctEngineStages { get; set; }

        /// <summary>Newest run timestamp, or null when nothing has ever been saved.</summary>
        public DateTime? LastRunUtc { get; set; }

        /// <summary>
        /// True only when the table was actually read and is genuinely
        /// empty. Distinct from "the read failed", which the page reports
        /// as an error rather than as a zero snapshot — the same rule
        /// <c>IndigoPresentationTests.Not_loaded_is_not_a_known_zero_snapshot</c>
        /// encodes for the chart models.
        /// </summary>
        public bool HasData => SavedRuns > 0;
    }

    /// <summary>
    /// One saved run, projected down to the six scalar columns the
    /// dashboard draws. Used for the recent-runs utilisation chart and
    /// the sheets-by-stage donut.
    /// </summary>
    public sealed class OptimizationRunPoint
    {
        public string Name { get; set; } = string.Empty;
        public string EngineStage { get; set; } = string.Empty;
        public int PieceCount { get; set; }
        public int SheetsUsed { get; set; }
        public int UnplacedCount { get; set; }
        public double UtilizationPct { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}
