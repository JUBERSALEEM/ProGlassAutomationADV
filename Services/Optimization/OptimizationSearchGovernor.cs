// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Optimization/OptimizationSearchGovernor.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization — adaptive search governor (2026-09-17)
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Globalization;

namespace ProGlassAutomation.Services.Optimization
{
    /// <summary>Search breadth tier for one cutting group.</summary>
    public enum SearchTier
    {
        /// <summary>≤ 2,000 parts: full search, unchanged.</summary>
        Full,
        /// <summary>2,001–20,000 parts: capped breadth, same orderings/splits.</summary>
        Narrowed,
        /// <summary>&gt; 20,000 parts: high-throughput grid search that still finishes.</summary>
        Minimum
    }

    /// <summary>
    /// What PackOptimize / PackQuick are allowed to run for one group.
    /// Caps default to "unbounded" so the FULL tier behaves exactly as
    /// before the governor existed.
    /// </summary>
    public sealed class SearchPlan
    {
        public SearchTier Tier { get; init; } = SearchTier.Full;

        /// <summary>Max deterministic-shuffle index per (order, split).</summary>
        public int ShuffleCap { get; init; } = int.MaxValue;

        /// <summary>Huge-job tier: every ordering × every split, zero
        /// shuffles. Windowed passes are cheap, so the job still gets a
        /// wide smart-search grid instead of a single guess.</summary>
        public bool ForceFullGrid { get; init; }

        /// <summary>Max simultaneously-open sheets per packing pass
        /// (industrial "active plates" window; MaxValue = unbounded).</summary>
        public int OpenSheetWindow { get; init; } = int.MaxValue;

        /// <summary>Splits attempted by sheet-fill / wall-seed (empty = skip).</summary>
        public SplitMode[] FillWallSplits { get; init; } = Array.Empty<SplitMode>();

        /// <summary>MaxRects orderings attempted (effort ≥ 3 only).</summary>
        public int MaxRectOrders { get; init; } = int.MaxValue;

        /// <summary>Consolidation outer sweeps per pass.</summary>
        public int MaxConsolidationSweeps { get; init; } = int.MaxValue;

        /// <summary>Donor attempts per consolidation (snapshot-bounded).</summary>
        public int MaxDonorAttempts { get; init; } = int.MaxValue;

        /// <summary>User-facing explanation (empty when FULL — nothing narrowed).</summary>
        public string Note { get; init; } = string.Empty;

        /// <summary>Short grid-cell tag: "narrowed search" / "high-throughput search".</summary>
        public string ShortTag { get; init; } = string.Empty;
    }

    public static class OptimizationSearchGovernor
    {
        /// <summary>Above this many parts (per group) the search narrows.</summary>
        public const int NarrowedAboveParts = 10000;

        /// <summary>Above this many parts (per group) the high-throughput plan runs.</summary>
        public const int MinimumAboveParts = 50000;

        public static SearchTier TierFor(int pieceCount) =>
            pieceCount > MinimumAboveParts ? SearchTier.Minimum
            : pieceCount > NarrowedAboveParts ? SearchTier.Narrowed
            : SearchTier.Full;

        private static readonly SplitMode[] AllSplits =
        {
            SplitMode.HorizontalFirst, SplitMode.VerticalFirst, SplitMode.LongerLeftover
        };

        private static readonly SplitMode[] FirstSplitOnly = { SplitMode.HorizontalFirst };

        private static string Count(int n) =>
            n.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>Search plan for PackOptimize (effort 1..5).</summary>
        public static SearchPlan PlanForOptimize(int pieceCount, int effortLevel)
        {
            var tier = TierFor(pieceCount);
            if (tier == SearchTier.Full)
            {
                return new SearchPlan
                {
                    Tier = tier,
                    FillWallSplits = AllSplits
                };
            }

            if (tier == SearchTier.Narrowed)
            {
                var caps = new List<string>();
                if (effortLevel >= 4) caps.Add("deterministic shuffles capped at 1");
                if (effortLevel >= 2) caps.Add("sheet-fill / wall-seed on one split");
                if (effortLevel >= 3) caps.Add("MaxRects orderings capped at 3");
                caps.Add("consolidation sweeps capped at 2");
                caps.Add("open-sheet window 256");
                return new SearchPlan
                {
                    Tier = tier,
                    ShuffleCap = 1,
                    FillWallSplits = FirstSplitOnly,
                    MaxRectOrders = 3,
                    MaxConsolidationSweeps = 2,
                    MaxDonorAttempts = 2000,
                    OpenSheetWindow = 256,
                    ShortTag = "narrowed search",
                    Note = $"Packing all {Count(pieceCount)} parts — search narrowed to finish " +
                           $"(over {Count(NarrowedAboveParts)}): " +
                           string.Join(", ", caps) +
                           ". All orderings and split rules are still tried."
                };
            }

            if (effortLevel <= 1)
            {
                return new SearchPlan
                {
                    Tier = tier,
                    ShuffleCap = 0,
                    FillWallSplits = Array.Empty<SplitMode>(),
                    MaxRectOrders = 0,
                    MaxConsolidationSweeps = 1,
                    MaxDonorAttempts = 400,
                    OpenSheetWindow = 128,
                    ShortTag = "high-throughput search",
                    Note = $"Packing all {Count(pieceCount)} parts — consolidation capped at " +
                           "1 sweep through a 128-sheet window to finish (single-pass level). " +
                           "Split the job into smaller groups for a deeper squeeze."
                };
            }

            var kept = new List<string> { "full order × split grid (18 candidates)", "128-sheet packing window" };
            if (effortLevel >= 3) kept.Add("1 MaxRects ordering");
            kept.Add("1 consolidation sweep");
            var dropped = new List<string>();
            if (effortLevel >= 4) dropped.Add("shuffles");
            if (effortLevel >= 2) dropped.Add("sheet-fill / wall-seed");
            return new SearchPlan
            {
                Tier = tier,
                ShuffleCap = 0,
                ForceFullGrid = true,
                FillWallSplits = Array.Empty<SplitMode>(),
                MaxRectOrders = 1,
                MaxConsolidationSweeps = 1,
                MaxDonorAttempts = 400,
                OpenSheetWindow = 128,
                ShortTag = "high-throughput search",
                Note = $"Packing all {Count(pieceCount)} parts — high-throughput search " +
                       $"(over {Count(MinimumAboveParts)}): " +
                       string.Join(", ", kept) + " — no " + string.Join(", no ", dropped) +
                       ": those are quadratic in the job size and would not finish. " +
                       "Split the job into smaller groups for a deeper search."
            };
        }

        /// <summary>Search plan for PackQuick.</summary>
        public static SearchPlan PlanForQuick(int pieceCount)
        {
            var tier = TierFor(pieceCount);
            if (tier == SearchTier.Full)
                return new SearchPlan { Tier = tier };

            if (tier == SearchTier.Narrowed)
            {
                return new SearchPlan
                {
                    Tier = tier,
                    MaxConsolidationSweeps = 2,
                    MaxDonorAttempts = 2000,
                    OpenSheetWindow = 256,
                    ShortTag = "narrowed search",
                    Note = $"Packing all {Count(pieceCount)} parts — consolidation capped at " +
                           "2 sweeps through a 256-sheet window to finish."
                };
            }

            return new SearchPlan
            {
                Tier = tier,
                MaxConsolidationSweeps = 1,
                MaxDonorAttempts = 400,
                OpenSheetWindow = 128,
                ShortTag = "high-throughput search",
                Note = $"Packing all {Count(pieceCount)} parts — consolidation capped at " +
                       "1 sweep through a 128-sheet window to finish. " +
                       "Split the job into smaller groups for a deeper squeeze."
            };
        }
    }
}
