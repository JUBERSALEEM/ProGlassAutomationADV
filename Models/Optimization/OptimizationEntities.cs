// ═══════════════════════════════════════════════════════════════════════
// FILE: Models/Optimization/OptimizationEntities.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization — persisted entities (design 2026-09-12)
//
// Sessions are stored as one row + JSON payloads (margins / stocks /
// pieces / result). The engine itself stays pure C# — persistence is a
// thin shell so saved runs can be reopened, re-verified and re-exported.
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.Models.Optimization
{
    /// <summary>A saved optimization run (reopenable, re-verifiable).</summary>
    public class SavedOptimizationSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;

        /// <summary>Confirmed Job Order references feeding the run (comma-separated; source lock).</summary>
        public string JobOrderRefs { get; set; } = string.Empty;

        /// <summary>QUICK or OPTIMIZE.</summary>
        public string EngineStage { get; set; } = "QUICK";
        public int EffortLevel { get; set; }
        public bool RotationEnabled { get; set; } = true;

        public int PieceCount { get; set; }
        public int SheetsUsed { get; set; }
        public int UnplacedCount { get; set; }
        public double UtilizationPct { get; set; }

        public string MarginsJson { get; set; } = string.Empty;
        public string StocksJson { get; set; } = string.Empty;
        public string PiecesJson { get; set; } = string.Empty;
        public string ResultJson { get; set; } = string.Empty;

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}
