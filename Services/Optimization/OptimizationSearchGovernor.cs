// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Optimization/OptimizationSearchGovernor.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization — Adaptive Industrial Resource & Search Governor
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Globalization;

namespace ProGlassAutomation.Services.Optimization
{
    /// <summary>Search breadth tier determined adaptively by workload and available resources.</summary>
    public enum SearchTier
    {
        /// <summary>Standard scale: deep exhaustive search.</summary>
        Full,
        /// <summary>Medium-large scale: deep bounded search with candidate indexing.</summary>
        Narrowed,
        /// <summary>Massive industrial scale (50K - 1M+): stratified partitioned search with active reservoirs.</summary>
        Minimum
    }

    /// <summary>
    /// Governs optimization strategies, resource allocations, and search bounds for a cutting run.
    /// Completely eliminates fixed part-count ceilings in favor of dynamic hardware-aware scaling.
    /// </summary>
    public sealed class SearchPlan
    {
        public SearchTier Tier { get; init; } = SearchTier.Full;

        /// <summary>Max deterministic-shuffle index per (order, split).</summary>
        public int ShuffleCap { get; init; } = int.MaxValue;

        /// <summary>Whether to evaluate the full multi-strategy grid across all orderings and splits.</summary>
        public bool ForceFullGrid { get; init; } = true;

        /// <summary>Max simultaneously-open sheets per packing pass.</summary>
        public int OpenSheetWindow { get; init; } = int.MaxValue;

        /// <summary>Splits attempted by sheet-fill / wall-seed.</summary>
        public SplitMode[] FillWallSplits { get; init; } = Array.Empty<SplitMode>();

        /// <summary>MaxRects orderings attempted (effort ≥ 3).</summary>
        public int MaxRectOrders { get; init; } = int.MaxValue;

        /// <summary>Consolidation outer sweeps per pass.</summary>
        public int MaxConsolidationSweeps { get; init; } = int.MaxValue;

        /// <summary>Donor attempts per consolidation.</summary>
        public int MaxDonorAttempts { get; init; } = int.MaxValue;

        /// <summary>Batch target size for partitioned adaptive execution.</summary>
        public int BatchTargetSize { get; init; } = 1500;

        /// <summary>Candidate selection window size for spatial retrieval.</summary>
        public int CandidateWindowSize { get; init; } = 500;

        /// <summary>User-facing explanation.</summary>
        public string Note { get; init; } = string.Empty;

        /// <summary>Short grid-cell tag.</summary>
        public string ShortTag { get; init; } = string.Empty;
    }

    public static class OptimizationSearchGovernor
    {
        private static readonly SplitMode[] AllSplits =
        {
            SplitMode.HorizontalFirst, SplitMode.VerticalFirst, SplitMode.LongerLeftover
        };

        private static readonly SplitMode[] FirstSplitOnly = { SplitMode.HorizontalFirst };

        private static string Count(int n) =>
            n.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>
        /// Adaptively derives search parameters based on piece count, hardware concurrency, and requested effort.
        /// Scales continuously from 100 to 1,000,000+ parts without reverting to un-optimized shelf packing.
        /// </summary>
        public static SearchPlan PlanForOptimize(int pieceCount, int effortLevel)
        {
            int cpuCores = Environment.ProcessorCount;

            // Tier 1: Small/Medium Workloads (<= 2,000 parts)
            if (pieceCount <= 2000)
            {
                return new SearchPlan
                {
                    Tier = SearchTier.Full,
                    FillWallSplits = AllSplits,
                    ShuffleCap = effortLevel >= 5 ? 6 : (effortLevel >= 4 ? 3 : 0),
                    ForceFullGrid = true,
                    MaxRectOrders = effortLevel >= 5 ? 6 : 3,
                    MaxConsolidationSweeps = int.MaxValue,
                    OpenSheetWindow = int.MaxValue,
                    BatchTargetSize = 2000,
                    CandidateWindowSize = int.MaxValue,
                    ShortTag = "Full Search",
                    Note = $"{Count(pieceCount)} parts: full multi-strategy search at effort {effortLevel}."
                };
            }

            // Tier 2: Large Workloads (2,001 - 15,000 parts)
            if (pieceCount <= 15000)
            {
                return new SearchPlan
                {
                    Tier = SearchTier.Narrowed,
                    FillWallSplits = effortLevel >= 3 ? AllSplits : FirstSplitOnly,
                    ShuffleCap = effortLevel >= 4 ? 2 : 0,
                    ForceFullGrid = true,
                    MaxRectOrders = effortLevel >= 4 ? 3 : 1,
                    MaxConsolidationSweeps = 4,
                    MaxDonorAttempts = 3000,
                    OpenSheetWindow = int.MaxValue,
                    BatchTargetSize = Math.Min(2500, Math.Max(1200, pieceCount / cpuCores)),
                    CandidateWindowSize = 600,
                    ShortTag = "Adaptive Bounded Search",
                    Note = $"Packing {Count(pieceCount)} parts — adaptive bounded multi-candidate grid with indexed candidate retrieval."
                };
            }

            // Tier 3: Industrial High-Volume Workloads (15,001 - 1,000,000+ parts)
            // Never disables optimization: all plates remain open to all size strata for maximum nesting yield
            int adaptiveBatchSize = Math.Clamp(pieceCount / (cpuCores * 2), 1200, 3000);

            return new SearchPlan
            {
                Tier = SearchTier.Minimum,
                FillWallSplits = AllSplits,
                ShuffleCap = effortLevel >= 5 ? 3 : (effortLevel >= 4 ? 2 : 0),
                ForceFullGrid = true,
                MaxRectOrders = effortLevel >= 4 ? 3 : 1,
                MaxConsolidationSweeps = 3,
                MaxDonorAttempts = 2000,
                OpenSheetWindow = int.MaxValue,
                BatchTargetSize = adaptiveBatchSize,
                CandidateWindowSize = 500,
                ShortTag = "Stratified Industrial Search",
                Note = $"Packing {Count(pieceCount)} parts across full open-plate strata with deep tail consolidation."
            };
        }

        /// <summary>Search plan for PackQuick.</summary>
        public static SearchPlan PlanForQuick(int pieceCount)
        {
            return new SearchPlan
            {
                Tier = pieceCount <= 2000 ? SearchTier.Full : SearchTier.Narrowed,
                OpenSheetWindow = int.MaxValue,
                MaxConsolidationSweeps = pieceCount <= 2000 ? 3 : 2,
                MaxDonorAttempts = 1500,
                ShortTag = pieceCount <= 2000 ? "Quick Single-Pass" : "Adaptive Quick Pass",
                Note = pieceCount <= 2000 ? "" : $"Packing {Count(pieceCount)} parts with full open-plate quick pass."
            };
        }
    }
}
