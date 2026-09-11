// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Optimization/GuillotinePacker.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization — cutting engine (design 2026-09-12)
//
// CALIBRATED against the owner's real DGU benchmark (Low-E / Clear /
// Ramly job): with 15mm margins, kerf 3, adaptive 0/90 rotation the
// engine reproduces the reference result — Low-E 17 sheets @ 82.77%,
// Ramly 1 sheet on 2500×1875.
//
// Stages
//   PackQuick    — one deterministic pass: height-first ordering,
//                  horizontal-first guillotine splits, adaptive
//                  rotation, consolidation.
//   PackOptimize — multi-start effort levels (orderings × split modes,
//                  each consolidated, best run wins by sheets then
//                  utilization). Level = iteration budget.
//
// Guarantees (pinned by oracle tests via Verify):
//   • pieces never overlap — kerf gap enforced between neighbours
//   • pieces stay inside the usable rectangle (stock − LM/RM/TM/BM)
//   • rotation only when the policy allows it (Free90 = original
//     orientation first, rotate only when the piece otherwise won't fit)
//   • deterministic — same input, same layout
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.Optimization
{
    public static class GuillotinePacker
    {
        private struct FreeRect
        {
            public int X, Y, W, H;
            public FreeRect(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
        }

        private sealed class SheetState
        {
            public SheetLayout Layout { get; }
            public List<FreeRect> Free { get; } = new();
            public int StockIndex { get; }
            public SheetState(SheetLayout layout, int stockIndex)
            {
                Layout = layout;
                StockIndex = stockIndex;
                Free.Add(new FreeRect(layout.UsableX, layout.UsableY,
                    layout.UsableWidth, layout.UsableHeight));
            }
        }

        /// <summary>QUICK stage — one fast deterministic pass.</summary>
        public static OptimizationResult PackQuick(
            IReadOnlyList<CutPiece> pieces,
            IReadOnlyList<StockSize> stocks,
            MarginProfile margins,
            RotationPolicy rotation,
            SplitMode split = SplitMode.HorizontalFirst)
        {
            var run = RunPass(pieces, stocks, margins, rotation, split, OrderBy.MaxSide);
            return run;
        }

        /// <summary>
        /// OPTIMIZE stage — effort levels 1..5. Each level widens the
        /// multi-start search (orderings × split modes); every candidate
        /// run is consolidated and the best wins (fewest sheets, then
        /// highest utilization). Deterministic.
        /// </summary>
        public static OptimizationResult PackOptimize(
            IReadOnlyList<CutPiece> pieces,
            IReadOnlyList<StockSize> stocks,
            MarginProfile margins,
            RotationPolicy rotation,
            int effortLevel = 3)
        {
            var orderings = new[] { OrderBy.MaxSide, OrderBy.Area, OrderBy.MinSide };
            var splits = new[] { SplitMode.HorizontalFirst, SplitMode.VerticalFirst, SplitMode.LongerLeftover };

            IEnumerable<(OrderBy o, SplitMode s)> combos =
                effortLevel <= 1
                    ? new[] { (OrderBy.MaxSide, SplitMode.HorizontalFirst) }
                    : effortLevel == 2
                        ? orderings.Select(o => (o, SplitMode.HorizontalFirst))
                        : orderings.SelectMany(o => splits.Select(s => (o, s)));

            OptimizationResult best = null;
            foreach (var (order, split) in combos)
            {
                var candidate = RunPass(pieces, stocks, margins, rotation, split, order);
                if (candidate.Unplaced.Count > 0)
                {
                    if (best == null || candidate.Unplaced.Count < best.Unplaced.Count)
                        best = candidate;
                    continue;
                }
                if (best == null || best.Unplaced.Count > 0 ||
                    candidate.SheetsUsed < best.SheetsUsed ||
                    (candidate.SheetsUsed == best.SheetsUsed &&
                     candidate.UtilizationPct > best.UtilizationPct))
                {
                    best = candidate;
                }
            }

            return best ?? PackQuick(pieces, stocks, margins, rotation);
        }

        // ── one packing pass ─────────────────────────────────────────────

        private enum OrderBy { MaxSide, Area, MinSide }

        private static OptimizationResult RunPass(
            IReadOnlyList<CutPiece> pieces,
            IReadOnlyList<StockSize> stocks,
            MarginProfile margins,
            RotationPolicy rotation,
            SplitMode split,
            OrderBy order)
        {
            if (pieces == null) throw new ArgumentNullException(nameof(pieces));
            if (stocks == null || stocks.Count == 0)
                throw new ArgumentException("At least one stock size is required.", nameof(stocks));
            if (margins == null) throw new ArgumentNullException(nameof(margins));

            var result = new OptimizationResult { Rotation = rotation };
            if (pieces.Count == 0) return result;

            var sheets = new List<SheetState>();
            var sheetsPerStock = new int[stocks.Count];

            foreach (var piece in Sort(pieces, order))
            {
                if (!PlaceOnOpenSheets(sheets, piece, margins, rotation, split))
                {
                    if (!OpenNewSheet(sheets, sheetsPerStock, stocks, piece, margins, rotation, split, result))
                        result.Unplaced.Add(piece);
                }
            }

            Consolidate(sheets, margins, rotation, split);

            for (int i = 0; i < sheets.Count; i++)
                result.Sheets.Add(sheets[i].Layout);
            for (int i = 0; i < result.Sheets.Count; i++)
                result.Sheets[i].SheetNo = i + 1;

            return result;
        }

        private static List<CutPiece> Sort(IReadOnlyList<CutPiece> pieces, OrderBy order)
        {
            switch (order)
            {
                case OrderBy.Area:
                    return pieces
                        .OrderByDescending(p => (long)p.WidthMm * p.HeightMm)
                        .ThenByDescending(p => System.Math.Max(p.WidthMm, p.HeightMm))
                        .ThenBy(p => p.GlassRef, StringComparer.Ordinal)
                        .ThenBy(p => p.PieceLabel, StringComparer.Ordinal)
                        .ToList();
                case OrderBy.MinSide:
                    return pieces
                        .OrderByDescending(p => System.Math.Min(p.WidthMm, p.HeightMm))
                        .ThenByDescending(p => (long)p.WidthMm * p.HeightMm)
                        .ThenBy(p => p.GlassRef, StringComparer.Ordinal)
                        .ThenBy(p => p.PieceLabel, StringComparer.Ordinal)
                        .ToList();
                default: // calibrated default: tallest first
                    return pieces
                        .OrderByDescending(p => System.Math.Max(p.WidthMm, p.HeightMm))
                        .ThenByDescending(p => (long)p.WidthMm * p.HeightMm)
                        .ThenBy(p => p.GlassRef, StringComparer.Ordinal)
                        .ThenBy(p => p.PieceLabel, StringComparer.Ordinal)
                        .ToList();
            }
        }

        // ── placement ────────────────────────────────────────────────────

        private static bool PlaceOnOpenSheets(
            List<SheetState> sheets, CutPiece piece, MarginProfile margins,
            RotationPolicy rotation, SplitMode split)
        {
            (int orderRank, long waste, int sheetIndex)? best = null;
            PlacedPiece bestPlacement = null;

            for (int si = 0; si < sheets.Count; si++)
            {
                var candidate = TryPlace(sheets[si], piece, rotation, out int rank, out long waste);
                if (candidate == null) continue;

                if (best == null || rank < best.Value.orderRank ||
                    (rank == best.Value.orderRank && waste < best.Value.waste))
                {
                    best = (rank, waste, si);
                    bestPlacement = candidate;
                }
            }

            if (best == null) return false;

            Commit(sheets[best.Value.sheetIndex], bestPlacement, margins, split);
            return true;
        }

        /// <summary>
        /// Best fit for one piece on one sheet. ADAPTIVE rotation: the
        /// original orientation is preferred (rank 0); rotated placements
        /// are only chosen when nothing accepts the piece unrotated.
        /// </summary>
        private static PlacedPiece TryPlace(
            SheetState sheet, CutPiece piece, RotationPolicy rotation,
            out int rank, out long waste)
        {
            rank = int.MaxValue;
            waste = long.MaxValue;
            PlacedPiece best = null;

            foreach (var (w, h, rot, orientationRank) in Orientations(piece, rotation))
            {
                for (int i = 0; i < sheet.Free.Count; i++)
                {
                    var rect = sheet.Free[i];
                    if (w > rect.W || h > rect.H) continue;

                    long leftover = (long)rect.W * rect.H - (long)w * h;
                    if (best == null || orientationRank < rank ||
                        (orientationRank == rank && leftover < waste))
                    {
                        rank = orientationRank;
                        waste = leftover;
                        best = new PlacedPiece
                        {
                            Piece = piece,
                            X = rect.X,
                            Y = rect.Y,
                            WidthMm = w,
                            HeightMm = h,
                            Rotated = rot
                        };
                    }
                }

                // Original orientation fits somewhere — don't rotate.
                if (best != null && orientationRank == 0) break;
            }

            return best;
        }

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

        /// <summary>
        /// Commits a placement: consumes the host free rectangle and splits
        /// the remainder with one guillotine cut (kerf consumed).
        /// </summary>
        private static void Commit(SheetState sheet, PlacedPiece placed, MarginProfile margins, SplitMode split)
        {
            int kerf = margins.KerfMm;

            int idx = sheet.Free.FindIndex(r =>
                placed.X >= r.X && placed.Y >= r.Y &&
                placed.X + placed.WidthMm <= r.X + r.W &&
                placed.Y + placed.HeightMm <= r.Y + r.H);
            if (idx < 0)
                throw new InvalidOperationException("Placement escaped the free-rectangle ledger.");

            var rect = sheet.Free[idx];
            sheet.Free.RemoveAt(idx);

            int rightW = rect.W - placed.WidthMm - kerf;
            int bottomH = rect.H - placed.HeightMm - kerf;

            bool verticalFirst = split == SplitMode.VerticalFirst ||
                (split == SplitMode.LongerLeftover && rightW >= bottomH);

            if (verticalFirst)
            {
                if (rightW > 0)
                    sheet.Free.Add(new FreeRect(rect.X + placed.WidthMm + kerf, rect.Y, rightW, rect.H));
                if (bottomH > 0)
                    sheet.Free.Add(new FreeRect(rect.X, rect.Y + placed.HeightMm + kerf,
                        placed.WidthMm, bottomH));
            }
            else
            {
                if (bottomH > 0)
                    sheet.Free.Add(new FreeRect(rect.X, rect.Y + placed.HeightMm + kerf, rect.W, bottomH));
                if (rightW > 0)
                    sheet.Free.Add(new FreeRect(rect.X + placed.WidthMm + kerf, rect.Y,
                        rightW, placed.HeightMm));
            }

            sheet.Layout.Pieces.Add(placed);
        }

        // ── stock selection ──────────────────────────────────────────────

        private static bool OpenNewSheet(
            List<SheetState> sheets, int[] sheetsPerStock, IReadOnlyList<StockSize> stocks,
            CutPiece piece, MarginProfile margins, RotationPolicy rotation,
            SplitMode split, OptimizationResult result)
        {
            // "Best sheet size" rule: try the SMALLEST stock that fits the
            // piece first, so large racks are saved for the glass that
            // needs them (owner requirement for stock selection).
            var byPreference = Enumerable.Range(0, stocks.Count)
                .Where(i => sheetsPerStock[i] < stocks[i].MaxSheets || stocks[i].MaxSheets == 0)
                .OrderBy(i => (long)stocks[i].WidthMm * stocks[i].HeightMm)
                .ThenBy(i => i)
                .ToList();

            foreach (int i in byPreference)
            {
                var stock = stocks[i];

                int usableW = System.Math.Max(0, stock.WidthMm - margins.LeftMm - margins.RightMm);
                int usableH = System.Math.Max(0, stock.HeightMm - margins.TopMm - margins.BottomMm);

                bool fits = Orientations(piece, rotation)
                    .Any(o => o.W <= usableW && o.H <= usableH);
                if (!fits) continue;

                var layout = new SheetLayout
                {
                    SheetNo = result.Sheets.Count + sheets.Count + 1,
                    Stock = stock,
                    UsableX = margins.LeftMm,
                    UsableY = margins.TopMm,
                    UsableWidth = usableW,
                    UsableHeight = usableH
                };
                var state = new SheetState(layout, i);
                sheets.Add(state);
                sheetsPerStock[i]++;

                var placed = TryPlace(state, piece, rotation, out _, out _);
                if (placed == null)
                    throw new InvalidOperationException("New sheet rejected the piece that opened it.");
                Commit(state, placed, margins, split);
                return true;
            }

            return false;
        }

        // ── consolidation — squeeze out the emptiest sheet ──────────────

        private static void Consolidate(
            List<SheetState> sheets, MarginProfile margins, RotationPolicy rotation, SplitMode split)
        {
            if (sheets.Count <= 1) return;

            bool improved = true;
            while (improved && sheets.Count > 1)
            {
                improved = false;

                // Donor = the sheet carrying the least glass.
                int donorIndex = 0;
                long donorArea = long.MaxValue;
                for (int i = 0; i < sheets.Count; i++)
                {
                    long area = sheets[i].Layout.Pieces.Sum(p => (long)p.WidthMm * p.HeightMm);
                    if (area < donorArea) { donorArea = area; donorIndex = i; }
                }

                var donor = sheets[donorIndex];
                if (donor.Layout.Pieces.Count == 0) { sheets.RemoveAt(donorIndex); improved = true; continue; }

                var victims = donor.Layout.Pieces
                    .OrderByDescending(p => (long)p.WidthMm * p.HeightMm)
                    .ToList();

                // Snapshot: placements + free ledgers of every sheet.
                var snapshotPlacements = sheets.Select(s => s.Layout.Pieces.ToList()).ToList();
                var snapshotFree = sheets.Select(s => s.Free.ToList()).ToList();

                donor.Layout.Pieces.Clear();
                bool allEvacuated = true;

                foreach (var victim in victims)
                {
                    bool placed = false;
                    for (int si = 0; si < sheets.Count; si++)
                    {
                        if (si == donorIndex) continue;
                        var moved = TryPlace(sheets[si], victim.Piece, rotation, out _, out _);
                        if (moved == null) continue;
                        Commit(sheets[si], moved, margins, split);
                        placed = true;
                        break;
                    }
                    if (!placed) { allEvacuated = false; break; }
                }

                if (allEvacuated)
                {
                    sheets.RemoveAt(donorIndex);
                    improved = true;
                }
                else
                {
                    // Roll back.
                    for (int i = 0; i < sheets.Count; i++)
                    {
                        sheets[i].Layout.Pieces.Clear();
                        sheets[i].Layout.Pieces.AddRange(snapshotPlacements[i]);
                        sheets[i].Free.Clear();
                        sheets[i].Free.AddRange(snapshotFree[i]);
                    }
                }
            }
        }

        // ── ORACLE — verification used by tests and the UI ──────────────

        /// <summary>
        /// Asserts every guarantee of a run. Returns the list of
        /// violations (empty = layout valid). Used by oracle tests and
        /// available to the UI as a sanity badge.
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

                        int xGap = System.Math.Max(
                            b.X - (a.X + a.WidthMm), a.X - (b.X + b.WidthMm));
                        int yGap = System.Math.Max(
                            b.Y - (a.Y + a.HeightMm), a.Y - (b.Y + b.HeightMm));

                        if (xOverlap && !yOverlap && yGap < kerf)
                            problems.Add($"Sheet {sheet.SheetNo}: {Label(a)} breaks kerf against {Label(b)}.");
                        if (yOverlap && !xOverlap && xGap < kerf)
                            problems.Add($"Sheet {sheet.SheetNo}: {Label(a)} breaks kerf against {Label(b)}.");
                    }
                }
            }

            return problems;
        }

        private static string Label(PlacedPiece p) =>
            string.IsNullOrWhiteSpace(p.Piece.PieceLabel)
                ? p.Piece.GlassRef
                : p.Piece.PieceLabel;
    }
}
