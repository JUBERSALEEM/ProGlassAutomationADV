// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27), Phase 25b — the
// grid candidate families:
//   Services/Optimization/GuillotinePacker.Advanced.cs (342 lines —
//     FinishFromSheets, OpenBlank, RunSheetFill, RunWallSeed,
//     RunMaxRects + MaxRectsPlace/PruneFree with the frame
//     decomposition and the depth ledger)
//   Services/Optimization/GuillotinePacker.BestSplit.cs (356 lines —
//     FillOneSheet, RunBestSplit greedy per-sheet split selection,
//     RunBestSplitBeam with the remainder lower bound and dedup,
//     LargestFreeRect, the remainder-aware split choice E2.2)
//   Services/Optimization/GuillotinePacker.CapacityStrips.cs (352
//     lines — RunCapacityStrips, the StripPool intrusive frontier,
//     TryStripOrientation/WidenStrip/FindStripPiece, the strip cut
//     recording PHASE 0)
//   Services/Optimization/GuillotinePacker.PatternSearch.cs (101
//     lines — ImprovePatterns, the bounded best-split pattern beam
//     with the tail-repair window)
//   Services/Optimization/GuillotinePacker.SmartPlacement.cs (464
//     lines — PackSmart, SmartOption, TrySmartPlace: capacity-
//     preserving, breakout-aware placement scored by what SURVIVES)
//   plus the Orientations / TryPlace(2-out) / StagedPlacementAllowed
//   helpers from GuillotinePacker.cs.
//
// NOT ported (documented): Services/Optimization/GuillotinePacker.Smart.cs
// (RunSkyline/ImproveByRepack) is UNREACHABLE DEAD CODE in the WPF — its
// own header records that nothing calls it, that it was measured (200
// runs over five orderings: beat the candidate pool 0 times; 17 sheets
// on the Clear group against BestSplit's 16) and that the recommendation
// is deletion. Porting dead code would be inventing work; the WPF's
// decision is preserved instead.
//
// Staged-cut enforcement (CutPolicy on SheetState, SplitRemainder's
// policy arg, RectCanHost) stays unwired in this slice — the
// StagedPlacementAllowed guard is ported and always consulted with a
// null policy, which is exactly what every Free-level run evaluates.
// ═══════════════════════════════════════════════════════════════════════

using System.Globalization;

namespace ProGlass.Saas.Domain.Optimization;

public static partial class GuillotinePacker
{
    // (AllSplits is declared once, in OptimizationEngine.Optimize.cs.)

    // ── shared helpers (GuillotinePacker.cs verbatim) ────────────────

    private static List<(int W, int H, bool Rotated, int Rank)> Orientations(
        CutPiece piece, RotationPolicy rotation)
    {
        var list = new List<(int, int, bool, int)> { (piece.WidthMm, piece.HeightMm, false, 0) };
        if (rotation == RotationPolicy.Free90 && piece.AllowRotate
            && piece.WidthMm != piece.HeightMm)
        {
            list.Add((piece.HeightMm, piece.WidthMm, true, 1));
        }
        return list;
    }

    /// <summary>Local best fit for one piece: exact edges / short-side
    /// remainder first, orientation only a tie-break (the 2-out TryPlace
    /// overload the advanced candidates use).</summary>
    private static PlacedPiece? TryPlace(
        SheetState sheet, CutPiece piece, RotationPolicy rotation,
        out int rank, out long waste)
    {
        if (!TryPlaceCore(sheet, piece, rotation, out var c))
        {
            rank = int.MaxValue;
            waste = long.MaxValue;
            return null;
        }

        rank = c.OrderRank;
        waste = c.Waste;
        return c.Materialize(piece);
    }

    /// <summary>
    /// The stage-budget guard (WPF StagedPlacementAllowed, verbatim). A
    /// null or non-depth-constrained policy always passes — the Free
    /// levels take no constraint path at all.
    /// </summary>
    private static bool StagedPlacementAllowed(in FreeRect rect, int w, int h,
        SplitMode split, CutPolicy? policy)
    {
        if (policy == null || !policy.IsDepthConstrained) return true;
        if (rect.Depth + 2 <= policy.MaxStages) return true;

        bool verticalFirst = split == SplitMode.VerticalFirst ||
            (split == SplitMode.LongerLeftover && (rect.W - w) >= (rect.H - h));
        return verticalFirst ? rect.H - h <= 0 : rect.W - w <= 0;
    }

    private static OptimizationResult FinishFromSheets(
        List<SheetState> sheets, List<CutPiece> unplaced, RotationPolicy rotation,
        string strategy = "")
    {
        var result = new OptimizationResult { Rotation = rotation, Strategy = strategy };
        foreach (var p in unplaced) result.Unplaced.Add(p);
        SortSheetsFullestFirst(sheets);
        for (int i = 0; i < sheets.Count; i++)
        {
            sheets[i].Layout.SheetNo = i + 1;
            result.Sheets.Add(sheets[i].Layout);
        }
        return result;
    }

    /// <summary>Opens a blank sheet for the smallest stock that fits the
    /// piece (WPF OpenBlank; consumes quota, does NOT append to a list).
    /// The CutPolicy parameter is dropped — staged enforcement is a later
    /// slice and every current caller passes null.</summary>
    private static SheetState? OpenBlank(
        IReadOnlyList<StockSize> stocks, int[] sheetsPerStock,
        CutPiece piece, MarginProfile margins, RotationPolicy rotation,
        int alreadyOpen)
    {
        var byPreference = Enumerable.Range(0, stocks.Count)
            .Where(i => sheetsPerStock[i] < stocks[i].MaxSheets || stocks[i].MaxSheets == StockSize.UnlimitedSheets)
            .OrderBy(i => (long)stocks[i].WidthMm * stocks[i].HeightMm)
            .ThenBy(i => i)
            .ToList();

        foreach (int i in byPreference)
        {
            var stock = stocks[i];
            int usableW = Math.Max(0, stock.WidthMm - margins.LeftMm - margins.RightMm);
            int usableH = Math.Max(0, stock.HeightMm - margins.TopMm - margins.BottomMm);
            bool fits = Orientations(piece, rotation).Any(o => o.W <= usableW && o.H <= usableH);
            if (!fits) continue;

            var layout = new SheetLayout
            {
                SheetNo = alreadyOpen + 1,
                Stock = stock,
                UsableX = margins.LeftMm,
                UsableY = margins.TopMm,
                UsableWidth = usableW,
                UsableHeight = usableH
            };
            sheetsPerStock[i]++;
            return new SheetState(layout, i);
        }
        return null;
    }

    // ── Advanced.cs — sheet-fill, wall-seed, MaxRects ────────────────

    /// <summary>
    /// Fill one sheet at a time: keep placing the remaining piece with
    /// the best BSSF score until the sheet is full, then open the next.
    /// Matches industrial "complete this plate before starting another".
    /// </summary>
    internal static OptimizationResult RunSheetFill(
        IReadOnlyList<CutPiece> pieces, IReadOnlyList<StockSize> stocks,
        MarginProfile margins, RotationPolicy rotation, SplitMode split,
        int consolidationSweeps = int.MaxValue,
        CancellationToken cancellationToken = default,
        int candidateWindowSize = int.MaxValue)
    {
        var remaining = Sort(pieces, OrderBy.MaxSide);
        var sheets = new List<SheetState>();
        var per = new int[stocks.Count];
        var unplaced = new List<CutPiece>();

        while (remaining.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var opener = remaining[0];
            var state = OpenBlank(stocks, per, opener, margins, rotation, sheets.Count);
            if (state == null)
            {
                unplaced.AddRange(remaining);
                break;
            }
            sheets.Add(state);

            bool progressed = true;
            while (progressed)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progressed = false;
                int bestIdx = -1;
                PlacementCandidate bestCand = default;
                bool haveBest = false;
                int bestRank = int.MaxValue, bestFit = int.MaxValue;
                long bestSs = long.MaxValue, bestWaste = long.MaxValue;

                int scanLimit = candidateWindowSize >= remaining.Count ? remaining.Count : candidateWindowSize;
                for (int i = 0; i < scanLimit; i++)
                {
                    // Scores are compared by value: no PlacedPiece is
                    // allocated for a candidate that loses.
                    if (!TryPlaceCore(state, remaining[i], rotation, out var cand)) continue;
                    int rank = cand.OrderRank, fit = cand.FitRank;
                    long ss = cand.ShortSide, waste = cand.Waste;
                    if (rank < bestRank
                        || (rank == bestRank && fit < bestFit)
                        || (rank == bestRank && fit == bestFit && ss < bestSs)
                        || (rank == bestRank && fit == bestFit && ss == bestSs && waste < bestWaste)
                        || (rank == bestRank && fit == bestFit && ss == bestSs && waste == bestWaste && i < bestIdx))
                    {
                        bestIdx = i;
                        bestCand = cand;
                        haveBest = true;
                        bestRank = rank; bestFit = fit; bestSs = ss; bestWaste = waste;
                    }
                }

                if (!haveBest) break;
                Commit(state, bestCand.Materialize(remaining[bestIdx]), margins, split);
                remaining.RemoveAt(bestIdx);
                progressed = true;
            }
        }

        ConsolidateAll(sheets, margins, rotation, split, consolidationSweeps,
            cancellationToken: cancellationToken);
        return FinishFromSheets(sheets, unplaced, rotation, "SHEET-FILL");
    }

    /// <summary>
    /// Seed sheets with 3-across tall walls (~2130 mm), then fill
    /// leftovers and pack the rest with sheet-fill.
    /// </summary>
    internal static OptimizationResult RunWallSeed(
        IReadOnlyList<CutPiece> pieces, IReadOnlyList<StockSize> stocks,
        MarginProfile margins, RotationPolicy rotation, SplitMode split,
        int consolidationSweeps = int.MaxValue,
        CancellationToken cancellationToken = default,
        int candidateWindowSize = int.MaxValue)
    {
        var remaining = pieces.ToList();
        var walls = remaining
            .Where(p =>
            {
                int a = Math.Max(p.WidthMm, p.HeightMm);
                int b = Math.Min(p.WidthMm, p.HeightMm);
                return a >= 2050 && b >= 800 && b <= 1200;
            })
            .OrderByDescending(p => Math.Max(p.WidthMm, p.HeightMm))
            .ThenByDescending(p => Math.Min(p.WidthMm, p.HeightMm))
            .ToList();

        var sheets = new List<SheetState>();
        var per = new int[stocks.Count];
        var used = new HashSet<CutPiece>();

        for (int i = 0; i + 2 < walls.Count; i += 3)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var trio = new[] { walls[i], walls[i + 1], walls[i + 2] };
            var state = OpenBlank(stocks, per, trio[0], margins, rotation, sheets.Count);
            if (state == null) break;

            bool all = true;
            foreach (var p in trio)
            {
                var placed = TryPlace(state, p, rotation, out _, out _);
                if (placed == null) { all = false; break; }
                Commit(state, placed, margins, split);
            }
            if (!all)
            {
                per[state.StockIndex] = Math.Max(0, per[state.StockIndex] - 1);
                continue;
            }
            sheets.Add(state);
            foreach (var p in trio) used.Add(p);
        }

        remaining = remaining.Where(p => !used.Contains(p)).ToList();
        remaining = Sort(remaining, OrderBy.MaxSide);

        // Fill leftover pockets on wall sheets, then open new sheets.
        var leftover = new List<CutPiece>();
        foreach (var piece in remaining)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!PlaceOnOpenSheets(sheets, piece, margins, rotation, split))
                leftover.Add(piece);
        }

        foreach (var piece in leftover.ToList())
        {
            if (OpenNewSheet(sheets, per, stocks, piece, margins, rotation, split))
                leftover.Remove(piece);
        }

        ConsolidateAll(sheets, margins, rotation, split, consolidationSweeps,
            cancellationToken: cancellationToken);
        return FinishFromSheets(sheets, leftover, rotation, "WALL-SEED");
    }

    /// <summary>
    /// Non-guillotine MaxRects BSSF with kerf inflation. Kept as a
    /// candidate only — Verify still enforces kerf + margins, and the
    /// guillotine gate / cut derivation treat it exactly as the WPF
    /// does (E4.1).
    /// </summary>
    internal static OptimizationResult RunMaxRects(
        IReadOnlyList<CutPiece> ordered, IReadOnlyList<StockSize> stocks,
        MarginProfile margins, RotationPolicy rotation,
        CancellationToken cancellationToken = default)
    {
        int kerf = margins.KerfMm;
        var remaining = ordered.ToList();
        var sheets = new List<SheetState>();
        var per = new int[stocks.Count];
        var unplaced = new List<CutPiece>();

        foreach (var piece in remaining)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (MaxRectsPlace(sheets, piece, rotation, kerf)) continue;

            var state = OpenBlank(stocks, per, piece, margins, rotation, sheets.Count);
            if (state == null) { unplaced.Add(piece); continue; }
            sheets.Add(state);
            if (!MaxRectsPlace(sheets, piece, rotation, kerf))
                unplaced.Add(piece);
        }

        // MaxRects placements live on Layout.Pieces; consolidation uses
        // guillotine TryPlace so it is skipped here — just sort.
        return FinishFromSheets(sheets, unplaced, rotation, "MAXRECTS-BSSF");
    }

    private static bool MaxRectsPlace(
        List<SheetState> sheets, CutPiece piece, RotationPolicy rotation, int kerf)
    {
        (int si, PlacedPiece placed, long waste, int rank)? best = null;

        for (int si = 0; si < sheets.Count; si++)
        {
            var sheet = sheets[si];
            foreach (var (w, h, rot, rank) in Orientations(piece, rotation))
            {
                int occW = w + kerf;
                int occH = h + kerf;
                for (int fi = 0; fi < sheet.Free.Count; fi++)
                {
                    var r = sheet.Free[fi];
                    if (occW > r.W || occH > r.H) continue;
                    long waste = (long)r.W * r.H - (long)occW * occH;
                    if (best == null || rank < best.Value.rank
                        || (rank == best.Value.rank && waste < best.Value.waste))
                    {
                        best = (si, new PlacedPiece(piece, r.X, r.Y, w, h, rot), waste, rank);
                    }
                }
            }
        }

        if (best == null) return false;

        var target = sheets[best.Value.si];
        var p = best.Value.placed;
        int pw = p.WidthMm + kerf;
        int ph = p.HeightMm + kerf;
        var next = new List<FreeRect>();
        foreach (var r in target.Free)
        {
            if (p.X >= r.X + r.W || p.X + pw <= r.X || p.Y >= r.Y + r.H || p.Y + ph <= r.Y)
            {
                next.Add(r);
                continue;
            }
            // Frame decomposition (clipped to the rect) — the old 4-strips
            // code double-claimed corners, letting two later placements
            // occupy the same space.
            int ix = Math.Max(p.X, r.X);
            int iy = Math.Max(p.Y, r.Y);
            int ix2 = Math.Min(p.X + pw, r.X + r.W);
            int iy2 = Math.Min(p.Y + ph, r.Y + r.H);
            // These sub-rectangles are one cut deeper than the rectangle
            // they were carved out of, so the depth ledger stays
            // meaningful even on paths that do not go through
            // SplitRemainder.
            int d = r.Depth + 1;
            if (iy > r.Y) next.Add(new FreeRect(r.X, r.Y, r.W, iy - r.Y, d));
            if (iy2 < r.Y + r.H) next.Add(new FreeRect(r.X, iy2, r.W, r.Y + r.H - iy2, d));
            if (ix > r.X) next.Add(new FreeRect(r.X, iy, ix - r.X, iy2 - iy, d));
            if (ix2 < r.X + r.W) next.Add(new FreeRect(ix2, iy, r.X + r.W - ix2, iy2 - iy, d));
        }
        PruneFree(next);
        target.Free.Clear();
        target.Free.AddRange(next);
        target.Layout.Pieces.Add(p);
        // The fourth and final mutation site for a sheet's Free / Pieces
        // lists. Refreshing here keeps MaxFreeW / MaxFreeH / PlacedArea
        // exact, which the TryPlace prune and the fullest-first sort both
        // depend on.
        target.RefreshAll();
        return true;
    }

    private static void PruneFree(List<FreeRect> rects)
    {
        for (int i = rects.Count - 1; i >= 0; i--)
        {
            if (rects[i].W <= 0 || rects[i].H <= 0) { rects.RemoveAt(i); continue; }
            for (int j = 0; j < rects.Count; j++)
            {
                if (i == j) continue;
                if (rects[i].X >= rects[j].X && rects[i].Y >= rects[j].Y
                    && rects[i].X + rects[i].W <= rects[j].X + rects[j].W
                    && rects[i].Y + rects[i].H <= rects[j].Y + rects[j].H)
                {
                    rects.RemoveAt(i);
                    break;
                }
            }
        }
    }

    // ── BestSplit.cs — per-sheet split selection + the beam ──────────

    /// <summary>
    /// Fills ONE sheet under a given split rule, always taking the best
    /// remaining piece for that sheet (same scoring as
    /// PlaceOnOpenSheets). sheetsPerStock is consumed — callers pass a
    /// clone so speculative rolls can be discarded.
    /// </summary>
    private static (SheetState? Sheet, List<CutPiece> Remaining) FillOneSheet(
        List<CutPiece> remaining, IReadOnlyList<StockSize> stocks, int[] sheetsPerStock,
        MarginProfile margins, RotationPolicy rotation, SplitMode split,
        CancellationToken cancellationToken = default,
        int candidateWindowSize = int.MaxValue)
    {
        var state = OpenBlank(stocks, sheetsPerStock, remaining[0], margins, rotation, 0);
        if (state == null) return (null, remaining);

        var work = new List<CutPiece>(remaining);
        bool progressed = true;
        while (progressed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progressed = false;
            int bestIdx = -1;
            PlacementCandidate bestCand = default;
            bool haveBest = false;
            (int fit, long ss, long waste, int rank, int i) bestKey = default;

            int scanLimit = candidateWindowSize >= work.Count ? work.Count : candidateWindowSize;
            for (int i = 0; i < scanLimit; i++)
            {
                if (!TryPlaceCore(state, work[i], rotation, out var cand)) continue;

                var key = (cand.FitRank, cand.ShortSide, cand.Waste, cand.OrderRank, i);
                if (!haveBest || key.CompareTo(bestKey) < 0)
                {
                    bestKey = key;
                    bestIdx = i;
                    bestCand = cand;
                    haveBest = true;
                }
            }

            if (!haveBest) break;
            Commit(state, bestCand.Materialize(work[bestIdx]), margins, split);
            work.RemoveAt(bestIdx);
            progressed = true;
        }

        return (state, work);
    }

    private static long PlacedArea(SheetState sheet) =>
        sheet.Layout.Pieces.Sum(p => (long)p.WidthMm * p.HeightMm);

    /// <summary>
    /// Per-sheet split selection, greedy. Chooses the plate that packs
    /// the most glass; ties prefer more pieces, then the split rule's
    /// declaration order, so the run stays deterministic. The owner's
    /// benchmark: Clear 17 @ 79.92% → 16 @ 84.92%.
    /// </summary>
    internal static OptimizationResult RunBestSplit(
        IReadOnlyList<CutPiece> pieces, IReadOnlyList<StockSize> stocks,
        MarginProfile margins, RotationPolicy rotation,
        int consolidationSweeps = int.MaxValue,
        CancellationToken cancellationToken = default,
        int candidateWindowSize = int.MaxValue,
        bool remainderAware = false)
    {
        var remaining = Sort(pieces, OrderBy.MaxSide);
        var sheets = new List<SheetState>();
        var per = new int[stocks.Count];
        var unplaced = new List<CutPiece>();

        while (remaining.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SheetState? chosen = null;
            List<CutPiece>? chosenLeft = null;
            int[]? chosenPer = null;
            long bestArea = -1;
            int bestCount = -1;
            int bestSplit = int.MaxValue;
            long bestRemainder = -1;

            for (int si = 0; si < AllSplits.Length; si++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var trialPer = (int[])per.Clone();
                var (sheet, left) = FillOneSheet(remaining, stocks, trialPer,
                      margins, rotation, AllSplits[si], cancellationToken, candidateWindowSize);
                if (sheet == null) continue;

                long area = PlacedArea(sheet);
                int count = sheet.Layout.Pieces.Count;

                // E2.2 — REMAINDER-AWARE SPLIT CHOICE: two splits can put
                // the same glass on this plate and leave very different
                // space behind. The largest remaining rectangle is the
                // signal greedy area was missing. Compared as ONE
                // lexicographic key; with the switch off the remainder
                // term is 0 for every candidate, collapsing the key to
                // exactly (area, count, -index).
                long remainder = remainderAware ? LargestFreeRect(sheet) : 0;

                var key = (area, remainder, (long)count, (long)-si);
                var best = (bestArea, bestRemainder, (long)bestCount, (long)-bestSplit);

                if (chosen == null || key.CompareTo(best) > 0)
                {
                    chosen = sheet; chosenLeft = left; chosenPer = trialPer;
                    bestArea = area; bestCount = count; bestSplit = si;
                    bestRemainder = remainder;
                }
            }

            if (chosen == null)
            {
                unplaced.AddRange(remaining);
                break;
            }

            sheets.Add(chosen);
            per = chosenPer!;
            remaining = chosenLeft!;
        }

        ConsolidateAll(sheets, margins, rotation, SplitMode.HorizontalFirst,
              consolidationSweeps, cancellationToken: cancellationToken);
        return FinishFromSheets(sheets, unplaced, rotation, "BEST-SPLIT");
    }

    /// <summary>
    /// The largest rectangle this plate still has, by area. Used only by
    /// the remainder-aware split choice, and only as a TIE-BREAK below
    /// placed area.
    /// </summary>
    private static long LargestFreeRect(SheetState sheet)
    {
        long best = 0;
        if (sheet == null) return 0;
        for (int i = 0; i < sheet.Free.Count; i++)
        {
            long w = sheet.Free[i].W;
            long h = sheet.Free[i].H;
            if (w <= 0 || h <= 0) continue;
            long area = w * h;
            if (area > best) best = area;
        }
        return best;
    }

    /// <summary>
    /// Lower bound on the sheets the remaining pieces still need: glass
    /// area over the largest usable rectangle in the pool. Ranks beam
    /// states so a greedily-full plate that strands awkward pieces is
    /// penalised for what it leaves behind.
    /// </summary>
    private static double SheetsLowerBound(
        List<CutPiece> remaining, IReadOnlyList<StockSize> stocks, MarginProfile margins)
    {
        if (remaining.Count == 0) return 0;

        long bestUsable = 0;
        foreach (var st in stocks)
        {
            long uw = Math.Max(0, st.WidthMm - margins.LeftMm - margins.RightMm);
            long uh = Math.Max(0, st.HeightMm - margins.TopMm - margins.BottomMm);
            if (uw * uh > bestUsable) bestUsable = uw * uh;
        }
        if (bestUsable <= 0) return double.PositiveInfinity;

        long glass = 0;
        foreach (var p in remaining) glass += (long)p.WidthMm * p.HeightMm;
        return (double)glass / bestUsable;
    }

    private sealed class BeamState
    {
        public List<SheetState> Sheets = null!;
        public int[] Per = null!;
        public List<CutPiece> Remaining = null!;
        public double Rank;
    }

    /// <summary>
    /// Beam search over per-sheet split selection. Keeps the beam best
    /// partial jobs, ranked by sheets opened plus the remainder's lower
    /// bound, so the myopia of RunBestSplit is corrected without giving
    /// up its win on the benchmark (beam 1 already reaches 16 sheets;
    /// the beam exists to protect the general case).
    /// </summary>
    internal static OptimizationResult RunBestSplitBeam(
        IReadOnlyList<CutPiece> pieces, IReadOnlyList<StockSize> stocks,
        MarginProfile margins, RotationPolicy rotation, int beam,
        CancellationToken cancellationToken = default,
        OrderBy ordering = OrderBy.MaxSide, int maxExpansions = int.MaxValue,
        int consolidationSweeps = int.MaxValue, int[]? initiallyUsed = null)
    {
        if (beam < 1) beam = 1;
        var start = Sort(pieces, ordering);
        int expansions = 0;

        var states = new List<BeamState>
        {
            new BeamState { Sheets = new List<SheetState>(), Per = initiallyUsed == null ? new int[stocks.Count] : (int[])initiallyUsed.Clone(), Remaining = start }
        };
        List<SheetState>? winner = null;
        long winnerArea = -1;

        while (states.Count > 0 && expansions < maxExpansions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var next = new List<BeamState>();

            foreach (var state in states)
            {
                if (state.Remaining.Count == 0)
                {
                    long area = state.Sheets.Sum(PlacedArea);
                    if (winner == null
                        || state.Sheets.Count < winner.Count
                        || (state.Sheets.Count == winner.Count && area > winnerArea))
                    {
                        winner = state.Sheets;
                        winnerArea = area;
                    }
                    continue;
                }

                if (expansions++ >= maxExpansions) break;
                foreach (var split in AllSplits)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var trialPer = (int[])state.Per.Clone();
                    var (sheet, left) = FillOneSheet(state.Remaining, stocks, trialPer,
                          margins, rotation, split, cancellationToken);
                    if (sheet == null) continue;

                    var sheets = new List<SheetState>(state.Sheets) { sheet };
                    next.Add(new BeamState
                    {
                        Sheets = sheets,
                        Per = trialPer,
                        Remaining = left,
                        Rank = sheets.Count + SheetsLowerBound(left, stocks, margins)
                    });
                }
            }

            if (next.Count == 0) break;

            var deduped = new List<BeamState>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in next
                .OrderBy(s => s.Rank)
                .ThenBy(s => s.Sheets.Count)
                .ThenByDescending(s => s.Sheets.Sum(PlacedArea)))
            {
                // Only merge equivalent rack consumption AND remaining
                // size/rotation multisets. Equal sheet counts alone hide
                // different finite-stock choices and locked-pane futures.
                string sig = string.Join(",", s.Per) + "|" + string.Join(",",
                    s.Remaining.OrderBy(p => p.WidthMm).ThenBy(p => p.HeightMm).ThenBy(p => p.AllowRotate)
                               .Select(p => p.WidthMm.ToString() + "x" + p.HeightMm.ToString() + ":" + p.AllowRotate));
                if (!seen.Add(sig)) continue;
                deduped.Add(s);
                if (deduped.Count >= beam) break;
            }

            states = deduped;
        }

        if (winner == null)
            return FinishFromSheets(new List<SheetState>(), start, rotation, "BEST-SPLIT-BEAM");

        ConsolidateAll(winner, margins, rotation, SplitMode.HorizontalFirst,
            consolidationSweeps, cancellationToken: cancellationToken);
        return FinishFromSheets(winner, new List<CutPiece>(), rotation, "BEST-SPLIT-BEAM");
    }

    // ── CapacityStrips.cs — the capacity-aware strip candidate ───────

    // A fixed search window, not an input/quantity limit. Largest anchors
    // and smallest fillers are both visible even on a million-piece order.
    private const int StripCandidateWindow = 128;

    /// <summary>
    /// Capacity-aware strip candidate: columns (or transposed rows), with
    /// stacked panes inside each strip. Seed orientation maximizes repeat
    /// capacity, rather than the short-side remainder of ONE piece.
    /// Narrow pieces fill residual strips. No dimension, sheet count, job
    /// name or composition-specific pattern is encoded here.
    /// </summary>
    internal static OptimizationResult RunCapacityStrips(
        IReadOnlyList<CutPiece> pieces, IReadOnlyList<StockSize> stocks,
        MarginProfile margins, RotationPolicy rotation, bool transpose,
        CancellationToken cancellationToken)
    {
        var result = new OptimizationResult
        {
            Rotation = rotation,
            Strategy = transpose ? "CAPACITY STRIPS (rows)" : "CAPACITY STRIPS (columns)"
        };
        cancellationToken.ThrowIfCancellationRequested();
        var ordered = Sort(pieces, OrderBy.Area);
        var pool = new StripPool(ordered.Count);
        var frontier = new List<int>(StripCandidateWindow);
        var usedSheets = new int[stocks.Count];
        var stockOrder = Enumerable.Range(0, stocks.Count)
            .OrderBy(i => (long)stocks[i].WidthMm * stocks[i].HeightMm).ThenBy(i => i).ToArray();
        // A separated offcut needs two cuts, not just an empty gap. When
        // breakout is disabled, ordinary kerf-only separation is enough.
        long gap = margins.MinBreakoutMm > 0
            ? (long)margins.MinBreakoutMm + 2L * margins.KerfMm : margins.KerfMm;

        while (pool.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int anchor = pool.Head;
            int stockIndex = -1;
            StripChoice opening = default;
            long usableU = 0, usableV = 0;
            foreach (int si in stockOrder)
            {
                var stock = stocks[si];
                if (stock.MaxSheets < 0 || (stock.MaxSheets > 0 && usedSheets[si] >= stock.MaxSheets)) continue;
                long w = (long)stock.WidthMm - margins.LeftMm - margins.RightMm;
                long h = (long)stock.HeightMm - margins.TopMm - margins.BottomMm;
                long u = transpose ? h : w, v = transpose ? w : h;
                if (!TryStripOrientation(ordered[anchor], u, v, rotation, transpose, gap,
                    margins, capacitySeed: true, out opening)) continue;
                stockIndex = si; usableU = u; usableV = v; break;
            }
            if (stockIndex < 0)
            {
                result.Unplaced.Add(ordered[anchor]);
                pool.Remove(anchor);
                continue;
            }

            usedSheets[stockIndex]++;
            var sheet = new SheetLayout
            {
                SheetNo = result.Sheets.Count + 1, Stock = stocks[stockIndex],
                UsableX = margins.LeftMm, UsableY = margins.TopMm,
                UsableWidth = (int)(transpose ? usableV : usableU),
                UsableHeight = (int)(transpose ? usableU : usableV)
            };
            long uOffset = 0;
            int seed = anchor;
            var seedChoice = opening;
            while (seed >= 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int stripWidth = WidenStrip(pool, frontier, ordered, seedChoice,
                    usableU - uOffset, usableV, rotation, transpose, gap, margins);
                long vOffset = 0;
                int selected = seed;
                var choice = seedChoice;
                while (selected >= 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    sheet.Pieces.Add(new PlacedPiece(
                        ordered[selected],
                        checked(margins.LeftMm + (int)(transpose ? vOffset : uOffset)),
                        checked(margins.TopMm + (int)(transpose ? uOffset : vOffset)),
                        transpose ? choice.V : choice.U,
                        transpose ? choice.U : choice.V,
                        choice.Rotated));
                    // PHASE 0: this path writes PlacedPiece directly, so it
                    // records its own cuts — see RecordStripPieceCuts.
                    RecordStripPieceCuts(sheet, transpose, uOffset, stripWidth, vOffset, choice);
                    pool.Remove(selected);
                    vOffset += choice.V + gap;
                    selected = FindStripPiece(pool, frontier, ordered, stripWidth, usableV - vOffset,
                        rotation, transpose, gap, margins, capacitySeed: false, out choice, flushWidth: uOffset + stripWidth == usableU);
                }
                // PHASE 0: the single cut that separates this strip from the
                // rest of the sheet, emitted after the strip's own cuts — a
                // legal execution order.
                if (uOffset + stripWidth < usableU)
                    RecordStripSeparator(sheet, transpose, uOffset + stripWidth);
                uOffset += stripWidth + gap;
                seed = FindStripPiece(pool, frontier, ordered, usableU - uOffset, usableV,
                    rotation, transpose, gap, margins, capacitySeed: true, out seedChoice);
            }
            result.Sheets.Add(sheet);
        }
        // Do not consolidate this candidate with the old free-rectangle
        // mover: it can reintroduce the narrow offcuts this construction
        // avoids.
        result.Sheets.Sort((a, b) => b.UsedSqm.CompareTo(a.UsedSqm));
        for (int i = 0; i < result.Sheets.Count; i++) result.Sheets[i].SheetNo = i + 1;
        return result;
    }

    /// <summary>
    /// PHASE 0: the strip builders write PlacedPiece directly instead of
    /// going through Commit, so they have to record their own cuts —
    /// otherwise a QUICK plan that wins on capacity strips would produce
    /// sheets with NO cut sequence at all. The order is the physical one:
    /// first the cut that separates this piece from whatever follows it
    /// inside the strip, then — when the piece is narrower than its
    /// strip — the cut that frees the sliver beside it.
    /// </summary>
    private static void RecordStripPieceCuts(
        SheetLayout sheet, bool transpose, long uOffset, int stripWidth, long vOffset,
        StripChoice choice)
    {
        int px = sheet.UsableX + (int)(transpose ? vOffset : uOffset);
        int py = sheet.UsableY + (int)(transpose ? uOffset : vOffset);
        int pw = transpose ? choice.V : choice.U;
        int ph = transpose ? choice.U : choice.V;

        int sx = sheet.UsableX + (int)(transpose ? 0 : uOffset);
        int sy = sheet.UsableY + (int)(transpose ? uOffset : 0);
        int sw = transpose ? sheet.UsableWidth : stripWidth;
        int sh = transpose ? stripWidth : sheet.UsableHeight;

        if (!transpose)
        {
            // Columns: pieces stack downwards, the sliver sits to the right.
            if (py + ph < sy + sh)
                AddStripCut(sheet, CutAxis.Horizontal, py + ph, sx, py, sw, sy + sh - py, 2);
            if (px + pw < sx + sw)
                AddStripCut(sheet, CutAxis.Vertical, px + pw, px, py, sx + sw - px, ph, 3);
        }
        else
        {
            // Rows: pieces run rightwards, the sliver sits underneath.
            if (px + pw < sx + sw)
                AddStripCut(sheet, CutAxis.Vertical, px + pw, px, py, sx + sw - px, sh, 2);
            if (py + ph < sy + sh)
                AddStripCut(sheet, CutAxis.Horizontal, py + ph, px, py, pw, sy + sh - py, 3);
        }
    }

    /// <summary>
    /// The single full-length cut that separates one strip from the sheet
    /// (stage 1). Drawn across the whole usable rectangle, which is what
    /// a guillotine blade does.
    /// </summary>
    private static void RecordStripSeparator(SheetLayout sheet, bool transpose, long boundary)
    {
        int at = (transpose ? sheet.UsableY : sheet.UsableX) + (int)boundary;
        AddStripCut(sheet, transpose ? CutAxis.Horizontal : CutAxis.Vertical, at,
            sheet.UsableX, sheet.UsableY, sheet.UsableWidth, sheet.UsableHeight, 1);
    }

    private static void AddStripCut(SheetLayout sheet, CutAxis axis, int atMm,
        int x, int y, int w, int h, int stage)
    {
        sheet.Cuts.Add(new CutStep(
            sheet.Cuts.Count + 1, axis, atMm, x, y, w, h, stage, IsTrim: false));
    }

    private static int WidenStrip(StripPool pool, List<int> frontier, List<CutPiece> pieces,
        StripChoice seed, long availableU, long availableV, RotationPolicy rotation,
        bool transpose, long gap, MarginProfile margins)
    {
        // Near-size pieces belong to the same repetition-capacity family,
        // not necessarily the same exact dimension. Widening within that
        // family preserves both the across/down counts and avoids a narrow
        // first filler preventing its slightly wider peers from rotating.
        long across = (availableU + gap) / (seed.U + gap);
        long down = (availableV + gap) / (seed.V + gap);
        int width = seed.U;
        pool.Frontier(frontier);
        foreach (int index in frontier)
        {
            var p = pieces[index];
            int turns = rotation == RotationPolicy.Free90 && p.AllowRotate ? 2 : 1;
            for (int turn = 0; turn < turns; turn++)
            {
                int w = turn == 0 ? p.WidthMm : p.HeightMm;
                int h = turn == 0 ? p.HeightMm : p.WidthMm;
                int u = transpose ? h : w, v = transpose ? w : h;
                if (u <= width || v <= 0 || u > availableU || v > availableV) continue;
                if ((availableU + gap) / (u + gap) != across
                    || (availableV + gap) / (v + gap) != down) continue;
                if (StripEdgeFits(availableU - u, margins)) width = u;
            }
        }
        return width;
    }

    private struct StripChoice
    {
        public int U, V;
        public bool Rotated;
    }

    private static bool TryStripOrientation(CutPiece piece, long availableU, long availableV,
        RotationPolicy rotation, bool transpose, long gap, MarginProfile margins,
        bool capacitySeed, out StripChoice choice, bool flushWidth = false)
    {
        choice = default;
        if (piece.WidthMm <= 0 || piece.HeightMm <= 0) return false;
        long bestCapacity = -1, bestDown = -1;
        int bestWidth = -1;
        int orientations = rotation == RotationPolicy.Free90 && piece.AllowRotate
            && piece.WidthMm != piece.HeightMm ? 2 : 1;
        for (int turn = 0; turn < orientations; turn++)
        {
            int w = turn == 0 ? piece.WidthMm : piece.HeightMm;
            int h = turn == 0 ? piece.HeightMm : piece.WidthMm;
            int u = transpose ? h : w, v = transpose ? w : h;
            if (u > availableU || v > availableV) continue;
            // Interior strip widths can vary: the reserved separator gives
            // even their largest member a cuttable offcut. Only the outer
            // boundary of the sheet needs the width-edge test here.
            if ((capacitySeed || flushWidth) && !StripEdgeFits(availableU - u, margins)) continue;
            if (!StripEdgeFits(availableV - v, margins)) continue;
            long down = (availableV + gap) / (v + gap);
            long capacity = ((availableU + gap) / (u + gap)) * down;
            bool better = bestCapacity < 0 || (capacitySeed
                ? capacity > bestCapacity || (capacity == bestCapacity && down > bestDown)
                : u > bestWidth || (u == bestWidth && down > bestDown));
            if (!better) continue;
            bestCapacity = capacity; bestDown = down; bestWidth = u;
            choice = new StripChoice { U = u, V = v, Rotated = turn != 0 };
        }
        return bestCapacity >= 0;
    }

    private static bool StripEdgeFits(long remainder, MarginProfile margins) => remainder == 0
        || remainder >= (long)margins.KerfMm + margins.MinBreakoutMm;

    private static int FindStripPiece(StripPool pool, List<int> frontier, List<CutPiece> pieces,
        long availableU, long availableV, RotationPolicy rotation, bool transpose, long gap,
        MarginProfile margins, bool capacitySeed, out StripChoice choice, bool flushWidth = false)
    {
        choice = default;
        if (pool.Count == 0 || availableU <= 0 || availableV <= 0) return -1;
        pool.Frontier(frontier);
        int best = -1;
        long bestArea = -1;
        foreach (int index in frontier)
        {
            var piece = pieces[index];
            long area = (long)piece.WidthMm * piece.HeightMm;
            if (area <= bestArea || !TryStripOrientation(piece, availableU, availableV, rotation,
                transpose, gap, margins, capacitySeed, out var candidate, flushWidth)) continue;
            best = index; bestArea = area; choice = candidate;
        }
        return best;
    }

    /// <summary>
    /// Intrusive ordered frontier: O(1) removal, O(K) queries, O(N)
    /// memory. Unlike repeatedly scanning a marked-used array, it never
    /// walks over all earlier sheets to find the next live piece. No
    /// pieces are dropped when the candidate window is full; every
    /// remaining head is eventually placed or explicitly returned as
    /// unplaced.
    /// </summary>
    private sealed class StripPool
    {
        private readonly int[] _next, _previous;
        public int Head { get; private set; }
        private int _tail;
        public int Count { get; private set; }
        public StripPool(int count)
        {
            Count = count; Head = count == 0 ? -1 : 0; _tail = count - 1;
            _next = new int[count]; _previous = new int[count];
            for (int i = 0; i < count; i++)
            { _next[i] = i + 1 == count ? -1 : i + 1; _previous[i] = i - 1; }
        }
        public void Remove(int index)
        {
            int before = _previous[index], after = _next[index];
            if (before < 0) Head = after; else _next[before] = after;
            if (after < 0) _tail = before; else _previous[after] = before;
            Count--;
        }
        public void Frontier(List<int> output)
        {
            output.Clear();
            int front = Head, back = _tail;
            int take = Math.Min(StripCandidateWindow, Count);
            for (int n = 0; n < take; n++)
            {
                if ((n & 1) == 0) { output.Add(front); front = _next[front]; }
                else { output.Add(back); back = _previous[back]; }
            }
        }
    }

    // ── PatternSearch.cs — the bounded best-split pattern beam ───────

    // Limits search work, NEVER input quantity. Large orders keep all panes
    // and apply the same search to a small set of their weakest sheets.
    private const int PatternPartWindow = 256;
    private const int PatternTailSheets = 6;

    private static IEnumerable<(int Width, OrderBy Order)> PatternCandidates(int firstLevel, int lastLevel)
    {
        if (firstLevel <= 1 && lastLevel >= 1)
        {
            yield return (4, OrderBy.MaxSide);
            yield return (8, OrderBy.MaxSide);
        }
        if (firstLevel <= 2 && lastLevel >= 2) yield return (12, OrderBy.Area);
        if (firstLevel <= 3 && lastLevel >= 3) yield return (16, OrderBy.MinSide);
        if (firstLevel <= 4 && lastLevel >= 4) yield return (24, OrderBy.Height);
        if (firstLevel <= 5 && lastLevel >= 5) yield return (32, OrderBy.Width);
    }

    private static OptimizationResult ImprovePatterns(OptimizationResult baseline,
        IReadOnlyList<CutPiece> pieces, IReadOnlyList<StockSize> stocks,
        MarginProfile margins, RotationPolicy rotation, int firstLevel, int lastLevel,
        CancellationToken token, Func<bool>? budgetExhausted = null)
    {
        if (pieces.Count == 0) return baseline;
        token.ThrowIfCancellationRequested();
        if (budgetExhausted?.Invoke() == true) return baseline;
        bool local = pieces.Count > PatternPartWindow;
        var removed = new HashSet<SheetLayout>();
        var selectedUnplaced = new HashSet<CutPiece>();
        var work = new List<CutPiece>();
        var used = new int[stocks.Count];
        if (!local) work.AddRange(pieces);
        else
        {
            // Reserve some recovery space, without allowing a large shortage
            // list to crowd every existing sheet out of the repair window.
            foreach (var p in baseline.Unplaced.Take(PatternPartWindow / 4))
            { work.Add(p); selectedUnplaced.Add(p); }
            foreach (var sheet in baseline.Sheets.OrderBy(s => s.UtilizationPct).ThenBy(s => s.SheetNo))
            {
                token.ThrowIfCancellationRequested();
                if (removed.Count >= PatternTailSheets) break;
                if (sheet.Pieces.Count + work.Count > PatternPartWindow) continue;
                removed.Add(sheet);
                work.AddRange(sheet.Pieces.Select(p => p.Piece));
            }
            if (work.Count == 0) return baseline;
            // Repacking may consume only inventory not committed outside
            // the window. Keep original StockSize references in the result.
            foreach (var sheet in baseline.Sheets.Where(s => !removed.Contains(s)))
            {
                token.ThrowIfCancellationRequested();
                for (int i = 0; i < stocks.Count; i++)
                    if (ReferenceEquals(stocks[i], sheet.Stock)) { used[i]++; break; }
            }
        }

        var best = baseline;
        foreach (var (width, order) in PatternCandidates(firstLevel, lastLevel))
        {
            token.ThrowIfCancellationRequested();
            if (budgetExhausted?.Invoke() == true) break;
            // At most window * beam state expansions, each considering the
            // three existing split directions. No whole-million-part beam.
            var candidate = RunBestSplitBeam(work, stocks, margins, rotation, width, token,
                ordering: order, maxExpansions: PatternPartWindow * width,
                consolidationSweeps: 3, initiallyUsed: used);
            candidate.Strategy = $"PATTERN BEAM ({order}, width {width}" + (local ? ", tail)" : ")");
            if (local)
            {
                var combined = new OptimizationResult { Rotation = rotation, Strategy = candidate.Strategy };
                // Clone sheet containers so renumbering cannot mutate the
                // retained floor. PlacedPiece objects are never edited here.
                foreach (var sheet in baseline.Sheets.Where(s => !removed.Contains(s)).Concat(candidate.Sheets))
                {
                    token.ThrowIfCancellationRequested();
                    var copy = new SheetLayout { SheetNo = combined.Sheets.Count + 1, Stock = sheet.Stock,
                        UsableX = sheet.UsableX, UsableY = sheet.UsableY,
                        UsableWidth = sheet.UsableWidth, UsableHeight = sheet.UsableHeight };
                    copy.Pieces.AddRange(sheet.Pieces);
                    combined.Sheets.Add(copy);
                }
                combined.Unplaced.AddRange(baseline.Unplaced.Where(p => !selectedUnplaced.Contains(p)));
                combined.Unplaced.AddRange(candidate.Unplaced);
                candidate = combined;
            }
            best = Better(best, CheckCandidate(candidate, pieces, margins, token))!;
        }
        return best;
    }

    // ── SmartPlacement.cs — SMART PLACEMENT ──────────────────────────

    /// <summary>Written to OptimizationResult.Strategy so a multi-start
    /// run can say which of its candidates actually won.</summary>
    public const string SmartStrategyLabel = "SMART PLACEMENT";

    /// <summary>
    /// SMART PLACEMENT — capacity-preserving, breakout-aware,
    /// sheet-at-a-time. Fills one sheet completely before opening the
    /// next (industrial practice: "finish this plate before you start
    /// another"), choosing each placement by what it leaves the sheet
    /// capable of rather than by how tightly the piece fits the
    /// rectangle it consumes.
    /// </summary>
    /// <param name="split">A PREFERENCE, used only to break ties. Both
    /// split directions are evaluated at every placement regardless.</param>
    public static OptimizationResult PackSmart(
        IReadOnlyList<CutPiece> pieces,
        IReadOnlyList<StockSize> stocks,
        MarginProfile margins,
        RotationPolicy rotation,
        SplitMode split = SplitMode.HorizontalFirst,
        int consolidationSweeps = int.MaxValue,
        CancellationToken cancellationToken = default,
        IProgress<OptimizeProgress>? progress = null,
        int candidateWindowSize = int.MaxValue,
        CutPolicy? policy = null)
    {
        if (pieces == null) throw new ArgumentNullException(nameof(pieces));
        if (stocks == null || stocks.Count == 0)
            throw new ArgumentException("At least one stock size is required.", nameof(stocks));
        if (margins == null) throw new ArgumentNullException(nameof(margins));

        if (pieces.Count == 0)
            return new OptimizationResult { Rotation = rotation, Strategy = SmartStrategyLabel };

        int breakout = margins.MinBreakoutMm;
        var remaining = Sort(pieces, OrderBy.Area);
        var sheets = new List<SheetState>();
        var per = new int[stocks.Count];
        var unplaced = new List<CutPiece>();

        // Reused for the whole run: scoring evaluates four splits per
        // (piece, rectangle, orientation), and allocating two lists each
        // time would be the same O(parts x sheets) garbage the legacy
        // scorer was refactored away from.
        var kept = new List<FreeRect>(4);
        var unbounded = new List<FreeRect>(4);

        int totalPieces = remaining.Count;

        // Priority candidate window: at large scales, scanning the top K
        // candidate parts preserves placement quality while reducing
        // complexity from O(N^2) to O(K*N).
        int scanLimit = candidateWindowSize > 0 && candidateWindowSize < int.MaxValue
            ? candidateWindowSize
            : (totalPieces > 2000 ? 400 : int.MaxValue);

        while (remaining.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var state = OpenBlank(stocks, per, remaining[0], margins, rotation, sheets.Count);
            if (state == null && remaining.Count > 1)
            {
                // The largest remaining piece can be bigger than any stock
                // still within its cap while smaller ones fit fine. Giving up
                // here would report the whole rest of the job as unplaced.
                // Probing uses a COPY of the per-stock counters, because
                // OpenBlank consumes quota when it succeeds and a probe must
                // not spend stock it is not going to use.
                int probeCount = Math.Min(remaining.Count, scanLimit);
                for (int i = 1; i < probeCount && state == null; i++)
                {
                    var probePer = (int[])per.Clone();
                    if (OpenBlank(stocks, probePer, remaining[i], margins, rotation, sheets.Count) == null)
                        continue;
                    state = OpenBlank(stocks, per, remaining[i], margins, rotation, sheets.Count);
                }
            }

            if (state == null)
            {
                unplaced.AddRange(remaining);
                break;
            }

            sheets.Add(state);
            int placedOnThisSheet = 0;

            bool progressed = true;
            while (progressed)
            {
                progressed = false;

                int bestIdx = -1;
                SmartOption bestOption = default;

                int searchLimit = Math.Min(remaining.Count, scanLimit);
                for (int i = 0; i < searchLimit; i++)
                {
                    // Per part, so Cancel lands inside one placement
                    // decision rather than after a whole sheet.
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!TrySmartPlace(state, remaining[i], margins, rotation, split,
                            kept, unbounded, out SmartOption option))
                        continue;

                    if (bestIdx < 0 || Compare(option, bestOption, i, bestIdx) < 0)
                    {
                        bestIdx = i;
                        bestOption = option;
                    }
                }

                if (bestIdx < 0) break;

                Commit(state, bestOption.Materialize(remaining[bestIdx]), margins,
                    bestOption.Split, breakout);
                remaining.RemoveAt(bestIdx);
                placedOnThisSheet++;
                progressed = true;
            }

            // Insurance against a pathological loop: a sheet that opened but
            // accepted nothing would otherwise be re-opened forever.
            if (placedOnThisSheet == 0)
            {
                sheets.RemoveAt(sheets.Count - 1);
                per[state.StockIndex] = Math.Max(0, per[state.StockIndex] - 1);
                unplaced.AddRange(remaining);
                break;
            }

            progress?.Report(OptimizeProgress.Of("smart",
                totalPieces - remaining.Count, totalPieces,
                string.Format(CultureInfo.InvariantCulture,
                    "Smart placement: {0} sheet(s) filled, {1} part(s) left",
                    sheets.Count, remaining.Count)));
        }

        // Consolidation still squeezes out the emptiest sheet — that is
        // what moves the sheet count — but it commits through the same
        // breakout rule, so it cannot reintroduce a sliver the placement
        // phase refused.
        int sweeps = totalPieces > 20000 ? Math.Min(consolidationSweeps, 1) : consolidationSweeps;
        ConsolidateAll(sheets, margins, rotation, split, sweeps, breakout,
            cancellationToken);

        var result = FinishFromSheets(sheets, unplaced, rotation);
        result.Strategy = SmartStrategyLabel;
        return result;
    }

    /// <summary>
    /// One placement considered by PackSmart, scored by value so the
    /// winner is the only thing that allocates.
    /// </summary>
    internal struct SmartOption
    {
        public int X, Y, W, H;
        public bool Rotated;
        public SplitMode Split;
        /// <summary>Stock area this placement turns into uncuttable trim.</summary>
        public long LostToTrim;
        /// <summary>Area of the largest free rectangle that survives it.</summary>
        public long Capacity;
        /// <summary>Shortest side of that surviving rectangle.</summary>
        public long CapacitySide;
        /// <summary>Legacy geometry rank: 0 exact both edges, 1 one edge, 2 none.</summary>
        public int FitRank;
        /// <summary>Legacy best-short-side-fit value.</summary>
        public long ShortSide;
        /// <summary>0 when this split direction is the caller's preference.</summary>
        public int SplitPreference;

        public PlacedPiece Materialize(CutPiece piece) =>
            new(piece, X, Y, W, H, Rotated);
    }

    /// <summary>
    /// The total order over two placement options. Deliberately ends in
    /// piece index so that a full tie is resolved by input order: the
    /// same job always produces the same plan.
    /// </summary>
    private static int Compare(SmartOption a, SmartOption b, int indexA, int indexB)
    {
        int cmp = a.LostToTrim.CompareTo(b.LostToTrim);
        if (cmp == 0) cmp = b.Capacity.CompareTo(a.Capacity);           // larger is better
        if (cmp == 0) cmp = b.CapacitySide.CompareTo(a.CapacitySide);   // larger is better
        if (cmp == 0) cmp = a.FitRank.CompareTo(b.FitRank);
        if (cmp == 0) cmp = a.ShortSide.CompareTo(b.ShortSide);
        if (cmp == 0) cmp = a.SplitPreference.CompareTo(b.SplitPreference);
        if (cmp == 0) cmp = indexA.CompareTo(indexB);
        return cmp;
    }

    /// <summary>
    /// Best placement for one piece on one sheet under the smart policy:
    /// every free rectangle × every allowed orientation × BOTH split
    /// directions, scored by what survives. Returns false when the sheet
    /// cannot take the piece at all.
    /// </summary>
    private static bool TrySmartPlace(
        SheetState sheet, CutPiece piece, MarginProfile margins, RotationPolicy rotation,
        SplitMode preferredSplit, List<FreeRect> kept, List<FreeRect> unbounded,
        out SmartOption best)
    {
        best = default;

        int ow = piece.WidthMm, oh = piece.HeightMm;
        bool canRotate = rotation == RotationPolicy.Free90 && piece.AllowRotate && ow != oh;
        int orientationCount = canRotate ? 2 : 1;
        int kerf = margins.KerfMm;
        int breakout = margins.MinBreakoutMm;

        // Same sound whole-sheet prune the legacy scorer uses: MaxFreeW and
        // MaxFreeH are exact maxima over the free rectangles, so an
        // orientation exceeding them exceeds every rectangle.
        bool anyFits = false;
        for (int oi = 0; oi < orientationCount; oi++)
        {
            int wProbe = oi == 0 ? ow : oh;
            int hProbe = oi == 0 ? oh : ow;
            if (wProbe <= sheet.MaxFreeW && hProbe <= sheet.MaxFreeH) { anyFits = true; break; }
        }
        if (!anyFits) return false;

        bool found = false;
        int bestIndex = 0;   // rectangle index, for the deterministic tie-break

        for (int oi = 0; oi < orientationCount; oi++)
        {
            int w = oi == 0 ? ow : oh;
            int h = oi == 0 ? oh : ow;
            if (w > sheet.MaxFreeW || h > sheet.MaxFreeH) continue;

            for (int ri = 0; ri < sheet.Free.Count; ri++)
            {
                FreeRect rect = sheet.Free[ri];
                if (w > rect.W || h > rect.H) continue;

                bool exactW = w == rect.W;
                bool exactH = h == rect.H;
                int fit = (exactW && exactH) ? 0 : (exactW || exactH) ? 1 : 2;
                long shortSide = Math.Min(rect.W - w, rect.H - h);

                for (int sd = 0; sd < 2; sd++)
                {
                    SplitMode mode = sd == 0
                        ? SplitMode.HorizontalFirst
                        : SplitMode.VerticalFirst;

                    // The same stage-budget guard TryPlaceCore applies
                    // (null policy = Free — always passes until the staged
                    // slice wires SheetState.Policy).
                    if (!StagedPlacementAllowed(rect, w, h, mode, null)) continue;

                    // What survives when the floor's minimum breakout is
                    // honoured, and what would have survived if it were not.
                    // The difference is the area this placement condemns to
                    // trim waste — the first thing the score minimises.
                    kept.Clear();
                    SplitRemainder(rect, w, h, kerf, breakout, mode, kept);
                    unbounded.Clear();
                    SplitRemainder(rect, w, h, kerf, 0, mode, unbounded);

                    long keptArea = 0, capacity = 0, capacitySide = 0;
                    for (int k = 0; k < kept.Count; k++)
                    {
                        long area = (long)kept[k].W * kept[k].H;
                        keptArea += area;
                        if (area > capacity)
                        {
                            capacity = area;
                            capacitySide = Math.Min(kept[k].W, kept[k].H);
                        }
                    }

                    long unboundedArea = 0;
                    for (int u = 0; u < unbounded.Count; u++)
                        unboundedArea += (long)unbounded[u].W * unbounded[u].H;

                    var option = new SmartOption
                    {
                        X = rect.X,
                        Y = rect.Y,
                        W = w,
                        H = h,
                        Rotated = oi == 1,
                        Split = mode,
                        LostToTrim = unboundedArea - keptArea,
                        Capacity = capacity,
                        CapacitySide = capacitySide,
                        FitRank = fit,
                        ShortSide = shortSide,
                        SplitPreference = mode == preferredSplit ? 0 : 1
                    };

                    // Rectangle index stands in for the legacy scorer's
                    // positional tie-break; orientation is already ordered
                    // by the loop, and Compare() ends on the piece index.
                    if (!found || Compare(option, best, ri, bestIndex) < 0)
                    {
                        found = true;
                        best = option;
                        bestIndex = ri;
                    }
                }
            }
        }

        return found;
    }
}
