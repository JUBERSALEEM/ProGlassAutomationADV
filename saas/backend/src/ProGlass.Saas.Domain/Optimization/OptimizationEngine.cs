// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27):
//   Services/Optimization/OptimizationModels.cs — RotationPolicy (Locked /
//     Free90 ADAPTIVE), SplitMode, CutAxis, CutStep, MarginProfile (the
//     owner-confirmed house table: thin ≤6mm 15/15/15/15 kerf 3; medium
//     7–8mm 30 all kerf 3; thick ≥9mm 50/50/0/50 kerf 4 with the 50↔40
//     breakout choice), StockSize (smallest-fits stock pool),
//     CutPiece/PlacedPiece/SheetLayout/RemnantRegion/OptimizationResult.
//   Services/Optimization/GuillotinePacker.cs — the CORE deterministic
//     shelf pass: Sort(OrderBy.Area — the 2026-09-16 calibration: Area
//     reaches 17 sheets @ 82.77% where MaxSide cost 18 @ 78.18%),
//     PlaceOnOpenSheets (lexicographic best fit: fitRank, shortSide,
//     waste, orientation rank, then first sheet), TryPlaceCore (BSSF
//     with adaptive rotation), Commit, SplitRemainder (the ONE source of
//     truth for the free-rect ledger AND the cut list, kerf consumed
//     from the remainder side), OpenNewSheet (smallest stock that fits
//     first — large racks saved for the glass that needs them),
//     SortSheetsFullestFirst, Keepable, and the industrial safety-net
//     validator (bounds, dimension match, no overlap, no double
//     placement, conservation).
//   Services/Optimization/GuillotinePacker.Remnants.cs — DeriveLeftovers
//     (regions replayed through the cut sequence, kerf eaten from the
//     remainder side, piece-holding regions excluded with kerf
//     tolerance, reusable = min side ≥ MinBreakout).
// The WPF files are the source of truth; the ported subset is
// deterministic and pure — same inputs in, identical plan out.
//
// SCOPE (documented in WPF_TO_SAAS_MAPPING §1.6a): this port carries the
// QUICK base pass (shelf/Area candidate). The WPF QUICK portfolio's
// additional candidates (capacity strips, bounded best-split pattern
// beam, repeated patterns, plate patterns, industrial patterns above the
// quantity threshold, offcut reuse, local search, the consolidation
// sweep) and the whole OPTIMIZE multi-start are NOT ported yet — they
// are later slices, to be ported file-by-file the way the WPF risk table
// demands. Structural invariants (no overlap, bounds, conservation) are
// validated on every run.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlass.Saas.Domain.Optimization;

/// <summary>How pieces may be turned on the sheet (WPF RotationPolicy).
/// Free90 is ADAPTIVE: original orientation first, rotate 90° only when
/// the piece needs it.</summary>
public enum RotationPolicy
{
    Locked,
    Free90
}

/// <summary>Guillotine split direction after a placement (WPF SplitMode;
/// HorizontalFirst is the calibrated default).</summary>
public enum SplitMode
{
    HorizontalFirst,
    VerticalFirst,
    LongerLeftover
}

/// <summary>Direction a guillotine cut line runs (WPF CutAxis).</summary>
public enum CutAxis
{
    Horizontal = 0,
    Vertical = 1
}

/// <summary>
/// One guillotine cut, in the order the floor executes it (WPF CutStep).
/// The score sits on the piece's trailing edge; kerf is consumed from
/// the remainder side.
/// </summary>
public sealed record CutStep(
    int Seq,
    CutAxis Axis,
    int AtMm,
    int FromX,
    int FromY,
    int FromW,
    int FromH,
    int Stage,
    bool IsTrim);

/// <summary>
/// Cutting allowances for one glass thickness (WPF MarginProfile).
/// Margins are the edge trims cut off the raw stock; kerf is the blade
/// gap between cuts; MinBreakout is the narrowest offcut worth keeping.
/// </summary>
public sealed record MarginProfile(
    int ThicknessMm,
    int LeftMm,
    int RightMm,
    int TopMm,
    int BottomMm,
    int KerfMm,
    int MinBreakoutMm,
    bool IsCustomized = false)
{
    public const int BreakoutPrimaryMm = 50;
    public const int BreakoutAlternateMm = 40;

    /// <summary>House bucket: unlisted thicknesses ROUND UP (thicker
    /// glass needs more trim — the conservative direction); unknown or
    /// non-positive takes the thin profile (the long-standing 6mm
    /// fallback).</summary>
    public static string BucketFor(int thicknessMm) =>
        thicknessMm <= 6 ? "Thin"
        : thicknessMm <= 8 ? "Medium"
        : "Thick";

    public static bool HasBreakoutChoice(int thicknessMm) =>
        BucketFor(thicknessMm) == "Thick";

    /// <summary>House defaults (WPF DefaultFor, owner-confirmed
    /// 2026-09-21: thick glass takes no trim on the top edge).</summary>
    public static MarginProfile DefaultFor(int thicknessMm, int? minBreakoutMm = null)
    {
        int left, right, top, bottom, breakout;
        switch (BucketFor(thicknessMm))
        {
            case "Thick":
                left = right = bottom = 50;
                top = 0;
                breakout = minBreakoutMm == BreakoutAlternateMm
                    ? BreakoutAlternateMm
                    : BreakoutPrimaryMm;
                break;
            case "Medium":
                left = right = top = bottom = 30;
                breakout = 30;
                break;
            default:
                left = right = top = bottom = 15;
                breakout = 15;
                break;
        }

        return new MarginProfile(
            thicknessMm, left, right, top, bottom,
            thicknessMm >= 10 ? 4 : 3, breakout);
    }
}

/// <summary>One stock sheet size in the run's pool (WPF StockSize).
/// MaxSheets 0 = the explicit "no limit" sentinel.</summary>
public sealed record StockSize(
    int WidthMm,
    int HeightMm,
    int ThicknessMm,
    string Color = "",
    string Label = "",
    string MaterialKey = "",
    int MaxSheets = 0)
{
    public const int UnlimitedSheets = 0;

    public string SizeLabel => string.IsNullOrWhiteSpace(Label)
        ? $"{WidthMm} × {HeightMm}"
        : Label;
}

/// <summary>One piece to cut (already expanded from row quantities).</summary>
public sealed record CutPiece(
    string JobRef,
    string GlassRef,
    string SpecLabel,
    string Position,
    string Color,
    int ThicknessMm,
    int WidthMm,
    int HeightMm,
    bool AllowRotate = true,
    string PieceLabel = "",
    string MaterialKey = "",
    int SpecGroupNumber = 0,
    int SourceRowSerial = 0)
{
    /// <summary>The per-pane glass composition merge key (WPF
    /// PaneDisplayName): two specs sharing the same outer pane collapse
    /// into ONE cutting group.</summary>
    public string Composition =>
        Proforma.SpecGrammar.ResolveAllPanes(SpecLabel).Count > 0
            ? Proforma.ProductionDistributionEngine.PaneDisplayName(
                Proforma.SpecGrammar.ResolveAllPanes(SpecLabel)[0])
            : SpecLabel;
}

/// <summary>One placed piece on one sheet (integer mm coordinates).</summary>
public sealed record PlacedPiece(
    CutPiece Piece,
    int X,
    int Y,
    int WidthMm,
    int HeightMm,
    bool Rotated);

/// <summary>A leftover region on a cut sheet (WPF RemnantRegion).</summary>
public sealed record RemnantRegion(
    int SheetNo,
    double X,
    double Y,
    double WidthMm,
    double HeightMm)
{
    public bool IsUsable { get; set; }

    public double MinSideMm =>
        Math.Min(WidthMm, HeightMm);

    /// <summary>Rotated-agnostic size label (WPF RemnantRegion.SizeLabel,
    /// ported with Phase 21 for the CNC offcut annotations).</summary>
    public string SizeLabel => string.Format(
        System.Globalization.CultureInfo.InvariantCulture,
        "{0:0} x {1:0}", WidthMm, HeightMm);
}

/// <summary>One optimized sheet with its placements and metrics.</summary>
public sealed class SheetLayout
{
    public int SheetNo { get; set; }
    public StockSize Stock { get; init; } = null!;
    public int UsableX { get; init; }
    public int UsableY { get; init; }
    public int UsableWidth { get; init; }
    public int UsableHeight { get; init; }
    public List<PlacedPiece> Pieces { get; } = new();
    public List<CutStep> Cuts { get; } = new();
    public List<RemnantRegion> Leftovers { get; } = new();

    public int TrimCutCount
    {
        get
        {
            var n = 0;
            for (var i = 0; i < Cuts.Count; i++)
            {
                if (Cuts[i].IsTrim)
                {
                    n++;
                }
            }
            return n;
        }
    }

    public decimal UsedSqm =>
        Math.Round(
            Pieces.Sum(p => (decimal)p.WidthMm * p.HeightMm) / 1_000_000m,
            4, MidpointRounding.AwayFromZero);

    public decimal StockSqm =>
        Math.Round(
            (decimal)Stock.WidthMm * Stock.HeightMm / 1_000_000m,
            4, MidpointRounding.AwayFromZero);

    public decimal UtilizationPct =>
        StockSqm <= 0 ? 0
        : Math.Round(UsedSqm * 100m / StockSqm, 2, MidpointRounding.AwayFromZero);
}

/// <summary>The run result (WPF OptimizationResult).</summary>
public sealed class OptimizationResult
{
    public List<SheetLayout> Sheets { get; } = new();
    public List<CutPiece> Unplaced { get; } = new();
    public RotationPolicy Rotation { get; init; }
    public List<string> ScaleNotes { get; } = new();
    public string Strategy { get; set; } = "";
    public List<string> ValidationProblems { get; } = new();

    public bool ConservationVerified { get; set; }
    public bool GeometryVerified { get; set; }
    public bool ConservationChecked { get; set; }
    public bool GeometryChecked { get; set; }

    public bool HasValidationFailure => ValidationProblems.Count > 0;

    /// <summary>Sheets the plan actually reaches for (WPF
    /// SheetsUsed).</summary>
    public int SheetsUsed => Sheets.Count;

    /// <summary>
    /// Records the oracle verdict on the plan that is actually returned
    /// (WPF RecordValidation): distinct problems only, flags only ever
    /// turn on.
    /// </summary>
    public void RecordValidation(IEnumerable<string> problems, bool conservationChecked, bool geometryChecked)
    {
        if (problems != null)
        {
            foreach (var p in problems)
            {
                if (!string.IsNullOrWhiteSpace(p) && !ValidationProblems.Contains(p))
                {
                    ValidationProblems.Add(p);
                }
            }
        }
        if (conservationChecked) ConservationChecked = true;
        if (geometryChecked) GeometryChecked = true;
    }

    public decimal TotalUsedSqm =>
        Math.Round(Sheets.Sum(s => s.UsedSqm), 4, MidpointRounding.AwayFromZero);

    public decimal TotalStockSqm =>
        Math.Round(Sheets.Sum(s => s.StockSqm), 4, MidpointRounding.AwayFromZero);

    public decimal UtilizationPct =>
        TotalStockSqm <= 0 ? 0
        : Math.Round(TotalUsedSqm * 100m / TotalStockSqm, 2,
            MidpointRounding.AwayFromZero);

    /// <summary>The fewest sheets any plan could need (total piece area
    /// over the largest usable stock area).</summary>
    public int SheetsLowerBound(decimal largestUsableSqm)
    {
        var pieceSqm = Math.Round(
            (Sheets.SelectMany(s => s.Pieces)
                .Sum(p => (decimal)p.WidthMm * p.HeightMm)
                + Unplaced.Sum(p => (decimal)p.WidthMm * p.HeightMm))
            / 1_000_000m, 4, MidpointRounding.AwayFromZero);
        return largestUsableSqm <= 0 ? 0
            : (int)Math.Ceiling(pieceSqm / largestUsableSqm);
    }
}

/// <summary>
/// The core deterministic guillotine packer (WPF GuillotinePacker).
/// Pure — no store, no clock. The QUICK base pass lives here; the
/// OPTIMIZE stage chassis lives in OptimizationEngine.Optimize.cs
/// (Phase 25a).
/// </summary>
public static partial class GuillotinePacker
{
    internal struct FreeRect
    {
        public int X, Y, W, H;
        /// <summary>Which cut stage produced this rectangle: 0 for the
        /// raw usable sheet. A rect at depth d hosts a placement whose
        /// first cut is stage d+1.</summary>
        public int Depth;

        public FreeRect(int x, int y, int w, int h)
        {
            X = x; Y = y; W = w; H = h; Depth = 0;
        }

        public FreeRect(int x, int y, int w, int h, int depth)
        {
            X = x; Y = y; W = w; H = h; Depth = depth;
        }
    }

    private sealed class SheetState
    {
        public SheetLayout Layout { get; }
        public List<FreeRect> Free { get; } = new();
        public int StockIndex { get; }
        public int MaxFreeW;
        public int MaxFreeH;
        public long PlacedArea;

        /// <summary>Sealed by the bounded open-sheet window (Phase 25a):
        /// the sheet already received every placement attempt since it
        /// opened, so it is near-final and placement stops scanning it.
        /// </summary>
        public bool IsClosed { get; set; }

        /// <summary>Evacuated/removed by consolidation — the rollback
        /// snapshots and the open-window queue skip removed sheets.</summary>
        public bool IsRemoved { get; set; }

        public SheetState(SheetLayout layout, int stockIndex)
        {
            Layout = layout;
            StockIndex = stockIndex;
            Free.Add(new FreeRect(
                layout.UsableX, layout.UsableY,
                layout.UsableWidth, layout.UsableHeight));
            RefreshFreeBounds();
            PlacedArea = 0;
        }

        public void RefreshFreeBounds()
        {
            var mw = 0;
            var mh = 0;
            for (var i = 0; i < Free.Count; i++)
            {
                if (Free[i].W > mw) mw = Free[i].W;
                if (Free[i].H > mh) mh = Free[i].H;
            }
            MaxFreeW = mw;
            MaxFreeH = mh;
        }

        /// <summary>Recompute bounds AND area (WPF RefreshAll). Use
        /// after a bulk rebuild of either list (construction, rollback,
        /// MaxRects commit).</summary>
        public void RefreshAll()
        {
            RefreshFreeBounds();
            long a = 0;
            var ps = Layout.Pieces;
            for (int i = 0; i < ps.Count; i++) a += (long)ps[i].WidthMm * ps[i].HeightMm;
            PlacedArea = a;
        }
    }

    /// <summary>A placement described by value (WPF PlacementCandidate —
    /// allocation-free scoring).</summary>
    private struct PlacementCandidate
    {
        public int X, Y, W, H;
        public bool Rotated;
        public int OrderRank, FitRank;
        public long ShortSide, Waste;

        public PlacedPiece Materialize(CutPiece piece) =>
            new(piece, X, Y, W, H, Rotated);
    }

    // ── the QUICK base pass ─────────────────────────────────────────

    /// <summary>
    /// PackQuick core: sort by AREA (the 2026-09-16 calibration), run the
    /// shelf pass, derive leftovers, validate. Deterministic.
    /// </summary>
    public static OptimizationResult PackQuick(
        IReadOnlyList<CutPiece> pieces,
        IReadOnlyList<StockSize> stocks,
        MarginProfile margins,
        RotationPolicy rotation,
        SplitMode split = SplitMode.HorizontalFirst)
    {
        if (pieces is null)
        {
            throw new ArgumentNullException(nameof(pieces));
        }
        if (stocks is null || stocks.Count == 0)
        {
            throw new ArgumentException(
                "At least one stock size is required.", nameof(stocks));
        }
        if (margins is null)
        {
            throw new ArgumentNullException(nameof(margins));
        }

        // The governor bounds QUICK too (WPF PlanForQuick): consolidation
        // sweeps 3 below 2,000 parts, 2 above, 5-minute budget.
        var quickPlan = OptimizationSearchGovernor.PlanForQuick(pieces.Count);
        var result = RunPass(SortByArea(pieces), stocks, margins, rotation, split,
            consolidationSweeps: quickPlan.MaxConsolidationSweeps,
            openSheetWindow: quickPlan.OpenSheetWindow);
        result.Strategy = $"QUICK SHELF (Area / {split})";
        result.ScaleNotes.Add(
            "SaaS port (Phase 25a): the QUICK shelf pass with the " +
            "consolidation sweep. The WPF's additional QUICK candidates " +
            "(capacity strips, bounded pattern beam, repeated patterns, " +
            "plate patterns) are later slices — documented in mapping " +
            "§1.6b — so this plan is valid and cuttable but not yet the " +
            "WPF's best candidate.");

        // Give every sheet its leftover regions, derived from the cut
        // sequence (WPF DeriveLeftovers).
        DeriveLeftovers(result,
            Math.Max(margins.MinBreakoutMm, 1), margins.KerfMm);

        // Industrial safety net: a plan nobody validated is a plan nobody
        // can cut (WPF CheckCandidate → ValidatePlan).
        Validate(result, pieces, stocks, margins);
        return result;
    }

    internal static OptimizationResult RunPass(
        IReadOnlyList<CutPiece> orderedPieces,
        IReadOnlyList<StockSize> stocks,
        MarginProfile margins,
        RotationPolicy rotation,
        SplitMode split,
        int consolidationSweeps = int.MaxValue,
        int openSheetWindow = int.MaxValue)
    {
        var result = new OptimizationResult { Rotation = rotation };
        if (orderedPieces.Count == 0)
        {
            return result;
        }

        var sheets = new List<SheetState>();
        var sheetsPerStock = new int[stocks.Count];
        var ctx = new PackingContext { OpenWindow = openSheetWindow };

        foreach (var piece in orderedPieces)
        {
            if (!PlaceOnOpenSheets(sheets, piece, margins, rotation, split))
            {
                if (!OpenNewSheet(
                        sheets, sheetsPerStock, stocks, piece,
                        margins, rotation, split, ctx))
                {
                    result.Unplaced.Add(piece);
                }
            }
        }

        // LiSEC-style leftover squeeze: try to empty every sheet from
        // sparsest to densest (WPF ConsolidateAll, Phase 25a).
        ConsolidateAll(sheets, margins, rotation, split, consolidationSweeps,
            ctx: ctx);

        SortSheetsFullestFirst(sheets);

        result.Strategy = $"GRID ({split})";
        for (var i = 0; i < sheets.Count; i++)
        {
            result.Sheets.Add(sheets[i].Layout);
        }
        for (var i = 0; i < result.Sheets.Count; i++)
        {
            result.Sheets[i].SheetNo = i + 1;
        }
        return result;
    }

    /// <summary>Industrial report order: fullest sheet first (WPF
    /// SortSheetsFullestFirst).</summary>
    private static void SortSheetsFullestFirst(List<SheetState> sheets)
    {
        sheets.Sort((a, b) =>
        {
            var cmp = b.PlacedArea.CompareTo(a.PlacedArea);
            return cmp != 0 ? cmp
                : b.Layout.Pieces.Count.CompareTo(a.Layout.Pieces.Count);
        });
    }

    /// <summary>QUICK's calibrated order: Area descending (WPF Sort,
    /// OrderBy.Area — MaxSide cost a whole sheet on the owner's own
    /// benchmark).</summary>
    internal static List<CutPiece> SortByArea(IReadOnlyList<CutPiece> pieces) =>
        pieces
            .OrderByDescending(p => (long)p.WidthMm * p.HeightMm)
            .ThenByDescending(p => Math.Max(p.WidthMm, p.HeightMm))
            .ThenBy(p => p.GlassRef, StringComparer.Ordinal)
            .ThenBy(p => p.PieceLabel, StringComparer.Ordinal)
            .ToList();

    // ── placement ───────────────────────────────────────────────────

    private static bool PlaceOnOpenSheets(
        List<SheetState> sheets, CutPiece piece, MarginProfile margins,
        RotationPolicy rotation, SplitMode split)
    {
        // Lexicographic minimum over (fitRank, shortSide, waste,
        // orderRank, sheetIndex); because si ascends, the FIRST sheet
        // still wins an exact tie.
        var found = false;
        var bestSheet = -1;
        var bestCand = default(PlacementCandidate);

        for (var si = 0; si < sheets.Count; si++)
        {
            var sheet = sheets[si];
            if (!TryPlaceCore(sheet, piece, rotation, out var candidate))
            {
                continue;
            }

            if (!found
                || candidate.FitRank < bestCand.FitRank
                || (candidate.FitRank == bestCand.FitRank
                    && candidate.ShortSide < bestCand.ShortSide)
                || (candidate.FitRank == bestCand.FitRank
                    && candidate.ShortSide == bestCand.ShortSide
                    && candidate.Waste < bestCand.Waste)
                || (candidate.FitRank == bestCand.FitRank
                    && candidate.ShortSide == bestCand.ShortSide
                    && candidate.Waste == bestCand.Waste
                    && candidate.OrderRank < bestCand.OrderRank)
                || (candidate.FitRank == bestCand.FitRank
                    && candidate.ShortSide == bestCand.ShortSide
                    && candidate.Waste == bestCand.Waste
                    && candidate.OrderRank == bestCand.OrderRank
                    && si < bestSheet))
            {
                found = true;
                bestSheet = si;
                bestCand = candidate;

                // Perfect fit shortcut: 0 waste unrotated cannot be
                // improved upon.
                if (candidate.FitRank == 0 && candidate.Waste == 0
                    && candidate.OrderRank == 0)
                {
                    break;
                }
            }
        }

        if (!found)
        {
            return false;
        }

        Commit(sheets[bestSheet], bestCand.Materialize(piece), margins, split);
        return true;
    }

    /// <summary>
    /// Best fit for one piece on one sheet. ADAPTIVE rotation: the
    /// original orientation is preferred (rank 0); rotated placements
    /// are only chosen when nothing accepts the piece unrotated.
    /// </summary>
    private static bool TryPlaceCore(
        SheetState sheet, CutPiece piece, RotationPolicy rotation,
        out PlacementCandidate best)
    {
        best = default;

        var ow = piece.WidthMm;
        var oh = piece.HeightMm;
        var canRotate = rotation == RotationPolicy.Free90
            && piece.AllowRotate && ow != oh;
        var orientationCount = canRotate ? 2 : 1;

        // Sound whole-sheet prune: an orientation exceeding the exact
        // maxima over the free rects exceeds every rect.
        var anyFits = false;
        for (var oi = 0; oi < orientationCount; oi++)
        {
            var wProbe = oi == 0 ? ow : oh;
            var hProbe = oi == 0 ? oh : ow;
            if (wProbe <= sheet.MaxFreeW && hProbe <= sheet.MaxFreeH)
            {
                anyFits = true;
                break;
            }
        }
        if (!anyFits)
        {
            return false;
        }

        var found = false;
        var bRank = int.MaxValue;
        var bFit = int.MaxValue;
        var bX = 0;
        var bY = 0;
        var bW = 0;
        var bH = 0;
        var bSs = long.MaxValue;
        var bWaste = long.MaxValue;
        var bRot = false;

        for (var oi = 0; oi < orientationCount; oi++)
        {
            var w = oi == 0 ? ow : oh;
            var h = oi == 0 ? oh : ow;
            var rot = oi == 1;
            var orientationRank = oi;
            if (w > sheet.MaxFreeW || h > sheet.MaxFreeH)
            {
                continue;
            }

            for (var i = 0; i < sheet.Free.Count; i++)
            {
                var rect = sheet.Free[i];
                if (w > rect.W || h > rect.H)
                {
                    continue;
                }

                // Geometry first (exact edge / BSSF), 0 vs 90 only as a
                // tie-break.
                var exactW = w == rect.W;
                var exactH = h == rect.H;
                int fit = exactW && exactH ? 0 : exactW || exactH ? 1 : 2;
                var ss = Math.Min(rect.W - w, rect.H - h);
                var leftover = (long)rect.W * rect.H - (long)w * h;

                var better = !found
                    || fit < bFit
                    || (fit == bFit && ss < bSs)
                    || (fit == bFit && ss == bSs && leftover < bWaste)
                    || (fit == bFit && ss == bSs && leftover == bWaste
                        && orientationRank < bRank)
                    || (fit == bFit && ss == bSs && leftover == bWaste
                        && orientationRank == bRank && rect.X < bX)
                    || (fit == bFit && ss == bSs && leftover == bWaste
                        && orientationRank == bRank && rect.X == bX && rect.Y < bY);
                if (!better)
                {
                    continue;
                }

                found = true;
                bRank = orientationRank;
                bFit = fit;
                bSs = ss;
                bWaste = leftover;
                bX = rect.X;
                bY = rect.Y;
                bW = w;
                bH = h;
                bRot = rot;
            }
        }

        if (!found)
        {
            return false;
        }

        best.X = bX;
        best.Y = bY;
        best.W = bW;
        best.H = bH;
        best.Rotated = bRot;
        best.OrderRank = bRank;
        best.FitRank = bFit;
        best.ShortSide = bSs;
        best.Waste = bWaste;
        return true;
    }

    private static void Commit(
        SheetState sheet, PlacedPiece placed, MarginProfile margins,
        SplitMode split, int minBreakout = 0)
    {
        var kerf = margins.KerfMm;

        var idx = sheet.Free.FindIndex(r =>
            placed.X >= r.X && placed.Y >= r.Y
            && placed.X + placed.WidthMm <= r.X + r.W
            && placed.Y + placed.HeightMm <= r.Y + r.H);
        if (idx < 0)
        {
            throw new InvalidOperationException(
                "Placement escaped the free-rectangle ledger.");
        }

        var rect = sheet.Free[idx];
        sheet.Free.RemoveAt(idx);

        SplitRemainder(rect, placed.WidthMm, placed.HeightMm, kerf,
            minBreakout, split, sheet.Free, sheet.Layout.Cuts);

        sheet.Layout.Pieces.Add(placed);
        sheet.PlacedArea += (long)placed.WidthMm * placed.HeightMm;
        sheet.RefreshFreeBounds();
    }

    // ── stock selection ─────────────────────────────────────────────

    private static bool OpenNewSheet(
        List<SheetState> sheets, int[] sheetsPerStock, IReadOnlyList<StockSize> stocks,
        CutPiece piece, MarginProfile margins, RotationPolicy rotation,
        SplitMode split, PackingContext? ctx = null)
    {
        // "Best sheet size" rule: the SMALLEST stock that fits the piece
        // first, so large racks are saved for the glass that needs them.
        var byPreference = Enumerable.Range(0, stocks.Count)
            .Where(i => sheetsPerStock[i] < stocks[i].MaxSheets
                || stocks[i].MaxSheets == StockSize.UnlimitedSheets)
            .OrderBy(i => (long)stocks[i].WidthMm * stocks[i].HeightMm)
            .ThenBy(i => i)
            .ToList();

        foreach (var i in byPreference)
        {
            var stock = stocks[i];

            var usableW = Math.Max(
                0, stock.WidthMm - margins.LeftMm - margins.RightMm);
            var usableH = Math.Max(
                0, stock.HeightMm - margins.TopMm - margins.BottomMm);

            var fitsUnrotated = piece.WidthMm <= usableW && piece.HeightMm <= usableH;
            var fitsRotated = rotation == RotationPolicy.Free90
                && piece.AllowRotate
                && piece.HeightMm <= usableW && piece.WidthMm <= usableH;
            if (!fitsUnrotated && !fitsRotated)
            {
                continue;
            }

            var layout = new SheetLayout
            {
                SheetNo = sheets.Count + 1,
                Stock = stock,
                UsableX = margins.LeftMm,
                UsableY = margins.TopMm,
                UsableWidth = usableW,
                UsableHeight = usableH
            };
            var state = new SheetState(layout, i);
            sheets.Add(state);
            sheetsPerStock[i]++;
            if (ctx != null) RegisterOpenedSheet(ctx, state);

            if (!TryPlaceCore(state, piece, rotation, out var candidate))
            {
                throw new InvalidOperationException(
                    "New sheet rejected the piece that opened it.");
            }
            Commit(state, candidate.Materialize(piece), margins, split);
            return true;
        }

        return false;
    }

    // ── the split (ONE source of truth for ledger + cut list) ───────

    internal static void SplitRemainder(
        FreeRect host, int pieceW, int pieceH, int kerf, int minBreakout,
        SplitMode split, List<FreeRect> output, List<CutStep>? cuts = null)
    {
        var rightW = host.W - pieceW - kerf;
        var bottomH = host.H - pieceH - kerf;

        var verticalFirst = split == SplitMode.VerticalFirst
            || split == SplitMode.LongerLeftover && rightW >= bottomH;

        // A placement makes at most two cuts. The FIRST spans the whole
        // host rectangle; the SECOND runs inside the strip the first one
        // created, one stage deeper. When the piece already spans the
        // host along that axis the first cut is not needed at all.
        var stageFirst = host.Depth + 1;

        if (verticalFirst)
        {
            var needsFirst = rightW > 0;
            if (needsFirst)
            {
                // Vertical cut, full height: splits off the right strip.
                var kept = Keepable(rightW, host.H, minBreakout);
                if (kept)
                {
                    output.Add(new FreeRect(
                        host.X + pieceW + kerf, host.Y, rightW, host.H,
                        stageFirst));
                }
                RecordCut(cuts, CutAxis.Vertical, host.X + pieceW,
                    host.X, host.Y, host.W, host.H, stageFirst, !kept);
            }
            if (bottomH > 0)
            {
                // Horizontal cut inside the left column: frees the piece.
                var stage = needsFirst ? stageFirst + 1 : stageFirst;
                var kept = Keepable(pieceW, bottomH, minBreakout);
                if (kept)
                {
                    output.Add(new FreeRect(
                        host.X, host.Y + pieceH + kerf, pieceW, bottomH,
                        stage));
                }
                RecordCut(cuts, CutAxis.Horizontal, host.Y + pieceH,
                    host.X, host.Y, needsFirst ? pieceW : host.W, host.H,
                    stage, !kept);
            }
        }
        else
        {
            var needsFirst = bottomH > 0;
            if (needsFirst)
            {
                // Horizontal cut, full width: splits off the bottom strip.
                var kept = Keepable(host.W, bottomH, minBreakout);
                if (kept)
                {
                    output.Add(new FreeRect(
                        host.X, host.Y + pieceH + kerf, host.W, bottomH,
                        stageFirst));
                }
                RecordCut(cuts, CutAxis.Horizontal, host.Y + pieceH,
                    host.X, host.Y, host.W, host.H, stageFirst, !kept);
            }
            if (rightW > 0)
            {
                // Vertical cut inside the top strip: frees the piece.
                var stage = needsFirst ? stageFirst + 1 : stageFirst;
                var kept = Keepable(rightW, pieceH, minBreakout);
                if (kept)
                {
                    output.Add(new FreeRect(
                        host.X + pieceW + kerf, host.Y, rightW, pieceH,
                        stage));
                }
                RecordCut(cuts, CutAxis.Vertical, host.X + pieceW,
                    host.X, host.Y, host.W, needsFirst ? pieceH : host.H,
                    stage, !kept);
            }
        }
    }

    internal static bool Keepable(int w, int h, int minBreakout) =>
        minBreakout <= 0 || w >= minBreakout && h >= minBreakout;

    private static void RecordCut(
        List<CutStep>? cuts, CutAxis axis, int atMm,
        int x, int y, int w, int h, int stage, bool isTrim)
    {
        cuts?.Add(new CutStep(cuts.Count + 1, axis, atMm, x, y, w, h,
            stage, isTrim));
    }

    // ── leftovers (WPF DeriveLeftovers) ─────────────────────────────

    public static void DeriveLeftovers(
        SheetLayout sheet, int minSideMm, int kerfMm)
    {
        sheet.Leftovers.Clear();

        if (sheet.UsableWidth <= 0 || sheet.UsableHeight <= 0)
        {
            return;
        }
        if (sheet.Cuts.Count == 0 && sheet.Pieces.Count == 0)
        {
            return;
        }

        var regions = new List<RemnantRegion>
        {
            new(sheet.SheetNo, sheet.UsableX, sheet.UsableY,
                sheet.UsableWidth, sheet.UsableHeight)
        };

        foreach (var cut in sheet.Cuts)
        {
            var next = new List<RemnantRegion>(regions.Count + 1);
            var splitAny = false;

            for (var i = 0; i < regions.Count; i++)
            {
                var r = regions[i];
                if (!Crosses(cut, r))
                {
                    next.Add(r);
                    continue;
                }

                splitAny = true;
                var kerf = kerfMm > 0 ? kerfMm : 0;

                if (cut.Axis == CutAxis.Vertical)
                {
                    var leftWidth = cut.AtMm - r.X;
                    next.Add(new RemnantRegion(
                        sheet.SheetNo, r.X, r.Y, leftWidth, r.HeightMm));
                    // The blade eats kerf from the remainder side.
                    next.Add(new RemnantRegion(
                        sheet.SheetNo, cut.AtMm + kerf, r.Y,
                        r.WidthMm - leftWidth - kerf, r.HeightMm));
                }
                else
                {
                    var bottomHeight = cut.AtMm - r.Y;
                    next.Add(new RemnantRegion(
                        sheet.SheetNo, r.X, r.Y, r.WidthMm, bottomHeight));
                    next.Add(new RemnantRegion(
                        sheet.SheetNo, r.X, cut.AtMm + kerf,
                        r.WidthMm, r.HeightMm - bottomHeight - kerf));
                }
            }

            if (splitAny)
            {
                regions = next;
            }
        }

        // A region holding a piece is not a leftover (tolerance covers
        // the kerf between a piece and the next cut line).
        var tolerance = kerfMm > 0 ? kerfMm + 0.5 : 0.5;

        for (var i = 0; i < regions.Count; i++)
        {
            var r = regions[i];
            if (r.WidthMm <= 0.5 || r.HeightMm <= 0.5)
            {
                continue;
            }
            if (HoldsPiece(sheet, r, tolerance))
            {
                continue;
            }

            r.IsUsable = r.MinSideMm >= minSideMm;
            sheet.Leftovers.Add(r);
        }
    }

    public static void DeriveLeftovers(
        OptimizationResult result, int minSideMm, int kerfMm)
    {
        foreach (var sheet in result.Sheets)
        {
            DeriveLeftovers(sheet, minSideMm, kerfMm);
        }
    }

    private static bool Crosses(CutStep cut, RemnantRegion r)
    {
        if (cut.Axis == CutAxis.Vertical)
        {
            if (cut.AtMm <= r.X + 0.5 || cut.AtMm >= r.X + r.WidthMm - 0.5)
            {
                return false;
            }
            return cut.FromY <= r.Y + 0.5
                && cut.FromY + cut.FromH >= r.Y + r.HeightMm - 0.5;
        }

        if (cut.AtMm <= r.Y + 0.5 || cut.AtMm >= r.Y + r.HeightMm - 0.5)
        {
            return false;
        }
        return cut.FromX <= r.X + 0.5
            && cut.FromX + cut.FromW >= r.X + r.WidthMm - 0.5;
    }

    private static bool HoldsPiece(
        SheetLayout sheet, RemnantRegion r, double tolerance)
    {
        for (var i = 0; i < sheet.Pieces.Count; i++)
        {
            var p = sheet.Pieces[i];
            if (p.X >= r.X - tolerance
                && p.Y >= r.Y - tolerance
                && p.X + p.WidthMm <= r.X + r.WidthMm + tolerance
                && p.Y + p.HeightMm <= r.Y + r.HeightMm + tolerance)
            {
                return true;
            }
        }
        return false;
    }

    // ── the industrial safety net (WPF ValidatePlan) ────────────────

    /// <summary>
    /// Per sheet: pieces within the usable rectangle, dimensions matching
    /// their source CutPiece (rotation allowed), no two pieces
    /// overlapping, no piece placed twice. Plan-wide: placed + unplaced
    /// account for every input piece.
    /// </summary>
    public static void Validate(
        OptimizationResult result,
        IReadOnlyList<CutPiece> input,
        IReadOnlyList<StockSize> stocks,
        MarginProfile margins)
    {
        var problems = new List<string>();
        var placedIds = new HashSet<CutPiece>();

        foreach (var sheet in result.Sheets)
        {
            for (var i = 0; i < sheet.Pieces.Count; i++)
            {
                var a = sheet.Pieces[i];
                if (a.X < sheet.UsableX
                    || a.Y < sheet.UsableY
                    || a.X + a.WidthMm > sheet.UsableX + sheet.UsableWidth
                    || a.Y + a.HeightMm > sheet.UsableY + sheet.UsableHeight)
                {
                    problems.Add(
                        $"Sheet {sheet.SheetNo}: piece {a.Piece.PieceLabel} " +
                        "escapes the usable rectangle.");
                }

                if ((a.WidthMm == a.Piece.WidthMm && a.HeightMm == a.Piece.HeightMm
                     && !a.Rotated)
                    || (a.WidthMm == a.Piece.HeightMm
                        && a.HeightMm == a.Piece.WidthMm && a.Rotated))
                {
                    // dimensions match
                }
                else
                {
                    problems.Add(
                        $"Sheet {sheet.SheetNo}: piece {a.Piece.PieceLabel} " +
                        "dimensions do not match its source piece.");
                }

                if (!placedIds.Add(a.Piece))
                {
                    problems.Add(
                        $"Piece {a.Piece.PieceLabel} is placed more than once.");
                }

                for (var j = i + 1; j < sheet.Pieces.Count; j++)
                {
                    var b = sheet.Pieces[j];
                    if (a.X < b.X + b.WidthMm && b.X < a.X + a.WidthMm
                        && a.Y < b.Y + b.HeightMm && b.Y < a.Y + a.HeightMm)
                    {
                        problems.Add(
                            $"Sheet {sheet.SheetNo}: pieces " +
                            $"{a.Piece.PieceLabel} and {b.Piece.PieceLabel} " +
                            "overlap.");
                    }
                }
            }
        }

        var total = result.Sheets.Sum(s => s.Pieces.Count) + result.Unplaced.Count;
        if (total != input.Count)
        {
            problems.Add(
                $"Conservation failure: {total} accounted for, " +
                $"{input.Count} expected.");
        }

        result.ValidationProblems.AddRange(problems);
        result.ConservationChecked = true;
        result.GeometryChecked = true;
        result.ConservationVerified = total == input.Count;
        result.GeometryVerified = problems.Count == 0;
    }
}
