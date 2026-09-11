// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Optimization/GuillotinePacker.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization — QUICK stage engine (design 2026-09-12)
//
// Guillotine shelf packer: every placement splits the remaining free
// space with one full straight cut — exactly what a cutting table does.
// Guarantees (pinned by oracle tests):
//   • pieces never overlap — kerf gap enforced between neighbours
//   • pieces stay inside the usable rectangle (stock minus LM/RM/TM/BM)
//   • rotation only when the policy and the piece allow it
//   • deterministic — same input, same layout
//
// The deeper OPTIMIZE stage (multi-start orderings, split variants,
// consolidation, levels 1–5) builds on this packer.
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

        private sealed class OpenSheet
        {
            public SheetLayout Layout { get; }
            public List<FreeRect> Free { get; } = new();
            public int StockIndex { get; }
            public OpenSheet(SheetLayout layout, int stockIndex, FreeRect usable)
            {
                Layout = layout;
                StockIndex = stockIndex;
                Free.Add(usable);
            }
        }

        /// <summary>
        /// QUICK stage: packs pieces onto the stock pool in one fast
        /// deterministic pass. All pieces must share the margin profile's
        /// thickness/color group (the smart picker guarantees that).
        /// </summary>
        public static OptimizationResult PackQuick(
            IReadOnlyList<CutPiece> pieces,
            IReadOnlyList<StockSize> stocks,
            MarginProfile margins,
            RotationPolicy rotation)
        {
            if (pieces == null) throw new ArgumentNullException(nameof(pieces));
            if (stocks == null || stocks.Count == 0)
                throw new ArgumentException("At least one stock size is required.", nameof(stocks));
            if (margins == null) throw new ArgumentNullException(nameof(margins));

            var result = new OptimizationResult { Rotation = rotation };
            if (pieces.Count == 0) return result;

            var sheets = new List<OpenSheet>();
            var sheetsPerStock = new int[stocks.Count];

            // Big pieces first — the classic FFD height-first ordering
            // keeps shelves tight and deterministic.
            var ordered = pieces
                .OrderByDescending(p => System.Math.Max(p.HeightMm, p.WidthMm))
                .ThenByDescending(p => (long)p.WidthMm * p.HeightMm)
                .ThenBy(p => p.GlassRef, StringComparer.Ordinal)
                .ThenBy(p => p.PieceLabel, StringComparer.Ordinal)
                .ToList();

            foreach (var piece in ordered)
            {
                var placement = PlaceOnOpenSheets(sheets, piece, margins, rotation);

                if (placement == null)
                {
                    int stockIndex = OpenSheetFor(piece, stocks, margins, rotation, sheetsPerStock);
                    if (stockIndex < 0)
                    {
                        result.Unplaced.Add(piece);
                        continue;
                    }

                    sheetsPerStock[stockIndex]++;
                    var sheet = CreateSheet(stocks[stockIndex], stockIndex, margins, result.Sheets.Count + 1);
                    var open = new OpenSheet(sheet, stockIndex, new FreeRect(
                        sheet.UsableX, sheet.UsableY, sheet.UsableWidth, sheet.UsableHeight));
                    sheets.Add(open);

                    placement = PlaceOnSheet(open, piece, margins, rotation);
                    if (placement == null)
                    {
                        // Stock too small even alone — report honestly.
                        sheets.RemoveAt(sheets.Count - 1);
                        sheetsPerStock[stockIndex]--;
                        result.Sheets.RemoveAt(result.Sheets.Count - 1);
                        result.Unplaced.Add(piece);
                        continue;
                    }
                }

                placement.Value.sheet.Layout.Pieces.Add(placement.Value.placed);
            }

            // Compact sheet numbers (open order is stable).
            for (int i = 0; i < result.Sheets.Count; i++)
                result.Sheets[i].SheetNo = i + 1;

            return result;
        }

        // ── placement ────────────────────────────────────────────────────

        private static (OpenSheet sheet, PlacedPiece placed)? PlaceOnOpenSheets(
            List<OpenSheet> sheets, CutPiece piece, MarginProfile margins, RotationPolicy rotation)
        {
            (OpenSheet sheet, PlacedPiece placed, long waste)? best = null;

            foreach (var sheet in sheets)
            {
                var candidate = TryPlace(sheet, piece, margins, rotation, out long waste);
                if (candidate == null) continue;

                // Best fit across the open sheets: smallest leftover.
                if (best == null || waste < best.Value.waste)
                    best = (sheet, candidate, waste);
            }

            if (best == null) return null;

            var chosen = best.Value;
            ApplyPlacement(chosen.sheet, chosen.placed, margins);
            return (chosen.sheet, chosen.placed);
        }

        private static (OpenSheet sheet, PlacedPiece placed)? PlaceOnSheet(
            OpenSheet sheet, CutPiece piece, MarginProfile margins, RotationPolicy rotation)
        {
            var placed = TryPlace(sheet, piece, margins, rotation, out _);
            if (placed == null) return null;
            ApplyPlacement(sheet, placed, margins);
            return (sheet, placed);
        }

        /// <summary>Best-fit rectangle + orientation for one piece (no mutation).</summary>
        private static PlacedPiece? TryPlace(
            OpenSheet sheet, CutPiece piece, MarginProfile margins,
            RotationPolicy rotation, out long waste)
        {
            waste = long.MaxValue;
            PlacedPiece? best = null;

            var orientations = Orientations(piece, rotation);

            for (int i = 0; i < sheet.Free.Count; i++)
            {
                var rect = sheet.Free[i];
                foreach (var (w, h, rotated) in orientations)
                {
                    if (w > rect.W || h > rect.H) continue;

                    long leftover = (long)rect.W * rect.H - (long)w * h;
                    if (best == null || leftover < waste)
                    {
                        waste = leftover;
                        best = new PlacedPiece
                        {
                            Piece = piece,
                            X = rect.X,
                            Y = rect.Y,
                            WidthMm = w,
                            HeightMm = h,
                            Rotated = rotated
                        };
                    }
                }
            }

            return best;
        }

        private static List<(int W, int H, bool Rotated)> Orientations(
            CutPiece piece, RotationPolicy rotation)
        {
            var list = new List<(int, int, bool)> { (piece.WidthMm, piece.HeightMm, false) };
            if (rotation == RotationPolicy.Free90 && piece.AllowRotate
                && piece.WidthMm != piece.HeightMm)
            {
                list.Add((piece.HeightMm, piece.WidthMm, true));
            }
            return list;
        }

        /// <summary>
        /// Commits a placement: consumes the free rectangle and splits the
        /// remainder with one guillotine cut (kerf consumed on both new
        /// edges). Split follows the longer leftover side.
        /// </summary>
        private static void ApplyPlacement(OpenSheet sheet, PlacedPiece placed, MarginProfile margins)
        {
            int kerf = margins.KerfMm;

            // Find and consume the rectangle containing the placement.
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

            if (rightW >= bottomH)
            {
                // Vertical split: right remainder keeps full height.
                if (rightW > 0)
                    sheet.Free.Add(new FreeRect(rect.X + placed.WidthMm + kerf, rect.Y, rightW, rect.H));
                if (bottomH > 0)
                    sheet.Free.Add(new FreeRect(rect.X, rect.Y + placed.HeightMm + kerf,
                        placed.WidthMm, bottomH));
            }
            else
            {
                // Horizontal split: bottom remainder keeps full width.
                if (bottomH > 0)
                    sheet.Free.Add(new FreeRect(rect.X, rect.Y + placed.HeightMm + kerf, rect.W, bottomH));
                if (rightW > 0)
                    sheet.Free.Add(new FreeRect(rect.X + placed.WidthMm + kerf, rect.Y,
                        rightW, placed.HeightMm));
            }
        }

        // ── stock selection ──────────────────────────────────────────────

        private static int OpenSheetFor(
            CutPiece piece, IReadOnlyList<StockSize> stocks, MarginProfile margins,
            RotationPolicy rotation, int[] sheetsPerStock)
        {
            for (int i = 0; i < stocks.Count; i++)
            {
                var stock = stocks[i];
                if (stock.MaxSheets > 0 && sheetsPerStock[i] >= stock.MaxSheets)
                    continue;

                int usableW = stock.WidthMm - margins.LeftMm - margins.RightMm;
                int usableH = stock.HeightMm - margins.TopMm - margins.BottomMm;

                foreach (var (w, h, _) in Orientations(piece, rotation))
                {
                    if (w <= usableW && h <= usableH) return i;
                }
            }
            return -1;
        }

        private static SheetLayout CreateSheet(
            StockSize stock, int stockIndex, MarginProfile margins, int sheetNo)
        {
            return new SheetLayout
            {
                SheetNo = sheetNo,
                Stock = stock,
                UsableX = margins.LeftMm,
                UsableY = margins.TopMm,
                UsableWidth = System.Math.Max(0, stock.WidthMm - margins.LeftMm - margins.RightMm),
                UsableHeight = System.Math.Max(0, stock.HeightMm - margins.TopMm - margins.BottomMm)
            };
        }

        // ── ORACLE — verification used by tests and the UI ──────────────

        /// <summary>
        /// Asserts every guarantee of a run. Returns the list of
        /// violations (empty = layout is valid). Used by oracle tests and
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

                        // Separation gaps between the two rectangles.
                        int xGap = System.Math.Max(
                            b.X - (a.X + a.WidthMm), a.X - (b.X + b.WidthMm));
                        int yGap = System.Math.Max(
                            b.Y - (a.Y + a.HeightMm), a.Y - (b.Y + b.HeightMm));

                        // Pieces sharing an X band need a blade path (kerf)
                        // between them in Y — and vice versa.
                        if (xOverlap && !yOverlap && yGap < kerf)
                            problems.Add($"Sheet {sheet.SheetNo}: {Label(a)} breaks kerf against {Label(b)}.");
                        if (yOverlap && !xOverlap && xGap < kerf)
                            problems.Add($"Sheet {sheet.SheetNo}: {Label(a)} breaks kerf against {Label(b)}.");
                    }
                }
            }

            if (result.Unplaced.Count > 0)
                problems.Add($"{result.Unplaced.Count} piece(s) could not be placed.");

            return problems;
        }

        private static string Label(PlacedPiece p) =>
            string.IsNullOrWhiteSpace(p.Piece.PieceLabel)
                ? p.Piece.GlassRef
                : p.Piece.PieceLabel;
    }
}
