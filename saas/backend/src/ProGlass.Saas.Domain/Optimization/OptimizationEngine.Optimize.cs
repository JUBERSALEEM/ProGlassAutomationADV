// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27), Phase 25a:
//   Services/Optimization/OptimizationSearchGovernor.cs (437 lines —
//     SearchTier, SearchPlan, the adaptive governor: PlanForOptimize,
//     PlanForQuick, RecommendEffort, BudgetFor, the memory-pressure
//     guard, WithReducedBreadth)
//   Services/Optimization/OptimizationModels.cs — PlatePattern,
//     CutComplexity, CutPolicy, ScaleTier, ScaleProfile, OptimizeProgress
//   Services/Optimization/GuillotinePacker.cs — the OPTIMIZE stage
//     chassis: OrderBy + Sort (6 orderings) + ShuffleDeterministic,
//     the Better() comparison chain (Utilization/TotalCuts/
//     DistinctStockSizes/StockSwitches/LargestLeftover/
//     UtilizationSpread), PackingContext (bounded open-sheet window),
//     ConsolidateAll + SnapshotBeforeWrite, the verification oracles
//     (Verify, VerifyComprehensive, AuditBreakout, ValidateResult,
//     CheckCandidate, Label), TryDeriveGuillotineCuts/DeriveCuts/
//     SplitAt/AllSheetsCuttable, and PackOptimize itself.
//
// SCOPE (documented in WPF_TO_SAAS_MAPPING §1.6b): this slice carries
// the multi-start GRID — every (ordering × split × deterministic
// shuffle) candidate with the governor's budget/tier machinery, the
// parallel evaluation switch, the guillotine-acceptance gate, the time
// budget, the validation safety nets and cut-sequence derivation. The
// WPF's additional candidate FAMILIES are later slices, each
// documented: capacity strips (RunCapacityStrips), bounded best-split
// pattern beam (ImprovePatterns/RunBestSplit/RunBestSplitBeam),
// sheet-fill/wall-seed/MaxRects (Advanced), smart placement
// (Smart/SmartPlacement), repeated patterns + the industrial pattern
// master (RepeatedPatterns/RepeatMaster/RepeatInteger/RepeatPricing/
// RepeatTail/PatternSearch/FillFirstPlate/Partitioned), offcut reuse
// (OffcutReuse), local search (LocalSearch), staged cut policies
// (ValidateCutPattern enforcement) and instrumentation. Because
// Better() keeps the minimum, a grid-only OPTIMIZE is still a valid
// deterministic improvement over QUICK — never worse, sometimes equal,
// until the remaining candidates land.
// ═══════════════════════════════════════════════════════════════════════

using System.Globalization;

namespace ProGlass.Saas.Domain.Optimization;

/// <summary>How the operator wants the plates balanced (WPF
/// PlatePattern). Unplaced count and sheet count always dominate — the
/// pattern only decides between plans that are already equal on
/// both.</summary>
public enum PlatePattern
{
    /// <summary>Fewest unplaced, then fewest sheets, then utilization.
    /// An equal sheet count keeps the incumbent.</summary>
    Yield = 0,
    /// <summary>Same sheet and unplaced limits, then the fuller leading
    /// sheet.</summary>
    FillFirstPlate = 1,
    /// <summary>Same sheet and unplaced limits, then the plan with fewer
    /// guillotine cuts. Decides between plans that already use the same
    /// number of sheets — it never buys a cut by adding a sheet.</summary>
    FewestCuts = 2,
    /// <summary>Same sheet and unplaced limits, then fewer distinct stock
    /// sizes and fewer switches between them. Every switch is a rack move
    /// and a setup on the floor.</summary>
    MinimiseStockChanges = 3,
    /// <summary>Same sheet and unplaced limits, then the plan that pushes
    /// its leftover onto ONE sheet, so the waste is big enough to come off
    /// as a usable remnant instead of several slivers.</summary>
    RemnantFirst = 4,
    /// <summary>Same sheet and unplaced limits, then the plan whose sheets
    /// are most evenly loaded — three even plates beat two full ones and a
    /// nearly empty one.</summary>
    BalancedSpread = 5
}

/// <summary>Staged-cut complexity the operator asked for (WPF
/// CutComplexity).</summary>
public enum CutComplexity
{
    /// <summary>Unlimited guillotine depth, no trim cuts. Today's
    /// behaviour and the default.</summary>
    FreeGuillotine = 0,
    /// <summary>Two stages, first cut horizontal (along the sheet width),
    /// producing stacked strips.</summary>
    SimpleFirstX = 1,
    /// <summary>Two stages, first cut vertical (along the sheet height),
    /// producing side-by-side columns.</summary>
    SimpleFirstY = 2,
    /// <summary>Two stages, first direction chosen per sheet (2SGP).</summary>
    Simple = 3,
    /// <summary>Three stages.</summary>
    ThreeStage = 4,
    /// <summary>Unlimited depth plus trim cuts to release remnants.</summary>
    ComplexTrims = 5
}

/// <summary>The cut pattern a plan must hold (WPF CutPolicy). Free is
/// the only policy under which existing calibration figures are
/// guaranteed exact.</summary>
public sealed class CutPolicy
{
    public static readonly CutPolicy Free =
        new(CutComplexity.FreeGuillotine, int.MaxValue, false, false, false, 0);

    private CutPolicy(CutComplexity complexity, int maxStages, bool forceFirstHorizontal,
        bool forceFirstVertical, bool allowTrimCuts, int minTrimWidthMm)
    {
        Complexity = complexity;
        MaxStages = maxStages;
        ForceFirstCutHorizontal = forceFirstHorizontal;
        ForceFirstCutVertical = forceFirstVertical;
        AllowTrimCuts = allowTrimCuts;
        MinTrimWidthMm = minTrimWidthMm;
    }

    public CutComplexity Complexity { get; }
    public int MaxStages { get; }
    public bool ForceFirstCutHorizontal { get; }
    public bool ForceFirstCutVertical { get; }
    public bool AllowTrimCuts { get; }
    public int MinTrimWidthMm { get; }

    /// <summary>True when the pattern limits how deep the guillotine tree
    /// may grow. Only these levels are subject to staged enforcement; the
    /// free levels take no constraint path at all.</summary>
    public bool IsDepthConstrained => MaxStages < int.MaxValue;

    /// <summary>Resolves a complexity level into the numbers the packer
    /// reads (WPF CutPolicy.For).</summary>
    public static CutPolicy For(CutComplexity complexity, int minTrimWidthMm = 0)
    {
        var width = minTrimWidthMm > 0 ? minTrimWidthMm : 0;
        switch (complexity)
        {
            case CutComplexity.SimpleFirstX:
                return new CutPolicy(complexity, 2, true, false, false, 0);
            case CutComplexity.SimpleFirstY:
                return new CutPolicy(complexity, 2, false, true, false, 0);
            case CutComplexity.Simple:
                return new CutPolicy(complexity, 2, false, false, false, 0);
            case CutComplexity.ThreeStage:
                return new CutPolicy(complexity, 3, false, false, false, 0);
            case CutComplexity.ComplexTrims:
                return new CutPolicy(complexity, int.MaxValue, false, false, true, width);
            default:
                return Free;
        }
    }
}

/// <summary>Progress checkpoint reported by the optimizer (WPF
/// OptimizeProgress). Emitted between candidate passes.</summary>
public sealed class OptimizeProgress
{
    public string Stage { get; init; } = "";
    public int Completed { get; init; }
    public int Total { get; init; }
    public string Message { get; init; } = "";

    public double Fraction => Total <= 0
        ? 0d
        : Math.Min(1d, (double)Completed / Total);

    public static OptimizeProgress Of(string stage, int completed, int total, string message = "") =>
        new() { Stage = stage, Completed = completed, Total = total, Message = message };
}

/// <summary>Search breadth tier determined adaptively by workload and
/// available resources (WPF SearchTier).</summary>
public enum SearchTier
{
    /// <summary>Standard scale: deep exhaustive search.</summary>
    Full,
    /// <summary>Medium-large scale: deep bounded search with candidate
    /// indexing.</summary>
    Narrowed,
    /// <summary>Massive industrial scale (50K – 1M+): stratified
    /// partitioned search with active reservoirs.</summary>
    Minimum
}

/// <summary>Governs search strategies, resource allocations, and search
/// bounds for a cutting run (WPF SearchPlan — immutable, transformed
/// only through CopyWith).</summary>
public sealed class SearchPlan
{
    public SearchTier Tier { get; init; } = SearchTier.Full;

    /// <summary>Max deterministic-shuffle index per (order, split).</summary>
    public int ShuffleCap { get; init; } = int.MaxValue;

    /// <summary>Whether to evaluate the full multi-strategy grid across
    /// all orderings and splits.</summary>
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

    /// <summary>Wall-clock budget for one cutting group's search. When it
    /// engages, the search stops adding candidates and keeps the best
    /// plan found so far — a usable plan beats an unfinished search.</summary>
    public TimeSpan TimeBudget { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Memory the machine had available when this plan was
    /// built, in bytes. Zero means the platform did not report it.</summary>
    public long ObservedAvailableMemoryBytes { get; init; }

    private SearchPlan CopyWith(
        SearchTier? tier = null,
        int? shuffleCap = null,
        bool? forceFullGrid = null,
        int? openSheetWindow = null,
        SplitMode[]? fillWallSplits = null,
        int? maxRectOrders = null,
        int? maxConsolidationSweeps = null,
        int? maxDonorAttempts = null,
        int? batchTargetSize = null,
        int? candidateWindowSize = null,
        TimeSpan? timeBudget = null,
        long? observedMemory = null,
        string? shortTag = null,
        string? note = null) => new()
        {
            Tier = tier ?? Tier,
            ShuffleCap = shuffleCap ?? ShuffleCap,
            ForceFullGrid = forceFullGrid ?? ForceFullGrid,
            OpenSheetWindow = openSheetWindow ?? OpenSheetWindow,
            FillWallSplits = fillWallSplits ?? FillWallSplits,
            MaxRectOrders = maxRectOrders ?? MaxRectOrders,
            MaxConsolidationSweeps = maxConsolidationSweeps ?? MaxConsolidationSweeps,
            MaxDonorAttempts = maxDonorAttempts ?? MaxDonorAttempts,
            BatchTargetSize = batchTargetSize ?? BatchTargetSize,
            CandidateWindowSize = candidateWindowSize ?? CandidateWindowSize,
            TimeBudget = timeBudget ?? TimeBudget,
            ObservedAvailableMemoryBytes = observedMemory ?? ObservedAvailableMemoryBytes,
            ShortTag = shortTag ?? ShortTag,
            Note = note ?? Note
        };

    /// <summary>Records the wall-clock budget for this group's search.</summary>
    public SearchPlan WithTimeBudget(TimeSpan budget) => CopyWith(timeBudget: budget);

    /// <summary>Records what the memory probe saw, without changing
    /// breadth.</summary>
    public SearchPlan WithObservedMemory(long bytes) => CopyWith(observedMemory: bytes);

    /// <summary>A narrower copy: the search gives up breadth before the
    /// allocator gives up entirely (WPF WithReducedBreadth).</summary>
    public SearchPlan WithReducedBreadth(string reason) => CopyWith(
        tier: Tier == SearchTier.Full ? SearchTier.Narrowed
            : Tier == SearchTier.Narrowed ? SearchTier.Minimum
            : SearchTier.Minimum,
        shuffleCap: Math.Min(ShuffleCap, 1),
        forceFullGrid: false,
        openSheetWindow: Math.Min(OpenSheetWindow, 400),
        fillWallSplits: FillWallSplits.Length > 1 ? new[] { FillWallSplits[0] } : FillWallSplits,
        maxRectOrders: Math.Min(MaxRectOrders, 1),
        maxConsolidationSweeps: Math.Min(MaxConsolidationSweeps, 2),
        maxDonorAttempts: Math.Min(MaxDonorAttempts, 1000),
        candidateWindowSize: Math.Min(CandidateWindowSize, 400),
        shortTag: ShortTag + " · memory-narrowed",
        note: string.IsNullOrEmpty(Note) ? reason : Note + " " + reason);
}

/// <summary>
/// The adaptive industrial resource &amp; search governor (WPF
/// OptimizationSearchGovernor, verbatim). Eliminates fixed part-count
/// ceilings in favour of dynamic hardware-aware scaling.
/// </summary>
public static class OptimizationSearchGovernor
{
    private static readonly SplitMode[] AllSplits =
    {
        SplitMode.HorizontalFirst, SplitMode.VerticalFirst, SplitMode.LongerLeftover
    };

    private static readonly SplitMode[] FirstSplitOnly = { SplitMode.HorizontalFirst };

    private static string Count(int n) =>
        n.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>Conservative working-set estimate per expanded part
    /// (1 KB — deliberately pessimistic; at this figure 2,000 parts need
    /// 2 MB and 1,000,000 parts need ~1 GB, where a tier drop is exactly
    /// the right response).</summary>
    internal const long EstimatedBytesPerPart = 1024;

    /// <summary>Memory the machine can still give this process, in bytes,
    /// or 0 when the platform does not report it. Never throws.</summary>
    public static long GetAvailableMemoryBytes()
    {
        try
        {
            var info = GC.GetGCMemoryInfo();
            long total = info.TotalAvailableMemoryBytes;
            long load = info.MemoryLoadBytes;

            if (total > 0 && load > 0 && total > load) return total - load;
            if (total > 0) return total;
        }
        catch
        {
            // A failed probe degrades to "no memory-driven narrowing",
            // not to a crashed run.
        }

        return 0;
    }

    /// <summary>Narrows the plan when the estimated working set will not
    /// comfortably fit (threshold: half of available memory — the
    /// estimate is rough and the process is not the only consumer).</summary>
    private static SearchPlan ApplyMemoryPressure(SearchPlan plan, int pieceCount)
    {
        long available = GetAvailableMemoryBytes();
        if (available <= 0) return plan.WithObservedMemory(available);

        long estimatedNeed = (long)pieceCount * EstimatedBytesPerPart;
        if (estimatedNeed <= available / 2) return plan.WithObservedMemory(available);

        string reason = string.Format(CultureInfo.InvariantCulture,
            "MEMORY GUARD: {0} parts need roughly {1:F1} GB of working set and only {2:F1} GB " +
            "was available, so the search was narrowed a tier to finish instead of exhausting " +
            "memory. Close other applications or split the job for the full search.",
            Count(pieceCount),
            estimatedNeed / 1_073_741_824.0,
            available / 1_073_741_824.0);

        return plan.WithReducedBreadth(reason).WithObservedMemory(available);
    }

    /// <summary>Adaptively derives search parameters from piece count,
    /// hardware concurrency and requested effort (WPF verbatim).</summary>
    public static SearchPlan PlanForOptimize(int pieceCount, int effortLevel)
    {
        var core = PlanForOptimizeCore(pieceCount, effortLevel);
        return ApplyMemoryPressure(core.WithTimeBudget(BudgetFor(core.Tier, effortLevel)), pieceCount);
    }

    /// <summary>Wall-clock budget for one cutting group, by tier and
    /// effort. Generous by design.</summary>
    private static TimeSpan BudgetFor(SearchTier tier, int effortLevel) => tier switch
    {
        SearchTier.Full => effortLevel >= 5 ? TimeSpan.FromMinutes(25)
                        : effortLevel >= 4 ? TimeSpan.FromMinutes(15)
                        : effortLevel >= 3 ? TimeSpan.FromMinutes(8)
                        : TimeSpan.FromMinutes(5),
        SearchTier.Narrowed => TimeSpan.FromMinutes(20),
        _ => TimeSpan.FromMinutes(40)
    };

    private static SearchPlan PlanForOptimizeCore(int pieceCount, int effortLevel)
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

    /// <summary>An existing stage, chosen from the cutting list. It does
    /// not invent a search and does not choose the plate pattern.</summary>
    public readonly struct EffortRecommendation
    {
        public int Stage { get; init; }
        public string Reason { get; init; }

        public EffortRecommendation()
        {
            Stage = 0;
            Reason = string.Empty;
        }
    }

    /// <summary>Same full-search ceiling as PlanForOptimizeCore and the
    /// partitioned handoff.</summary>
    public const int FullSearchPieceLimit = 2000;

    /// <summary>Same narrowed-search ceiling as PlanForOptimizeCore.</summary>
    public const int NarrowedSearchPieceLimit = 15000;

    /// <summary>Same industrial handoff as the packer. The small-job grid
    /// is not run at this count.</summary>
    public const int IndustrialPieceLimit = 100000;

    /// <summary>Picks stage 3, 4, or 5. Stage 1 and stage 2 stay manual. A
    /// higher stage can improve the plan; it cannot replace a better
    /// one. Yield is not changed here.</summary>
    public static EffortRecommendation RecommendEffort(int pieceCount, int stockSizeCount, bool finiteStock)
    {
        if (pieceCount < 0) pieceCount = 0;
        if (stockSizeCount < 1) stockSizeCount = 1;

        if (pieceCount >= IndustrialPieceLimit)
        {
            return new EffortRecommendation
            {
                Stage = 3,
                Reason = "industrial quantity uses the pattern master, not the small-job grid. Stage 3 keeps the default depth."
            };
        }

        if (pieceCount > NarrowedSearchPieceLimit && !finiteStock)
        {
            return new EffortRecommendation
            {
                Stage = 3,
                Reason = "above 15,000 pieces the batched search is already narrowed. Stage 3 stays inside that budget."
            };
        }

        if (pieceCount > FullSearchPieceLimit)
        {
            return new EffortRecommendation
            {
                Stage = 4,
                Reason = finiteStock
                    ? "a finite rack stays on the global search. Stage 4 runs every ordering without the stage 5 shuffle grid."
                    : "above 2,000 pieces the batched search adds best-split, sheet-fill, and wall-seed at stage 4."
            };
        }

        if (pieceCount <= 200)
        {
            return new EffortRecommendation
            {
                Stage = 5,
                Reason = "a short list finishes the deepest existing stage quickly, and that stage cannot replace a better plan."
            };
        }

        if (stockSizeCount > 1 || finiteStock)
        {
            return new EffortRecommendation
            {
                Stage = 4,
                Reason = "mixed stock sizes or a finite rack use stage 4, which tries every ordering and split."
            };
        }

        return new EffortRecommendation
        {
            Stage = 3,
            Reason = "one stock size in the normal shop range stays on stage 3, the calibrated default."
        };
    }

    /// <summary>Search plan for PackQuick. QUICK is the interactive path
    /// — the operator is watching — so its budget is the tightest of the
    /// three, and it is still far above what a normal quick pass
    /// takes.</summary>
    public static SearchPlan PlanForQuick(int pieceCount)
    {
        return ApplyMemoryPressure(new SearchPlan
        {
            Tier = pieceCount <= 2000 ? SearchTier.Full : SearchTier.Narrowed,
            OpenSheetWindow = int.MaxValue,
            MaxConsolidationSweeps = pieceCount <= 2000 ? 3 : 2,
            MaxDonorAttempts = 1500,
            ShortTag = pieceCount <= 2000 ? "Quick Single-Pass" : "Adaptive Quick Pass",
            Note = pieceCount <= 2000 ? "" : $"Packing {Count(pieceCount)} parts with full open-plate quick pass.",
            TimeBudget = TimeSpan.FromMinutes(5)
        }, pieceCount);
    }
}

/// <summary>Which scale band an input falls into (WPF ScaleTier).
/// Decided purely by part count so a given job always gets the same
/// search budget and the engine stays deterministic.</summary>
public enum ScaleTier
{
    /// <summary>Full search. Everything the calibration benchmark covers
    /// lands here, so results stay bit-identical to earlier releases.</summary>
    Standard = 0,
    /// <summary>Search breadth trimmed; ordering and split diversity
    /// kept.</summary>
    Large = 1,
    /// <summary>Minimum viable search, chosen so the run terminates.</summary>
    Huge = 2
}

/// <summary>How much search the optimizer may perform for a given input
/// size (WPF ScaleProfile). Large inputs trade search BREADTH for
/// termination; every reduction is recorded on the result so it is
/// visible to the operator.</summary>
public sealed class ScaleProfile
{
    public ScaleProfile(int pieceCount, int requestedEffort, ScaleTier tier,
        int shuffleCap, bool allowBeam, int maxRectsOrderings, int consolidationSweeps)
    {
        PieceCount = pieceCount;
        RequestedEffort = requestedEffort;
        Tier = tier;
        ShuffleCap = shuffleCap;
        AllowBeam = allowBeam;
        MaxRectsOrderings = maxRectsOrderings;
        ConsolidationSweeps = consolidationSweeps;
    }

    public int PieceCount { get; }
    public int RequestedEffort { get; }
    public ScaleTier Tier { get; }
    /// <summary>Maximum number of deterministic shuffles per
    /// ordering/split pair.</summary>
    public int ShuffleCap { get; }
    /// <summary>Beam search copies the whole open-sheet list per
    /// candidate.</summary>
    public bool AllowBeam { get; }
    /// <summary>How many orderings the MaxRects candidate may run.</summary>
    public int MaxRectsOrderings { get; }
    /// <summary>Consolidation sweeps cap.</summary>
    public int ConsolidationSweeps { get; }

    /// <summary>True when any search breadth was reduced because of
    /// size.</summary>
    public bool IsReduced => Tier != ScaleTier.Standard;

    /// <summary>Whether sheet-at-a-time quadratic strategies are
    /// allowed.</summary>
    public bool AllowQuadraticStrategies => Tier != ScaleTier.Huge;

    public static ScaleProfile For(int pieceCount, int requestedEffort)
    {
        var plan = OptimizationSearchGovernor.PlanForOptimize(pieceCount, requestedEffort);
        var tier = plan.Tier switch
        {
            SearchTier.Full => ScaleTier.Standard,
            SearchTier.Narrowed => ScaleTier.Large,
            _ => ScaleTier.Huge
        };

        return new ScaleProfile(
            pieceCount,
            requestedEffort,
            tier,
            plan.ShuffleCap,
            plan.Tier == SearchTier.Full,
            plan.MaxRectOrders,
            plan.MaxConsolidationSweeps);
    }

    /// <summary>Plain-language description of what was reduced and why.</summary>
    public string Summary
    {
        get
        {
            var plan = OptimizationSearchGovernor.PlanForOptimize(PieceCount, RequestedEffort);
            return !string.IsNullOrWhiteSpace(plan.Note) ? plan.Note : $"{PieceCount} parts: adaptive search at effort {RequestedEffort}.";
        }
    }
}

public static partial class GuillotinePacker
{
    /// <summary>WPF GuillotinePacker.RepeatedPatterns: above this count
    /// the WPF hands the group to the industrial pattern master instead
    /// of the small-job grid.</summary>
    private const int IndustrialQuantityThreshold = 100000;

    private static readonly string IndustrialStagedUnsupportedNote =
        "STAGED CUTS: the industrial pattern path does not enforce staged cut patterns — " +
        "inspection only, not exportable.";

    /// <summary>Piece-ordering key (WPF OrderBy).</summary>
    internal enum OrderBy
    {
        MaxSide, Area, MinSide, Height, Width, Perimeter
    }

    /// <summary>
    /// OPTIMIZE stage — effort levels 1..5 (WPF PackOptimize). Each
    /// level widens the multi-start search (orderings × split modes);
    /// every candidate run is consolidated and the best wins (fewest
    /// unplaced, then fewest sheets, then the plate pattern's axis, then
    /// utilization). Deterministic.
    /// </summary>
    public static OptimizationResult PackOptimize(
        IReadOnlyList<CutPiece> pieces,
        IReadOnlyList<StockSize> stocks,
        MarginProfile margins,
        RotationPolicy rotation,
        int effortLevel = 3,
        CancellationToken cancellationToken = default,
        IProgress<OptimizeProgress>? progress = null,
        PlatePattern platePattern = PlatePattern.Yield,
        CutPolicy? policy = null,
        int maxStage = 0,
        bool reuseOffcuts = false,
        bool localSearch = false,
        bool enableSwaps = false,
        bool remainderAware = false,
        bool wideBeam = false,
        bool parallelEval = false,
        bool requireGuillotine = false)
    {
        if (pieces == null) throw new ArgumentNullException(nameof(pieces));
        if (stocks == null || stocks.Count == 0)
            throw new ArgumentException("At least one stock size is required.", nameof(stocks));
        if (margins == null) throw new ArgumentNullException(nameof(margins));

        // ── Phase 25a documented difference (mapping §1.6b): the WPF hands
        // groups at/above the industrial threshold to the pattern master
        // (RepeatedPatterns/FillFirstPlate/Partitioned) — a batched engine
        // with per-batch validation that is a later slice. Here ONE
        // governor-bounded shelf pass runs instead, with a visible note.
        // The pairwise layout oracles are deliberately NOT run at this
        // scale — they are O(parts^2) per sheet, which is exactly why the
        // WPF never runs the small-job grid up here; conservation and
        // non-overlap hold by construction (every input piece is either
        // placed through the free-rect ledger or recorded in Unplaced).
        if (pieces.Count >= IndustrialQuantityThreshold)
        {
            var plan = OptimizationSearchGovernor.PlanForOptimize(pieces.Count, effortLevel);
            var result = RunPass(Sort(pieces, OrderBy.Area), stocks, margins, rotation,
                SplitMode.HorizontalFirst,
                consolidationSweeps: plan.MaxConsolidationSweeps,
                openSheetWindow: plan.OpenSheetWindow);
            result.Strategy = "QUICK SHELF (Area / HorizontalFirst)";
            result.ScaleNotes.Add(
                "INDUSTRIAL PATH: " + pieces.Count.ToString("N0", CultureInfo.InvariantCulture) +
                " parts is at/above the 100,000-part threshold where the WPF runs the pattern " +
                "master instead of the small-job grid; that engine is not in this slice yet " +
                "(mapping §1.6b), so one bounded shelf pass ran with construction invariants " +
                "only (the pairwise layout oracle is O(parts^2) and is not run at this scale).");
            DeriveLeftovers(result, Math.Max(margins.MinBreakoutMm, 1), margins.KerfMm);
            return result;
        }

        return RunOptimizeGrid(pieces, stocks, margins, rotation, effortLevel,
            cancellationToken, progress, platePattern, policy, maxStage, requireGuillotine);
    }

    /// <summary>Test seam: the comparison chain is private in the WPF;
    /// the port exposes it internally so golden tests can assert the
    /// exact ordering semantics without widening the public API.</summary>
    internal static OptimizationResult? BetterPublic(
        OptimizationResult? a, OptimizationResult? b,
        PlatePattern pattern = PlatePattern.Yield) =>
        Better(a, b, pattern);

    /// <summary>The multi-start grid chassis (the body of the WPF
    /// PackOptimize below the industrial handoff): stage/effort split,
    /// the sort cache, the shuffle grid, the time budget, the
    /// guillotine-acceptance gate, the parallel evaluation switch, the
    /// validation safety net, cut-sequence derivation, leftover
    /// derivation and the scale notes.</summary>
    private static OptimizationResult RunOptimizeGrid(
        IReadOnlyList<CutPiece> pieces,
        IReadOnlyList<StockSize> stocks,
        MarginProfile margins,
        RotationPolicy rotation,
        int effortLevel,
        CancellationToken cancellationToken,
        IProgress<OptimizeProgress>? progress,
        PlatePattern platePattern,
        CutPolicy? policy,
        int maxStage,
        bool requireGuillotine)
    {
        // PHASE 5a — two axes, kept apart (WPF verbatim):
        //   stage  = WHICH engines and orderings run (the portfolio).
        //   effort = HOW HARD they search (time budget, shuffles, beam,
        //            consolidation sweeps).
        int stage = maxStage > 0 ? maxStage : effortLevel;

        if (stage <= 1)
            return PackQuick(pieces, stocks, margins, rotation,
                cancellationToken: cancellationToken, platePattern: platePattern,
                policy: policy);

        var plan = OptimizationSearchGovernor.PlanForOptimize(pieces.Count, effortLevel);
        var scale = ScaleProfile.For(pieces.Count, effortLevel);

        // The search now has a deadline. Every candidate boundary checks
        // it, and when it engages the search STOPS ADDING CANDIDATES AND
        // KEEPS THE BEST PLAN FOUND SO FAR — Better() only ever replaces
        // best with something strictly better. Cancellation still throws;
        // the budget does not, because a budget expiry is a normal
        // outcome with a usable result, not a failure.
        var budgetWatch = System.Diagnostics.Stopwatch.StartNew();
        bool budgetStopped = false;
        bool BudgetExhausted() => budgetWatch.Elapsed >= plan.TimeBudget;

        if (pieces.Count == 0)
            return new OptimizationResult { Rotation = rotation };

        // LiSEC / Editway-style multi-start: several decreasing
        // orderings × guillotine split rules. Higher effort adds
        // deterministic shuffles (reproducible, not random).
        var orderings = new[]
        {
            OrderBy.MaxSide, OrderBy.Area, OrderBy.MinSide,
            OrderBy.Height, OrderBy.Width, OrderBy.Perimeter
        };
        var splits = new[]
        {
            SplitMode.HorizontalFirst, SplitMode.VerticalFirst, SplitMode.LongerLeftover
        };

        IEnumerable<(OrderBy o, SplitMode s, int shuffle)> combos;
        if (stage <= 1)
            // Must mirror PackQuick exactly: the NeverRegressesQuick
            // contract asserts OPTIMIZE <= QUICK.
            combos = new[] { (OrderBy.Area, SplitMode.HorizontalFirst, 0) };
        else if (stage == 2)
            combos = orderings.Take(3).Select(o => (o, SplitMode.HorizontalFirst, 0));
        else if (stage == 3)
            combos = orderings.Take(4).SelectMany(o => splits.Select(s => (o, s, 0)));
        else
        {
            int shuffles = stage >= 5 ? 6 : 3;
            shuffles = Math.Min(shuffles, plan.ShuffleCap);
            combos = orderings.SelectMany(o => splits.SelectMany(s =>
                Enumerable.Range(0, shuffles + 1).Select(k => (o, s, k))));
        }

        var comboList = combos.ToList();

        // SMART PLACEMENT adds one or two more complete plans to the
        // search (see OptimizationEngine.Candidates.cs). Counted up
        // front so the bar keeps moving forward instead of reaching
        // 100% and then dropping back while the last candidates run.
        // Zero below stage 2 and in the Huge tier.
        int smartPasses = stage >= 2 && scale.Tier != ScaleTier.Huge
            ? (effortLevel >= 4 && scale.Tier == ScaleTier.Standard ? 2 : 1)
            : 0;
        int smartTotal = comboList.Count + smartPasses;

        // ── SCALE FIX 4: cache the base sorts. Sort(pieces, order) is a
        // pure function of the input and the ordering key; there are only
        // six distinct keys, so re-sorting for every combo is redundant.
        var sortCache = new Dictionary<OrderBy, List<CutPiece>>();

        int guillotineRejects = 0;

        OptimizationResult? best = PackQuick(pieces, stocks, margins, rotation,
            cancellationToken: cancellationToken, policy: policy);

        // PHASE 1: same rule as QUICK — the beam and the repeated-pattern
        // pass build their own rectangles and cannot honour a stage
        // budget. (Staged enforcement itself lands with the staged-cuts
        // slice; the gate is already exact.)
        bool staged = policy != null && policy.IsDepthConstrained;

        // The bounded best-split pattern beam over the QUICK floor
        // (WPF: ImprovePatterns(2, effortLevel), budget-gated).
        if (!staged)
        {
            var improved = ImprovePatterns(best!, pieces, stocks, margins, rotation,
                2, effortLevel, cancellationToken, BudgetExhausted);
            if (Accepts(improved)) best = improved;
            // 25c: the WPF then runs RunRepeatedPatterns here — the
            // repeated/partitioned pattern family is the next slice.
        }

        // E4.1 — a plan that cannot be cut is not a plan: a candidate is
        // admissible or it is not considered. best is seeded from
        // PackQuick, which is guillotine by construction, so rejecting
        // every candidate still leaves the default QUICK plan standing.
        bool Accepts(OptimizationResult candidate)
        {
            if (!requireGuillotine) return true;
            if (AllSheetsCuttable(candidate)) return true;
            guillotineRejects++;
            return false;
        }

        void Consider(OptimizationResult candidate)
        {
            var next = Better(best, CheckCandidate(candidate, pieces, margins, cancellationToken),
                platePattern);

            // Nothing changed, or there was nothing to compare against.
            if (next == null || ReferenceEquals(next, best)) { best = next; return; }

            // It won the comparison, so it is the plan that would ship.
            if (!Accepts(next)) return;
            best = next;
        }

        // E3.1 — evaluate the pool in parallel. Multi-start is
        // embarrassingly parallel and the reduction is a deterministic
        // minimum over a total order, so the winner is the same plan the
        // sequential loop would have produced — provided the search
        // budget does not bind. When it binds it CHANGES the plan (it
        // finishes all candidates); that is a better plan more slowly,
        // which is a trade made deliberately, hence the switch.
        void RunCombosParallel()
        {
            var orderedBy = new List<CutPiece>[comboList.Count];
            for (int ci = 0; ci < comboList.Count; ci++)
            {
                var (order, split, shuffle) = comboList[ci];
                if (!sortCache.TryGetValue(order, out var ordered))
                {
                    ordered = Sort(pieces, order);
                    sortCache[order] = ordered;
                }
                orderedBy[ci] = shuffle > 0
                    ? ShuffleDeterministic(ordered, 17 + shuffle * 131 + (int)order * 7)
                    : ordered;
            }

            var results = new OptimizationResult[comboList.Count];
            System.Threading.Tasks.Parallel.For(0, comboList.Count,
                new System.Threading.Tasks.ParallelOptions { CancellationToken = cancellationToken },
                ci =>
                {
                    var (order, split, shuffle) = comboList[ci];
                    results[ci] = RunPass(orderedBy[ci], stocks, margins, rotation, split,
                        consolidationSweeps: plan.MaxConsolidationSweeps,
                        openSheetWindow: plan.OpenSheetWindow);
                });

            // Fold in candidate order. Better() is a deterministic total
            // preorder, so folding in a fixed order reaches the same
            // winner the sequential loop would have reached.
            for (int ci = 0; ci < results.Length; ci++)
            {
                var candidate = results[ci];
                if (candidate == null) continue;

                var (order, split, shuffle) = comboList[ci];
                candidate.Strategy = $"GRID ({order} / {split}"
                    + (shuffle > 0 ? $" / shuffle {shuffle}" : "") + ")";
                Consider(candidate);
            }

            progress?.Report(OptimizeProgress.Of("pass", comboList.Count, smartTotal,
                $"Evaluated {comboList.Count} candidates in parallel"));
        }

        if (parallelEval && comboList.Count > 1)
        {
            RunCombosParallel();
        }
        else
        {
            for (int ci = 0; ci < comboList.Count; ci++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Checked at the candidate boundary, never mid-placement:
                // a plan half-built is worth nothing, a plan completed and
                // then stopped is worth exactly what it always was.
                if (BudgetExhausted())
                {
                    budgetStopped = true;
                    break;
                }

                var (order, split, shuffle) = comboList[ci];

                if (!sortCache.TryGetValue(order, out var ordered))
                {
                    ordered = Sort(pieces, order);
                    sortCache[order] = ordered;
                }
                if (shuffle > 0)
                    ordered = ShuffleDeterministic(ordered, 17 + shuffle * 131 + (int)order * 7);

                progress?.Report(OptimizeProgress.Of("pass", ci, smartTotal,
                    $"Pass {ci + 1} of {comboList.Count}: {order} / {split}" +
                    (shuffle > 0 ? $" / shuffle {shuffle}" : "")));

                var candidate = RunPass(ordered, stocks, margins, rotation, split,
                    consolidationSweeps: plan.MaxConsolidationSweeps,
                    openSheetWindow: plan.OpenSheetWindow);
                // Tag with the EXACT candidate this plan came from. Effort
                // 1 runs exactly the QUICK pass (the NeverRegressesQuick
                // contract), so it carries the same label QUICK does — a
                // one-candidate run is not a grid.
                candidate.Strategy = comboList.Count == 1
                    ? $"QUICK SHELF ({order} / {split})"
                    : $"GRID ({order} / {split}" +
                      (shuffle > 0 ? $" / shuffle {shuffle}" : "") + ")";
                Consider(candidate);
            }
        }

        // With windowed candidate scanning, advanced strategies (BestSplit,
        // SheetFill, WallSeed, SmartPlacement) run in bounded O(K * N) time
        // across all tiers (WPF verbatim).
        if (stage >= 2 && !budgetStopped)
        {
            int windowSize = scale.Tier == ScaleTier.Huge ? 300 : scale.Tier == ScaleTier.Large ? 400 : int.MaxValue;

            foreach (var split in splits)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Consider(RunSheetFill(pieces, stocks, margins, rotation, split,
                    scale.ConsolidationSweeps, cancellationToken, windowSize));
                Consider(RunWallSeed(pieces, stocks, margins, rotation, split,
                    scale.ConsolidationSweeps, cancellationToken, windowSize));
            }
            cancellationToken.ThrowIfCancellationRequested();

            // Per-sheet split selection — see OptimizationEngine.Candidates.cs.
            // This is the candidate that takes the WPF's Clear group from 17
            // sheets to the owner's reference 16.
            Consider(RunBestSplit(pieces, stocks, margins, rotation,
                scale.ConsolidationSweeps, cancellationToken, windowSize,
                remainderAware: remainderAware));

            // ── SMART PLACEMENT ──────────────────────────
            // Capacity-preserving placement, per-placement split choice and
            // the configured minimum breakout enforced. Added as a candidate,
            // never as a replacement: Better() keeps the minimum on unplaced,
            // then sheets, then utilization, so this can only improve the
            // chosen plan or lose.
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(OptimizeProgress.Of("smart", comboList.Count, smartTotal,
                "Smart placement: capacity-preserving, breakout-aware"));
            Consider(PackSmart(pieces, stocks, margins, rotation,
                SplitMode.HorizontalFirst, scale.ConsolidationSweeps, cancellationToken,
                progress: null, candidateWindowSize: windowSize, policy: policy));

            if (stage >= 4)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(OptimizeProgress.Of("smart", comboList.Count + 1, smartTotal,
                    "Smart placement: vertical-split preference"));
                Consider(PackSmart(pieces, stocks, margins, rotation,
                    SplitMode.VerticalFirst, scale.ConsolidationSweeps, cancellationToken,
                    progress: null, candidateWindowSize: windowSize, policy: policy));
            }
        }

        if (stage >= 3 && !budgetStopped && !staged)
        {
            // Math.Min with an unlimited cap leaves the standard band
            // exactly as it was (6 orderings at effort 5, 3 below).
            foreach (var order in orderings.Take(Math.Min(
                         stage >= 5 ? 6 : 3, scale.MaxRectsOrderings)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                Consider(RunMaxRects(Sort(pieces, order), stocks, margins, rotation,
                        cancellationToken));
            }

            // Beam-corrected version: greedy per-sheet selection is myopic
            // (measured worse than the shipped engine on 17 of 40
            // randomized instances when used ALONE), so it is widened here.
            // Adding candidates can never regress — Better() takes the min.
            if (scale.AllowBeam)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Consider(RunBestSplitBeam(pieces, stocks, margins, rotation, 4, cancellationToken));
                if (stage >= 4)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Consider(RunBestSplitBeam(pieces, stocks, margins, rotation, 8, cancellationToken));
                }

                // PHASE E3.3. Beam 16 at the top effort only, and behind a
                // switch. It only ever ADDS a candidate and Better() takes
                // the minimum, so it cannot make a plan worse - only slower.
                if (stage >= 5 && wideBeam)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Consider(RunBestSplitBeam(pieces, stocks, margins, rotation, 16, cancellationToken));
                }
            }
        }

        var final = best ?? PackQuick(pieces, stocks, margins, rotation, policy: policy);

        // Never let a size-driven reduction be invisible.
        if (scale.IsReduced) final.ScaleNotes.Add(scale.Summary);

        // A refusal the operator cannot see is worse than no policy at
        // all: the sheet count would look worse for no stated reason.
        if (requireGuillotine && guillotineRejects > 0)
            final.ScaleNotes.Add("GUILLOTINE POLICY: " + guillotineRejects
                + " candidate plan(s) were refused because no sequence of straight cuts "
                + "produces their layout. The plan shown is the best one a guillotine "
                + "table can actually cut, which is not always the fewest sheets.");

        // Same rule for the time budget. A truncated search that reported
        // nothing would look like a complete one.
        if (budgetStopped)
        {
            final.ScaleNotes.Add(string.Format(CultureInfo.InvariantCulture,
                "TIME BUDGET: the search reached its {0:mm\\:ss} limit after {1:F1}s and stopped " +
                "adding candidates — this is the best plan found by then, not a fully searched one. " +
                "Splitting the job into smaller cutting groups would search deeper.",
                plan.TimeBudget, budgetWatch.Elapsed.TotalSeconds));
        }

        // Industrial safety net: a plan that fails validation must never
        // reach a cut file. Fall back to the quick pass; if even that
        // fails (should be impossible), keep the original and say so.
        var problems = VerifyComprehensive(final, pieces, margins);
        if (problems.Count > 0)
        {
            var quick = PackQuick(pieces, stocks, margins, rotation,
                cancellationToken: cancellationToken, policy: policy);
            var quickProblems = VerifyComprehensive(quick, pieces, margins);
            if (quickProblems.Count == 0)
            {
                // The note goes on the plan that is actually returned.
                quick.ScaleNotes.Add("WARNING: selected plan failed layout validation " +
                                     $"({problems[0]}); replaced with validated quick pass.");
                final = quick;
                problems = quickProblems;
            }
            else
            {
                final.ScaleNotes.Add("WARNING: layout validation failed " +
                                     $"({problems[0]}; {problems.Count} issue(s)) — result not re-run.");
            }
        }

        // The verdict travels with the plan, so the run header can say
        // what was actually checked. RecordValidation derives the
        // Verified flags (WPF verbatim, 25b reconciliation): the grid
        // path validates through CheckCandidate, which used to leave
        // them false so clean OPTIMIZE plans reported "Not verified".
        final.RecordValidation(problems, conservationChecked: true, geometryChecked: true);

        // PHASE 0: give every sheet a cut sequence. Paths that stamp
        // pieces straight onto a layout leave it without one, and a plan
        // nobody can cut is not a plan.
        for (int i = 0; i < final.Sheets.Count; i++)
        {
            var sheet = final.Sheets[i];
            if (sheet.Cuts.Count != 0) continue;

            if (!TryDeriveGuillotineCuts(sheet) && sheet.Pieces.Count > 0)
                final.ScaleNotes.Add(
                    "WARNING: sheet " + sheet.SheetNo + " holds "
                    + sheet.Pieces.Count + " pane(s) but no sequence of "
                    + "straight cuts produces that layout. It cannot be cut "
                    + "as drawn - open it in the 2D VIEW before exporting.");
        }

        // Derived from cuts, never from placements (WPF PHASE 2).
        DeriveLeftovers(final, Math.Max(margins.MinBreakoutMm, 1), margins.KerfMm);

        progress?.Report(OptimizeProgress.Of("done", smartTotal, smartTotal,
            $"{final.Sheets.Count} sheets"));
        return final;
    }

    // ── the comparison chain ────────────────────────────────────────

    /// <summary>
    /// Keeps the better of two plans (WPF Better). Unplaced pieces and
    /// sheet count always dominate — no pattern may buy its preference
    /// by leaving a piece out or adding a sheet. The plate pattern
    /// decides between plans that are already equal on both.
    /// </summary>
    private static OptimizationResult? Better(OptimizationResult? a, OptimizationResult? b) =>
        Better(a, b, PlatePattern.Yield);

    private static OptimizationResult? Better(
        OptimizationResult? a, OptimizationResult? b, PlatePattern pattern)
    {
        if (b == null) return a;
        if (a == null) return b;
        if (a.HasValidationFailure != b.HasValidationFailure)
            return a.HasValidationFailure ? b : a;
        if (b.Unplaced.Count != a.Unplaced.Count)
            return b.Unplaced.Count < a.Unplaced.Count ? b : a;
        if (b.SheetsUsed != a.SheetsUsed)
            return b.SheetsUsed < a.SheetsUsed ? b : a;

        switch (pattern)
        {
            case PlatePattern.FewestCuts:
                int cutA = TotalCuts(a), cutB = TotalCuts(b);
                if (cutB != cutA) return cutB < cutA ? b : a;
                break;
            case PlatePattern.MinimiseStockChanges:
                int sizeA = DistinctStockSizes(a), sizeB = DistinctStockSizes(b);
                if (sizeB != sizeA) return sizeB < sizeA ? b : a;
                int switchA = StockSwitches(a), switchB = StockSwitches(b);
                if (switchB != switchA) return switchB < switchA ? b : a;
                break;
            case PlatePattern.RemnantFirst:
                long leftA = LargestLeftover(a), leftB = LargestLeftover(b);
                if (leftB != leftA) return leftB > leftA ? b : a;
                break;
            case PlatePattern.BalancedSpread:
                decimal spreadA = UtilizationSpread(a), spreadB = UtilizationSpread(b);
                if (spreadB != spreadA) return spreadB < spreadA ? b : a;
                break;
        }

        return Utilization(b) > Utilization(a) ? b : a;
    }

    private static decimal Utilization(OptimizationResult r)
    {
        decimal stock = r.Sheets.Sum(sheet => (decimal)sheet.Stock.WidthMm * sheet.Stock.HeightMm);
        decimal glass = r.Sheets.Sum(sheet => sheet.Pieces.Sum(p => (decimal)p.WidthMm * p.HeightMm));
        return stock == 0 ? 0 : glass / stock;
    }

    /// <summary>Total guillotine cuts across a plan. Sheets that were
    /// stamped rather than split get their sequence reconstructed first,
    /// so a plan can never win "fewest cuts" by having failed to record
    /// them.</summary>
    internal static int TotalCuts(OptimizationResult result)
    {
        int total = 0;
        for (int i = 0; i < result.Sheets.Count; i++)
        {
            var sheet = result.Sheets[i];
            if (sheet.Cuts.Count == 0 && sheet.Pieces.Count > 0)
                TryDeriveGuillotineCuts(sheet);
            total += sheet.Cuts.Count;
        }
        return total;
    }

    /// <summary>How many different stock sizes a plan reaches for.</summary>
    internal static int DistinctStockSizes(OptimizationResult result)
    {
        var seen = new HashSet<long>();
        for (int i = 0; i < result.Sheets.Count; i++)
        {
            var stock = result.Sheets[i].Stock;
            if (stock == null) continue;
            seen.Add(StockKey(stock));
        }
        return seen.Count;
    }

    /// <summary>One number per stock size, so "same size" is a value test
    /// rather than a string comparison per sheet.</summary>
    private static long StockKey(StockSize stock) =>
        ((long)stock.WidthMm << 32) | (uint)stock.HeightMm;

    /// <summary>How often consecutive sheets change stock size — the rack
    /// moves and setups a floor actually pays for. Sheet order is the
    /// report order (fullest first), which is the order the table sees
    /// them.</summary>
    internal static int StockSwitches(OptimizationResult result)
    {
        int switches = 0;
        bool hasPrevious = false;
        long previous = 0;
        for (int i = 0; i < result.Sheets.Count; i++)
        {
            var stock = result.Sheets[i].Stock;
            if (stock == null) continue;
            long key = StockKey(stock);
            if (hasPrevious && key != previous) switches++;
            previous = key;
            hasPrevious = true;
        }
        return switches;
    }

    /// <summary>Largest single-sheet leftover area in mm². Maximised by
    /// the remnant-first pattern: with the sheet count and the piece
    /// count already equal, concentrating the waste on ONE sheet is what
    /// makes it big enough to come off as a usable remnant.</summary>
    internal static long LargestLeftover(OptimizationResult result)
    {
        long best = 0;
        for (int i = 0; i < result.Sheets.Count; i++)
        {
            var sheet = result.Sheets[i];
            long usable = (long)sheet.UsableWidth * sheet.UsableHeight;
            long placed = 0;
            for (int p = 0; p < sheet.Pieces.Count; p++)
                placed += (long)sheet.Pieces[p].WidthMm * sheet.Pieces[p].HeightMm;
            long leftover = usable - placed;
            if (leftover > best) best = leftover;
        }
        return best;
    }

    /// <summary>Gap between the fullest and the emptiest sheet, 0..1.
    /// Minimised by the balanced-spread pattern.</summary>
    internal static decimal UtilizationSpread(OptimizationResult result)
    {
        if (result.Sheets.Count <= 1) return 0m;
        decimal min = 1m, max = 0m;
        for (int i = 0; i < result.Sheets.Count; i++)
        {
            var sheet = result.Sheets[i];
            decimal stock = (decimal)sheet.Stock.WidthMm * sheet.Stock.HeightMm;
            if (stock <= 0) continue;
            decimal glass = 0m;
            for (int p = 0; p < sheet.Pieces.Count; p++)
                glass += (decimal)sheet.Pieces[p].WidthMm * sheet.Pieces[p].HeightMm;
            decimal u = glass / stock;
            if (u < min) min = u;
            if (u > max) max = u;
        }
        return max - min;
    }

    // ── orderings + shuffles ────────────────────────────────────────

    internal static List<CutPiece> Sort(IReadOnlyList<CutPiece> pieces, OrderBy order)
    {
        switch (order)
        {
            case OrderBy.Area:
                return pieces
                    .OrderByDescending(p => (long)p.WidthMm * p.HeightMm)
                    .ThenByDescending(p => Math.Max(p.WidthMm, p.HeightMm))
                    .ThenBy(p => p.GlassRef, StringComparer.Ordinal)
                    .ThenBy(p => p.PieceLabel, StringComparer.Ordinal)
                    .ToList();
            case OrderBy.MinSide:
                return pieces
                    .OrderByDescending(p => Math.Min(p.WidthMm, p.HeightMm))
                    .ThenByDescending(p => (long)p.WidthMm * p.HeightMm)
                    .ThenBy(p => p.GlassRef, StringComparer.Ordinal)
                    .ThenBy(p => p.PieceLabel, StringComparer.Ordinal)
                    .ToList();
            case OrderBy.Height:
                return pieces
                    .OrderByDescending(p => p.HeightMm)
                    .ThenByDescending(p => p.WidthMm)
                    .ThenBy(p => p.GlassRef, StringComparer.Ordinal)
                    .ThenBy(p => p.PieceLabel, StringComparer.Ordinal)
                    .ToList();
            case OrderBy.Width:
                return pieces
                    .OrderByDescending(p => p.WidthMm)
                    .ThenByDescending(p => p.HeightMm)
                    .ThenBy(p => p.GlassRef, StringComparer.Ordinal)
                    .ThenBy(p => p.PieceLabel, StringComparer.Ordinal)
                    .ToList();
            case OrderBy.Perimeter:
                return pieces
                    .OrderByDescending(p => (long)p.WidthMm + p.HeightMm)
                    .ThenByDescending(p => (long)p.WidthMm * p.HeightMm)
                    .ThenBy(p => p.GlassRef, StringComparer.Ordinal)
                    .ThenBy(p => p.PieceLabel, StringComparer.Ordinal)
                    .ToList();
            default: // calibrated default: tallest first
                return pieces
                    .OrderByDescending(p => Math.Max(p.WidthMm, p.HeightMm))
                    .ThenByDescending(p => (long)p.WidthMm * p.HeightMm)
                    .ThenBy(p => p.GlassRef, StringComparer.Ordinal)
                    .ThenBy(p => p.PieceLabel, StringComparer.Ordinal)
                    .ToList();
        }
    }

    /// <summary>Fisher–Yates on an LCG — reproducible, not random (WPF
    /// ShuffleDeterministic verbatim: state = state * 1103515245 +
    /// 12345, j = (uint)state % (i + 1)).</summary>
    internal static List<CutPiece> ShuffleDeterministic(List<CutPiece> src, int seed)
    {
        var list = src.ToList();
        int state = seed;
        for (int i = list.Count - 1; i > 0; i--)
        {
            state = unchecked(state * 1103515245 + 12345);
            int j = (int)((uint)state % (i + 1));
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    // ── the bounded open-sheet window (WPF PackingContext) ──────────

    /// <summary>
    /// Bounded open-sheet window — the industrial "active plates"
    /// pattern. A pass keeps at most OpenWindow sheets open for
    /// placement; opening beyond that seals the OLDEST open sheet (it
    /// already received every placement attempt since it opened, so it
    /// is near-final). Per-piece scan cost drops from O(all sheets) to
    /// O(window). FIFO order is deterministic, so runs stay
    /// reproducible. int.MaxValue = unbounded (FULL tier).
    /// </summary>
    private sealed class PackingContext
    {
        public int OpenWindow = int.MaxValue;
        public int OpenCount;
        public readonly Queue<SheetState> OpenQueue = new();
    }

    /// <summary>Registers a freshly opened sheet; seals the oldest open
    /// sheet when the window is full (amortized O(1)).</summary>
    private static void RegisterOpenedSheet(PackingContext ctx, SheetState state)
    {
        if (ctx.OpenCount >= ctx.OpenWindow)
            CloseOldestOpenSheet(ctx);
        else
            ctx.OpenCount++;
        ctx.OpenQueue.Enqueue(state);
    }

    private static void CloseOldestOpenSheet(PackingContext ctx)
    {
        while (ctx.OpenQueue.Count > 0)
        {
            var s = ctx.OpenQueue.Dequeue();
            if (s.IsRemoved || s.IsClosed) continue;
            s.IsClosed = true;
            return;
        }
    }

    // ── consolidation — squeeze out the emptiest sheet ──────────────

    /// <summary>
    /// Records a copy-on-write snapshot of the sheet the first time an
    /// operation is about to modify it. Repeat calls for the same sheet
    /// are no-ops, so the snapshot always holds the pre-attempt state.
    /// Must be called BEFORE the write.
    /// </summary>
    private static void SnapshotBeforeWrite(
        SheetState sheet,
        HashSet<SheetState> seen,
        List<SheetState> touched,
        List<List<PlacedPiece>> touchedPieces,
        List<List<FreeRect>> touchedFree,
        List<List<CutStep>> touchedCuts)
    {
        if (!seen.Add(sheet)) return;
        touched.Add(sheet);
        touchedPieces.Add(sheet.Layout.Pieces.ToList());
        touchedFree.Add(sheet.Free.ToList());
        // A rolled-back placement must take its cuts with it, or the
        // sheet would keep a cut list describing cuts that no longer
        // exist. Cuts are recorded by SplitRemainder, so they live and
        // die with the free-rect ledger they were derived from.
        touchedCuts.Add(sheet.Layout.Cuts.ToList());
    }

    /// <summary>
    /// Try to empty every sheet from sparsest to densest. One successful
    /// evacuation restarts the scan — LiSEC-style leftover squeeze (WPF
    /// ConsolidateAll verbatim, including the copy-on-write rollback).
    /// </summary>
    private static void ConsolidateAll(
        List<SheetState> sheets, MarginProfile margins, RotationPolicy rotation, SplitMode split,
        int maxSweeps = int.MaxValue, int minBreakout = 0,
        CancellationToken cancellationToken = default,
        PackingContext? ctx = null)
    {
        if (sheets.Count <= 1) return;

        // Each successful evacuation breaks out and restarts the whole
        // donor scan; maxSweeps bounds the pathological case.
        bool improved = true;
        int sweeps = 0;
        while (improved && sheets.Count > 1 && sweeps < maxSweeps)
        {
            sweeps++;
            improved = false;
            // PlacedArea is maintained incrementally by Commit. OrderBy
            // is a stable sort, so identical keys yield an identical
            // order.
            var order = Enumerable.Range(0, sheets.Count)
                .OrderBy(i => sheets[i].PlacedArea)
                .ToList();

            foreach (int donorIndex in order)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (sheets.Count <= 1) break;
                if (donorIndex >= sheets.Count) continue;

                var donor = sheets[donorIndex];
                if (donor.Layout.Pieces.Count == 0)
                {
                    bool donorWasOpen = !donor.IsClosed;
                    donor.IsRemoved = true;
                    sheets.RemoveAt(donorIndex);
                    if (donorWasOpen && ctx != null) ctx.OpenCount--;
                    improved = true;
                    break;
                }

                var victims = donor.Layout.Pieces
                    .OrderByDescending(p => (long)p.WidthMm * p.HeightMm)
                    .ToList();

                // Snapshot only what this attempt writes: the rollback is
                // exact (same pieces, same rects, same order) at
                // O(touched) instead of O(sheets x parts).
                var touched = new List<SheetState>();
                var touchedPieces = new List<List<PlacedPiece>>();
                var touchedFree = new List<List<FreeRect>>();
                var touchedCuts = new List<List<CutStep>>();
                var seen = new HashSet<SheetState>();

                SnapshotBeforeWrite(donor, seen, touched, touchedPieces, touchedFree, touchedCuts);
                donor.Layout.Pieces.Clear();
                donor.PlacedArea = 0;
                bool allEvacuated = true;

                foreach (var victim in victims)
                {
                    bool placed = false;
                    for (int si = 0; si < sheets.Count; si++)
                    {
                        if (si == donorIndex) continue;
                        var host = sheets[si];
                        if (host.IsClosed) continue;
                        if (!TryPlaceCore(host, victim.Piece, rotation, out var moved)) continue;
                        SnapshotBeforeWrite(host, seen, touched, touchedPieces, touchedFree, touchedCuts);
                        Commit(host, moved.Materialize(victim.Piece), margins,
                            split, minBreakout);
                        placed = true;
                        break;
                    }
                    if (!placed) { allEvacuated = false; break; }
                }

                if (allEvacuated)
                {
                    bool donorWasOpen = !sheets[donorIndex].IsClosed;
                    sheets[donorIndex].IsRemoved = true;
                    sheets.RemoveAt(donorIndex);
                    if (donorWasOpen && ctx != null) ctx.OpenCount--;
                    improved = true;
                    break;
                }

                // Roll back exactly the sheets we wrote, newest first,
                // and restore their cached metrics so the invariants
                // hold.
                for (int t = touched.Count - 1; t >= 0; t--)
                {
                    touched[t].Layout.Pieces.Clear();
                    touched[t].Layout.Pieces.AddRange(touchedPieces[t]);
                    touched[t].Free.Clear();
                    touched[t].Free.AddRange(touchedFree[t]);
                    touched[t].Layout.Cuts.Clear();
                    touched[t].Layout.Cuts.AddRange(touchedCuts[t]);
                    touched[t].RefreshAll();
                }
            }
        }
    }

    // ── the verification oracles ────────────────────────────────────

    /// <summary>
    /// The candidate gate (WPF CheckCandidate): a candidate that fails
    /// comprehensive verification carries its verdict — it can still
    /// win only against another failed plan, never against a valid one.
    /// </summary>
    private static OptimizationResult CheckCandidate(OptimizationResult candidate,
        IReadOnlyList<CutPiece> pieces, MarginProfile margins,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        candidate.RecordValidation(VerifyComprehensive(candidate, pieces, margins),
            conservationChecked: true, geometryChecked: true);
        return candidate;
    }

    /// <summary>
    /// Asserts every geometric guarantee of a run (WPF Verify): margin
    /// zone, rotation policy, dimension integrity, overlap, kerf.
    /// Returns the list of violations (empty = layout valid).
    /// </summary>
    public static List<string> Verify(OptimizationResult result, MarginProfile margins)
    {
        var problems = new List<string>();
        int kerf = margins.KerfMm;

        foreach (var sheet in result.Sheets)
        {
            int usableRight = sheet.UsableX + sheet.UsableWidth;
            int usableBottom = sheet.UsableY + sheet.UsableHeight;

            foreach (var p in sheet.Pieces)
            {
                if (p.X < sheet.UsableX || p.Y < sheet.UsableY ||
                    p.X + p.WidthMm > usableRight || p.Y + p.HeightMm > usableBottom)
                    problems.Add($"Sheet {sheet.SheetNo}: {Label(p)} breaches the margin zone.");

                if (result.Rotation == RotationPolicy.Locked && p.Rotated)
                    problems.Add($"Sheet {sheet.SheetNo}: {Label(p)} is rotated under Locked policy.");

                if (p.Piece == null)
                {
                    problems.Add($"Sheet {sheet.SheetNo}: {Label(p)} has no source piece to verify dimensions against.");
                }
                else
                {
                    bool origMatch = (p.WidthMm == p.Piece.WidthMm && p.HeightMm == p.Piece.HeightMm) ||
                                     (p.WidthMm == p.Piece.HeightMm && p.HeightMm == p.Piece.WidthMm && p.Rotated);
                    if (!origMatch)
                        problems.Add($"Sheet {sheet.SheetNo}: {Label(p)} dimension mutated from {p.Piece.WidthMm}x{p.Piece.HeightMm} to {p.WidthMm}x{p.HeightMm}.");
                }
            }

            for (int i = 0; i < sheet.Pieces.Count; i++)
            {
                for (int j = i + 1; j < sheet.Pieces.Count; j++)
                {
                    var a = sheet.Pieces[i];
                    var b = sheet.Pieces[j];

                    bool xOverlap = a.X < b.X + b.WidthMm && b.X < a.X + a.WidthMm;
                    bool yOverlap = a.Y < b.Y + b.HeightMm && b.Y < a.Y + a.HeightMm;
                    if (xOverlap && yOverlap)
                    {
                        problems.Add($"Sheet {sheet.SheetNo}: {Label(a)} overlaps {Label(b)}.");
                        continue;
                    }

                    int xGap = Math.Max(
                        b.X - (a.X + a.WidthMm), a.X - (b.X + b.WidthMm));
                    int yGap = Math.Max(
                        b.Y - (a.Y + a.HeightMm), a.Y - (b.Y + b.HeightMm));

                    if (xOverlap && !yOverlap && yGap < kerf)
                        problems.Add($"Sheet {sheet.SheetNo}: {Label(a)} breaks kerf against {Label(b)} ({yGap}mm < {kerf}mm).");
                    if (yOverlap && !xOverlap && xGap < kerf)
                        problems.Add($"Sheet {sheet.SheetNo}: {Label(a)} breaks kerf against {Label(b)} ({xGap}mm < {kerf}mm).");
                }
            }
        }

        return problems;
    }

    /// <summary>
    /// Comprehensive independent verification oracle (WPF
    /// VerifyComprehensive): geometry, kerf, margins, minimum breakout,
    /// quantity conservation and material conservation.
    /// </summary>
    public static List<string> VerifyComprehensive(
        OptimizationResult result,
        IReadOnlyList<CutPiece> inputPieces,
        MarginProfile margins)
    {
        var problems = Verify(result, margins);
        problems.AddRange(AuditBreakout(result, margins));
        if (inputPieces != null) problems.AddRange(ValidateResult(result, inputPieces));

        // Quantity conservation: InputCount == PlacedCount + UnplacedCount
        int totalPlaced = result.Sheets.Sum(s => s.Pieces.Count);
        int totalUnplaced = result.Unplaced.Count;
        if (inputPieces != null && totalPlaced + totalUnplaced != inputPieces.Count)
        {
            problems.Add($"Quantity conservation violation: input {inputPieces.Count} != placed {totalPlaced} + unplaced {totalUnplaced}.");
        }

        foreach (var rack in result.Sheets.GroupBy(sheet => sheet.Stock))
        {
            if (rack.Key.MaxSheets > 0 && rack.Count() > rack.Key.MaxSheets)
                problems.Add($"Stock cap violated: {rack.Count()} sheets used; only {rack.Key.MaxSheets} allowed.");
        }

        // Material conservation: every piece on a sheet must match the
        // sheet's stock material / thickness.
        foreach (var sheet in result.Sheets)
        {
            foreach (var p in sheet.Pieces)
            {
                if (sheet.Stock != null && p.Piece.ThicknessMm > 0 && sheet.Stock.ThicknessMm > 0 &&
                    p.Piece.ThicknessMm != sheet.Stock.ThicknessMm)
                {
                    problems.Add($"Sheet {sheet.SheetNo}: piece {Label(p)} thickness {p.Piece.ThicknessMm}mm does not match stock {sheet.Stock.ThicknessMm}mm.");
                }
            }
        }

        return problems;
    }

    /// <summary>
    /// The PHASE 1 breakout oracle (WPF AuditBreakout): does the plan
    /// leave offcuts below the configured minimum breakout between
    /// neighbouring pieces?
    /// </summary>
    public static List<string> AuditBreakout(OptimizationResult result, MarginProfile margins)
    {
        var problems = new List<string>();
        if (result == null || margins == null) return problems;

        int min = margins.MinBreakoutMm;
        if (min <= 0) return problems;
        int kerf = margins.KerfMm;

        foreach (var sheet in result.Sheets)
        {
            for (int i = 0; i < sheet.Pieces.Count; i++)
            {
                for (int j = i + 1; j < sheet.Pieces.Count; j++)
                {
                    var a = sheet.Pieces[i];
                    var b = sheet.Pieces[j];

                    bool xOverlap = a.X < b.X + b.WidthMm && b.X < a.X + a.WidthMm;
                    bool yOverlap = a.Y < b.Y + b.HeightMm && b.Y < a.Y + a.HeightMm;
                    if (xOverlap && yOverlap) continue;   // Verify's job, not ours

                    int gap;
                    if (xOverlap)
                        gap = Math.Max(b.Y - (a.Y + a.HeightMm), a.Y - (b.Y + b.HeightMm));
                    else if (yOverlap)
                        gap = Math.Max(b.X - (a.X + a.WidthMm), a.X - (b.X + b.WidthMm));
                    else
                        continue;   // diagonal neighbours leave no strip between them

                    int offcut = gap - kerf;
                    if (offcut <= 0 || offcut >= min) continue;

                    problems.Add(string.Format(CultureInfo.InvariantCulture,
                        "Sheet {0}: {1} leaves a {2} mm offcut against {3} — below the {4} mm minimum breakout.",
                        sheet.SheetNo, Label(a), offcut, Label(b), min));
                }
            }
        }

        return problems;
    }

    /// <summary>
    /// The conservation oracle (WPF ValidateResult): dimension match,
    /// double placement, bounds, overlap, piece accounting.
    /// </summary>
    internal static List<string> ValidateResult(OptimizationResult result, IReadOnlyList<CutPiece> pieces)
    {
        var problems = new List<string>();
        if (result == null) { problems.Add("null result"); return problems; }

        var seen = new HashSet<CutPiece>();
        long placedArea = 0;
        foreach (var sheet in result.Sheets)
        {
            for (int a = 0; a < sheet.Pieces.Count; a++)
            {
                var pa = sheet.Pieces[a];
                var src = pa.Piece;
                bool dimOk = src != null &&
                    ((pa.WidthMm == src.WidthMm && pa.HeightMm == src.HeightMm) ||
                     (src.AllowRotate && pa.WidthMm == src.HeightMm && pa.HeightMm == src.WidthMm));
                if (!dimOk)
                    problems.Add($"Sheet {sheet.SheetNo} piece {a + 1}: placed {pa.WidthMm}x{pa.HeightMm} does not match source dims");
                if (src != null && !seen.Add(src))
                    problems.Add($"Sheet {sheet.SheetNo} piece {a + 1}: piece placed more than once");
                if (pa.X < sheet.UsableX || pa.Y < sheet.UsableY ||
                    pa.X + pa.WidthMm > sheet.UsableX + sheet.UsableWidth ||
                    pa.Y + pa.HeightMm > sheet.UsableY + sheet.UsableHeight)
                {
                    problems.Add($"Sheet {sheet.SheetNo} piece {a + 1}: escapes usable rect at {pa.X},{pa.Y} {pa.WidthMm}x{pa.HeightMm}");
                    continue; // out-of-bounds pieces break the overlap test's meaning
                }
                placedArea += (long)pa.WidthMm * pa.HeightMm;
                for (int b = a + 1; b < sheet.Pieces.Count; b++)
                {
                    var pb = sheet.Pieces[b];
                    if (pa.X < pb.X + pb.WidthMm && pb.X < pa.X + pa.WidthMm &&
                        pa.Y < pb.Y + pb.HeightMm && pb.Y < pa.Y + pa.HeightMm)
                    {
                        problems.Add($"Sheet {sheet.SheetNo}: pieces {a + 1} and {b + 1} overlap " +
                            $"({pa.X},{pa.Y} {pa.WidthMm}x{pa.HeightMm}) vs ({pb.X},{pb.Y} {pb.WidthMm}x{pb.HeightMm})");
                    }
                }
            }
        }

        long expectedArea = 0;
        var unplacedSet = new HashSet<CutPiece>();
        foreach (var up in result.Unplaced)
        {
            if (up != null)
            {
                unplacedSet.Add(up);
                expectedArea += (long)up.WidthMm * up.HeightMm;
            }
        }
        foreach (var p in pieces)
        {
            if (!unplacedSet.Contains(p) && !seen.Contains(p))
                problems.Add($"Piece missing from plan: {p.PieceLabel}");
        }
        if (pieces.Count != result.Unplaced.Count + result.Sheets.Sum(s => s.Pieces.Count))
            problems.Add($"Piece accounting mismatch: input {pieces.Count}, placed " +
                $"{result.Sheets.Sum(s => s.Pieces.Count)} + unplaced {result.Unplaced.Count}");

        return problems;
    }

    /// <summary>
    /// Names a placed piece for an oracle message. A label must never be
    /// the thing that breaks a verifier (WPF Label, finding 10).
    /// </summary>
    private static string Label(PlacedPiece p)
    {
        if (p == null) return "(null placement)";
        if (p.Piece == null) return "(placement with no source piece)";
        return string.IsNullOrWhiteSpace(p.Piece.PieceLabel)
            ? p.Piece.GlassRef
            : p.Piece.PieceLabel;
    }

    // ── cut-sequence derivation (WPF PHASE 0 fallback) ──────────────

    /// <summary>
    /// True when every sheet either has nothing on it, already carries a
    /// cut sequence, or one can be derived for it.
    /// </summary>
    internal static bool AllSheetsCuttable(OptimizationResult result)
    {
        if (result == null) return false;

        for (int i = 0; i < result.Sheets.Count; i++)
        {
            var sheet = result.Sheets[i];
            if (sheet.Pieces.Count == 0) continue;   // nothing to cut
            if (sheet.Cuts.Count > 0) continue;      // sequence already known
            if (!TryDeriveGuillotineCuts(sheet)) return false;
        }
        return true;
    }

    /// <summary>
    /// Reconstructs a sheet's guillotine cut sequence from the pieces
    /// already sitting on it. It finds a straight line that splits the
    /// region's pieces into two non-empty groups without crossing any
    /// piece — the definition of a guillotine cut — and recurses. It
    /// never INVENTS cuts: when no such line exists the layout is not
    /// guillotine-cuttable, and the sheet is deliberately left with an
    /// empty cut list.
    /// </summary>
    internal static bool TryDeriveGuillotineCuts(SheetLayout sheet)
    {
        if (sheet == null || sheet.Pieces.Count == 0) return false;

        var cuts = new List<CutStep>();
        int budget = 20000;
        bool ok = DeriveCuts(sheet.UsableX, sheet.UsableY, sheet.UsableWidth, sheet.UsableHeight,
            new List<PlacedPiece>(sheet.Pieces), 1, cuts, ref budget);
        if (!ok) return false;

        sheet.Cuts.Clear();
        for (int i = 0; i < cuts.Count; i++)
            sheet.Cuts.Add(cuts[i]);
        return true;
    }

    private static bool DeriveCuts(int x, int y, int w, int h, List<PlacedPiece> pieces,
        int stage, List<CutStep> cuts, ref int budget)
    {
        if (pieces.Count <= 1) return true;
        if (budget <= 0) return false;

        for (int i = 0; i < pieces.Count; i++)
        {
            budget--;
            int cutAt = pieces[i].X + pieces[i].WidthMm;
            if (cutAt <= x || cutAt >= x + w) continue;
            if (!SplitAt(pieces, vertical: true, cutAt, out var left, out var right)) continue;

            int mark = cuts.Count;
            cuts.Add(DerivedCut(cuts.Count + 1, CutAxis.Vertical, cutAt, x, y, w, h, stage));
            if (DeriveCuts(x, y, cutAt - x, h, left, stage + 1, cuts, ref budget)
                && DeriveCuts(cutAt, y, x + w - cutAt, h, right, stage + 1, cuts, ref budget))
                return true;
            cuts.RemoveRange(mark, cuts.Count - mark);
        }

        for (int i = 0; i < pieces.Count; i++)
        {
            budget--;
            int cutAt = pieces[i].Y + pieces[i].HeightMm;
            if (cutAt <= y || cutAt >= y + h) continue;
            if (!SplitAt(pieces, vertical: false, cutAt, out var below, out var above)) continue;

            int mark = cuts.Count;
            cuts.Add(DerivedCut(cuts.Count + 1, CutAxis.Horizontal, cutAt, x, y, w, h, stage));
            if (DeriveCuts(x, y, w, cutAt - y, below, stage + 1, cuts, ref budget)
                && DeriveCuts(x, cutAt, w, y + h - cutAt, above, stage + 1, cuts, ref budget))
                return true;
            cuts.RemoveRange(mark, cuts.Count - mark);
        }

        return false;
    }

    /// <summary>Splits a piece list at a candidate cut line. False when
    /// the line crosses a piece (not a guillotine cut) or leaves one
    /// side empty (a cut that accomplishes nothing).</summary>
    private static bool SplitAt(List<PlacedPiece> pieces, bool vertical, int at,
        out List<PlacedPiece> low, out List<PlacedPiece> high)
    {
        low = new List<PlacedPiece>();
        high = new List<PlacedPiece>();
        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            int start = vertical ? p.X : p.Y;
            int end = start + (vertical ? p.WidthMm : p.HeightMm);
            if (end <= at) low.Add(p);
            else if (start >= at) high.Add(p);
            else return false;
        }
        return low.Count > 0 && high.Count > 0;
    }

    private static CutStep DerivedCut(int seq, CutAxis axis, int atMm,
        int x, int y, int w, int h, int stage) => new(
        seq, axis, atMm, x, y, w, h, stage, IsTrim: false);
}
