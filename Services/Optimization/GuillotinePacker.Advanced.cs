// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Optimization/GuillotinePacker.Advanced.cs
// Extra search strategies for PackOptimize: sheet-at-a-time fill,
// tall-wall seeding (LiSEC 3-across), MaxRects BSSF. Best run still
// wins by unplaced → sheets → utilization; Verify is unchanged.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.Optimization
{
    public static partial class GuillotinePacker
    {
        private static OptimizationResult FinishFromSheets(
            List<SheetState> sheets, List<CutPiece> unplaced, RotationPolicy rotation)
        {
            var result = new OptimizationResult { Rotation = rotation };
            foreach (var p in unplaced) result.Unplaced.Add(p);
            SortSheetsFullestFirst(sheets);
            for (int i = 0; i < sheets.Count; i++)
            {
                sheets[i].Layout.SheetNo = i + 1;
                result.Sheets.Add(sheets[i].Layout);
            }
            return result;
        }

        private static SheetState OpenBlank(
            IReadOnlyList<StockSize> stocks, int[] sheetsPerStock,
            CutPiece piece, MarginProfile margins, RotationPolicy rotation,
            int alreadyOpen)
        {
            var byPreference = Enumerable.Range(0, stocks.Count)
                .Where(i => sheetsPerStock[i] < stocks[i].MaxSheets || stocks[i].MaxSheets == 0)
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

        /// <summary>
        /// Fill one sheet at a time: keep placing the remaining piece with
        /// the best BSSF score until the sheet is full, then open the next.
        /// Matches industrial "complete this plate before starting another".
        /// </summary>
        private static OptimizationResult RunSheetFill(
            IReadOnlyList<CutPiece> pieces, IReadOnlyList<StockSize> stocks,
            MarginProfile margins, RotationPolicy rotation, SplitMode split,
            int consolidationSweeps = int.MaxValue)
        {
            var remaining = Sort(pieces, OrderBy.MaxSide);
            var sheets = new List<SheetState>();
            var per = new int[stocks.Count];
            var unplaced = new List<CutPiece>();

            while (remaining.Count > 0)
            {
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
                    progressed = false;
                    int bestIdx = -1;
                    PlacedPiece bestPlaced = null;
                    int bestRank = int.MaxValue, bestFit = int.MaxValue;
                    long bestSs = long.MaxValue, bestWaste = long.MaxValue;

                    for (int i = 0; i < remaining.Count; i++)
                    {
                        var cand = TryPlace(state, remaining[i], rotation,
                            out int rank, out int fit, out long ss, out long waste);
                        if (cand == null) continue;
                        if (rank < bestRank
                            || (rank == bestRank && fit < bestFit)
                            || (rank == bestRank && fit == bestFit && ss < bestSs)
                            || (rank == bestRank && fit == bestFit && ss == bestSs && waste < bestWaste)
                            || (rank == bestRank && fit == bestFit && ss == bestSs && waste == bestWaste && i < bestIdx))
                        {
                            bestIdx = i;
                            bestPlaced = cand;
                            bestRank = rank; bestFit = fit; bestSs = ss; bestWaste = waste;
                        }
                    }

                    if (bestPlaced == null) break;
                    Commit(state, bestPlaced, margins, split);
                    remaining.RemoveAt(bestIdx);
                    progressed = true;
                }
            }

            ConsolidateAll(sheets, margins, rotation, split, consolidationSweeps);
            return FinishFromSheets(sheets, unplaced, rotation);
        }

        /// <summary>
        /// Seed sheets with 3-across tall walls (~2130 mm), then fill
        /// leftovers and pack the rest with sheet-fill.
        /// </summary>
        private static OptimizationResult RunWallSeed(
            IReadOnlyList<CutPiece> pieces, IReadOnlyList<StockSize> stocks,
            MarginProfile margins, RotationPolicy rotation, SplitMode split,
            int consolidationSweeps = int.MaxValue)
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
                if (!PlaceOnOpenSheets(sheets, piece, margins, rotation, split))
                    leftover.Add(piece);
            }

            foreach (var piece in leftover.ToList())
            {
                if (OpenNewSheet(sheets, per, stocks, piece, margins, rotation, split,
                        new OptimizationResult { Rotation = rotation }))
                    leftover.Remove(piece);
            }

            ConsolidateAll(sheets, margins, rotation, split, consolidationSweeps);
            return FinishFromSheets(sheets, leftover, rotation);
        }

        /// <summary>
        /// Non-guillotine MaxRects BSSF with kerf inflation. Kept as a
        /// candidate only — Verify still enforces kerf + margins.
        /// </summary>
        private static OptimizationResult RunMaxRects(
            IReadOnlyList<CutPiece> ordered, IReadOnlyList<StockSize> stocks,
            MarginProfile margins, RotationPolicy rotation)
        {
            int kerf = margins.KerfMm;
            var remaining = ordered.ToList();
            var sheets = new List<SheetState>();
            var per = new int[stocks.Count];
            var unplaced = new List<CutPiece>();

            foreach (var piece in remaining)
            {
                if (MaxRectsPlace(sheets, piece, rotation, kerf)) continue;

                var state = OpenBlank(stocks, per, piece, margins, rotation, sheets.Count);
                if (state == null) { unplaced.Add(piece); continue; }
                sheets.Add(state);
                if (!MaxRectsPlace(sheets, piece, rotation, kerf))
                    unplaced.Add(piece);
            }

            // Rebuild guillotine free-rects is not required; MaxRects
            // placements live on Layout.Pieces. Consolidation uses
            // TryPlace (guillotine) so skip it here — just sort.
            return FinishFromSheets(sheets, unplaced, rotation);
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
                            best = (si, new PlacedPiece
                            {
                                Piece = piece, X = r.X, Y = r.Y,
                                WidthMm = w, HeightMm = h, Rotated = rot
                            }, waste, rank);
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
                if (p.X > r.X) next.Add(new FreeRect(r.X, r.Y, p.X - r.X, r.H));
                if (p.X + pw < r.X + r.W) next.Add(new FreeRect(p.X + pw, r.Y, r.X + r.W - (p.X + pw), r.H));
                if (p.Y > r.Y) next.Add(new FreeRect(r.X, r.Y, r.W, p.Y - r.Y));
                if (p.Y + ph < r.Y + r.H) next.Add(new FreeRect(r.X, p.Y + ph, r.W, r.Y + r.H - (p.Y + ph)));
            }
            PruneFree(next);
            target.Free.Clear();
            target.Free.AddRange(next);
            target.Layout.Pieces.Add(p);
            // This is the fourth and final mutation site for a sheet's Free /
            // Pieces lists. Refreshing here keeps MaxFreeW / MaxFreeH /
            // PlacedArea exact, which the TryPlace prune and the fullest-first
            // sort both depend on for result-identical behaviour.
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
    }
}
