// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Optimization/OptimizationDashboardService.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization — dashboard aggregates (Phase 28)
//
// Static service mirroring Services/Production/Control/ProductionDashboardService.cs,
// which is the existing precedent for a module-local dashboard feed.
//
// WHY THIS EXISTS INSTEAD OF REUSING OptimizationSessionRepository.ListAsync
// -------------------------------------------------------------------------
// ListAsync returns whole SavedOptimizationSession entities for the newest
// 100 rows. Each entity carries MarginsJson, StocksJson, PiecesJson and
// ResultJson — the complete optimisation payload, which for a real nest is
// hundreds of KB to megabytes per row. A dashboard that only needs four
// totals and twelve small rows must not pay for any of it.
//
// So every query here is a PROJECTION:
//   • the totals are computed by SQL (COUNT / SUM / AVG / MAX), so the row
//     count of optimization.db never affects how much is transferred;
//   • the recent-runs list Select()s six scalar columns and is capped by
//     RecentRunLimit, and SavedOptimizationSession.CreatedUtc is indexed
//     (OptimizationDbContext.OnModelCreating) so the ORDER BY … LIMIT is an
//     index scan, not a sort of the whole table;
//   • nothing is tracked.
//
// The reserved per-document "latest run" rows are excluded. Those are
// written by UpsertLatestRunAsync under ids starting
// "__latest_run__:" (SavedOptimizationSession.LatestRunIdPrefix) and are
// COPIES of a real run kept for fast lookup, so counting them would
// double-count both the run total and every sum.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Optimization;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Optimization
{
    public static class OptimizationDashboardService
    {
        /// <summary>
        /// How many recent runs the utilisation chart draws. Bounded so the
        /// page costs the same on a machine with 50 runs as on one with
        /// 5,000; the chart is a trend, not a table, and 12 bars is the most
        /// that stay legible at the card width.
        /// </summary>
        public const int RecentRunLimit = 12;

        /// <summary>
        /// One round trip for the aggregates and one for the recent runs.
        /// Never throws for an empty table — an empty database yields
        /// <see cref="OptimizationDashboardKpis.HasData"/> == false, which the
        /// page renders as an empty state. A database that cannot be read is
        /// the caller's error path, not a zero snapshot.
        /// </summary>
        public static async Task<(OptimizationDashboardKpis Kpis, List<OptimizationRunPoint> RecentRuns)>
            GetSnapshotAsync(CancellationToken ct = default)
        {
            await using var db = new OptimizationDbContext();

            // Idempotent, and required: optimization.db is created on first
            // use by the repository, so a machine that has never run the
            // optimiser has no file yet. Without this the first aggregate
            // query throws "no such table" and the dashboard shows an error
            // where an empty state belongs.
            await db.Database.EnsureCreatedAsync(ct).ConfigureAwait(false);

            // Excludes the reserved latest-run copies. StartsWith translates
            // to a LIKE/instr test; even read literally with "_" as a LIKE
            // wildcard it cannot match a real id, because ids are
            // Guid.ToString("N") — 32 hex characters, so the literal
            // "latest_run" substring the prefix requires can never occur.
            var real = db.Sessions
                .AsNoTracking()
                .Where(s => !s.Id.StartsWith(SavedOptimizationSession.LatestRunIdPrefix));

            // Each figure is its own COUNT / SUM / AVG / MAX call rather than
            // one grouped projection. They are individually documented EF
            // methods whose SQL translation is not in question, and this
            // dashboard loads once per tab visit against a small table, so the
            // extra round trips cost nothing measurable. (A single
            // GroupBy(_ => 1) aggregate would also work, but nothing here can
            // be executed to prove the translation, and a runtime
            // InvalidOperationException on first open is a worse trade than
            // four indexed queries.)
            var kpis = new OptimizationDashboardKpis
            {
                SavedRuns = await real.CountAsync(ct).ConfigureAwait(false)
            };

            if (kpis.SavedRuns > 0)
            {
                kpis.TotalPieces = await real.SumAsync(s => s.PieceCount, ct).ConfigureAwait(false);
                kpis.TotalSheetsUsed = await real.SumAsync(s => s.SheetsUsed, ct).ConfigureAwait(false);
                kpis.TotalUnplaced = await real.SumAsync(s => s.UnplacedCount, ct).ConfigureAwait(false);
                kpis.AverageUtilizationPct = Math.Round(
                    await real.AverageAsync(s => s.UtilizationPct, ct).ConfigureAwait(false), 1);
                kpis.BestUtilizationPct = Math.Round(
                    await real.MaxAsync(s => s.UtilizationPct, ct).ConfigureAwait(false), 1);
                kpis.LastRunUtc = await real.MaxAsync(s => (DateTime?)s.CreatedUtc, ct).ConfigureAwait(false);
                kpis.DistinctEngineStages = await real
                    .Select(s => s.EngineStage)
                    .Distinct()
                    .CountAsync(ct)
                    .ConfigureAwait(false);
            }

            var recent = await real
                .OrderByDescending(s => s.CreatedUtc)
                .Take(RecentRunLimit)
                .Select(s => new OptimizationRunPoint
                {
                    Name = s.Name,
                    EngineStage = s.EngineStage,
                    PieceCount = s.PieceCount,
                    SheetsUsed = s.SheetsUsed,
                    UnplacedCount = s.UnplacedCount,
                    UtilizationPct = s.UtilizationPct,
                    CreatedUtc = s.CreatedUtc
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return (kpis, recent);
        }
    }
}
